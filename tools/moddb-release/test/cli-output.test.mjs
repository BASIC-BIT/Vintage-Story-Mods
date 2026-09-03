import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test, { after } from "node:test";
import { fileURLToPath } from "node:url";

import { strToU8, zipSync } from "fflate";

import { inspectArtifact } from "../src/artifact.mjs";
import { main } from "../src/cli.mjs";
import { ACCOUNT_SECRET_ID, SESSION_COOKIE_NAME } from "../src/config.mjs";
import { BrokerError, ExitCode } from "../src/contracts.mjs";
import { FakeSecretsManagerClient } from "./support/fake-secrets-manager.mjs";

const CLI = fileURLToPath(new URL("../src/cli.mjs", import.meta.url));
const COOKIE = "fixture-cookie-never-print";
const PASSWORD = "fixture-password-never-print";
const NEVER_PRINT = [COOKIE, PASSWORD];
const NOW = new Date("2026-09-10T12:00:00.000Z");
const tmp = os.tmpdir();
const tmpBefore = new Set(readdirSync(tmp));
const fixtureDir = mkdtempSync(path.join(tmp, "moddb-cli-"));

const transcript = []; // everything main wrote or returned in-process
after(() => {
  const created = readdirSync(tmp).filter((name) => !tmpBefore.has(name));
  const files = created.flatMap((name) => walk(path.join(tmp, name)));
  const text = [...transcript, ...files.map((file) => readFileSync(file, "latin1"))].join("\n");
  for (const fixture of NEVER_PRINT) assert.equal(text.includes(fixture), false, `${fixture} leaked`);
  rmSync(fixtureDir, { recursive: true, force: true });
});
function walk(entry) {
  try {
    if (!statSync(entry).isDirectory()) return [entry];
    return readdirSync(entry).flatMap((name) => walk(path.join(entry, name)));
  } catch {
    return [];
  }
}

function session(expiry = "2026-09-17T00:00:00.000Z") {
  return {
    schemaVersion: 1,
    cookieName: SESSION_COOKIE_NAME,
    cookieValue: COOKIE,
    capturedAt: "2026-09-03T00:00:00.000Z",
    observedCookieExpiresAt: null,
    modDbValidUntilEstimate: expiry,
    validatedAt: "2026-09-03T00:00:00.000Z",
    validatedAccount: "123",
  };
}

async function run(argv, { current = session(), validate = async () => ({}), env = {} } = {}) {
  let stored = { value: current, versionId: "v-old" };
  const client = new FakeSecretsManagerClient()
    .respond("GetSecretValueCommand", ({ SecretId }) =>
      SecretId === ACCOUNT_SECRET_ID
        ? { SecretString: JSON.stringify({ schemaVersion: 1, email: "fixture-email@example.invalid", password: PASSWORD }), VersionId: "login-v1" }
        : { SecretString: JSON.stringify(stored.value), VersionId: stored.versionId },
    )
    .respond("PutSecretValueCommand", ({ SecretString }) => ((stored = { value: JSON.parse(SecretString), versionId: "v-candidate" }), { VersionId: "v-candidate" }))
    .respond("UpdateSecretVersionStageCommand", () => ({}));
  const out = { stdout: "", stderr: "" };
  const exitCode = await main(argv, {
    secretsClient: client,
    stdin: null,
    stdout: { write: (chunk) => ((out.stdout += String(chunk)), true) },
    stderr: { write: (chunk) => ((out.stderr += String(chunk)), true) },
    isTTY: true,
    platform: "win32",
    env,
    readMaskedLine: async () => PASSWORD,
    readWinCred: async () => COOKIE,
    deleteWinCred: async () => ({ deleted: true }),
    browserRenewal: async ({ onHumanActionRequired }) => {
      onHumanActionRequired();
      return { cookieName: SESSION_COOKIE_NAME, cookieValue: `${COOKIE}-renewed`, observedCookieExpiresAt: null };
    },
    modDbFactory: () => ({ completeLoginBridge: async () => {}, validateAccount: validate, publishRelease: async () => assert.fail("publishRelease called") }),
    inspectArtifact,
    readFile: () => "Fixed the thing.",
    clock: () => NOW,
  });
  transcript.push(out.stdout, out.stderr, String(exitCode));
  return { exitCode, ...out };
}

function oneJsonLine(stdout) {
  const lines = stdout.split("\n");
  assert.equal(lines.length, 2, "exactly one line");
  assert.equal(lines[1], "");
  return JSON.parse(lines[0]);
}

test("help prints usage to stdout and exits 0", async () => {
  for (const argv of [[], ["--help"], ["-h"]]) {
    const result = await run(argv);
    assert.equal(result.exitCode, ExitCode.ok);
    assert.match(result.stdout, /session status/);
    assert.match(result.stdout, /release publish .*--expected-file-id/);
    assert.equal(result.stderr, "");
  }
});

