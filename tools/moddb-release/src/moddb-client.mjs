// The ModDB release protocol, ported behavior-for-behavior from the proven
// PowerShell automation. The session cookie lives only inside the closure
// created by createModDbClient and is attached only to requests that resolve
// to the configured origin. Errors carry HTTP status codes, expected public
// identity, and same-origin URLs; never headers or bodies.
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";

import { parse } from "parse5";

import { MODDB_ORIGIN, SESSION_COOKIE_NAME } from "./config.mjs";
import { BrokerError, ExitCode } from "./contracts.mjs";

const fail = (code, message) => new BrokerError(code, message);
const authFailed = () =>
  new BrokerError("authentication-failed", "ModDB did not accept the session", { exitCode: ExitCode.renewalRequired });

const LOGIN_REQUIRED = /401\s*-\s*You need to log in/;
const nonblank = (value) => typeof value === "string" && value.trim() !== "";
const positiveInt = (value) => Number.isSafeInteger(value) && value > 0;
const sha256 = (bytes) => createHash("sha256").update(bytes).digest("hex");

// ---- HTML: the little we need from parse5 -------------------------------

function walk(node, visit) {
  visit(node);
  for (const child of node.childNodes ?? []) walk(child, visit);
}

const attr = (node, name) => node.attrs?.find((a) => a.name === name)?.value;

function textOf(node) {
  let text = "";
  walk(node, (n) => {
    if (n.nodeName === "#text") text += n.value;
  });
  return text.trim();
}

// parse5 already HTML-decodes attribute values and text.
function readHtml(html) {
  const document = parse(html);
  const inputs = [];
  let accountMenu;
  walk(document, (node) => {
    if (node.nodeName === "input" && attr(node, "name") !== undefined) inputs.push({ name: attr(node, "name"), value: attr(node, "value") ?? "" });
    if (attr(node, "id") === "account-menu") accountMenu = node;
  });
  const input = (name) => inputs.find((i) => i.name === name)?.value;
  const inputsNamed = (name) => inputs.filter((i) => i.name === name).map((i) => i.value);
  // Header template: <span id="account-menu"><span>{username}</span><nav>...</nav></span>
  const accountName = accountMenu?.childNodes?.find((n) => n.nodeName === "span");
  return { input, inputsNamed, accountName: accountName ? textOf(accountName) : undefined };
}

// ---- Client --------------------------------------------------------------

