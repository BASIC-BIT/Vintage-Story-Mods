import assert from "node:assert/strict";
import test, { after } from "node:test";

import { ACCOUNT_SECRET_ID, SESSION_COOKIE_NAME, SESSION_SECRET_ID } from "../src/config.mjs";
import { BrokerError } from "../src/contracts.mjs";
import { createAccountAdminStore, createPublisherStore, createRenewalStore } from "../src/secret-store.mjs";
import { FakeSecretsManagerClient, awsError } from "./support/fake-secrets-manager.mjs";

const NEVER_PRINT = ["fixture-cookie-never-print", "fixture-password-never-print", "aws-message-never-print"];
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

const SESSION = {
  schemaVersion: 1,
  cookieName: SESSION_COOKIE_NAME,
  cookieValue: "fixture-cookie-never-print",
  capturedAt: "2026-09-03T00:00:00.000Z",
  observedCookieExpiresAt: null,
  modDbValidUntilEstimate: "2026-09-17T00:00:00.000Z",
  validatedAt: "2026-09-03T00:00:00.000Z",
  validatedAccount: "basic",
};
const LOGIN = { schemaVersion: 1, email: "fixture-email@example.invalid", password: "fixture-password-never-print" };
const TOKEN = "11111111-2222-4333-8444-555555555555";
const fixedUuid = () => TOKEN;

const sessionClient = (output = { SecretString: JSON.stringify(SESSION), VersionId: "current-v1" }) =>
  new FakeSecretsManagerClient().respond("GetSecretValueCommand", output);

async function rejectsWithCode(promise, code) {
  let caught;
  try {
    await promise;
  } catch (error) {
    caught = error;
  }
  assert.ok(caught instanceof BrokerError, "expected a BrokerError");
  assert.equal(caught.code, code);
  const text = [String(caught), caught.stack, JSON.stringify({ ...caught })].join("\n");
  for (const fixture of NEVER_PRINT) assert.equal(text.includes(fixture), false, `error leaked ${fixture}`);
  assert.equal(text.includes("SecretString"), false, "error leaked command shape");
  return caught;
}

test("stores expose exactly their capability", () => {
  const client = new FakeSecretsManagerClient();
  assert.deepEqual(Object.keys(createAccountAdminStore(client)), ["putAccountLogin"]);
  assert.deepEqual(Object.keys(createPublisherStore(client)), ["readCurrentSession"]);
  assert.deepEqual(Object.keys(createRenewalStore(client)), [
    "readAccountLogin",
    "readCurrentSession",
    "putPendingSession",
    "promoteSession",
  ]);
});

test("readCurrentSession reads AWSCURRENT and keeps VersionId beside the session", async () => {
  const client = sessionClient();
  const result = await createPublisherStore(client).readCurrentSession();
  assert.deepEqual(client.lastInput("GetSecretValueCommand"), {
    SecretId: SESSION_SECRET_ID,
    VersionStage: "AWSCURRENT",
  });
  assert.deepEqual(result, { session: SESSION, versionId: "current-v1" });
  assert.equal("versionId" in result.session, false);
});

test("readAccountLogin reads AWSCURRENT of the account secret", async () => {
  const client = new FakeSecretsManagerClient().respond("GetSecretValueCommand", {
    SecretString: JSON.stringify({ ...LOGIN, extra: "dropped" }),
    VersionId: "login-v1",
  });
  const login = await createRenewalStore(client).readAccountLogin();
  assert.deepEqual(client.lastInput("GetSecretValueCommand"), {
    SecretId: ACCOUNT_SECRET_ID,
    VersionStage: "AWSCURRENT",
  });
  assert.deepEqual(login, LOGIN);
});

test("an empty session container reports first bootstrap", async () => {
  const client = new FakeSecretsManagerClient().respond("GetSecretValueCommand", () => {
    throw awsError("ResourceNotFoundException");
  });
  await rejectsWithCode(createRenewalStore(client).readCurrentSession(), "SESSION_SECRET_EMPTY");
  await rejectsWithCode(createRenewalStore(client).readAccountLogin(), "ACCOUNT_SECRET_EMPTY");
});

