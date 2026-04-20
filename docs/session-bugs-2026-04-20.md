# Session bug log — 2026-04-20 (Ironclad run, Floor 1 → Floor 11)

Live-run findings accumulated during the current Ironclad session. Letters
reflect observation order; gaps (A–I, K, L, N, O, R, S, Z, AA, AC, AE) were
either immediately resolved, merged into other entries, or reclassified
before this log was written.

Bridge version at time of observation: pre-tooltip-extraction build.
Tooltip-extraction build (this session) resolved **M** and **Y**.

## Status legend

- **Open** — reproducible, needs fix or investigation
- **Resolved** — fixed this session (verify after rebuild)
- **Retracted** — turned out not to be a bridge bug
- **Mechanics** — real game behavior, not a defect; documenting only

## Summary

| Tag | Severity | Status | One-liner |
|---|---|---|---|
| T | medium | fix-applied | `card.energyCost` is base cost; now also surfacing `effectiveEnergyCost` via `CardEnergyCost.GetAmountToSpend()` which accounts for discounts (Stomp, per-turn modifiers). Energy cost is CardEnergyCost._base; effective is the method engine itself uses when spending. Needs verification on a card with an active discount (Stomp after Attacks played, or a relic-sourced discount). |
| U | high | open | `SpecialCardReward` command returns ok but applies no effect |
| V | medium | open | Selecting a potion reward when potion inventory is full silently no-ops (no toast, no state change) |
| W | high | fix-pending-verify | State stale after `UsePotion`: `run.potions[]` slot still shows consumed potion until next PlayCard. `ScheduleDeferredStateRefresh` is already wired into UsePotion/DiscardPotion — reproduce in next potion use to confirm resolved. |
| X | high | fix-pending-verify | State stale after `UsePotion`: resulting power (e.g. Strength from Flex) not visible in `combat.player.powers[]` until next PlayCard. Same refresh path as W — verify on next Flex-like potion. |
| Y | medium | resolved | Event-granted card enchantment not reflected in deck JSON — fixed by tooltip extraction (enchant now baked into `description`) |
| Q | medium | fix-applied | Duplicator event option returns ok but card not actually duplicated in deck. Added `ScheduleDeferredStateRefresh` to `DispatchSelectEventOption` (BridgeCommandDispatcher.cs). Deck/relics/potions now re-snapshot 2 pump ticks after any event option. Verify at next Duplicator/Nloth-style event. |
| J | medium | fix-applied | Deck entries show pre-upgrade stats after event-driven upgrade. Same fix as Q — event-option deferred refresh now covers all deck-mutating events (upgrade, remove, duplicate, add). Verify at next upgrade event. |
| P | low | open | `state.run.block` can be stale; field reads `player.Creature.Block` which should be authoritative. May resolve automatically when other refresh paths fire. Low priority — controllers can read `combat.player.block` directly. |
| M | — | resolved | Card descriptions missing from state.json — fixed this session via `GetDescriptionForPile` |
| AD | — | retracted | Myte "Strategic" intent misread — was actually unblocked Toxic self-damage at end of turn |
| AF | — | retracted | Treasure-room gold "anomaly" — just normal async state update |
| AB | — | mechanics | Mysterious mid-combat block gain — likely an unnoticed relic/power proc (Bronze Scales thorns counter-block? Permafrost? needs logging to confirm) |
| — (extra) | — | retracted | Enemy `powers[].name` — field is `title`, not `name`; my pwsh column select was wrong |
| EVOPTVAR | medium | resolved | Event option descriptions had unresolved template vars; fix: reflect `EventModel.DynamicVars`, merge into option description via manual `{Token}` regex substitution (Wellspring `{BatheCurses}` now resolves to `1`) |
| COMBATBLIND | HIGH | resolved | Closing the map during active combat clobbered combat state to null; MapScreenClosePatch now preserves `screen=Combat` and re-pushes live combat payload when `CurrentCombatRoom` is non-null |
| NEOWDESC | low | resolved | Neow description was a raw loc key; fix: `CleanEventDescription` strips dotted loc-key patterns matching `^[A-Za-z]\w*(\.\w+){2,}$` when returning `description`/`initialDescription` from `ExtractEvent`. Verified: `event.description` on Defect Neow now empty string instead of `ancients.NEOW.pages.INITIAL.description`. |
| NOORBS | medium | resolved | Defect orb queue was not serialized; added `ExtractOrb` + `combat.orbs[]` + `combat.orbCapacity`. Each orb exposes `id`, `title`, `description`, `passiveVal`, `evokeVal`. Verified on Defect Floor 1 combat: Lightning orb (passive 3, evoke 8, capacity 3). |
| OSTY | HIGH | resolved | Necrobinder's Osty minion now surfaces in `combat.allies[]` (CombatState.Allies filtered to exclude player self) |
| NEOWRELIC | medium | retracted | Neow-granted relic appears correctly in `run.relics` from `Player.Relics`; earlier Map-screen miss was likely a state-capture timing artifact, not reproducible |
| POTEMPTY | low | retracted | Empty potion slots surface as `null` entries in `run.potions` — this is **intentional**, positional index must match `player.PotionSlots` so `UsePotion slotIndex=N` / `DiscardPotion slotIndex=N` work correctly. Documented at `BridgeCommandDispatcher.cs:1705-1707`. Not a bug. |
| CARDFLAGS | low | resolved | `ExtractCard` originally reflected `IsEthereal/Exhausts/Retains/IsInnate/IsCurse/IsStatus` on CardModel — none exist in StS2. Diagnostic dump revealed flags live in `Keywords` (IReadOnlySet) / `CanonicalKeywords`. Replaced static-bool reflection with `CardHasKeyword(card, "Ethereal"/"Innate"/"Retain"/"Exhaust")`. Also surfaced runtime flags `willExhaust`/`willRetain`, `tags[]`, `keywords[]`. **Verified 2026-04-20T17:16Z** on Arcane-Scroll-granted Eradicate (Rare Necrobinder, X-cost Attack): `keywords=["Retain"]`, `retain=true`, `willRetain=true`, `costsX=true`, `targetType=AnyEnemy`, `effectiveEnergyCost=0`. All fields populate correctly. |
| CARDGRIDAPPLY | MEDIUM | resolved-pending-neow | **Shop card removal WORKS** (verified 2026-04-20T17:41Z, cmd 2775 Necrobinder Floor 5 shop): removed 1 Strike, deck 13→12, gold 167→92. **Rest-site Smith upgrade WORKS** (verified 2026-04-20T17:56Z, cmd 2801 Floor 6 rest): upgraded Eradicate, `isUpgraded=true currentUpgradeLevel=1`, dispatcher reports `"selected 1 card(s) on NDeckUpgradeSelectScreen"`. Both NDeckCardSelectScreen variants (removal + upgrade) commit correctly via OnCardClicked+CheckIfSelectionComplete. **Q/J deferred-refresh verified** on upgrade path (deck reflects upgrade in same post-command read). **Neow Precise Scissors failure** remains unverified (needs another Precise Scissors offer); likely Neow-specific callback wiring, unrelated to general grid-select path. |
| WPOTION | HIGH | resolved | **W bug confirmed resolved** (verified 2026-04-20T17:50Z, cmd 2780 Weak Potion on Nibbit floor 6). Potion slot cleared immediately after dispatch. Weak power surfaces on enemy as `powers=[{title:"Weak",amount:1}]` (read slightly delayed — full resolution visible at next tick after UsePotionResolve triggers fire, revs 244→245). `ScheduleDeferredStateRefresh` via UsePotionResolve hook works correctly. |
| SNAP-HANDSELECT | — | coverage-gain | **Snap OstyAttack flow verified** (cmd 2783-2784 floor 6). Playing Snap triggers hand-select secondary screen (`handSelect.active=true, mode=SimpleSelect, prompt="Select a card to add Retain to"`). `HandSelectCard` dispatcher command with `handIndex` commits the sub-selection. Post-select state shows target card's `retain=true willRetain=true` — dynamic card-state mutations track correctly through sub-screen flow. |
| INTENTBLOCK | MEDIUM | resolved | Enemy intents exposed `kind`/`intentType`/`title`/`prefix`/`damage`/`repeats` only. `DefendIntent`/`BuffIntent`/`DebuffIntent`/`StunIntent`/`SleepIntent`/`UnknownIntent` have NO numeric amount property in the Core asm — the block/buff preview is baked into the LocString returned by `AbstractIntent.GetIntentLabel(opponents, owner)` (short form: `"8"` or `"Gain 8 Block"`) and `GetIntentDescription(opponents, owner)` (full sentence: `"This enemy intends to Defend for 8 Block."`). Added both as `label` and `description` on every intent payload. **Verified 2026-04-20T18:22Z** on Shrinker Beetle Floor 4: T1 DebuffIntent Strategic → `label="", description="This enemy intends to apply a Debuff to you."`; T2 SingleAttackIntent → `label="7", description="This enemy intends to Attack for 7 damage."`; T3 same intent post-buff → `label="13", description="... for 13 damage."`. Implemented via `CombatState.GetOpponentsOf(owner)` reflection, falling back to `[owner]` if CombatState unavailable. BridgeStateExtractor.cs:681-725. |

