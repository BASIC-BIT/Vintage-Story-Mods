import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test, { after, afterEach, beforeEach } from "node:test";

import { strToU8, zipSync } from "fflate";

import { inspectArtifact } from "../src/artifact.mjs";
import { BrokerError, ExitCode } from "../src/contracts.mjs";
import { createModDbClient } from "../src/moddb-client.mjs";
import { startFakeModDb } from "./support/fake-moddb.mjs";

const COOKIE = "fixture-cookie-never-print";
const MOD_ID = 640;
const CHANGELOG = "<h2>The BASICs v5.9.1</h2><p>notes &amp; more</p>";
const COMPAT = ["1.22.6", "1.22.7"];

const tempDir = mkdtempSync(path.join(os.tmpdir(), "moddb-client-"));
const zipBytes = zipSync({ "modinfo.json": strToU8('{"modid":"thebasics","version":"5.9.1"}'), "thebasics.dll": new Uint8Array([7, 7, 7]) });
const zipPath = path.join(tempDir, "thebasics-v5.9.1.zip");
writeFileSync(zipPath, zipBytes);
const artifact = inspectArtifact(zipPath);
const identity = { modId: MOD_ID, artifact, expectedModIdentifier: "thebasics", expectedVersion: "5.9.1" };

// Everything a test observed that could leak: thrown errors and process output.
const thrown = [];
const processOutput = [];
for (const stream of [process.stdout, process.stderr]) {
  const original = stream.write.bind(stream);
  stream.write = (chunk, ...rest) => {
    processOutput.push(String(chunk));
    return original(chunk, ...rest);
  };
}

let fake;
let client;
beforeEach(async () => {
  fake = await startFakeModDb({ cookieValue: COOKIE });
  client = createModDbClient({ origin: fake.origin, cookieValue: COOKIE });
});
afterEach(async () => {
  for (const request of fake.requests.filter((r) => r.server !== "moddb")) {
    assert.equal(JSON.stringify(request).includes(COOKIE), false, "cookie reached another origin");
  }
  await fake.close();
});
after(() => {
  rmSync(tempDir, { recursive: true, force: true });
  for (const error of thrown) {
    const dump = [String(error), error.stack, JSON.stringify(error), JSON.stringify(Object.getOwnPropertyNames(error).map((k) => String(error[k])))].join("\n");
    assert.equal(dump.includes(COOKIE), false, "cookie leaked through an error");
  }
  assert.equal(processOutput.join("").includes(COOKIE), false, "cookie reached process output");
});

async function rejectsWith(promise, code, exitCode = ExitCode.failed) {
  let error;
  await assert.rejects(promise, (caught) => ((error = caught), thrown.push(caught), caught instanceof BrokerError));
  assert.equal(error.code, code);
  assert.equal(error.exitCode, exitCode);
  return error;
}

const requestsTo = (pathname, server = "moddb") => fake.requests.filter((r) => r.server === server && r.path === pathname);
const stage = (fileId = 501) => fake.state.staged.push({ fileId, modIdentifier: "thebasics", version: "5.9.1", fileName: artifact.fileName, bytes: zipBytes });

test("client construction requires an exact origin and a cookie", () => {
  assert.throws(() => createModDbClient({ origin: `${fake.origin}/path`, cookieValue: COOKIE }), (e) => (thrown.push(e), e.code === "MODDB_INVALID_ORIGIN"));
  assert.throws(() => createModDbClient({ origin: fake.origin, cookieValue: " " }), (e) => (thrown.push(e), e.code === "MODDB_COOKIE_MISSING"));
  assert.deepEqual(Object.keys(client).sort(), ["completeLoginBridge", "prepareRelease", "publishRelease", "readPublicState", "validateAccount", "verifyPublishedArtifact"]);
  assert.equal(JSON.stringify(client).includes(COOKIE), false);
});

test("validateAccount reads the account menu on /accountsettings", async () => {
  assert.deepEqual(await client.validateAccount("BASICBIT"), { account: "BASICBIT" });
  const [request] = requestsTo("/accountsettings");
  assert.equal(request.headers.cookie, `vs_websessionkey=${COOKIE}`);
  const error = await rejectsWith(client.validateAccount("someoneElse"), "MODDB_ACCOUNT_MISMATCH");
  assert.equal(error.message.includes("BASICBIT"), false, "actual account name is not echoed");
});

