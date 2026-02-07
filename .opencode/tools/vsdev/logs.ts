import fs from "node:fs/promises"
import fss from "node:fs"
import readline from "node:readline"
import { listFilesSorted } from "./_fs"
import type { VsProfile } from "./_types"

export async function newestLog(prof: VsProfile, nameContains?: string) {
  const files = await listFilesSorted(prof.logsPath, 200)
  const needle = (nameContains ?? "").trim().toLowerCase()
  const filtered = needle ? files.filter((f) => f.path.toLowerCase().includes(needle)) : files
  return filtered[0] || null
}

export async function tailFile(filePath: string, lines: number) {
  if (filePath.toLowerCase().includes(".env")) {
    return "Refusing to read .env files"
  }

  const n = Math.min(2000, Math.max(1, Math.floor(lines)))
  let content: string
  try {
    content = await fs.readFile(filePath, "utf8")
  } catch (e: any) {
    return `Failed to read file: ${e?.message ?? String(e)}`
  }

  const all = content.split(/\r?\n/)
  const tail = all.slice(Math.max(0, all.length - n))
  return tail.join("\n")
}

export async function grepFile(
  filePath: string,
  pattern: string,
  opts?: {
    regex?: boolean
    ignoreCase?: boolean
    maxMatches?: number
  }
): Promise<string> {
  if (filePath.toLowerCase().includes(".env")) {
    return "Refusing to read .env files"
  }

  const pat = String(pattern || "").trim()
  if (!pat) {
    return JSON.stringify({ error: "pattern is required" }, null, 2)
  }

  const maxMatches = Math.min(500, Math.max(1, Math.floor(opts?.maxMatches ?? 50)))
  const ignoreCase = opts?.ignoreCase !== false
  const useRegex = opts?.regex === true

  let re: RegExp | null = null
  if (useRegex) {
    try {
      re = new RegExp(pat, ignoreCase ? "i" : undefined)
    } catch (e: any) {
      return JSON.stringify({ error: "invalid regex", message: e?.message ?? String(e) }, null, 2)
    }
  }

  const matches: { lineNumber: number; line: string }[] = []

  let lineNumber = 0
  try {
    const stream = fss.createReadStream(filePath, { encoding: "utf8" })
    const rl = readline.createInterface({ input: stream, crlfDelay: Infinity })

    for await (const line of rl) {
      lineNumber += 1

      const hay = ignoreCase ? line.toLowerCase() : line
      const ok = useRegex ? re!.test(line) : hay.includes(ignoreCase ? pat.toLowerCase() : pat)
      if (!ok) continue

      matches.push({ lineNumber, line })
      if (matches.length > maxMatches) {
        matches.shift()
      }
    }
  } catch (e: any) {
    return JSON.stringify({ error: "failed to grep file", message: e?.message ?? String(e) }, null, 2)
  }

  return JSON.stringify({ filePath, pattern: pat, regex: useRegex, ignoreCase, matches }, null, 2)
}