## Details

### T — energyCost is base cost, not effective cost
- **Observed**: Stomp (base cost 2) discounts by 1 per Attack played this turn.
  After playing 1 Attack, state.json still showed `energyCost: 2`.
- **Impact**: Controller cannot tell when Stomp becomes free / 1-cost without
  replicating the discount formula. Same class of issue would hit any
  cost-modifying card.
- **Likely location**: `BridgeStateExtractor.ExtractCard` reads `card.EnergyCost`
  (the model field) rather than the resolved effective cost. CardModel should
  expose a method like `GetEffectiveEnergyCost(PileType)` or similar.
- **Reflection target**: grep `_tmp_CardModel.cs` for `EnergyCost`, `Cost`,
  `GetCost`, `EffectiveCost`, `CurrentCost`.
- **Fix approach**: call the dynamic cost resolver if it exists; otherwise
  add a parallel field `effectiveEnergyCost` (keep `energyCost` as base for
  back-compat).

### U — SpecialCardReward no-op
- **Observed**: On a card reward screen that included a "Special" rarity
  choice (Colorless pool card), command `SelectReward` with the right index
  returned `ok=true` but deck didn't gain the card.
- **Likely location**: `DispatchSelectReward` in BridgeCommandDispatcher,
  branch that handles Special rewards. May be wired to the wrong
  selection method.
