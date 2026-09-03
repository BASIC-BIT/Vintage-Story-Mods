# Vintage Story Client Agent Control: Research and Design

Date: 2026-07-25

Status: owner-review design; no production mod has been implemented

Tested local baseline: Vintage Story 1.22.2 Stable, The BASICs 5.8.1

## Executive verdict

A client-side control substrate is feasible. It should be designed as simple,
open, owner-trusted power tooling for Vintage Story, not permanently constrained
to a release-QA instrument. Disposable local QA is the first proving ground
because it supplies concrete success criteria and low-consequence iteration,
not because QA defines the eventual capability boundary.

The useful design is a three-layer ladder:

1. a small set of composable native primitives plus trusted native extensions,
   dispatched on the Vintage Story client/game thread where required;
2. structured client-visible observations, screenshots and UI inspection, with
   semantic UI actions first and bounded coordinate clicking for gaps;
3. external Computer Use only when neither native layer covers the task.

The controller should not grow into a workflow engine, policy language,
classifier, or giant catalog of feature-specific QA commands. The calling agent
should compose the behavior. The game-side core only needs a typed
request/response envelope, observations, a few input/time/batch primitives,
game-thread dispatch, cooperative cancellation/timeouts, and receipts.

The clean native escape hatch is a small shared extension contract implemented
by trusted compiled C# assemblies. An ordinary Vintage Story client mod can
register named delegates through that contract, and the controller can
optionally load an explicitly selected extension assembly with a custom
`AssemblyLoadContext`. Both discovery paths expose the same invocation surface.
Extensions receive `ICoreClientAPI` and can run arbitrary trusted game/client
code; the host does not need to anticipate every future operation.

On Windows, the canonical game endpoint should remain a per-user named pipe
carrying versioned JSON-RPC. A CLI and MCP stdio bridge are thin clients of the
same endpoint. An optional broker can later add loopback Streamable HTTP,
profile multiplexing and window capture without moving game semantics out of
the mod.

The smallest coherent MVP is one client-only controller, one CLI, basic
observation/input/batch calls, the shared extension contract, and one compiled
sample extension proving access to a Vintage Story client API that is not
hard-coded into the core. The current `/top` temporal-gear flow is one initial
acceptance test alongside an extension-registration/invocation test, not the product
scope.

## Scope and non-goals

This proposal is for an open local control substrate operated by its owner.
Initial development and acceptance happen in disposable/local QA. It is not:

- a way to bypass server authority, privileges, reach, cooldowns, or mod rules;
- a claim that client-visible state is authoritative server state;
- an automatic right to operate on someone else's multiplayer server;
- a hostile-code sandbox: loaded extensions are explicitly trusted native code;
- a generic shell, filesystem service, credential broker, or network proxy;
- a replacement for human visual judgment;
- permission to mutate the current test server or begin manual QA.

The production implementation remains gated on owner review of this packet.

“Open” here means a documented host-neutral JSON protocol, a small public C#
extension contract, discoverable extension operations, and sample/template code
that any trusted local agent or mod can use. It does not mean coupling the core
to one LLM vendor or requiring controller releases for every new operation.

## Owner direction and superseded constraints

The following conclusions from the first version of this packet are explicitly
superseded by owner direction:

| Previous constraint | Revised direction |
| --- | --- |
| Permanently define the product as a private/disposable QA instrument | QA is the first target and proving ground; the product is an open, trusted Vintage Story control substrate. |
| Ban arbitrary C# or native extension execution | Permit explicitly trusted compiled client/game extensions through a small host contract. Do not misrepresent in-process code as sandboxed. |
| Grow only a closed, allowlisted catalog of typed actions | Keep a few stable primitives; let extensions register new operations without changing the controller core. |
| Treat public/multiplayer denial and per-command policy as central architecture | Keep clear server-policy caveats and conservative defaults, but make connection policy owner-configurable rather than hard-wired into the substrate. |
| Require a capability/policy DSL and tightly classified predicates for every operation | Use a simple session capability set and typed envelopes. Extensions own their argument validation and report their effects. |
| Position `/top` as the scope-defining proof | Use `/top` as one initial acceptance test; separately prove the general native-extension surface. |
| Defer all native extension mechanisms indefinitely | Include one extension contract and one compiled-extension discovery path in the MVP. |

Still in force are the layered fallback order, loopback/local transport,
explicit enablement, session secret, game-thread marshaling, cancellation,
timeouts, receipts/logging, conspicuous activity/kill switch, and honest
client-versus-server authority boundaries.

## Evidence method and limitation

The feasibility findings below were checked against:

- repository source and QA/release documentation in this checkout;
- the installed 1.22.2 client and its two local test profiles;
- the installed `VintagestoryAPI.xml` reference documentation;
- official Vintage Story API documentation and modding wiki;
- primary papers and official project repositories for the Minecraft systems;
- the in-progress `/top` temporal-gear branch, read-only.

During investigation, `ilspycmd` was invoked as a command-line decompiler. It
returned exit code 1 while emitting partial output on several calls, one call
timed out after 30 seconds, and the process appears to have caused visible ILSpy
error windows. Standard error had been suppressed in those calls, so the exact
dialog text was not captured. No `ilspycmd` process remained after the failure,
and no further ILSpy calls were made. Consequently:

- public API claims in this packet rely on the shipped XML docs, public
  signatures, repository code, and official documentation;
- no claim depending only on a decompiled 1.22.2 internal implementation is
  treated as verified;
- native screenshot capture and generic UI-tree enumeration remain explicitly
  unverified instead of being inferred from internals.

This limitation does not block the proposed proof, which deliberately uses
public APIs and an external screenshot fallback.

## Prior-work inventory

### Repository and release tooling

The repository already has the surrounding mechanics that make a local QA
controller useful:

| Area | Current evidence | Design consequence |
| --- | --- | --- |
| Build/package | `mods-dll/thebasics/scripts/build-and-package.ps1` and `package.ps1` | The controller can be built as a separate client-only mod without changing The BASICs packaging. |
| Automated checks | GitHub Actions for build, release, CodeQL and infrastructure; unit-test projects for The BASICs and DimensionLib | Add dispatcher/timing/schema unit tests locally and to normal build CI later; do not pretend they replace in-game QA. |
| Manual release QA | `mods-dll/thebasics/docs/RELEASE_SMOKE_TEST.md` and the `human-qa` skill | Generate numbered, concrete cards and receipts that a human can audit. |
| Server evidence | `fetch-logs.ps1`, boot/load checks, server main/debug/chat/audit logs | Server logs remain an independent oracle where authorized; the client mod must not expose server files. |
| Client QA | Profile2 auto-connects; Profile3 is a second/manual client | The broker may eventually multiplex two explicit profile endpoints, but the proof should control only one profile at a time. |
| Client artifact hygiene | Release workflow compares local profile zip hashes with the freshly built zip | Handshake should report controller build hash and profile identity to reduce stale-client false failures. |

The installed game is `D:\Games\Vintagestory`, and recent client logs identify
it as Vintage Story 1.22.2 Stable. Both Profile2 and Profile3 currently contain
the same locally packaged mod set, including `thebasics_5_8_1.zip`.

Profile3 contains simple built-in command macros for Mapping QA. These show that
command sequences are already useful in testing, but they do not provide
deadlines, cancellation, preconditions, observations, or receipts.

