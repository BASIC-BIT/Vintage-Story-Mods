import { tool } from "@opencode-ai/plugin"
import fs from "node:fs/promises"
import path from "node:path"

type PteroConfig = {
  baseUrl: string
  token: string
  serverId: string
}

function getConfig(): { ok: true; config: PteroConfig } | { ok: false; error: string } {
  const baseUrl = (process.env.PTERO_BASE_URL || "").trim().replace(/\/$/, "")
  const token = (process.env.PTERO_TOKEN || "").trim()
  const serverId = (process.env.PTERO_SERVER_ID || "").trim()

  const missing: string[] = []
  if (!baseUrl) missing.push("PTERO_BASE_URL")
  if (!token) missing.push("PTERO_TOKEN")
  if (!serverId) missing.push("PTERO_SERVER_ID")

  if (missing.length) {
    return {
      ok: false,
      error:
        "Missing Pterodactyl configuration env vars: " +
        missing.join(", ") +
        ". Set them in your shell (not in git).",
    }
  }

  return { ok: true, config: { baseUrl, token, serverId } }
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

export const status = tool({
  description: "Get Pterodactyl server resource status via Client API (read-only)",
  args: {},
  async execute() {
    const cfgRes = getConfig()
    if (!cfgRes.ok) return cfgRes.error

    const cfg = cfgRes.config
    const r = await pteroFetch(cfg, "GET", `/api/client/servers/${cfg.serverId}/resources`)
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
    confirm: tool.schema
      .boolean()
      .optional()
      .describe("Must be true to execute"),
  },
  async execute(args) {
    const cfgRes = getConfig()
    if (!cfgRes.ok) return cfgRes.error

    if (!isTruthy(process.env.PTERO_ALLOW_POWER)) {
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
    const r = await pteroFetch(cfg, "POST", `/api/client/servers/${cfg.serverId}/power`, { signal })
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
    directory: tool.schema.string().optional().describe("Directory path (default: '/')"),
  },
  async execute(args) {
    const cfgRes = getConfig()
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    const directory = (args.directory || "/").toString()
    const q = encodeURIComponent(directory)
    const r = await pteroFetch(cfg, "GET", `/api/client/servers/${cfg.serverId}/files/list?directory=${q}`)
    if (!r.ok) {
      return JSON.stringify({ error: "Pterodactyl request failed", status: r.status, statusText: r.statusText, body: r.json ?? r.text }, null, 2)
    }
    return JSON.stringify(r.json, null, 2)
  },
})

export const files_upload = tool({
  description: "Upload a local file to the server via Pterodactyl signed upload URL (destructive)",
  args: {
    localPath: tool.schema.string().describe("Absolute path to local file"),
    directory: tool.schema.string().optional().describe("Target directory on server (default: '/')"),
    confirm: tool.schema.boolean().optional().describe("Must be true to execute"),
  },
  async execute(args) {
    const cfgRes = getConfig()
    if (!cfgRes.ok) return cfgRes.error
    const cfg = cfgRes.config

    if (!isTruthy(process.env.PTERO_ALLOW_FILES)) {
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
    const u = await pteroFetch(cfg, "GET", `/api/client/servers/${cfg.serverId}/files/upload?directory=${q}`)
    if (!u.ok) {
      return JSON.stringify({ error: "Failed to get signed upload URL", status: u.status, statusText: u.statusText, body: u.json ?? u.text }, null, 2)
    }

    const signedUrl = u.json?.attributes?.url
    if (!signedUrl) {
      return JSON.stringify({ error: "Signed URL missing in response", body: u.json ?? u.text }, null, 2)
    }

    // Step 2: upload
    const form = new FormData()
    const file = new File([bytes], path.basename(localPath))
    form.append("files", file)
    form.append("directory", directory)

    const uploadUrl = `${signedUrl}?directory=${encodeURIComponent(directory)}`
    const res = await fetch(uploadUrl, { method: "POST", body: form })
    const text = await res.text()
    let json: any = null
    try {
      json = text ? JSON.parse(text) : null
    } catch {
      // ignore
    }

    if (!res.ok) {
      return JSON.stringify({ error: "Upload failed", status: res.status, statusText: res.statusText, body: json ?? text }, null, 2)
    }

    return JSON.stringify({ ok: true, uploaded: path.basename(localPath), directory, status: res.status }, null, 2)
  },
})