- **Verify**: grep dispatcher for `Special`, check whether it routes through
  `NCardRewardSelectionScreen.SelectCard` vs a separate special-card handler.

### V — Full-inventory potion reward no-op
- **Observed**: Combat reward had a potion. Inventory was full (3/3 slots).
  Selecting the potion reward returned `ok=true`, no state change, no
  in-game "inventory full" prompt surfaced via bridge.
- **Expected**: Either the game's "discard existing potion" dialog should
  surface in `handSelect`/similar, or the bridge should return
  `ok=false error="inventory full"` so the controller can choose.
- **Likely location**: reward dispatcher should check inventory capacity
  pre-dispatch OR wire in the discard-dialog screen.

### W — run.potions[] stale after UsePotion
- **Observed**: After UsePotion slot=1, `state.run.potions[1]` still named
  the consumed potion for several seconds. Re-calling UsePotion on slot 1
  returned `potion slot 1 is empty` — so game-side state had updated, but
  bridge state hadn't.
- **Cross-ref**: This may already be addressed by the 2026-04-19 handoff's
  `ScheduleDeferredStateRefresh` (BridgeCommandDispatcher.cs:1704-1725).
  **Verify**: that fix landed in the current build; reproduce in next run.

### X — Post-potion power not reflected
- **Observed**: UsePotion FLEX_POTION (grants Strength for the turn).
  `combat.player.powers[]` did not contain Strength until the next PlayCard.
