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

function isTruthy(v: string | undefined) {
  const s = (v || "").trim().toLowerCase()
  return s === "1" || s === "true" || s === "yes"
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
    let serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (!picked) return "Missing server id. Set PTERO_SERVER_ID."
      serverId = picked
    }

    let r = await pteroFetch(cfg as any, "POST", `/api/client/servers/${serverId}/power`, { signal })
    if (!r.ok && r.status === 404) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (picked && picked !== serverId) {
        serverId = picked
        r = await pteroFetch(cfg as any, "POST", `/api/client/servers/${serverId}/power`, { signal })
      }
    }
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

    let serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (!picked) return "Missing server id. Set PTERO_SERVER_ID."
      serverId = picked
    }

    const file = String(args.file || "").trim()
    if (!file) return "file is required"

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

    let serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (!picked) return "Missing server id. Set PTERO_SERVER_ID."
      serverId = picked
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

    let bytes: Uint8Array
    try {
      bytes = await fs.readFile(localPath)
    } catch (e: any) {
      return JSON.stringify({ error: "Failed to read local file", message: e?.message ?? String(e), localPath }, null, 2)
    }

    // Step 1: get signed URL
    const q = encodeURIComponent(directory)
    let u = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/files/upload?directory=${q}`)
    if (!u.ok && u.status === 404) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (picked && picked !== serverId) {
        serverId = picked
        u = await pteroFetch(cfg as any, "GET", `/api/client/servers/${serverId}/files/upload?directory=${q}`)
      }
    }
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

    let serverId = String((args as any).serverId || cfg.serverId || "").trim()
    if (!serverId) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (!picked) return "Missing server id. Set PTERO_SERVER_ID."
      serverId = picked
    }

    const file = String(args.file || "").trim()
    if (!file) return "file is required"

    const content = String(args.content ?? "")

    const q = encodeURIComponent(file)
    let r = await pteroFetch(cfg as any, "POST", `/api/client/servers/${serverId}/files/write?file=${q}`, content)
    if (!r.ok && r.status === 404) {
      const picked = await tryAutoPickOnlyServerId(cfg)
      if (picked && picked !== serverId) {
        serverId = picked
        r = await pteroFetch(cfg as any, "POST", `/api/client/servers/${serverId}/files/write?file=${q}`, content)
      }
    }
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }

    return JSON.stringify({ ok: true, file }, null, 2)
  },
})
