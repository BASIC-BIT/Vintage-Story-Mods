import fs from "node:fs/promises"
import path from "node:path"

export async function pathExists(p: string): Promise<boolean> {
  try {
    await fs.stat(p)
    return true
  } catch {
    return false
  }
}

export async function listFilesSorted(dir: string, limit: number): Promise<{ path: string; mtimeMs: number }[]> {
  const out: { path: string; mtimeMs: number }[] = []
  let names: string[]
  try {
    names = await fs.readdir(dir)
  } catch {
    return out
  }

  for (const name of names) {
    const p = path.normalize(path.join(dir, name))
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

export async function walkFiles(
  dir: string,
  maxDepth: number,
  limit: number,
  out: { path: string; mtimeMs: number }[] = [],
  depth = 0
): Promise<{ path: string; mtimeMs: number }[]> {
  if (depth > maxDepth) return out
  if (out.length >= limit) return out

  let entries: any[]
  try {
    entries = await fs.readdir(dir, { withFileTypes: true } as any)
  } catch {
    return out
  }

  for (const ent of entries) {
    if (out.length >= limit) break
    const p = path.normalize(path.join(dir, ent.name))
    try {
      if (ent.isDirectory()) {
        await walkFiles(p, maxDepth, limit, out, depth + 1)
      } else if (ent.isFile()) {
        const st = await fs.stat(p)
        out.push({ path: p, mtimeMs: st.mtimeMs })
      }
    } catch {
      // ignore
    }
  }

  return out
}
