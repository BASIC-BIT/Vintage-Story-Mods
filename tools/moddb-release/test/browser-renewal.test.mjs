// Drives the real browser boundary against the fake account server with the
// bundled Chromium. A headed window opens briefly per test. Test origins are
// injected through browserConfig; the production CLI exposes no override.
import assert from "node:assert/strict";
import { readdirSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test, { after, afterEach, beforeEach } from "node:test";

import { SESSION_COOKIE_NAME } from "../src/config.mjs";
import { BrokerError } from "../src/contracts.mjs";
import { renewInBrowser } from "../src/browser-renewal.mjs";
import { startFakeAccountServer } from "./support/fake-account-server.mjs";
import { startFakeModDb } from "./support/fake-moddb.mjs";

const PASSWORD = "fixture-password-never-print";
const COOKIE = "fixture-cookie-never-print";
const EMAIL = "fixture-email@example.invalid";
const ACCOUNT = "BASICBIT";
const accountLogin = { schemaVersion: 1, email: EMAIL, password: PASSWORD };

const thrown = [];
const processOutput = [];
for (const stream of [process.stdout, process.stderr]) {
  const original = stream.write.bind(stream);
  stream.write = (chunk, ...rest) => {
    processOutput.push(String(chunk));
    return original(chunk, ...rest);
  };
}

const profileDirs = () => readdirSync(os.tmpdir()).filter((name) => name.startsWith("moddb-renewal-"));
const walk = (dir) => readdirSync(dir, { recursive: true }).map(String);
const CAPTURE_FILE = /(^|[\\/])trace[^\\/]*$|\.(har|webm|png)$/i;

let fake;
let before;
let seenProfileDirs;
beforeEach(async () => {
  fake = await startFakeAccountServer({ email: EMAIL, password: PASSWORD, cookieValue: COOKIE, accountName: ACCOUNT });
  before = profileDirs();
  seenProfileDirs = [];
});
afterEach(async () => {
  await fake.close();
  assert.deepEqual(profileDirs(), before, "disposable profile survived cleanup");
  assert.ok(seenProfileDirs.length > 0, "the driver never created a profile directory");
  for (const request of fake.requests.filter((r) => r.server !== "account")) {
    assert.equal(JSON.stringify(request).includes(PASSWORD), false, "password reached another origin");
  }
});
after(() => {
  for (const error of thrown) {
    const dump = [String(error), error.stack, JSON.stringify(error), JSON.stringify(Object.getOwnPropertyNames(error).map((k) => String(error[k])))].join("\n");
    for (const fixture of [PASSWORD, COOKIE]) assert.equal(dump.includes(fixture), false, `${fixture} leaked through an error`);
  }
  const output = processOutput.join("");
  for (const fixture of [PASSWORD, COOKIE]) assert.equal(output.includes(fixture), false, `${fixture} reached process output`);
});

const config = (loginPath = "/?autohuman=300") => ({
  accountOrigin: fake.origin,
  modDbOrigin: fake.origin,
  loginPath,
  channel: "chromium",
  allowedOrigins: [fake.origin],
});

// Test-only hook: inspect the profile directory right before the driver removes it.
const onBeforeCleanup = (profileDir) => {
  seenProfileDirs.push(profileDir);
  assert.equal(path.dirname(profileDir), os.tmpdir());
  assert.ok(path.basename(profileDir).startsWith("moddb-renewal-"));
  const captures = walk(profileDir).filter((entry) => CAPTURE_FILE.test(entry));
  assert.deepEqual(captures, [], "profile contains capture artifacts");
};

async function rejectsWith(promise, code) {
  let error;
  try {
    await promise;
  } catch (caught) {
    error = caught;
  }
  thrown.push(error);
  assert.ok(error instanceof BrokerError, `expected BrokerError, got ${error?.name}`);
  assert.equal(error.code, code);
  return error;
}

test("fills credentials only on the verified origin, waits for the human, and returns only the named cookie", async () => {
  fake.state.cookieMaxAge = 3600;
  const startedAt = Date.now();
  let humanPrompted = 0;
  const result = await renewInBrowser({
    accountLogin,
    expectedAccount: ACCOUNT,
    browserConfig: config(),
    onHumanActionRequired: () => humanPrompted++,
    onBeforeCleanup,
    timeoutMs: 30_000,
  });

  assert.equal(humanPrompted, 1);
  assert.deepEqual(Object.keys(result), ["cookieName", "cookieValue", "observedCookieExpiresAt"]);
  assert.equal(result.cookieName, SESSION_COOKIE_NAME);
  assert.equal(result.cookieValue, COOKIE);
  const expires = Date.parse(result.observedCookieExpiresAt);
  assert.ok(expires >= startedAt + 3_500_000 && expires <= Date.now() + 3_600_000, "expiry is not roughly one hour out");
  assert.equal(JSON.stringify(result).includes("decoy"), false, "a decoy cookie was captured");

  const logins = fake.requests.filter((r) => r.path === "/attemptlogin");
  assert.equal(logins.length, 1);
  assert.equal(logins[0].fields.email, EMAIL);
  assert.equal(logins[0].fields.password, PASSWORD);
  assert.equal(logins[0].fields.loginredir, "mods");
  assert.equal(logins[0].fields.humandone, "1");
  assert.equal(logins[0].fields.filledBeforeHuman, "1", "credentials were not in place before the human step");
  assert.equal(fake.requests.some((r) => r.server === "decoy"), false);
});

// ModDB registers the cookie only when the account login's redirect reaches
// its /login bridge, so the window must stay open until that redirect lands
// somewhere else on the ModDB origin. The fake bridge redirects late.
test("keeps the browser open until the ModDB login bridge has landed", async () => {
  const moddb = await startFakeModDb({ cookieValue: COOKIE, accountName: ACCOUNT });
  moddb.state.requireBridge = true;
  moddb.state.bridgeDelayMs = 1500;
  fake.state.redirectTo = `${moddb.origin}/login`;
  try {
    const result = await renewInBrowser({
      accountLogin,
      expectedAccount: ACCOUNT,
      browserConfig: { ...config(), modDbOrigin: moddb.origin, allowedOrigins: [fake.origin, moddb.origin] },
      onBeforeCleanup,
      timeoutMs: 30_000,
    });
    assert.equal(result.cookieValue, COOKIE);
    const paths = moddb.requests.map((r) => r.path);
    assert.ok(paths.includes("/login"), "the bridge was never requested");
    assert.ok(paths.includes("/"), "the browser closed before the bridge redirect landed");
    assert.ok(moddb.requests.find((r) => r.path === "/login").headers.cookie.includes(`vs_websessionkey=${COOKIE}`));
    assert.ok(moddb.state.bridged.has(COOKIE));
    assert.equal(JSON.stringify(moddb.requests).includes(PASSWORD), false);
  } finally {
    await moddb.close();
  }
});

test("returns the captured cookie when the bridge never lands before the deadline", async () => {
  const moddb = await startFakeModDb({ cookieValue: COOKIE, accountName: ACCOUNT });
  moddb.state.bridgeStalls = true;
  fake.state.redirectTo = `${moddb.origin}/login`;
  try {
    const result = await renewInBrowser({
      accountLogin,
      expectedAccount: ACCOUNT,
      browserConfig: { ...config(), modDbOrigin: moddb.origin, allowedOrigins: [fake.origin, moddb.origin] },
      onBeforeCleanup,
      timeoutMs: 6_000,
    });
    assert.equal(result.cookieValue, COOKIE);
    const paths = moddb.requests.map((r) => r.path);
    assert.ok(paths.includes("/login"));
    assert.equal(paths.includes("/"), false);
  } finally {
    await moddb.close();
  }
});

test("session cookies without an expiry report null", async () => {
  const result = await renewInBrowser({ accountLogin, expectedAccount: ACCOUNT, browserConfig: config(), onBeforeCleanup, timeoutMs: 30_000 });
  assert.equal(result.observedCookieExpiresAt, null);
});

test("times out while the human never completes and still removes the profile", async () => {
  await rejectsWith(
    renewInBrowser({ accountLogin, expectedAccount: ACCOUNT, browserConfig: config("/"), onBeforeCleanup, timeoutMs: 1_500 }),
    "RENEWAL_TIMEOUT",
  );
  assert.equal(fake.requests.some((r) => r.path === "/attemptlogin"), false);
});

// Playwright cannot intercept redirect hops, so the decoy may see the bare
// redirected GET; what matters is that no credential or cookie travels with it
// and the renewal is rejected.
test("rejects a login that redirects to an unexpected origin and leaks nothing to it", async () => {
  fake.state.redirectTo = `${fake.decoyOrigin}/landing`;
  await rejectsWith(
    renewInBrowser({ accountLogin, expectedAccount: ACCOUNT, browserConfig: config(), onBeforeCleanup, timeoutMs: 30_000 }),
    "RENEWAL_ORIGIN_MISMATCH",
  );
  const decoyRequests = fake.requests.filter((r) => r.server === "decoy");
  assert.equal(decoyRequests.some((r) => r.method !== "GET"), false, "credentials were posted to the decoy");
  assert.equal(JSON.stringify(decoyRequests).includes(COOKIE), false, "the session cookie reached the decoy");
});

test("refuses to fill when the login page is not on the account origin", async () => {
  fake.state.loginRedirectTo = `${fake.decoyOrigin}/login`;
  await rejectsWith(
    renewInBrowser({
      accountLogin,
      expectedAccount: ACCOUNT,
      browserConfig: { ...config(), allowedOrigins: [fake.origin, fake.decoyOrigin] },
      onBeforeCleanup,
      timeoutMs: 30_000,
    }),
    "RENEWAL_ORIGIN_MISMATCH",
  );
  assert.equal(fake.requests.some((r) => r.path === "/attemptlogin"), false, "credentials were submitted");
  assert.equal(fake.requests.some((r) => r.server === "decoy" && r.method === "POST"), false);
});

test("maps a browser that cannot launch to RENEWAL_BROWSER_FAILED", async () => {
  await rejectsWith(
    renewInBrowser({ accountLogin, expectedAccount: ACCOUNT, browserConfig: { ...config(), channel: "no-such-browser-channel" }, onBeforeCleanup, timeoutMs: 30_000 }),
    "RENEWAL_BROWSER_FAILED",
  );
});
