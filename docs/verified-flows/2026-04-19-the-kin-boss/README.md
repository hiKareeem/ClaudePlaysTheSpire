# Act 1 boss: The Kin — verified 2026-04-19 (loss)

Evidence archive for the first live boss attempt through HermesBridge. The run
ended in player defeat on turn 6, but the fight produced a clean end-to-end
trace covering combat start, multi-turn play, potion use, turn end, GameOver,
and auto-return to MainMenu.

## Outcome

- **Result**: Defeat at end of player turn 6.
- **Final state** (`state-final.json`):
  - Ironclad: 1 HP / 80 max (combat snapshot) — 2 HP / 80 on the run snapshot
    (minor timing mismatch between combat-side and run-side writes).
  - Kin Priest: 52 / 190 HP (72% dealt, ~138 damage total).
  - Kin Follower A: 22 / 59 HP.
  - Kin Follower B: 21 / 58 HP.
- **Takeaway**: deck was under-scaled. Primal Force / Giant Rock burst landed
  on Priest but Followers were largely ignored, giving them six full turns of
  damage output.

## Kin composition (confirmed)

Three enemies: **1× Kin Priest + 2× Kin Follower**.
(Previous notes referenced "Champ" — that was a misremembering. Only
`Kin Priest` and `Kin Follower` (×2) appear in `state.combat.enemies[]`.)

Per state.json at turn 1 snapshot:
- `Kin Priest` — 190 max HP, large support/damage threat.
- `Kin Follower` ×2 — ~58–59 max HP each, consistent melee damage.

Follower HP mismatch (59 vs 58) is normal — enemies roll HP within a small
range on spawn.

## Turn-by-turn summary (from trace.log)

| Turn | Notable plays | Player HP end | Notes |
|------|---------------|---------------|-------|
| 1 | Thunderclap, Twin Strike → Priest, Anger → Priest, Strike → Priest | ~150 | `Defend` handIndex=0 failed once (`ok=False`) — target on a self-skill. |
| 2 | Inflame, Defend, Breakthrough (9 AoE, -1 HP) | ~120 | Breakthrough self-damage did not trigger Rupture (no Rupture in deck). |
| 3 | Primal Force, Offering, Primal Force (×2) | ~95 | Two Primal Force casts in a single turn — generated 6 Giant Rocks between them. |
| 4 | Defend ×2, Giant Rock → Priest ×3 | ~70 | Priest took ~48 damage this turn; Followers still untouched. |
| 5 | Bottled Potential (slot 2), Bash+ (ok=False), Defend ×2, Thunderclap | ~30 | `Bash+` at reported handIndex=2 returned `ok=False` — likely the Bottled Potential hand-refresh bug: state.json handIndex didn't match real hand. |
| 6 | Anger → Priest, Giant Rock → Priest, Defend, Breakthrough | 1 | Ended turn; enemy burst closed the gap. GameOver fired at T+17:48:07. |

Timing reference: combat started T+17:25:13, GameOver at T+17:48:07 → ~23 min
clock time (lots of deliberation between plays).

## Cards observed this fight

Added to the Ironclad reference:
- **Anger** — played turn 1 and turn 6 against Kin Priest. Exact rider (adds
  copy to discard? damage value?) still TBD.
- **Offering** — played turn 3. StS1: lose 6 HP, gain 2 energy, draw 3,
  exhaust. StS2 behavior not yet verified numerically. `ok=True` on the
  play, followed immediately by two Primal Force casts (consistent with
  +2 energy burst).

Both should be added to `reference-ironclad.md` under attacks/skills with
`[seen but mechanics TBD]` markers on next pass.

## Confirmed bridge behaviors

- `DispatchPlayCard target=<none>` works for Breakthrough (AoE), Thunderclap
  (AoE), Inflame (power), Primal Force (generator), Defend (self).
- `DispatchPlayCard target=Kin Priest` works by name-match on enemies.
- `DispatchUsePotion slot=2 potion=BOTTLED_POTENTIAL target=The Ironclad`
  returned `ok` but triggered the known hand-refresh bug.
- `GameOverScreen auto-dismissed via ReturnToMainMenu` fired ~3 s after the
  GameOver screen was ready (line 561, ~3.0 s after line 557). Clean exit.

## Confirmed bugs observed in this fight

1. **Bottled Potential hand refresh** — turn 5 `Bash+` play failed
   (`ok=False`). The real hand had been shuffled; `handIndex=2` from state.json
   did not resolve the card the controller expected. See
   `bridge-protocol-notes.md`.
2. **PlayCard with stale target** — turn 1 `Defend target=<none>` succeeded
   (line 292) but an earlier `Defend handIndex=0 target=<none>` at line 260
   returned `ok=False`. Likely the handIndex pointed at a non-Defend after
   a draw; same kind of state-refresh lag.
3. **Run-snapshot vs combat-snapshot HP mismatch** at GameOver (1 HP vs 2 HP).
   Minor — both writers race around the death event.

## Files

- `trace.log` — full 565-line trace from run start through GameOver and
  return to MainMenu.
- `state-final.json` — post-GameOver state snapshot (MainMenu screen, final
  combat entry still present).

## Strategic notes for next attempt

- **Kill a Follower first**, not the Priest. Followers are ~58 HP each;
  one Primal Force+Giant Rock sequence kills a Follower outright. Reducing
  incoming damage by ~33% per dead Follower is worth more than chipping at
  the 190-HP Priest.
- Burst turns (Primal Force × 2 in turn 3) are wasted without target discipline.
- Deck lacked scaling: no Inflame+, no Demon Form, no Limit Break. The single
  Inflame on turn 2 was the only +Str source. Boss-scale HP pools require
  at least one scaling power to be reliable.
- Bottled Potential is risky under the current bridge refresh bug — prefer
  other potions until the bug is fixed.
