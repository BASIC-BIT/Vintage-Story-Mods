# Dice rolls and Th3Essentials Discord delivery

Research follow-up, 2026-09-07. User confirmed chat/command rolls, no physical system, and requested dice results appear in Discord through Th3Essentials. No implementation or live send performed.

## Existing route

The BASICs already includes an optional bridge in [Th3EssentialsDiscordRelay.cs](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Th3EssentialsDiscordRelay.cs). Source inspection establishes this route:

```text
Accepted The BASICs chat context
  -> canonical rendered log message
  -> ProximityChatMessageProcessed event
  -> The BASICs Th3Essentials bridge
  -> plain text, mass-mention suppression
  -> Th3Essentials sendQueue
  -> Discord delivery managed by Th3Essentials
```

The last step is the intended external behavior, not a live observation from this research.

- [RPProximityChatSystem.cs](../../mods-dll/thebasics/src/ModSystems/ProximityChat/RPProximityChatSystem.cs) subscribes its relay handler to the processed event and passes the rendered message to the bridge.
- [TransformerSystem.cs](../../mods-dll/thebasics/src/ModSystems/ProximityChat/Transformers/TransformerSystem.cs), `LogChatMessageOnce`, publishes the event once per context, then records history. In the inspected checkout this occurs before recipient sends, including when there are no immediate or pending recipients. The API documentation's wording about requiring delivery is therefore not a reliable description of this source snapshot. Do not equate event publication with successful delivery.
- The BASICs `EnableTh3EssentialsDiscordRelay` defaults to false. The bridge also requires Th3Essentials' `DiscordChatRelay` to be true and `DiscordChannel` to be non-null.
- The bridge strips VTML, decodes entities, trims, and neutralizes `@everyone` and `@here`. It does not establish general Discord mention or Markdown sanitization. Optional player-authored roll reasons should be tested against the actual outbound policy.
- Queue access uses reflection against a private `ConcurrentQueue<string>` named `sendQueue`; configuration and channel discovery also assume named members. Failures are caught/logged. This is an existing compatibility seam, not a stable upstream contract.
- [Bridge tests](../../mods-dll/thebasics.Tests/ModSystems/ProximityChat/Th3EssentialsDiscordRelayTests.cs) exercise formatting, fake queue insertion, the upstream toggle, and changed config shapes. These are not installed-version or live Discord proof, and were inspected rather than rerun for this research.

The publisher advertises bidirectional integration between in-game General chat and one Discord channel. That does not establish that arbitrary server command responses are automatically relayed. The existing The BASICs processed-message bridge is the relevant route. [Th3Essentials publisher listing](https://mods.vintagestory.at/theessentials)

## Proposed dice behavior

Compute each accepted roll once on the game server. Carry a result containing expression, dice, modifier, total, identity, and optional reason to in-game formatting, history, and the existing relay. Discord gets a plain-text rendering of the same result. Never evaluate the expression again for Discord or once per in-game recipient.

Example proposed output:

```text
[Roll] Mira | forcing the gate | 2d6+3: [4, 5] + 3 = 12
```

The expression syntax and presentation are illustrative, not approved command grammar. A future keep-high roll should show all generated dice and distinguish the retained result. A success-count pool must identify successes rather than presenting the sum as if equivalent.

Use the existing relay setting for ordinary public scene rolls unless the owner requests dice-only forwarding. That choice avoids a new bot, credentials, or general relay subsystem. A separate dice-only setting is justified only if a server wants dice in Discord while keeping ordinary proximity chat in-game. This preference was asked asynchronously and remains unresolved at authoring.

Ensure a new roll message kind is explicitly classified by the event/history surfaces. Reusing the processed event should be the sole forwarding path; also directly calling the relay would duplicate the roll. If a narrow delivery method is chosen instead, preserve one explicit canonical event/history step.

Discord downtime should not prevent or undo a valid in-game roll. Queue insertion is not a confirmed Discord delivery receipt. No durable retry ledger or exactly-once external-delivery guarantee is proposed here.

Private/GM/self rolls are a later audience decision. They cannot safely be added merely by restricting the in-game recipient list while leaving every result on the current unconditional processed-message relay. Avoid interpreting inbound Discord chat as executable dice commands unless separately designed and requested.

## Verification for a later implementation

Use the actual server's The BASICs and Th3Essentials package versions for an owner-approved test. Roll near two clients and compare exact expression, dice and total with the single Discord message and staff history. Verify out-of-range behavior separately from intentional Discord visibility. Check disabled toggles, absent bridge, disconnected Discord, invalid requests, reasons with markup/mentions, repeated rolls and command collisions. No such live actions are authorized or performed by this research request.

## Evidence limits

The [upstream GitLab project](https://gitlab.com/th3dilli_vintagestory/th3essentials) was located and its root tree retrieved. Raw Discord implementation fetches failed in the web tool. Exact current upstream queue internals and the community server's installed configuration remain unverified. The integration findings above come from current local source, not an assumption that upstream internals never change.
