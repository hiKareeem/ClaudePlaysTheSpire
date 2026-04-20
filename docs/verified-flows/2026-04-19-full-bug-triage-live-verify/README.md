# Live Verification of Hermes's 2026-04-19 Full Bug-Triage Fixes

Session date: 2026-04-19 (immediately after Hermes handoff-reply landed).
Build: deployed before run; `dotnet build` clean (0 warn / 0 err).
Run: fresh Ironclad, ascension 0, no seed.
Commands issued: 857 – 1092 (continuation: elite fight, restsite, combat, treasure, shop, restsite smith, event, 2× Nibbit floor 14, restsite floor 15, **Act 1 boss The Kin — death on T9**).

## Summary

| Bug | Area | Verdict | Evidence |
|-----|------|---------|----------|
| 3 | PlayCard stale handIndex | ✅ **VERIFIED** | See `verified-flows/2026-04-19-playcard-expectedcardid-guard/` |
| 4 | Rewards ghost entries | ✅ **VERIFIED** | Gold picked → `rewards[]` compacted to only Card entry (ids 873, 884) |
| 5 | Transient payload lingering | ✅ **VERIFIED (incl. treasure)** | Event null after MapScreenOpen (ids 860, 880). Treasure payload populates `hasChestBeenOpened` + `relicChoices` after OpenChest, clears after Proceed (ids 983–987). |
| 6 | CardGrid screen stick | ✅ **VERIFIED** | Card removal + Smith both opened `NDeckCardSelectScreen` / `NDeckUpgradeSelectScreen`, `screen=CardGridSelection` while open, cleared back to `Room:Shop`/`Room:RestSite` after confirm (ids 990–991, 995–996). |
| 7 | Elite relic null | ✅ **VERIFIED** | Bygone Effigy elite dropped `RELIC:ODDLY_SMOOTH_STONE`, non-null, accepted via SelectReward (ids 940–947). Relic worked in combat (Dex 1 power applied combat start). |
| 8 | Run/combat HP mismatch at GameOver | ⏳ not yet triggered | No death |
| 9 | Missing `handIndex` field | ✅ **VERIFIED** | Every hand card carries `handIndex` (0..N-1) compacted after plays |
| 10 | Curse `isPlayable=true` | ✅ **VERIFIED** | Dazed drawn during Fogmog+Eye fight (id range 953–978): `isPlayable:false`, `energyCost:-1`. |
| 11 | Slimed disappearing | ✅ **likely already fixed** | Slimed cards present in discard pile (ids 884) and later drawn into hand (id 891). Type=`Status`, id=`CARD:SLIMED`. No disappearance observed. |
| 12 | Enchant/modifier state | ✅ **VERIFIED structurally (combat-scoped)** | Every card carries `enchantment` + `affliction` fields in combat payload. Out-of-combat `run.deck[]` still shows `enchantment:null` for all cards — note for Hermes: probably expected (no affixes in deck), but confirm field is populated when non-trivial. |
| 13 | Merchant click animation | ✅ **VERIFIED implicitly** | `Purchase` (Feed, cmd 989) and `PurchaseCardRemoval` (cmd 990) both completed without animation stalls; state updated cleanly. |
| 15–17 | Plating/Rupture/Giant Rock mechanics | ⏳ not triggered | None of those relics/cards in deck yet |
| — | EndTurn stale-state guards | ✅ **VERIFIED** | `expectedScreen` + `expectedCurrentSide` + `expectedRoundNumber` accepted and checked (ids 867, 870, 884, 888, 891) |
| — | `Purchase` command (first live test) | ✅ **VERIFIED** | Bought Feed (`category=character_card`, `index=1`, cost 77). Gold 300→223, card in deck, shop slot unstocked (cmd 989). |
| — | `PurchaseCardRemoval` + grid-select flow | ✅ **VERIFIED** | Opens `NDeckCardSelectScreen`, confirm with `SelectCardsInGrid {cardIndices:[N]}`. Gold deducts on confirm, not on invoke (cmds 990–991). |
| — | Smith restsite option | ✅ **VERIFIED** | `SelectRestOption {optionIndex:1}` → `NDeckUpgradeSelectScreen` → `SelectCardsInGrid`. Bash → Bash+ (cmds 995–996). |

## Known bridge issues found DURING verification

