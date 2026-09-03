import assert from "node:assert/strict";
import test, { after } from "node:test";

import { ACCOUNT_SECRET_ID, SESSION_COOKIE_NAME, SESSION_SECRET_ID } from "../src/config.mjs";
import { BrokerError, ExitCode, safeResult } from "../src/contracts.mjs";
import { createPublisherStore, createRenewalStore } from "../src/secret-store.mjs";
import { SESSION_COOKIE, ensureSession } from "../src/session-service.mjs";
import { FakeSecretsManagerClient, awsError } from "./support/fake-secrets-manager.mjs";

const OLD_COOKIE = "fixture-cookie-never-print";
const NEW_COOKIE = "fixture-cookie-new-never-print";
const PASSWORD = "fixture-password-never-print";
const NEVER_PRINT = [OLD_COOKIE, NEW_COOKIE, PASSWORD, "fixture-cookie-other-never-print", "aws-message-never-print"];
const ACCOUNT = "basic";
const NOW = new Date("2026-09-10T12:00:00.000Z");
const clock = () => NOW;

const processOutput = [];
for (const stream of [process.stdout, process.stderr]) {
  const original = stream.write.bind(stream);
  stream.write = (chunk, ...rest) => {
    processOutput.push(String(chunk));
    return original(chunk, ...rest);
  };
}
const observed = []; // every thrown error and returned result
after(() => {
  const text = [processOutput.join(""), ...observed.map(dump)].join("\n");
  for (const fixture of NEVER_PRINT) assert.equal(text.includes(fixture), false, `${fixture} leaked`);
});
const dump = (value) =>
  value instanceof Error
    ? [String(value), value.stack, JSON.stringify(value), JSON.stringify(Object.getOwnPropertyNames(value).map((k) => String(value[k])))].join("\n")
    : JSON.stringify(value);

const session = (overrides = {}) => ({
  schemaVersion: 1,
  cookieName: SESSION_COOKIE_NAME,
  cookieValue: OLD_COOKIE,
  capturedAt: "2026-09-03T00:00:00.000Z",
  observedCookieExpiresAt: null,
  modDbValidUntilEstimate: "2026-09-17T00:00:00.000Z",
  validatedAt: "2026-09-03T00:00:00.000Z",
  validatedAccount: ACCOUNT,
  ...overrides,
});
const EXPIRED = session({ modDbValidUntilEstimate: "2026-09-09T00:00:00.000Z" });
const LOGIN = { schemaVersion: 1, email: "fixture-email@example.invalid", password: PASSWORD };

// Secrets Manager fake with a session container whose AWSCURRENT can move.
function secrets({ current = session(), currentVersionId = "v-old", promoteError = null } = {}) {
  const client = new FakeSecretsManagerClient();
  const state = { current, currentVersionId };
  client.respond("GetSecretValueCommand", (input) => {
    if (input.SecretId === ACCOUNT_SECRET_ID) return { SecretString: JSON.stringify(LOGIN), VersionId: "login-v1" };
    if (state.current === null) throw awsError("ResourceNotFoundException");
    return { SecretString: JSON.stringify(state.current), VersionId: state.currentVersionId };
  });
  client.respond("PutSecretValueCommand", (input) => {
    state.pending = JSON.parse(input.SecretString);
    return { VersionId: "v-candidate" };
  });
  client.respond("UpdateSecretVersionStageCommand", (input) => {
    if (promoteError) throw promoteError;
    state.current = state.pending;
    state.currentVersionId = input.MoveToVersionId;
    return {};
  });
  return { client, state, commands: () => client.calls.map((call) => call.name) };
}

// ModDB fake: `outcomes` is consumed one validateAccount call at a time; a
// string is a BrokerError code, "auth" is the authentication-failed variant.
function modDb(...outcomes) {
  const calls = [];
  const factory = ({ cookieValue }) => ({
    async validateAccount(account) {
      calls.push({ cookieValue, account });
      const outcome = outcomes.shift() ?? "ok";
      if (outcome === "ok") return { account };
      if (outcome === "auth") throw new BrokerError("authentication-failed", "denied", { exitCode: ExitCode.renewalRequired });
      throw new BrokerError(outcome, outcome);
    },
  });
  return { factory, calls };
}

