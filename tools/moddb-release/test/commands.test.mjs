import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { EventEmitter } from "node:events";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test, { after } from "node:test";

import { strToU8, zipSync } from "fflate";

import { inspectArtifact } from "../src/artifact.mjs";
import { createCommands, readMaskedLine } from "../src/commands.mjs";
import { ACCOUNT_SECRET_ID, SESSION_COOKIE_NAME, SESSION_SECRET_ID } from "../src/config.mjs";
import { BrokerError, ExitCode } from "../src/contracts.mjs";
import { FakeSecretsManagerClient, awsError } from "./support/fake-secrets-manager.mjs";

const OLD_COOKIE = "fixture-cookie-never-print";
const NEW_COOKIE = "fixture-cookie-new-never-print";
const WINCRED_COOKIE = "fixture-cookie-wincred-never-print";
const PASSWORD = "fixture-password-never-print";
const EMAIL = "fixture-email@example.invalid";
const NEVER_PRINT = [OLD_COOKIE, NEW_COOKIE, WINCRED_COOKIE, PASSWORD, EMAIL, "aws-message-never-print"];
const ACCOUNT = "BASICBIT";
const NOW = new Date("2026-09-10T12:00:00.000Z");
const clock = () => NOW;

const tempDir = mkdtempSync(path.join(os.tmpdir(), "moddb-commands-"));
const zipBytes = zipSync({ "modinfo.json": strToU8('{"modid":"thebasics","version":"5.9.1"}'), "thebasics.dll": new Uint8Array([7, 7, 7]) });
const ZIP = path.join(tempDir, "thebasics-v5.9.1.zip");
writeFileSync(ZIP, zipBytes);
const SHA = createHash("sha256").update(zipBytes).digest("hex");
const CHANGELOG = "C:\\fixtures\\changelog.txt";

