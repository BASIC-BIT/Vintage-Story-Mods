# BASICs Agentic Dogfooding Repo (Extraction Plan)

Goal: move the reusable agentic tooling + playbooks out of the mod repo into a dedicated repo ("BASICs Agentic Dogfooding") while keeping the mod repo lean.

## What should move (candidate)

Reusable agent engineering artifacts:

- `.opencode/skills/*/SKILL.md`
- `.opencode/tools/*` (repo-local OpenCode tools)
- `docs/opencode/*`
- selected `docs/ops/*` that are generic (Pterodactyl orchestration, verification loops)
- `.env.example` patterns (but keep mod-specific env vars in the mod repo)

## What should stay

- Mod code (`mods-dll/thebasics/*`, etc.)
- Mod-specific ops docs (release steps, moddb upload specifics)
- Branching/versioning strategy that is mod-specific

## Suggested shape of the new repo

- `AGENTS.md` (generic agent rules)
- `.opencode/skills/*`
- `.opencode/tools/*`
- `docs/opencode/*`
- `docs/ops/*` (generic only)
- `templates/` (starter `.env.example`, example scripts)

## Migration approaches

Option A: Copy-forward (recommended)

- Create new repo
- Copy selected directories/files
- Keep history in current repo; new repo starts fresh
- Pros: simple, low risk
- Cons: loses fine-grained history for meta docs

Option B: Git subtree split (history-preserving)

- Use `git subtree split` on a chosen prefix
- Pros: preserves history for moved files
- Cons: more complex; easy to get wrong

Option C: Filter-repo (history rewrite)

- Use `git filter-repo` to extract paths into a new repo
- Pros: best history preservation
- Cons: most complex; highest footgun risk

## Near-term next steps

1) Decide repo visibility + owner (org/user)
2) Choose migration approach (A/B/C)
3) Create repo and seed structure
4) Add a short README explaining how to consume it from other repos
