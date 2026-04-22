# Run 7: Regent — LOSS at F8 Elite Phrog Parasite

**Date**: 2026-04-21  
**Character**: Regent  
**Seed**: 17893919398861223207  
**Cause of Death**: INFESTED_POWER(4) poison killed at 1 HP after blocking combat damage vs Phrog Parasite (Elite)

## Run Summary

| Floor | Type | Result | HP After | Notes |
|-------|------|--------|----------|-------|
| Neow | - | Nutritious Oyster (+11 Max HP) | 86/86 | +War Paint relic |
| F1 | Nibbit | WON T4 | 71/86 | Picked Particle Wall |
| F2 | 3 Slimes | WON T4 | 57/86 | Picked Wrought in War + Orobic Acid |
| F3 | Event (Wellspring) | Exchange Gold for 2 Potions | 57/86 | Got Power Potion + Orobic Acid |
| F4 | Nibbit | WON T5 | 45→36/86 | Picked Collision Course. Sovereign Blade appeared from Forge. |
| F5 | Shop | Bought Monologue (39g ON SALE, 0e Str-scaling Skill) | 36/86 | 66g remaining |
| F6 | Elite Bygone Effigy | WON T4 at 1 HP! | 1/86 | Epic fight. Meat on the Bone relic + Child of the Stars card. Potion belt full crisis → DiscardPotion saves. |
| F7 | Event (Wellspring) | Bathe: Remove Strike + add Guilty | 1/86 | Belt full, couldn't take Bottle option |
| F8 | Elite Phrog Parasite | **DEAD T2** | 0/86 | INFESTED_POWER(4) poison at end of turn killed at 1 HP |

## Final Deck (14 cards)
- 3× Strike, 4× Defend, 1× Falling Star, 1× Venerate, 1× Spoils of Battle, 1× Wrought in War, 1× Collision Course, 1× Monologue, 1× Child of the Stars

## Relics
- Divine Right (starter, +3 stars at combat start)
- Nutritious Oyster (+11 Max HP from Neow)
- War Paint (from Small Capsule → upgraded 2 Defend)
- Meat on the Bone (heal 12 HP EoC if ≤50%)

## Key Discoveries

### New Enemy: Phrog Parasite (Elite)
- **63 HP**, has INFESTED_POWER(4) — applies 4 Poison at end of player turn
- Strategic turn first (no direct damage), then MultiAttackIntent(4)
- Low raw damage but poison is lethal at low HP

### Regent Card Discoveries
- **Spoils of Battle** (1e Skill): Forge 5 + Draw 2. Massive star generation.
- **Wrought in War** (1e Attack): 7 dmg + Forge 7. Attack + star economy.
- **Collision Course** (0e Attack): 11 dmg + Debris. Free damage.
- **Monologue** (0e Skill): +1 Str per card played this turn. Insane scaling with 0-cost cards.
- **Child of the Stars** (1e Power): gain 2 Block per star spent. Defensive scaling.
- **Sovereign Blade** (2e Attack, 14 dmg, Retain): appears in hand from Forge mechanic, not in deck.

### Bridge Discoveries
- **DiscardPotion slotIndex=N** frees a belt slot. Critical when full of choice-screen potions.
- Choice-screen potions (Power Potion, Orobic Acid) cannot be used outside combat.
- SkipReward requires rewardIndex parameter.
- `$s.run.potionSlots` does not exist as a property.

### Strategic Lessons
- INFESTED_POWER poison damage at EoT is deadly — must account for it when at low HP.
- At 1 HP, ANY EoT damage effect is lethal regardless of block.
- Monologue + 0-cost card chain (Falling Star, Collision Course, Monologue itself) = massive Str scaling.
- Elite pathing at low HP is extremely risky — should prioritize RestSite.
