# 7-Run Autonomous Gauntlet — Master Findings

**Date**: 2026-04-21  
**Protocol**: HermesBridge via IPC (state.json / commands.json)  
**Result**: 0 Wins, 7 Losses — massive bridge & game knowledge gained

---

## Gauntlet Scoreboard

| Run | Character   | Result | Floor | Boss/Elite              | Cause of Death                                                     |
| --- | ----------- | ------ | ----- | ----------------------- | ------------------------------------------------------------------ |
| 1   | Necrobinder | LOSS   | ~F10  | —                       | Early run, pre-detailed logging                                    |
| 2   | Necrobinder | LOSS   | F17   | Kin Boss                | Overwhelmed by boss scaling                                        |
| 3   | Defect      | LOSS   | F16   | Ceremonial Beast (Boss) | Ringing(1) status blocked all attack card plays                    |
| 4   | Ironclad    | LOSS   | F11   | Map dead-end            | `available=[]` — Winged Boots unusable via bridge, no forward path |
| 5   | Silent      | LOSS   | F3    | Nibbit                  | 3 HP at Aggressive turn, couldn't survive                          |
| 6   | Regent      | LOSS   | F6    | Bygone Effigy (Elite)   | Out of block at 3 HP vs 23 dmg attack                             |
| 7   | Regent      | LOSS   | F8    | Phrog Parasite (Elite)  | INFESTED_POWER(4) poison killed at 1 HP EoT                       |

### Per-run logs
- `docs/verified-flows/2026-04-21-run2-necrobinder-kin-loss/`
- `docs/verified-flows/2026-04-21-run3-defect-boss-loss/`
- `docs/verified-flows/2026-04-21-run4-ironclad-inprogress/`
- `docs/verified-flows/2026-04-21-run5-silent-nibbit-loss/`
- `docs/verified-flows/2026-04-21-run6-regent-effigy-loss/`
- `docs/verified-flows/2026-04-21-run7-regent-phrog-loss/`

---

## Bridge Command Reference (Verified)

### Core Commands
| Command | Parameters | Notes |
|---------|-----------|-------|
| `StartRun` | `character` (uppercase: Ironclad, Silent, Defect, Necrobinder, Regent) | AbandonRun first if in progress |
| `Proceed` | none | Routes by screen. On RestSite SKIPS rest — choose option FIRST |
| `PlayCard` | `handIndex`, `expectedCardId`, optional `targetIndex` (0-based) | Omit targetIndex for Self/AllEnemies. Passing it to AllEnemies → CMD_ERROR |
| `EndTurn` | none | Benign CMD_ERROR if combat already ended |
| `SelectMapNode` | `col`, `row` | Must be in `available[]`. 3s sleep after RestSite choice before map refresh |
| `SelectEventOption` | `optionIndex` | 0-based |
| `SelectReward` | `rewardIndex` | Top-level `.index` on reward entry |
| `SelectCardOption` | `cardIndex` | ONLY for `NCardRewardSelectionScreen` (combat card rewards) |
| `SelectCardsInGrid` | `cardIndices=@(int)` ARRAY | For Smith, card removal, Dew Gaze, enchant, choose-N-from-deck |
| `HandConfirmSelect` | `cardIndices=@(int)` | For Armaments-style upgrade (handSelect.active=true, mode=UpgradeSelect) |
| `SelectRestOption` | `optionIndex` | HEAL=0, SMITH=1. Sleep 3+s before Proceed or map may be untravelable |
| `OpenChest` | none | Opens treasure |
| `SkipReward` | `rewardIndex` | REQUIRES rewardIndex parameter |
| `SkipAllRewards` | none | Skips all remaining rewards |
| `Purchase` | `category`, `index` | category: character_card, colorless_card, potion, relic |
| `PurchaseCardRemoval` | none | Shop card removal service |
| `LeaveShop` | none | Advances floor; can't re-enter |
| `AbandonRun` | none | From active run |
| `ReturnToMenu` | none | Returns to MainMenu from Map (escape stuck states) |
| `ContinueRun` | none | Resume after ReturnToMenu |
| `DiscardPotion` | `slotIndex` (required) | Frees a potion belt slot — array index, not a property on potion objects |
| `UsePotion` | `slotIndex`, `expectedPotionId` | **BROKEN** for choice-screen potions (Skill, Fire, Power, Colorless, Orobic Acid) — returns ok=True but does nothing, wedges slot. Direct-effect potions (Explosive Ampoule, Beetle Juice, Strength Potion) work fine. slotIndex = array index in `$s.run.potions[]` |
| `SelectTreasureRelic` | (unknown params) | For treasure room relics — exists in dispatcher but wasn't tested |

