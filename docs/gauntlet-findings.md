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
| 8   | Ironclad    | LOSS   | A2F15 | The Insatiable (A2 Boss)| HP 31→0; Sandpit (countdown timer) misunderstood — didn't play Frantic Escape |
| 9   | Silent      | LOSS   | A1F16 | The Kin (A1 Boss)       | HP 19→0; Str scaling + multi-enemy boss overwhelmed low HP          |
| 10  | Silent      | LOSS   | A1F6  | Snapping Jaxfruit + Flyconid | HP 6→0; Str scaling + Vuln debuff overwhelmed at 2 HP       |
| 11  | Regent      | LOSS   | A1F10 | 2 Nibbits              | HP 4→0; Str-2 Nibbit 14 dmg, only 5 block, no Fairy left             |
| 12  | Defect      | LOSS   | A2F5  | 2 Chompers (Artifact x2) | HP 4→0; 16 incoming, max 12 block from available hand; orb position issues (Dualcast kept evoking Frost instead of Dark) |
| 13  | Necrobinder | LOSS   | A1F16 | Kin Priest + 2 Followers (A1 Boss) | HP 7→0 T9; Priest Str scaled to 4, incoming 28 dmg with only 3E/no Bodyguard in hand — couldn't both damage and block enough. Calcify+ Osty combo got Priest to 82/190 but couldn't break through. |

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

---

## Run 8 — Ironclad (Act 2 Boss Death)

**Date**: 2026-04-22  
**Result**: LOSS at Act 2 Floor 15 (The Insatiable, 321 HP)  
**Final HP**: 0/80 at death, 31/80 entering fatal turn  
**Death cause**: The Insatiable's Sandpit power ramps damage (8×2 → 10×2 → 28 → 20), combined with Str scaling (gained +2 Str on Buff turns). Low HP from brutal mid-Act fights (Louse Progenitor, Mytes, Bowlbug Beetle) left no buffer.

### Deck (33 cards at death)
5 Strike, 4 Defend, Bash+, PS+, Pommel Strike+, Cruelty+, Twin Strike×2, Iron Wave, Hemokinesis, Uppercut, Thunderclap, Juggling, Pillage, Taunt, Molten Fist, Battle Trance, Hellraiser, Shrug It Off, Whirlwind, Dark Embrace, Colossus++, Fiend Fire, Inflame

### Relics (8)
Burning Blood, Large Capsule, Prayer Wheel, Horn Cleat, Nunchaku, Seal of Gold, Potion Belt, Art of War

### Path taken (Act 2)
r1c2 Monster → r2c1 Event (Symbiote, transformed Armaments → Whirlwind) → r3c1 Event (Merchant???) → r4c1 Monster (Thieving Hopper) → r5c0 Monster (Bowlbug Rock+Silk+Beetle) → r6c0 Event (Ranwid, traded Block Potion → Potion Belt) → r7c0 Monster (Louse Progenitor) → r8c0 Treasure (Art of War) → r9c0 Monster (2 Mytes) → r10c0 Shop (auto-exited) → r11c1 RestSite (Heal 24) → r12c0 Monster (2 Chompers) → r13c0 Monster (Hunter Killer) → r14c1 RestSite (Heal 24) → r15c3 Boss (The Insatiable)

### Notable combats
- **Louse Progenitor** (136 HP, Curl Up x14): Nearly died at 10/80 HP. Block Potion saved. PS+ 54 dmg with Cruelty amp.
- **Bowlbug Rock+Silk+Slumbering Beetle**: Beetle woke despite avoidance strategy. 45 dmg taken. HP dropped to 25.
- **2 Mytes**: Toxic status spam nearly killed at 3/80. Colossus++ + Thunderclap kept alive. Seal of Gold refunded Toxic exhaust energy.
- **Hunter Killer** (121 HP): Tender debuff (-1 Str/Dex per card played). Inflame + Bash+ + PS+ killed at Str 2.

### Key mechanics discovered

