# Review Instructions

## What Important Means Here

Reserve Important findings for issues introduced by the PR that could corrupt
world or player data, cross client/server trust boundaries, expose private data
or credentials, bypass privileges, break mod loading or network compatibility,
make teleportation unsafe, or make build, deployment, release, or recovery
unsafe.

Style, naming, broad refactor preferences, missing comments, and test coverage
suggestions are Nit at most unless they hide a concrete runtime, security, data,
or operational risk.

## Noise Controls

- Do not report formatting, compiler errors, generated files, packaged mod
  archives, routine lockfile churn, or issues already enforced by CI. Do report
  dependency or release-artifact changes that create concrete security,
  integrity, source, compatibility, or runtime risk.
- Do not recommend new abstractions unless duplicated code creates a real
  correctness, security, compatibility, data, or operational risk.
- Do not flag pre-existing issues as PR blockers. Mark them as pre-existing in
  the summary if they are worth follow-up.
- On follow-up reviews for the same PR, suppress new nits unless the latest
  pushed code introduced them.

## Evidence Bar

Every finding should include the exact file and line, the changed behavior, why
it matters for these Vintage Story mods, and the smallest safe fix. If the
concern depends on product judgment, game-version behavior, or missing runtime
evidence, put it in the summary instead of presenting it as a blocker.

## Vintage Story Mod Checks

- Keep client-only, server-only, and shared behavior on the correct API side;
  never trust client packets or client-owned state for server authority.
- Existing live configuration fields must retain their `ProtoMember` numbers.
  New fields must use the next available sequential number and preserve safe
  defaults and backward compatibility.
- Network changes must preserve channel registration order, packet
  compatibility, connection timing, and server-side validation. Client sends
  should continue to use `SafeClientNetworkChannel` where applicable.
- Player statistics, language state, homes, spawn data, and other persisted
  state must survive missing, old, partial, or malformed data without silent
  loss or cross-player leakage.
- Teleport, home, spawn, privilege, and admin flows must remain server-authorized
  and fail safely when players disconnect, dimensions change, or destinations
  are unsafe or unavailable.
- Harmony patches and game lifecycle hooks must be side-correct, narrowly
  targeted, idempotent across reloads where required, and compatible with the
  repository's supported Vintage Story version.
- Proximity chat, typing indicators, overhead bubbles, language handling, and
  map visibility must not reveal players beyond the configured audience or
  distance. In particular, do not use `mapShowGroupPlayers` as a proximity-map
  default because the proximity chat group can include the whole server.
- Release and CI changes must preserve least-privilege credentials, avoid
  running untrusted pull-request code with secrets, and keep generated packages
  traceable to the reviewed source commit.