test("other read failures map to a fixed code", async () => {
  const client = new FakeSecretsManagerClient().respond("GetSecretValueCommand", () => {
    throw awsError("AccessDeniedException");
  });
  await rejectsWithCode(createPublisherStore(client).readCurrentSession(), "SECRET_READ_FAILED");
});

// Schema failures name the offending field in the code itself, never a value.
test("binary, missing, and malformed secret values are rejected safely", async () => {
  const { cookieValue: _dropped, ...withoutCookie } = SESSION;
  const cases = [
    [{ SecretBinary: new Uint8Array([1]), VersionId: "v" }, "SECRET_MISSING"],
    [{ VersionId: "v" }, "SECRET_MISSING"],
    [{ SecretString: '{"cookieValue": "fixture-cookie-never-print"', VersionId: "v" }, "SECRET_MALFORMED"],
    [{ SecretString: JSON.stringify({ ...SESSION, schemaVersion: 2 }), VersionId: "v" }, "SECRET_MALFORMED_schemaVersion"],
    [{ SecretString: JSON.stringify(withoutCookie), VersionId: "v" }, "SECRET_MALFORMED_cookieValue"],
    [{ SecretString: JSON.stringify({ ...SESSION, capturedAt: "fixture-cookie-never-print" }), VersionId: "v" }, "SECRET_MALFORMED_capturedAt"],
    [{ SecretString: JSON.stringify(SESSION) }, "SECRET_MALFORMED"],
  ];
  for (const [output, code] of cases) {
    const error = await rejectsWithCode(createPublisherStore(sessionClient(output)).readCurrentSession(), code);
    assert.match(error.code, /^[A-Za-z][A-Za-z0-9_-]{0,63}$/);
  }
});

test("putAccountLogin writes schema version 1 as AWSCURRENT with a caller token", async () => {
  const client = new FakeSecretsManagerClient().respond("PutSecretValueCommand", { VersionId: "login-v2" });
  await createAccountAdminStore(client, { uuid: fixedUuid }).putAccountLogin({
    email: LOGIN.email,
    password: LOGIN.password,
  });
  const input = client.lastInput("PutSecretValueCommand");
  assert.deepEqual(JSON.parse(input.SecretString), LOGIN);
  assert.deepEqual(
    { ...input, SecretString: undefined },
    { SecretId: ACCOUNT_SECRET_ID, ClientRequestToken: TOKEN, VersionStages: ["AWSCURRENT"], SecretString: undefined },
  );
});

test("putAccountLogin rejects blank credentials without sending", async () => {
  const client = new FakeSecretsManagerClient();
  await rejectsWithCode(createAccountAdminStore(client).putAccountLogin({ email: "", password: "x" }), "ACCOUNT_INVALID_email");
  await rejectsWithCode(createAccountAdminStore(client).putAccountLogin({ email: "x", password: " " }), "ACCOUNT_INVALID_password");
  assert.deepEqual(client.calls, []);
});

test("putPendingSession attaches only AWSPENDING and returns the new version", async () => {
  const client = new FakeSecretsManagerClient().respond("PutSecretValueCommand", { VersionId: "candidate-v2" });
  const result = await createRenewalStore(client, { uuid: fixedUuid }).putPendingSession(SESSION);
  assert.deepEqual(result, { versionId: "candidate-v2" });
  const input = client.lastInput("PutSecretValueCommand");
  assert.deepEqual(JSON.parse(input.SecretString), SESSION);
  assert.deepEqual(
    { ...input, SecretString: undefined },
    { SecretId: SESSION_SECRET_ID, ClientRequestToken: TOKEN, VersionStages: ["AWSPENDING"], SecretString: undefined },
  );
});

test("putPendingSession validates the candidate before sending", async () => {
  const client = new FakeSecretsManagerClient();
  await rejectsWithCode(
    createRenewalStore(client).putPendingSession({ ...SESSION, cookieValue: " " }),
    "SESSION_CANDIDATE_INVALID_cookieValue",
  );
  await rejectsWithCode(createRenewalStore(client).putPendingSession("not an object"), "SESSION_CANDIDATE_INVALID");
  assert.deepEqual(client.calls, []);
});