1. **Neow / event grants extraction timing** — Large Capsule's 2 relics and +1 Strike/+1 Defend, and Jungle Maze's +53 gold, did not show in the immediate `PostDispatch:*` snapshots. They became visible only on the next snapshot trigger (combat-start / room-enter). Documented at `verified-flows/2026-04-19-large-capsule-anomaly/`. Fix pattern: extend Hermes's `ScheduleDeferredStateRefresh` to `SelectEventOption` and `Proceed`.
2. **Event description templates unresolved** — Jungle Maze Adventure option descriptions contain literal `{SoloGold}` / `{SoloHp}` / `{JoinForcesGold}` placeholders instead of resolved numbers. Also observed on Brain Leech (`{CardChoiceCount}`, `{FromCardChoiceCount}`, `{RipHpLoss}`). Cosmetic for bridge output but blocks informed decision-making by an AI consumer. Low priority.
3. **`targetSlotName` obsolete** — `DispatchPlayCard` uses `targetIndex` (numeric), `targetSelf` (bool), or nothing. Old notes mentioning `targetSlotName` are wrong. Controller taxonomy doc should reflect this.
4. **Intent damage excludes STR bonus** — State extractor's enemy intent damage field does not include the enemy's current Strength modifier. Observed when Fogmog was (accidentally) buffed with Strength Potion: intent damage string didn't reflect the boost. Low priority / informational field.
5. **Brain Leech "Rip the Leech Off" — snapshot-timing only, see #10 below.** Originally flagged as "HP cost not applied"; superseded by observation that HP did update after full event close.
6. **Potion target semantics** — `UsePotion` with `targetIndex:0` on a potion whose `targetType` is `AnyPlayer` (e.g. Strength Potion) targets the enemy at live index 0, not self. Caused Strength Potion to buff Fogmog instead of Ironclad. **For self-use on `AnyPlayer` potions, OMIT `targetIndex`.** Consumer-side gotcha, not a bridge bug, but worth documenting prominently.
7. **Dead enemies retain slot indices** — Enemy array keeps dead enemies in their original positions (`currentHp:0`). `targetIndex` must be the live slot index, not array position. Observed: killed Eye With Teeth stayed at index 0, Fogmog remained at index 1. Consumer-side gotcha.
8. **Shop UI inventory didn't render on entry** — Second shop (floor 10): entering via map travel left the merchant UI blank; user manually invoked it to see stock. Bridge's `shop` payload was fully populated the whole time and `Purchase` worked without the UI rendering. Suggests the UI hydration path and `NMerchantInventory` read path are decoupled. Not bridge-blocking; low-priority game-side bug.
9. **Empty-seed StartRun is deterministic** — `NGame.StartNewSingleplayerRun(seed: "")` treats empty as literal seed "" rather than "pick random", so repeat `StartRun` calls with no `seed` field replay the same run. **PATCHED locally** in `DispatchStartRun`: when the caller omits/empties `seed`, the dispatcher now synthesizes a 64-bit random decimal string before calling the game and logs it (`DispatchStartRun: empty seed, synthesized '<n>'`). Will take effect after the next `dotnet build` + restart. Not yet live-verified.
10. **Brain Leech "Rip" HP cost applies on event exit, not on option select** — Selected Rip (cmd 999); snapshot immediately after still showed HP 42/87. HP only dropped to 37/87 once the event fully closed (double-Proceed cascade past `Rewards`→`RewardsClosed`→map). So the -5 HP DID apply, just visible one snapshot trigger later. Classifying as a **snapshot-timing quirk**, not a missed cost. Similar family to the Neow / Large Capsule deferred-extraction issue (#1). Fix pattern would be the same: `ScheduleDeferredStateRefresh` on `SelectEventOption` / `Proceed`.

## Mechanics observations this session

- **Paper Phrog confirmed at +75%**: Strike (base 6) vs Vulnerable enemy dealt 10 damage (6 × 1.75 = 10.5 → floor 10). Without Paper Phrog would be 9 (6 × 1.5).
- **Twin Strike** with Paper Phrog + Vuln: 16 dmg (8+8, each hit 5 × 1.75 = 8.75 → floor 8).
- **Burning Blood** heals 6 at end of combat — confirmed across two fights.
- **Strawberry** raised maxHp 80 → 87 (+7) ✓ (wiki-accurate).
- **Bash** at base: 8 damage + 2 Vulnerable, cost 2 energy — confirmed.
- **Vulnerable** decays 1 per turn on enemies (observed 2 → 1).

## Known-good command shapes observed

| Type | Required | Notes |
|------|----------|-------|
| `StartRun` | `character` (str) | NOT `characterClass`; `ascensionLevel` optional |
| `PlayCard` | `handIndex`, `targetIndex` or `targetSelf` | `expectedCardId` / `expectedTitle` highly recommended. For self-targeted cards (Defend, Powers) OMIT target; `targetSelf:true` returned unplayable. |
| `EndTurn` | none | `expectedScreen`/`expectedCurrentSide`/`expectedRoundNumber` recommended |
| `SelectReward` | `rewardIndex` (uses `RewardsSetIndex`, not array position) | For `kind:"Card"` rewards this opens the `CardReward` card-selection overlay; you must then call `SelectCardOption {cardIndex}` to pick. Calling `SelectCardOption` directly on the Rewards list without first `SelectReward`ing the card entry fails with "no live NCardRewardSelectionScreen". |
| `SkipReward` | `rewardIndex` | Required even for single-reward screens; use `rewards.index` value. |
| `SelectCardOption` | `cardIndex` | NOT `optionIndex`. For `NCardRewardSelectionScreen` ONLY. |
| `SelectCardsInGrid` | `cardIndices` (array of int) | For `NDeckCardSelectScreen` / `NDeckUpgradeSelectScreen` / `NCardGridSelectionScreen`. |
| `SelectEventOption` | `optionIndex` | |
| `SelectRestOption` | `optionIndex` | 0=Heal, 1=Smith (for Ironclad restsite). |
| `SelectMapNode` | `col`, `row` | NOT `x`/`y`, NOT `ChooseMapNode`. |
| `OpenChest` + `SelectTreasureRelic` | `index` (on SelectTreasureRelic) | Two-step; OpenChest populates `treasure` payload. |
| `Purchase` | `category`, `index` | Categories: `character_card`, `colorless_card`, `potion`, `relic`. |
| `PurchaseCardRemoval` | none | Opens grid-select; confirm with `SelectCardsInGrid`. Gold deducts on confirm. |
| `UsePotion` | `slotIndex`, optional `targetIndex` | For self-use on `AnyPlayer` potions, OMIT `targetIndex` or it hits an enemy. |
| `Proceed` | none | Dismisses rewards / restsite / treasure / merchant / event. After skipping a reward from an event, TWO Proceeds are needed (one closes `Rewards`→`RewardsClosed`, one exits event room). |

## Still outstanding (from original 17-bug list)

Bugs 1 (Bottled Potential), 14, 15, 16, 17 — not yet reachable in this run. Continue normal play and verify as they come up.
Bug 8 (GameOver HP) — now triggered (see below), needs verification.

## Continuation: Floor 14–17, Kin boss death (cmds 1005–1092)

After the event-skip cascade at floor ~12, continued through:

- **Floor 14 (4,14) Monster** — 2× Nibbit fight. Won with ~7 HP loss. Rewards: 14 gold + **True Grit** picked (over Tremble, Body Slam). Verified SelectReward(Card) + SelectCardOption + Proceed flow.
- **Floor 15 (5,15) RestSite** — Heal: 45 → 72 HP. `SelectRestOption {optionIndex:0}`.
- **Floor 16 (3,16) Boss: The Kin** — 3 enemies:
  - `Kin Follower` at `slot1`, `combatId:1`, array index 0, 59 HP, Minion + STR 2 (grew each turn: 2→4→6).
  - `Kin Follower` at `slot2`, `combatId:2`, array index 1, 58 HP, Minion + STR 2 (grew 2→4).
  - `Kin Priest` (leader) at `leaderSlot`, `combatId:3`, array index 2, 190 HP, STR 2 (grew 2→4), inflicts Vulnerable/Frail/Weak debuffs.

Fight outcome: **Ironclad died on T9 turn-end** at HP 0/90. Priest was at 19 HP. Cause: consistently under-estimated incoming damage because state.json intent `damage` field excludes enemy STR bonus (known issue #4 above, now shown to be CRITICAL for AI play, not merely "informational"). Combined with Swift Potion bridge bug (see below) that removed emergency draw option.

### Death-snapshot behaviour (Bug 8 verification)

At the moment of death, snapshot showed:
- `screen.name: "MainMenu"` (combat screen torn down immediately — no GameOver screen captured).
- `run.currentHp: 0`, `combat.roundNumber: null`, `combat.currentSide: null`.

So the game skipped any GameOver intermediate state in our snapshot triggers and went straight to MainMenu. **Bug 8 fix status**: unclear — we never observed a `GameOver` screen payload. If Hermes intended to extract run stats on death, that trigger may still be missing. Low priority; a consumer can infer death from `currentHp:0 + screen:MainMenu`.

### Mechanics verified during Kin fight

| Card / Relic / Power | Observed | Notes |
|---|---|---|
| **Twin Strike** base | 6 dmg × 2 hits | Wiki had 5; StS2 base is 6. Sharp enchant adds +2 **per hit**, so Sharp Twin Strike = 2×8 = 16 base. |
| **Twin Strike Sharp** vs Vuln 3 | 24 dmg | 2 × (6+2) × 1.5 = 24, confirmed multiple times. |
| **Pillage** | 6 dmg + draw until non-attack | NOT 9 dmg (wiki). StS2 base 6. Draw stops on first non-attack drawn. |
| **Bash+** | 10 dmg + Vuln 3 | STS2 confirmed. Costs 2 energy. |
| **True Grit** | 7 block + exhaust 1 random card | Exhausts random card from hand. FNP fires on the exhaust (+3 block). Net 10 block + lose one card. |
| **Taunt** (StS2) | 7 block + 1 Vuln to target | Defensive + debuff hybrid. Vulnerable stacks additively (observed Vuln 2 → 3 → 4). |
| **Defend** | 5 base + Dex | Frail reduces by 25% (floored). |
| **Feel No Pain** | 3 block **per card exhausted** | Correct mechanic: triggers on exhaust, not on play. Most visible pattern: at each turn start Drum of Battle exhausts top of deck → +3 block immediately. Also fires when True Grit exhausts its random target. |
| **Drum of Battle** | 0-cost Power; draws 2 on play; exhausts top of deck at start of each subsequent turn | 0 energy cost is the hook — essentially "free power". |
| **Dexterity 1** (from Oddly Smooth Stone) | +1 block on any block-generating card | Stacks with Defend, Taunt, True Grit. |
| **Letter Opener** | +5 dmg to ALL enemies when N skills played in a turn | Did NOT fire at 2 skills/turn; fired once earlier (T5) when 3 skills played. **Threshold appears to be 3 skills, not 2** — needs more observation. |
| **Kin Follower STR growth** | +2 STR per turn (per-turn buff) | Observed F1 STR 2 → 4 → 6 across 3 turns. Unclear if F2 buffs F1 or each follower self-buffs. |
| **Kin Priest STR growth** | +2 STR at some turn | Priest STR 2 → 4 observed between T7 and T9. |
| **Vulnerable decay** | −1 per turn on debuffed target | Standard. |
| **MultiAttackIntent damage math** | `(damage + enemy STR) × repeats` | Example: F2 intent `damage:2, repeats:2` with STR 4 = (2+4)×2 = 12 dmg. State field is base-only. |

## New bridge bugs discovered (Kin fight)

### Bug A: `UsePotion` silently fails for `AnyPlayer` potions used on self

**Repro:** Swift Potion in slot 0. Command `{"type":"UsePotion","slotIndex":0}` (no targetIndex, correct per note #6 above).

**Expected:** Potion consumed, effect applied (Swift Potion draws 3 cards).

**Observed:**
```json
{"id":1083,"status":"ok","message":"enqueued use of SWIFT_POTION (slot 0)","revision":1026}
```
Then over multiple snapshot triggers (2+ seconds, subsequent card plays, etc.):
- `run.potions[0]` still shows the Swift Potion object (not consumed).
- Hand size unchanged (no draw fired).
- No error surfaced.

Also tried `{"type":"UsePotion","slotIndex":0,"targetIndex":-1}` — got clean error `"targetIndex -1 out of range (enemy count 3)"` which confirms the dispatcher does validate targetIndex when present.

**Hypothesis:** `DispatchUsePotion` uses an enqueue path for `AnyPlayer` potions (probably awaiting a target pick) and the queued action is never flushed when no target is provided. The "ok" status is misleading — it reports enqueue success, not execution success.

**Impact:** HIGH — potions are a core combat resource; silent failure during a life-or-death turn directly caused player death.

**Suggested fix:** For potions where `targetType == AnyPlayer` and no `targetIndex` is provided, dispatch immediately against the player (self) instead of enqueuing. Alternatively, after enqueue, force the queue to resolve and return an accurate status (and perhaps a `resolved:true/false` flag).

### Bug B: `UsePotion` parameter name clarity

Not a bug per se, but `UsePotion` requires `slotIndex`, not `potionIndex`. The dispatcher returns a clear error (`"UsePotion requires numeric 'slotIndex'"`) which helped rapid recovery. The runbook + command-shape table already lists `slotIndex`; leaving this in place as the canonical name.

### Bug C (re-classification of #4): Intent damage STR exclusion is CRITICAL, not informational

Originally flagged at priority "Low priority / informational field". The Kin boss fight demonstrates this is a correctness-blocking issue for any automated/AI player:
- T5 I computed "~17 incoming"; actual was ~31 due to STR on all 3 enemies.
- T8/T9 repeatedly mis-sized block because state showed `damage:5` for a hit that dealt 11.
- Without STR folded in, `intents[]` is actively misleading; a consumer would need to read enemy `powers` and re-do the math.

**Suggested fix:** In `BridgeStateExtractor` when populating intents, compute display damage as `base + stackedStr` (and multiply by Vulnerable if target is self? — actually Vuln is on the target of the hit, which is the player for attack intents; multiply by 1.5 if player has Vulnerable). At minimum apply STR. Leave a separate `baseDamage` field if raw is also needed.

## Action items for next session

1. **Patch Bug A (potions silent-fail).** Highest priority — blocker for autonomous play.
2. **Patch Bug C (intent STR).** Second priority — also blocks autonomous play.
3. Start a fresh Ironclad run to verify both patches + re-attempt Kin boss.
4. Continue hunt for remaining bugs 1, 14, 15, 16, 17.
5. Collect more Letter Opener data to confirm the "3 skills" threshold hypothesis.

---

## Post-patch live verification (2026-04-19 evening, second run)

### Setup
- Fresh Ironclad run, seed auto-synthesized = `6397495365065998678` (empty-seed StartRun synthesis confirmed working end-to-end).
- Neow: took Option 1 (+1 potion slot + 2 random potions → Flex Potion (AnyPlayer), Touch of Insanity (Self), 4 slots).
- Floor 1: Nibbit combat, single enemy, 46 HP.

### Bug C (intent STR folding) — ✅ VERIFIED FIXED

**Observation:** T3 Nibbit cast a Buff intent that granted itself **STRENGTH 2**. T4 `intents[0]` then showed `intentType:Attack, damage:14`. Base move damage = 12; with pre-patch code the state would have reported `damage:12`. The additional +2 exactly matches enemy STR, confirming `ExtractIntent` now folds STR in correctly. End-of-turn reconciliation: T4 Nibbit hit for 14, I had 0 block, took exactly 14 (HP 72→58) — matches `damage:14` precisely. Patch is correct.

### Empty-seed StartRun synthesis — ✅ VERIFIED

Sent `{"type":"StartRun","character":"IRONCLAD","ascensionLevel":0}` with no seed field. Result message: `"started run: IRONCLAD (mode=Standard, asc=0, seed='6397495365065998678')"`. Game entered Neow event cleanly on revision 15.

### Bug A (AnyPlayer potion self-use) — ⏳ PENDING LIVE TEST

Patch deployed, but we haven't actually been in a combat with an AnyPlayer potion and drunk it yet. Flex Potion is in slot 0 — will verify on the next real combat.

### NEW Bug D: event option descriptions contain unsubstituted placeholders

**Repro:** F2 "Jungle" unknown-room event (encountered via an Unknown node). State shows:

```
Options:
[0] "Gain [blue]{Gold}[/blue] [gold]Gold[/gold]. Lose [red]{HpLoss}[/red] HP."
[1] "Heal [green]{Heal}[/green] HP. Fight some [red]enemies[/red]."
```

The actual on-screen values (per user) are `Gold=96`, `HpLoss=8`, `Heal=24`. The bridge is emitting the raw localization string with `{Gold}/{HpLoss}/{Heal}` tokens unsubstituted.

**Impact:** HIGH for an AI consumer — can't make informed event choices without knowing the magnitudes. Would force the consumer to ask the human or guess.

**Hypothesis:** The game likely substitutes these at UI render time (label node draws them from an event-parameter map), not at the localization layer. `BridgeStateExtractor`'s event-option emission is probably grabbing `Option.DescriptionKey` or the raw Tr()'d template without performing the parameter substitution that the UI does. Need to inspect:
- `NEventRoom` / `NEventOption` types for a `GetFormattedDescription()` or `Parameters` dictionary.
- Whether BBCode formatter takes a params bag (e.g. `Tr(key, args)`).

**Suggested fix:** Find how the event UI panel formats the option text and mirror that. Likely `string.Format` with a dict lookup, or a Godot `Tr(key, placeholders)` overload. Until the game's format method is identified, a fallback could be: scan the description for `{Name}` tokens and expose the current option's `Params` dict alongside as a structured field so the consumer can substitute.

### Mechanics data collected this run

- **Strike (basic, unupgraded):** Confirms **6 damage base** (Nibbit 46→40 on Vuln-free hit; 40→34 same). Previous speculation of "5 dmg" ruled out.
- **Bash (basic, unupgraded):** 8 damage + Vulnerable 2 (matches StS1).
- **Defend (basic, unupgraded):** 5 Block (matches StS1).
- **Feel No Pain (Uncommon Power):** 1 energy cost in StS2 (same as StS1).
- **Bloodletting (Common Skill):** 0 energy cost in StS2.
- **Havoc (Common Skill):** 1 energy cost in StS2.
- **Ironclad end-of-combat heal:** HP went 58→64 between Nibbit death and Rewards screen. +6 HP suggests Ironclad has a starter relic granting post-combat heal (similar to StS1 Burning Blood). Value appears to be 6 at this stage (not yet confirmed relic name).

### NEW Bug E: event continuation page (e.g. "Fight!" button) not surfaced in state.json; `Proceed` skips it

**Repro (Dense Vegetation event, F2 Unknown node):**
1. Sent `SelectEventOption optionIndex:1` (user-described label "Heal X HP. Fight some enemies."). Trace shows this resolves to `option=Rest` internally, and state transitions through `PostDispatch:SelectEventOption` → `EventRefreshState` → `EventRefreshStatePayload` at revisions 127–129. **Heal was applied correctly** (HP 64→80 confirmed by screenshot).
2. The event UI on-screen then shows a continuation page titled "Dense Vegetation" with narrative text ("You're dead tired and take a nap... only to be awoken by something wriggling around on top of you!") and a single centered button **"Fight!"**.
3. But state.json at this point did **not** emit any event options for the continuation page — `state.event` was empty/absent.
4. Sent `Proceed`. Trace: `NEventRoom.Proceed postfix fired` → `EventProceed-ClearPayload` → `screen=Map`. The Proceed call cleared the event payload and switched the bridge's screen label to `Map`, but the on-screen UI was still showing the Fight! button waiting for input (the map overlay and the event overlay were both active, with the event taking visual priority).
5. After the user manually closed the map overlay with Esc, screen went to `MapClosed` and stayed there — state.json never surfaced the underlying event continuation.
6. Workaround: sent `SelectEventOption optionIndex:0` blindly. This correctly clicked "Fight!" and combat began (4× Wriggler encounter).

**So the bug is two-headed:**
- **E1 (extractor):** `BridgeStateExtractor` is not re-populating `state.event.options` on the event-continuation page (the second "page" of an event that opens after an option is chosen). The trace hook `EventRefreshState` fires, but whatever options-list the extractor reads from `NEventRoom` is empty while the UI's "Fight!" button is live. Likely the continuation button is stored in a different field (e.g. `NEventRoom.CurrentPage.Buttons` or similar) than the first-page options, and the extractor only knows the first location.
- **E2 (dispatcher):** `DispatchProceed` on an NEventRoom mid-event-continuation does NOT click the continuation button; instead it calls `NEventRoom.Proceed()` which prematurely resolves the event (clear payload + return to map) while the actual on-screen button is still unclicked. This mismatches consumer expectations — from outside, after a `SelectEventOption` you'd expect `Proceed` to advance the event to its natural conclusion, not abandon it.

**Impact:** HIGH — any multi-page event (accept → narrative → fight/reward) will be silently botched if the consumer tries to `Proceed` after the first option. Worked around here only because `SelectEventOption 0` happens to match the single continuation button by position.

**Suggested fixes:**
- (E1) Inspect `NEventRoom` (or whatever controls the event UI panel after an option is chosen) for the continuation-button list. Mirror that list into `state.event.options` when the extractor sees the continuation page.
- (E2) In `DispatchProceed`, when the active room is an `NEventRoom` AND the event is not yet in a terminal state (there is still a visible continuation button), EITHER error out with "event has pending continuation; use SelectEventOption" OR route the Proceed to the continuation button. Erroring is probably safer so consumers don't accidentally auto-click a random option.

**Additional observation:** The first-page options' `title` field is the internal dev title (e.g. `"Rest"`, `"Phial Holster"`) not the localized player-facing label. The DEV titles are actually MORE useful for an AI consumer than the {placeholder}-riddled descriptions, because they're unambiguous. But ideally both should be exposed: `title` (dev), `displayTitle` (localized label), `description` (fully-substituted flavor text).

### Ceremonial Beast boss note (user-provided, for planning)

Act 1 boss this run is Ceremonial Beast. Its debuff restricts the player to **playing only 1 card per turn**. Implications:
- Powers become weak (one-turn tempo sink).
- 0-cost cards lose most of their value (can't chain).
- Potions are unaffected — they don't count as card plays. Extra potion slots (Neow option 1 this run) become very strong here.
- High-base-damage single cards (Bash, Twin Strike, Iron Wave+) and single-card block generators (Shrug It Off, Impervious) matter most.




---

## Run outcome: DIED F14 Monster (Nibbits x2)

**Final state:** Floor 14, HP 0/85, Gold 277, 17-card deck. Died to Crimson Mantle self-damage at start of turn 4 (HP 1  0), not to enemy damage.

### Fight sequence
- **F10 Treasure (1,9):** Took Kusarigama relic.
- **F11 RestSite (0,10):** Rested for +39 HP (base 24 + Regal Pillow bonus +15). HP 10  49.
- **F12 Elite Bygone Effigy:** Won after 8 turns. Drank Fruit Juice turn 1 (+5 MaxHP, applied at start of next combat).
  Used Armaments to upgrade Pillage+, Shrug It Off+, Iron Wave+. Fairy-in-a-Bottle triggered at fatal on turn 7 (healed to 25).
  **Rewards LOST: 43 gold, Duplicator potion, Venerable Tea Set relic** (Bug H: Proceed from CardReward skips uncollected rewards).
  Only kept **Crimson Mantle** (Rare Power) card.
- **F13 Monster Vine Shambler:** Won turn 4 with lethal Hemokinesis (~22 dmg Vuln-boosted on 19 HP). HP 12 at end.
  Reward: **Flame Barrier** (Uncommon Skill).
- **F14 Monster Nibbits x2:** DIED turn 4 start to Crimson Mantle self-damage at 1 HP.

### Nibbits encounter (new monster data)
- **Front Nibbit:** 44 HP. Moves: Aggressive (SingleAttack 6 dmg), Defensive (DefendIntent, no visible damage).
- **Back Nibbit:** 42 HP. Moves: Empower (BuffIntent, grants STR to self and/or ally), Defensive (Defend + 8 dmg attack combo).
- **Behavior:** Back Nibbit buffs both to STR+2 on turn 2 (observed). Subsequent attacks scale with STR (front 6+2=8? back 8+2=10 plus possibly more).
- **Strategy:** Kill back Nibbit first to shut off buffs, OR race with Bash+Vuln. Low HP runs should carry block.

### Crimson Mantle in-combat behavior (CONFIRMED from death)
- Playing the card: costs 1 E, adds `POWER:CRIMSON_MANTLE_POWER` stack=8 to player.
- **At start of each of player's turns (AFTER the first play):** -1 HP to player + gain 8 block.
- **The HP loss is unblockable** and applies BEFORE card draw - this means playing Mantle at 1 HP is an immediate loss-of-game next turn regardless of enemy damage.
- **Rule for HermesBridge strategy:** DO NOT play Crimson Mantle when currentHp <= 1. Prefer playing it when at high HP and combat is expected to be multi-turn.

### Flame Barrier in-combat behavior (CONFIRMED)
- 2E Skill. Gives 12 block immediately + adds `POWER:FLAME_BARRIER_POWER` stack=4 (thorns for remainder of turn).
- Thorns proc on every enemy attack this turn (observed 4 dmg on both Nibbits from their attacks).
- **Does NOT persist past end of turn** (the power decrements to 0 at turn end in StS1; assumed same here, unverified).

### Bug H (NEW, HIGH UX IMPACT)
`Proceed` from a CardReward screen silently skips all other uncollected rewards (gold, potion, relic). This session cost 43 gold + 1 potion + 1 relic after the Bygone Effigy fight.
**Workaround:** Always `SelectReward` every non-card reward individually BEFORE opening the CardReward. Only Proceed after all rewards are empty or after CardReward's SelectCardOption/skip has been resolved.
**Suggested fix:** In `DispatchProceed` on a Rewards screen, if any non-card rewards are still present, either auto-collect them, or return an error "rewards pending; collect or skip first".

### Bug I (NEW, potential) - Intent damage display vs applied damage
State shows intents with `damage` field and separately enemy `powers[STR]`. After Bug C patch, damage SHOULD be STR-folded. In this fight, front Nibbit showed `SingleAttackIntent:14x1` + STR:2, so apparent displayed damage 14 but real hit likely was 16 (14+STR? or 14 pre-fold?).
**Unresolved:** need a clean test - observe intent damage on an enemy that is about to attack, note the value, let it hit us with 0 block, compare HP delta. Do this on the very next run to confirm whether Bug C fold is correctly applied in this scenario or whether STR is being double- or non-counted.

### Bug G (confirmed low-prio)
`UsePotion` on Map screen returns `"no current player (not in run?)"`. Potions are only usable in combat.

### Confirmed mechanics to add to reference-*.md

- **Bash (Ironclad Basic Attack):** 2E in StS2 (NOT 3E as originally noted - re-verify). Deals 8 dmg + Vuln 2. With Sharp +2 enchantment: 10 dmg + Vuln 2. (Note: prior notes said Bash=3E; revisit - state.json consistently shows `energyCost: 2` for Bash+Sharp.)
- **Vulnerable:** 1.5x dmg multiplier, does NOT decrement per hit, decrements by 1 at enemy turn end.
- **Hemokinesis:** 1E Uncommon Attack. 15 base dmg, -2 HP, Exhausts. ~22-24 dmg under Vuln.
- **Pillage:** 1E Uncommon Attack. Observed 9 dmg sometimes, 6 other times (likely 6 base +3 if target has block, StS1 behavior).
- **Iron Wave:** 1E Common Attack. 5 dmg + 5 block. Iron Wave+: 7 dmg + 7 block.
- **Flame Barrier:** 2E Uncommon Skill. 12 block + 4 thorns next turn.
- **Crimson Mantle:** 1E Rare Power. Start of each turn (after first play): lose 1 HP + gain 8 block.
- **Fruit Juice:** Potion. +5 MaxHP (applied at start of next combat entered, delayed).
- **Regal Pillow:** Common relic. Rest action heals +15 HP extra (24 + 15 = 39 at RestSite for fully-rested).
- **Burning Blood:** Starter relic. +6 HP at end of each combat.
- **Kusarigama:** Uncommon relic. Attacks may draw extra cards (observed hand growth mid-turn).
- **Phial Holster:** Ancient relic (Neow gift this run). +2 potion slots.

---

## Diagnostic probes deployed (post-mortem, before next run)

Three read-only reflection dumps added to help triage Bugs D/E2/F during the next Ironclad run. All fire **once per distinct event** to keep trace.log readable.

1. **Bug D probe** — `BridgeStateExtractor.ExtractEventOption`: first time an event option's description contains a `{Name}` token, emits `BugD[diag]: FIELD/PROP ...` lines enumerating every member of the `EventOption` instance and its base-type chain. Purpose: locate the params dict used by the UI to substitute `{Gold}`, `{HpLoss}`, `{Heal}`, `{CardChoiceCount}`, etc.
2. **Bug E2 probe** — `BridgeCommandDispatcher.DispatchProceed` event branch: first time `Proceed` is dispatched on an event, dumps both the `EventRoom` logical-model object and the `NEventRoom.Instance` Godot-node object (fields + props). Purpose: identify the continuation-button list so we can surface it and either click it from Proceed or refuse with "continuation pending".
3. **Bug F probe** — `BridgeCommandDispatcher.DispatchUsePotion`: per distinct potion id (first use only), dumps the `PotionModel`'s declared methods, properties, and base-type chain. Purpose: understand why `Touch of Insanity` silently resolves nothing after `EnqueueManualUse(player.Creature)` — looking for a `ChooseACard`/`PickFromDeck` hook or a different Self-target entry point.

Build succeeded, DLL deployed to `E:/SteamLibrary/steamapps/common/Slay the Spire 2/mods/HermesBridge/HermesBridge.dll`. All probes are safe (caught + logged), don't alter gameplay, and self-rate-limit via `bool`/`HashSet` gates.

**Next action:** Start a new Ironclad run. Trigger at least one event (Unknown node in Act 1), dispatch `Proceed` from that event, and drink any Self-target potion available (especially `Touch of Insanity` if it shows up again). Then read `C:\Users\hi\AppData\Roaming\SlayTheSpire2\hermesbridge\trace.log` for `BugD[diag]:` / `BugE2[diag]:` / `BugF[diag]:` lines and use them to write the real fixes.

---

## Session 2026-04-19 continuation — new Ironclad run F1–F10

### New confirmed mechanics
- **Shrinker Beetle (F5/F6 monster):** 39 HP. Turn 1 intent "Strategic" = applies `Shrink -1` (Strength-down, ~30% outgoing damage penalty per point, not flat -1). Confirmed by Strike 6 base → 4 damage observed, 4*1.5 (Vuln) = 6 matches in-game hit.
- **Fuzzy Wurm Crawler (F7 monster):** 57 HP. Turn 1 intent "Buff" = applies `Strength +7` to self. Attack goes 4 base → 11 after Str. Confirmed by damage delta.
- **Mawler (F8 monster):** 72 HP. Alternates 4x2 (8 total) and 14x1 incoming. No powers visible; may be fixed alternating attack pattern.
- **Vuln formula confirmed:** `floor(dmg * 1.5)`. Twin Strike (5x2) under Vuln = 7x2 = 14 observed.
- **Sword Boomerang:** 3 hits of 3 dmg, random-target (single-enemy deterministic). Takes no `targetIndex`.
- **Room Full of Cheese → Gorge:** pick 2 of 8 Commons, added to deck.
- **Byrdonis Nest → Eat the Egg:** +7 MaxHP instant.
- **Aroma of Chaos event:** "Let Go" = transform a deck card (random). "Maintain Control" = upgrade a deck card (permanent). Uses `CardGridSelection` screen with mode `NDeckUpgradeSelectScreen` internally.
- **Permafrost (Uncommon relic):** effect TBD (picked at F9 Treasure, not yet tested in combat).
- **Molten Fist / Cinder (Common Ironclad cards):** skipped as card rewards — unknowns, flagged for future investigation.
- **Armaments (1E Common Skill):** Block 5 + Upgrade 1 card in hand for combat. Uses `handSelect` sub-screen with mode `UpgradeSelect`.

### New bugs / issues captured

**Bug J — SelectCardsInGrid task-completion race (previous session)**
Already documented. Still unfixed. Fix needed: `DispatchSelectCardsInGrid` must always `ScheduleDeferredStateRefresh` even on error path.

**Bug J-related — deck stale after upgrade via event (NEW, high severity)**
After Aroma of Chaos → Maintain Control → upgrade Bash, and even after Proceed to Map, `state.json.run.deck` still shows Bash as `isUpgraded=false`, `currentUpgradeLevel=0`. Upgrade persisted in game (confirmed when Bash drew in next turn as Bash+). Same root cause as Bug J: deck snapshot not refreshed after `NDeckUpgradeSelectScreen` confirms.
**Repro:** F9 Aroma of Chaos, Maintain Control, pick Bash (grid idx 9). Check `state.json.run.deck` — Bash entry shows no upgrade flag. Persists through Proceed + Map + Treasure + Proceed.
**Fix hook:** `DispatchSelectCardsInGrid` + all card-grid confirm paths should `ScheduleDeferredStateRefresh` post-confirm, including for event-triggered deck mutations.

**Bug K — Random-target cards reject `targetIndex` (NEW, low severity)**
`PlayCard` with `targetIndex: 0` for Sword Boomerang returns `TryManualPlay returned false`. Must omit `targetIndex` entirely for random-target AoE cards. Workaround known; should be documented in schema.

**Bug L — Armaments `handSelect` doesn't auto-confirm at maxSelect=1 (NEW, low severity)**
`handSelect` state showed `maxSelect=1, requireManualConfirmation=false, selectedCount=1` after a single `HandSelectCard` — but the selection did not auto-resolve. Required explicit `HandConfirmSelect` to complete. Either fix bridge to auto-confirm when conditions met, or document that `requireManualConfirmation=false` currently does not imply auto-confirm.

**Bug M — Card reward / card choice entries lack descriptions (NEW, medium severity for distribution)**
`rewards[].cards[]`, `cardRewardOptions[].cards`, `chooseACardScreen.cards[]`, `handSelect.cards[]`, `combat.hand.cards[]` etc. all lack card `description` / `effectText`. Consumers (including Claude on Twitch) cannot reason about unfamiliar cards without an external reference. Relevant now because StS2 has many cards our knowledge base doesn't cover (Molten Fist, Cinder encountered in this run).
**Fix:** extend card extraction in `BridgeStateExtractor.ExtractCard` to include `description = SafeLocString(card.DescriptionLoc)` (or equivalent). Mirrors what's already done for events.

**Bug N — `combat.hand` stale during `handSelect` sub-screen (NEW, medium severity)**
When `handSelect.active=true`, `combat.hand.cards` still includes the card that triggered the sub-screen (e.g. Armaments shows in `combat.hand` even after being played and awaiting upgrade target). `handSelect.cards` has the correct current hand. Consumer must know to read from `handSelect.cards` during selection. Could be fixed either by documenting this invariant or by syncing `combat.hand` during `handSelect.active=true`.

**SelectTreasureRelic parameter name** — uses `index`, not `relicIndex`. Worth documenting in command schema.

### Release-readiness punchlist (distribution + Twitch/streaming)

Captured here while fresh — covers GitHub + NexusMods publishing + stability for a `ClaudePlaysTheSpire` downstream consumer.

**Stability (blocks streaming):**
1. Fix Bug J + Bug J-related stale deck bug (`DispatchSelectCardsInGrid` + all card-grid confirm paths must `ScheduleDeferredStateRefresh`). **Top priority.**
2. Bug N — sync `combat.hand` during `handSelect.active=true` (or document).
3. Audit all `Dispatch*` methods for error paths that skip state refresh.
4. Verify `state.json` writes are atomic (temp file + rename). If `result.json` uses `.tmp`, apply same pattern to `state.json` and `trace.log`.
5. Gate `BugDDiagnostic`, `BugE2Diagnostic`, `BugFDiagnostic` probes behind `HERMES_DEBUG=1` env var before release — currently always on.
6. Structured error codes in result.json (enum: `BadTarget`, `NoEnergy`, `NotPlayable`, `WrongScreen`, `Internal`) instead of free-text `TryManualPlay returned false (...)`.

**Schema stability (blocks consumer code):**
7. Add card descriptions (Bug M) — extend `ExtractCard` in `BridgeStateExtractor`.
8. Write `SCHEMA.md` documenting every top-level field + sub-screen invariants.
9. Bump `schemaVersion` discipline — any breaking shape change increments the integer. Consumer pins supported version range.
10. Document command-ID reset semantics (what happens on game restart / mod reload).

**Distribution (blocks GitHub/NexusMods):**
11. Add `LICENSE` (MIT or Apache-2.0 standard for StS mods).
12. Replace hardcoded `E:\SteamLibrary\...` in `HermesBridge.csproj` post-build step with `$(STS2_MOD_DIR)` env var + fallback.
13. `lib/` folder convention for game-extracted DLLs (MegaCrit.Sts2.Core etc. can't be redistributed). Add `lib/README.md` explaining how to populate + `lib/*.dll` to `.gitignore`.
14. Sweep `docs/` for absolute paths (`D:\hermes-addenda\play-sts2-run.mjs` reference, `E:\` paths) before first public commit.
15. README for end users covering install, uninstall, schema overview, example consumer snippet.

**Mechanics coverage (ongoing):**
16. New characters unverified: **Regent** (stars resource persists between turns — verify `baseStarCost`/`currentStarCost` extraction and star carryover logic), **Necrobinder** (pet damage-absorption — verify `combat.player` vs pet representation, intent forwarding), **Defect** (orbs — likely needs new schema fields for orb slots + focus power).
17. Unverified card types: powers, curses, statuses, enchantments (partially observed), afflictions (partially observed).
18. Unverified screens: Shop, Rest site Smith/Recall/Dig, Act boss reward, card removal service.

### Current run state at pause point (ID 2320 done, F10 pending)
- Floor 10, col 3 → (2,10) Monster available. Path continues toward Treasure-rich col 3/5.
- Player: Ironclad HP 62/87, 156 gold, 3/3 potions (Flex, Explosive Ampoule, Binding), relics: Burning Blood, Lost Coffer, Phial Holster (4 potion slots), Permafrost.
- Deck 17 cards: 5 Strike, 4 Defend, Bash (upgraded in-game, stale in state.json), Thunderclap, Sword Boomerang, Pommel x2, Twin Strike x2, Armaments.
- Last command ID: **2320**. Next: **2321+**.


