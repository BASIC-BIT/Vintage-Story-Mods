import path from "node:path"

export function safeJoin(...parts: string[]) {
  return path.normalize(path.join(...parts))
}

export function defaultPrimaryDataPath(): string | null {
  const appdata = process.env.APPDATA
  if (!appdata) return null
  return safeJoin(appdata, "VintagestoryData")
}
