# Necrobinder autonomous run — 2026-04-20 (Floor 0 → Floor 17, Boss death)

Full autonomous Necrobinder run driven via Bash-based `autopilot-lib.ps1` invocations.
Character: Necrobinder. Died to The Kin (Act 1 boss) on Floor 17.

## Run summary

| Floor | Type | Outcome | HP | Gold | Notes |
|-------|------|---------|----|------|-------|
| 0 | Neow | Neow's Talisman (upgrade 1 Strike + 1 Defend) | 66/66 | 99 | `CardGridApply` indices @(0,5) |
| 1 | Monster | Nibbit (45 HP), 4 rounds | 66→? | +gold | RHH free attack useful |
| 2 | Unknown→Event | Jungle Maze Adventure, Join Forces | — | +47 | No HP loss |
| 3 | Monster | Fuzzy Wurm Crawler (56 HP), 4 rounds | — | — | Unleash+Bodyguard combo |
| 4 | Unknown→Event | Shrinker Beetle (39 HP), 3 rounds | — | — | Shrink debuff (-1 dmg/attack) |
| 5 | Monster | Ruby Raiders ×3, 6 rounds | 66→22 | — | Heavy damage. Focused Tracker→Brute→Crossbow |
| 6 | Card reward | Picked Danse Macabre | 22/66 | 202 | Power: gain 4 block on 2+ cost cards |
| 7 | Unknown→Event | The Future of Potions | — | — | Traded Fortifier for Sic'Em+ |
| 8 | Monster | Vine Shambler (61 HP), 5 rounds | 22→18 | — | Tangled (+1 cost) complicated plays |
| 8 | Card reward | Picked Hang | 18/66 | — | Rare, 1 cost, 10 dmg + doubles all Hang dmg |
| 9 | RestSite | Rest (+19 HP) | 18→31 | — | `SelectRestOption` |
| 9 | Treasure | Planisphere relic | 31→31 | — | Heal 5 HP on entering ? rooms |
| 10 | Unknown→Combat | Slithering Strangler + Snapping Jaxfruit | 31→19 | +18 | Constrict stacks (3→6). Osty died, revived via Bodyguard |
| 10 | Card reward | Picked Calcify | 19/66 | — | Power: Osty attacks +4 damage |
| 11 | Unknown→Event | Unrest Site, Rest Anyways | 19→66 | — | Full heal, Poor Sleep curse (Unplayable, Retain) |
| 12 | RestSite | Smith: Bodyguard→Bodyguard+ | 66/66 | — | Summon 5→7 |
| 14 | Elite | Bygone Effigy (127 HP), 6 rounds | 66→47 | +45 | Slow + Hang mechanics. Big Hat relic |
| 14 | Card reward | Skipped (Eidolon/Grave Warden/Drain Power) | 47/66 | 325 | — |
| 15 | Monster | Cubex Construct (65 HP), 5 rounds | 47→32 | +13 | Calcify + Sic'Em+ synergy. Picked Deathbringer |
| 16 | RestSite | Rest (+20 HP) | 32→51 | — | Needed before boss |
| 17 | BOSS | The Kin (3 enemies), 5 rounds | 51→0 | — | Kin Priest (190 HP) + 2 Followers. Died round 5 |

### Final deck (19 cards)

Strike+, Strike ×3, Defend+, Defend ×3, Bodyguard+, Unleash+, Drain Power,
Right Hand Hand, Pull Aggro, Danse Macabre, Sic'Em+, Hang, Calcify,
Deathbringer, Poor Sleep (curse)

### Final relics

Bound Phylactery, Neow's Talisman, Planisphere, Big Hat

### Final potions

Weak Potion, Fortifier ×1

---

## Protocol reference — verified commands

All commands verified during this autonomous run. Listed here to consolidate
lessons from multiple sessions into one reference.

### Map / Navigation

| Action | Command | Params |
|--------|---------|--------|
| Travel to map node | `SelectMapNode` | `col`, `row` |
| Proceed from screen | `Proceed` | `@{ type='Proceed' }` |

### Rest Site

