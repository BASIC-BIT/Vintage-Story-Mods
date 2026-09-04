import assert from "node:assert/strict";
import test, { after } from "node:test";

import {
  BrokerError,
  ExitCode,
  classifyError,
  safeFailure,
  safeResult,
  writeResult,
} from "../src/contracts.mjs";

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

function captureStream() {
  const chunks = [];
  return { chunks, stream: { write: (chunk) => chunks.push(chunk) } };
}

test("exit codes are fixed", () => {
  assert.deepEqual(ExitCode, { ok: 0, failed: 1, renewalRequired: 2, approvalRequired: 3 });
  assert.ok(Object.isFrozen(ExitCode));
});

test("safeResult wraps explicit safe data", () => {
  const data = { fileId: 123, sha256: "ab", compatibleVersions: ["1.22.6"], nested: { versionId: "v1" } };
  assert.deepEqual(safeResult("prepared", data), { ok: true, status: "prepared", data });
});

test("safeResult rejects unknown statuses and non-object data", () => {
  assert.throws(() => safeResult("done", {}), /invalid result status/);
  assert.throws(() => safeResult("valid", "text"), /forbidden result field: data/);
  assert.throws(() => safeResult("valid", ["a"]), /forbidden result field: data/);
  assert.throws(() => safeResult("valid", null), /forbidden result field: data/);
});

test("safeResult rejects credential fields", () => {
  assert.throws(
    () => safeResult("valid", { cookieValue: "fixture-cookie-never-print" }),
    /forbidden result field: cookieValue/,
  );
});

test("safeResult rejects nested and case-variant credential fields", () => {
  assert.throws(
    () => safeResult("valid", { session: { versions: [{ Token: "fixture-cookie-never-print" }] } }),
    /^Error: forbidden result field: Token$/,
  );
  assert.throws(() => safeResult("valid", { "set-cookie": "x" }), /forbidden result field: set-cookie/);
  assert.throws(() => safeResult("valid", { SECRET_STRING: "x" }), /forbidden result field: SECRET_STRING/);
  assert.throws(() => safeResult("valid", { EMAIL: "x" }), /forbidden result field: EMAIL/);
  assert.throws(() => safeResult("valid", { AUTHORIZATION: "x" }), /forbidden result field: AUTHORIZATION/);
  assert.throws(
    () => safeResult("valid", { password: "fixture-password-never-print" }),
    /forbidden result field: password/,
  );
});

test("safeResult rejects symbol keys, functions, non-identifier keys, and non-plain objects", () => {
  const hidden = Symbol("rawCookie");
  assert.throws(
    () => safeResult("valid", { evidence: { [hidden]: "fixture-cookie-never-print" } }),
    /^Error: forbidden result field: symbol$/,
  );
  assert.throws(
    () => safeResult("valid", { nested: { toJSON: () => "fixture-cookie-never-print" } }),
    /forbidden result field: toJSON/,
  );
  assert.throws(() => safeResult("valid", { when: new Date(0) }), /forbidden result field: when/);
  assert.throws(() => safeResult("valid", { versions: new Map() }), /forbidden result field: versions/);
  assert.throws(() => safeResult("valid", new (class Evidence {})()), /forbidden result field: data/);
  assert.throws(
    () => safeResult("valid", { "fixture-cookie-never-print": true }),
    /^Error: forbidden result field: non-identifier$/,
  );
});

test("safeFailure allows only the fixed stop shapes", () => {
  assert.deepEqual(safeFailure("renewal-required", "expired"), {
    ok: false,
    status: "renewal-required",
    reason: "expired",
  });
  assert.deepEqual(safeFailure("renewal-required", "authentication-failed"), {
    ok: false,
    status: "renewal-required",
    reason: "authentication-failed",
  });
  assert.deepEqual(safeFailure("approval-required", "renewed-during-publish"), {
    ok: false,
    status: "approval-required",
    reason: "renewed-during-publish",
  });
  assert.deepEqual(safeFailure("failed", "unexpected-error"), {
    ok: false,
    status: "failed",
    reason: "unexpected-error",
  });
  assert.throws(() => safeFailure("renewal-required", "renewed-during-publish"), /invalid failure reason/);
  assert.throws(() => safeFailure("approval-required", "expired"), /invalid failure reason/);
  assert.throws(() => safeFailure("failed", "cookie was fixture-cookie-never-print"), /invalid failure reason/);
  assert.throws(() => safeFailure("failed", ""), /invalid failure reason/);
  assert.throws(() => safeFailure("valid", "expired"), /invalid failure status/);
});