- **Cross-ref**: Same root cause family as W — post-potion state refresh
  timing. Same verification path.

### Q — Duplicator no-op
- **Observed**: At a Duplicator-style event (or Nloth/similar that copies
  a card), selected a card, got ok=true, deck count unchanged.
- **Need**: Confirm which event exactly. Re-reproduce with trace.log
  capturing the event type and option key before/after deck snapshot.
- **Likely**: event option dispatcher doesn't refresh deck, OR the event
  effect uses a game path the bridge isn't hooked into.

### J — Stale deck after event upgrade
- **Observed**: Event that upgrades a chosen card (e.g. Bonfire Spirits
  variant, or an upgrade-all event). After selection, deck entry for the
  upgraded card still showed base stats.
- **Fix approach**: Event option dispatcher should `RequestWrite` with a
  small deferred tick (like ScheduleDeferredStateRefresh) so the game has
  time to apply the upgrade before snapshot.

### P — run.block stale
- **Observed**: `state.run.block` occasionally non-zero outside of combat,
  or didn't match `state.combat.player.block` mid-combat.
- **Likely**: `run.block` is a vestigial field; it should either mirror
  `combat.player.block` during combat or be omitted outside combat.
- **Low priority** since controllers can read `combat.player.block`
  directly.

### AB — Mysterious block gain (MECHANICS, not a bug)
- **Observed**: Combat mid-turn, player block went up without an obvious
  card play.
- **Likely cause**: Bronze Scales doesn't grant block (grants Thorns), but
  Permafrost's "first turn: 7 block" could land late in some turn timing,
  or a relic proc we weren't watching. Not a bridge bug.
- **Action**: log all power/relic triggers during next run to attribute
  block sources cleanly. No code change needed.

### ~~Enemy powers[].name empty string~~ — RETRACTED
- **Actual schema**: `powers[]` entries have fields `id`, `title`,
  `description`, `amount`, `type`, `isVisible`. There is no `name`
  field. Earlier "empty name" reports were my pwsh format-table
  using `.name` which silently returns empty for objects lacking it.
- Player-side verified on Regent Floor 4 (Shrink power from Shrinker
  Beetle): `title="Shrink"`, full description resolved.

### EVOPTVAR — Event option descriptions have unresolved template vars — **RESOLVED**
- **Observed** on "This or That?" (Floor 3, Regent run) and Wellspring
  (Floor 3, Necrobinder run): option descriptions carried literal
  `{HpLoss}`, `{Gold}`, `{Curse}`, `{BatheCurses}` placeholders.
- **Root cause (confirmed via `BugEv` reflection diagnostic):**
  event-level vars live on `EventModel.DynamicVars` (type
  `DynamicVarSet`), NOT on the `EventOption.Description.Variables`
  dict. The option-level LocString bag only carries character gender/
  pronoun vars. `GetFormattedText()` on the option therefore leaves
  `{BatheCurses}` unresolved.
- **Fix** (`BridgeStateExtractor.cs`): added `CollectEventDynamicVars`
  helper that reflects the `DynamicVars` property/field off EventModel
  and iterates it as `IDictionary` or `IEnumerable` (extracting
  `Key`/`Value` or `Name`/`Value` per item into a
  `Dictionary<string,string>`). Added `SubstituteTokens` regex helper
  (`\{([A-Za-z][A-Za-z0-9_]*)\}`). `ExtractEvent` collects once and
  passes the dict to each `ExtractEventOption` call; substitution also
  runs on event-level `description`/`initialDescription`.
