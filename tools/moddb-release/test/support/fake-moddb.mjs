// In-process stand-in for mods.vintagestory.at plus a second origin that plays
// the download CDN. It speaks the protocol the client is ported from: the
// release form with hidden inputs, the multipart upload endpoint, the
// urlencoded save that answers with a redirect, the public mod API, and the
// tracked download link that redirects off-origin. Every request is recorded
// so tests can prove which origin saw which headers and fields.
import { createServer } from "node:http";
import { createHash } from "node:crypto";
import { once } from "node:events";

import { unzipSync } from "fflate";

const AUTH_REQUIRED = "<html><body><h1>401 - You need to log in to access this page</h1></body></html>";

const escapeHtml = (value) =>
  String(value).replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

const hidden = (name, value) => `<input type="hidden" name="${escapeHtml(name)}" value="${escapeHtml(value)}">`;

async function readBody(req) {
  const chunks = [];
  for await (const chunk of req) chunks.push(chunk);
  return Buffer.concat(chunks);
}

export async function startFakeModDb({ cookieValue, accountName = "BASICBIT", actionToken = 'tok&"<1>' } = {}) {
  const requests = [];
  const state = {
    cookieValue,
    accountName,
    actionToken,
    // Login bridge (GET /login): the real ModDB only accepts a cookie after
    // this page has registered it. With requireBridge the fake does the same.
    requireBridge: false,
    bridged: new Set(), // cookie values /login has registered
    bridgeRedirect: null, // Location override for a registered cookie
    bridgeDelayMs: 0, // >0: /login answers 200 and the page redirects itself to / after the delay
    bridgeStalls: false, // /login answers 200 and never leaves
    staged: [], // { fileId, modIdentifier, version, fileName, bytes }
    releases: [], // public API rows plus bytes
    nextFileId: 501,
    nextAssetId: 9001,
    uploadOverride: null, // object -> returned verbatim as the upload JSON
    saveOverride: null, // (saved) => { status, headers } after the save is applied
    formRedirect: null, // Location for the release form GET
    stageTwice: false,
    downloadViaCdn: true,
    tamperDownload: false,
  };

  const sessionCookie = (req) =>
    (req.headers.cookie ?? "")
      .split(/;\s*/)
      .find((pair) => pair.startsWith("vs_websessionkey="))
      ?.slice("vs_websessionkey=".length);
  const isAuthenticated = (req) => sessionCookie(req) === cookieValue && (!state.requireBridge || state.bridged.has(cookieValue));

  const record = (server, req, fields) => {
    const url = new URL(req.url, "http://fake");
    requests.push({ server, method: req.method, path: url.pathname, query: Object.fromEntries(url.searchParams), headers: req.headers, fields });
  };

  const send = (res, status, body, headers = {}) => {
    res.writeHead(status, headers);
    res.end(body);
  };

  const releaseForm = () => {
    const first = state.staged[0];
    return `<html><body><form method="post">
      ${hidden("at", state.actionToken)}
      ${hidden("modidstr", first?.modIdentifier ?? "")}
      ${hidden("modversion", first?.version ?? "")}
      ${state.staged.map((file) => hidden("fileIds[]", file.fileId)).join("\n")}
      <span id="account-menu"><span>${escapeHtml(state.accountName)}</span></span>
    </form></body></html>`;
  };

  async function handleModDb(req, res) {
    const url = new URL(req.url, "http://fake");
    const body = await readBody(req);

    if (req.method === "GET" && url.pathname === "/login") {
      record("moddb", req);
      const cookie = sessionCookie(req);
      if (cookie === undefined) return send(res, 302, "", { location: `${cdnOrigin}/?loginredir=mods` }); // the real bounce goes to the account origin
      if (cookie !== cookieValue) return send(res, 401, AUTH_REQUIRED, { "content-type": "text/html" });
      state.bridged.add(cookie);
      if (state.bridgeRedirect) return send(res, 302, "", { location: state.bridgeRedirect });
      if (state.bridgeStalls) return send(res, 200, "<html><body>bridging</body></html>", { "content-type": "text/html" });
      if (state.bridgeDelayMs > 0) {
        return send(res, 200, `<html><body>bridging<script>setTimeout(() => location.replace("/"), ${state.bridgeDelayMs});</script></body></html>`, { "content-type": "text/html" });
      }
      return send(res, 302, "", { location: "/" });
    }

    if (req.method === "GET" && url.pathname === "/") {
      record("moddb", req);
      return send(res, 200, "<html><body>Mod DB</body></html>", { "content-type": "text/html" });
    }

    if (req.method === "GET" && url.pathname === "/edit/release/") {
      record("moddb", req);
      if (state.formRedirect) return send(res, 302, "", { location: state.formRedirect });
      if (!isAuthenticated(req)) return send(res, 401, AUTH_REQUIRED, { "content-type": "text/html" });
      return send(res, 200, releaseForm(), { "content-type": "text/html" });
    }

    if (req.method === "GET" && url.pathname === "/accountsettings") {
      record("moddb", req);
      if (!isAuthenticated(req)) return send(res, 401, AUTH_REQUIRED, { "content-type": "text/html" });
      const html = `<html><body><nav><span id="account-menu" class="submenu"><span>${escapeHtml(state.accountName)}</span><nav><a href="/show/user/abc">Profile</a></nav></span></nav></body></html>`;
      return send(res, 200, html, { "content-type": "text/html" });
    }

    if (req.method === "POST" && url.pathname === "/edit-uploadfile") {
      const form = await new Response(body, { headers: { "content-type": req.headers["content-type"] } }).formData();
      const file = form.get("file");
      const fields = {};
      for (const [key, value] of form.entries()) if (typeof value === "string") fields[key] = value;
      const bytes = file ? new Uint8Array(await file.arrayBuffer()) : null;
      record("moddb", req, { ...fields, file: file ? { name: file.name, type: file.type, size: file.size, sha256: createHash("sha256").update(bytes).digest("hex") } : null });
      if (!isAuthenticated(req)) return send(res, 302, "", { location: "/login" });
      if (state.uploadOverride) return send(res, 200, JSON.stringify(state.uploadOverride), { "content-type": "application/json" });
      const modinfo = JSON.parse(new TextDecoder().decode(unzipSync(bytes)["modinfo.json"]));
      const stage = () => {
        const staged = { fileId: state.nextFileId++, modIdentifier: modinfo.modid, version: modinfo.version, fileName: file.name, bytes };
        state.staged.push(staged);
        return staged;
      };
      const staged = stage();
      if (state.stageTwice) stage();
      const json = { status: "ok", errormessage: null, modparse: "ok", parsemsg: null, modid: staged.modIdentifier, modversion: staged.version, fileid: staged.fileId };
      return send(res, 200, JSON.stringify(json), { "content-type": "application/json" });
    }

    if (req.method === "POST" && url.pathname === "/edit/release/") {
      const pairs = [...new URLSearchParams(body.toString())];
      record("moddb", req, pairs);
      if (!isAuthenticated(req)) return send(res, 401, AUTH_REQUIRED);
      const get = (name) => pairs.filter(([key]) => key === name).map(([, value]) => value);
      if (get("at")[0] !== state.actionToken) return send(res, 400, "bad action token");
      if (get("save")[0] !== "1" || state.staged.length !== 1 || get("cgvs[]").length === 0) return send(res, 200, releaseForm());
      const [staged] = state.staged.splice(0);
      const assetId = state.nextAssetId++;
      state.releases.unshift({
        assetId,
        fileid: staged.fileId,
        filename: staged.fileName,
        modidstr: staged.modIdentifier,
        modversion: staged.version,
        tags: get("cgvs[]"),
        bytes: staged.bytes,
        changelog: get("text")[0],
      });
      if (state.saveOverride) {
        const { status, headers = {} } = state.saveOverride({ assetId });
        return send(res, status, "", headers);
      }
      return send(res, 302, "", { location: `/edit/release/?assetid=${assetId}` });
    }

    const apiMatch = url.pathname.match(/^\/api\/mod\/(\d+)$/);
    if (req.method === "GET" && apiMatch) {
      record("moddb", req);
      const releases = state.releases.map(({ bytes, assetId, ...row }) => ({ releaseid: assetId, mainfile: `${cdnOrigin}/cdn/${row.fileid}`, downloads: 0, created: "2026-09-03 00:00:00", ...row }));
      return send(res, 200, JSON.stringify({ statuscode: "200", mod: { modid: Number(apiMatch[1]), releases } }), { "content-type": "application/json" });
    }

    const downloadMatch = url.pathname.match(/^\/download\/(\d+)\//);
    if (req.method === "GET" && downloadMatch) {
      record("moddb", req);
      const release = state.releases.find((row) => row.fileid === Number(downloadMatch[1]));
      if (!release) return send(res, 404, "File not found.");
      if (state.downloadViaCdn) return send(res, 302, "", { location: `${cdnOrigin}/cdn/${release.fileid}` });
      return send(res, 200, Buffer.from(release.bytes), { "content-type": "application/zip" });
    }

    record("moddb", req);
    send(res, 404, "not found");
  }

  async function handleCdn(req, res) {
    await readBody(req);
    record("cdn", req);
    const match = new URL(req.url, "http://fake").pathname.match(/^\/cdn\/(\d+)$/);
    const release = match && state.releases.find((row) => row.fileid === Number(match[1]));
    if (!release) return send(res, 404, "missing");
    const bytes = Buffer.from(release.bytes);
    if (state.tamperDownload) bytes[bytes.length - 1] ^= 0xff;
    send(res, 200, bytes, { "content-type": "application/zip" });
  }

  const wrap = (handler) => (req, res) =>
    handler(req, res).catch((error) => send(res, 500, `fake failure: ${error.message}`));

  const moddb = createServer(wrap(handleModDb));
  const cdn = createServer(wrap(handleCdn));
  moddb.listen(0, "127.0.0.1");
  cdn.listen(0, "127.0.0.1");
  await Promise.all([once(moddb, "listening"), once(cdn, "listening")]);
  const origin = `http://127.0.0.1:${moddb.address().port}`;
  const cdnOrigin = `http://127.0.0.1:${cdn.address().port}`;

  return {
    origin,
    cdnOrigin,
    state,
    requests,
    close: () => Promise.all([moddb, cdn].map((server) => new Promise((resolve) => server.close(resolve)))),
  };
}