// ModDB accepts a cookie only after its /login bridge has registered it.
test("completeLoginBridge registers a fresh cookie before the account check can pass", async () => {
  fake.state.requireBridge = true;
  await rejectsWith(client.validateAccount("BASICBIT"), "authentication-failed", ExitCode.renewalRequired);
  await client.completeLoginBridge();
  assert.deepEqual(await client.validateAccount("BASICBIT"), { account: "BASICBIT" });
  const [bridge] = requestsTo("/login");
  assert.equal(bridge.headers.cookie, `vs_websessionkey=${COOKIE}`);
  assert.deepEqual(fake.requests.map((r) => r.path), ["/accountsettings", "/login", "/accountsettings"]);
});

test("completeLoginBridge accepts 200 or a same-origin redirect and treats bounces as authentication failures", async () => {
  fake.state.bridgeStalls = true;
  await client.completeLoginBridge();
  fake.state.bridgeStalls = false;
  fake.state.bridgeRedirect = "/login";
  await rejectsWith(client.completeLoginBridge(), "authentication-failed", ExitCode.renewalRequired);
  fake.state.bridgeRedirect = `${fake.cdnOrigin}/?loginredir=mods`;
  await rejectsWith(client.completeLoginBridge(), "authentication-failed", ExitCode.renewalRequired);
  const stale = createModDbClient({ origin: fake.origin, cookieValue: "fixture-stale-cookie" });
  await rejectsWith(stale.completeLoginBridge(), "authentication-failed", ExitCode.renewalRequired);
  assert.equal(fake.requests.filter((r) => r.server === "cdn").length, 0, "the bounce was followed");
});

test("unauthenticated pages stop with renewal-required", async () => {
  const stale = createModDbClient({ origin: fake.origin, cookieValue: "fixture-stale-cookie" });
  await rejectsWith(stale.validateAccount("BASICBIT"), "authentication-failed", ExitCode.renewalRequired);
  await rejectsWith(stale.prepareRelease(identity), "authentication-failed", ExitCode.renewalRequired);
  assert.equal(requestsTo("/edit-uploadfile").length, 0);
});

test("redirects are never followed and cross-origin locations are rejected", async () => {
  fake.state.formRedirect = `${fake.cdnOrigin}/elsewhere`;
  await rejectsWith(client.prepareRelease(identity), "MODDB_FORM_UNAVAILABLE");
  fake.state.formRedirect = "/login";
  await rejectsWith(client.prepareRelease(identity), "authentication-failed", ExitCode.renewalRequired);
  assert.equal(fake.requests.filter((r) => r.server === "cdn").length, 0);
});

test("prepare uploads the exact multipart form and proves the staged file", async () => {
  const result = await client.prepareRelease(identity);
  assert.deepEqual(result, { fileId: 501, modIdentifier: "thebasics", version: "5.9.1" });

  const [upload] = requestsTo("/edit-uploadfile");
  assert.match(upload.headers["content-type"], /^multipart\/form-data; boundary=/);
  assert.equal(upload.headers.cookie, `vs_websessionkey=${COOKIE}`);
  assert.deepEqual(upload.fields, {
    upload: "1",
    assettypeid: "2",
    assetid: "0",
    modId: "640",
    file: { name: "thebasics-v5.9.1.zip", type: "application/zip", size: zipBytes.byteLength, sha256: artifact.sha256 },
  });
  const forms = requestsTo("/edit/release/");
  assert.deepEqual(forms.map((r) => [r.method, r.query.modid]), [["GET", "640"], ["GET", "640"]]);
  assert.equal(fake.requests.length, 3);
});

test("prepare rejects local identity mismatches before touching the network", async () => {
  await rejectsWith(client.prepareRelease({ ...identity, expectedVersion: "5.9.2" }), "ARTIFACT_IDENTITY_MISMATCH");
  await rejectsWith(client.prepareRelease({ ...identity, modId: "640" }), "MODDB_INVALID_MOD_ID");
  assert.equal(fake.requests.length, 0);
});

test("prepare is blocked by any preexisting staged upload", async () => {
  stage(77);
  await rejectsWith(client.prepareRelease(identity), "MODDB_STAGED_FILE_EXISTS");
  fake.state.staged = [{ fileId: 78, modIdentifier: "", version: "", bytes: zipBytes, fileName: "x.zip" }];
  await rejectsWith(client.prepareRelease(identity), "MODDB_STAGED_FILE_EXISTS");
  assert.equal(requestsTo("/edit-uploadfile").length, 0);
});

