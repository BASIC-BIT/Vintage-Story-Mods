import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { closeSync, ftruncateSync, mkdtempSync, openSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test, { after } from "node:test";

import { strToU8, zipSync } from "fflate";

import { ARTIFACT_SIZE_LIMIT, inspectArtifact } from "../src/artifact.mjs";
import { BrokerError } from "../src/contracts.mjs";

const tempDir = mkdtempSync(path.join(os.tmpdir(), "moddb-artifact-"));
after(() => rmSync(tempDir, { recursive: true, force: true }));

let counter = 0;
function writeFixture(bytes, name = `fixture-${counter++}.zip`) {
  const file = path.join(tempDir, name);
  writeFileSync(file, bytes);
  return file;
}

const modinfo = (extra = "") => strToU8(`{"modid":"thebasics","version":"5.9.1"${extra}}`);
const sha256 = (bytes) => createHash("sha256").update(bytes).digest("hex");

function rejects(fn, code) {
  let error;
  assert.throws(fn, (caught) => ((error = caught), caught instanceof BrokerError));
  assert.equal(error.code, code);
  return error;
}

test("reads exact identity and evidence", async () => {
  const zip = zipSync({ "modinfo.json": modinfo() });
  const file = writeFixture(zip, "thebasics-v5.9.1.zip");
  const evidence = await inspectArtifact(file);
  assert.deepEqual(
    [evidence.modIdentifier, evidence.version, evidence.entryCount],
    ["thebasics", "5.9.1", 1],
  );
  assert.deepEqual(evidence, {
    fileName: "thebasics-v5.9.1.zip",
    zipPath: file,
    modIdentifier: "thebasics",
    version: "5.9.1",
    sha256: sha256(zip),
    byteSize: zip.byteLength,
    entryCount: 1,
  });
});

test("counts every archive entry and accepts case-variant modinfo keys", () => {
  const zip = zipSync({
    "modinfo.json": strToU8('{"ModId":"thebasics","Version":"5.9.1","name":"x"}'),
    "assets/thebasics/lang/en.json": strToU8("{}"),
    "thebasics.dll": new Uint8Array([1, 2, 3]),
  });
  const evidence = inspectArtifact(writeFixture(zip));
  assert.equal(evidence.entryCount, 3);
  assert.equal(evidence.modIdentifier, "thebasics");
  assert.equal(evidence.version, "5.9.1");
});

test("rejects absent, nested-only, and duplicate modinfo", () => {
  rejects(() => inspectArtifact(writeFixture(zipSync({ "readme.txt": strToU8("hi") }))), "ARTIFACT_MODINFO_MISSING");
  rejects(
    () => inspectArtifact(writeFixture(zipSync({ "thebasics/modinfo.json": modinfo() }))),
    "ARTIFACT_MODINFO_MISSING",
  );
  rejects(
    () => inspectArtifact(writeFixture(zipSync({ "modinfo.json": modinfo(), "MODINFO.JSON": modinfo() }))),
    "ARTIFACT_MODINFO_DUPLICATE",
  );
});

test("rejects blank or missing modid and version", () => {
  for (const body of ['{"modid":" ","version":"1.0.0"}', '{"modid":"thebasics"}', '{"modid":"a","version":1}', "not json"]) {
    rejects(
      () => inspectArtifact(writeFixture(zipSync({ "modinfo.json": strToU8(body) }))),
      "ARTIFACT_MODINFO_INVALID",
    );
  }
});

test("compares expected identity, version, and hash", () => {
  const zip = zipSync({ "modinfo.json": modinfo() });
  const file = writeFixture(zip);
  const ok = inspectArtifact(file, { modIdentifier: "thebasics", version: "5.9.1", sha256: sha256(zip).toUpperCase() });
  assert.equal(ok.sha256, sha256(zip));
  rejects(() => inspectArtifact(file, { modIdentifier: "otherMod" }), "ARTIFACT_IDENTITY_MISMATCH");
  rejects(() => inspectArtifact(file, { version: "5.9.0" }), "ARTIFACT_IDENTITY_MISMATCH");
  rejects(() => inspectArtifact(file, { sha256: "00".repeat(32) }), "ARTIFACT_HASH_MISMATCH");
});

test("rejects missing files and corrupt archives", () => {
  rejects(() => inspectArtifact(path.join(tempDir, "missing.zip")), "ARTIFACT_NOT_FOUND");
  rejects(() => inspectArtifact(writeFixture(new Uint8Array(64))), "ARTIFACT_INVALID_ZIP");
});

test("enforces the size limit from file metadata before reading", () => {
  assert.equal(ARTIFACT_SIZE_LIMIT, 256 * 1024 * 1024);
  const file = path.join(tempDir, "huge.zip");
  const fd = openSync(file, "w");
  ftruncateSync(fd, ARTIFACT_SIZE_LIMIT + 1); // sparse: no real bytes written
  closeSync(fd);
  const error = rejects(() => inspectArtifact(file), "ARTIFACT_TOO_LARGE");
  assert.match(error.message, /256 MiB/);
});
