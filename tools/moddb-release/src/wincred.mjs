// One-time Windows Credential Manager migration boundary. The checked-in
// PowerShell adapter is the only thing that touches the Win32 API; this module
// runs it with private redirected pipes and never forwards child output. Every
// failure collapses to a fixed BrokerError so no child text, exit code, target,
// or value can leak through an exception.

import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

import { WINCRED_TARGET } from "./config.mjs";
import { BrokerError } from "./contracts.mjs";

const ADAPTER_PATH = fileURLToPath(new URL("../scripts/wincred-session.ps1", import.meta.url));
const EXECUTABLES = Object.freeze(["pwsh.exe", "powershell.exe"]);
const STDOUT_CAP = 4096;
const TIMEOUT_MS = 10_000;

// Resolves the child's stdout bytes on exit 0. Rejects with a bare sentinel
// (never the child's text) on any other outcome; "ENOENT" means try the next
// executable.
function runOnce(executable, args, timeoutMs) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    let child;
    try {
      child = spawn(executable, args, {
        shell: false,
        windowsHide: true,
        stdio: ["ignore", "pipe", "pipe"],
        timeout: timeoutMs,
      });
    } catch {
      reject("spawn");
      return;
    }
    child.on("error", (error) => reject(error?.code === "ENOENT" ? "ENOENT" : "spawn"));
    child.stderr.resume();
    child.stdout.on("data", (chunk) => {
      size += chunk.length;
      if (size > STDOUT_CAP) {
        child.kill();
        reject("overflow");
        return;
      }
      chunks.push(chunk);
    });
    child.on("close", (code) => {
      if (code === 0) resolve(Buffer.concat(chunks));
      else reject("exit");
      for (const chunk of chunks) chunk.fill(0);
    });
  });
}

async function runAdapter(operation, { target, executables, adapterPath, timeoutMs }) {
  const args = ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", adapterPath, "-Operation", operation, "-Target", target];
  for (const executable of executables) {
    try {
      return await runOnce(executable, args, timeoutMs);
    } catch (reason) {
      if (reason !== "ENOENT") return null;
    }
  }
  return null;
}

function options({ target = WINCRED_TARGET, executables = EXECUTABLES, adapterPath = ADAPTER_PATH, timeoutMs = TIMEOUT_MS } = {}) {
  return { target, executables, adapterPath, timeoutMs };
}

export async function readWinCredSession(opts) {
  if (process.platform !== "win32") throw new BrokerError("WINCRED_UNSUPPORTED_PLATFORM", "wincred requires windows");
  const bytes = await runAdapter("Read", options(opts));
  if (bytes === null) throw new BrokerError("WINCRED_READ_FAILED", "wincred read failed");
  const value = bytes.toString("utf8");
  bytes.fill(0);
  return value;
}

export async function deleteWinCredSession(opts) {
  if (process.platform !== "win32") throw new BrokerError("WINCRED_UNSUPPORTED_PLATFORM", "wincred requires windows");
  const bytes = await runAdapter("Delete", options(opts));
  if (bytes === null) throw new BrokerError("WINCRED_DELETE_FAILED", "wincred delete failed");
  return { deleted: true };
}