test("a valid session is one JSON line with exit 0", async () => {
  const result = await run(["session", "status"]);
  assert.equal(result.exitCode, ExitCode.ok);
  assert.deepEqual(oneJsonLine(result.stdout), { ok: true, status: "valid", data: { versionId: "v-old", validatedAccount: "123", effectiveExpiry: "2026-09-17T00:00:00.000Z" } });
  assert.equal(result.stderr, "");
});

test("renewal-required exits 2", async () => {
  const result = await run(["session", "status"], { current: session("2026-09-09T00:00:00.000Z") });
  assert.equal(result.exitCode, ExitCode.renewalRequired);
  assert.deepEqual(oneJsonLine(result.stdout), { ok: false, status: "renewal-required", reason: "expired" });
});

test("renewal during publish exits 3 and never saves", async () => {
  const zipBytes = zipSync({ "modinfo.json": strToU8('{"modid":"thebasics","version":"5.9.1"}') });
  const zip = path.join(fixtureDir, "thebasics-v5.9.1.zip");
  writeFileSync(zip, zipBytes);
  const sha = createHash("sha256").update(zipBytes).digest("hex");
  const argv = ["release", "publish", "--mod-id", "42", "--expected-mod-identifier", "thebasics", "--expected-version", "5.9.1", "--zip", zip, "--changelog", "x", "--compatible-version", "1.21.0", "--expected-sha256", sha, "--expected-file-id", "501"];
  const result = await run(argv, { current: session("2026-09-09T00:00:00.000Z") });
  assert.equal(result.exitCode, ExitCode.approvalRequired);
  assert.deepEqual(oneJsonLine(result.stdout), { ok: false, status: "approval-required", reason: "renewed-during-publish" });
});

test("session renew exits 0 with renewed and one fixed stderr line", async () => {
  const result = await run(["session", "renew", "--expected-account", "123"], { current: session("2026-09-09T00:00:00.000Z") });
  assert.equal(result.exitCode, ExitCode.ok);
  assert.equal(oneJsonLine(result.stdout).status, "renewed");
  assert.equal(result.stderr, "Complete the reCAPTCHA in the Chrome window.\n");
});

test("broker errors exit 1 with the code as reason and nothing on stderr", async () => {
  const result = await run(["session", "renew", "--expected-account", "123"], {
    current: session("2026-09-09T00:00:00.000Z"),
    validate: async () => {
      throw new BrokerError("MODDB_ACCOUNT_MISMATCH", `not ${COOKIE}`);
    },
  });
  assert.equal(result.exitCode, ExitCode.failed);
  assert.deepEqual(oneJsonLine(result.stdout), { ok: false, status: "failed", reason: "MODDB_ACCOUNT_MISMATCH" });
  assert.equal(result.stderr, "Complete the reCAPTCHA in the Chrome window.\n");
});

test("unexpected errors collapse to a constant reason", async () => {
  const result = await run(["session", "status"], {
    validate: async () => {
      throw new Error(`boom ${COOKIE} ${PASSWORD}`);
    },
  });
  assert.equal(result.exitCode, ExitCode.failed);
  assert.deepEqual(oneJsonLine(result.stdout), { ok: false, status: "failed", reason: "unexpected-error" });
  assert.equal(result.stderr, "");
});

test("invalid arguments exit 1 without touching AWS", async () => {
  const result = await run(["release", "prepare"]);
  assert.equal(result.exitCode, ExitCode.failed);
  assert.deepEqual(oneJsonLine(result.stdout), { ok: false, status: "failed", reason: "INVALID_ARGUMENTS" });
});

// ---- the real executable -------------------------------------------------

const spawnCli = (...args) => {
  const child = spawnSync(process.execPath, [CLI, ...args], { encoding: "utf8", env: { ...process.env, AWS_EC2_METADATA_DISABLED: "true" } });
  transcript.push(child.stdout, child.stderr);
  return child;
};

test("node src/cli.mjs --help prints usage and exits 0", () => {
  const child = spawnCli("--help");
  assert.equal(child.status, 0);
  assert.match(child.stdout, /account set/);
  assert.equal(child.stderr, "");
});

test("node src/cli.mjs rejects a credential option with one JSON line and no stack trace", () => {
  const child = spawnCli("release", "prepare", "--password", PASSWORD);
  assert.equal(child.status, 1);
  assert.deepEqual(oneJsonLine(child.stdout), { ok: false, status: "failed", reason: "INVALID_ARGUMENTS" });
  assert.equal(child.stderr, "");
  assert.equal(child.stdout.includes("at "), false);
});
