# Run 6: Regent — LOSS at F6 Elite (Bygone Effigy)

**Date**: 2026-04-21
**Character**: The Regent
**Seed**: 17893919398861223207
**Death Floor**: F6 (Elite: Bygone Effigy)
**Death Cause**: Overwhelmed by Bygone Effigy's Strength 10 + 23 dmg/turn at 3 HP

## Setup

- **Neow**: Small Capsule → War Paint (upgrade 2 random Skills → Defend×2 → Defend+)
- **Starter Relic**: Divine Right
- **Starting Deck**: 4×Strike, 2×Defend, 2×Defend+, 1×Falling Star, 1×Venerate

## Floor-by-Floor

| Floor | Type | Result | HP After | Notes |
|-------|------|--------|----------|-------|
| F1 | Nibbit 46 HP | WIN T4 | 71/75 | Falling Star Weak+Vuln opener, picked Particle Wall |
| F2 | Shrinker Beetle 38 HP | WIN T5 | 64/75 | Strike kills + block, Empower scaling dmg |
| F3 | Fuzzy Wurm Crawler 57 HP | WIN T5 | 61/75 | Falling Star+Venerate star engine, Celestial Might |
| F4 | Shop | — | 61/75 | Bought Furnace (36g) + Celestial Might (48g) |
| F5 | 4 Slimes | WIN T7 | 34/75 | Epic 7-turn fight, Celestial Might + star engine, picked Solar Strike |
| F6 | Elite: Bygone Effigy 127 HP | **DEAD T8** | 0/75 | Starved at 3 HP, couldn't survive 23 dmg/turn |

## F6 Elite Combat Log

- **T1** (34/75, stars=3): Venerate (+2→5 stars) + Celestial Might (18 dmg). Effigy sleeping. → 109/127
- **T2** (34/75, stars=5): Furnace (Forge 4/turn) + Strike×2 (12 dmg). → 96/127. Effigy woke, BuffIntent.
- **T3** (34/75, stars=5): Particle Wall (9 block) + Defend+ (8 block) + Solar Strike (9 dmg) + Strike (6 dmg). 17 block vs 23 = took 6 dmg. → HP 28, Effigy 79/127
- **T4** (28/75, stars=4): Defend (5) + Defend+ (8) + Strike (6 dmg). Sovereign Blade NO (not in hand). 13 block vs 23 = took 10. → HP 18, Effigy 72/127
- **T5** (18/75, stars=4): Celestial Might (18 dmg) + Gather Light (8 block). 8 block vs 23 = took 15. → HP 3, Effigy 54/127
- **T6** (3/75, stars=5): **Beetle Juice** (-30% dmg 4 turns) + Particle Wall (9) + Defend+ (8) + Falling Star (8 dmg + Weak + Vuln). 17 block > 16 (reduced) = 0 dmg! → HP 3, Effigy 45/127
- **T7** (3/75): Sovereign Blade (14 dmg with Vuln ≈21). No block available. Enemy hit 23 × modifiers → **DEAD**.

## Discoveries

### Regent Star Mechanics (CONFIRMED)
- `combat.stars` = integer field showing available star count
- Star-positive: Venerate (+2), Gather Light (+1), Glow (+1), Solar Strike (+1), Furnace (Forge 4/turn)
- Star-consuming: Falling Star (2), Particle Wall (2), Guiding Star (2)
- When stars < card cost, card shows `isPlayable=False`
- Furnace applies `FURNACE_POWER(4)` — generates 4 stars at turn start

### Combat State
- `combat.player` = player object with HP, block, powers
- `combat.roundNumber` (not `.round`)
- `combat.stars` = star resource count
- Sovereign Blade appears in hand during combat (not in deck list) — likely from Forge or Divine Right mechanic
- Bygone Effigy starts with SLOW_POWER(1) and SleepIntent — safe setup turns

### Potions
- **Beetle Juice** is a direct-effect potion (UsePotion works!) — reduces enemy damage 30% for 4 turns
- Potion array index = slot for UsePotion. No `slotIndex` field on potion objects.
- Colorless Potion is a choice-screen potion (likely broken like Fire Potion)

### Bygone Effigy Elite Profile
- 127 HP. Slow(1) → sleeping initially. Wakes on first damage.
- Gains Strength rapidly (had Str 10 by T3). Attacks for 23 dmg/turn consistently.
- Alternates Buff/Attack intents once awake.

## Strategy Notes
- Regent star economy is strong (Furnace + Venerate engine) but needs more block cards
- Elite fights are very dangerous at <40 HP with no healing relics
- Should have skipped Elite path at 34/75 HP
- Beetle Juice saved one turn but couldn't overcome sustained 23 dmg
