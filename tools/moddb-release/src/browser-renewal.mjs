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

// Best effort, as the spec says: a lingering Chrome lock file must not turn a
// successful capture into a failure. One retry, then silence.
export async function removeProfileDir(dir, { rm = fs.rmSync, delay = 500 } = {}) {
  const options = { recursive: true, force: true, maxRetries: 5, retryDelay: 200 };
  try {
    rm(dir, options);
  } catch {
    await new Promise((resolve) => setTimeout(resolve, delay));
    try {
      rm(dir, options);
    } catch {
      // still locked; the directory holds no credentials and the OS temp cleanup will take it
    }
  }
}

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

// Resolves true once the main frame has landed on the ModDB origin past its
// /login bridge, false at the deadline or once the context is gone. An
// off-origin navigation rejects.
async function waitForBridgeLanding({ remaining, landed, offOriginNavigated, isClosed }) {
  const deadline = Date.now() + remaining();
  while (Date.now() < deadline && !isClosed()) {
    if (offOriginNavigated()) throw fail("RENEWAL_ORIGIN_MISMATCH");
    if (landed()) return true;
    await new Promise((resolve) => setTimeout(resolve, POLL_MS));
  }
  return false;
}

export async function renewInBrowser({ accountLogin, browserConfig = DEFAULT_BROWSER_CONFIG, onHumanActionRequired = () => {}, timeoutMs = 600_000 }) {
  const { accountOrigin, modDbOrigin, loginPath, channel, allowedOrigins } = { ...DEFAULT_BROWSER_CONFIG, ...browserConfig };
  const allowed = new Set(allowedOrigins);
  const hosts = [new URL(accountOrigin).hostname, new URL(modDbOrigin).hostname];
  const deadline = Date.now() + timeoutMs; // one budget for launch, load, human, and capture
  const remaining = () => Math.max(1, deadline - Date.now());
  const profileDir = createProfileDir();
  let context = null;
  let closed = false;
  let interrupted = false;
  let offOrigin = false;
  let humanStep = false; // navigations before the human step are the login page itself
  let landed = false; // main frame committed on modDbOrigin, not on its /login bridge
  // Playwright's own SIGINT handler would exit the process before the
  // profile is removed, so Ctrl-C is handled here for the duration: close
  // the context, and let the normal cancellation and cleanup paths run.
  const onSigint = () => {
    interrupted = true;
    context?.close().catch(() => {});
  };
  process.once("SIGINT", onSigint);

  try {
    try {
      context = await chromium.launchPersistentContext(profileDir, { channel, headless: false, acceptDownloads: false, handleSIGINT: false });
    } catch {
      throw fail("RENEWAL_BROWSER_FAILED");
    }
    context.once("close", () => {
      closed = true;
    });
    if (interrupted) throw fail("RENEWAL_CANCELLED");

    await context.route("**/*", (route) => {
      const request = route.request();
      try {
        if (allowed.has(new URL(request.url()).origin)) return route.continue().catch(() => {});
        if (request.isNavigationRequest() && request.frame().parentFrame() === null) offOrigin = true;
      } catch {
        // unparseable or frameless request: treat as disallowed
      }
      return route.abort("blockedbyclient").catch(() => {}); // the context may already be closing
    });

    // Redirect hops bypass route(), so the main frame's committed origin is
    // watched as well; an off-origin landing fails the renewal.
    const page = context.pages()[0] ?? (await context.newPage());
    page.on("framenavigated", (frame) => {
      if (frame !== page.mainFrame()) return;
      const { origin, pathname } = new URL(frame.url());
      if (!allowed.has(origin)) offOrigin = true;
      else if (humanStep && origin === modDbOrigin && pathname !== "/login") landed = true;
    });
    await page.goto(accountOrigin + loginPath, { waitUntil: "domcontentloaded", timeout: remaining() });
    if (new URL(page.url()).origin !== accountOrigin) throw fail("RENEWAL_ORIGIN_MISMATCH");

    await page.fill('input[name="email"]', accountLogin.email, { timeout: remaining() });
    await page.fill('input[name="password"]', accountLogin.password, { timeout: remaining() });
    const redirect = page.locator('input[name="loginredir"]');
    if ((await redirect.count()) > 0) await redirect.first().evaluate((el) => (el.value = "mods"), undefined, { timeout: remaining() });

    humanStep = true;
    onHumanActionRequired();
    const cookie = await waitForSessionCookie(context, { hosts, remaining, offOriginNavigated: () => offOrigin });
    // The cookie lands on the login response, but ModDB only accepts it once
    // the redirect has reached its /login bridge, so the window stays open
    // until the main frame lands past it. The deadline still returns the
    // cookie: the client's completeLoginBridge covers the rest.
    await waitForBridgeLanding({ remaining, landed: () => landed, offOriginNavigated: () => offOrigin, isClosed: () => closed });
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
    process.off("SIGINT", onSigint);
    try {
      await context?.close();
    } catch {
      // already closed or crashed; the profile removal below still runs
    }
    await removeProfileDir(profileDir);
  }
}
