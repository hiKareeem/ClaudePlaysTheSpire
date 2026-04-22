# Run 2 — Necrobinder vs The Kin (Loss, Floor 17, Turn 10)

## Outcome
Defeated on Turn 10 of The Kin boss fight. HP 7→0 from combined Priest (12) + Follower2 (11+Str6 buffed) attacks after insufficient block.

## Character / Loadout
- **Character:** Necrobinder, Ascension 0
- **Final HP:** 66 max
- **Gold:** 196
- **Relics:** Bound Phylactery, Precise Scissors, Sparkling Rouge, Gorget, Reptile Trinket (+1 Str per potion used, stacked x3)
- **Deck:** 16 cards including unplayed Necro Mastery+
- **Potions at death:** Snecko Oil (used T10), Fire Potion (wedged since Run 1 from UsePotion targetIndex bug)

## Bridge Schema Findings (Verified)

### 1. `PlayCard` requires BOTH `expectedCardId` AND `handIndex`
First Soul card play attempt with only `expectedCardId` returned `ok:false` silently. Re-submission with both fields worked. **Schema requires handIndex as primary locator; expectedCardId is stale-guard only.**

### 2. `UsePotion` with `slotIndex` + `expectedPotionId` works for self-target potions
Snecko Oil played successfully from slot 1. Confirms safe pattern for non-targeting potions. **Enemy-targeting potions (Fire Potion) remain broken** — see earlier Run 1 findings.

### 3. Potion state propagation is DELAYED
`UsePotion` returned `ok:true` immediately, but hand size (5→7) and Str buff (3→6 via Reptile Trinket) only appeared after playing subsequent cards. **Always Read-State AFTER each play following a potion, not immediately after UsePotion response.**

### 4. Conditional-cost cards + Snecko Oil interaction UNCLEAR
Flatten ("0 cost if Osty attacked") played successfully after Poke→Osty-attack. But energy accounting suggested Strike failed at what should have been energy=2. Either:
- Flatten's conditional-cost bypasses Snecko randomization (likely)
- Or Snecko set Flatten to cost >0 and conditional waive also applied
Needs more data.

### 5. `s.screen.name` (not `.screenType`)
Correct path: `$s.screen.name`. Value = `'MainMenu'` after death. `$s.combat` becomes null when not in combat.

### 6. HP is on `$s.run`, not `$s.combat.player`
Correct paths: `$s.run.currentHp`, `$s.run.maxHp`. During combat, `$s.combat.player.block` and `.energy` work (but `.energy` errors during some mid-transition reads).

## Tactical Post-mortem

### What went right (T1-T9)
Survived 9 turns of Kin boss on a caster class via Necrobinder's Grave Warden + Bodyguard + Osty ally wall. Killed Follower1 T4. Applied Doom x21 to Priest.

### What went wrong (T10)
- **Opened with Snecko Oil instead of Sow (AoE)**. Should have led with Sow for guaranteed 14 AoE damage to both F2 and Priest before any randomization.
- **Wasted energy on Snecko's random costs** — ended turn with Sow in hand and 0 energy.
- **Strike failed "not enough energy"** due to Snecko unpredictability.
- **Left F2 at 3 HP** — one more attack anywhere would have killed it and removed the 11+Str6 threat.

### Doom mechanic clarification
Priest had Doom x21 applied T2. **No passive HP drain observed over 8 turns.** Doom likely triggers on enemy turn-end or on-kill proc, not per-turn tick. Prior strategy of "kill Priest first to trigger Doom cascade" was invalid.

### Kin Minion power
F2 had Minion x1. Unclear if this ties F2 to Priest (dies with Priest) or Priest to F2. Given Doom didn't drain Priest, this wasn't tested — but for future runs, **killing F2 first remains correct** since F2 Str scales via Empower (x0→x2→x4→x6→... by T10).

## Lessons for future Kin encounters
1. **AoE before single-target** when both enemies present
2. **Never Snecko mid-fight at low HP** — energy unpredictability = dead
3. **Empower scaling** on F2 is the primary time pressure, not Priest's Doom
4. **Target priority:** F2 first (lower HP + escalating threat), Priest second
5. **Fire Potion still wedged** — do not touch slot 2 until bridge fix

## Files Referenced
- `autopilot-lib.ps1` — primitives
- `HermesBridgeCode/BridgeCommandDispatcher.cs:218` — targetIndex schema
- Prior: `docs/verified-flows/2026-04-19-the-kin-boss/README.md`