test("writeResult writes exactly one JSON line", () => {
  const { chunks, stream } = captureStream();
  writeResult(safeResult("valid", { versionId: "v1", list: ["a", "b"] }), stream);
  assert.equal(chunks.length, 1);
  assert.equal(chunks[0].endsWith("\n"), true);
  assert.equal(chunks[0].slice(0, -1).includes("\n"), false);
  assert.deepEqual(JSON.parse(chunks[0]), {
    ok: true,
    status: "valid",
    data: { versionId: "v1", list: ["a", "b"] },
  });
});

test("writeResult revalidates results and drops envelope extras", () => {
  const tampered = safeResult("valid", {});
  tampered.data.cookieValue = "fixture-cookie-never-print";
  const { chunks, stream } = captureStream();
  assert.throws(() => writeResult(tampered, stream), /forbidden result field: cookieValue/);
  assert.deepEqual(chunks, []);

  writeResult(
    { ok: false, status: "failed", reason: "unexpected-error", message: "fixture-cookie-never-print" },
    stream,
  );
  assert.deepEqual(JSON.parse(chunks[0]), { ok: false, status: "failed", reason: "unexpected-error" });
  assert.throws(() => writeResult({ status: "failed" }, stream), /invalid failure reason/);
  assert.throws(() => writeResult(undefined, stream), /invalid failure status/);
  assert.equal(chunks.length, 1);
});

test("classifyError maps broker errors to fixed codes", () => {
  const renewal = new BrokerError("authentication-failed", "ModDB rejected the session", {
    exitCode: ExitCode.renewalRequired,
  });
  assert.equal(renewal.message, "ModDB rejected the session");
  assert.deepEqual(classifyError(renewal), {
    exitCode: 2,
    result: { ok: false, status: "renewal-required", reason: "authentication-failed" },
  });
  const approval = new BrokerError("renewed-during-publish", "renewed", { exitCode: ExitCode.approvalRequired });
  assert.deepEqual(classifyError(approval), {
    exitCode: 3,
    result: { ok: false, status: "approval-required", reason: "renewed-during-publish" },
  });
  const generic = new BrokerError("WINCRED_READ_FAILED", "child failed");
  assert.deepEqual(classifyError(generic), {
    exitCode: 1,
    result: { ok: false, status: "failed", reason: "WINCRED_READ_FAILED" },
  });
  assert.throws(
    () => new BrokerError("bogus", "x", { exitCode: ExitCode.renewalRequired }),
    /invalid failure reason/,
  );
  assert.throws(() => new BrokerError("expired", "x", { exitCode: ExitCode.ok }), /invalid broker exit code/);
});

test("classifyError never serializes unknown errors", () => {
  const expected = { exitCode: 1, result: { ok: false, status: "failed", reason: "unexpected-error" } };
  const error = new Error("cookie=fixture-cookie-never-print");
  error.code = "ECONNRESET";
  error.response = { headers: { "set-cookie": "fixture-cookie-never-print" } };
  const classified = classifyError(error);
  assert.deepEqual(classified, expected);
  assert.equal(JSON.stringify(classified).includes("fixture-cookie"), false);
  assert.equal(JSON.stringify(classified).includes("ECONNRESET"), false);
  assert.deepEqual(classifyError("fixture-cookie-never-print"), expected);
  assert.deepEqual(classifyError({ code: "expired", exitCode: 2 }), expected);
  assert.deepEqual(classifyError(undefined), expected);
});