const browser = (outcome = "ok") => {
  const calls = [];
  const fn = async (input) => {
    calls.push(input);
    if (outcome !== "ok") throw new BrokerError(outcome, outcome);
    return { cookieName: SESSION_COOKIE_NAME, cookieValue: NEW_COOKIE, observedCookieExpiresAt: null };
  };
  fn.calls = calls;
  return fn;
};

const INTERACTIVE = { interactiveWindows: true };
const CLOUD = { interactiveWindows: false };

function run({ purpose = "prepare", runtime = INTERACTIVE, store = secrets(), db = modDb(), renewal = browser(), publisherOnly = false, expectedAccount = ACCOUNT } = {}) {
  const promise = ensureSession({
    purpose,
    expectedAccount,
    runtime,
    renewalStore: publisherOnly ? undefined : createRenewalStore(store.client, { uuid: () => "token" }),
    publisherStore: createPublisherStore(store.client),
    browserRenewal: renewal,
    modDbFactory: db.factory,
    clock,
    onHumanActionRequired: () => {},
  });
  return promise.then(
    (result) => (observed.push(result), result),
    (error) => (observed.push(error), Promise.reject(error)),
  );
}

async function rejectsWithCode(promise, code) {
  let caught;
  try {
    await promise;
  } catch (error) {
    caught = error;
  }
  assert.ok(caught instanceof BrokerError, "expected a BrokerError");
  assert.equal(caught.code, code);
  return caught;
}

const withoutCookie = ({ [SESSION_COOKIE]: _cookie, ...rest }) => rest;
const loginReads = (store) => store.client.inputs("GetSecretValueCommand").filter((input) => input.SecretId === ACCOUNT_SECRET_ID).length;
const assertCurrentUnchanged = (store, renewal) => {
  assert.equal(store.client.inputs("UpdateSecretVersionStageCommand").length, 0, "AWSCURRENT was moved");
  assert.equal(store.state.currentVersionId, "v-old");
  if (renewal) assert.equal(renewal.calls.length, 0, "browser launched");
};

test("unexpired session with a passing live check is valid and never reads the account login", async () => {
  const store = secrets();
  const db = modDb();
  const renewal = browser();
  const result = await run({ store, db, renewal });
  assert.deepEqual(withoutCookie(result), { status: "valid", versionId: "v-old", validatedAccount: ACCOUNT, effectiveExpiry: "2026-09-17T00:00:00.000Z" });
  assert.equal(result[SESSION_COOKIE], OLD_COOKIE);
  assert.deepEqual(db.calls, [{ cookieValue: OLD_COOKIE, account: ACCOUNT }]);
  assert.equal(loginReads(store), 0);
  assertCurrentUnchanged(store, renewal);
  assert.deepEqual(store.commands(), ["GetSecretValueCommand"]);
});

test("valid result stringifies without the cookie and safeResult rejects the raw object", async () => {
  const result = await run();
  assert.equal(JSON.stringify(result).includes(OLD_COOKIE), false);
  assert.throws(() => safeResult("valid", result), /forbidden result field: symbol/);
  assert.equal(safeResult("valid", withoutCookie(result)).ok, true);
});

test("expected account defaults to the stored validatedAccount", async () => {
  const db = modDb();
  await run({ db, expectedAccount: null });
  assert.equal(db.calls[0].account, ACCOUNT);
});

test("purpose renew returns valid without renewing when the session still works", async () => {
  const renewal = browser();
  const result = await run({ purpose: "renew", renewal });
  assert.equal(result.status, "valid");
  assert.equal(renewal.calls.length, 0);
});

for (const purpose of ["prepare", "publish", "renew"]) {
  test(`cloud ${purpose} on expired metadata returns renewal-required without reading login or launching a browser`, async () => {
    const store = secrets({ current: EXPIRED });
    const db = modDb();
    const renewal = browser();
    const result = await run({ purpose, runtime: CLOUD, store, db, renewal });
    assert.deepEqual(result, { status: "renewal-required", reason: "expired" });
    assert.equal(loginReads(store), 0);
    assert.equal(db.calls.length, 0);
    assertCurrentUnchanged(store, renewal);
  });
}

