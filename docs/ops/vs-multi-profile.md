# Vintage Story Multi-Profile (Multi-Account) Setup

This repo commonly uses multiple Vintage Story data profiles for testing (e.g. Profile2 + Profile3 for two accounts).

## Key Concept

Vintage Story supports a custom data directory via the `--dataPath` command-line option.

Each data directory is effectively its own "profile": separate mods, configs, logs, and login state.

## Example Profiles

- `D:\Games\VSProfiles\Profile2`
- `D:\Games\VSProfiles\Profile3`

Each profile will have its own:

- `Mods/`
- `ModConfig/`
- `Logs/`

## Launching The Client Into A Specific Profile

Create a shortcut (or a small `.cmd` script) that launches the game with `--dataPath`.

Example `.cmd` for Profile2:

```bat
@echo off
set "VS_EXE=D:\Games\Vintagestory\Vintagestory.exe"
"%VS_EXE%" --dataPath "D:\Games\VSProfiles\Profile2"
```

Example `.cmd` for Profile3:

```bat
@echo off
set "VS_EXE=D:\Games\Vintagestory\Vintagestory.exe"
"%VS_EXE%" --dataPath "D:\Games\VSProfiles\Profile3"
```

Notes:

- Logging into each account separately is expected: each `--dataPath` profile stores its own login.
- If a shortcut placed *inside* the profile folder "doesn't work", it is usually a quoting issue. Prefer a `.cmd` like the above.

## Optional: Auto-Connect To A Test Server

You can also provide a server address:

```bat
@echo off
set "VS_EXE=D:\Games\Vintagestory\Vintagestory.exe"
"%VS_EXE%" --dataPath "D:\Games\VSProfiles\Profile2" --connect "15.235.75.126:30000"
```

## Integrating With This Repo

- Set `VS_PROFILES_DIR` (example: `D:/Games/VSProfiles`) in your local `.env`.
- `mods-dll/thebasics/scripts/package.ps1` will deploy the built mod zip into every `Profile*/Mods` folder it finds under `VS_PROFILES_DIR`.
- OpenCode `vsdev_profiles` will discover profiles and show paths for logs/mods/config.