- **Verified**: Wellspring "Bathe" now reads
  `"Remove [blue]1[/blue] card from your [gold]Deck[/gold]. Add
  [blue]1[/blue] [red]Guilty[/red] to your [gold]Deck[/gold]."` —
  `{BatheCurses}` → `1`.

### NEOWDESC — Neow event description returns raw loc key
- **Observed**: `event.description` on Neow = `"ancients.NEOW.pages.INITIAL.description"`.
- **Not systemic**: "This or That?" event (Floor 3) returns a fully
  resolved description with BBCode. So `ExtractEvent` + `SafeLocString`
  work correctly in general. The anomaly is Neow-specific.
- **Hypothesis**: Neow uses a page/stepper pattern where root
  `Description` is intentionally the key and displayed text lives
  under `CurrentPage.Description` or similar. Needs code inspection
  to confirm.
- **Severity**: low — only affects the one event.

### OSTY — Necrobinder minions absent from combat state
- **Observed on Necrobinder run, Floor 1 Slimes encounter**:
  - Bound Phylactery promises "Summon 1" at start of turn. No new creature appears in `combat.enemies`, `combat.player.powers`, or any other array.
  - Played Bodyguard ("Summon 5"). Energy went 3→2, card entered discard, no minion surfaces anywhere in `state.combat`.
  - `combat` top-level keys observed: `currentSide, discardPile, drawPile, encounter, enemies, energy, exhaustPile, hand, maxEnergy, player, roundNumber, stars`. No `allies`/`minions`/`summons`/etc.
- **Impact**: Necrobinder is unplayable via bridge without Osty state. Cannot know Osty's HP, buffs, next action, whether it's alive. This entire character class is invisible.
- **Diagnostic added**: `EmitOstyDiagnostic` (one-shot, fires on first ExtractCombat) reflects CombatState / Player / Player.Creature / CombatRoom for field/prop names matching `ally|allie|minion|summon|osty|creature|pet|companion|friend`. Rebuild and re-enter combat to capture.
- **Severity**: HIGH.

### NEOWRELIC — Neow relic not in extract until first combat
- **Observed on Necrobinder run**: Selected Winged Boots at Neow. Proceeded to Map. `run.relics` showed only `RELIC:BOUND_PHYLACTERY`, no Winged Boots. User confirmed visually that Winged Boots IS on the HUD from the moment we hit the Map. Entered combat (SelectMapNode to Monster), re-read state: `run.relics` now includes `RELIC:WINGED_BOOTS`. So the relic is granted asynchronously; the state write at MapScreenOpen trigger runs before `player.Relics` contains it.
- **Likely location**: the SelectEventOption dispatcher's deferred state refresh (if any) happens before Neow's `Chosen()` task completes the relic grant. Or Neow sequences through multiple tasks (award relic → advance Page → close event → open map), with our snapshot landing between award-creation and relic-commit.
- **Fix approach**: extend `ScheduleDeferredStateRefresh` to cover SelectEventOption with a longer delay or multiple ticks, OR hook Neow's `IntegrateRelic`-equivalent method and trigger a refresh there.
- **Severity**: medium. Self-healing on first combat entry.

### POTEMPTY — Empty potion slots serialize as null
- **Observed on Necrobinder start**: `run.potions` = `[null, null, null]` for 3 empty slots (`maxPotionCount=3`).
- **Extractor site**: BridgeStateExtractor.cs:670 iterates `player.PotionSlots` and emits `null` for null entries. Functional but wastes bytes and forces consumers to handle null-vs-object.
- **Fix approach**: either omit nulls (simpler) or emit `{slot:N, empty:true}` (preserves slot index). Omit seems fine — slot index can be derived from array index; `maxPotionCount` already tells consumers how many total slots exist.
- **Severity**: low.

## Resolved this session