test("cloud live authentication failure returns renewal-required with authentication-failed", async () => {
  const store = secrets();
  const renewal = browser();
  const result = await run({ runtime: CLOUD, store, db: modDb("auth"), renewal });
  assert.deepEqual(result, { status: "renewal-required", reason: "authentication-failed" });
  assert.equal(loginReads(store), 0);
  assertCurrentUnchanged(store, renewal);
});

test("status never renews even on interactive Windows", async () => {
  const store = secrets({ current: EXPIRED });
  const renewal = browser();
  const result = await run({ purpose: "status", store, renewal });
  assert.deepEqual(result, { status: "renewal-required", reason: "expired" });
  assert.equal(loginReads(store), 0);
  assertCurrentUnchanged(store, renewal);
});

test("a publisher-only caller cannot renew", async () => {
  const store = secrets({ current: EXPIRED });
  const renewal = browser();
  const result = await run({ store, renewal, publisherOnly: true });
  assert.deepEqual(result, { status: "renewal-required", reason: "expired" });
  assertCurrentUnchanged(store, renewal);
});

test("an empty session container counts as expired", async () => {
  const store = secrets({ current: null });
  const result = await run({ runtime: CLOUD, store });
  assert.deepEqual(result, { status: "renewal-required", reason: "expired" });
});

test("non-authentication live failures propagate unchanged", async () => {
  const store = secrets();
  const renewal = browser();
  await rejectsWithCode(run({ store, db: modDb("MODDB_ACCOUNT_MISMATCH"), renewal }), "MODDB_ACCOUNT_MISMATCH");
  assertCurrentUnchanged(store, renewal);
});

test("interactive Windows renews an expired session in the approved order", async () => {
  const store = secrets({ current: EXPIRED });
  const db = modDb();
  const renewal = browser();
  const result = await run({ store, db, renewal });

  assert.deepEqual(withoutCookie(result), {
    status: "renewed",
    versionId: "v-candidate",
    previousVersionId: "v-old",
    validatedAccount: ACCOUNT,
    effectiveExpiry: "2026-09-24T12:00:00.000Z",
  });
  assert.equal(result[SESSION_COOKIE], NEW_COOKIE);
  assert.equal(JSON.stringify(result).includes(NEW_COOKIE), false);

  assert.deepEqual(store.commands(), [
    "GetSecretValueCommand",
    "GetSecretValueCommand",
    "PutSecretValueCommand",
    "UpdateSecretVersionStageCommand",
    "GetSecretValueCommand",
  ]);
  assert.deepEqual(
    store.client.inputs("GetSecretValueCommand").map((input) => input.SecretId),
    [SESSION_SECRET_ID, ACCOUNT_SECRET_ID, SESSION_SECRET_ID],
  );
  assert.deepEqual(store.client.lastInput("PutSecretValueCommand").VersionStages, ["AWSPENDING"]);
  assert.deepEqual(store.client.lastInput("UpdateSecretVersionStageCommand"), {
    SecretId: SESSION_SECRET_ID,
    VersionStage: "AWSCURRENT",
    MoveToVersionId: "v-candidate",
    RemoveFromVersionId: "v-old",
  });
  assert.equal(store.state.pending.cookieValue, NEW_COOKIE);
  assert.equal(store.state.pending.validatedAccount, ACCOUNT);
  assert.equal(store.state.pending.capturedAt, NOW.toISOString());

  assert.equal(renewal.calls.length, 1);
  assert.deepEqual(renewal.calls[0].accountLogin, LOGIN);
  assert.equal(renewal.calls[0].expectedAccount, ACCOUNT);
  assert.equal(typeof renewal.calls[0].onHumanActionRequired, "function");
  // candidate validated before promotion, AWSCURRENT validated after
  assert.deepEqual(db.calls, [{ cookieValue: NEW_COOKIE, account: ACCOUNT }, { cookieValue: NEW_COOKIE, account: ACCOUNT }]);
});

