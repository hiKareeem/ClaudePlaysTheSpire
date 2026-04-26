# 7-Run Autonomous Gauntlet — Master Findings

> **OFF-LIMITS to SpireBench trial agents.** This file contains accumulated
> strategy lessons, enemy/relic/character intel, and run narratives from
> prior autonomous runs. All bridge-protocol findings have been merged into
> `SKILL.md` and `docs/bridge-protocol-notes.md` (audit 2026-04-26). Trial
> agents must not read this file. Operators and the maintainer use it as a
> historical record only. See `docs/benchmark/protocol.md` for the trial-v0
> allowed-reading list.

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
| 14  | Ironclad    | LOSS   | A2F29 | Decimillipede (Elite, 3 segments) | HP 41→0; 3 segments with Reattach (revive in 2 turns), 28+ combined incoming dmg, only 24 HP. Best gauntlet run so far — first Act 1 boss win (Ceremonial Beast). |
| 15  | Silent      | LOSS   | A1F7  | Twig Slime M            | HP 9→0; 4-slime marathon left HP at 8, Twig M attacked for 11. No BB heal for Silent, HP management critical. |
| 16  | Regent      | LOSS   | A1F17 | Vantom Boss             | HP 6→0 R7 Dismember; star economy strong (Black Hole), Slippery 9 burned, but low HP spiral. |
| 17  | Defect      | LOSS   | A2F24 | Decimillipede (Elite)   | HP 3→0; same 3-segment Reattach elite as Run 14. Hailstorm+Frost engine, A1 boss win (Kin Priest). |
| 18  | Necrobinder | LOSS   | A1F17 | Kin Priest Boss         | HP 2→0 R7; Invoke×3 ramp, Eradicate 55 dmg at 5E. Osty Die-for-You absorb. Kin Priest multi-attack overwhelming. |
| 19  | Ironclad    | LOSS   | A1F15 | 3 Raiders               | HP 12→0 R3; Droplet of Precognition SelectCardsInGrid FAILED. Low HP from Nibbit fight + Raiders burst. |
| 20  | Silent      | LOSS   | A1F8  | Phrog Parasite (Elite)  | HP 4→0; Envenom+Poisoned Stab+Bouncing Flask engine, but Phrog 4×4 multi-attack at 4HP unsurvivable. |
| 21  | Regent      | LOSS   | A1F17 | Ceremonial Beast Boss   | HP 10→0 R6 Ringing(1 card); best Regent run. Black Hole+Devastate+ star economy. CB Plow triggered. |
| 22  | Defect      | LOSS   | A2F5  | The Obscura             | HP 23→0 R6; A1 boss win (Vantom, Fairy+Reptile Trinket synergy). Parafright Illusion revives, Obscura Str scaling. |
| 23  | Necrobinder | LOSS   | A1F17 | Kin Priest Boss         | HP 13→0 R7; Sleight of Flesh++ (13 dmg/debuff) + Debilitate+Enfeebling combo. Frozen Egg+COTV++. |
| 24  | Ironclad    | LOSS   | A1F12 | Leaf Slime M + Flyconid | HP 16→0; Juggernaut+Unmovable block engine, Paper Phrog. Frail+Vuln debuffs at low HP, dual enemy burst. |
| 25  | Silent      | LOSS   | A1F11 | Vine Shambler           | HP 1→0; Poison engine (Bouncing Flask+Deadly Poison+Outbreak). No BB heal, HP spiral from F5 onward. |
| 26  | Regent      | LOSS   | A1F14 | Phrog Parasite + Infection | HP 3→0; COTS (1 block/★ spent) star-block engine, Infection Status (3 dmg EoT unplayable) mathematically impossible. |
| 27  | Ironclad    | LOSS   | A2F22 | Spiny Toad (Thorns 5)   | HP 5→0; A1 boss win (Ceremonial Beast). Cruelty+Vuln synergy. Pael's Wing sacrifice. Thorns prevented attack-based kill at low HP. |
| 28  | Ironclad    | LOSS   | A1F17 | Kin Priest Boss         | HP 6→0 R3 boss; entered boss at 34 HP, 3-enemy boss (Kin Priest 190hp + 2 Followers). Weak Potion on Priest helped but ~14 dmg/turn incoming overwhelmed. Neow: Precise Scissors (-1 Strike). Deck: Vuln/Str synergy (Bash, Uppercut, O2P, Hemokinesis, Vicious, Inflame, Rupture, Perfected Strike, Flame Barrier, Battle Trance). Key: SLIPPERY_POWER halves damage, Vuln bonus confirmed working, Infection status unplayable. |

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
- `GiveUp` — exists in dispatcher (line 63), untested

