---
name: vintage-story-agent-control
description: Operate the disposable Vintage Story QA client through Agent Control, recover its Profile2 setup, and capture native setup-wizard screenshots or sampled-pose clips.
---

Use this workflow for repeatable game-client QA in this repository. Read `AGENTS.local.md` for current machine paths and standing authorization. The skill does not grant permission to restart another server, stop another player's client, publish a baseline, or complete human QA.

## Choose the entry point

- **An existing connected session:** run `vsctl hello`, `extensions`, and `observe` with the profile's pipe before sending a batch. Discover operations on that session; an installed helper is not proof it is loaded or enabled.
- **Recover or launch Profile2:** follow [the client workflow](references/client-workflow.md#recover-and-launch-profile2). It reconstructs the helper from a pinned Git snapshot plus the tracked patch, backs up profile changes, and launches the menu before forwarding the connection.
- **Stage a BASICs build:** follow [local build and QA staging](references/client-workflow.md#build-and-stage-the-basics). Keep local builds separate from explicit staging and restart.
- **Capture screenshots, scales, or a clip:** follow [visual evidence](references/client-workflow.md#capture-and-review). Native screenshots use the BASICs wizard extension. Standalone GUI renders use the existing GuiPreview workflow.
- **Restore profile changes:** use the preparation or scale receipt as described in [restoration](references/client-workflow.md#restore-and-handoff).

## Game interactions

Agent Control exposes bounded observation and action batches through `vsctl`, over a Windows current-user named pipe. Its recovered source `<OutputRoot>/agentcontrol-source/mods-dll/agentcontrol/RUNBOOK.md` documents observing position, orientation, inventory, chat and selection; movement/look/hotbar actions; chat/commands; waits; extension invocation; cancellation and session shutdown. Read that runbook when constructing a new action batch. Before reconstruction, read it with `git show ffab5508d10153f0dce3fe0073a94b9172d3a568:mods-dll/agentcontrol/RUNBOOK.md`.

Use the existing controller and discoverable extension registry. The wizard supplies `thebasics.setup.open`, `thebasics.setup.capture`, and `thebasics.setup.poll`; capture-only sessions cannot save configuration or acknowledge invitations. Other GUIs need a purpose-built extension before they can be automated this way. Arbitrary GUI clicking, crawling and generic framebuffer screenshots are not provided by this controller.

Keep the session grant and kill switches: `Ctrl+Alt+F9` cancels controls and pending work; `Ctrl+Alt+F8` disables the session. Enable unattended startup only in the authorized disposable Profile2. Never write the in-memory session secret to an artifact.

## Evidence and completion

Record source and package hashes, profile/pipe identity, current operation discovery, action receipts, screenshot manifests and restoration receipts. Outputs belong in fresh artifact directories; script implementations belong in Git.

Image review establishes the appearance of captured samples. A sampled-pose clip does not establish real-time smoothness. Human observation, approved screenshot baselines, gameplay isolation, save/restart behavior and PostHog ingestion remain separate checks. Use the existing `human-qa` skill for owner observations.
