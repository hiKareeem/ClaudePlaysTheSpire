# Run 5: Silent — LOSS at F3 (Nibbit)

**Date**: 2026-04-21  
**Character**: Silent  
**Seed**: 17840346142006181407  
**Result**: LOSS Floor 3  
**Cause**: Death to Nibbit (42 HP) at 3 HP — 14-dmg Aggressive attack impossible to block  
**Final HP**: 0/70  

## Setup

- **Starter Relic**: Ring of the Snake (draw 2 extra at combat start)
- **Neow**: Phial Holster (+1 potion slot, 2 random potions)
- **Starting Potions**: Flex Potion (+5 Str this turn), Colorless Potion (free random colorless card)
- **Starting Deck**: 5×Strike, 5×Defend, 1×Survivor, 1×Neutralize

## Floor Log

| Floor | Type | Encounter | Result | HP After | Notes |
|-------|------|-----------|--------|----------|-------|
| 1 | Monster | Slimes_Weak (3) | WON T14 | 13/70 | Slimed flooded hand; 57 dmg taken |
| 2 | Monster | Shrinker Beetle 39 HP | WON T5 | 3/70 | Survived by blocking; beetle had unexpected DR |
| 3 | Monster | Nibbit 42 HP | **DEAD** T4 | 0/70 | T1 full block (15>12), T4 14-dmg hit unblockable at 3 HP |

## Card Picks

- **F1**: Backflip (1e Skill Self: 5 Block + Draw 2)
- **F2**: Sucker Punch (1e Attack AnyEnemy: 8 dmg + 1 Weak)

## Analysis

- **F1 Slimes_Weak was catastrophic**: 14 turns, HP dropped 70→13. Slimed status cards (cost 1, unplayable) flooded hand every turn. Silent has no natural exhaust (unlike Ironclad's Second Wind).
- **3 HP after F1 = death sentence**: No realistic recovery path. Every fight risks instant death.
- **Potions unused**: Flex Potion (offense only) and Colorless Potion (risky random card) couldn't save us. Both are poor defensive tools.
- **Ring of the Snake** draws 2 extra = 7-card hand initially. Mixed blessing — more options but more Slimed drawn.
- **Survivor handSelect**: Cards in `$s.handSelect.cards[]` have `handIndex` but no `.title` property.

## Discoveries

- Potion array in `$s.run.potions` — items have no `.id`/`.title` property (structure TBD, likely different field names).
- Silent has extreme difficulty with status-heavy fights early — no card cycling or exhaust.
- Weak from Neutralize/Sucker Punch reduces incoming damage but not enough when HP is critically low.