test("a live authentication failure on interactive Windows also renews", async () => {
  const store = secrets();
  const result = await run({ store, db: modDb("auth", "ok", "ok") });
  assert.equal(result.status, "renewed");
  assert.equal(store.state.currentVersionId, "v-candidate");
});

test("bootstrapping from an empty container promotes without RemoveFromVersionId", async () => {
  const store = secrets({ current: null });
  const result = await run({ store });
  assert.equal(result.status, "renewed");
  assert.equal(result.previousVersionId, null);
  assert.equal("RemoveFromVersionId" in store.client.lastInput("UpdateSecretVersionStageCommand"), false);
});

test("publish caller gets approval-required after renewal and no cookie", async () => {
  const store = secrets({ current: EXPIRED });
  const result = await run({ purpose: "publish", store });
  assert.deepEqual(result, { status: "approval-required", reason: "renewed-during-publish", versionId: "v-candidate" });
  assert.equal(Object.getOwnPropertySymbols(result).length, 0);
  assert.equal(store.state.currentVersionId, "v-candidate", "the renewal itself still completed");
});

test("wrong account on the candidate leaves AWSCURRENT unchanged and never promotes", async () => {
  const store = secrets({ current: EXPIRED });
  await rejectsWithCode(run({ store, db: modDb("MODDB_ACCOUNT_MISMATCH") }), "MODDB_ACCOUNT_MISMATCH");
  assert.equal(store.client.inputs("PutSecretValueCommand").length, 1, "candidate was written as pending");
  assertCurrentUnchanged(store);
});

test("candidate that ModDB rejects leaves AWSCURRENT unchanged", async () => {
  const store = secrets({ current: EXPIRED });
  await rejectsWithCode(run({ store, db: modDb("auth") }), "authentication-failed");
  assertCurrentUnchanged(store);
});

for (const code of ["RENEWAL_CANCELLED", "RENEWAL_TIMEOUT", "RENEWAL_BROWSER_FAILED", "RENEWAL_ORIGIN_MISMATCH"]) {
  test(`browser outcome ${code} propagates and writes nothing`, async () => {
    const store = secrets({ current: EXPIRED });
    const db = modDb();
    await rejectsWithCode(run({ store, db, renewal: browser(code) }), code);
    assert.equal(store.client.inputs("PutSecretValueCommand").length, 0);
    assert.equal(db.calls.length, 0);
    assertCurrentUnchanged(store);
  });
}

test("promotion conflict propagates SESSION_PROMOTION_CONFLICT with no further writes or validation", async () => {
  const store = secrets({ current: EXPIRED, promoteError: awsError("InvalidParameterException") });
  const db = modDb();
  await rejectsWithCode(run({ store, db }), "SESSION_PROMOTION_CONFLICT");
  assert.deepEqual(store.commands(), ["GetSecretValueCommand", "GetSecretValueCommand", "PutSecretValueCommand", "UpdateSecretVersionStageCommand"]);
  assert.equal(db.calls.length, 1);
  assert.equal(store.state.currentVersionId, "v-old");
});

test("a reread that is not the candidate is reported as a promotion conflict and never rolled back", async () => {
  const store = secrets({ current: EXPIRED });
  store.client.respond("UpdateSecretVersionStageCommand", () => {
    store.state.current = session({ cookieValue: "fixture-cookie-other-never-print" });
    store.state.currentVersionId = "v-someone-else";
    return {};
  });
  await rejectsWithCode(run({ store }), "SESSION_PROMOTION_CONFLICT");
  assert.equal(store.client.inputs("UpdateSecretVersionStageCommand").length, 1);
});

test("renewal without an expected account stops before reading the login", async () => {
  const store = secrets({ current: null });
  const renewal = browser();
  await rejectsWithCode(run({ store, renewal, expectedAccount: null }), "MODDB_ACCOUNT_MISSING");
  assert.equal(loginReads(store), 0);
  assertCurrentUnchanged(store, renewal);
});
