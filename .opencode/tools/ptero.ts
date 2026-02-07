import { tool } from "@opencode-ai/plugin"
import fs from "node:fs/promises"
import path from "node:path"

type PteroConfig = {
  baseUrl: string
  token: string
  serverId?: string
}

async function tryLoadDotEnv(): Promise<Record<string, string>> {
  // Minimal dotenv parser. We do not support multiline values.
  // This is intentionally local-only: `.env` is gitignored.
  const fp = path.join(process.cwd(), ".env")
  try {
    const raw = await fs.readFile(fp, "utf8")
    const out: Record<string, string> = {}
    for (const line of raw.split(/\r?\n/)) {
      const trimmed = line.trim()
      if (!trimmed || trimmed.startsWith("#")) continue
      const eq = trimmed.indexOf("=")
      if (eq <= 0) continue
      const key = trimmed.slice(0, eq).trim()
      let val = trimmed.slice(eq + 1).trim()
      if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
        val = val.slice(1, -1)
      }
      if (key) out[key] = val
    }
    return out
  } catch {
    return {}
  }
}

async function getConfig(requireServerId: boolean): Promise<{ ok: true; config: PteroConfig } | { ok: false; error: string }> {
  // Prefer real environment variables, but fall back to repo-local .env.
  const dot = await tryLoadDotEnv()

  const baseUrl = (process.env.PTERO_BASE_URL || dot.PTERO_BASE_URL || "").trim().replace(/\/$/, "")
  const token = (process.env.PTERO_TOKEN || dot.PTERO_TOKEN || "").trim()
  const serverId = (process.env.PTERO_SERVER_ID || dot.PTERO_SERVER_ID || "").trim()

  const missing: string[] = []
  if (!baseUrl) missing.push("PTERO_BASE_URL")
  if (!token) missing.push("PTERO_TOKEN")
  if (requireServerId && !serverId) missing.push("PTERO_SERVER_ID")

  if (missing.length) {
    return {
      ok: false,
      error:
        "Pterodactyl configuration is missing, so this tool can’t run.\n\nMissing env vars: `" +
        missing.join("`, `") +
        "`",
    }
  }

  // This tool uses the Pterodactyl *Client* API.
  // Application API keys (`ptla_`) cannot be used on /api/client endpoints.
  if (token.startsWith("ptla_")) {
    return {
      ok: false,
      error:
        "PTERO_TOKEN looks like an *application* API key (ptla_...), but ptero_* tools use the *client* API.\n\n" +
        "Create a client key in the panel under your account API page (usually /account/api), then set PTERO_TOKEN to the ptlc_... value.",
    }
  }

  return { ok: true, config: { baseUrl, token, serverId: serverId || undefined } }
}

async function getApplicationConfig(): Promise<{ ok: true; config: PteroConfig } | { ok: false; error: string }> {
  // Prefer real environment variables, but fall back to repo-local .env.
  const dot = await tryLoadDotEnv()

  const baseUrl = (process.env.PTERO_BASE_URL || dot.PTERO_BASE_URL || "").trim().replace(/\/$/, "")
  const token = (process.env.PTERO_TOKEN_APPLICATION || dot.PTERO_TOKEN_APPLICATION || "").trim()

  const missing: string[] = []
  if (!baseUrl) missing.push("PTERO_BASE_URL")
  if (!token) missing.push("PTERO_TOKEN_APPLICATION")

  if (missing.length) {
    return {
      ok: false,
      error:
        "Pterodactyl application configuration is missing, so this tool can’t run.\n\nMissing env vars: `" +
        missing.join("`, `") +
        "`",
    }
  }

  // Application API requires a ptla_ token.
  if (!token.startsWith("ptla_")) {
    return {
      ok: false,
      error:
        "PTERO_TOKEN_APPLICATION must be an *application* API key (ptla_...).\n\n" +
        "Client keys (ptlc_...) do not work on /api/application endpoints.",
    }
  }

  return { ok: true, config: { baseUrl, token } }
}