export function createModDbClient({ origin = MODDB_ORIGIN, cookieValue, fetchImpl = fetch } = {}) {
  let parsedOrigin;
  try {
    parsedOrigin = new URL(origin);
  } catch {
    throw fail("MODDB_INVALID_ORIGIN", "ModDB origin is not a URL");
  }
  if (parsedOrigin.origin !== origin || !/^https?:$/.test(parsedOrigin.protocol)) {
    throw fail("MODDB_INVALID_ORIGIN", `ModDB origin must be a bare http(s) origin, got ${origin}`);
  }
  if (!nonblank(cookieValue)) throw fail("MODDB_COOKIE_MISSING", "ModDB session cookie value is blank");

  const resolve = (target) => {
    const url = new URL(target, origin);
    if (url.origin !== origin) throw fail("MODDB_CROSS_ORIGIN", `refusing to contact ${url.origin}; only ${origin} is allowed`);
    return url;
  };

  // Every request funnels through here. Cookie only for the exact origin,
  // redirects never followed, and a transport failure is reported without
  // the request that caused it.
  async function request(target, { authenticated = true, ...init } = {}) {
    const url = resolve(target);
    const headers = new Headers(init.headers);
    if (authenticated) headers.set("cookie", `${SESSION_COOKIE_NAME}=${cookieValue}`);
    try {
      return await fetchImpl(url, { ...init, headers, redirect: "manual" });
    } catch {
      throw fail("MODDB_NETWORK_ERROR", `network error while requesting ${url.pathname}`);
    }
  }

  // Shared read of an authenticated HTML page: resolves auth failures the way
  // ModDB reports them (401 body/status, or a bounce to /login) and returns
  // the parsed page on 200 only.
  async function readAuthenticatedPage(target, unavailableCode) {
    const response = await request(target);
    const location = response.headers.get("location") ?? "";
    if ([301, 302, 303, 307, 308].includes(response.status) && new URL(location, origin).pathname === "/login") throw authFailed();
    const html = await response.text();
    if (response.status === 401 || LOGIN_REQUIRED.test(html)) throw authFailed();
    if (response.status !== 200) throw fail(unavailableCode, `ModDB returned HTTP ${response.status} for ${target}`);
    return readHtml(html);
  }

  const assertModId = (modId) => {
    if (!positiveInt(modId)) throw fail("MODDB_INVALID_MOD_ID", "modId must be a positive integer");
  };

  const assertArtifactIdentity = (artifact, expectedModIdentifier, expectedVersion) => {
    if (artifact?.modIdentifier !== expectedModIdentifier || artifact?.version !== expectedVersion) {
      throw fail("ARTIFACT_IDENTITY_MISMATCH", `local artifact is not ${expectedModIdentifier} ${expectedVersion}`);
    }
  };

  const formPath = (modId) => `/edit/release/?modid=${modId}`;

  async function readReleaseForm(modId) {
    const page = await readAuthenticatedPage(formPath(modId), "MODDB_FORM_UNAVAILABLE");
    const actionToken = page.input("at");
    if (!nonblank(actionToken)) throw fail("MODDB_FORM_TOKEN_MISSING", "ModDB release form has no action token");
    const fileIds = page.inputsNamed("fileIds[]").map((value) => (/^\d+$/.test(value) ? Number(value) : NaN));
    if (fileIds.some((id) => !positiveInt(id))) throw fail("MODDB_STAGED_STATE_MISMATCH", "ModDB release form lists a non-numeric staged file id");
    return {
      actionToken,
      staged: { modIdentifier: page.input("modidstr") ?? "", version: page.input("modversion") ?? "", fileIds },
    };
  }

  const stagedIsEmpty = ({ modIdentifier, version, fileIds }) => fileIds.length === 0 && !nonblank(modIdentifier) && !nonblank(version);

  function assertStagedExactly(staged, expectedModIdentifier, expectedVersion, expectedFileId) {
    const ok = staged.modIdentifier === expectedModIdentifier && staged.version === expectedVersion && staged.fileIds.length === 1 && staged.fileIds[0] === expectedFileId;
    if (!ok) {
      throw fail(
        "MODDB_STAGED_STATE_MISMATCH",
        `ModDB staged state is not exactly one file ${expectedFileId} for ${expectedModIdentifier} ${expectedVersion} (found ${staged.fileIds.length} staged file(s))`,
      );
    }
  }

  async function readPublicState({ modId, expectedVersion }) {
    assertModId(modId);
    if (!nonblank(expectedVersion)) throw fail("MODDB_PUBLIC_VERSION_MISSING", "expectedVersion is blank");
    const response = await request(`/api/mod/${modId}`, { authenticated: false });
    if (response.status !== 200) throw fail("MODDB_PUBLIC_STATE_UNAVAILABLE", `ModDB public API returned HTTP ${response.status}`);
    let body;
    try {
      body = JSON.parse(await response.text());
    } catch {
      throw fail("MODDB_PUBLIC_STATE_UNAVAILABLE", "ModDB public API response was not JSON");
    }
    const rows = Array.isArray(body?.mod?.releases) ? body.mod.releases : [];
    const releases = rows
      .filter((row) => row?.modversion === expectedVersion)
      .map((row) => ({
        fileId: Number(row.fileid),
        fileName: String(row.filename ?? ""),
        modIdentifier: String(row.modidstr ?? ""),
        version: String(row.modversion),
        compatibleVersions: Array.isArray(row.tags) ? row.tags.map(String) : [],
      }));
    return { published: releases.length > 0, releases };
  }

  return {
    // Uses /accountsettings: it is the only page that both requires a login
    // (401 otherwise) and needs no mod id, and its header renders the
    // logged-in username inside #account-menu.
    async validateAccount(expectedAccount) {
      if (!nonblank(expectedAccount)) throw fail("MODDB_ACCOUNT_MISSING", "expected account is blank");
      const page = await readAuthenticatedPage("/accountsettings", "MODDB_ACCOUNT_PAGE_UNAVAILABLE");
      if (page.accountName !== expectedAccount) throw fail("MODDB_ACCOUNT_MISMATCH", `ModDB session is not the expected account ${expectedAccount}`);
      return { account: expectedAccount };
    },

    async prepareRelease({ modId, artifact, expectedModIdentifier, expectedVersion }) {
      assertModId(modId);
      assertArtifactIdentity(artifact, expectedModIdentifier, expectedVersion);
      const bytes = readFileSync(artifact.zipPath);
      if (sha256(bytes) !== artifact.sha256) throw fail("ARTIFACT_HASH_MISMATCH", "release zip changed since it was inspected");

      const before = await readReleaseForm(modId);
      if (!stagedIsEmpty(before.staged)) {
        throw fail("MODDB_STAGED_FILE_EXISTS", `ModDB already has ${before.staged.fileIds.length || "a"} staged upload(s); inspect or remove it before preparing`);
      }

      const form = new FormData();
      form.append("upload", "1");
      form.append("assettypeid", "2");
      form.append("assetid", "0");
      form.append("modId", String(modId));
      form.append("file", new File([bytes], artifact.fileName, { type: "application/zip" }));
      const response = await request("/edit-uploadfile", { method: "POST", body: form });
      if ([301, 302, 303, 307, 308].includes(response.status) || response.status === 401) throw authFailed();
      if (response.status !== 200) throw fail("MODDB_UPLOAD_FAILED", `ModDB upload returned HTTP ${response.status}`);
      let result;
      try {
        result = JSON.parse(await response.text());
      } catch {
        throw fail("MODDB_UPLOAD_INVALID_RESPONSE", "ModDB upload response was not JSON");
      }
      if (result?.status !== "ok") throw fail("MODDB_UPLOAD_REJECTED", "ModDB rejected the upload");
      if (result.modparse !== "ok") throw fail("MODDB_UPLOAD_PARSE_FAILED", "ModDB could not parse the uploaded mod");
      if (result.modid !== expectedModIdentifier || result.modversion !== expectedVersion) {
        throw fail("MODDB_UPLOAD_IDENTITY_MISMATCH", `ModDB parsed a different identity than ${expectedModIdentifier} ${expectedVersion}`);
      }
      const fileId = typeof result.fileid === "string" && /^\d+$/.test(result.fileid) ? Number(result.fileid) : result.fileid;
      if (!positiveInt(fileId)) throw fail("MODDB_UPLOAD_FILE_ID_INVALID", "ModDB upload did not return a positive file id");

      const after = await readReleaseForm(modId);
      assertStagedExactly(after.staged, expectedModIdentifier, expectedVersion, fileId);
      return { fileId, modIdentifier: after.staged.modIdentifier, version: after.staged.version };
    },

    async publishRelease({ modId, artifact, expectedModIdentifier, expectedVersion, expectedFileId, changelogHtml, compatibleVersions }) {
      assertModId(modId);
      if (!positiveInt(expectedFileId)) throw fail("MODDB_INVALID_FILE_ID", "expectedFileId must be a positive integer");
      if (!Array.isArray(compatibleVersions) || compatibleVersions.length === 0 || !compatibleVersions.every(nonblank)) {
        throw fail("MODDB_COMPATIBILITY_MISSING", "at least one nonblank compatible Vintage Story version is required");
      }
      if (!nonblank(changelogHtml)) throw fail("MODDB_CHANGELOG_EMPTY", "changelog is empty");
      if (changelogHtml.includes("—")) throw fail("MODDB_CHANGELOG_EM_DASH", "public release notes must not contain U+2014");
      assertArtifactIdentity(artifact, expectedModIdentifier, expectedVersion);

      const form = await readReleaseForm(modId);
      assertStagedExactly(form.staged, expectedModIdentifier, expectedVersion, expectedFileId);

      const fields = new URLSearchParams();
      fields.append("at", form.actionToken);
      fields.append("save", "1");
      fields.append("assetid", "0");
      fields.append("modid", String(modId));
      fields.append("numsaved", "0");
      fields.append("saveandback", "0");
      fields.append("text", changelogHtml);
      for (const version of compatibleVersions) fields.append("cgvs[]", version);

      const indeterminate = (detail) =>
        fail("MODDB_PUBLISH_INDETERMINATE", `ModDB did not confirm the save (${detail}); call readPublicState before any retry`);
      let response;
      try {
        response = await request(formPath(modId), { method: "POST", body: fields });
      } catch {
        throw indeterminate("network error after POST");
      }
      if (response.status !== 302 && response.status !== 303) throw indeterminate(`HTTP ${response.status}`);
      let releaseUrl;
      try {
        releaseUrl = resolve(response.headers.get("location") ?? "");
      } catch {
        throw indeterminate("redirect left the ModDB origin");
      }
      const assetId = Number(releaseUrl.searchParams.get("assetid"));
      if (!positiveInt(assetId)) throw indeterminate("redirect had no assetid");
      return { assetId, releaseUrl: releaseUrl.href };
    },

    // Public, unauthenticated view of what ModDB serves for this mod. Use it
    // after an indeterminate save and before any retry.
    readPublicState,

    async verifyPublishedArtifact({ modId, expectedModIdentifier, expectedVersion, expectedSha256, compatibleVersions = [] }) {
      const { releases } = await readPublicState({ modId, expectedVersion });
      if (releases.length !== 1) throw fail("MODDB_PUBLIC_RELEASE_MISSING", `ModDB lists ${releases.length} public release(s) for ${expectedModIdentifier} ${expectedVersion}, expected one`);
      const [release] = releases;
      if (release.modIdentifier !== expectedModIdentifier) throw fail("MODDB_PUBLIC_IDENTITY_MISMATCH", `public release is not ${expectedModIdentifier}`);
      const missing = compatibleVersions.filter((version) => !release.compatibleVersions.includes(version));
      if (missing.length > 0) throw fail("MODDB_PUBLIC_COMPATIBILITY_MISMATCH", `public release lacks compatible version(s) ${missing.join(", ")}`);
      if (!positiveInt(release.fileId) || !nonblank(release.fileName)) throw fail("MODDB_PUBLIC_RELEASE_MISSING", "public release has no downloadable file");

      // Same-origin tracked download link; ModDB answers with a redirect to
      // its CDN. No cookie is ever attached, so following is safe.
      const downloadUrl = resolve(`/download/${release.fileId}/${encodeURIComponent(release.fileName)}`);
      let response;
      try {
        response = await fetchImpl(downloadUrl, { redirect: "follow" });
      } catch {
        throw fail("MODDB_NETWORK_ERROR", `network error while downloading ${downloadUrl.pathname}`);
      }
      if (response.status !== 200) throw fail("MODDB_PUBLIC_DOWNLOAD_FAILED", `public download returned HTTP ${response.status}`);
      const digest = sha256(new Uint8Array(await response.arrayBuffer()));
      if (digest !== String(expectedSha256).toLowerCase()) throw fail("MODDB_PUBLIC_HASH_MISMATCH", `public artifact SHA-256 ${digest} does not match the expected hash`);
      return { verified: true, fileId: release.fileId, sha256: digest, downloadUrl: downloadUrl.href };
    },
  };
}