test("prepare validates the upload response exactly", async () => {
  const cases = [
    [{ status: "error", errormessage: "Too large" }, "MODDB_UPLOAD_REJECTED"],
    [{ status: "ok", modparse: "error", parsemsg: "bad modinfo" }, "MODDB_UPLOAD_PARSE_FAILED"],
    [{ status: "ok", modparse: "ok", modid: "thebasics", modversion: "5.9.0", fileid: 5 }, "MODDB_UPLOAD_IDENTITY_MISMATCH"],
    [{ status: "ok", modparse: "ok", modid: "TheBasics", modversion: "5.9.1", fileid: 5 }, "MODDB_UPLOAD_IDENTITY_MISMATCH"],
    [{ status: "ok", modparse: "ok", modid: "thebasics", modversion: "5.9.1", fileid: 0 }, "MODDB_UPLOAD_FILE_ID_INVALID"],
    [{ status: "ok", modparse: "ok", modid: "thebasics", modversion: "5.9.1", fileid: "12abc" }, "MODDB_UPLOAD_FILE_ID_INVALID"],
  ];
  for (const [override, code] of cases) {
    fake.state.uploadOverride = override;
    const error = await rejectsWith(client.prepareRelease(identity), code);
    assert.equal(error.message.includes("Too large") || error.message.includes("bad modinfo"), false, "response body is not echoed");
  }
});

test("prepare requires exactly one staged file equal to the returned file id", async () => {
  fake.state.stageTwice = true;
  await rejectsWith(client.prepareRelease(identity), "MODDB_STAGED_STATE_MISMATCH");
});

test("publish posts the ordered urlencoded form with the decoded token and repeated cgvs[]", async () => {
  stage();
  const result = await client.publishRelease({ ...identity, expectedFileId: 501, changelogHtml: CHANGELOG, compatibleVersions: COMPAT });
  assert.deepEqual(result, { assetId: 9001, releaseUrl: `${fake.origin}/edit/release/?assetid=9001` });

  const [save] = requestsTo("/edit/release/").filter((r) => r.method === "POST");
  assert.match(save.headers["content-type"], /^application\/x-www-form-urlencoded/);
  assert.equal(save.headers.cookie, `vs_websessionkey=${COOKIE}`);
  assert.deepEqual(save.fields, [
    ["at", 'tok&"<1>'],
    ["save", "1"],
    ["assetid", "0"],
    ["modid", "640"],
    ["numsaved", "0"],
    ["saveandback", "0"],
    ["text", CHANGELOG],
    ["cgvs[]", "1.22.6"],
    ["cgvs[]", "1.22.7"],
  ]);
  assert.deepEqual(fake.state.staged, []);
  assert.equal(fake.state.releases[0].tags.join(), COMPAT.join());
});

test("publish requires the owner-approved staged file and release inputs", async () => {
  const publish = (overrides) =>
    client.publishRelease({ ...identity, expectedFileId: 501, changelogHtml: CHANGELOG, compatibleVersions: COMPAT, ...overrides });
  await rejectsWith(publish({ compatibleVersions: [] }), "MODDB_COMPATIBILITY_MISSING");
  await rejectsWith(publish({ compatibleVersions: ["1.22.7", " "] }), "MODDB_COMPATIBILITY_MISSING");
  await rejectsWith(publish({ changelogHtml: " " }), "MODDB_CHANGELOG_EMPTY");
  await rejectsWith(publish({ changelogHtml: "a — b" }), "MODDB_CHANGELOG_EM_DASH");
  await rejectsWith(publish({ expectedFileId: 0 }), "MODDB_INVALID_FILE_ID");
  assert.equal(fake.requests.length, 0);

  await rejectsWith(publish(), "MODDB_STAGED_STATE_MISMATCH"); // nothing staged
  stage(502);
  await rejectsWith(publish(), "MODDB_STAGED_STATE_MISMATCH"); // different file id
  fake.state.staged = [{ fileId: 501, modIdentifier: "thebasics", version: "5.9.0", fileName: "x.zip", bytes: zipBytes }];
  await rejectsWith(publish(), "MODDB_STAGED_STATE_MISMATCH"); // different version
  fake.state.staged = [];
  stage(501);
  stage(503);
  await rejectsWith(publish(), "MODDB_STAGED_STATE_MISMATCH"); // two files
  assert.equal(requestsTo("/edit/release/").filter((r) => r.method === "POST").length, 0);
});