function normalizeApplicationPath(p: string): string {
  const raw = (p || "").trim()
  if (!raw) return ""
  if (raw.startsWith("/api/application/")) return raw
  if (raw.startsWith("api/application/")) return "/" + raw
  // Convenience: allow "servers" or "servers/123".
  if (raw.startsWith("/")) return "/api/application" + raw
  return "/api/application/" + raw
}

async function pteroFetch(cfg: PteroConfig, method: string, path: string, body?: unknown) {
  const url = cfg.baseUrl + path
  const headers: Record<string, string> = {
    Authorization: `Bearer ${cfg.token}`,
    Accept: "Application/vnd.pterodactyl.v1+json",
    "Content-Type": "application/json",
  }

  const res = await fetch(url, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  const text = await res.text()
  let json: any = null
  try {
    json = text ? JSON.parse(text) : null
  } catch {
    // ignore
  }

  return {
    ok: res.ok,
    status: res.status,
    statusText: res.statusText,
    json,
    text,
  }
}

async function pteroFetchText(cfg: PteroConfig, method: string, path: string, content: string) {
  const url = cfg.baseUrl + path
  const headers: Record<string, string> = {
    Authorization: `Bearer ${cfg.token}`,
    Accept: "Application/vnd.pterodactyl.v1+json",
    "Content-Type": "text/plain; charset=utf-8",
  }

  const res = await fetch(url, {
    method,
    headers,
    body: content,
  })

  const text = await res.text()
  let json: any = null
  try {
    json = text ? JSON.parse(text) : null
  } catch {
    // ignore
  }

  return {
    ok: res.ok,
    status: res.status,
    statusText: res.statusText,
    json,
    text,
  }
}

function isTruthy(v: string | undefined) {
  const s = (v || "").trim().toLowerCase()
  return s === "1" || s === "true" || s === "yes"
}

function isPathAllowedForWrite(p: string): boolean {
  const norm = (p || "").trim().replace(/\\/g, "/")
  return (
    norm.startsWith("data/Mods") ||
    norm.startsWith("/data/Mods") ||
    norm.startsWith("data/ModConfig") ||
    norm.startsWith("/data/ModConfig")
  )
}

function looksLikeSecretPath(p: string): boolean {
  const norm = (p || "").trim().toLowerCase().replace(/\\/g, "/")
  const base = norm.split("/").pop() || norm
  if (base === ".env") return true
  if (base.endsWith(".pem") || base.endsWith(".key") || base.endsWith(".pfx")) return true
  return false
}

async function tryAutoPickOnlyServerId(cfg: PteroConfig): Promise<string | null> {
  // If the account can only see one server, we can auto-select it.
  // This avoids brittle PTERO_SERVER_ID configuration and reduces 404 failures.
  const r = await pteroFetch(cfg as any, "GET", `/api/client`)
  if (!r.ok) return null

  const servers = r.json?.data
  if (!Array.isArray(servers) || servers.length !== 1) return null

  const id = servers[0]?.attributes?.identifier
  return typeof id === "string" && id.trim() ? id.trim() : null
}

export const status = tool({
  description: "Get Pterodactyl server resource status via Client API (read-only)",
  args: {
    serverId: tool.schema.string().optional().describe("Override server identifier (defaults to PTERO_SERVER_ID)")
  },
  async execute(args) {
    const cfgRes = await getConfig(false)
    if (!cfgRes.ok) return cfgRes.error

    const cfg = cfgRes.config
    let serverId = String((args as any)?.serverId || cfg.serverId || "").trim()
    if (!serverId) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (!picked) return "Missing server id. Set PTERO_SERVER_ID."
      serverId = picked
    }

    let r = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/resources`)
    if (!r.ok && r.status === 404) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (picked && picked !== serverId) {
        serverId = picked
        r = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/resources`)
      }
    }
    if (!r.ok) {
      return JSON.stringify(
        {
          error: "Pterodactyl request failed",
          status: r.status,
          statusText: r.statusText,
          body: r.json ?? r.text,
        },
        null,
        2
      )
    }
    return JSON.stringify(r.json, null, 2)
  },
})

