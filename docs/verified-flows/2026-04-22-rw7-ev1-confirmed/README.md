# Rw7 + Ev1 — Confirmed Fixed (v0.1.1)

Date: 2026-04-22 (Twitch gauntlet stream)
Related: `docs/gauntlet-findings.md` §"Verified items"

## Summary

The two bridge bugs shipped as fixed in HermesBridge v0.1.1 were observed working across multiple run attempts during the 2026-04-22 Twitch gauntlet stream (Ironclad, Silent, Regent, Defect, Necrobinder rotation).

## Rw7 — Sticky rewards panel closes on last reward ✅

**Fix shipped in:** `HermesBridgeCode/BridgeCommandDispatcher.cs`, v0.1.1 (commit `6a9014a`).

**Observed behavior (post-fix):** `SkipAllRewards` correctly closes the rewards panel when the reward list is empty but the panel is still `Visible`. Verified 6+ times across multiple reward shapes during the gauntlet — no agent got stuck on `Rewards` after the final reward was skipped.

**Pre-fix failure mode:** Panel remained on screen with no entries after skipping the final reward, trapping the agent on `Rewards` screen.

## Ev1 — Event option preview text ✅

**Fix shipped in:** `HermesBridgeCode/BridgeStateExtractor.cs`, v0.1.1 (commit `6a9014a`).

**Observed behavior (post-fix):** `ExtractEventOption` + `BuildEventOptionPreview` correctly reflect over `EventOption` subclass fields (gold, hpLoss, hpGain, maxHpChange, potion, card outcomes). Confirmed on the Sapphire Seed event during the gauntlet — option preview text rendered correctly in `state.json`.

**Pre-fix failure mode:** Event option preview returned blank, forcing agents to commit blindly to event choices.

## Evidence

The per-run stream logs (state.json snapshots, trace.log tails) from 2026-04-22 were not preserved because the agents writing at the time used `docs/sessions/session-ironclad-run2-2026-04-22.md` instead of the `autopilot-session-*.md` + verified-flows pattern mandated by SKILL.md. This gap is what prompted the session-log protocol audit in v0.1.2.

The verification signal is therefore the absence of Rw7/Ev1 entries in any new bug findings during the 2026-04-22 gauntlet despite the full 5-character rotation, combined with the ✅ entries added to `gauntlet-findings.md` line 278-279 by the live agents.

## Follow-up

- No further code action needed on Rw7/Ev1.
- Session log protocol clarified in v0.1.2 (`next-agent-prompt.md`, `docs/sessions/README.md`) to prevent the evidence gap on future verifications.
