import assert from "node:assert/strict";
import test, { after } from "node:test";

import { MODDB_SESSION_DAYS, SESSION_COOKIE_NAME } from "../src/config.mjs";
import {
  buildSessionCandidate,
  getEffectiveExpiry,
  isExpired,
  parseAccountLogin,
  parseSession,
} from "../src/session-schema.mjs";

const NEVER_PRINT = ["fixture-cookie-never-print", "fixture-password-never-print"];
const processOutput = [];
for (const stream of [process.stdout, process.stderr]) {
  const original = stream.write.bind(stream);
  stream.write = (chunk, ...rest) => {
    processOutput.push(String(chunk));
    return original(chunk, ...rest);
  };
}

after(() => {
  const output = processOutput.join("");
  for (const fixture of NEVER_PRINT) {
    assert.equal(output.includes(fixture), false, "fixture credential reached process output");
  }
});

function validSession(overrides = {}) {
  return {
    schemaVersion: 1,
    cookieName: SESSION_COOKIE_NAME,
    cookieValue: "fixture-cookie-never-print",
    capturedAt: "2026-09-03T00:00:00.000Z",
    observedCookieExpiresAt: null,
    modDbValidUntilEstimate: "2026-09-17T00:00:00.000Z",
    validatedAt: "2026-09-03T00:00:00.000Z",
    validatedAccount: "basic",
    ...overrides,
  };
}

function validAccountLogin(overrides = {}) {
  return {
    schemaVersion: 1,
    email: "fixture-email@example.invalid",
    password: "fixture-password-never-print",
    ...overrides,
  };
}

function messageOf(fn) {
  try {
    fn();
  } catch (error) {
    return error.message;
  }
  return assert.fail("expected a throw");
}

test("parseSession accepts JSON text or objects and keeps only known fields", () => {
  const session = validSession();
  assert.deepEqual(parseSession(JSON.stringify(session)), session);
  assert.deepEqual(parseSession({ ...session, note: "extra" }), session);
});

test("parseSession requires schema version 1 exactly", () => {
  assert.equal(
    messageOf(() => parseSession(validSession({ schemaVersion: "1" }))),
    "invalid session field: schemaVersion",
  );
  assert.equal(
    messageOf(() => parseSession(validSession({ schemaVersion: 2 }))),
    "invalid session field: schemaVersion",
  );
});

test("parseSession errors name only the offending field", () => {
  for (const field of ["cookieName", "cookieValue", "validatedAccount"]) {
    assert.equal(messageOf(() => parseSession(validSession({ [field]: "  " }))), `invalid session field: ${field}`);
    assert.equal(
      messageOf(() => parseSession(validSession({ [field]: undefined }))),
      `invalid session field: ${field}`,
    );
  }
  for (const field of ["capturedAt", "modDbValidUntilEstimate", "validatedAt"]) {
    assert.equal(messageOf(() => parseSession(validSession({ [field]: "soon" }))), `invalid session field: ${field}`);
    assert.equal(messageOf(() => parseSession(validSession({ [field]: null }))), `invalid session field: ${field}`);
  }
  assert.equal(
    messageOf(() => parseSession(validSession({ observedCookieExpiresAt: "2026-13-40T00:00:00.000Z" }))),
    "invalid session field: observedCookieExpiresAt",
  );
  assert.equal(
    messageOf(() => parseSession(validSession({ observedCookieExpiresAt: undefined }))),
    "invalid session field: observedCookieExpiresAt",
  );
});

test("parseSession never echoes malformed input", () => {
  assert.equal(
    messageOf(() => parseSession('{"cookieValue": "fixture-cookie-never-print"')),
    "invalid session json",
  );
  assert.equal(messageOf(() => parseSession("null")), "invalid session json");
  assert.equal(messageOf(() => parseSession(undefined)), "invalid session json");
});

test("parseAccountLogin mirrors the session rules", () => {
  const login = validAccountLogin();
  assert.deepEqual(parseAccountLogin(JSON.stringify(login)), login);
  assert.equal(
    messageOf(() => parseAccountLogin(validAccountLogin({ password: "" }))),
    "invalid account-login field: password",
  );
  assert.equal(
    messageOf(() => parseAccountLogin(validAccountLogin({ email: 5 }))),
    "invalid account-login field: email",
  );
  assert.equal(
    messageOf(() => parseAccountLogin(validAccountLogin({ schemaVersion: 1.5 }))),
    "invalid account-login field: schemaVersion",
  );
  assert.equal(messageOf(() => parseAccountLogin("{fixture-password-never-print")), "invalid account-login json");
});

test("effective deadline uses the earlier known expiry", () => {
  const session = validSession({
    observedCookieExpiresAt: "2026-09-10T00:00:00.000Z",
    modDbValidUntilEstimate: "2026-09-17T00:00:00.000Z",
  });
  assert.equal(getEffectiveExpiry(session).toISOString(), "2026-09-10T00:00:00.000Z");
});

test("effective deadline falls back to the estimate", () => {
  assert.equal(getEffectiveExpiry(validSession()).toISOString(), "2026-09-17T00:00:00.000Z");
  const laterObserved = validSession({ observedCookieExpiresAt: "2026-09-30T00:00:00.000Z" });
  assert.equal(getEffectiveExpiry(laterObserved).toISOString(), "2026-09-17T00:00:00.000Z");
});

test("isExpired treats the deadline itself as expired", () => {
  const session = validSession({ observedCookieExpiresAt: "2026-09-10T00:00:00.000Z" });
  const deadline = Date.parse("2026-09-10T00:00:00.000Z");
  assert.equal(isExpired(session, new Date(deadline - 1)), false);
  assert.equal(isExpired(session, new Date(deadline)), true);
  assert.equal(isExpired(session, new Date(deadline + 1)), true);
});

test("buildSessionCandidate estimates exactly 14 days and round-trips the schema", () => {
  const now = new Date("2026-09-03T12:34:56.789Z");
  const candidate = buildSessionCandidate({
    cookieName: SESSION_COOKIE_NAME,
    cookieValue: "fixture-cookie-never-print",
    observedCookieExpiresAt: null,
    validatedAccount: "basic",
    now,
  });
  assert.equal(MODDB_SESSION_DAYS, 14);
  assert.equal(candidate.schemaVersion, 1);
  assert.equal(candidate.capturedAt, "2026-09-03T12:34:56.789Z");
  assert.equal(candidate.validatedAt, "2026-09-03T12:34:56.789Z");
  assert.equal(candidate.modDbValidUntilEstimate, "2026-09-17T12:34:56.789Z");
  assert.equal(
    Date.parse(candidate.modDbValidUntilEstimate) - now.getTime(),
    MODDB_SESSION_DAYS * 86_400_000,
  );
  assert.equal(candidate.observedCookieExpiresAt, null);
  assert.deepEqual(parseSession(candidate), candidate);

  const observed = buildSessionCandidate({
    cookieName: SESSION_COOKIE_NAME,
    cookieValue: "fixture-cookie-never-print",
    observedCookieExpiresAt: "2026-09-05T00:00:00.000Z",
    validatedAccount: "basic",
    now,
  });
  assert.equal(getEffectiveExpiry(observed).toISOString(), "2026-09-05T00:00:00.000Z");
});

test("buildSessionCandidate rejects blank values by field name", () => {
  assert.equal(
    messageOf(() =>
      buildSessionCandidate({
        cookieName: SESSION_COOKIE_NAME,
        cookieValue: " ",
        observedCookieExpiresAt: null,
        validatedAccount: "basic",
      }),
    ),
    "invalid session field: cookieValue",
  );
});