const observed = []; // every result, error, and stream write
after(() => {
  rmSync(tempDir, { recursive: true, force: true });
  const text = observed.map(dump).join("\n");
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

function secrets({ current = session(), currentVersionId = "v-old", labelFirstCurrent = true } = {}) {
  const client = new FakeSecretsManagerClient();
  const state = { current, currentVersionId };
  client.respond("GetSecretValueCommand", (input) => {
    if (input.SecretId === ACCOUNT_SECRET_ID) return { SecretString: JSON.stringify({ schemaVersion: 1, email: EMAIL, password: PASSWORD }), VersionId: "login-v1" };
    if (state.current === null) throw awsError("ResourceNotFoundException");
    return { SecretString: JSON.stringify(state.current), VersionId: state.currentVersionId };
  });
  client.respond("PutSecretValueCommand", (input) => {
    if (input.SecretId === ACCOUNT_SECRET_ID) return { VersionId: "login-v2" };
    state.pending = JSON.parse(input.SecretString);
    // Real Secrets Manager attaches AWSCURRENT to the first version of an
    // empty secret whatever VersionStages asked for (labelFirstCurrent=false
    // models the documented behaviour, where it stays AWSPENDING only).
    if (state.current === null && labelFirstCurrent) {
      state.current = state.pending;
      state.currentVersionId = "v-candidate";
    }
    return { VersionId: "v-candidate" };
  });
  client.respond("UpdateSecretVersionStageCommand", (input) => {
    state.current = state.pending;
    state.currentVersionId = input.MoveToVersionId;
    return {};
  });
  return { client, state };
}

// `validate` outcomes are consumed per validateAccount call ("ok" | "auth" | code).
// Set `db.client` to the store's FakeSecretsManagerClient to record bridge and
// validation calls in its command timeline.
function modDb({ validate = [], publish = "ok", published = false, publicFileId = 501 } = {}) {
  const calls = { bridge: [], validate: [], prepare: [], publish: [], publicState: [], verify: [], order: [] };
  const db = { calls, client: null };
  const factory = ({ cookieValue }) => ({
    async completeLoginBridge() {
      calls.bridge.push({ cookieValue });
      calls.order.push("bridge");
      db.client?.calls.push({ name: "completeLoginBridge", input: {} });
    },
    async validateAccount(account) {
      calls.validate.push({ cookieValue, account });
      calls.order.push("validate");
      db.client?.calls.push({ name: "validateAccount", input: {} });
      const outcome = validate.shift() ?? "ok";
      if (outcome === "ok") return { account };
      if (outcome === "auth") throw new BrokerError("authentication-failed", "denied", { exitCode: ExitCode.renewalRequired });
      throw new BrokerError(outcome, outcome);
    },
    async prepareRelease(input) {
      calls.prepare.push({ cookieValue, ...input });
      return { fileId: 501, modIdentifier: input.expectedModIdentifier, version: input.expectedVersion };
    },
    async publishRelease(input) {
      calls.publish.push({ cookieValue, ...input });
      if (publish === "indeterminate") throw new BrokerError("MODDB_PUBLISH_INDETERMINATE", "unconfirmed");
      return { assetId: 9001, releaseUrl: "https://mods.vintagestory.at/edit/release/?assetid=9001" };
    },
    async readPublicState(input) {
      calls.publicState.push(input);
      return { published, fileId: published ? publicFileId : null, releases: [] };
    },
    async verifyPublishedArtifact(input) {
      calls.verify.push(input);
      return { verified: true, fileId: 501, sha256: input.expectedSha256, downloadUrl: "https://mods.vintagestory.at/download/501/thebasics-v5.9.1.zip", compatibleVersions: ["1.21.1", "1.21.0"] };
    },
  });
  db.factory = factory;
  return db;
}

const counted = (fn) => {
  const wrapped = async (...args) => (wrapped.calls.push(args), fn(...args));
  wrapped.calls = [];
  return wrapped;
};

function harness({ store = secrets(), db = modDb(), answers = [EMAIL, PASSWORD, PASSWORD], files = { [CHANGELOG]: "Fixed the thing." }, ...overrides } = {}) {
  const out = { stdout: "", stderr: "", prompts: [] };
  const queue = [...answers];
  const deps = {
    secretsClient: store.client,
    stdin: null,
    stdout: { write: (chunk) => ((out.stdout += String(chunk)), observed.push(String(chunk)), true) },
    stderr: { write: (chunk) => ((out.stderr += String(chunk)), observed.push(String(chunk)), true) },
    isTTY: true,
    platform: "win32",
    env: {},
    readMaskedLine: async (prompt) => (out.prompts.push(prompt), queue.shift()),
    readWinCred: counted(async () => WINCRED_COOKIE),
    deleteWinCred: counted(async () => ({ deleted: true })),
    browserRenewal: counted(async ({ onHumanActionRequired }) => {
      onHumanActionRequired();
      return { cookieName: SESSION_COOKIE_NAME, cookieValue: NEW_COOKIE, observedCookieExpiresAt: null };
    }),
    modDbFactory: db.factory,
    inspectArtifact,
    readFile: (file) => {
      if (!Object.hasOwn(files, file)) throw new Error("ENOENT");
      return files[file];
    },
    clock,
    ...overrides,
  };
  const commands = createCommands(deps);
  const run = (name, options) =>
    commands[name](options).then(
      (result) => (observed.push(result), result),
      (error) => (observed.push(error), Promise.reject(error)),
    );
  return { run, store, db, deps, out, commands: () => store.client.calls.map((call) => call.name) };
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

const release = (extra = {}) => ({
  modId: 42,
  expectedModIdentifier: "thebasics",
  expectedVersion: "5.9.1",
  zip: ZIP,
  changelog: CHANGELOG,
  compatibleVersions: ["1.21.0", "1.21.1"],
  expectedSha256: SHA,
  ...extra,
});
const CLOUD = { env: { GITHUB_ACTIONS: "true" } };

// ---- account set ---------------------------------------------------------

test("account set requires a TTY and touches nothing", async () => {
  const h = harness({ isTTY: false });
  await rejectsWithCode(h.run("accountSet", {}), "TTY_REQUIRED");
  assert.deepEqual(h.commands(), []);
  assert.deepEqual(h.out.prompts, []);
});

test("account set stops on password mismatch before any AWS call", async () => {
  const h = harness({ answers: [EMAIL, PASSWORD, "fixture-password-other-never-print"] });
  await rejectsWithCode(h.run("accountSet", {}), "PASSWORD_MISMATCH");
  assert.deepEqual(h.commands(), []);
});

test("account set prompts email, password, confirmation and writes the login secret", async () => {
  const h = harness();
  const result = await h.run("accountSet", {});
  assert.deepEqual(result, { ok: true, status: "imported", data: { secretId: ACCOUNT_SECRET_ID, versionId: "login-v2" } });
  assert.deepEqual(h.out.prompts, ["ModDB account email: ", "Password: ", "Confirm password: "]);
  assert.deepEqual(h.commands(), ["PutSecretValueCommand"]);
  const put = h.store.client.lastInput("PutSecretValueCommand");
  assert.equal(put.SecretId, ACCOUNT_SECRET_ID);
  assert.deepEqual(put.VersionStages, ["AWSCURRENT"]);
  assert.deepEqual(JSON.parse(put.SecretString), { schemaVersion: 1, email: EMAIL, password: PASSWORD });
  assert.equal(h.out.stdout, "");
});

// ---- session status / renew ----------------------------------------------

test("session status reports a valid session without the cookie", async () => {
  const h = harness();
  const result = await h.run("sessionStatus", {});
  assert.deepEqual(result, { ok: true, status: "valid", data: { versionId: "v-old", validatedAccount: ACCOUNT, effectiveExpiry: "2026-09-17T00:00:00.000Z" } });
  assert.deepEqual(Object.getOwnPropertySymbols(result.data), []);
  assert.deepEqual(h.db.calls.validate, [{ cookieValue: OLD_COOKIE, account: ACCOUNT }]);
});

test("session status never renews, even expired on interactive Windows", async () => {
  const h = harness({ store: secrets({ current: EXPIRED }) });
  const result = await h.run("sessionStatus", {});
  assert.deepEqual(result, { ok: false, status: "renewal-required", reason: "expired" });
  assert.deepEqual(h.commands(), ["GetSecretValueCommand"]);
  assert.equal(h.deps.browserRenewal.calls.length, 0);
});

test("session status on an empty container is renewal-required", async () => {
  const h = harness({ store: secrets({ current: null }) });
  assert.deepEqual(await h.run("sessionStatus", {}), { ok: false, status: "renewal-required", reason: "expired" });
});

for (const [label, overrides] of [["GitHub Actions", CLOUD], ["non-Windows", { platform: "linux" }], ["no TTY", { isTTY: false }]]) {
  test(`session renew refuses on ${label} before touching AWS`, async () => {
    const h = harness(overrides);
    await rejectsWithCode(h.run("sessionRenew", { expectedAccount: ACCOUNT }), "INTERACTIVE_WINDOWS_REQUIRED");
    assert.deepEqual(h.commands(), []);
    assert.equal(h.deps.browserRenewal.calls.length, 0);
  });
}

test("session renew renews an expired session and prompts for the reCAPTCHA once", async () => {
  const h = harness({ store: secrets({ current: EXPIRED }) });
  const result = await h.run("sessionRenew", { expectedAccount: ACCOUNT });
  assert.deepEqual(result, {
    ok: true,
    status: "renewed",
    data: { versionId: "v-candidate", previousVersionId: "v-old", validatedAccount: ACCOUNT, effectiveExpiry: "2026-09-24T12:00:00.000Z" },
  });
  assert.equal(h.deps.browserRenewal.calls.length, 1);
  assert.equal(h.out.stderr, "Complete the reCAPTCHA in the Chrome window, then press the login button.\n");
  assert.equal(h.out.stdout, "");
  assert.equal(h.store.state.currentVersionId, "v-candidate");
});

test("session renew returns valid without a browser when the session still works", async () => {
  const h = harness();
  assert.equal((await h.run("sessionRenew", { expectedAccount: ACCOUNT })).status, "valid");
  assert.equal(h.deps.browserRenewal.calls.length, 0);
  assert.equal(h.out.stderr, "");
});

// ---- session import-wincred ----------------------------------------------

test("import-wincred requires Windows", async () => {
  const h = harness({ platform: "linux" });
  await rejectsWithCode(h.run("sessionImportWincred", { expectedAccount: ACCOUNT }), "WINCRED_UNSUPPORTED_PLATFORM");
  await rejectsWithCode(h.run("sessionImportWincred", { finalizeVersion: "v-old" }), "WINCRED_UNSUPPORTED_PLATFORM");
  assert.deepEqual(h.commands(), []);
  assert.equal(h.deps.readWinCred.calls.length, 0);
});

test("import-wincred validates, stages, promotes, and keeps the Windows entry", async () => {
  const h = harness();
  h.db.client = h.store.client;
  const result = await h.run("sessionImportWincred", { expectedAccount: ACCOUNT });
  assert.deepEqual(result, {
    ok: true,
    status: "imported",
    data: { versionId: "v-candidate", previousVersionId: "v-old", validatedAccount: ACCOUNT, effectiveExpiry: "2026-09-24T12:00:00.000Z" },
  });
  // Validation precedes the pending write; the promoted version is reread but not revalidated here.
  assert.deepEqual(h.commands(), [
    "GetSecretValueCommand",
    "completeLoginBridge",
    "validateAccount",
    "PutSecretValueCommand",
    "UpdateSecretVersionStageCommand",
    "GetSecretValueCommand",
  ]);
  const put = h.store.client.lastInput("PutSecretValueCommand");
  assert.deepEqual(put.VersionStages, ["AWSPENDING"]);
  assert.deepEqual(JSON.parse(put.SecretString), {
    schemaVersion: 1,
    cookieName: SESSION_COOKIE_NAME,
    cookieValue: WINCRED_COOKIE,
    capturedAt: NOW.toISOString(),
    observedCookieExpiresAt: null,
    modDbValidUntilEstimate: "2026-09-24T12:00:00.000Z",
    validatedAt: NOW.toISOString(),
    validatedAccount: ACCOUNT,
  });
  assert.deepEqual(h.store.client.lastInput("UpdateSecretVersionStageCommand"), {
    SecretId: SESSION_SECRET_ID,
    VersionStage: "AWSCURRENT",
    MoveToVersionId: "v-candidate",
    RemoveFromVersionId: "v-old",
  });
  assert.deepEqual(h.db.calls.bridge, [{ cookieValue: WINCRED_COOKIE }]);
  assert.deepEqual(h.db.calls.validate, [{ cookieValue: WINCRED_COOKIE, account: ACCOUNT }]);
  assert.deepEqual(h.db.calls.order, ["bridge", "validate"]);
  assert.equal(h.deps.readWinCred.calls.length, 1);
  assert.equal(h.deps.deleteWinCred.calls.length, 0);
});

test("import-wincred bootstraps an empty container that AWS labels current on the first write", async () => {
  const h = harness({ store: secrets({ current: null }) });
  const result = await h.run("sessionImportWincred", { expectedAccount: ACCOUNT });
  assert.equal(result.status, "imported");
  assert.equal(result.data.versionId, "v-candidate");
  assert.equal(result.data.previousVersionId, null);
  assert.equal(h.store.client.inputs("UpdateSecretVersionStageCommand").length, 0);
});

test("import-wincred bootstraps an empty container whose first write stays pending", async () => {
  const h = harness({ store: secrets({ current: null, labelFirstCurrent: false }) });
  const result = await h.run("sessionImportWincred", { expectedAccount: ACCOUNT });
  assert.equal(result.status, "imported");
  assert.equal(result.data.versionId, "v-candidate");
  assert.equal(result.data.previousVersionId, null);
  assert.equal(h.store.client.inputs("UpdateSecretVersionStageCommand").length, 1);
  assert.equal(Object.hasOwn(h.store.client.lastInput("UpdateSecretVersionStageCommand"), "RemoveFromVersionId"), false);
});

test("import-wincred does not write or promote when the account does not match", async () => {
  const h = harness({ db: modDb({ validate: ["MODDB_ACCOUNT_MISMATCH"] }) });
  await rejectsWithCode(h.run("sessionImportWincred", { expectedAccount: ACCOUNT }), "MODDB_ACCOUNT_MISMATCH");
  assert.equal(h.store.client.inputs("PutSecretValueCommand").length, 0);
  assert.equal(h.store.client.inputs("UpdateSecretVersionStageCommand").length, 0);
  assert.equal(h.deps.deleteWinCred.calls.length, 0);
});

// Regression (seen against real AWS 2026-09-03): the first version of an
// empty secret gets AWSCURRENT no matter what stages were requested, so a
// dead Windows cookie written before validation became the live session.
test("import-wincred into an empty container writes nothing when the cookie is dead", async () => {
  const h = harness({ store: secrets({ current: null }), db: modDb({ validate: ["auth"] }) });
  await rejectsWithCode(h.run("sessionImportWincred", { expectedAccount: ACCOUNT }), "authentication-failed");
  assert.deepEqual(h.commands(), ["GetSecretValueCommand"]);
  assert.equal(h.store.state.current, null, "the empty container gained a current version");
  assert.equal(h.deps.deleteWinCred.calls.length, 0);
});

test("finalize requires the exact AWSCURRENT version before deleting the Windows entry", async () => {
  const h = harness();
  await rejectsWithCode(h.run("sessionImportWincred", { finalizeVersion: "v-other" }), "SESSION_VERSION_MISMATCH");
  assert.equal(h.deps.deleteWinCred.calls.length, 0);
  const result = await h.run("sessionImportWincred", { finalizeVersion: "v-old" });
  assert.deepEqual(result, { ok: true, status: "finalized", data: { versionId: "v-old", validatedAccount: ACCOUNT, winCredDeleted: true } });
  assert.deepEqual(h.db.calls.validate, [{ cookieValue: OLD_COOKIE, account: ACCOUNT }]);
  assert.deepEqual(h.db.calls.bridge, [], "an already registered session is not bridged again");
  assert.equal(h.deps.deleteWinCred.calls.length, 1);
});

test("finalize keeps the Windows entry when the live check fails", async () => {
  const h = harness({ db: modDb({ validate: ["auth"] }) });
  await rejectsWithCode(h.run("sessionImportWincred", { finalizeVersion: "v-old" }), "authentication-failed");
  assert.equal(h.deps.deleteWinCred.calls.length, 0);
});

test("import then finalize in sequence", async () => {
  const h = harness();
  const imported = await h.run("sessionImportWincred", { expectedAccount: ACCOUNT });
  const finalized = await h.run("sessionImportWincred", { finalizeVersion: imported.data.versionId });
  assert.equal(finalized.status, "finalized");
  assert.deepEqual([h.deps.readWinCred.calls.length, h.deps.deleteWinCred.calls.length], [1, 1]);
});

// ---- release prepare -----------------------------------------------------

test("prepare inspects the artifact before any AWS call", async () => {
  const h = harness();
  await rejectsWithCode(h.run("releasePrepare", release({ expectedSha256: "0".repeat(64) })), "ARTIFACT_HASH_MISMATCH");
  await rejectsWithCode(h.run("releasePrepare", release({ expectedVersion: "5.9.2" })), "ARTIFACT_IDENTITY_MISMATCH");
  await rejectsWithCode(h.run("releasePrepare", release({ zip: path.join(tempDir, "missing.zip") })), "ARTIFACT_NOT_FOUND");
  assert.deepEqual(h.commands(), []);
  assert.deepEqual(h.db.calls.prepare, []);
});

test("prepare validates the changelog before any AWS call", async () => {
  const h = harness({ files: { [CHANGELOG]: "Breaking \u2014 change", "C:\\fixtures\\blank.txt": " \n", "C:\\fixtures\\agent.txt": "[AGENT] drafted this" } });
  await rejectsWithCode(h.run("releasePrepare", release()), "CHANGELOG_INVALID");
  await rejectsWithCode(h.run("releasePrepare", release({ changelog: "C:\\fixtures\\blank.txt" })), "CHANGELOG_INVALID");
  await rejectsWithCode(h.run("releasePrepare", release({ changelog: "C:\\fixtures\\agent.txt" })), "CHANGELOG_INVALID");
  await rejectsWithCode(h.run("releasePrepare", release({ changelog: "C:\\fixtures\\none.txt" })), "CHANGELOG_NOT_FOUND");
  assert.deepEqual(h.commands(), []);
});

test("prepare returns exact staged evidence without the zip path", async () => {
  const h = harness();
  const result = await h.run("releasePrepare", release());
  assert.deepEqual(result, {
    ok: true,
    status: "prepared",
    data: {
      fileId: 501,
      modIdentifier: "thebasics",
      version: "5.9.1",
      fileName: "thebasics-v5.9.1.zip",
      byteSize: zipBytes.length,
      entryCount: 2,
      sha256: SHA,
      sessionVersionId: "v-old",
      sessionStatus: "valid",
      compatibleVersions: ["1.21.0", "1.21.1"],
    },
  });
  const [call] = h.db.calls.prepare;
  assert.equal(call.cookieValue, OLD_COOKIE);
  assert.deepEqual([call.modId, call.expectedModIdentifier, call.expectedVersion, call.artifact.sha256], [42, "thebasics", "5.9.1", SHA]);
  assert.equal(JSON.stringify(result).includes(tempDir), false);
});

test("prepare and publish check an explicitly expected account, else the stored one", async () => {
  const h = harness();
  await h.run("releasePrepare", release({ expectedAccount: "someoneElse" }));
  await h.run("releasePublish", release({ expectedFileId: 501, expectedAccount: "someoneElse" }));
  await h.run("releasePrepare", release());
  assert.deepEqual(
    h.db.calls.validate.map((call) => call.account),
    ["someoneElse", "someoneElse", ACCOUNT],
  );
});

test("prepare renews first on interactive Windows and reports it", async () => {
  const h = harness({ store: secrets({ current: EXPIRED }) });
  const result = await h.run("releasePrepare", release());
  assert.deepEqual([result.status, result.data.sessionStatus, result.data.sessionVersionId], ["prepared", "renewed", "v-candidate"]);
  assert.equal(h.db.calls.prepare[0].cookieValue, NEW_COOKIE);
  assert.equal(h.out.stderr, "Complete the reCAPTCHA in the Chrome window, then press the login button.\n");
});

test("prepare in the cloud stops with renewal-required and never stages", async () => {
  const h = harness({ store: secrets({ current: EXPIRED }), ...CLOUD });
  assert.deepEqual(await h.run("releasePrepare", release()), { ok: false, status: "renewal-required", reason: "expired" });
  assert.deepEqual(h.commands(), ["GetSecretValueCommand"]);
  assert.deepEqual(h.db.calls.prepare, []);
  assert.equal(h.deps.browserRenewal.calls.length, 0);
});

test("prepare in the cloud reports a failed live check", async () => {
  const h = harness({ db: modDb({ validate: ["auth"] }), ...CLOUD });
  assert.deepEqual(await h.run("releasePrepare", release()), { ok: false, status: "renewal-required", reason: "authentication-failed" });
});

// ---- release publish -----------------------------------------------------

test("publish rechecks artifact, changelog, and file id, then saves and verifies", async () => {
  const h = harness();
  const result = await h.run("releasePublish", release({ expectedFileId: 501 }));
  assert.deepEqual(result, {
    ok: true,
    status: "published",
    data: {
      fileId: 501,
      assetId: 9001,
      releaseUrl: "https://mods.vintagestory.at/edit/release/?assetid=9001",
      modIdentifier: "thebasics",
      version: "5.9.1",
      sha256: SHA,
      verifiedSha256: SHA,
      downloadUrl: "https://mods.vintagestory.at/download/501/thebasics-v5.9.1.zip",
      compatibleVersions: ["1.21.1", "1.21.0"], // what ModDB serves, not the requested input
      sessionVersionId: "v-old",
    },
  });
  const [call] = h.db.calls.publish;
  assert.equal(call.cookieValue, OLD_COOKIE);
  assert.deepEqual(
    [call.modId, call.expectedModIdentifier, call.expectedVersion, call.expectedFileId, call.changelogHtml, call.compatibleVersions, call.artifact.sha256],
    [42, "thebasics", "5.9.1", 501, "Fixed the thing.", ["1.21.0", "1.21.1"], SHA],
  );
  assert.deepEqual(h.db.calls.verify, [{ modId: 42, expectedModIdentifier: "thebasics", expectedVersion: "5.9.1", expectedFileId: 501, expectedSha256: SHA, compatibleVersions: ["1.21.0", "1.21.1"] }]);
});

test("publish validates locally before any AWS call", async () => {
  const h = harness({ files: { [CHANGELOG]: "em \u2014 dash", "C:\\fixtures\\ok.txt": "ok", "C:\\fixtures\\agent.txt": "notes [AGENT]" } });
  await rejectsWithCode(h.run("releasePublish", release({ expectedFileId: 501 })), "CHANGELOG_INVALID");
  await rejectsWithCode(h.run("releasePublish", release({ expectedFileId: 501, changelog: "C:\\fixtures\\agent.txt" })), "CHANGELOG_INVALID");
  await rejectsWithCode(h.run("releasePublish", release({ expectedFileId: 501, changelog: "C:\\fixtures\\ok.txt", expectedSha256: "0".repeat(64) })), "ARTIFACT_HASH_MISMATCH");
  assert.deepEqual(h.commands(), []);
  assert.deepEqual(h.db.calls.publish, []);
});

test("renewal during publish stops with approval-required and never saves", async () => {
  const h = harness({ store: secrets({ current: EXPIRED }) });
  const result = await h.run("releasePublish", release({ expectedFileId: 501 }));
  assert.deepEqual(result, { ok: false, status: "approval-required", reason: "renewed-during-publish" });
  assert.equal(h.store.state.currentVersionId, "v-candidate");
  assert.deepEqual(h.db.calls.publish, []);
  assert.deepEqual(h.db.calls.verify, []);
});

test("publish in the cloud with an expired session is renewal-required", async () => {
  const h = harness({ store: secrets({ current: EXPIRED }), ...CLOUD });
  assert.deepEqual(await h.run("releasePublish", release({ expectedFileId: 501 })), { ok: false, status: "renewal-required", reason: "expired" });
  assert.deepEqual(h.db.calls.publish, []);
});

test("an indeterminate save is verified through public state before continuing", async () => {
  const h = harness({ db: modDb({ publish: "indeterminate", published: true }) });
  const result = await h.run("releasePublish", release({ expectedFileId: 501 }));
  assert.deepEqual([result.status, result.data.assetId, result.data.releaseUrl, result.data.verifiedSha256], ["published", null, null, SHA]);
  assert.deepEqual(h.db.calls.publicState, [{ modId: 42, expectedVersion: "5.9.1" }]);
  assert.equal(h.db.calls.verify.length, 1);
});

test("an indeterminate save whose public file id is not the staged one rethrows", async () => {
  const h = harness({ db: modDb({ publish: "indeterminate", published: true, publicFileId: 499 }) });
  await rejectsWithCode(h.run("releasePublish", release({ expectedFileId: 501 })), "MODDB_PUBLISH_INDETERMINATE");
  assert.equal(h.db.calls.publicState.length, 1);
  assert.deepEqual(h.db.calls.verify, []);
});

test("an indeterminate save that is not public rethrows", async () => {
  const h = harness({ db: modDb({ publish: "indeterminate", published: false }) });
  await rejectsWithCode(h.run("releasePublish", release({ expectedFileId: 501 })), "MODDB_PUBLISH_INDETERMINATE");
  assert.equal(h.db.calls.publicState.length, 1);
  assert.deepEqual(h.db.calls.verify, []);
});

// ---- readMaskedLine ------------------------------------------------------

function fakeTerminal() {
  const stdin = new EventEmitter();
  stdin.isTTY = true;
  stdin.rawModes = [];
  stdin.paused = false;
  stdin.setRawMode = (on) => stdin.rawModes.push(on);
  stdin.resume = () => (stdin.paused = false);
  stdin.pause = () => (stdin.paused = true);
  stdin.setEncoding = () => {};
  let written = "";
  const stdout = { write: (chunk) => ((written += String(chunk)), true) };
  return { stdin, stdout, written: () => written };
}

test("readMaskedLine echoes nothing, handles backspace, and restores the terminal", async () => {
  const term = fakeTerminal();
  const pending = readMaskedLine("Password: ", term);
  term.stdin.emit("data", "fixture-passwor");
  term.stdin.emit("data", "x\u007f");
  term.stdin.emit("data", "d-never-print\r");
  assert.equal(await pending, PASSWORD);
  assert.equal(term.written(), "Password: \n");
  assert.deepEqual(term.stdin.rawModes, [true, false]);
  assert.equal(term.stdin.paused, true);
  assert.equal(term.stdin.listenerCount("data"), 0);
});

test("readMaskedLine rejects on Ctrl-C and still restores the terminal", async () => {
  const term = fakeTerminal();
  const pending = readMaskedLine("Password: ", term);
  term.stdin.emit("data", "ab\u0003");
  await rejectsWithCode(pending, "PROMPT_CANCELLED");
  assert.deepEqual(term.stdin.rawModes, [true, false]);
  assert.equal(term.stdin.paused, true);
  assert.equal(term.written(), "Password: \n");
});