### Newly Verified (Session 2026-04-23)
- `SelectTreasureRelic` uses `index` param — **CONFIRMED WORKING** (Run 14, F10)
- `ChooseACard` uses `cardIndex` — for ChooseACardSelection screen (Skill/Power/Fire/Colorless potion choice). NOT SelectCardOption.
- `Purchase` confirmed: `category` (character_card, colorless_card, potion, relic, card_removal) + `index`
- `AnyEnemy` target cards need `targetIndex` even with single enemy on field

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

---

## Runs 14-27 Cross-Run Analysis (2026-04-24)

### Scoreboard Summary: 27 runs, 0 wins
- Best runs: Run 14 Ironclad (A2F29), Run 17 Defect (A2F24), Run 27 Ironclad (A2F22)
- A1 boss wins: Runs 14, 17, 22, 27 (4/27 = 15%)
- Most common death: Low HP spiral (15/27 runs), elite/boss burst (12/27)
- Character performance: Ironclad > Defect > Regent > Necrobinder > Silent (by floor depth)

### New Mechanics Verified (Runs 14-27)
- **Cruelty**: 1E Power, Vuln targets take +25% dmg (multiplicative with Vuln = 1.5×1.25=1.875). Verified Run 27.
- **COTS (Child of the Stars)**: 1E Power, gain 1 block per ★ spent. Stacks x2. Verified Run 26.
- **Juggernaut**: 2E Power, 5 random dmg per block gain. Verified Run 24.
- **Unmovable**: 2E, doubles first block gain each turn. Verified Run 24.
- **Paper Phrog**: Vuln = 75% more dmg instead of 50%. Verified Run 24.
- **Sleight of Flesh+**: 13 dmg per debuff applied (incredible with Debilitate+Enfeebling Touch). Verified Run 23.
- **Fairy in Bottle**: Auto-revive at 30% HP, triggers Reptile Trinket (+3 Str). Verified Run 22.
- **Pael's Wing**: Sacrifice card rewards to Pael, every 2 sacrifices → relic. Verified Run 27.
- **Colorful Philosophers**: Act 2 event, pick 3 cards from another character's pool (Common+Uncommon+Rare tier). Verified Run 27.
- **Spiny Toad**: Act 2 enemy, 116HP, Thorns(5), alternates Empower and Aggressive (23 dmg scaling).
- **Decimillipede**: 3 segments (42+44+40), Reattach revive in 2 turns. Killed Runs 14+17. AVOID unless massive AoE.
- **The Obscura**: 123HP + Parafright (Illusion=revives). Focus Obscura only, ignore Parafright. Verified Run 22.
- **Infection**: Unplayable Status, deals 3 dmg EoT per copy. Mathematically impossible at low HP. Verified Run 26.
- **Double Energy**: In StS2 gives +1 energy (NOT double). Verified Runs 17, 22.

