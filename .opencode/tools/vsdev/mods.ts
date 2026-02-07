import { safeJoin } from "./_paths"
import { walkFiles } from "./_fs"
import type { VsProfile } from "./_types"

export async function findModFiles(prof: VsProfile, query: string, limit: number) {
  const modsByServer = safeJoin(prof.dataPath, "ModsByServer")
  const files = [] as { path: string; mtimeMs: number }[]

  await walkFiles(prof.modsPath, 2, limit, files)
  await walkFiles(modsByServer, 4, limit, files)

  const q = query.trim().toLowerCase()
  const matches = files.filter((f) => f.path.toLowerCase().includes(q))
  matches.sort((a, b) => b.mtimeMs - a.mtimeMs)

  return {
    searched: { modsPath: prof.modsPath, modsByServerPath: modsByServer },
    matches: matches.slice(0, limit),
  }
}