- **M — Card descriptions missing.** Added `description` field to
  ExtractCard/Relic/Potion/Power via tooltip extraction
  (`GetDescriptionForPile`, `DynamicDescription`, `DumbHoverTip.Description`).
  Verified live on Floor 11: Strike with Eternal rider, Defend+ with
  upgrade value, Perfected Strike with dynamic var rider, Crimson Mantle+
  with Perfect Fit enchant, relics, potions all render resolved text.

- **Y — Event enchantment not in JSON.** Fixed by the same tooltip
  extraction: enchantments are baked into the resolved card description
  (verified via Crimson Mantle+ showing "Perfect Fit"). Separate structured
  `enchant` field may still be worth adding later for controllers that
  want to reason about enchant state programmatically.

## Retracted

- **AD — Myte Strategic intent.** The ~20 HP loss on Floor 8 was unblocked
  Toxic end-of-turn damage (5 HP × retained Toxic cards). Toxic *is*
  playable (costs 1E to play, blockable as self-damage). Myte intent
  readout was accurate.
- **AF — Treasure-room gold anomaly.** Gold just updated asynchronously
  after the treasure open animation.

## Cross-reference to 2026-04-19 handoff

The 2026-04-19 full-bug-triage handoff applied fixes for:
- Bottled Potential / general post-potion stale state → relates to **W**, **X**
- Rewards ghost entries → not observed this session (may be fixed)
- treasure.relicChoices persistence → not exercised this run yet
- Elite relic reward null → not exercised yet
- run/combat HP mismatch at GameOver → not exercised (no deaths yet)
- Hand cards now have handIndex → verified present in state.json this run
- Deck entries enchant/modifier → see note under **Y**

Those fixes are in the current-build DLL (same build produced tooltip
extraction), so re-running a scenario that exercises them is the cheapest
verification path.

## Necrobinder autonomous run (session 2) — 2026-04-20

Second autopilot session driving a Necrobinder run via Bash-based
`autopilot-lib.ps1` invocations (one `Clear-Ipc` + `Send-BridgeCommand`
per batch). Session ID counter started at 3050, reached ~3155 by Floor 4.

### Run progress

| Floor | Type | Outcome | HP | Gold | Notes |
|-------|------|---------|----|------|-------|
| 0 | Neow | optionIndex=2 (2 random relics + extra cards) | 66/66 | 99 | Relics appeared after Floor 1 combat |
| 1 | Monster (Shrinker Beetle) | Victory, 5 rounds | 60/66 | 119 | Osty grew 1→2→3, took damage. Picked Snap. |
| 2 | Event (Wood Carvings) | Enchanted Strike with Slither | 60/66 | 119 | cardGrid.cards nested under `.card` |
| 3 | Monster (Leaf Slime x3) | Victory, ~14 dmg | 52/66 | 135 | SNAPRETAIN stall. Osty died. Picked Negative Pulse. |
| 4 | Map (Monster) | — in progress — | 52/66 | 135 | Session paused for documentation |

Final state: Floor 4, Map, HP 52/66, Gold 135, Deck 14, Relics 4, Potions 3.

### Relics granted by Neow (appeared after Floor 1 combat)
Bound Phylactery, Large Capsule, Horn Cleat, Meal Ticket.

### Deck (14 cards)
5× Strike_N (one enchanted with Slither), 5× Defend_N, Unleash, Bodyguard, Snap, Negative Pulse.

### Findings confirmed or re-verified

1. **SNAP-HANDSELECT** (already logged above) — confirmed again on this run.
   Snap's retain-prompt blocks combat for ~20 no-op commands.
   `HandSelectCard handIndex=N` resolves it.
2. **NEOWRELIC** — confirmed: relics not in `run.relics` after Neow, appear
   after first combat. Self-healing behavior.
3. **cardGrid nesting** — `cardGrid.cards[i].card.title` not `cards[i].title`.
   Same pattern as `cardRewardOptions.cards`.