test("publish accepts 302 or 303 with a same-origin assetid location only", async () => {
  const publish = () => client.publishRelease({ ...identity, expectedFileId: 501, changelogHtml: CHANGELOG, compatibleVersions: COMPAT });
  stage(501);
  fake.state.saveOverride = ({ assetId }) => ({ status: 303, headers: { location: `${fake.origin}/edit/release/?assetid=${assetId}` } });
  assert.equal((await publish()).assetId, 9001);

  for (const override of [
    ({ assetId }) => ({ status: 302, headers: { location: `${fake.cdnOrigin}/edit/release/?assetid=${assetId}` } }),
    () => ({ status: 302, headers: { location: "/edit/release/?modid=640" } }),
    () => ({ status: 200 }),
    () => ({ status: 500 }),
  ]) {
    fake.state.staged = [];
    stage(501);
    fake.state.saveOverride = override;
    const error = await rejectsWith(publish(), "MODDB_PUBLISH_INDETERMINATE");
    assert.match(error.message, /readPublicState/);
  }
  assert.equal(fake.requests.filter((r) => r.server === "cdn").length, 0);
});

test("indeterminate save is checked against public state before any retry", async () => {
  stage(501);
  fake.state.saveOverride = () => ({ status: 500 });
  const publish = () => client.publishRelease({ ...identity, expectedFileId: 501, changelogHtml: CHANGELOG, compatibleVersions: COMPAT });
  await rejectsWith(publish(), "MODDB_PUBLISH_INDETERMINATE");

  const state = await client.readPublicState({ modId: MOD_ID, expectedVersion: "5.9.1" });
  assert.deepEqual(state, {
    published: true,
    releases: [{ fileId: 501, fileName: "thebasics-v5.9.1.zip", modIdentifier: "thebasics", version: "5.9.1", compatibleVersions: COMPAT }],
  });
  const [api] = requestsTo("/api/mod/640");
  assert.equal("cookie" in api.headers, false, "public reads carry no cookie");
  assert.deepEqual(await client.readPublicState({ modId: MOD_ID, expectedVersion: "5.9.2" }), { published: false, releases: [] });
  await rejectsWith(publish(), "MODDB_STAGED_STATE_MISMATCH"); // the blind retry is refused
});

test("verifyPublishedArtifact downloads without a cookie and compares the hash", async () => {
  stage(501);
  await client.publishRelease({ ...identity, expectedFileId: 501, changelogHtml: CHANGELOG, compatibleVersions: COMPAT });
  const verify = (overrides) =>
    client.verifyPublishedArtifact({ modId: MOD_ID, expectedModIdentifier: "thebasics", expectedVersion: "5.9.1", expectedSha256: artifact.sha256.toUpperCase(), compatibleVersions: ["1.22.7"], ...overrides });

  assert.deepEqual(await verify(), { verified: true, fileId: 501, sha256: artifact.sha256, downloadUrl: `${fake.origin}/download/501/thebasics-v5.9.1.zip` });
  const [download] = fake.requests.filter((r) => r.path === "/download/501/thebasics-v5.9.1.zip");
  assert.equal("cookie" in download.headers, false);
  const [cdnHit] = fake.requests.filter((r) => r.server === "cdn");
  assert.equal("cookie" in cdnHit.headers, false);

  fake.state.downloadViaCdn = false;
  assert.equal((await verify()).verified, true);

  await rejectsWith(verify({ expectedVersion: "5.9.0" }), "MODDB_PUBLIC_RELEASE_MISSING");
  await rejectsWith(verify({ expectedModIdentifier: "othermod" }), "MODDB_PUBLIC_IDENTITY_MISMATCH");
  await rejectsWith(verify({ compatibleVersions: ["1.22.7", "1.23.0"] }), "MODDB_PUBLIC_COMPATIBILITY_MISMATCH");
  await rejectsWith(verify({ expectedSha256: createHash("sha256").update("x").digest("hex") }), "MODDB_PUBLIC_HASH_MISMATCH");
  fake.state.tamperDownload = true;
  fake.state.downloadViaCdn = true;
  await rejectsWith(verify(), "MODDB_PUBLIC_HASH_MISMATCH");
});

test("network failures never carry the cookie", async () => {
  await fake.close();
  const dead = createModDbClient({ origin: fake.origin, cookieValue: COOKIE });
  await rejectsWith(dead.validateAccount("BASICBIT"), "MODDB_NETWORK_ERROR");
  stage(501);
  await rejectsWith(dead.publishRelease({ ...identity, expectedFileId: 501, changelogHtml: CHANGELOG, compatibleVersions: COMPAT }), "MODDB_NETWORK_ERROR");
  fake = await startFakeModDb({ cookieValue: COOKIE }); // afterEach closes it
});
