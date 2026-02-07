import { tool } from "@opencode-ai/plugin"

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
    Accept: "application/json",
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

    const allow = (process.env.PTERO_ALLOW_POWER || "").trim().toLowerCase()
    if (!(allow === "1" || allow === "true" || allow === "yes")) {
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