1. **The Insatiable (Act 2 Boss)**: 321 HP. Sandpit power (increments, unclear exact mechanic — appears to amplify attack damage and generate Frantic Escape status cards). Gains Str on Buff turns. Spams 6+ status cards per turn. Frantic Escape status: INCREASES Sandpit — DO NOT PLAY.
2. **Cruelty+ (upgraded)**: 50% additional damage to Vuln enemies (vs 25% base). Additive with Vuln 50% = 2.0x total.
3. **Hellraiser**: Auto-plays Strike-tagged cards on draw (both turn-start and card-effect draws like Battle Trance). Massive value in Strike-heavy deck.
4. **Inflame**: +2 Str for combat. Excellent with multi-attack cards (Twin Strike, Whirlwind).
5. **Fiend Fire**: Exhausts hand, 7 dmg per exhausted card (NOT counting itself — only other hand cards). At 4 other cards = 28 dmg. Exhausts self after.
6. **Tender (enemy debuff)**: -1 Str and -1 Dex PER CARD PLAYED this turn. Cumulative within the turn. Resets next turn. Front-load high-Str attacks.
7. **Sandpit (The Insatiable power)**: ⚠️ COUNTDOWN TIMER — when it reaches 0, you die. Frantic Escape status cards ADD to Sandpit counter (extend your life). **ALWAYS play Frantic Escape.** Not playing them is what killed this run. Sandpit does NOT ramp damage directly.
8. **Flutter (Thieving Hopper)**: 50% less damage from attacks. Cancels Vuln 50% exactly. With Cruelty, net = +50% damage.
9. **Hard to Kill**: Caps damage per instance at the stack count (x9 = max 9 dmg per hit). Multi-hit cards help bypass.
10. **Curl Up**: Enemy gains block on first damage instance (once per combat). Burn it early with a weak attack.
11. **Slippery (Inklets)**: First HP-loss instance capped to 1 dmg. Consumed after first hit. Subsequent hits deal full damage.
12. **Colossus++**: 8 block + 50% less damage from Vuln enemies that turn. Excellent defensive tool paired with Thunderclap.
13. **Dark Embrace**: Draw 1 when any card is exhausted. Synergizes with Molten Fist, Fiend Fire, Toxic exhausts.
14. **Toxic status (Mytes)**: 5 dmg EACH at end of turn if still in hand. Costs 1E to exhaust. Enemies spam 2/turn. Seal of Gold may refund the energy.