### Known Missing/Unverified
- `Winged Boots` activation — no known bridge command to use path-skip charges
- `SelectTreasureRelic` — exists but param schema untested
- `GiveUp` — exists in dispatcher (line 63), untested

---

## State Schema (Verified)

### Root
`schemaVersion`, `modVersion`, `revision`, `source`, `screen`, `rewards`, `cardRewardOptions`, `combat`, `run`, `event`, `shop`, `restSite`, `treasure`, `map`, `cardGrid`, `chooseACardScreen`, `handSelect`

### Screen Names
`MainMenu`, `Map`, `Combat`, `Event`, `Room:Shop`, `Room:RestSite`, `Rewards`, `CardGridSelection`, `GameOver`

### `$s.run`
`currentHp`, `maxHp`, `gold`, `totalFloor`, `character`, `potions[]` (fields: id, title, description, rarity, targetType — **no slotIndex field**), `map`

### `$s.combat`
`energy`, `maxEnergy`, `roundNumber`, `stars` (Regent only), `hand.cards[]`, `enemies` (SINGLETON when 1 enemy — always `@($s.combat.enemies)`), `player` (has `block`, `powers[]`), `orbs[]` (Defect only)

### `$s.combat.hand.cards[]`
`.handIndex`, `.id`, `.title`, `.description`, `.type`, `.rarity`, `.energyCost`, `.effectiveEnergyCost`, `.targetType`, `.isUpgraded`, `.isPlayable`, `.tags`

### `$s.combat.enemies[]`
`.name`, `.currentHp`, `.maxHp`, `.isAlive`, `.block`, `.powers[]` (`.id`, `.amount`), `.intents[]` (`.kind`, `.intentType`, `.title`, `.damage`, `.repeats`)

### `$s.map`
`rowCount`, `colCount`, `currentCoord:{col,row}`, `available:[{col,row,pointType,state}]`. PointTypes: Monster, Shop, Elite, RestSite, Treasure, Unknown, Boss, Ancient

### `$s.rewards` (ROOT level)
Kinds: Gold(`.amount`), Card(`.cards[]` with `.index`), Relic(`.relic`), Potion(`.potion`)

### `$s.cardGrid`
Smith: `screenType=NDeckUpgradeSelectScreen`, `.cards[]` with `{index, card:{title,id,...}}` — **nested under .card, not flat**

### `$s.shop`
`playerGold`, `characterCards[]`, `colorlessCards[]`, `potions[]`, `relics[]`, `cardRemoval{index,cost,used}`. Shop items nested under `.detail.card` not flat `.card`

### `$s.handSelect`
`active`, `mode` (SimpleSelect, UpgradeSelect), `cards[]` — cards have `handIndex` but **no `.title`** in SimpleSelect mode

### `$s.event`
`title`, `options[]` — fields: `.index`, `.title`, `.description`, `.textKey`, `.historyName`, `.isLocked`, `.disableOnChosen`, `.wasChosen`, `.isProceed`, `.relic`. **No `.label`**. No `.text` on event root.

---

## Enemy Encyclopedia

