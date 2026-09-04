// In-process stand-in for account.vintagestory.at plus a decoy origin that the
// renewal driver must never contact. The login page carries the real form's
// fields (email, password, hidden loginredir=mods), a "human completion"
// checkbox that plays reCAPTCHA, and a submit that POSTs to /attemptlogin.
// With ?autohuman=<ms> the page ticks the box and submits itself after a
// delay, which is how tests drive the human step inside a persistent context.
// A successful login sets the session cookie plus a decoy cookie and redirects
// to /mods (or wherever state.redirectTo points). Every request is recorded.
import { createServer } from "node:http";
import { once } from "node:events";

const escapeHtml = (value) =>
  String(value).replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

async function readBody(req) {
  const chunks = [];
  for await (const chunk of req) chunks.push(chunk);
  return Buffer.concat(chunks).toString();
}

const loginPage = (autohuman) => `<html><body>
<form id="login" method="post" action="/attemptlogin">
  <input type="email" name="email">
  <input type="password" name="password">
  <input type="hidden" name="loginredir" value="mods">
  <input type="hidden" name="filledBeforeHuman" id="filled-before-human" value="">
  <label><input type="checkbox" id="human-done" name="humandone" value="1"> I am human</label>
  <button type="submit">Login</button>
</form>
<script>
  const humanDone = () => {
    const form = document.getElementById("login");
    document.getElementById("filled-before-human").value = form.email.value && form.password.value ? "1" : "0";
    document.getElementById("human-done").checked = true;
    form.submit();
  };
  ${autohuman === null ? "" : `setTimeout(humanDone, ${autohuman});`}
</script>
</body></html>`;

export async function startFakeAccountServer({ email, password, cookieValue, accountName = "BASICBIT" }) {
  const requests = [];
  const state = { redirectTo: "/mods", loginRedirectTo: null, cookieMaxAge: null };

  const record = (server, req, fields) => {
    const url = new URL(req.url, "http://fake");
    requests.push({ server, method: req.method, path: url.pathname, query: Object.fromEntries(url.searchParams), headers: req.headers, fields });
  };
  const send = (res, status, body, headers = {}) => {
    res.writeHead(status, headers);
    res.end(body);
  };
  const hasSession = (req) => (req.headers.cookie ?? "").split(/;\s*/).includes(`vs_websessionkey=${cookieValue}`);

  async function handleAccount(req, res) {
    const url = new URL(req.url, "http://fake");
    const body = await readBody(req);

    if (req.method === "GET" && url.pathname === "/") {
      record("account", req);
      if (state.loginRedirectTo) return send(res, 302, "", { location: state.loginRedirectTo });
      const autohuman = url.searchParams.has("autohuman") ? Number(url.searchParams.get("autohuman")) : null;
      return send(res, 200, loginPage(autohuman), { "content-type": "text/html" });
    }

    if (req.method === "POST" && url.pathname === "/attemptlogin") {
      const fields = Object.fromEntries(new URLSearchParams(body));
      record("account", req, fields);
      if (fields.email !== email || fields.password !== password || fields.humandone !== "1") return send(res, 200, "login failed");
      const maxAge = state.cookieMaxAge === null ? "" : `; Max-Age=${state.cookieMaxAge}`;
      return send(res, 302, "", {
        location: state.redirectTo,
        "set-cookie": [`vs_websessionkey=${cookieValue}; Path=/; HttpOnly${maxAge}`, "decoy_cookie=decoy-never-capture; Path=/"],
      });
    }

    if (req.method === "GET" && url.pathname === "/mods") {
      record("account", req);
      return send(res, 200, "<html><body>Mod DB</body></html>", { "content-type": "text/html" });
    }

    if (req.method === "GET" && url.pathname === "/accountsettings") {
      record("account", req);
      if (!hasSession(req)) return send(res, 401, "<html><body><h1>401 - You need to log in to access this page</h1></body></html>", { "content-type": "text/html" });
      return send(res, 200, `<html><body><span id="account-menu"><span>${escapeHtml(accountName)}</span></span></body></html>`, { "content-type": "text/html" });
    }

    record("account", req);
    send(res, 404, "not found");
  }

  async function handleDecoy(req, res) {
    const body = await readBody(req);
    record("decoy", req, body);
    send(res, 200, "<html><body>decoy</body></html>", { "content-type": "text/html" });
  }

  const wrap = (handler) => (req, res) => handler(req, res).catch((error) => send(res, 500, `fake failure: ${error.message}`));
  const account = createServer(wrap(handleAccount));
  const decoy = createServer(wrap(handleDecoy));
  account.listen(0, "127.0.0.1");
  decoy.listen(0, "localhost"); // a different cookie host, so the session cookie is never shared with it
  await Promise.all([once(account, "listening"), once(decoy, "listening")]);

  return {
    origin: `http://127.0.0.1:${account.address().port}`,
    decoyOrigin: `http://localhost:${decoy.address().port}`,
    state,
    requests,
    close: () => Promise.all([account, decoy].map((server) => new Promise((resolve) => server.close(resolve)))),
  };
}
