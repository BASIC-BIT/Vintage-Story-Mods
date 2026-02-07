import { tool } from "@opencode-ai/plugin"
import fs from "node:fs/promises"
import path from "node:path"

type VsProfile = {
  name: string
  dataPath: string
  logsPath: string
  modsPath: string
  modConfigPath: string
  exists: boolean
}

async function pathExists(p: string): Promise<boolean> {
  try {
    await fs.stat(p)
    return true
  } catch {
    return false
  }
}

function safeJoin(...parts: string[]) {
  return path.normalize(path.join(...parts))
}

function defaultPrimaryDataPath(): string | null {
  const appdata = process.env.APPDATA
  if (!appdata) return null
  return safeJoin(appdata, "VintagestoryData")
}

async function discoverProfiles(): Promise<VsProfile[]> {
  const profiles: VsProfile[] = []

  const primary = defaultPrimaryDataPath()
  if (primary) {
    profiles.push({
      name: "primary",
      dataPath: primary,
      logsPath: safeJoin(primary, "Logs"),
      modsPath: safeJoin(primary, "Mods"),
      modConfigPath: safeJoin(primary, "ModConfig"),
      exists: await pathExists(primary),
    })
  }

  const extraProfilesRoot = process.env.VS_PROFILES_DIR || "D:/Games/VSProfiles"
  if (await pathExists(extraProfilesRoot)) {
    try {
      const entries = await fs.readdir(extraProfilesRoot, { withFileTypes: true })
      for (const ent of entries) {
        if (!ent.isDirectory()) continue
        const dataPath = safeJoin(extraProfilesRoot, ent.name)
        const exists = await pathExists(dataPath)

        // Heuristic: only include directories that look like VS data folders
        const logsPath = safeJoin(dataPath, "Logs")
        const modsPath = safeJoin(dataPath, "Mods")
        const modConfigPath = safeJoin(dataPath, "ModConfig")
        const looksLikeVsData = (await pathExists(logsPath)) || (await pathExists(modConfigPath))
        if (!looksLikeVsData) continue

        profiles.push({
          name: ent.name,
          dataPath,
          logsPath,
          modsPath,
          modConfigPath,
          exists,
        })
      }
    } catch {
      // ignore
    }
  }

  return profiles
}

async function resolveProfile(args: { profile?: string; profilePath?: string }): Promise<{ ok: true; profile: VsProfile } | { ok: false; error: any }> {
  const profiles = await discoverProfiles()

  if (args.profilePath) {
    const dp = path.normalize(args.profilePath)
    return {
      ok: true,
      profile: {
        name: "custom",
        dataPath: dp,
        logsPath: safeJoin(dp, "Logs"),
        modsPath: safeJoin(dp, "Mods"),
        modConfigPath: safeJoin(dp, "ModConfig"),
        exists: await pathExists(dp),
      },
    }
  }

  const prof = profiles.find((p) => p.name === (args.profile ?? "primary"))
  if (!prof) {
    return { ok: false, error: { error: "Profile not found", available: profiles.map((p) => p.name) } }
  }

  return { ok: true, profile: prof }
}

async function listFilesSorted(dir: string, limit: number): Promise<{ path: string; mtimeMs: number }[]> {
  const out: { path: string; mtimeMs: number }[] = []
  let names: string[]
  try {
    names = await fs.readdir(dir)
  } catch {
    return out
  }

  for (const name of names) {
    const p = safeJoin(dir, name)
    try {
      const st = await fs.stat(p)
      if (st.isFile()) out.push({ path: p, mtimeMs: st.mtimeMs })
    } catch {
      // ignore
    }
  }

  out.sort((a, b) => b.mtimeMs - a.mtimeMs)
  return out.slice(0, Math.max(0, limit))
}

export const profiles = tool({
  description: "Discover local Vintage Story data profiles (logs/mods/config paths)",
  args: {},
  async execute() {
    const p = await discoverProfiles()
    return JSON.stringify(p, null, 2)
  },
})

export const logs_list = tool({
  description: "List recent log files for a given VS profile (by name or path)",
  args: {
    profile: tool.schema.string().optional().describe("Profile name (e.g. 'primary' or a folder name under VS_PROFILES_DIR)"),
    profilePath: tool.schema.string().optional().describe("Explicit VS data path (folder containing Logs/ModConfig/etc.)"),
    limit: tool.schema.number().optional().describe("Max files to return (default 20)"),
  },
  async execute(args) {
    const limit = Math.floor(args.limit ?? 20)

    const profRes = await resolveProfile(args)
    if (!profRes.ok) return JSON.stringify(profRes.error, null, 2)
    const prof = profRes.profile

    const files = await listFilesSorted(prof.logsPath, limit)
    return JSON.stringify({ profile: prof, files }, null, 2)
  },
})

export const logs_latest = tool({
  description: "Get the newest log file path for a VS profile",
  args: {
    profile: tool.schema.string().optional().describe("Profile name (default: primary)"),
    profilePath: tool.schema.string().optional().describe("Explicit VS data path"),
    nameContains: tool.schema.string().optional().describe("Optional substring filter (e.g. 'client-main' or 'server-main')"),
  },
  async execute(args) {
    const profRes = await resolveProfile(args)
    if (!profRes.ok) return JSON.stringify(profRes.error, null, 2)
    const prof = profRes.profile

    const files = await listFilesSorted(prof.logsPath, 200)
    const needle = (args.nameContains ?? "").trim().toLowerCase()
    const filtered = needle ? files.filter((f) => f.path.toLowerCase().includes(needle)) : files
    const newest = filtered[0]
    if (!newest) {
      return JSON.stringify({ error: "No log files found", profile: prof }, null, 2)
    }

    return JSON.stringify({ profile: prof, newest }, null, 2)
  },
})

export const logs_tail_latest = tool({
  description: "Tail the newest log file for a VS profile",
  args: {
    profile: tool.schema.string().optional().describe("Profile name (default: primary)"),
    profilePath: tool.schema.string().optional().describe("Explicit VS data path"),
    nameContains: tool.schema.string().optional().describe("Optional substring filter (e.g. 'client-main')"),
    lines: tool.schema.number().optional().describe("Lines to return (default 200, max 2000)"),
  },
  async execute(args) {
    const latest = await logs_latest.execute({
      profile: args.profile,
      profilePath: args.profilePath,
      nameContains: args.nameContains,
    } as any)

    let parsed: any
    try {
      parsed = JSON.parse(String(latest))
    } catch {
      return String(latest)
    }

    const fp = parsed?.newest?.path
    if (!fp) {
      return JSON.stringify(parsed, null, 2)
    }

    return logs_tail.execute({ filePath: fp, lines: args.lines } as any)
  },
})

export const logs_tail = tool({
  description: "Tail a local log file (last N lines). Refuses to read .env files.",
  args: {
    filePath: tool.schema.string().describe("Absolute path to the log file"),
    lines: tool.schema.number().optional().describe("Lines to return (default 200, max 2000)"),
  },
  async execute(args) {
    const filePath = path.normalize(args.filePath)
    if (filePath.toLowerCase().includes(".env")) {
      return "Refusing to read .env files"
    }

    const lines = Math.min(2000, Math.max(1, Math.floor(args.lines ?? 200)))
    let content: string
    try {
      content = await fs.readFile(filePath, "utf8")
    } catch (e: any) {
      return `Failed to read file: ${e?.message ?? String(e)}`
    }

    const all = content.split(/\r?\n/)
    const tail = all.slice(Math.max(0, all.length - lines))
    return tail.join("\n")
  },
})