### Verified items
- **Rw7** ✅ (SkipAllRewards auto-closes panel — verified 6+ times across multiple reward shapes)
- **Ev1** ✅ (Event option preview text surfaced correctly — Sapphire Seed)
- **Au1** 🐛 (Armaments upgrade display-only — card damage values don't reflect the upgrade)

### Bridge quirks rediscovered
- Self-target cards (Defend, Cruelty, etc): omit `targetIndex` entirely (not -1, not 0)
- Hand reindexes after EVERY card play — must re-read before next play
- Seal of Gold energy display is inconsistent (sometimes shows 3/3 after playing cards)
- Nunchaku (every 10 attacks = 1 energy) contributes to confusing energy displays
- Shop: Proceed auto-exits. Need different command to browse/purchase.
- Fiend Fire: only counts other hand cards toward damage, not itself

---

## Run 9 — Silent (Act 1 Boss Death)

**Date**: 2026-04-22
**Result**: LOSS at Act 1 Boss (The Kin — Priest 190 HP + 2 Followers)
**Death cause**: Low HP (19/81) entering Priest-only phase with no potions remaining. Str-scaling Priest overwhelmed.

### Key items
- **Neow**: Nutritious Oyster (+11 Max HP → 81/81)
- **Shop**: Bought Speedster (draw=1 dmg AoE Power), Blur (5 block carry-over), Energy Potion. Removed 1 Strike.
- **Key cards**: Assassinate (Sharp 2, 0E Innate 12 dmg + Vuln), Backflip+ (8 block + draw 2), Speedster, Echoing Slash (10 dmg AoE per kill), Mirage (block = total poison on enemies)
- **Akabeko**: +8 dmg first attack each combat

### Notable findings
- **Speedster**: 1 dmg to ALL enemies per card drawn. Triggers on Backflip+ draws, turn-start draws, Dagger Throw draws, Clarity draws. Stacks (x2 = 2 dmg per draw).
- **Mirage**: Block = total poison on ALL enemies. Only useful in multi-enemy poison scenarios.
- **Dagger Throw handSelect**: Triggers EVERY time. Must resolve with HandSelectCard before any other action.
- **Shop commands**: `Purchase {category, index}`, `PurchaseCardRemoval`, `LeaveShop`
- **Reward order**: Always claim cards BEFORE potions — claiming potions first can close card reward screen.

---

## Run 10 — Silent (Early Death r6c5)

**Date**: 2026-04-22
**Result**: LOSS at r6c5 (Snapping Jaxfruit 31 HP + Flyconid 49 HP)
**Death cause**: HP 6 entering combat → took Vuln x2 from Flyconid → Str-scaling Jaxfruit (13 dmg) + Flyconid (12 dmg) = 25 incoming vs 16 block at 2 HP.

### Key items
- **Neow**: Silver Crucible (first 3 card rewards upgraded, first Treasure empty)
- **Shop (r3c6)**: Bought Outbreak (every 3 Poison apps = 11 AoE dmg), removed 1 Strike.
- **Key cards**: C&D++ (6 block + 2 Shivs), Expertise++ (draw to 6), Backflip++ (8 block + draw 2), Noxious Fumes (2 Poison/turn to all enemies)

### Notable findings
- **Noxious Fumes**: 2 Poison to ALL enemies at start of turn. Excellent sustain damage.
- **Outbreak**: Every 3 Poison applications → 11 AoE dmg. Power, 1E. Needs poison cards to trigger.
- **Snapping Jaxfruit**: Gains +2 Str per Buff turn. At Str 6 = 9 base dmg × Vuln 1.5 = 13.
- **Flyconid**: Alternates Attack (8 dmg) + Debuff (Vuln x2 to player). Deadly combo with Str-scalers.
- **Brute Raider**: +3 Str per Buff turn. At Str 12 = 19 dmg. MUST kill fast.
- **Frail on player**: -25% block from cards. Devastating for Silent.
- **Ring of the Snake**: +1 card draw on first turn (7 cards instead of 5).
- **Silver Crucible**: Card rewards from first 3 combats auto-upgraded. First Treasure is empty.
- **Shivs**: 0E, 4 dmg, Exhaust. Generated by Cloak and Dagger.

---

## Run 11 — Regent (Act 1 Floor 10 Death)

**Date**: 2026-04-22
**Result**: LOSS at r10c1 (2 Nibbits)
**Death cause**: HP 4/75 entering combat with Str-scaling enemies. Nibbit[1] buffed to Str 2 (14 dmg) with only 5 carry-over block. No potions to save.

### Key items
- **Neow**: Lava Rock (Act 1 Boss drops 2 Relics)
- **Shop (r2c5)**: Bought Glow (⭐+draw) + BEGONE! (transform card → Minion Strike)
- **Elite r8c1**: Phrog Parasite (Infested x4) — Fairy in a Bottle saved us at 0 HP

### Key cards
- **Sovereign Blade**: 2E, Retain, 19 base dmg + Forge value. Generated by Divine Right on Forge.
- **Refine Blade**: Forge 9 + Energy Next Turn. Triggers Divine Right → SB.
- **Falling Star**: 0E, costs 2⭐, 8 dmg + Weak 1 + Vuln 1
- **Parry**: Power — SB play → gain 8-10 block
- **Pillar of Creation**: Power — card creation → gain 3-5 block
- **BEGONE!**: 1E, transform hand card → Minion Strike (0E, 6 dmg + draw + exhaust)
- **Big Bang**: Rare — Draw 1, Gain Energy, Forge 5, Exhaust
- **Glitterstream**: 11 block + 5 next turn carry-over

### Regent mechanics verified
- **Stars/Forge**: `combat.stars` starts at 3 each combat. Cards cost ⭐ (Falling Star = 2⭐). Venerate gives ⭐⭐, Glow gives ⭐+draw, Solar Strike gives ⭐+9 dmg.
- **Forge**: Adds to Sovereign Blade damage (19 base + Forge value). SB generated by Divine Right relic. Sometimes SB goes to draw pile instead of hand.
- **Sovereign Blade**: 19 base + Forge dmg added per Forge play. Saw 19 and 28 (19+9 from Refine Blade).
- **Infection status**: 3 dmg EoT. Block DOES absorb it.
- **Fairy in a Bottle**: Triggers at 0 HP, heals 30% Max HP. Single use.
- **Phrog Parasite (Infested x4)**: Spawns 4 Wrigglers (18-21 HP) on death. They gain Str +2/buff and spam Infection.
- **Kunai**: +1 Dex per 3 attacks in single turn.
- **Meal Ticket**: Heal 15 HP when entering shop.
- **Poor Sleep**: Unplayable + Retain curse. Stays in hand permanently.
- **find-stars.ps1**: Created tool to read combat.stars field.

### Bridge quirks
- Falling Star sometimes fails PlayCard — may need re-read after hand reindex before targeting works.
- Hand reindex after EVERY card play confirmed again.

---

## Run 12 — Defect (Act 1 Boss WIN — entering Act 2)

**Date**: 2026-04-23
**Seed**: 8999857598395777821
**Neow**: Neow's Talisman (Upgrade 1 Strike + 1 Defend)
**Result**: ACT 1 WIN at r16c3 — Vantom Boss killed T10, HP 5/96. **Entering Act 2.**

### Deck at Act 1 end (17 cards)
Strike+, Defend+, 2 Strike, 3 Defend, Zap, Dualcast, Capacitor, Coolheaded, Null, Sunder+, Double Energy, Barrage, Shadow Shield, Multi-Cast

### Relics (4)
Cracked Core (Channel 1 Lightning at combat start), Neow's Talisman, Mango (+14 Max HP → max 96), Nunchaku (every 10 attacks = 1 Energy)

### Potions: Power Potion (2 empty slots)

### Act 1 path
r1c3 Shrinker Beetle (0 dmg) → r2c4 Wellspring (Heart of Iron potion) → r3c5 Fuzzy Wurm (4 dmg) → r4c5 3 Slimes (5 dmg) → r5c5 Byrdonis Nest (+7 Max HP → 82) → r6c6 Shop (Sunder 36g SALE + removed Strike) → r7c6 Elite Phrog Parasite (27 dmg, Mango +14HP → 96 max) → r8c6 3 Raiders (13 dmg) → r9c5 Treasure (Nunchaku + 46g) → r10c4 RestSite Smith (Sunder+) → r11c4 Shop (Barrage 51g) → r12c3 RestSite Heal → r13c4 Vine Shambler (13 dmg, +Strength Potion) → r14c3 Jaxfruit+Flyconid (BRUTAL, HP 79→23) → r15c4 RestSite Heal → r16c3 **BOSS VANTOM KILLED T10**.

### Key Defect mechanics verified
- **Orb types**: Lightning (passive 3 dmg random, evoke 8), Frost (passive 2 block, evoke 5 block), Dark (passive 0, evoke 24 dmg)
- **Cracked Core**: Auto-channels 1 Lightning at combat start
- **Sunder+**: 32 dmg, refund 3E on kill. Net 0E if kills. Upgraded from 24 via Smith.
- **Double Energy**: 1E cost, doubles remaining energy. Exhaust. Unplayable at max energy (3/3).
- **Multi-Cast**: X-cost Rare, Evoke rightmost orb X times. With Dark = 24×X potential damage.
- **Null**: 2E, 10 dmg + Weak 2 + Channel Dark. Excellent utility.
- **Barrage**: 1E, 5 dmg per channeled Orb to single target. With 3 orbs = 15 dmg.
- **Shadow Shield**: 2E, 11 block + Channel Dark. Core defense + offense card.
- **Coolheaded**: 1E, Channel Frost + draw 1. Consistent block + draw.
- **Capacitor**: +2 Orb slots. Setup card.
- **Vantom Boss**: 173 HP, Slippery x9 (each dmg instance = 1 HP until all consumed), Str +2 per Buff turn, gives Wounds.
- **Slippery**: Each HP-loss instance capped at 1. Consumed per instance, not per turn. Multi-hit cards burn multiple stacks (Barrage with 2 orbs = 2 stacks burned).

### Bridge quirks
- PlayCard for combat, SelectEventOption for events (NOT ChooseEventOption)
- DiscardPotion uses slotIndex (not slot)
- Purchase {category,index} for shop. PurchaseCardRemoval then SelectCardsInGrid.
- Hand reindexes after EVERY card play AND after potion use.
- Dark Orb passive = 0 dmg (NOT 6 as initially assumed). Only Evoke deals damage.

---

## Defect Run 12 — LOSS at A2F5 (2 Chompers)

**Seed**: 8999857598395777821  
**Neow**: Talisman (upgraded Strike + Defend)  
**Death**: Act 2 Floor 5, 2 Chompers (Artifact x2). HP 4→0, 16 incoming vs 12 max block.

### Act 1 Summary
- Cleared full Act 1 including Vantom Boss (173 HP, Slippery x9) in 10 turns.
- Shop bought Sunder (36g sale) + removed 1 Strike.
- Smith: Sunder+ (32 dmg, refund 3E on kill).
- Elite: Phrog Parasite (Infested x4) → got Mango (+14 Max HP → 96 max).
- Boss: Vantom T10, HP 5/96 survived. Multi-Cast (Rare: Evoke X times) from rewards.

### Act 2 Progress
- r1c2: Tunneler (87 HP, Burrowed). Cleared T4. Multi-Cast failed vs Burrowed (only 1 Dark orb). HP 94.
- r2c2: 3 Exoskeletons (Hard to Kill x9). Cleared T6. Sunder capped at 9. HP 47.
- r3c2: This or That? event → took 48 gold, -6 HP. HP 41.
- r4c2: Hunter Killer (121 HP, Tender). Cleared T7. Sunder+ front-loaded 32 dmg. Evoke ignores Tender. HP 10.
- r5c2: 2 Chompers (62/63 HP, Artifact x2). DEAD T3. 16 incoming, max ~12 block from hand. HP 4→0.

### Key Mechanics Discovered
- **Multi-Cast** (X-cost): Uses ALL energy. Evoke rightmost orb X times. **CRITICAL: If only 1 Dark orb, evokes once then fails remaining.** Need MULTIPLE Dark orbs for value.
- **Orb Position**: Rightmost = most recently channeled. Coolheaded (Frost) after Shadow Shield (Dark) → Frost becomes rightmost → Dualcast evokes Frost not Dark. **Must track orb order carefully.**
- **Pael's Tears**: End turn with unspent Energy → gain +2E next turn. **NEVER TRIGGERED** despite multiple 1E-unspent tests. Possibly bugged.
- **Tender**: -1 Str/Dex per card played, cumulative within turn, resets each turn. First card = full value. Orb evoke damage ignores Tender.
- **Burrowed**: Block persists turn-to-turn. Stunned if all Block removed. Multi-Cast wasted on Burrowed (Dark evoke damage absorbed by block).
- **Tunneler**: 87 HP, gains Burrowed block, can reach 32 block.
- **Hunter Killer**: 121 HP, Tender debuff. Alternates Attack (17 dmg) and multi-hit (7×3). Str scaling.
- **Chomper**: Artifact x2, alternates Attack (8×2) and Status card giving.
- **Double Energy**: 1E cost, doubles remaining energy. Exhaust. Unplayable at max energy (3/3).
- **Shadow Shield++**: 15 block + Channel Dark (upgraded from 11).
- **Dark Orb**: Passive = 0 dmg. Evoke = 24 dmg. Lightning: Passive 3, Evoke 8. Frost: Passive 2 block, Evoke 5 block.
- **Sunder+**: 32 dmg (upgraded from 24). Refunds 3E on kill. Excellent with Double Energy.
- **Barrage**: 5 dmg × number of channeled orbs.
- **Power Potion / Colorless Potion bug**: Consumed without effect (no card selection screen appeared).

### Deck at Death (18 cards)
Strike+, Defend+, 2 Strike, 3 Defend, Zap, Dualcast, Capacitor, Coolheaded, Null, Sunder+, Double Energy, Barrage, Shadow Shield, Multi-Cast, Shadow Shield++

### Relics (5)
Cracked Core, Neow's Talisman, Mango, Nunchaku, Pael's Tears

### Death Analysis
- Root cause: Low HP (10/96) from cumulative Act 2 combat damage + orb position confusion causing wrong evokes (Frost instead of Dark).
- Contributing: Pael's Tears never triggered (would have provided extra energy for more plays). Power Potion consumed without effect. Multi-Cast underwhelming with single Dark orb.
- Lesson: Track orb channel order. Shadow Shield (Dark) before Coolheaded (Frost) to keep Dark rightmost for Dualcast/Multi-Cast.