test("a retried pending write with the same token is byte-identical", async () => {
  const client = new FakeSecretsManagerClient().respond("PutSecretValueCommand", { VersionId: "candidate-v2" });
  const store = createRenewalStore(client, { uuid: fixedUuid });
  await store.putPendingSession(SESSION);
  await store.putPendingSession(SESSION);
  const [first, second] = client.inputs("PutSecretValueCommand");
  assert.deepEqual(first, second);
});

test("each renewal store draws a fresh token by default", async () => {
  const client = new FakeSecretsManagerClient().respond("PutSecretValueCommand", { VersionId: "v" });
  const store = createRenewalStore(client);
  await store.putPendingSession(SESSION);
  await store.putPendingSession(SESSION);
  const [first, second] = client.inputs("PutSecretValueCommand");
  assert.match(first.ClientRequestToken, /^[0-9a-f-]{36}$/);
  assert.notEqual(first.ClientRequestToken, second.ClientRequestToken);
});

test("pending write failures map to a fixed code", async () => {
  const client = new FakeSecretsManagerClient().respond("PutSecretValueCommand", () => {
    throw awsError("ResourceExistsException");
  });
  await rejectsWithCode(createRenewalStore(client).putPendingSession(SESSION), "SESSION_WRITE_FAILED");
  await rejectsWithCode(
    createAccountAdminStore(client).putAccountLogin({ email: LOGIN.email, password: LOGIN.password }),
    "ACCOUNT_WRITE_FAILED",
  );
});

test("promotion compares against observed current", async () => {
  const client = new FakeSecretsManagerClient().respond("UpdateSecretVersionStageCommand", {});
  const store = createRenewalStore(client);
  await store.promoteSession({ candidateVersionId: "candidate-v2", originalCurrentVersionId: "current-v1" });
  assert.deepEqual(client.lastInput("UpdateSecretVersionStageCommand"), {
    SecretId: SESSION_SECRET_ID,
    VersionStage: "AWSCURRENT",
    MoveToVersionId: "candidate-v2",
    RemoveFromVersionId: "current-v1",
  });
});

test("first bootstrap promotes without a removal target", async () => {
  const client = new FakeSecretsManagerClient().respond("UpdateSecretVersionStageCommand", {});
  await createRenewalStore(client).promoteSession({ candidateVersionId: "candidate-v1", originalCurrentVersionId: null });
  assert.deepEqual(client.lastInput("UpdateSecretVersionStageCommand"), {
    SecretId: SESSION_SECRET_ID,
    VersionStage: "AWSCURRENT",
    MoveToVersionId: "candidate-v1",
  });
});

test("promotion refuses an unknown original version instead of treating it as bootstrap", async () => {
  const client = new FakeSecretsManagerClient();
  const store = createRenewalStore(client);
  await rejectsWithCode(store.promoteSession({ candidateVersionId: "candidate-v1" }), "SESSION_VERSION_INVALID");
  await rejectsWithCode(
    store.promoteSession({ candidateVersionId: "", originalCurrentVersionId: "current-v1" }),
    "SESSION_VERSION_INVALID",
  );
  assert.deepEqual(client.calls, []);
});

test("a lost promotion race fails closed; every other promotion error is a plain failure", async () => {
  const promote = (error) => {
    const client = new FakeSecretsManagerClient().respond("UpdateSecretVersionStageCommand", () => {
      throw error;
    });
    return createRenewalStore(client).promoteSession({ candidateVersionId: "c", originalCurrentVersionId: "o" });
  };
  for (const name of ["InvalidRequestException", "ResourceExistsException", "InvalidParameterException"]) {
    await rejectsWithCode(promote(awsError(name)), "SESSION_PROMOTION_CONFLICT");
  }
  for (const name of ["AccessDeniedException", "ResourceNotFoundException", "InternalServiceError", "ThrottlingException"]) {
    await rejectsWithCode(promote(awsError(name)), "SESSION_PROMOTION_FAILED");
  }
  await rejectsWithCode(promote(new TypeError("fetch failed: aws-message-never-print")), "SESSION_PROMOTION_FAILED");
});