| Action | Command | Params |
|--------|---------|--------|
| Rest (heal) | `SelectRestOption` | `optionIndex=0` |
| Smith (upgrade) | `SelectRestOption` | `optionIndex=1` |
| Pick card to upgrade | `SelectCardsInGrid` | `cardIndices=@(N)` |

NOT `SelectRestSiteOption`, NOT `ApplyCardGrid`.

### Treasure

| Action | Command | Params |
|--------|---------|--------|
| Open chest | `OpenChest` | — |
| Pick relic | `SelectTreasureRelic` | `index` |

### Events

| Action | Command | Params |
|--------|---------|--------|
| Choose event option | `SelectEventOption` | `optionIndex=N` |

NOT `index=N`.

### Rewards

| Action | Command | Params |
|--------|---------|--------|
| Collect reward | `SelectReward` | `rewardIndex=N` |
| Skip reward | `SkipReward` | `rewardIndex=N` |
| Pick card from reward | `SelectCardOption` | `cardIndex=N` |

NOT `CollectReward`. `rewardIndex` comes from `rewards[i].index` field, NOT
array position.

### Combat

| Action | Command | Params |
|--------|---------|--------|
| Play card | `PlayCard` | `handIndex=N`, optionally `targetIndex=N` |
| End turn | `EndTurn` | — |
| Use potion | `UsePotion` | `slotIndex=N` (NOT array index) |
| Hand select (Snap etc.) | `HandSelectCard` | `handIndex=N` |

### Critical targeting rules

1. **Enemy target is array position** in `combat.enemies[]`, NOT a field on
   the enemy object. When enemies die, the array shrinks and indices shift.
2. **Self-target cards** (Defend, Bodyguard, etc.) MUST NOT have `targetIndex`
   param — causes `TryManualPlay returned false`.
3. **AllEnemies cards** (Deathbringer, etc.) also must NOT have `targetIndex`.
4. **Potion `slotIndex` ≠ array index.** Empty slots persist in the middle.
   Slot 0 may be Weak Potion, slot 1 empty, slot 2 Fortifier. Check
   `run.potions[]` null entries to map correctly.

### Card data nesting

- Card reward options: `cardRewardOptions.cards[i].card.title` (nested
  under `.card`), same pattern for `cardGrid.cards`.
- Hand cards: `combat.hand.cards[i]` with `handIndex`, `title`,
  `effectiveEnergyCost`, `targetType`.

---

## Combat strategy lessons (Necrobinder-specific)

### Osty (summoned ally)

- Surfaces in `combat.allies[]` with HP, powers, etc.
- Has **Die For You** power — absorbs unblocked attack damage.
- Grows via Bodyguard (Summon 5→7 with Bodyguard+).
- Can die and be revived via Bodyguard or similar summon cards.
- **Calcify** (Power) adds +4 damage to all Osty attacks permanently.
- **Sic'Em+** triggers Osty attack (6 base + Calcify bonus).

### Card synergies observed

- **Danse Macabre** (Power) + any 2+ cost card = gain 4 block. Works with
  Pull Aggro, Deathbringer, etc.
- **Pull Aggro** (Summon 4 + 7 block) — note: block appeared as 3 on one
  play, possibly state refresh lag or debuff interaction.
- **Hang** stacks a multiplier (Hang=2→4), doubling all Hang damage to target.
  Strong against single high-HP enemies.
- **Drain Power** appears to auto-upgrade without HandSelect prompt. Upgraded
  Unleash → Unleash+ mid-combat, Bodyguard → Bodyguard+.
- **Deathbringer** (2 cost) applies 21 Doom + 1 Weak to ALL enemies. Good
  opener for multi-enemy fights.

### Debuffs encountered

- **Shrink**: -1 damage per attack (from Shrinker Beetle event).
- **Tangled**: attacks cost +1 for the turn (from Vine Shambler).
- **Constrict**: end-of-turn stacking damage (from Slithering Strangler).
  Amount grows (3→6 over rounds).
- **Slow** (enemy power): each card played gives +10% attack damage to that
  enemy this turn. Observed on Bygone Effigy.