### Act 1 Regulars
| Enemy | HP | Notes |
|-------|-----|-------|
| Nibbit | 44-46 | Hits 12-14. Simple beatdown. |
| Shrinker Beetle | 38 | Empower→Str scaling. Hits 7-13. |
| Twig Slime (S/M) | 8-28 | Part of Slimes_Weak encounters. Low damage. |
| Leaf Slime (S/M) | 12-35 | Alternates Aggressive/Strategic. |
| Fogmog | 74 | Summons Eye With Teeth (6 HP, revives on Heal). Kill Fogmog directly. |
| Fuzzy Wurm Crawler | 56-57 | Empower turns → Aggressive scaling dmg. |
| Flyconid | 47 | — |
| Vine Shambler | 61 | Scaling attack 6→8→16 |
| Mawler | 72 | Empower scaling dmg (4→8→11→14→15). Must kill fast. |

### Act 1 Elites
| Elite | HP | Notes |
|-------|-----|-------|
| Bygone Effigy | 127 | Starts with SLOW_POWER + SleepIntent (safe setup turns). Gains Str rapidly via Empower. Hits 23+ when aggressive. |
| Byrdonis | 83 | Territorial +1 Str EoT, 17 dmg / 4x3 multi. |
| Phrog Parasite | 63 | **INFESTED_POWER(4)** — applies 4 poison EoT. Low raw damage but poison lethal at low HP. Strategic first turn. |

### Act 1 Boss
| Boss | HP | Notes |
|------|-----|-------|
| Ceremonial Beast | 252 | PLOW_POWER(150), Str scaling via Empower. **RINGING_POWER(1)** blocks attack card plays — hypothesized mechanic. Alternates Empower/Aggressive/Strategic. |

---

## Character Mechanics

### Ironclad
- **Burning Blood** (starter relic): heal 6 HP end of combat
- **Bash**: 2e, 8 dmg + 2 Vuln. Vuln applies AFTER Bash's own damage (Bash hit not boosted)
- **Perfected Strike**: 2e, 6 + 2×(Strike-tagged cards in deck). Scales with deck composition
- **Second Wind**: 2e, exhaust all non-attacks, 5 block per card exhausted
- **Armaments**: triggers `handSelect.active=true mode=UpgradeSelect` — use `HandConfirmSelect cardIndices=@(handIndex)`

### Defect
- **Orb queue is FIFO**: channel appends RIGHT, evoke takes LEFT
- **Dualcast**: evokes LEFTMOST orb TWICE (consumes 1 orb, fires 2 evoke triggers)
- **Thunder+**: pure buff (no channel). THUNDER_POWER(8): +8 dmg to each enemy on Lightning evoke
- **Lightning orb**: passive 3 dmg random enemy per card played. Evoke 8 dmg
- **Frost orb**: passive 2 block EoT. Evoke 5 block
- **Dark orb**: passive 6, evoke 6
- **Consuming Shadow**: 2e, channels 2 Dark + EoT evoke leftmost
- **Focus**: +1 per +1 to Lightning passive / Frost block

### Silent
- **Ring of the Snake** (starter): draw 2 extra cards at combat start
- **Survivor**: triggers handSelect (mode=SimpleSelect) to pick discard target
- **Neutralize**: 0e, 3 dmg + 1 Weak — premium
- **Slimed status** devastating — no natural exhaust mechanism

### Regent
- **Star mechanic**: Venerate gives ⭐⭐(2). Falling Star, Particle Wall cost stars to play. When stars=0, star-costing cards show `isPlayable=False`.
- **Forge**: star generation mechanic. Spoils of Battle (Forge 5 + Draw 2), Wrought in War (7 dmg + Forge 7), Furnace power (Forge 4/turn).
- **Sovereign Blade**: 2e, 14 dmg, Retain — appears in hand from Forge mechanic, NOT in deck
- **Divine Right** (starter): +3 stars at combat start
- **`$s.combat.stars`**: integer tracking current star resource

---

