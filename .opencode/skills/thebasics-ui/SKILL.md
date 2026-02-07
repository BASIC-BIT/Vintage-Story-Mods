---
name: thebasics-ui
description: UI + chat rendering workflows for The BASICs (Vintage Story)
---

# The BASICs UI Skill

Purpose: make safe, low-risk UI changes in `thebasics` (chat HUD patches, nametag-adjacent rendering, cosmetic indicators), with a deterministic verify loop.

## When To Use

- You are touching `HudDialogChat`, nametag/overhead rendering, or chat UX.
- You need to add/adjust a client-side renderer (e.g. typing indicator).
- You need to debug “works on one client but not another” issues across multiple profiles/accounts.

## Guardrails (Ship-Safe)

- Never crash the client for cosmetic features; fail closed on exceptions.
- Avoid per-frame/per-tick logging; log only on state changes and gate debug behind config.
- Prefer additive protobuf changes: never renumber `[ProtoMember(n)]` IDs.

## Key Files (thebasics)

- Client chat hooks: `mods-dll/thebasics/src/ModSystems/ChatUiSystem/ChatUiSystem.cs`
- Typing indicator render: `mods-dll/thebasics/src/ModSystems/ChatUiSystem/TypingIndicatorRenderer.cs`
- Server relay + config distribution: `mods-dll/thebasics/src/ModSystems/ProximityChat/RPProximityChatSystem.cs`
- Config contract: `mods-dll/thebasics/src/Configs/ModConfig.cs`

## Typing Indicator Notes

Server config keys (in `data/ModConfig/the_basics.json` on server):

- `EnableTypingIndicator`
- `TypingIndicatorMaxRange`
- `TypingIndicatorTimeoutSeconds`
- `TypingIndicatorRequireNonEmptyText`
- `TypingIndicatorShowWhileChatFocused`
- `TypingIndicatorTextOverride`

Renderer positioning:

- Use the *target* entity’s `EntityShapeRenderer.getAboveHeadPosition(entity)` and add a small world-space Y offset to avoid nametag overlap.
- Avoid relying on mount APIs that have changed across VS versions.

## Deterministic Verify Loop

Local build/package:

- `dotnet build mods-dll/thebasics/thebasics.csproj -c Release`
- Packaging emits `mods-dll/thebasics/thebasics_VERSION.zip`.

Multi-profile local deploy:

- Set `VS_PROFILES_DIR` (e.g. `D:/Games/VSProfiles`) to deploy to multiple `Profile*/Mods` dirs.

Test server deploy:

- Prefer SFTP via `mods-dll/thebasics/scripts/package.ps1` (opt-in), or Pterodactyl `ptero_*` tools.

Wait points (human testing):

- When you need the user to perform an in-game action (connect 2 clients, open chat, etc.), use the OpenCode `question` prompt tool to pause and wait for the response.

## Log Triage

- Local profiles: use `vsdev_profiles`, `vsdev_logs_tail_latest`, `vsdev_logs_grep_latest`.
- Server: use `ptero_files_read data/Logs/server-main.log` and `ptero_files_read data/Logs/server-debug.log`.

## Future Work Queue

- Understand/replace vanilla overhead “speech text” rendering (for RP-language bubbles).
- Custom nametag rendering and nickname/nameplate consistency across clients.
- Screenshot-based verification loop (requires human session + stable capture method).
