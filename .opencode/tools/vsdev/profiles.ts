import fs from "node:fs/promises"
import path from "node:path"
import { defaultPrimaryDataPath, safeJoin } from "./_paths"
import { pathExists } from "./_fs"
import type { VsProfile } from "./_types"

export async function discoverProfiles(): Promise<VsProfile[]> {
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
          exists: await pathExists(dataPath),
        })
      }
    } catch {
      // ignore
    }
  }

  return profiles
}

export async function resolveProfile(args: {
  profile?: string
  profilePath?: string
}): Promise<{ ok: true; profile: VsProfile } | { ok: false; error: any }> {
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
