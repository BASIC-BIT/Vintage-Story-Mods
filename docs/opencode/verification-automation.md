# Verification Automation Ladder

Goal: shrink the human-in-the-loop step over time, without making the dev loop fragile.

## Level 0: Deterministic Human Step (Baseline)

- Use the OpenCode `question` tool as the explicit wait point.
- Ask for exactly one concrete action + one expected observation.
- Immediately pull logs (client + server) and confirm.

This is reliable and low-risk.

## Level 1: Reduce Manual Clicks

Vintage Story supports client args:

- `--dataPath <dir>`: run multiple profiles/accounts in parallel.
- `--connect <ip:port>`: auto-connect to a test server.

Combine these with `VS_PROFILES_DIR` so packaging deploys to all profiles automatically.

## Level 2: Semi-Automated Evidence Capture

If the verification requires visuals, prefer tooling that doesn’t need interacting with the window:

- Use logs/telemetry for behavioral confirmation.
- Keep optional debug renders behind config flags.

For screenshots, the game hotkey is global (F12 by default), but triggering it programmatically is OS-level automation.

## Level 3: OS-Level UI Automation (Brittle)

Options (Windows):

- AutoHotkey: can focus the window, click UI buttons, and send keys (e.g., join server, take screenshot).
- WinAppDriver / UIAutomation: heavier setup, more complex to maintain.

Tradeoffs:

- Very powerful, but sensitive to window focus, resolution, UI changes, and timing.
- Best used for repeatable "smoke tests" rather than pixel-perfect assertions.

## Level 4: In-Game API Screenshot Capture (Not Currently Exposed)

In VS 1.21, screenshot capture is implemented internally (see `SystemScreenshot`), but there is no obvious public mod API surface to trigger it directly.

If we ever pursue this, treat it as an advanced/experimental path (reflection/patching risk) and keep it out of production builds.