export const servers_list = tool({
  description: "List servers accessible via Pterodactyl Client API (read-only)",
  args: {},
  async execute() {
    const cfgRes = await getConfig(false)
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const r = await pteroFetch(cfg as any, "GET", `/api/client`)
    if (!r.ok) {
      return JSON.stringify(
        {
          error: "Pterodactyl request failed",
          status: r.status,
          statusText: r.statusText,
          body: r.json ?? r.text,
        },
        null,
        2
      )
    }
    return JSON.stringify(r.json, null, 2)
  },
})

export const power = tool({
  description: "Send a power signal (start/stop/restart/kill) to a Pterodactyl server (destructive)",
  args: {
    signal: tool.schema
      .string()
      .describe("One of: start, stop, restart, kill"),
    serverId: tool.schema.string().optional().describe("Override server identifier (defaults to PTERO_SERVER_ID)"),
    confirm: tool.schema
      .boolean()
      .optional()
      .describe("Must be true to execute"),
  },
  async execute(args) {
    const cfgRes = await getConfig(false)
    if (!cfgRes.ok) return cfgRes.error

    const dot = await tryLoadDotEnv()

    if (!isTruthy(process.env.PTERO_ALLOW_POWER || dot.PTERO_ALLOW_POWER)) {
      return "Refusing: set PTERO_ALLOW_POWER=1 to enable destructive power actions"
    }

    if (args.confirm !== true) {
      return "Refusing: pass confirm=true"
    }

    const signal = String(args.signal || "").trim()
    if (!["start", "stop", "restart", "kill"].includes(signal)) {
      return "Invalid signal. Use one of: start, stop, restart, kill"
    }

    const cfg = cfgRes.config
    // Destructive action: require an explicit server identifier.
    // Avoid auto-picking to reduce the chance of hitting the wrong environment.
    const serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      return "Missing server identifier. Set PTERO_SERVER_ID or pass serverId."
    }

    const r = await pteroFetch(cfg as any, "POST", `/api/client/servers/${serverId}/power`, { signal })
    if (!r.ok) {
      return JSON.stringify(
        {
          error: "Pterodactyl power request failed",
          status: r.status,
          statusText: r.statusText,
          body: r.json ?? r.text,
        },
        null,
        2
      )
    }

    return JSON.stringify({ ok: true, signal }, null, 2)
  },
})

export const files_list = tool({
  description: "List files in a server directory via Pterodactyl Client API (read-only)",
  args: {
    serverId: tool.schema.string().optional().describe("Override server identifier (defaults to PTERO_SERVER_ID)"),
    directory: tool.schema.string().optional().describe("Directory path (default: '/')"),
  },
  async execute(args) {
    const cfgRes = await getConfig(false)
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    let serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (!picked) return "Missing server id. Set PTERO_SERVER_ID."
      serverId = picked
    }

    const directory = (args.directory || "/").toString()
    const q = encodeURIComponent(directory)
    let r = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/files/list?directory=${q}`)
    if (!r.ok && r.status === 404) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (picked && picked !== serverId) {
        serverId = picked
        r = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/files/list?directory=${q}`)
      }
    }
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }
    return JSON.stringify(r.json, null, 2)
  },
})

export const files_read = tool({
  description: "Read a server file via Pterodactyl Client API (read-only)",
  args: {
    serverId: tool.schema.string().optional().describe("Override server identifier (defaults to PTERO_SERVER_ID)"),
    file: tool.schema.string().describe("File path to read (e.g. 'data/Logs/server-main.log')"),
    maxBytes: tool.schema.number().optional().describe("Max bytes to return (default 50000, max 200000)"),
  },
  async execute(args) {
    const cfgRes = await getConfig(false)
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const file = String(args.file || "").trim()
    if (!file) return "file is required"

    if (!isPathAllowedForWrite(file)) {
      return "Refusing: writes are restricted to data/Mods and data/ModConfig"
    }

    // Destructive action: require an explicit server identifier.
    const serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      return "Missing server identifier. Set PTERO_SERVER_ID or pass serverId."
    }

    if (looksLikeSecretPath(file)) {
      return "Refusing: file looks like a secret (.env/.pem/.key/.pfx)"
    }

    const q = encodeURIComponent(file)
    let r = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/files/contents?file=${q}`)
    if (!r.ok && r.status === 404) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (picked && picked !== serverId) {
        serverId = picked
        r = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/files/contents?file=${q}`)
      }
    }
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }

    const maxBytes = Math.min(200000, Math.max(1, Math.floor(args.maxBytes ?? 50000)))
    const text = (r.text || "").slice(-maxBytes)
    return text
  },
})