### Existing client hooks

The most direct precedent is `mods/autorun/src/Main.cs`. It is a client-side
`ModSystem` that registers a hotkey and sets:

```csharp
_api.World.Player.WorldData.EntityControls.Forward = true;
```

That verifies the basic native-control route. It also demonstrates what the new
engine must fix: a one-way boolean assignment has no duration, ownership,
disconnect release, receipt, or deterministic terminal state.

Pocket Dimensions and DimensionLib already use normal client-side primitives
such as `ICoreClientAPI`, hotkeys, dialogs, HUD rendering, player state, and
`RegisterGameTickListener`. No generic agent-control or client automation
harness was found. Mapping QA is present as a packaged QA mod/macros in the
profiles, not as a general controller source in this checkout.

An older Profile3 incident in
`docs/agent-context/2026-05-05-issue-125-config-ui-dev-tracker.md` is relevant:
a broken key mapping could crash before the target mod loaded, and a stale or
incorrect profile mod path confused results. The controller must therefore
report game/profile/build/session identity and fail closed when no world/player
is ready.

### Basic Life and sandbox context

No earlier Vintage Story native-control design was found in the relevant Basic
Life or sandbox material. The transferable local pattern is one canonical
runtime with thin global agent skills/adapters, rather than separate behavioral
implementations for Codex, Claude, and OpenClaw. That principle is adopted here,
but the game-side runtime remains deliberately smaller than a general agent
gateway.

### Current `/top` work

The concrete feature under QA is in the separate
`codex/top-temporal-gear` worktree at commit `a370844`, read-only during this
research.

The branch adds `TopRequireTemporalGear` and arranges `/top` as follows:

1. the server finds a safe destination;
2. the server validates the temporal gear requirement;
3. a warmup begins;
4. movement, damage, interaction, death, or disconnect can cancel it;
5. gear is consumed only when the teleport completes;
6. the server performs the teleport and applies cooldown.

This is a strong design case because the client can initiate and observe the
workflow, while the authority and final mutation correctly remain server-side.
The `/top` acceptance test must use the normal server command path rather than
faking success by setting client coordinates.

The existing server warmup uses a 100 ms game-tick listener but wall-clock
`DateTime.UtcNow` deadlines. That is evidence about the feature under test, not
a pattern for the controller. The controller's minimal tick driver should use a
monotonic clock.

## Closest Minecraft research

### Closest match: GITM

The closest paper to the recalled “text or structured interface instead of raw
keyboard/mouse” system is **Ghost in the Minecraft: Generally Capable Agents for
Open-World Environments via Large Language Models with Text-based Knowledge and
Memory (GITM)**, arXiv:2305.17144.

