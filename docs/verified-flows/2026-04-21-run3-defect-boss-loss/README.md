# Run 3 Defect — Act 1 Boss Loss

## Final Result: DEFEATED at F16 Ceremonial Beast (Act 1 Boss)

**Death turn (T8)**: HP 13, 2 energy, hand had Focused Strike+, Ball Lightning, Strike, Zap, Strike.
Cards refused to play (returned `TryManualPlay returned false`). Likely cause: **Ringing(1) power** — applied by Ceremonial Beast mid-fight, prevents attack cards from resolving (untested hypothesis but fits observed behavior). EndTurn → Beast attacked 15 dmg → HP 13-15 → DEAD.

## New Schema Discoveries This Run

### `SelectCardsInGrid` (NOT `SelectCardOption`)
- For Smith upgrade screen and any `NSimpleCardSelectScreen`/`NDeckCardSelectScreen` (Smith, card removal, Dew Gaze, choose-N-from-deck cards).
- Command: `@{type='SelectCardsInGrid'; cardIndices=@(<int>)}` (ARRAY of indices).
- Dispatcher: `BridgeCommandDispatcher.cs:70` / `:1936`.
- `SelectCardOption cardIndex=N` is only for `NCardRewardSelectionScreen` (combat rewards).

### Stale state.json during sub-screen transitions
- After closing map back to Smith screen, state.json showed `Screen=MapClosed` and `cardGrid=null` but the actual Smith grid was live.
- Bridge commands target live Godot objects (e.g., `Patches.CardGridSelectionConnectPatch.LastScreen`), not the state.json snapshot.
- **Lesson**: Don't trust stale state.json to block commands. Send the command; it works if the underlying screen is live.

### Proceed routing pitfalls
- `Proceed` from Rest→Map transition can return ok=True but leave map untravelable (`available=[]`) when mid-animation. User-verified manual fix: close map → on Smith grid.
- **Workaround**: sleep 3+ seconds after RestSite choice before Proceed.

### Dualcast mechanics CORRECTED
- **Dualcast evokes LEFTMOST orb, TWICE.** (Previously incorrectly noted as rightmost.) Consumes 1 orb, triggers 2 evokes.
- With Thunder+ active: Lightning evoke x2 = 2x (8 direct + 8 Thunder) = 32 AoE damage.
- Orb queue is FIFO: channel appends to RIGHT, evoke takes LEFT.

### Thunder+ does NOT channel Lightning
- Pure Power Self buff: applies `POWER:THUNDER_POWER(8)`.
- Description: "Whenever you Evoke Lightning, deal 8 damage to each enemy hit."
- Synergy: requires separate Lightning channel source (Ball Lightning, Zap).

### Consuming Shadow (2e Rare Power Self)
- Channels 2 Dark orbs.
- Applies `POWER:CONSUMING_SHADOW_POWER(1)`: at end of your turn, Evoke your leftmost Orb.
- **Dark orb**: passive=6, evoke=6 (at channel time; likely scales per turn).

### AllEnemies targeting
- Cards with `targetType='AllEnemies'` (e.g., Sweeping Beam) MUST NOT pass `targetIndex`.
- Passing it returns CMD_ERROR 'TryManualPlay returned false'.

### Potion flaws
- **Skill Potion (slot 1) did not activate** — `UsePotion` returned ok=True but no picker screen opened and potion remained in slot. Similar to the Fire Potion bug user mentioned. Hypothesis: potions that spawn choice screens may not dispatch correctly via bridge in the middle of combat.
- **Explosive Ampoule worked correctly** (10 AoE, AllEnemies, no picker needed).

### New relic: Lasting Candy (Uncommon)
- "Every other combat, card rewards gain a Power."
- Obtained F14 Elite Byrdonis reward.

