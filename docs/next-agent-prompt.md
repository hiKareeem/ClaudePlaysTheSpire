# Next-Agent Prompt: Twitch Stream Gauntlet

Purpose: run a continuous Slay the Spire 2 stream, rotating characters until one achieves a full-act victory. Log findings between runs.

## Character rotation

Play in this order. On death (or full-act win), swap to the next character, then continue cycling:

1. Ironclad
2. Silent
3. Regent
4. Defect
5. Necrobinder

Loop indefinitely. Stop only when a character completes an act with a win, or the user intervenes.

## Overlay discipline (CRITICAL)

This has been the top failure mode in prior sessions. Agents read overlay docs once, then stop calling `Set-OverlayText` after their first context compression.

Rules:

- Every turn, set overlay text reflecting current state and intent (HP, energy, what you're about to do and why).
- After ANY context compression event, immediately re-read `AGENTS.md` and `SKILL.md` §Overlay before your next command.
- Use `Clear-Overlay` between major screen transitions (combat end, map open, event resolve).
- If you notice overlay hasn't updated in >3 commands, that is a regression — fix it immediately and note it in the session log.

See `SKILL.md` lines ~492–494 and ~560–583 for `Set-OverlayText`, `Clear-Overlay`, `New-OverlaySrt` usage.

## Runtime verification tasks (from prior session fixes)

Two bridge fixes shipped unverified at runtime. Verify them opportunistically during the gauntlet:

1. **Rw7 — sticky rewards panel**
   - Fix at `HermesBridgeCode/BridgeCommandDispatcher.cs` DispatchSkipAllRewards (~line 506).
   - Repro: after `SelectReward` or `SkipReward` on the final reward, panel used to remain `Visible` with empty list. New code calls the close path when list is empty but panel visible.
   - Log whether the panel closes cleanly on the last reward of any combat.

2. **Ev1 — event option preview**
   - Fix at `HermesBridgeCode/BridgeStateExtractor.cs` ExtractEventOption + BuildEventOptionPreview (~lines 1610–1700).
   - Now reflects over EventOption subclass fields for gold / hpLoss / hpGain / maxHpChange / potion / card outcomes.
   - Log any event where the preview field is missing, malformed, or wrong compared to the actual outcome.

## State-schema reminders (verified in prior session)

- `hand` is `{cards:[]}`, not a bare array.
- Card objects in `hand` are flat `CardModel`; in `handSelect` and `cardRewardOptions` they are nested.
- `energy` lives at the combat top level, not under `.player`.
- `intents` is an array and can contain dual-intent entries.
- Powers use `.title`, not `.name`.

## Session log protocol

Between every character swap (death or win), append to `docs/gauntlet-findings.md`:

- Character, act/floor reached, HP at death, killing enemy (or win details).
- Any Rw7 / Ev1 observations (confirm working, or repro the bug).
- New bugs, schema deviations, command quirks.
- Do not compress findings that contradict prior entries — keep both.

## Hard rules (from AGENTS.md)

- One tick at a time. No wrapper scripts. No batched commands.
- Check live `state.json` before every decision; never act on stale state.
- Query MemPalace before tactical decisions involving named bosses, unfamiliar relics/potions, or known bridge bugs.
- Authority order: `SKILL.md` → `docs/` → `HermesBridgeCode/` → MemPalace.

## Startup sequence

1. Read `AGENTS.md`.
2. Read `SKILL.md` in full (especially §Overlay).
3. Read `docs/bridge-protocol-notes.md`.
4. Read `docs/hermes-sts2-runbook.md`.
5. Skim `docs/gauntlet-findings.md` for prior failure patterns.
6. Set initial overlay. Start Ironclad run.
