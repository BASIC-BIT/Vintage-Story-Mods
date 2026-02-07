import { tool } from "@opencode-ai/plugin"
import { listFilesSorted } from "./vsdev/_fs"
import { discoverProfiles, resolveProfile } from "./vsdev/profiles"
import { grepFile, newestLog, tailFile } from "./vsdev/logs"
import { findModFiles } from "./vsdev/mods"

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

    const newest = await newestLog(prof, args.nameContains)
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
    const profRes = await resolveProfile(args)
    if (!profRes.ok) return JSON.stringify(profRes.error, null, 2)
    const prof = profRes.profile

    const newest = await newestLog(prof, args.nameContains)
    if (!newest) {
      return JSON.stringify({ error: "No log files found", profile: prof }, null, 2)
    }

    return tailFile(newest.path, args.lines ?? 200)
  },
})

export const mods_find = tool({
  description: "Find matching mod files in Mods and ModsByServer for a VS profile",
  args: {
    profile: tool.schema.string().optional().describe("Profile name (default: primary)"),
    profilePath: tool.schema.string().optional().describe("Explicit VS data path"),
    query: tool.schema.string().describe("Substring to match (case-insensitive). Example: 'thebasics'"),
    limit: tool.schema.number().optional().describe("Max results (default 50, max 500)"),
  },
  async execute(args) {
    const profRes = await resolveProfile(args)
    if (!profRes.ok) return JSON.stringify(profRes.error, null, 2)
    const prof = profRes.profile

    const query = String(args.query || "").trim().toLowerCase()
    if (!query) {
      return JSON.stringify({ error: "query is required" }, null, 2)
    }

    const limit = Math.min(500, Math.max(1, Math.floor(args.limit ?? 50)))

    const res = await findModFiles(prof, query, limit)
    return JSON.stringify({ profile: prof, ...res }, null, 2)
  },
})

export const logs_tail = tool({
  description: "Tail a local log file (last N lines). Refuses to read .env files.",
  args: {
    filePath: tool.schema.string().describe("Absolute path to the log file"),
    lines: tool.schema.number().optional().describe("Lines to return (default 200, max 2000)"),
  },
  async execute(args) {
    return tailFile(String(args.filePath || ""), args.lines ?? 200)
  },
})

export const logs_grep = tool({
  description: "Search a log file for a pattern and return the last N matches",
  args: {
    filePath: tool.schema.string().describe("Absolute path to the log file"),
    pattern: tool.schema.string().describe("Substring or regex pattern"),
    regex: tool.schema.boolean().optional().describe("When true, treat pattern as regex"),
    ignoreCase: tool.schema.boolean().optional().describe("Default true"),
    maxMatches: tool.schema.number().optional().describe("Default 50, max 500"),
  },
  async execute(args) {
    return grepFile(String(args.filePath || ""), String(args.pattern || ""), {
      regex: args.regex,
      ignoreCase: args.ignoreCase,
      maxMatches: args.maxMatches,
    })
  },
})

export const logs_grep_latest = tool({
  description: "Search the newest log file for a VS profile",
  args: {
    profile: tool.schema.string().optional().describe("Profile name (default: primary)"),
    profilePath: tool.schema.string().optional().describe("Explicit VS data path"),
    nameContains: tool.schema.string().optional().describe("Optional substring filter (e.g. 'client-main')"),
    pattern: tool.schema.string().describe("Substring or regex pattern"),
    regex: tool.schema.boolean().optional().describe("When true, treat pattern as regex"),
    ignoreCase: tool.schema.boolean().optional().describe("Default true"),
    maxMatches: tool.schema.number().optional().describe("Default 50, max 500"),
  },
  async execute(args) {
    const profRes = await resolveProfile(args)
    if (!profRes.ok) return JSON.stringify(profRes.error, null, 2)
    const prof = profRes.profile

    const newest = await newestLog(prof, args.nameContains)
    if (!newest) {
      return JSON.stringify({ error: "No log files found", profile: prof }, null, 2)
    }

    return grepFile(newest.path, String(args.pattern || ""), {
      regex: args.regex,
      ignoreCase: args.ignoreCase,
      maxMatches: args.maxMatches,
    })
  },
})
