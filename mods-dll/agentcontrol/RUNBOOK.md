# Agent Control 0.1.0 runbook

## Build and package

```powershell
dotnet test mods-dll/agentcontrol.Tests/agentcontrol.Tests.csproj -c Release
dotnet build mods-dll/agentcontrol/agentcontrol.csproj -c Release
dotnet build mods-dll/agentcontrol.sample/agentcontrol.sample.csproj -c Release
dotnet publish tools/vsctl/vsctl.csproj -c Release
```

The mod builds create:

- `mods-dll/agentcontrol/agentcontrol_0_1_0.zip`
- `mods-dll/agentcontrol.sample/agentcontrol_sample_0_1_0.zip`
- `tools/vsctl/bin/Release/net10.0/publish/vsctl.exe`

Install both zips in the disposable client's `Mods` directory and restart the client. Agent Control intentionally has no hot reload.

## Enable and inspect

1. Join only the disposable Profile2 test world.
2. Press `Ctrl+Alt+F8`. Confirm the `AGENT CONTROL: READY` HUD and mutation-grant status.
3. Run:

```powershell
tools/vsctl/bin/Release/net10.0/publish/vsctl.exe hello
tools/vsctl/bin/Release/net10.0/publish/vsctl.exe extensions
tools/vsctl/bin/Release/net10.0/publish/vsctl.exe observe
```

The hello output must report protocol `1.0`, mod `0.1.0`, game `1.22.2`, exactly six methods, and a redacted secret. Extensions must include `selection.describe`.

## Sample extension acceptance

Use `examples/selection-describe.json`:

```powershell
tools/vsctl/bin/Release/net10.0/publish/vsctl.exe execute --file mods-dll/agentcontrol/examples/selection-describe.json
```

Aim at a block and run the command. The completed action receipt must contain the selected block code and position. `selection.describe` must not appear in the RPC method list.

## `/top` acceptance

This is a disposable-server test. First enable `Teleportation.TopRequireTemporalGear`, put at least two temporal gears in a Profile2 hotbar slot, select that slot so a gear is held, and note the observation's starting gear count and chat sequence.

Completion batch (`top-complete.json`):

```json
[
  {"type":"send","text":"/top"},
  {"type":"wait_for","timeoutMs":10000,"condition":{"kind":"chat_contains","afterSequence":0,"text":"teleport"}},
  {"type":"wait_for","timeoutMs":10000,"condition":{"kind":"inventory_count","code":"game:gear-temporal","operator":"eq","count":1}}
]
```

Replace `afterSequence` and `count` with values derived from the immediately preceding `observe` receipt. Success is server-authoritative: the client mod only sends `/top` and waits for server-produced chat/inventory state.

Cancellation batch (`top-cancel.json`):

```json
[
  {"type":"send","text":"/top"},
  {"type":"wait","durationMs":1000},
  {"type":"control","controls":{"forward":true},"durationMs":5000}
]
```

Start it, then run `vsctl cancel --execution-id <id>` from a second terminal during warmup. Verify a `cancelled` receipt, unchanged temporal-gear count, server cancellation chat/log evidence, and no continued movement. Repeat using `Ctrl+Alt+F9`, then repeat by terminating the executing CLI to exercise disconnect cancellation.

## Human/in-client checks

The automated receipts do not replace checking that the HUD is conspicuous, physical input feels released immediately, movement visibly stops, and no control remains asserted after cancel, timeout, CLI disconnect, leaving the world, disabling the mod, or closing the client. External screenshot/Computer Use remains the evidence fallback in this MVP.

Do not use this runbook on a public server without separate owner and server-policy approval.