export const files_upload = tool({
  description: "Upload a local file to the server via Pterodactyl signed upload URL (destructive)",
  args: {
    localPath: tool.schema.string().describe("Absolute path to local file"),
    serverId: tool.schema.string().optional().describe("Override server identifier (defaults to PTERO_SERVER_ID)"),
    directory: tool.schema.string().optional().describe("Target directory on server (default: '/')"),
    confirm: tool.schema.boolean().optional().describe("Must be true to execute"),
  },
  async execute(args) {
    const cfgRes = await getConfig(false)
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const dot = await tryLoadDotEnv()
 
    // Destructive action: require an explicit server identifier.
    const serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      return "Missing server identifier. Set PTERO_SERVER_ID or pass serverId."
    }

    if (!isTruthy(process.env.PTERO_ALLOW_FILES || dot.PTERO_ALLOW_FILES)) {
      return "Refusing: set PTERO_ALLOW_FILES=1 to enable file uploads"
    }
    if (args.confirm !== true) {
      return "Refusing: pass confirm=true"
    }

    const localPath = path.normalize(String(args.localPath || "")).trim()
    if (!localPath) return "localPath is required"
    const directory = (args.directory || "/").toString()

    if (!isPathAllowedForWrite(directory)) {
      return "Refusing: uploads are restricted to data/Mods and data/ModConfig"
    }

    let bytes: Uint8Array
    try {
      bytes = await fs.readFile(localPath)
    } catch (e: any) {
      return JSON.stringify({ error: "Failed to read local file", message: e?.message ?? String(e), localPath }, null, 2)
    }

    // Step 1: get signed URL
    const q = encodeURIComponent(directory)
    const u = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/files/upload?directory=${q}`)
    if (!u.ok) {
      return JSON.stringify({ error: "Failed to get signed upload URL", status: u.status, statusText: u.statusText, body: u.json ?? u.text }, null, 2)
    }

    const signedUrl = u.json?.attributes?.url
    if (!signedUrl) {
      return JSON.stringify({ error: "Signed URL missing in response", body: u.json ?? u.text }, null, 2)
    }

    // Step 2: upload
    const file = new File([bytes], path.basename(localPath))

    // Some Wings setups are picky about whether `directory` is provided in the
    // query string vs the multipart form. Try both patterns.
    const attempts: Array<{ url: string; form: FormData; label: string }> = []

    // Attempt A: query param only (doc example)
    {
      const form = new FormData()
      form.append("files", file)
      attempts.push({ url: `${signedUrl}?directory=${encodeURIComponent(directory)}`, form, label: "query" })
    }

    // Attempt B: form field only (some panels expect this)
    {
      const form = new FormData()
      form.append("files", file)
      form.append("directory", directory)
      attempts.push({ url: signedUrl, form, label: "form" })
    }

    let lastErr: any = null
    for (const att of attempts) {
      const res = await fetch(att.url, { method: "POST", body: att.form })
      const text = await res.text()
      let json: any = null
      try {
        json = text ? JSON.parse(text) : null
      } catch {
        // ignore
      }

      if (res.ok) {
        return JSON.stringify({ ok: true, uploaded: path.basename(localPath), directory, status: res.status, mode: att.label }, null, 2)
      }

      lastErr = { status: res.status, statusText: res.statusText, body: json ?? text, mode: att.label }
    }

    return JSON.stringify({ error: "Upload failed", ...lastErr }, null, 2)
  },
})

export const files_write = tool({
  description: "Write file contents on the server via Pterodactyl Client API (destructive)",
  args: {
    serverId: tool.schema.string().optional().describe("Override server identifier (defaults to PTERO_SERVER_ID)"),
    file: tool.schema.string().describe("File path to write (e.g. 'data/ModConfig/the_basics.json')"),
    content: tool.schema.string().describe("Full file contents"),
    confirm: tool.schema.boolean().optional().describe("Must be true to execute"),
  },
  async execute(args) {
    const cfgRes = await getConfig(false)
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const dot = await tryLoadDotEnv()
    if (!isTruthy(process.env.PTERO_ALLOW_FILES || dot.PTERO_ALLOW_FILES)) {
      return "Refusing: set PTERO_ALLOW_FILES=1 to enable file writes/uploads"
    }
    if (args.confirm !== true) {
      return "Refusing: pass confirm=true"
    }

    const file = String(args.file || "").trim()
    if (!file) return "file is required"

    if (!isPathAllowedForWrite(file)) {
      return "Refusing: writes are restricted to data/Mods and data/ModConfig"
    }

    // Destructive action: require an explicit server identifier.
    const serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      return "Missing server identifier. Set PTERO_SERVER_ID or pass serverId."
    }

    const content = String(args.content ?? "")
 
    const q = encodeURIComponent(file)
    const r = await pteroFetchText(cfg as any, "POST", `/api/client/servers/${serverId}/files/write?file=${q}`, content)
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }

    return JSON.stringify({ ok: true, file }, null, 2)
  },
})

export const app_get = tool({
  description: "GET an endpoint via Pterodactyl Application API (read-only)",
  args: {
    path: tool.schema.string().describe("Application API path. Examples: 'servers', 'servers/1', '/api/application/servers?per_page=100'")
  },
  async execute(args) {
    const cfgRes = await getApplicationConfig()
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const dot = await tryLoadDotEnv()

    const p = normalizeApplicationPath(String((args as any).path || ""))
    if (!p) return "path is required"

    // Application tokens are powerful; restrict reads by default to reduce accidental data disclosure.
    // Allow overriding with PTERO_APP_ALLOW_ALL_READ=1.
    if (!isTruthy(process.env.PTERO_APP_ALLOW_ALL_READ || dot.PTERO_APP_ALLOW_ALL_READ)) {
      const allow = ["/api/application/servers", "/api/application/nodes", "/api/application/locations", "/api/application/nests"]
      if (!allow.some((prefix) => p.startsWith(prefix))) {
        return "Refusing: ptero_app_get is restricted by default. Set PTERO_APP_ALLOW_ALL_READ=1 to allow arbitrary application reads"
      }
    }

    const r = await pteroFetch(cfg as any, "GET", p)
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }
    return JSON.stringify(r.json, null, 2)
  },
})

export const app_request = tool({
  description: "Call Pterodactyl Application API with write methods (destructive)",
  args: {
    method: tool.schema.string().describe("HTTP method (POST, PATCH, PUT, DELETE)"),
    path: tool.schema.string().describe("Application API path. Examples: 'servers', 'servers/1'") ,
    bodyJson: tool.schema.string().optional().describe("Optional JSON string body"),
    confirm: tool.schema.boolean().optional().describe("Must be true to execute"),
  },
  async execute(args) {
    const cfgRes = await getApplicationConfig()
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const dot = await tryLoadDotEnv()
    if (!isTruthy(process.env.PTERO_APP_ALLOW_WRITE || dot.PTERO_APP_ALLOW_WRITE)) {
      return "Refusing: set PTERO_APP_ALLOW_WRITE=1 to enable application API write operations"
    }

    if ((args as any).confirm !== true) {
      return "Refusing: pass confirm=true"
    }

    const method = String((args as any).method || "").trim().toUpperCase()
    if (!method) return "method is required"
    if (method === "GET") return "Use ptero_app_get for read-only GET requests"
    if (!['POST', 'PATCH', 'PUT', 'DELETE'].includes(method)) {
      return "Invalid method. Use one of: POST, PATCH, PUT, DELETE"
    }

    const p = normalizeApplicationPath(String((args as any).path || ""))
    if (!p) return "path is required"

    // Reduce blast radius by default. This is an ops tool, but we primarily expect server orchestration.
    // If you truly need wider access (users/nodes/locations), remove or relax this guard.
    if (!p.startsWith("/api/application/servers")) {
      return "Refusing: ptero_app_request is restricted to /api/application/servers* by default"
    }

    let body: any = undefined
    const bodyJson = (args as any).bodyJson
    if (typeof bodyJson === 'string' && bodyJson.trim()) {
      try {
        body = JSON.parse(bodyJson)
      } catch (e: any) {
        return `bodyJson is not valid JSON: ${e?.message || e}`
      }
    }

    const r = await pteroFetch(cfg as any, method, p, body)
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }

    return JSON.stringify(r.json ?? { ok: true }, null, 2)
  },
})

export const app_servers_list = tool({
  description: "List servers via Pterodactyl Application API (read-only)",
  args: {
    page: tool.schema.number().optional().describe("Page number (default 1)"),
    perPage: tool.schema.number().optional().describe("Results per page (1-100, default 50)"),
    include: tool.schema.string().optional().describe("Comma-separated includes (e.g. 'user,node,allocations')"),
    filterName: tool.schema.string().optional().describe("Filter by name (maps to filter[name])"),
  },
  async execute(args) {
    const cfgRes = await getApplicationConfig()
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const page = Number.isFinite((args as any).page) ? Math.max(1, Math.floor((args as any).page)) : 1
    const perPage = Number.isFinite((args as any).perPage) ? Math.min(100, Math.max(1, Math.floor((args as any).perPage))) : 50
    const include = String((args as any).include || "").trim()
    const filterName = String((args as any).filterName || "").trim()

    const qp: string[] = [`page=${page}`, `per_page=${perPage}`]
    if (include) qp.push(`include=${encodeURIComponent(include)}`)
    if (filterName) qp.push(`filter[name]=${encodeURIComponent(filterName)}`)
    const p = `/api/application/servers?${qp.join("&")}`

    const r = await pteroFetch(cfg as any, "GET", p)
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }
    return JSON.stringify(r.json, null, 2)
  },
})

export const app_nodes_list = tool({
  description: "List nodes via Pterodactyl Application API (read-only)",
  args: {
    page: tool.schema.number().optional().describe("Page number (default 1)"),
    perPage: tool.schema.number().optional().describe("Results per page (1-100, default 50)"),
    include: tool.schema.string().optional().describe("Comma-separated includes (e.g. 'allocations,location')"),
  },
  async execute(args) {
    const cfgRes = await getApplicationConfig()
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const page = Number.isFinite((args as any).page) ? Math.max(1, Math.floor((args as any).page)) : 1
    const perPage = Number.isFinite((args as any).perPage) ? Math.min(100, Math.max(1, Math.floor((args as any).perPage))) : 50
    const include = String((args as any).include || "").trim()

    const qp: string[] = [`page=${page}`, `per_page=${perPage}`]
    if (include) qp.push(`include=${encodeURIComponent(include)}`)
    const p = `/api/application/nodes?${qp.join("&")}`

    const r = await pteroFetch(cfg as any, "GET", p)
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }
    return JSON.stringify(r.json, null, 2)
  },
})

export const app_node_allocations_list = tool({
  description: "List allocations for a node via Pterodactyl Application API (read-only)",
  args: {
    nodeId: tool.schema.number().describe("Node ID"),
    page: tool.schema.number().optional().describe("Page number (default 1)"),
    perPage: tool.schema.number().optional().describe("Results per page (1-100, default 50)"),
  },
  async execute(args) {
    const cfgRes = await getApplicationConfig()
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const nodeId = Math.floor(Number((args as any).nodeId))
    if (!Number.isFinite(nodeId) || nodeId <= 0) return "nodeId must be a positive integer"

    const page = Number.isFinite((args as any).page) ? Math.max(1, Math.floor((args as any).page)) : 1
    const perPage = Number.isFinite((args as any).perPage) ? Math.min(100, Math.max(1, Math.floor((args as any).perPage))) : 50

    const p = `/api/application/nodes/${nodeId}/allocations?page=${page}&per_page=${perPage}`
    const r = await pteroFetch(cfg as any, "GET", p)
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }
    return JSON.stringify(r.json, null, 2)
  },
})

export const app_server_clone = tool({
  description: "Clone a server via Pterodactyl Application API (destructive)",
  args: {
    sourceServerId: tool.schema.number().describe("Source server ID (numeric)"),
    newName: tool.schema.string().describe("New server name"),
    allocationDefaultId: tool.schema.number().describe("Primary allocation ID to assign to the new server"),
    copyEnvironment: tool.schema.boolean().optional().describe("When true, copies non-internal environment variables from the source server"),
    confirm: tool.schema.boolean().optional().describe("Must be true to execute"),
  },
  async execute(args) {
    const cfgRes = await getApplicationConfig()
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const dot = await tryLoadDotEnv()
    if (!isTruthy(process.env.PTERO_APP_ALLOW_WRITE || dot.PTERO_APP_ALLOW_WRITE)) {
      return "Refusing: set PTERO_APP_ALLOW_WRITE=1 to enable application API write operations"
    }

    if ((args as any).confirm !== true) {
      return "Refusing: pass confirm=true"
    }

    const sourceServerId = Math.floor(Number((args as any).sourceServerId))
    if (!Number.isFinite(sourceServerId) || sourceServerId <= 0) return "sourceServerId must be a positive integer"

    const allocationDefaultId = Math.floor(Number((args as any).allocationDefaultId))
    if (!Number.isFinite(allocationDefaultId) || allocationDefaultId <= 0) return "allocationDefaultId must be a positive integer"

    const newName = String((args as any).newName || "").trim()
    if (!newName) return "newName is required"

    const copyEnvironment = (args as any).copyEnvironment === true

    // Fetch source server details
    const sourceResp = await pteroFetch(cfg as any, "GET", `/api/application/servers/${sourceServerId}`)
    if (!sourceResp.ok) {
      return JSON.stringify({ error: "Failed to fetch source server", status: sourceResp.status, statusText: sourceResp.statusText, body: sourceResp.json ?? sourceResp.text }, null, 2)
    }

    const src = sourceResp.json?.attributes
    if (!src) {
      return JSON.stringify({ error: "Unexpected response shape from application API", body: sourceResp.json ?? sourceResp.text }, null, 2)
    }

    if (!src.limits || !src.feature_limits) {
      return JSON.stringify({ error: "Source server is missing limits/feature_limits in response; cannot clone deterministically", body: sourceResp.json ?? sourceResp.text }, null, 2)
    }

    if (!src.container || !src.container.image || !src.container.startup_command) {
      return JSON.stringify({ error: "Source server is missing container details (image/startup_command); cannot clone deterministically", body: sourceResp.json ?? sourceResp.text }, null, 2)
    }

    const payload: any = {
      name: newName,
      user: src.user,
      egg: src.egg,
      docker_image: src.container?.image,
      startup: src.container?.startup_command,
      limits: src.limits,
      feature_limits: src.feature_limits,
      allocation: { default: allocationDefaultId },
    }

    if (copyEnvironment && src.container?.environment && typeof src.container.environment === "object") {
      const envOut: Record<string, any> = {}
      for (const [k, v] of Object.entries(src.container.environment)) {
        // Skip internal/panel-provided variables.
        if (!k) continue
        if (k.startsWith("P_")) continue
        if (k === "STARTUP") continue
        envOut[k] = v
      }
      payload.environment = envOut
    }
    else if (copyEnvironment) {
      // Not all panels expose environment variables here; surface this so operators don't assume it's copied.
      return JSON.stringify({ error: "copyEnvironment=true requested, but source server environment was not present in the response", hint: "Try copyEnvironment=false or use the startup endpoint to set variables explicitly." }, null, 2)
    }

    // Create server
    const createResp = await pteroFetch(cfg as any, "POST", `/api/application/servers`, payload)
    if (!createResp.ok) {
      return JSON.stringify({ error: "Failed to create server", status: createResp.status, statusText: createResp.statusText, body: createResp.json ?? createResp.text }, null, 2)
    }

    return JSON.stringify(createResp.json, null, 2)
  },
})