[GITM paper](https://arxiv.org/abs/2305.17144)

The match is evidentiary, not just thematic. GITM frames the problem as mapping
long-horizon goals to lowest-level keyboard/mouse operations, then introduces
structured actions and text observations. Its action feedback includes current
inventory/environment and action success or failure. The paper's ablations
attribute meaningful performance gains to the structured action layer.

Transferable principles:

- give the reasoning agent named, bounded actions rather than continuous key
  ownership;
- return explicit success/failure and updated state after each action;
- make action sequences compositional;
- keep world knowledge/strategy outside the low-level executor;
- use the action layer to turn ambiguous embodied behavior into inspectable
  transactions.

The proposed Vintage Story controller applies these principles at two levels:
stable primitives remain finite and typed, while trusted C# extensions can add
new semantic operations without expanding the core. Server-visible mutations
remain explicit in calls and receipts.

### Adjacent systems

| System | What it contributes | Why it is not the closest match |
| --- | --- | --- |
| [Voyager](https://arxiv.org/abs/2305.16291) | Temporally extended compositional skills, iterative feedback, environment state and self-verification; its Mineflayer code-as-action space is highly influential. | It emphasizes an autonomous generated-code loop. This design permits owner-trusted compiled extensions but keeps compilation/loading explicit and outside ordinary action calls. |
| [Mineflayer API](https://github.com/PrismarineJS/mineflayer/blob/master/docs/api.md) | Practical high-level controls, state, chat, slots, looking, interaction and explicit control-state clearing. | It is a Minecraft protocol bot library rather than the recalled paper, and its server connection model does not map directly to a Vintage Story client mod. |
| [Project Malmo](https://www.microsoft.com/en-us/research/publication/malmo-platform-artificial-intelligence-experimentation/) | A game mod plus language-independent agent interface, scenario abstraction and systematic experiments. | It is primarily an AI experimentation platform rather than an LLM structured-action system. Its mod/interface separation is nevertheless an architectural precedent. |
| [MineDojo](https://arxiv.org/abs/2206.08853) | A broad task/environment suite and multimodal knowledge substrate. | It is closer to a benchmark and learning environment than a local deterministic QA-control surface. |
| [STEVE](https://arxiv.org/abs/2311.15209) | Vision, language planning, and a retrievable action-codebase hierarchy. | Its visual encoder and executable skill approach are broader and less deterministic than needed here. |
| [Mindcraft](https://github.com/mindcraft-bots/mindcraft) | A real Mineflayer/LLM agent and a concrete warning about generated code on public servers. | It is an implementation ecosystem, not the closest paper. Its warning supports making trusted-code and server-policy boundaries conspicuous rather than pretending arbitrary native code is harmless. |

The important synthesis is therefore **GITM's structured action/feedback
interface + Voyager's compositional skills + Malmo's language-independent
game-side adapter**. Unlike the initial packet, the synthesis now includes an
explicit owner-trusted native extension escape hatch.

## Verified Vintage Story API feasibility

Primary references:

- [EntityControls](https://apidocs.vintagestory.at/api/Vintagestory.API.Common.EntityControls.html)
- [IInputAPI](https://apidocs.vintagestory.at/api/Vintagestory.API.Client.IInputAPI.html)
- [ICoreClientAPI](https://apidocs.vintagestory.at/api/Vintagestory.API.Client.ICoreClientAPI.html)
- [IEventAPI](https://apidocs.vintagestory.at/api/Vintagestory.API.Common.IEventAPI.html)
- [IClientEventAPI](https://apidocs.vintagestory.at/api/Vintagestory.API.Client.IClientEventAPI.html)
- [IPlayerInventoryManager](https://apidocs.vintagestory.at/api/Vintagestory.API.Common.IPlayerInventoryManager.html)
- [IGuiAPI](https://apidocs.vintagestory.at/api/Vintagestory.API.Client.IGuiAPI.html)
- [GuiComposer](https://apidocs.vintagestory.at/api/Vintagestory.API.Client.GuiComposer.html)
- [server/client considerations](https://wiki.vintagestory.at/Modding%3AServer-Client_Considerations)

### Native action surfaces

The public surface supports the proof:

- `EntityControls` exposes public control properties including forward,
  backward, left, right, jump, sneak, sprint, left mouse and right mouse.
- `IInputAPI.MouseYaw` and `MousePitch` are settable.
- `IClientPlayer.CameraYaw` and `CameraPitch` are settable.
- `IPlayer.InventoryManager.ActiveHotbarSlotNumber` is settable.
- `ICoreClientAPI.SendChatMessage` sends chat text or a slash command through
  the normal client/server path.
- `IEventAPI.EnqueueMainThreadTask` safely marshals work to the main thread.
- `RegisterGameTickListener` runs callbacks on fixed ticks; the documentation
  warns a callback may be slightly late, which is why terminal conditions must
  compare against a monotonic deadline rather than count ticks.
- client events expose received chat and active-slot changes.

Movement and interaction should set ordinary client controls, not call internal
world-mutation functions. That keeps normal game hooks, packets, reach checks,
latency, and server validation in the path.

### Observation surfaces

The public client API can expose:

- own position, orientation, motion, health and selected slot when available;
- own client-available inventories and active/offhand slots;
- current block/entity selection;
- received chat captured after the controller was enabled;
- loaded chunks/entities already present in the client cache;
- currently opened/loaded GUI objects and their public metadata;
- controller/session status and action receipts.

Every response must label its scope `client_visible`. Loaded entities are a
cache, not an omniscient world query. The service must not imply access to:

- unloaded or hidden entities/blocks;
- another player's private inventory;
- server-only configuration, privileges, logs, claims or mod state;
- authoritative completion merely because the client predicted it;
- any private server data not deliberately synchronized to this client.

### UI feasibility boundary

`IGuiAPI` exposes opened/loaded GUIs and window bounds. `GuiDialog` has public
mouse event methods, and `GuiComposer` can find an element when its stable key is
already known.

However, the installed public API does not expose a clean generic enumeration of
all interactive/static composer-element dictionaries. A universal accessibility
tree should therefore not be promised.

The supported UI order should be:

1. a registered semantic extension operation such as
   `thebasics.config.set("TopRequireTemporalGear", true)`;
2. a known dialog/composer element action whose key and contract are owned;
3. a bounded click in the game-window coordinate system;
4. external Computer Use.

Named UI adapters must be registered and versioned. A reflective crawler over
private GUI fields would be brittle across game versions, so it should be an
extension-owned experiment rather than a promise made by the stable core.

### Screenshot boundary

No public screenshot API was found in the installed 1.22.2 API XML. Native
in-process screenshot capture is therefore **unverified**.

For the initial acceptance pass, screenshots can be captured by the existing external
Computer Use/window-capture path and correlated to the receipt ID. A later local
broker may use Windows Graphics Capture for the selected Vintage Story window,
with strict pixel/byte/rate limits. Calling a private game screenshot method by
reflection is a high-maintenance extension option, not a stable core primitive.

## Native extension architecture

### Runtime facts

The installed Vintage Story 1.22.2 client runs on `net10.0` with
`Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App` 10.0.0. The repository
code mods also target `net10.0`. The install includes Roslyn compiler assemblies
`Microsoft.CodeAnalysis.dll` and `Microsoft.CodeAnalysis.CSharp.dll` 4.14, but
does not include the `Microsoft.CodeAnalysis.CSharp.Scripting` assembly.

Vintage Story already has a compiled extension model: client-side code mods with
`ModSystem.StartClientSide(ICoreClientAPI)` and `Dispose()`. Code-mod metadata
can declare `side: "Client"` and dependencies.

- [Vintage Story `ModSystem`](https://apidocs.vintagestory.at/api/Vintagestory.API.Common.ModSystem.html)
- [Vintage Story code-mod metadata](https://wiki.vintagestory.at/Modding%3AModinfo)
- [.NET plugin tutorial](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)
- [.NET assembly unloadability](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability)

These facts favor compiled extensions over an embedded eval language.

### Serious options

| Option | How it works | Game-thread and lifecycle reality | Version-drift behavior | Verdict |
| --- | --- | --- | --- | --- |
| Ordinary Vintage Story client mod registers delegates | Another code mod references a tiny controller contract and calls `Register` during `StartClientSide`; Vintage Story owns loading and lifecycle. | Registration occurs in normal mod lifecycle. Invocations are dispatched by the controller on the game thread. Cleanup unregisters delegates in `Dispose`. | Best fit: normal mod/game dependency metadata, compile-time API checking, and familiar VS packaging. Requires a client restart to update. | Primary extension path |
| Controller loads a compiled extension assembly | Owner explicitly selects a DLL/package; a custom collectible `AssemblyLoadContext` plus `AssemblyDependencyResolver` loads it and finds `IAgentExtension`. | Host calls `Initialize` and registered short delegates on the game thread. Long work must return a cooperative tick operation or compose host primitives. Live unload is not guaranteed. | Good compile-time checking against an exact SDK/game version; private dependencies can be isolated. Host contract and Vintage Story API assemblies must resolve from the default context to preserve type identity. | Natural second iteration if restart/package friction is real |
| Reflection over arbitrary type/method names | Request supplies assembly/type/member and arguments; host invokes it. | Can call on game thread, but overload selection, object conversion, lifetime, and effects are ambiguous. | Very brittle: names/signatures/private members drift silently. Receipts cannot describe intent well. | Do not make this the public escape hatch; reflection may be used inside a trusted extension |
| Embedded Roslyn C# scripting/eval | Add scripting packages, compile source strings in process, expose globals such as `ICoreClientAPI`. | Compilation can run off-thread, but executed script with game APIs must marshal to the game thread. Cancellation is cooperative; a blocking script can freeze the client. Roslyn is not a security sandbox. | Convenient for experiments but references/imports and game internals drift; adds dependencies not shipped by the game. Script caching and diagnostics become another subsystem. | Defer until compiled extensions prove insufficient |
| Compile outside the game, then load | CLI creates/builds a small extension project against the exact controller SDK and VS dependencies, then asks the host to load it or instructs a restart. | Keeps compiler, package restore and diagnostics out of the game process; execution is identical to compiled extension loading. | Strong: normal C# compiler errors expose drift before load. Rebuild per supported game/API version. | Recommended authoring workflow for the secondary path |

An out-of-process plugin cannot directly call `ICoreClientAPI`; it can only call
the same RPC surface. That is useful for untrusted computation but is not a
native escape hatch. Conversely, .NET explicitly warns that untrusted code
cannot be safely loaded into a trusted process. This proposal embraces trusted
local code and does not claim `AssemblyLoadContext` is a sandbox.

### One contract, two discovery paths

The host SDK should be a tiny stable assembly shared in the default load
context. An ordinary client mod can declare a dependency on the controller,
locate its public host/`ModSystem` through `api.ModLoader.GetModSystem`, and
register through the SDK. Conceptually:

```csharp
public interface IAgentExtension
{
    string Id { get; }
    string Version { get; }
    void Register(IAgentExtensionRegistry registry);
}

public interface IAgentExtensionRegistry
{
    ICoreClientAPI ClientApi { get; }

    IDisposable RegisterMainThread(
        string operation,
        Func<AgentCallContext, JsonElement, JsonElement> handler);

    IDisposable RegisterTickOperation(
        string operation,
        Func<AgentCallContext, JsonElement, IAgentTickOperation> factory);
}

public interface IAgentTickOperation : IDisposable
{
    AgentOperationStep Tick(long monotonicTimestamp);
    void Cancel();
}
```

This is an illustrative contract, not production code. Keep the real version as
small as possible:

- `RegisterMainThread` is for short, nonblocking calls.
- `RegisterTickOperation` is the one escape valve for work spanning ticks. It is
  not a workflow engine; the calling agent still composes workflows.
- the host supplies call ID, session, cancellation token, monotonic time, logger
  and `ICoreClientAPI`;
- an extension returns JSON-compatible results and structured receipt metadata;
- registration returns `IDisposable` so ordinary client mods can unregister;
- both a normal Vintage Story mod and a dynamically loaded assembly register
  through exactly this API.

The dynamic loader must share the controller contract, `VintagestoryAPI`, and
other game assemblies from the default context. `AssemblyDependencyResolver`
may resolve extension-private dependencies. Loading is an explicit, logged
`extensions` operation. The normal source is a dedicated extension directory;
the owner-trusted local CLI may also provide an absolute assembly path. The host
loads assemblies from disk, but does not become a general file read/write API.

### Cancellation truth

Timeouts can stop queued/tick-based host primitives and cooperative extension
operations. They cannot safely preempt arbitrary synchronous C# already running
on the game thread. If a trusted extension blocks or loops forever, the game
freezes and even an in-game kill switch cannot execute.

Therefore:

- main-thread delegate calls have a documented “return quickly” contract and
  elapsed-time diagnostics;
- longer work must use `IAgentTickOperation`, host primitives, or an external
  worker that marshals only small game calls;
- extension cancellation is cooperative;
- process restart is the final recovery boundary for a wedged extension;
- the MVP loads extensions once per client process and does not promise hot
  unload.

.NET collectible `AssemblyLoadContext` unloading is cooperative: it completes
only after executing threads and all strong references disappear. Vintage Story
event subscriptions, delegates, statics and tick listeners are exactly the
kinds of references likely to prevent unload. A future reload command may call
extension cleanup and attempt unload, but it must report `restart_required` when
the context remains alive.

### Extension invocation envelope

```json
{
  "jsonrpc": "2.0",
  "id": "native-1",
  "method": "execute",
  "params": {
    "action": {
      "type": "extension.invoke",
      "extension": "sample.client",
      "version": "0.1.0",
      "operation": "selection.describe",
      "args": {
        "includeBlockAttributes": false
      },
      "timeoutMs": 1000
    },
    "timeoutMs": 1500
  }
}
```

The receipt reports extension ID/version/assembly hash, operation, start/end
monotonic time, cooperative cancellation status, result or exception summary,
and declared/observed side-effect notes. The substrate cannot prove the full
effects of arbitrary in-process code; that is part of the trusted-extension
contract.

## Capability matrix

| Capability | Public route | Authority/result | Proof status | Risk |
| --- | --- | --- | --- | --- |
| Observe own pose/status | client player/entity state | client-visible snapshot | Include | Low |
| Observe own inventory | inventory manager/client inventories | client-visible, may lag server | Include | Medium |
| Observe received chat | client chat event + bounded ring | only messages delivered to client | Include | Medium/privacy |
| Turn/look | input yaw/pitch or client camera | normal client control | `control` primitive | Low |
| Move/jump/sneak/sprint | `EntityControls` on ticks | server may correct/reject movement | `control` primitive | Medium |
| Select hotbar slot | inventory manager active slot | normal client selection | `control` primitive | Low |
| Send chat | `SendChatMessage` | external multiplayer mutation | `mutate` primitive | High/audited |
| Execute slash command | `SendChatMessage` with `/` | server parses and authorizes | `mutate` primitive | High/audited |
| Interact/use/attack | ordinary mouse controls/selections | server reach/rules authoritative | Natural primitive after MVP | High |
| Inspect opened UI | `IGuiAPI`, known dialogs/composers | client-local | Later metadata subset | Medium |
| Semantic UI action | trusted extension for a dialog/mod | may cause server mutation | Extension operation | Medium/high |
| Generic UI tree | no clean public full enumeration verified | incomplete/brittle | Do not promise | High maintenance |
| Coordinate click | dialog methods or OS/window input | focus/scale/layout dependent | Fallback only | High |
| Screenshot | no public API verified | visual evidence, may contain secrets/chat | External in proof | Medium/privacy |
| Register/invoke trusted C# extension | ordinary client mod registration or compiled assembly loader | full in-process client power; server still authoritative | Include one sample extension | Owner-trusted/high |
| Reflective/internal game access | implemented inside an extension | version-fragile and may bypass normal client abstractions | Escape hatch, not stable promise | High maintenance |
| Teleport/set world state | client internals may appear mutable, but server remains authoritative | corrections/rejection/desync possible | Extension can experiment; receipt must not claim server success | Critical |
| Read server logs/config | not client-visible unless separately supplied | server-private | Outside client substrate | Critical |
| Shell/files/network proxy | possible from trusted C#, but unrelated to game control | expands host-machine effects | Do not expose as controller RPC methods | Critical |

## Proposed command and observation protocol

Use JSON-RPC 2.0 framing over a local transport. Protocol SemVer is independent
of the mod version and game version.

### Handshake

Request:

```json
{
  "jsonrpc": "2.0",
  "id": "hello-1",
  "method": "hello",
  "params": {
    "protocol": { "min": "0.1.0", "max": "0.1.0" },
    "client": { "name": "vsctl", "version": "0.1.0" },
    "requestedCapabilities": [
      "read",
      "control",
      "mutate",
      "extensions"
    ]
  }
}
```

Response:

```json
{
  "jsonrpc": "2.0",
  "id": "hello-1",
  "result": {
    "protocol": "0.1.0",
    "gameVersion": "1.22.2",
    "modVersion": "0.1.0-dev",
    "buildHash": "sha256:...",
    "profileId": "Profile2",
    "sessionEpoch": "world-7",
    "worldReady": true,
    "server": {
      "mode": "multiplayer",
      "fingerprint": "sha256:...",
      "policy": "owner-acknowledged"
    },
    "grantedCapabilities": [
      "read",
      "control",
      "mutate",
      "extensions"
    ],
    "limits": {
      "maxActionsPerBatch": 32,
      "maxBatchDurationMs": 30000,
      "maxQueuedBatches": 4,
      "maxRequestBytes": 65536
    }
  }
}
```

The real server address need not be returned by default. A stable local hash is
enough for the operator to recognize a server policy/receipt record.

### Observation

```json
{
  "jsonrpc": "2.0",
  "id": "obs-1",
  "method": "observe",
  "params": {
    "include": ["player", "inventory", "chat", "ui"],
    "chatAfterSequence": 418,
    "uiDetail": "opened-dialogs"
  }
}
```

```json
{
  "jsonrpc": "2.0",
  "id": "obs-1",
  "result": {
    "scope": "client_visible",
    "sessionEpoch": "world-7",
    "sampledAtMonotonicMs": 8123401,
    "player": {
      "position": { "x": 124.2, "y": 81.0, "z": -43.7 },
      "yawDegrees": 91.5,
      "pitchDegrees": -4.0,
      "onGround": true,
      "activeSlot": 2
    },
    "inventory": {
      "revision": 77,
      "active": {
        "inventoryId": "hotbar",
        "slot": 2,
        "code": "game:gear-temporal",
        "stackSize": 1
      },
      "counts": { "game:gear-temporal": 1 }
    },
    "chat": {
      "redacted": false,
      "nextSequence": 420,
      "messages": [
        {
          "sequence": 419,
          "type": "notification",
          "text": "Teleporting in 5 seconds..."
        }
      ]
    },
    "ui": {
      "opened": [
        {
          "adapterId": null,
          "dialogType": "GuiDialogChat",
          "bounds": { "x": 22, "y": 744, "width": 820, "height": 240 }
        }
      ]
    }
  }
}
```

Inventory codes and chat contents require explicit read capabilities. A privacy
mode may return hashes/classifications instead of text or item codes.

### Deterministic batch

```json
{
  "jsonrpc": "2.0",
  "id": "batch-request-19",
  "method": "execute",
  "params": {
    "batchId": "top-cancel-001",
    "sessionEpoch": "world-7",
    "leaseId": "lease-2",
    "timeoutMs": 12000,
    "onFailure": "stop",
    "actions": [
      {
        "id": "a1",
        "type": "send",
        "text": "/top",
        "confirmMutation": true
      },
      {
        "id": "a2",
        "type": "wait_for",
        "timeoutMs": 3000,
        "predicate": {
          "kind": "chat_matches",
          "patternId": "top.warmup.started"
        }
      },
      {
        "id": "a3",
        "type": "control",
        "states": { "forward": true },
        "durationMs": 250,
        "maxDistance": 0.8
      },
      {
        "id": "a4",
        "type": "wait_for",
        "timeoutMs": 3000,
        "predicate": {
          "kind": "chat_matches",
          "patternId": "top.warmup.cancelled"
        }
      }
    ]
  }
}
```

This batch is a bounded sequential convenience, not a workflow language. It has
no branches, loops, variables, expression evaluator, rollback, or durable
orchestration. The calling agent decides what batch to submit next from the
receipt and observations. `wait_for` supports only a few common observation
conditions; an extension can implement a domain-specific wait when needed.

Pattern IDs may come from an extension, or the caller may provide a bounded
exact/substring match. Regex support is optional and should use ordinary
length/time safeguards, not a policy DSL.

### Typed action vocabulary

The stable primitive schema uses a small closed discriminator and rejects
unknown primitive types. `extension.invoke` is the deliberate open namespace,
so new game operations do not require adding discriminators to the core.

| Action | Essential arguments and bounds | Capability | Phase |
| --- | --- | --- | --- |
| `look` | absolute or relative yaw/pitch; clamped pitch | `control` | MVP |
| `control` | set ordinary control states for a bounded duration/distance; covers move/jump/sneak/sprint/mouse actions | `control` | MVP |
| `select_slot` | hotbar slot index plus optional expected item code | `control` | MVP |
| `send` | bounded chat or slash-command text through the normal client path | `mutate` | MVP |
| `wait` | monotonic duration | `control` | MVP |
| `wait_for` | one common observation condition and deadline | `read` | MVP |
| `extension.invoke` | extension ID/version, operation, JSON args and deadline | `extensions` | MVP |
| `ui.click` | game-window x/y, button, expected bounds revision | `ui` | Layer-two fallback |

`ui.click` coordinates are always relative to the reported game client area, not
the desktop. The request must include the dialog identity and bounds revision it
was based on; a resize, scale change, focus loss, or UI revision invalidates the
action rather than clicking stale coordinates.

Named UI example:

```json
{
  "id": "a-ui-1",
  "type": "extension.invoke",
  "extension": "thebasics.config",
  "version": "1.0.0",
  "operation": "set_boolean",
  "args": {
    "setting": "TopRequireTemporalGear",
    "value": true
  },
  "confirmMutation": true
}
```

This example is a proposed extension contract, not an existing hook. It must
preserve The BASICs' normal server authorization, config registry, validation,
live/restart behavior and audit path.

`confirmMutation` is optional session policy metadata, not a required prompt for
every call. The recommended default is to grant `mutate` once when the owner
enables the session and log each resulting mutation.

### Receipt

```json
{
  "jsonrpc": "2.0",
  "id": "batch-request-19",
  "result": {
    "receiptVersion": "0.1.0",
    "batchId": "top-cancel-001",
    "requestId": "batch-request-19",
    "sessionEpoch": "world-7",
    "status": "succeeded",
    "acceptedAtMonotonicMs": 8124000,
    "startedAtMonotonicMs": 8124012,
    "finishedAtMonotonicMs": 8125377,
    "actions": [
      {
        "id": "a1",
        "status": "succeeded",
        "sideEffects": ["server_command_sent"],
        "chatSequenceAfter": 421
      },
      {
        "id": "a2",
        "status": "succeeded",
        "matchedChatSequence": 421
      },
      {
        "id": "a3",
        "status": "succeeded",
        "actualDurationMs": 266,
        "actualDistance": 0.31,
        "controlsReleased": true
      },
      {
        "id": "a4",
        "status": "succeeded",
        "matchedChatSequence": 422
      }
    ],
    "preSnapshotHash": "sha256:...",
    "postSnapshotHash": "sha256:...",
    "auditId": "audit-20260725-000031"
  }
}
```

Receipts state what the client observed, not an invented server transaction.
`server_command_sent` means the command left the client; later chat, position,
inventory, or server-log evidence establishes its outcome.

Cancellation is separate and idempotent:

```json
{
  "jsonrpc": "2.0",
  "id": "cancel-1",
  "method": "cancel",
  "params": {
    "batchId": "top-cancel-001",
    "reason": "operator_kill_switch"
  }
}
```

## Minimal timing, dispatch, and state

This is intentionally not a general scheduler. It is the least machinery needed
to hold input for bounded time, advance cooperative tick operations, cancel
them, and produce a terminal receipt.

### Threading model

The transport reader must never touch game state. It should:

1. authenticate and apply byte/rate limits before parsing;
2. parse and validate into an immutable bounded request;
3. enqueue the request;
4. use `EnqueueMainThreadTask` to signal the engine;
5. let a registered client tick listener advance all game-facing state;
6. serialize a completed immutable receipt back off-thread.

Only the game thread reads player/world/UI state or changes controls.

### Clock and deadlines

Use `Stopwatch.GetTimestamp()` (or an injected equivalent) as the monotonic time
base. Store absolute monotonic deadlines for every action and batch.

Do not use:

- cron or scheduled OS tasks;
- wall-clock `DateTime` for elapsed action timing;
- `Task.Delay` as the owner of movement state;
- an LLM connection holding a key down;
- tick counts as time without also checking the monotonic deadline.

The tick may arrive late. On the first tick at or after a deadline, the engine
releases the control and records the actual elapsed time.

### Minimal operation states

```text
Accepted
  -> Dispatching
  -> Running primitive or cooperative extension operation
  -> Completed

Any nonterminal state
  -> Failed | TimedOut | Cancelled | SessionInvalidated
  -> ReleaseAllOwnedInputs
  -> Terminal receipt
```

There must be exactly one terminal path, and it must always:

- clear every control the engine owns;
- clear mouse-button actions;
- release the execution lease;
- record whether release succeeded;
- complete or fail the waiting RPC;
- leave no background continuation able to reassert input.

### Control ownership

The engine needs a control lease rather than saving/restoring arbitrary boolean
states. It records which controls it asserted and releases only those controls.
If physical user input conflicts, default behavior is to cancel the batch and
yield to the human. The owner kill switch bypasses the queue and releases all
agent-owned input on the next main-thread opportunity.

On world unload, disconnect, player death, session epoch change, transport
disconnect, mod disable, or unhandled engine exception, cancel immediately and
release input.

### No workflow engine

The host checks only universal call validity: authenticated session, compatible
version, world/player readiness where relevant, lease ownership, request bounds
and deadline. Domain preconditions and postconditions belong to the calling
agent or extension.

Batch “atomicity” means only queue/lease ownership and fail-fast sequential
dispatch. It does not mean rollback. Chat messages, commands, movement and
server outcomes are external side effects and cannot generally be undone.

### Backpressure and reconnects

- one active batch per game client;
- a small configurable pending queue;
- maximum 32 actions and 30 seconds per batch;
- maximum 5 seconds for one continuous input hold;
- bounded request, response, chat ring, observation and audit sizes;
- reject excess work as `busy`/`rate_limited`, never grow an unbounded queue;
- cache final receipts briefly by idempotency key so reconnect retries do not
  resend chat or commands;
- create a new `sessionEpoch` on each world/session lifecycle;
- invalidate leases and queued requests on epoch change;
- require a new handshake and lease after reconnect.

## Transport and packaging decision

### Comparison

| Option | Advantages | Costs/risks | Decision |
| --- | --- | --- | --- |
| Windows named pipe | Local only, current-user ACL, no TCP listener, good duplex RPC and reconnect semantics | Windows-specific; hosts need a small client library | Canonical first transport |
| Loopback HTTP/JSON-RPC | Easy language interoperability; good for remote agent hosts running locally | TCP attack surface, bearer/Origin/Host/rate/body/session handling | Optional broker transport later |
| CLI only | Excellent for debugging, scripts, auditability | Process-per-action is poor for leases/events; not a runtime | Thin client, not canonical |
| MCP stdio | Standard host integration and lifecycle; child process boundary | MCP server lifetime differs from game lifetime | Preferred agent bridge over the named pipe |
| MCP Streamable HTTP | Multiple clients/reconnects and standard transport | More lifecycle/security machinery inside or beside the game | Optional broker surface, not in mod |
| Small local broker | Can find/multiplex Profile2/Profile3, capture windows, expose stdio/HTTP | Another process and lifecycle to secure | Add only when two-client or screenshots justify it |

### Recommended process layout

```text
Codex / Claude / OpenClaw
            |
     one thin global skill
            |
     MCP stdio bridge ---- vsctl CLI
            |                |
            +------ named pipe
                       |
           Vintage Story client-only mod
           minimal dispatcher + input/tick operations
           + trusted extension registry

Later only:
local broker -> profile discovery/multiplexing
             -> Windows window screenshot capture
             -> optional loopback MCP Streamable HTTP
```

The global skill should teach hosts the same small MCP surface and operational
caveats. It must not duplicate game semantics. Transport validation,
game-thread dispatch and receipts belong to the game-side runtime; workflows
belong to the calling agent.

Directly embedding a full MCP server in the mod is not recommended. It couples
the game process to MCP transport/lifecycle churn and expands the parsing and
network surface. The stdio bridge can negotiate MCP using the official lifecycle
while separately negotiating the stable game protocol.

Official MCP references:

- [MCP architecture](https://modelcontextprotocol.io/docs/learn/architecture)
- [2025-06-18 lifecycle](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle)
- [2025-06-18 transports](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)

Use the stable 2025-06-18 specification for a first implementation. MCP's
standard transports are stdio and Streamable HTTP. If HTTP is later enabled, the
spec specifically supports the needed local safeguards: validate `Origin`, bind
to loopback, and authenticate.

### Screenshot transfer

Screenshots should not be base64 embedded in ordinary JSON observations. In the
initial MVP, external Computer Use saves/correlates the image outside this protocol.
If the broker later captures the game window, the flow should be:

1. request one capture with maximum width/height/encoded-byte limits;
2. receive a short-lived opaque artifact ID, dimensions, media type, SHA-256,
   capture monotonic time and receipt correlation ID;
3. fetch the artifact through a separate length-delimited binary pipe message
   or authenticated loopback response;
4. expire it after a short TTL and allow only one or a very small number of
   outstanding captures per client.

The broker must capture only the pinned Vintage Story client area, not the
desktop. It must apply backpressure before capture/encoding and must never accept
an arbitrary output path or upload URL. Chat/privacy redaction may require a
human-reviewed crop or an option to hide chat before capture.

### Windows lifecycle

The mod creates a profile-specific pipe only after explicit in-game enablement
and removes/stops accepting on shutdown. The pipe ACL permits only the current
Windows user and optionally LocalSystem for debugging only if separately
approved.

The bridge:

- retries pipe discovery with bounded exponential backoff;
- reports “game not running”, “controller disabled”, “world not ready”, and
  “profile ambiguous” distinctly;
- never launches the game without an explicit operator command;
- pins a target profile/session rather than choosing whichever client answers
  first;
- exits cleanly when its MCP host closes stdio;
- does not keep the game controller enabled after its lease expires.

## Threat model and safety posture

### Trust boundary

This is owner-trusted local power tooling. The primary boundary is between the
owner's enabled session and unrelated local/network callers, not between the
owner and their own extension code.

A loaded extension has the effective authority of an in-process Vintage Story
mod and the current desktop user. It can crash/freeze the game, read process
state and call ordinary .NET APIs. `AssemblyLoadContext`, reflection filters and
timeouts do not turn it into untrusted-code containment. Only load extensions
the owner trusts; use an OS process/VM boundary if untrusted code ever becomes a
requirement.

Operational risks still matter:

- an agent or extension issues the wrong game operation;
- a lost connection leaves movement or mouse input asserted;
- stale retry duplicates a chat message or slash command;
- arbitrary synchronous extension code blocks the game thread;
- client observations are mistaken for server-authoritative truth;
- operation occurs on a server whose rules disallow automation;
- private chat or secrets appear in screenshots/receipts;
- an unrelated local process discovers an enabled endpoint.

### Sensible defaults

- disabled by default and explicitly enabled in game;
- named pipe restricted to the current OS user;
- random per-enable session secret, never logged;
- conspicuous enabled/connected/active HUD state;
- in-game kill switch that cancels cooperative work and releases owned input;
- immediate input release on transport loss, session expiry or world unload;
- bounded request size, queue, action duration, input hold and screenshot size;
- idempotency keys for calls with external side effects;
- concise receipts/logs for native extension load/invoke and chat/commands;
- Host/Origin/authentication checks if loopback HTTP is added;
- clear world/server identity in handshake and every mutating receipt.

Use four coarse session capabilities rather than a growing policy taxonomy:

| Capability | Covers |
| --- | --- |
| `read` | structured observations, chat/UI metadata and screenshot handles |
| `control` | look, input states, slots, waits and batches |
| `mutate` | chat, slash commands and clicks that can create external effects |
| `extensions` | invoke already registered/loaded trusted native extensions |

An optional `ui` capability may be split from `mutate` if coordinate clicking
proves risky in practice. Do not build a classifier or feature-by-feature
capability registry preemptively.

Singleplayer/local QA can use a remembered owner default. On multiplayer, the UI
should prominently show server identity and require a one-time session
acknowledgment unless that server is in the owner's trusted list. This is a
sensible default, not an immutable denial: server owners and communities set
their own automation rules, and the tool should make compliance an informed
operator decision.

Chat/UI observations are data, not controller instructions. The calling agent
decides how to interpret them. The controller itself never changes capabilities
or loads code because a chat/UI string requested it.

The RPC surface should still omit generic shell/filesystem/network-proxy
methods. Trusted extensions can technically use normal .NET APIs, but that
authority is explicit native code, not ambient functionality disguised as a
game-control command.

### Audit events

Record enable/disable, handshake/grants, lease creation/expiry, every mutating
request, batch terminal result, kill switch, disconnect release, and dropped
work. Audit records should contain hashes or redacted forms where full chat text
is unnecessary.

Sending chat or a command is always an external mutation, even in a disposable
world. The receipt must say who/what requested it, the exact text or a configured
redacted hash, when it was sent, and what client-visible evidence followed.

## `/top` temporal-gear deterministic test

This is a proposed test design, not an executed QA result.

### Operator-controlled setup

The owner or an already-authorized server workflow must establish:

- the `a370844` feature build on the disposable server/client;
- `RegisterTopCommand = true` (restart-required);
- `TopRequireTemporalGear = true` (live configuration);
- a known `TopWarmupSeconds`, suggested 5 seconds;
- a player below a known safe surface with cooldown clear;
- an exact starting temporal-gear count.

The current config surface is `/thebasics config`, a server-authoritative GUI.
Until a trusted The BASICs extension exists, configuration remains a human/admin
step or a bounded UI fallback. A future semantic extension should drive the
normal config/network path rather than mutating server state behind it.

### Card A: missing gear

1. Human/setup confirms no temporal gear in eligible inventory/hand.
2. Controller snapshots pose, inventory count and next chat sequence.
3. Controller sends `/top` with the session's `mutate` capability and records
   the external mutation.
4. Controller waits for the stable “gear required” chat pattern.
5. Controller asserts position remains within tolerance and gear remains zero.
6. External capture records the client view correlated with the receipt.

Expected: command is refused; no warmup, teleport, or inventory change.

### Card B: successful completion

1. Human/setup places exactly one gear in the eligible slot and confirms the
   safe destination.
2. Controller snapshots pose/inventory and selects the known slot if required.
3. Controller sends `/top`.
4. Controller waits for the warmup-start chat receipt.
5. Controller holds no movement or interaction and waits for success evidence.
6. Controller asserts:
   - temporal gear count decreased by exactly one;
   - player Y/position moved to the expected safe region;
   - success chat was received;
   - no agent-owned control remains asserted.
7. External capture records after-state and receipt ID.

Expected: one gear is consumed only after warmup completion and the player lands
at the safe destination.

### Card C: movement cancellation

1. Setup restores exactly one gear and starting position.
2. Controller snapshots pose/inventory and sends `/top`.
3. After the warmup-start chat, controller moves forward for a bounded 250 ms,
   capped at 0.8 blocks, then releases.
4. Controller waits for cancellation chat.
5. Controller asserts:
   - actual movement exceeded the server cancellation threshold;
   - teleport did not occur;
   - temporal gear count is unchanged;
   - all controls were released.
6. External capture records the cancelled state.

The current server code samples warmup at 100 ms and cancels around a 0.05-block
position change. The test should measure actual position delta rather than
assuming a key duration was sufficient.

### Card D: interaction/damage/disconnect coverage

These remain later or human-assisted cards. Interaction needs a reviewed native
interact action; damage needs a controlled second client or server fixture;
disconnect intentionally invalidates the controller session and must be judged
against both client and server evidence. They are not necessary for the first
proof.

### Evidence reconciliation

For each card, retain:

- pre/post client-visible snapshots;
- per-action receipt and monotonic timings;
- correlated screenshots;
- operator-provided server-log excerpt only when separately authorized;
- explicit human observation for visual/safety claims.

If client receipt and server log disagree, report the disagreement. Do not let a
client-side assertion overwrite the server-authoritative result.

## What must remain human/in-client QA

Automation can establish timing, commands, position changes, counts, chat
receipts, control release and repeatability. A person must still judge:

- that the destination is visually safe, not clipped, suffocating or misleading;
- message legibility, timing and whether the chat/notification appears in the
  intended UI;
- that movement and camera behavior feel normal after cancellation;
- that the conspicuous controller indicator and kill switch are discoverable;
- configuration-dialog layout, scaling and named-adapter correctness;
- screenshot content and whether private chat/secrets need redaction;
- interactions between two visible clients and third-party mods;
- whether the overall behavior matches product intent rather than merely the
  machine predicate.

Manual QA may begin only after the owner approves a concrete card plan, and it
may be marked complete only from the owner's explicit observations. Automated
receipts are evidence for those cards, not permission to check them off.

## MVP cut line

The first build should contain only four artifacts:

| Artifact | Contains |
| --- | --- |
| `agentcontrol` client mod | named-pipe endpoint, main-thread dispatcher, minimal tick/input ownership, observations, extension registry, HUD indicator and kill switch |
| `agentcontrol.abstractions` assembly | extension interfaces and small request/result context types; no transport, workflows or game-specific commands |
| `vsctl` CLI | connect, inspect, execute, cancel and print/save receipts |
| `agentcontrol.sample` client mod | one normal Vintage Story `ModSystem` that registers a genuinely new operation through the abstractions contract |

The sample operation should be `selection.describe`: return a structured
description of the current block/entity selection plus one useful API-derived
detail that the controller core does not know about. It is intentionally small,
but proves that:

- another compiled client mod can find the registry and register;
- `ICoreClientAPI` crosses the host boundary with correct type identity;
- the operation runs on the game thread;
- JSON arguments/results and exceptions round-trip;
- the receipt identifies the extension ID, version and assembly hash;
- adding the operation required no controller-core command change.

The complete MVP RPC surface can be six methods:

| Method | Purpose |
| --- | --- |
| `hello` | protocol/game/profile/server/session/capability negotiation |
| `observe` | one structured client-visible snapshot |
| `execute` | one primitive or a bounded sequential array of primitives/extension calls |
| `cancel` | cancel the active cooperative execution and release owned input |
| `extensions.list` | list registered operation IDs, versions and compatibility |
| `shutdownSession` | revoke the secret, cancel and disable remote control without exiting the game |

`execute` may keep its RPC request open until a terminal receipt; `cancel` can
arrive on a second pipe connection. The host permits one active execution and a
small bounded queue. It does not expose leases, durable operation storage,
polling subscriptions, streaming events, a workflow graph or a separate
scheduler API in the MVP.

Explicitly out of the first build:

- MCP, HTTP and a broker;
- dynamic assembly loading/unloading;
- Roslyn scripting;
- native screenshot capture;
- generic UI-tree discovery or coordinate clicking;
- pathfinding, navigation meshes, combat or building semantics;
- a feature-specific `/top` extension;
- extension dependency isolation;
- hot reload and process recovery beyond an honest client restart.

External screenshots and normal Computer Use preserve the layered fallback
during this cut. The next feature should be chosen from real friction after the
primitive, extension and `/top` acceptance tests, not from an anticipated
catalog.

## Testing strategy

### Pure automated tests

Build the engine around injected clock, game facade and transport interfaces.
Unit tests should cover:

- every valid and invalid state transition;
- monotonic deadlines when ticks are early, exact, late and skipped;
- cancellation from every nonterminal state;
- input release after success, failure, timeout, disconnect and exception;
- physical-user conflict yielding;
- bounded observation matching;
- session epoch invalidation;
- idempotent request replay without duplicated chat/commands;
- batch, body, queue, duration and observation limits;
- coarse capability denial and multiplayer acknowledgment behavior;
- extension registration conflicts, dispatch and exception receipts;
- default-context identity for the host/game contract assemblies;
- cooperative tick-operation cancellation and cleanup;
- redaction and audit completeness;
- protocol negotiation across compatible/incompatible versions.

Property/fuzz tests should target JSON/schema validation, length-prefix framing,
partial pipe reads, duplicate IDs, extreme numeric values and state-machine
sequences. The parser must reject unknown action types by default.

### Contract and integration tests

A headless fake client facade can drive ticks and observed state without
starting the game. Golden protocol fixtures should validate CLI, MCP bridge and
mod against the same schema.

In-game integration checks should be deliberately small:

- enable/disable/indicator/kill switch;
- look and a 200 ms movement action in singleplayer/disposable QA;
- disconnect during movement and verify release;
- command mutation receipt;
- register a sample compiled client-mod extension and invoke one operation that is not
  implemented by the controller core;
- restart with an incompatible extension and verify a clear compatibility
  failure rather than a partial load;
- `/top` Cards A-C.

Do not build a large visual automation suite or domain command catalog before
the MVP proves that both primitives and extensions compose cleanly.

### Release regression gates

For each supported Vintage Story minor:

1. compile against the versioned official dependencies;
2. run schema/state-machine/bridge contract tests;
3. launch one disposable profile and run a read-only handshake smoke;
4. run the bounded movement/release smoke with owner approval;
5. register/invoke the sample extension;
6. run one current high-value feature card such as `/top`;
7. retain game/controller/extension versions and artifact hashes in evidence.

## Versioning and maintenance cost

Keep four identities separate:

- `protocolVersion`: compatibility of requests, observations and receipts;
- `controllerModVersion`: implementation/release version;
- `gameVersion`: exact Vintage Story runtime, initially 1.22.2;
- `extensionSdkVersion`: compatibility of the registration/invocation contract;
- extension ID/version/assembly hash for every loaded native module.

Handshake negotiates a protocol range and capability list. Adding an optional
field/action is a minor protocol change; changing semantics or removing a field
is major. Experimental capabilities must be explicitly labeled and off by
default.

Initial support should be exact-tested Vintage Story minor versions, not an
unverified “1.22+”. Fail closed on unknown major/minor until a compatibility
smoke passes.

Expected maintenance:

| Surface | Cost | Reason |
| --- | --- | --- |
| Minimal dispatcher, schemas, named pipe | Low/medium | Mostly game-independent and heavily unit-testable |
| Public player/input/inventory API | Medium | Public, but behavior/packet timing can shift by game minor |
| Extension SDK and ordinary mod registration | Medium | Small contract, but type identity and lifecycle must remain stable |
| Dynamic compiled-extension loading | Medium/high | Dependency resolution, compatibility diagnostics and cooperative cleanup |
| Extension-owned chat/UI semantics | Owned by extension | Localization/copy and mod behavior evolve without growing the core |
| Coordinate clicks | High | Resolution, scale, focus and layout dependent |
| Extension reflection/internal screenshot hooks | Very high | Private implementation churn isolated to the extension |
| Two-client broker/window capture | Medium/high | Profile discovery, focus and Windows lifecycle complexity |

The cost-control rule is to keep core primitives few and typed, put specialized
game/mod knowledge in extensions, and leave one-off UI gaps to the screenshot/
click layer or normal Computer Use.

## Phased implementation plan

### Phase 0: owner decisions and contract sketch

- confirm the open, owner-trusted substrate posture;
- choose the initial disposable profile/server for development;
- approve the four coarse session capabilities and audit retention;
- approve one extension SDK/registry contract for ordinary client mods that
  does not preclude an explicit assembly loader later.

### Phase 1: coherent open-substrate MVP

- separate client-only mod;
- in-game enable/disable, HUD indicator and kill switch;
- current-user named pipe plus random per-enable bearer;
- version handshake, server identity/acknowledgment and one execution lease;
- player/inventory/chat observations;
- look, timed control states, select slot, send, wait/wait-for;
- bounded sequential batches, monotonic timing, cancellation and receipts;
- tiny extension SDK and registry;
- ordinary Vintage Story client-mod registration path;
- one sample extension with an operation absent from the core;
- `vsctl` CLI;
- unit/contract tests;
- `/top` Cards A-C using external screenshots.

Exit condition: the MVP invokes the same sample operation through at least one
native extension discovery path, composes primitives to complete/cancel `/top`,
never leaves owned input asserted, and reports game/client/server/extension
identity honestly.

### Phase 2: host integration

- MCP stdio bridge over the same named pipe;
- one global skill/tool contract for Codex, Claude and OpenClaw;
- profile/session selection;
- receipt artifact export with redaction.

No game-domain logic moves into the skill or bridge.

### Phase 3: bounded UI and visual evidence

- opened-dialog metadata;
- extension-owned semantic UI operations;
- optional Windows window-capture broker;
- bounded game-window click fallback;
- screenshot privacy/redaction policy;
- second-client multiplexing if a real card requires it.

### Phase 4: natural extension iteration

- add a core primitive only when it is broadly useful and materially safer or
  more interoperable than an extension;
- add compiled extension templates/build commands against exact game/SDK
  versions;
- add explicit compiled-assembly loading through the same SDK if normal
  mod-package/restart friction is material;
- add best-effort extension reload only if restart friction is material;
- consider Roslyn scripting only if external compilation plus loading is too
  slow for real workflows;
- let pathfinding, building, combat, mod-specific actions and internal UI access
  evolve as extensions rather than expanding the substrate core.

## Owner decisions needed

Only a few decisions block an MVP:

1. **Extension discovery:** use ordinary Vintage Story client-mod registration
   as the sole MVP mechanism, then add explicit compiled-assembly loading only
   if the build/package/restart loop is materially slow. This is the simplest
   recommendation.
2. **Load lifecycle:** require client restart for extension updates in the MVP.
   Revisit dynamic loading and best-effort unload together rather than building
   half a reload system.
3. **First environment:** choose Profile2 or a new disposable profile and the
   first server/world for acceptance. This is a proving environment, not a
   permanent allowlist.
4. **Mutation acknowledgment:** grant `mutate` for the enabled session, or also
   require per-call `confirmMutation`. The simpler recommendation is session
   grant with every mutation logged.
5. **Audit privacy:** choose full local chat/arguments, configured redaction, or
   hashes in retained receipts, and a retention duration.

## Recommendation

Approve Phase 1 as a time-boxed open-substrate MVP.

Its two acceptance questions are:

> Can an agent compose a few deterministic primitives to perform and verify the
> current `/top` temporal-gear flow while preserving server-authority truth?

> Can trusted C# add a genuinely new Vintage Story client operation through the
> shared extension contract without modifying the controller core?

If both are yes, add the MCP stdio bridge/global skill and then improve
screenshots/UI. If primitives work but extension loading is awkward, simplify
the extension lifecycle before adding commands. If neither produces a clean
calling surface, stop and redesign the contract rather than building a workflow
engine around it.
