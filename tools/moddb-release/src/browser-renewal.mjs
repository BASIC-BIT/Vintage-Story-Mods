// Human-assisted login through installed Chrome. The credentials exist only
// inside this call: they are typed into the account origin after that origin
// is verified, the human completes reCAPTCHA and submits, and the driver
// waits for the session cookie to appear. The browser runs in a disposable,
// user-restricted profile with no tracing, screenshots, video, HAR, or
// downloads, and the profile is removed on every exit path. Thrown errors
// carry a fixed code only: never page content, URLs, cookies, or Playwright
// messages.
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { chromium } from "playwright";

import { ACCOUNT_ORIGIN, MODDB_ORIGIN, SESSION_COOKIE_NAME } from "./config.mjs";
import { BrokerError } from "./contracts.mjs";

const DEFAULT_BROWSER_CONFIG = Object.freeze({
  accountOrigin: ACCOUNT_ORIGIN,
  modDbOrigin: MODDB_ORIGIN,
  loginPath: "/",
  channel: "chrome",
  allowedOrigins: [ACCOUNT_ORIGIN, MODDB_ORIGIN, "https://www.google.com", "https://www.gstatic.com", "https://www.recaptcha.net"],
});
const POLL_MS = 250;

const fail = (code) => new BrokerError(code, code);

function createProfileDir() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "moddb-renewal-"));
  if (process.platform === "win32" && process.env.USERNAME) {
    try {
      spawnSync("icacls", [dir, "/inheritance:r", "/grant:r", `${process.env.USERNAME}:(OI)(CI)F`], { shell: false, stdio: "ignore" });
    } catch {
      // best effort: the directory is still process-owned and short-lived
    }
  }
  return dir;
}

const cookieDomainMatches = (cookie, hosts) => {
  const domain = cookie.domain.replace(/^\./, "");
  return hosts.some((host) => host === domain || host.endsWith(`.${domain}`));
};

const expiresToIso = (expires) => (typeof expires === "number" && expires > 0 ? new Date(expires * 1000).toISOString() : null);

// Resolves with the raw cookie once it exists on an expected host; rejects on
// an off-origin top-level navigation, the deadline, or the context closing.
function waitForSessionCookie(context, { hosts, remaining, offOriginNavigated }) {
  return new Promise((resolve, reject) => {
    let settled = false;
    const finish = (fn, value) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      fn(value);
    };
    const timer = setTimeout(() => finish(reject, fail("RENEWAL_TIMEOUT")), remaining());
    context.once("close", () => finish(reject, fail("RENEWAL_CANCELLED")));
    const tick = async () => {
      if (settled) return;
      if (offOriginNavigated()) return finish(reject, fail("RENEWAL_ORIGIN_MISMATCH"));
      let cookies;
      try {
        cookies = await context.cookies();
      } catch {
        return finish(reject, fail("RENEWAL_CANCELLED"));
      }
      const cookie = cookies.find((c) => c.name === SESSION_COOKIE_NAME && cookieDomainMatches(c, hosts));
      if (cookie) return finish(resolve, cookie);
      setTimeout(tick, POLL_MS);
    };
    tick();
  });
}

export async function renewInBrowser({
  accountLogin,
  expectedAccount, // reserved for the caller's post-capture validation; the browser never sees it
  browserConfig = DEFAULT_BROWSER_CONFIG,
  onHumanActionRequired = () => {},
  onBeforeCleanup = () => {}, // test-only hook
  timeoutMs = 600_000,
}) {
  void expectedAccount;
  const { accountOrigin, modDbOrigin, loginPath, channel, allowedOrigins } = { ...DEFAULT_BROWSER_CONFIG, ...browserConfig };
  const allowed = new Set(allowedOrigins);
  const hosts = [new URL(accountOrigin).hostname, new URL(modDbOrigin).hostname];
  const deadline = Date.now() + timeoutMs; // one budget for launch, load, human, and capture
  const remaining = () => Math.max(1, deadline - Date.now());
  const profileDir = createProfileDir();
  let context = null;
  let closed = false;
  let offOrigin = false;

  try {
    try {
      context = await chromium.launchPersistentContext(profileDir, { channel, headless: false, acceptDownloads: false });
    } catch {
      throw fail("RENEWAL_BROWSER_FAILED");
    }
    context.once("close", () => {
      closed = true;
    });

    await context.route("**/*", (route) => {
      const request = route.request();
      try {
        if (allowed.has(new URL(request.url()).origin)) return route.continue();
        if (request.isNavigationRequest() && request.frame().parentFrame() === null) offOrigin = true;
      } catch {
        // unparseable or frameless request: treat as disallowed
      }
      return route.abort("blockedbyclient");
    });

    // Redirect hops bypass route(), so the main frame's committed origin is
    // watched as well; an off-origin landing fails the renewal.
    const page = context.pages()[0] ?? (await context.newPage());
    page.on("framenavigated", (frame) => {
      if (frame === page.mainFrame() && !allowed.has(new URL(frame.url()).origin)) offOrigin = true;
    });
    await page.goto(accountOrigin + loginPath, { waitUntil: "domcontentloaded", timeout: remaining() });
    if (new URL(page.url()).origin !== accountOrigin) throw fail("RENEWAL_ORIGIN_MISMATCH");

    await page.fill('input[name="email"]', accountLogin.email, { timeout: remaining() });
    await page.fill('input[name="password"]', accountLogin.password, { timeout: remaining() });
    const redirect = page.locator('input[name="loginredir"]');
    if ((await redirect.count()) > 0) await redirect.first().evaluate((el) => (el.value = "mods"), undefined, { timeout: remaining() });

    onHumanActionRequired();
    const cookie = await waitForSessionCookie(context, { hosts, remaining, offOriginNavigated: () => offOrigin });
    // The cookie lands on the login response; give its redirect a moment to be
    // intercepted before trusting where the browser ended up.
    await new Promise((resolve) => setTimeout(resolve, 2 * POLL_MS));
    if (offOrigin) throw fail("RENEWAL_ORIGIN_MISMATCH");
    const finalOrigin = new URL(page.url()).origin;
    if (finalOrigin !== accountOrigin && finalOrigin !== modDbOrigin) throw fail("RENEWAL_ORIGIN_MISMATCH");

    return { cookieName: cookie.name, cookieValue: cookie.value, observedCookieExpiresAt: expiresToIso(cookie.expires) };
  } catch (error) {
    if (error instanceof BrokerError) throw error;
    if (offOrigin) throw fail("RENEWAL_ORIGIN_MISMATCH");
    if (closed) throw fail("RENEWAL_CANCELLED");
    throw fail(error?.name === "TimeoutError" ? "RENEWAL_TIMEOUT" : "RENEWAL_BROWSER_FAILED");
  } finally {
    try {
      await context?.close();
    } catch {
      // already closed or crashed; the profile removal below still runs
    }
    try {
      onBeforeCleanup(profileDir);
    } finally {
      fs.rmSync(profileDir, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
    }
  }
}