### Boss strategy failure — The Kin

The Kin has 3 enemies: 2 Kin Followers (~58-59 HP each) and 1 Kin Priest
(190 HP). Followers gain Strength over time. Key mistakes:

1. **Should have killed Followers first** to reduce incoming damage. Focusing
   the 190 HP Priest let followers escalate Strength unchecked.
2. **Fortifier potion used too late** — round 5 at 17 HP instead of earlier
   when we had more buffer.
3. **Weak Potion unused** — should have been thrown at Priest to reduce
   incoming damage.
4. **Doom (21/round) insufficient** against 190 HP boss — needed burst
   damage rather than slow attrition.

---

## Bridge stability and bugs observed

### No IPC errors or stalls

The entire 17-floor run completed without IPC communication failures, state
corruption, or bridge stalls. Session ID persistence fix (from earlier session)
worked correctly.

### State staleness patterns

1. **Post-EndTurn state can lag** — wait for `roundNumber` to advance before
   reading combat state. Revision counter helps detect stale reads.
2. **Post-Smith deck snapshot lags** — upgrade only visible after Proceed to
   Map, not immediately after `SelectCardsInGrid`.
3. **Potion effects not immediately visible** in block/power stats — takes
   a tick or two to reflect.
4. **Send-BridgeCommand result state can be stale** — always re-read with
   `Read-State` / `Get-State` for fresh data.

### Potential bugs (not bridge defects)

1. **Strike damage anomaly**: Strike dealt 3 damage instead of 6 against
   Crossbow on Floor 5. Cause unclear — possibly a debuff from Shrinker
   Beetle event carried over, or enemy armor/reduction not surfaced in state.
2. **Pull Aggro block anomaly**: appeared to give 3 block instead of 7 on
   one play. Could be state refresh lag or a debuff reducing block.

### Screen transitions

- **MapClosed is transitional** — poll for next screen rather than assuming
  immediate state.
- **Multiple Proceeds may be needed**: Rewards → RewardsClosed → Map.
- **Unknown nodes** resolve to their actual type (Event, Combat, etc.) only
  after traveling to them.

---

## Driver pattern (Bash-based autopilot)

```
# Each command invocation:
powershell -c ". E:\...\autopilot-lib.ps1; Clear-Ipc; Send-BridgeCommand @{type='...'}; Start-Sleep -Ms 500; Wait-StateChange -TimeoutMs 5000; Get-State"
```

### Key rules

1. **Dot-source `autopilot-lib.ps1` each call** — ensures fresh state.
2. **One `Send-BridgeCommand` per call** — keeps commands atomic.
3. **`Clear-Ipc` before each batch** — prevents stale commands blocking dispatch.
4. **Session state persisted** to `autopilot-session.json` in IPC directory
   (prevents stale command replay from ID mismatch).
5. **`Reset-Session -StartingId N`** at session start to sync with bridge's
   `_lastProcessedId`.

### Hand index tracking

Hand card indices shift after every card play. When playing multiple cards
per turn, must recalculate indices after each PlayCard command. The state
read after each play reflects the new hand ordering.

---

## Combat data schema quick reference

### combat (top-level keys)

```
currentSide, discardPile, drawPile, encounter, enemies, energy,
exhaustPile, hand, maxEnergy, player, roundNumber, stars, allies
```

### enemies[] entry

```
hp, maxHp, block, intents[], powers[], name/title
```

- `intents[]` has `kind`, `intentType`, `title`, `label`, `description`,
  `damage`, `repeats`.
- `powers[]` has `title`, `amount`, `description`, `type`, `isVisible`.

### allies[] entry (Osty)

```
hp, maxHp, block, powers[], name/title
```

- Die For You power visible in powers array.

### hand.cards[] entry

```
handIndex, title, description, energyCost, effectiveEnergyCost,
targetType, keywords[], tags[], isUpgraded, willExhaust, willRetain
```

- `targetType` values observed: `AnyEnemy`, `Self`, `AllEnemies`.
- `effectiveEnergyCost` accounts for discounts (T fix from prior session).