## Run Progression Final Table
| F   | Type                | HP    | Gold | Outcome                                    |
| --- | ------------------- | ----- | ---- | ------------------------------------------ |
| 1   | Neow                | 75/75 | 99   | Silver Crucible                            |
| 2   | Beetle              | 73/75 | 119  | Cold Snap+ (SC#1)                          |
| 3   | Nibbit              | 61/75 | 139  | Thunder+ (SC#2)                            |
| 4   | Slimes              | 61/75 | 155  | Focused Strike+ (SC#3)                     |
| 5   | Fogmog              | 49/75 | 173  | Skill Potion + Sweeping Beam               |
| 6   | Shop                | 49/75 | 173  | Left without buying                        |
| 7   | Elite Bygone Effigy | 14/75 | 217  | WON; +Centennial Puzzle; +Consuming Shadow |
| 8   | Vine Shambler       | 1/75  | 234  | WON (clutch); +Ball Lightning              |
| 9   | Treasure            | 1/75  | 234  | Empty chest (Silver Crucible penalty)      |
| 10  | Rest Heal           | 45/75 | 234  | +22 HP                                     |
| 11  | Jaxfruit+Flyconid   | 32/75 | 246  | WON; +Sunder                               |
| 12  | Rest Smith          | 32/75 | 246  | Upgraded Sunder→Sunder+                    |
| 13  | Event Tablet of Truth | 52/75 | 246 | Chose Smash, +20 HP heal                   |
| 14  | Elite Byrdonis      | 40→22/75 | 282 | WON; +Subroutine, +Lasting Candy, +Explosive Ampoule |
| 15  | Rest Heal           | 54/75 | 282  | +22 HP                                     |
| 16  | **Boss Ceremonial Beast (252HP)** | 54→0/75 | 282 | **LOSS** |

## Ceremonial Beast Boss Profile (Act 1 Boss)
- **252 HP**, Powers: `PLOW_POWER(150)` [unknown], `STRENGTH_POWER` scaling via Empower turns.
- **Pattern**: Alternates Empower (buff +2 Str, no dmg) and Aggressive Attack.
  - T1 Empower, T2 Aggressive 18, T3 Empower, T4 Aggressive 24, T5 Aggressive 26, T6 Aggressive..., T7 Strategic (0 dmg), T8 Aggressive 15.
  - Eventually debuffs player: **applied `RINGING_POWER(1)` to player** around T7-T8 (likely tied to damage thresholds or turn count).
- Ringing(1) likely prevented attack cards from playing in final turn.

## Strategic Lessons
1. **Don't bring Cracked Core Defect into long fights without evoke tech.** Lightning orb alone provides only 3 dmg/card; insufficient for 252 HP boss.
2. **Sunder+ as finisher requires sub-32 HP targets for energy refund loop.** Used T1 at full HP = no refund.
3. **Ball Lightning + Dualcast + Thunder+ is the real damage engine.** Execute this combo before entering Empower turns.
4. **Pre-boss rest**: HEAL was correct over SMITH (already had 4 upgrades; HP more valuable).
5. **Skill Potion unreliability** — same bug class as fire potion. Do not depend on potions that spawn choice screens.
6. **Test Explosive Ampoule potion always worked** — prefer direct-effect potions.

---

# Run 3 Defect — Live Session Findings

## Bridge Schema

### Purchase command (SHOP)
```
Send-BridgeCommand -Command @{
  type='Purchase'
  category='character_card' | 'colorless_card' | 'potion' | 'relic'  # lowercase_underscore
  index=<int, 0-based into that category's list>
}
```
**NOT** `merchantKind`. Dispatcher: `BridgeCommandDispatcher.cs:1501-1522`.

For card removal use separate command:
```
Send-BridgeCommand -Command @{ type='PurchaseCardRemoval' }
```

### Leaving a shop
`LeaveShop` command advances floor counter (Floor 5→6 at leave).
⚠️ **WARNING**: Once LeaveShop is called, you cannot re-enter — shop is gone, even if current map pos still shows the shop node. Make all purchases BEFORE calling LeaveShop.

### Shop state shape (`$s.shop`)
- `playerGold` — gold (preferred over $s.run.gold for live update)
- `characterCards[]`, `colorlessCards[]`, `potions[]`, `relics[]` — each entry: `{index, entryKind, cost, enoughGold, isStocked, detail: {kind, isOnSale?, card/potion/relic}}`
- `cardRemoval` — `{index, cost, used}`, called via `PurchaseCardRemoval` (no index param needed)

### Rewards shape (confirmed F4/F5)
Card reward shape: `.index` for SelectReward, then SelectCardOption with `cardIndex` (0-based into `.cards[]`).
Card object has `.description` (NOT `.effectsText`), `.type` ('Attack'/'Skill'/'Power'), `.rarity`, `.targetType`.
Gold reward: amount varies (+16, +18, +20 observed).

### Silver Crucible rule
Upgrades only the **first 3 card rewards** of the run. F5 onwards = unupgraded cards.

## Defect Mechanics
- Lightning orb passive (3 dmg random enemy on each card play) triggers per card, NOT per turn. 2 card plays = 2 passive ticks. Can kill low-HP adds incidentally (killed 6HP Eye With Teeth in F5).
- **Dualcast + Thunder+ combo**: Dualcast (1e, targetType=Self, evoke current orbs twice). Thunder+ power triggers 8 AoE on each Evoke. So Dualcast = 2 Thunder triggers = 16 AoE minimum. Core combo.
- Focus buff from Focused Strike+: "+2 Focus this turn" — boosts Lightning passive from 3→5 and Frost block from 2→4 per tick. Does NOT boost Strike damage.
- Frost orb: passive = block per turn end, evoke = block on demand. Default passive=2, evoke=5.

## Enemy Intelligence
- **Fogmog**: 74 HP, starts with Summon intent (brings Eye With Teeth 6HP) on T1, then Aggressive escalating dmg (8→15→9 cycle observed).
- **Eye With Teeth**: 6 HP, alternates Heal/Strategic — WILL REVIVE Fogmog summons if not killed. Priority kill.
- **Nibbit**: 46 HP, alternates Aggressive 12 / Lighter attack 6.

## Run Progression So Far
| F | Type | HP | Gold | Event |
|---|------|-----|------|-------|
| 1 | Neow | 75/75 | 99 | Silver Crucible chosen |
| 2 | Shrinker Beetle | 73/75 | 119 | Cold Snap+ added (SC upgrade #1) |
| 3 | Nibbit | 61/75 | 139 | Thunder+ added (SC upgrade #2) |
| 4 | Slimes Weak | 61/75 | 155 | Focused Strike+ added (SC upgrade #3) |
| 5 | Fogmog | 49/75 | 173 | Skill Potion + Sweeping Beam |
| 6 | Shop | 49/75 | 173 | **BUG: wrong cmd schema, left w/o buying** |
| 7 | Elite | TBD | TBD | ... |

Deck so far (14 cards): 4 Strike, 4 Defend, Zap, Dualcast, Cold Snap+, Thunder+, Focused Strike+, Sweeping Beam.
Potions: Speed, Skill, EMPTY.
