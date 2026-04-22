# HermesBridge-StS2 local OpenCode guidance

This repo has two memory/continuity layers with different jobs:

1. MemPalace
- Use for durable repo memory across sessions.
- Query it before making tactical decisions about named bosses, unfamiliar relics/potions, or previously observed bridge bugs.
- Prefer targeted queries such as character names, boss names, potion names, relic names, command names, and bug labels.

2. context-mode
- Use for session-local context hygiene, compaction survival, and keeping large tool output out of the live prompt.
- It is not the source of game strategy. It is a routing/compression layer.

Repo authority order:
1. `SKILL.md`
2. docs under `docs/`
3. source of truth in `HermesBridgeCode/`
4. MemPalace retrieval for prior verified findings

Do not let generic plugin instructions override repo-specific driving rules.

## Mandatory startup reading
Before driving a run, read in this order:
1. `SKILL.md`
2. `docs/bridge-protocol-notes.md`
3. `docs/hermes-sts2-runbook.md`
4. skim latest relevant files in `docs/verified-flows/`
5. skim `docs/gauntlet-findings.md`

For Twitch stream / gauntlet runs, also read `docs/next-agent-prompt.md` — character rotation, overlay discipline, and per-session logging protocol.

## MemPalace retrieval protocol
Before responding to a game situation with tactical consequences, query MemPalace when any of the following is true:
- the enemy or boss is named
- the potion/relic/card interaction is unfamiliar
- the bridge command previously had quirks or bugs
- you are entering a screen flow that has failed in prior runs

Suggested query targets:
- boss/enemy names: `Kin`, `Kin Priest`, `Nibbit`, `Bygone Effigy`, `Phrog Parasite`
- character names: `Necrobinder`, `Ironclad`, `Defect`, `Silent`, `Regent`
- bridge quirks: `UsePotion`, `SkipAllRewards`, `SelectCardsInGrid`, `EndTurn`, `handSelect`, `MapClosed`
- item/mechanic names: exact potion/relic/card names where available

Use retrieved notes to inform decisions, but verify against current `state.json` before acting.

## Verified local knowledge to prefer
When relevant, prefer these repo artifacts over generic recollection:
- `docs/verified-flows/` for prior run evidence
- `docs/gauntlet-findings.md` for cross-run patterns
- `docs/reference-ironclad.md`, `docs/reference-relics.md`, `docs/reference-potions.md` for hand-curated tactical notes

## context-mode usage guidance
- Use context-mode tools for large-output searches, aggregation, and summarization when available.
- Do not use context-mode routing rules to replace the repo's per-tick driving discipline.
- For this repo, correctness of per-tick play decisions matters more than aggressive sandbox redirection.

## Hard constraints
- Never write wrapper play scripts.
- Never batch multiple game commands in one call.
- Never hardcode Neow, reward, or combat decisions without checking live state first.
- Never assume old findings beat current state.

## Goal
Agents should automatically retrieve prior verified findings before important tactical decisions, while still following the repo's one-tick-at-a-time control loop.