## Relics Discovered
| Relic | Rarity | Effect |
|-------|--------|--------|
| Burning Blood | Starter (Ironclad) | Heal 6 HP EoC |
| Ring of the Snake | Starter (Silent) | Draw 2 extra cards at combat start |
| Divine Right | Starter (Regent) | +3 stars at combat start |
| Silver Crucible | — | Upgrades ONLY first 3 card rewards; first Treasure chest = EMPTY |
| Centennial Puzzle | — | First HP loss each combat → draw 3 |
| Lasting Candy | Uncommon | Every other combat, card rewards gain a Power |
| War Paint | — | Upgrade 2 random Skills on pickup |
| Small Capsule | — | Random relic on pickup |
| Nutritious Oyster | — | +11 Max HP |
| Meat on the Bone | Rare | Heal 12 HP EoC if HP ≤50% |
| Phial Holster | — | +1 potion slot, 2 random potions |
| Winged Boots | — | 3× ignore-path charges (unusable via bridge) |
| Juzu Bracelet | Common | No regular enemies in ? rooms |

---

## Potions
### Direct-Effect (Work via UsePotion)
- Strength Potion: +2 Str permanent
- Speed Potion: +5 Str this turn
- Snecko Oil: rare draw potion
- Beetle Juice: -30% enemy dmg for 4 turns
- Explosive Ampoule: direct damage
- Fruit Juice: (effect TBD)

### Choice-Screen (BROKEN via UsePotion)
- Power Potion, Fire Potion, Skill Potion, Colorless Potion, Orobic Acid
- UsePotion returns ok=True but does nothing. Wedges belt slot.
- **Must use `DiscardPotion`** to free belt slot when stuck

---

## Strategic Lessons

1. **EoT damage effects are lethal at low HP** — INFESTED_POWER poison kills regardless of block. Account for all EoT effects before ending turn.
2. **Hand index shifting** — card indices change as cards leave hand. Must re-read state after each play.
3. **Map dead-ends** can occur — `available=[]` with no forward path. Winged Boots doesn't help via bridge. ReturnToMenu to escape.
4. **Potion belt management** — Choice-screen potions can't be used outside combat. If belt fills with them, rewards collection loops infinitely. Use `DiscardPotion` to free slots.
5. **Monologue + 0-cost chain** — Monologue (0e, +1 Str per card played) with Falling Star, Collision Course, and Monologue itself = massive Str scaling in a single turn.
6. **Bash Vuln timing** — Vuln applies AFTER Bash's own damage. Subsequent attacks same turn get the 1.5× boost.
7. **Defect Ringing lockout** — Ceremonial Beast's RINGING_POWER(1) can block attack card plays entirely. Need non-attack damage sources (orbs, powers).
8. **Regent star economy** — must maintain positive star generation. Venerate, Gather Light, Solar Strike, Glow are star-positive. Furnace provides passive star generation. Running out of stars = key cards become unplayable.
9. **Enchant events are rare and powerful** — permanent +dmg to a card. Prioritize enchanting high-impact cards.
10. **Ironclad Perfected Strike** — scales with total Strike-tagged cards in deck, not just hand. Adding more Strikes (including Pommel Strike) increases its damage.

---

## Known Bridge Bugs

1. **UsePotion on choice-screen potions** — returns ok=True, does nothing, wedges belt slot
2. **Map dead-end with Winged Boots** — no bridge command to activate path-skip charges
3. **Stale state.json during sub-screen transitions** — combat object can persist from prior run after StartRun. Trust `$s.run.character` and `$s.run.totalFloor` over stale combat snapshot.
4. **SelectTreasureRelic** — command exists but param schema unverified; treasure relics may be uncollectable
5. **EndTurn after combat ends** — returns CMD_ERROR 'no current player' (benign)
6. **handSelect cards lack `.title`** — in SimpleSelect mode, cards only have `handIndex`, making title-based selection impossible
7. **SkipReward requires rewardIndex** — bare SkipReward without rewardIndex loops infinitely
