# Next-Agent Prompt: Twitch Stream Gauntlet

Purpose: run a continuous Slay the Spire 2 stream, rotating characters until one achieves a full-act victory. Log findings between runs.

## Character rotation

Play in this order. On death (or full-act win), swap to the next character, then continue cycling:

1. Ironclad
2. Silent
3. Regent
4. Defect
5. Necrobinder

Loop indefinitely. Stop only when a character completes act III with a win, or the user intervenes.

## State-schema reminders (verified in prior session)

- `hand` is `{cards:[]}`, not a bare array.
- Card objects in `hand` are flat `CardModel`; in `handSelect` and `cardRewardOptions` they are nested.
- `energy` lives at the combat top level, not under `.player`.
- `intents` is an array and can contain dual-intent entries.
- Powers use `.title`, not `.name`.

## Session log protocol

Between every character swap (death or win), append to `docs/gauntlet-findings.md`:

- Character, act/floor reached, HP at death, killing enemy (or win details).
- New bugs, schema deviations, command quirks.
- Do not compress findings that contradict prior entries — keep both.

## Hard rules (from AGENTS.md)

- One tick at a time. No wrapper scripts. No batched commands.
- Check live `state.json` before every decision; never act on stale state.
- Query MemPalace before tactical decisions involving named bosses, unfamiliar relics/potions, or known bridge bugs.
- Authority order: `SKILL.md` → `docs/` → `HermesBridgeCode/` → MemPalace.

## Startup sequence

1. Read `AGENTS.md`.
2. Read `SKILL.md` in full.
3. Read `docs/bridge-protocol-notes.md`.
4. Read `docs/hermes-sts2-runbook.md`.
5. Skim `docs/gauntlet-findings.md` for prior failure patterns.
6. Start Ironclad run.