### Critical IPC Lessons (Runs 14-27)
- **autopilot-lib.ps1 at repo root** — use `Send-BridgeCommand @{ type='X'; param=val }` inline (NOT run-cmd.ps1 with -ParamsJson)
- **handSelect.active** — checked via raw state.json only (read-combat.ps1 doesn't show it)
- **rewardPosition shifts** — after each SelectReward/SkipReward collection, remaining shift down
- **Use `$true`** for PowerShell booleans
- **Deferred state** — wait for revision bump after PlayCard before next action
- **Act 2 map bug** — row-0 off-grid nodes not in `available[]`, use SelectMapNode directly
- **Proceed on RestSite** — auto-skips rest. ALWAYS use SelectRestOption(0=Rest, 1=Smith)
- **ChooseACard** — for Skill Potion/ChooseACardSelection screen (NOT SelectCardOption)
- **Purchase** — needs `category` (CharacterCard/ColorlessCard/Potion/Relic/CardRemoval) + `index`
- **Self/AllEnemies** — Do NOT include `targetIndex` param; just `{handIndex:N}`

### Recurring Death Patterns
1. **Decimillipede** (Runs 14, 17): 3 segments + Reattach = need massive AoE or AVOID
2. **Kin Priest** (Runs 18, 23): 190HP + 2 Followers, sustained damage overwhelms
3. **Low HP spiral**: No BB heal for Silent/Regent/Necrobinder — HP management critical
4. **Phrog Parasite Infested**: 4 Wrigglers spawn on death, chip damage kills
5. **Infection** (Run 26): Unplayable Status, 3 dmg EoT, mathematically impossible at low HP
6. **Thorns** (Run 27): Spiny Toad Thorns(5) prevents attack-based finish at low HP


### Run 29 — Silent (A1F7)
- **Result**: LOSS
- **Death**: A1F7, Shrinker Beetle + Fuzzy Wurm Crawler. HP 7 with 5 block, Beetle hit through block.
- **Neow**: Phial Holser (+1 potion slot + 2 random potions)
- **Path**: Monster(3,1)→Unknown(3,2)→Monster(2,3)→Monster(1,4)→Shop(1,5)→Monster(2,6)
- **Deck at death (19 cards)**: 5 Strike, 5 Defend, 1 Neutralize, 1 Survivor, 1 Dodge and Roll, 1 Deadly Poison, 1 Cloak and Dagger, 1 Haze, 1 Strangle, 1 Pinpoint, 1 Mirage
- **Relics**: Ring of the Snake, Phial Holster
- **Potions used**: Flex Potion (on the killing attempt)
- **Key findings**:
  - Shop commands: `Purchase category='character_card' index=N`, `PurchaseCardRemoval`, `LeaveShop`
  - Shop items are under `state.shop.characterCards`, `state.shop.colorlessCards`, `state.shop.potions`, `state.shop.relics` with `cost`, `isStocked`, `enoughGold` fields
  - Pinpoint (3e, costs 1 less per Skill played) is strong with skill-heavy Silent decks
  - Strangle applies STRANGLE_POWER debuff (2 HP per card played to enemy) — visible in enemy powers
  - Mirage (Block = total Poison on all enemies, Exhaust) — excellent poison synergy card, bought at shop
  - Dodge and Roll "next turn" block procs correctly
  - Shrink power from Shrinker Beetle reduces block by 1 per card AND shows in player powers as SHRINK_POWER(-1)
  - Haze (3e, 4 poison to ALL) — only playable sometimes (Sly keyword, conditional)
  - HP management is critical with poison builds — need more block or healing
  - Fuzzy Wurm gains Str+7 per buff turn, extremely dangerous in multi-enemy fights
  - Colorless Potion, Dexterity Potion obtained during run
  - `state.run` now contains player state (hp, gold, deck, potions, relics) instead of top-level fields
  - `state.map.available` shows travelable nodes with `pointType` and `state` fields

---

## Run 28 — Ironclad (A1F17 Boss Death)

**Date**: 2026-04-26
**Seed**: 15063147177190926645
**Neow**: Precise Scissors (Remove 1 card) — removed Strike
**Result**: DEATH at A1F17 — Kin Priest Boss (HP 34→0 T3)

### Deck at death (18 cards)
3 Strike, 4 Defend, Bash, Hemokinesis, Uppercut, One-Two Punch, Vicious, Flame Barrier, Perfected Strike, Inflame, Battle Trance, Rupture

### Relics: Burning Blood, Precise Scissors, Mercury Hourglass

### Key findings
- **One-Two Punch**: next Attack plays twice. Doubled Bash = 16 dmg + 4 Vuln. Doubled Hemokinesis = 30 dmg.
- **Vicious**: draw 1 when applying Vulnerable. Triggers off Bash and Uppercut.
- **Inflame**: +2 Str Power. Simple, effective.
- **Mercury Hourglass**: 3 dmg to ALL enemies at turn start. Very useful.
- **Perfected Strike**: 6 + 2 per "Strike" card in deck (4 Strikes = 14 dmg for 2e).
- **Rupture**: draw 1 when losing HP from cards. Synergizes with Hemokinesis (2 HP cost).
- **Kin Priest Boss**: 190 HP. Followers (58, 59 HP) assist. Killing Priest makes Followers flee.
- **Vulnerable damage bonus inconsistent** — sometimes applies, sometimes flat damage. Needs more testing.
- **Phrog Parasite spawns Wrigglers on death** — MUST save resources for Wriggler phase.
- **SLIPPERY_POWER on Inklets** reduces incoming damage significantly (~50% or flat reduction).
- **Infection status** (from Infested/Wrigglers): unplayable, 3 end-of-turn damage, clutters hand.

---

## Run 29 — Silent (A1F7 Death)

**Date**: 2026-04-26
**Result**: DEATH at A1F7 — Shrinker Beetle + Fuzzy Wurm Crawler (HP 7→0)

### Deck at death (19 cards)
5 Strike, 5 Defend, Neutralize, Survivor, Dodge and Roll, Deadly Poison, Cloak and Dagger, Haze, Strangle, Pinpoint, Mirage

### Relics: Ring of the Snake, Phial Holser

### Key findings
- **Shop commands**: `Purchase category='character_card' index=N`, `PurchaseCardRemoval`, `LeaveShop`
- **Pinpoint**: 3e, 15 dmg, costs 1 less per Skill played. Can be 0-cost with enough Skills.
- **Mirage**: Block = total Poison on all enemies. Exhaust. Good poison synergy.
- **Strangle**: applies STRANGLE_POWER (2 HP per card to enemy per turn).
- **Haze**: 3e, 4 poison to ALL. Sly keyword = conditional playability.
- **Dodge and Roll**: 4 block now + 4 next turn. Separate block applications.
- **Shrink power**: SHRINK_POWER(-1) reduces block per card. Shows in player powers.
- **Survivor triggers handSelect**: must use `HandSelectCard handIndex=N` before EndTurn.
- **SelectCardsInGrid**: `cardIndices=@(N,N)` for multi-card grid selection (has race condition bug but works).

---

## Run 30 — Regent (A1F10 Elite Death)

**Date**: 2026-04-26
**Seed**: 15063147177190926645 (different seed per run)
**Neow**: Golden Pearl (+150 Gold = 249 starting)
**Result**: DEATH at A1F10 Elite — Phrog Parasite Wriggler spawn (HP 9→0)

### Deck at death (16 cards)
3 Strike, 4 Defend, FallingStar+, Venerate, Radiate, Gather Light, Solar Strike, Convergence, Astral Pulse, Monologue

### Relics: Divine Right, Golden Pearl, Oddly Smooth Stone

### Key Regent mechanics verified
- **Stars resource**: generated by Venerate and other cards. Some cards cost Stars instead of energy.
- **Falling Star** (0e): 8 dmg + 1 Weak + 1 Vuln. Starter. Upgraded = FallingStar+.
- **Venerate** (1e): gain Stars. Core resource generator.
- **Radiate** (0e): 3 dmg to ALL per Star gained THIS turn. Free AOE with Star synergy.
- **Gather Light** (1e): 8 Block + gain Stars. Good defense + resource.
- **Solar Strike** (1e): 9 dmg + gain Star. Attack + resource.
- **Convergence** (1e): gain +1 energy next turn. Energy ramp.
- **Astral Pulse** (0e, 3 Star cost): 14 dmg to ALL enemies. Massive free AOE.
- **Monologue** (0e): each card played this turn gives +1 Str this turn. Insane with low-cost cards.
- **Divine Right relic**: Starter. Unknown effect (needs investigation).
- **Oddly Smooth Stone**: +1 Dex at combat start. Boosts block cards.
- **Fysh Oil potion**: +1 Str and +1 Dex for combat.
- **currentStarCost field**: tracks Star cost. Cards with Star cost show `isPlayable:false` when insufficient Stars.
- **Phrog Parasite spawns 4 Wrigglers on death** — same as Run 28 finding. Confirmed across Ironclad and Regent.

---

## Run 31 — Defect (A1F16 Boss Death)

**Date**: 2026-04-26
**Seed**: 17590763020381124404
**Neow**: Winged Boots (ignore path restrictions 3 times)
**Result**: DEATH at A1F16 Boss — Ceremonial Beast (252 HP, HP 28→10→dead)

### Deck at death (21 cards)
4 Strike, 4 Defend, Zap, Dualcast, Cold Snap, Lightning Rod, White Noise, Ball Lightning, Capacitor, Double Energy, Barrage, 2x Momentum Strike, Go for the Eyes, Beam Cell, Iteration, Storm

### Relics: Cracked Core, Winged Boots, Bag of Preparation
### Potions: Heart of Iron, Flex Potion, Liquid Bronze

### Key findings
- **Ceremonial Beast boss** (252 HP): has Plow(150) power — gains Str+5 when Plow threshold reached. At Str+7 hit for ~18 dmg/turn. Very dangerous.
- **Momentum Strike** (Defect): 10 dmg first play, then 0 cost for rest of combat. Two copies = 20 free dmg/turn. Excellent.
- **Barrage**: 5 dmg per Channeled Orb (scales with orb count). Good synergy with orb generators.
- **Double Energy** (1e): doubles remaining energy, Exhaust. Great with Capacitor for orb slot expansion.
- **Bag of Preparation**: +2 cards at combat start (5→7 draw). Solid relic for Defect.
- **Storm Power**: Channel 1 Lightning per Power played. Good synergy with White Noise.
- **Beam Cell** (0e): 3 dmg + 1 Vuln. Free utility debuff.
- **Go for the Eyes** (0e): 3 dmg + 1 Weak. Free utility debuff.
- **Slippery power** (Inklets): next damage only loses 1 HP. Must pop with cheap attack first.
- **Dualcast in StS2** costs 1e (not 0e like StS1). Does NOT take targetIndex.
- **White Noise**: gives random Power for free. Can give Storm, Iteration, etc.
- **Capacitor**: +2 Orb Slots. Essential for Defect orb synergy.
- **Liquid Bronze potion**: unknown effect (not used).

### Bridge findings
- Hand card data is flat (not nested under `.card`) — fields like `title`, `id`, `energyCost`, `effectiveEnergyCost` are direct on the card object.
- `isPlayable` field works correctly for Star-cost and energy-cost cards.
- Card `tags` array includes "Strike", "Defend" etc.
- Enemy intent field may be `undefined` even during combat.
- Flex Potion shows as power `Flex Potion(5)` on enemies.
- Plow power: `Plow(150)` — threshold mechanic, triggers Str gain.

## Run 32 — Ironclad (A1F8 Elite Death: Phrog Parasite Wrigglers)

**Result**: LOSS | **Character**: Ironclad | **Floor**: A1F8 | **Cause**: Phrog Parasite → 4 Wriggler spawn, died at 5 HP T3 of Wriggler phase
**HP at death**: 0/80 | **Gold**: ~130 | **Deck**: 16 cards

**Neow**: Pomander → upgraded Bash to Bash+ (10 dmg, 3 Vuln instead of 2)

**Deck** (16): 5 Strike, 4 Defend, Bash+, Shrug It Off, Perfected Strike, Iron Wave, Dismantle, Uppercut, Demon Form
**Relics**: Burning Blood, Pomander

**Path**: (2,2)Event[Wellspring: Skill Potion] → (1,3)Monster[Nibbit] → (1,4)Monster[Beetle] → (1,5)Monster[Mawler] → (0,6)Monster[Cubex Construct] → (0,7)Monster[Ruby Raiders] → (0,8)Elite[Phrog Parasite → Wrigglers]

**Key findings**:
- **Dismantle** (1e, 8 dmg, double-hit if enemy Vulnerable) — confirmed working. Must be played AFTER the turn Vuln was applied (Bash+ applies Vuln after its damage, so same-turn Dismantle does NOT double-hit). Next turn with active Vuln = 16 dmg for 1e.
- **Dominate** (1e, +1 Vuln, +1 Str per Vuln stack on enemy, Exhaust) — great Str scaling with Vuln. Gave Str+2 after Uppercut applied Vuln.
- **Demon Form** (3e, +2 Str/turn Rare) — too expensive for emergency situations. Need early setup turns to justify.
- **ChooseACard** command for Skill Potion / card choice screens (NOT SelectCardOption).
- **Wellspring event**: Bottle option gives random Potion. Bathe removes card + adds Guilty.
- **Enemy reindex confirmed**: When enemy dies, remaining enemies reindex (Wriggler at index 1 → new index 0).
- **Iron Wave**: 1e, 5 block + 5 dmg. Block and dmg both scale with Str.
- **Uppercut**: 2e, 13 dmg + 1 Weak + 1 Vuln. With Str bonus, dealt 15+ dmg.
- **Perfected Strike**: 6 + 2 per Strike card in deck. With 5 Strikes = 16 dmg base.
- **Skill Potion**: POTION:SKILL_POTION — Choose 1 of 3 Skill cards, added to hand free this turn. Uses ChooseACard screen.
- **Ruby Raiders**: Tracker(24hp), Brute(30hp), Assassin(19hp). ~18 total dmg/turn with all alive. Focus kill order matters.
- **Cubex Construct**: 65hp, Str+2/turn. Very dangerous if not killed fast.
- **Phrog Parasite spawns 4 Wrigglers on death** — confirmed again (3rd time seeing this). Fatal at low HP without AOE.
- **Vuln 1.5x damage confirmed**: Strike did 9 dmg (6×1.5) with Vuln, consistent across runs.

### Run 33 — Silent — A1F7 Bridge Stall (LOSS)
- **Character**: Silent | **HP at death**: N/A (bridge stall) | **Floor**: 7
- **Cause**: Bridge hooks stopped responding during Fogmog combat. State frozen at T1 with 1 energy. Game process alive but mod update loop stuck. Required game restart, run lost.
- **Deck (17)**: 5 Strike, 5 Defend, 1 Neutralize, 1 Survivor, 1 Abrasive, 1 Precise Cut, 1 Bouncing Flask, 1 Backflip, 1 Tools of the Trade
- **Relics**: Ring of the Snake, Arcane Scroll | **Gold**: ~163
- **Key cards picked**: Backflip (1e, 5 block + draw 2), Tools of the Trade (1e Power, draw 1 discard 1 per turn)
- **Neow**: Arcane Scroll (random Rare → Abrasive: 3e Power, +1 Dex +4 Thorns)
- **Path**: (3,0)→(2,1)M[Wurm]→(2,2)M[Beetle]→(2,3)M[3 Slimes]→(1,4)Shop→(1,5)M[Cubex]→(1,6)M[Fogmog STALL]
- **Fogmog**: 74hp, has Summon intent. Bridge stuck after T1 where ToT + Strike were played.
- **Findings**: 
  - Cubex Construct Artifact(1) blocks 3 of Bouncing Flask's 9 poison (applied 6 instead)
  - Bouncing Flask requires enough energy AND does NOT take targetIndex (error: "card unplayable: bad target")
  - Backflip (1e, 5 block + draw 2) — solid Silent defensive card
  - Tools of the Trade (1e Power, draw+discard at turn start) — confirmed working
  - Bridge stall bug: game can freeze mid-combat, bridge hooks stop writing state.json
  - Ring of the Snake (Silent starter): draw 2 extra cards at combat start
  - Abrasive (3e Power, +1 Dex +4 Thorns): significant investment, good for long fights
  - Precise Cut (0e, 13-2 per other card): terrible with large hands
