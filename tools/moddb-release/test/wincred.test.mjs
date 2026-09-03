import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import test, { after, before } from "node:test";
import { fileURLToPath } from "node:url";

import { BrokerError } from "../src/contracts.mjs";
import { deleteWinCredSession, readWinCredSession } from "../src/wincred.mjs";

const FIXTURE_VALUE = "fixture-cookie-never-print";
const FIXTURE_SCRIPT = fileURLToPath(new URL("./support/wincred-fixture.ps1", import.meta.url));
const TARGET = `TheBasics.ModDb.Test.${randomBytes(8).toString("hex")}`;

// Everything the parent process writes during this file, so the after hook can
// prove the fixture value never reached stdout or stderr.
const processOutput = [];
for (const stream of [process.stdout, process.stderr]) {
  const original = stream.write.bind(stream);
  stream.write = (chunk, ...rest) => {
    processOutput.push(String(chunk));
    return original(chunk, ...rest);
  };
}

const PS_ARGS = ["-NoLogo", "-NoProfile", "-NonInteractive", "-File"];
function runFixture(args, input) {
  for (const exe of ["pwsh.exe", "powershell.exe"]) {
    const result = spawnSync(exe, [...PS_ARGS, FIXTURE_SCRIPT, ...args], { input, windowsHide: true });
    if (result.error?.code === "ENOENT") continue;
    return result;
  }
  throw new Error("no PowerShell executable found");
}
const writeFixture = (target) => assert.equal(runFixture(["-Target", target], FIXTURE_VALUE).status, 0);

// Records (and still forwards, so the runner keeps working) everything the
// parent writes while fn runs.
async function captureDuring(fn) {
  const chunks = [];
  const originals = [process.stdout.write, process.stderr.write];
  for (const stream of [process.stdout, process.stderr]) {
    const original = stream.write;
    stream.write = (chunk, ...rest) => (chunks.push(String(chunk)), original.call(stream, chunk, ...rest));
  }
  try {
    return { result: await fn(), chunks };
  } finally {
    [process.stdout.write, process.stderr.write] = originals;
  }
}

async function rejectsWith(code, fn) {
  const error = await fn().then(
    () => assert.fail("expected rejection"),
    (e) => e,
  );
  assert.ok(error instanceof BrokerError);
  assert.equal(error.code, code);
  const text = [String(error), error.stack, JSON.stringify({ ...error }), error.message].join("\n");
  assert.equal(text.includes(FIXTURE_VALUE), false);
  assert.equal(text.includes(TARGET), false);
  return error;
}

// The test runner shares this process's stdout, so silence is proven in a
// plain node child: the snippet succeeds and both parent streams stay empty.
function assertSilentInChild(snippet) {
  const child = spawnSync(
    process.execPath,
    [
      "--input-type=module",
      "-e",
      `import { deleteWinCredSession, readWinCredSession } from "./src/wincred.mjs";
${snippet}`,
      TARGET,
    ],
    { cwd: fileURLToPath(new URL("..", import.meta.url)), windowsHide: true },
  );
  assert.equal(child.status, 0);
  assert.equal(child.stdout.length, 0);
  assert.equal(child.stderr.length, 0);
}

function walk(dir, out = []) {
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch {
    return out;
  }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else if (entry.isFile()) out.push(full);
  }
  return out;
}

if (process.platform !== "win32") {
  test("wincred adapter", { skip: "win32 only" }, () => {});
} else {
  const tmpBefore = new Set(fs.readdirSync(os.tmpdir()));
  const fakeDir = fs.mkdtempSync(path.join(os.tmpdir(), "wincred-fake-"));

  before(() => writeFixture(TARGET));

  after(async () => {
    try {
      await deleteWinCredSession({ target: TARGET }).catch(() => {});
      fs.rmSync(fakeDir, { recursive: true, force: true });
      for (const name of fs.readdirSync(os.tmpdir())) {
        if (tmpBefore.has(name)) continue;
        const full = path.join(os.tmpdir(), name);
        const files = fs.statSync(full, { throwIfNoEntry: false })?.isDirectory() ? walk(full) : [full];
        for (const file of files) {
          let content = "";
          try {
            content = fs.readFileSync(file, "latin1");
          } catch {
            continue;
          }
          assert.equal(content.includes(FIXTURE_VALUE), false, `fixture value leaked into ${file}`);
        }
      }
    } finally {
      assert.equal(processOutput.join("").includes(FIXTURE_VALUE), false, "fixture reached process output");
    }
  });

  test("read returns the stored value without touching parent streams", async () => {
    const { result, chunks } = await captureDuring(() => readWinCredSession({ target: TARGET }));
    assert.equal(result, FIXTURE_VALUE);
    assert.equal(chunks.join("").includes(FIXTURE_VALUE), false);
    assertSilentInChild(
      `const value = await readWinCredSession({ target: process.argv[1] });
       process.exitCode = value === "${FIXTURE_VALUE}" ? 0 : 3;`,
    );
  });

  test("falls back to the next executable when the first is missing", async () => {
    const value = await readWinCredSession({
      target: TARGET,
      executables: ["definitely-not-a-shell-xyz.exe", "powershell.exe"],
    });
    assert.equal(value, FIXTURE_VALUE);
    await rejectsWith("WINCRED_READ_FAILED", () =>
      readWinCredSession({ target: TARGET, executables: ["definitely-not-a-shell-xyz.exe"] }),
    );
  });

  test("delete removes the credential and is idempotent", async () => {
    const { result, chunks } = await captureDuring(() => deleteWinCredSession({ target: TARGET }));
    assert.deepEqual(result, { deleted: true });
    assert.equal(chunks.join("").includes(FIXTURE_VALUE), false);
    assertSilentInChild(`await deleteWinCredSession({ target: process.argv[1] });`);
    await rejectsWith("WINCRED_READ_FAILED", () => readWinCredSession({ target: TARGET }));
    assert.deepEqual(await deleteWinCredSession({ target: TARGET }), { deleted: true });
  });

  test("reading an absent target fails with a value-free error", async () => {
    const error = await rejectsWith("WINCRED_READ_FAILED", () =>
      readWinCredSession({ target: `${TARGET}.absent` }),
    );
    assert.equal(error.message, "wincred read failed");
  });

  test("oversized child output fails", async () => {
    const adapterPath = path.join(fakeDir, "big.ps1");
    fs.writeFileSync(adapterPath, '[Console]::Out.Write("x" * 5120)\n');
    await rejectsWith("WINCRED_READ_FAILED", () => readWinCredSession({ target: TARGET, adapterPath }));
  });

  test("hanging child times out", async () => {
    const adapterPath = path.join(fakeDir, "hang.ps1");
    fs.writeFileSync(adapterPath, "Start-Sleep -Seconds 30\n");
    const started = Date.now();
    await rejectsWith("WINCRED_READ_FAILED", () =>
      readWinCredSession({ target: TARGET, adapterPath, timeoutMs: 500 }),
    );
    assert.ok(Date.now() - started < 10_000);
  });

  test("delete failures map to a fixed error", async () => {
    const adapterPath = path.join(fakeDir, "fail.ps1");
    fs.writeFileSync(adapterPath, '[Console]::Error.WriteLine("boom"); exit 1\n');
    const error = await rejectsWith("WINCRED_DELETE_FAILED", () =>
      deleteWinCredSession({ target: TARGET, adapterPath }),
    );
    assert.equal(error.message, "wincred delete failed");
    assert.equal(String(error).includes("boom"), false);
  });
}
