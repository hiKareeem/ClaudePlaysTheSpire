# Regent / Necrobinder / Defect mechanics exploration — 2026-04-20

Fresh run started to surface mechanics the bridge may not extract for
non-Ironclad classes. Current run: **Regent**, seed 3386509356231047970,
Floor 1 Neow event.

## Findings so far

### Good — already extracted
- `deck[].baseStarCost` and `deck[].currentStarCost` are already
  present (value `-1` for non-star cards like Strike, `2` for
  Falling Star). Confirmed during Nibbit fight.
- `deck[].enchantment` and `deck[].affliction` fields present (null
  on basics — will see population during run).
- Tooltip extraction working on Regent basics: Strike shows
  "Deal 6 damage." clean. Falling Star shows full text with
  BBCode `[gold]Weak[/gold]`, `[gold]Vulnerable[/gold]`.
- Relic description for Divine Right resolves, including `[img]`
  markup for star icons.
- `combat.stars` field: top-level sibling of `energy`/`maxEnergy`.
  Correctly tracks Divine Right's 3-star grant at combat start,
  drops to 1 after Falling Star (cost 2), back to 3 after Venerate
  (+2). Persists across player turns (Venerate gains stayed).
- **Multi-intent extraction works**: Nibbit round 2 showed
  both `SingleAttackIntent` (damage 6) and `DefendIntent` in
  `intents[]` array — bridge extracts multi-intent correctly.
- **Power amount + description**: on-hit Weak and Vulnerable
  applied to Nibbit surface as `powers[]` entries with
  `amount:1`, full resolved `description`, `type:"Debuff"`,
  `isVisible:true`. Correct.
- `run.character` field is `"CHARACTER:REGENT"` (properly
  populated — my earlier CID bug was reading the wrong key).

### Open — gaps identified

#### ~~EVDESC — Event top-level description is a raw loc key~~ — NARROWED to NEOW-ONLY
- **Re-check on Floor 2 "This or That?" event**: `event.description` is
  fully resolved with BBCode: `"Arms suddenly jut out from a nearby
  hole, clutching a [gold]suspicious bag of riches[/gold] and a clearly
  [purple]cursed relic[/purple]...[jitter][blue]\"This... or That?\"[/blue][/jitter]"`.
  `event.initialDescription` is also resolved identically.
- So `ExtractEvent` + `SafeLocString` work in the general case.
- **Remaining anomaly**: Only **NEOW** returns the raw key
  `"ancients.NEOW.pages.INITIAL.description"`. Likely Neow uses a
  page/stepper structure where the root `Description` is intentionally
  the key and the real displayed text lives under
  `CurrentPage.Description` or similar. Specific to Neow, not a
  systemic `ExtractEvent` bug. LOW priority; revisit if another Neow
  occurs or by code inspection.

#### EVOPTVAR — Event option descriptions contain unresolved template vars
- **Observed** on "This or That?": option descriptions carry literal
  `{HpLoss}`, `{Gold}`, `{Curse}` placeholders:
  - "Lose [red]{HpLoss}[/red] HP. Gain [blue]{Gold}[/blue] [gold]Gold[/gold]."
  - "Add [red]{Curse}[/red] to your [gold]Deck[/gold]. Obtain a random [gold]Relic[/gold]."
- Game UI presumably substitutes these with the per-run values.
- **Likely cause**: option description resolver uses base loc text
  without running the event's variable substitution step (event-scoped
  `GetFormattedText` variant or the page's `FormatOptionDescription`).
- **Severity**: medium. Decision-making controllers can't see real
  HP/gold/curse values without parsing `title` or poking internal
  fields. Worth fixing.
- **Fix lead**: check `EventOption` / `EventPage` for a formatted
  description accessor (e.g. `GetFormattedDescription()`) and use that
  in `ExtractEvent`.

#### Event option field naming (correction to prior notes)
- Actual option fields: `index`, `title`, `description`, `textKey`,
  `historyName`, `isLocked`, `disableOnChosen`, `wasChosen`,
  `isProceed`, `relic`.
- (Earlier notes mentioning `optionIndex` and `text` were wrong — those
  fields don't exist; I was mis-selecting in pwsh format-table output.)

#### ~~CID — `run.characterId` empty~~ — RETRACTED
- I was reading `run.characterId` which doesn't exist.
  The actual field is `run.character` and it correctly reads
  `"CHARACTER:REGENT"`. Non-bug — my mistake.

#### ~~STAR — `run.stars` empty~~ — RETRACTED
- Same class of error: `run.stars` doesn't exist as a field.
  Stars are combat-scoped and surface correctly as
  `combat.stars`. No bug.

#### ~~NEOW — Arcane Scroll option dropped reward~~ — RETRACTED
- **Re-check on Floor 2 combat start**: `run.relics` has 2 entries
  (Divine Right + **Arcane Scroll**), `run.deck.Count` = 11 with
  **Bundle of Joy** (Rare) added. Reward landed correctly.
- **Root cause of the false alarm**: I inspected `state.json` too soon
  after `Proceed`, before the async `chosen.Chosen()` task and the
  subsequent auto-reward-pickup finished applying. Classic async-timing
  read.
- **Defensive suggestion (LOW priority)**: `ScheduleDeferredStateRefresh`
  already exists at BridgeCommandDispatcher.cs:1704 for similar races;
  DispatchSelectEventOption could schedule one after `chosen.Chosen()`
  to close the window for tools. Not a user-visible bug.

## Next

- Pick a Neow option (likely **Arcane Scroll** — random rare card adds
  variety for mechanics exploration; alternative: **New Leaf** for
  safer info-gather, but it just transforms a card).
- Enter Floor 2 combat → inspect combat.player for stars field.
- Progress through 2–3 combats, 1 event, 1 shop to survey
  class-specific state fields.
- Watch for: star cost on non-basic Regent cards, event dialog fields
  unique to Regent (if any), shop pricing for star-cost cards.