4. **Map node `pointType`** — not `type`.
5. **Stale command replay** — `Reset-Session -StartingId N` workaround confirmed.
6. **SkipAllRewards CMD_ERROR** when rewards already empty — expected, not a bug.
7. **Osty ally** — surfaces in `combat.allies[]`. Confirmed working. Osty
   died (HP 0) on Floor 3 (Leaf Slime fight).

### New: BRIDGE_STALL pattern for controllers

When the game enters an unhandled UI state (e.g., Snap's hand-select),
ALL commands return `ok=True` but have no effect. No error signal.

**Detection**: revision doesn't advance despite `ok=True`.
**Recovery**: try `HandSelectCard`, `HandConfirmSelect`, `Proceed`,
or `HandCancelSelect`.

This should be documented in the runbook for future controller implementations.

### New: Bash-based control pattern

```
# Each command invocation:
powershell -c ". E:\...\autopilot-lib.ps1; Clear-Ipc; Send-BridgeCommand @{type='...'}; Start-Sleep -Ms 500; Wait-StateChange -TimeoutMs 5000; Get-State"
```

- Must `Clear-Ipc` before each batch to prevent stale commands blocking dispatch.
- `Reset-Session -StartingId N` at session start prevents stale command replay.
- PTY sessions accumulate buffer and eventually misbehave; Bash is more stable
  for long autonomous runs.

---

### ~~BRIDGE_SILENCE~~ — RETRACTED: Controller session ID mismatch

**Status**: RETRACTED — not a bridge bug. Was a controller-side session ID persistence issue.

**Original observation**: Bridge appeared to stop processing commands. Commands timed out with no result.

**Actual cause**: The autopilot controller (autopilot-lib.ps1) resets `$script:NextId = 1` on every
dot-source. Each new script invocation starts from ID 1. But the bridge's `BridgeCommandReader`
tracks `_lastProcessedId` and silently drops any command with `id <= _lastProcessedId`.

When the controller script ran in a new process (e.g., after relaunch), it sent commands with
IDs like 1, 2, 3 — which the bridge had already processed in a previous invocation (IDs up
to 700+). The bridge silently dropped them as "stale."

**Fix applied**: autopilot-lib.ps1 now persists session state to `autopilot-session.json` in
the IPC directory. The `NextId` is loaded from the file on each dot-source, and saved after
each command dispatch. `Reset-Session` uses `Math.Max(requestedId, currentNextId)` to prevent
accidentally lowering the counter.

**Lesson**: Always verify command IDs are above the bridge's last processed ID. The bridge
exposes no way to query `_lastProcessedId` — consider adding it to state.json.

**Note**: The code analysis revealing discarded Tasks and silent catches is still valid and
should be addressed, but none of these caused the observed "silence."

---

## Necrobinder run 2 — fresh start (session 2, continued)

Fresh run after fixing session ID persistence. Neow: option 0 (upgrade Strike+Defend)
via CardGridApply indices @(0,5). Proceeded to Map.

**Floor 1: Monster (col=2, row=1) — DEATH**

Combat driver issues:
1. Round 1: combat state not populated (hand/enemies null). Driver skipped.
2. Enemy `index` property doesn't exist — targeting failed silently, attack cards became self-target plays.
3. Card sort by effectiveEnergyCost put Defends before Strikes — only blocks were played.
4. Death at round 6, HP 0/66.

**Enemy targeting fix needed**: Check bridge-protocol-notes.md for enemy schema.
Enemies use `slotName` (string), not `index` (int). Target index is likely the array position.

**Card priority fix needed**: Sort by card type (Attack first), then by cost.

---

## Resume here

1. Fix combat driver: enemy targeting (array index), card priority (attacks first), wait for combat state
2. Start a fresh Necrobinder run
3. Drive further into Act 1
4. After successful run: triage bugs, GitHub release prep
