// Local evidence about a release ZIP, gathered before any network access.
import { createHash } from "node:crypto";
import { readFileSync, statSync } from "node:fs";
import path from "node:path";

import { unzipSync } from "fflate";

import { BrokerError } from "./contracts.mjs";

export const ARTIFACT_SIZE_LIMIT = 256 * 1024 * 1024;

const MODINFO = /^modinfo\.json$/i;
const nonblank = (value) => typeof value === "string" && value.trim() !== "";

// ModDB's own parser reads modinfo keys case-insensitively, so accept that too.
function modinfoField(modinfo, name) {
  const key = Object.keys(modinfo).find((candidate) => candidate.toLowerCase() === name);
  return key === undefined ? undefined : modinfo[key];
}

export function inspectArtifact(zipPath, expected = {}) {
  const resolved = path.resolve(zipPath);
  let stats;
  try {
    stats = statSync(resolved);
  } catch {
    throw new BrokerError("ARTIFACT_NOT_FOUND", `release zip not found: ${resolved}`);
  }
  if (!stats.isFile()) throw new BrokerError("ARTIFACT_NOT_FOUND", `release zip is not a file: ${resolved}`);
  if (stats.size > ARTIFACT_SIZE_LIMIT) {
    throw new BrokerError("ARTIFACT_TOO_LARGE", `release zip exceeds the 256 MiB limit (${stats.size} bytes)`);
  }

  const bytes = readFileSync(resolved);
  const sha256 = createHash("sha256").update(bytes).digest("hex");

  const names = [];
  let extracted;
  try {
    extracted = unzipSync(bytes, {
      filter: (entry) => {
        names.push(entry.name);
        return MODINFO.test(entry.name);
      },
    });
  } catch {
    throw new BrokerError("ARTIFACT_INVALID_ZIP", `release zip could not be parsed: ${resolved}`);
  }
  const rootMatches = names.filter((name) => MODINFO.test(name));
  if (rootMatches.length > 1) throw new BrokerError("ARTIFACT_MODINFO_DUPLICATE", "release zip contains more than one root modinfo.json");
  if (rootMatches.length === 0) {
    const nested = names.some((name) => MODINFO.test(path.posix.basename(name)));
    throw new BrokerError("ARTIFACT_MODINFO_MISSING", nested ? "modinfo.json is only nested inside a folder, not at the zip root" : "release zip has no root modinfo.json");
  }

  let modinfo;
  try {
    modinfo = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(extracted[rootMatches[0]]));
  } catch {
    throw new BrokerError("ARTIFACT_MODINFO_INVALID", "modinfo.json is not valid JSON");
  }
  const modIdentifier = modinfo && typeof modinfo === "object" ? modinfoField(modinfo, "modid") : undefined;
  const version = modinfo && typeof modinfo === "object" ? modinfoField(modinfo, "version") : undefined;
  if (!nonblank(modIdentifier) || !nonblank(version)) {
    throw new BrokerError("ARTIFACT_MODINFO_INVALID", "modinfo.json must contain nonblank modid and version");
  }

  if (
    (expected.modIdentifier !== undefined && expected.modIdentifier !== modIdentifier) ||
    (expected.version !== undefined && expected.version !== version)
  ) {
    throw new BrokerError(
      "ARTIFACT_IDENTITY_MISMATCH",
      `release zip contains ${modIdentifier} ${version}, expected ${expected.modIdentifier ?? modIdentifier} ${expected.version ?? version}`,
    );
  }
  if (expected.sha256 !== undefined && String(expected.sha256).toLowerCase() !== sha256) {
    throw new BrokerError("ARTIFACT_HASH_MISMATCH", `release zip SHA-256 ${sha256} does not match the expected hash`);
  }

  return {
    fileName: path.basename(resolved),
    zipPath: resolved,
    modIdentifier,
    version,
    sha256,
    byteSize: stats.size,
    entryCount: names.length,
  };
}
