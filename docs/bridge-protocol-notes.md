# Bridge protocol notes

## Files
- `state.json`
- `commands.json`
- `result.json`
- `trace.log`
- `last-error.txt`

## State payload areas currently observed
- `screen`
- `combat`
- `run`
- `event`
- `shop`
- `restSite`
- `map`
- `cardGrid`
- `chooseACardScreen`
- `handSelect`
- `rewards`
- `cardRewardOptions`

## Observed screen names
- `MainMenu`
- `SingleplayerSubmenu`
- `CharacterSelect`
- `Map`
- `MapClosed`
- `Rewards`
- `RewardsClosed`
- `CardReward`
- `CardRewardClosed`
- `Event`
- `Room:Event`
- `Room:Shop`
- `Combat`
- `GameOver`

## Observed triggers worth trusting
- `MainMenuReady`
- `SingleplayerSubmenuReady`
- `SingleplayerSubmenuOpened`
- `CharacterSelectOpened`
- `MapScreenReady`
- `MapScreenClose`
- `BeforeRoomEntered`
- `AfterRoomEntered`
- `BeforeCombatStart`
- `AfterPlayerTurnStart`
- `AfterCardPlayed`
- `AfterCardPlayedLate`
- `BeforeTurnEnd`
- `PostDispatch:<CommandType>`

## Notes
- Post-dispatch snapshot forcing is useful because callers immediately see post-command state.
- The bridge can emit multiple near-duplicate writes during transitions; consumers should compare revision and payload, not assume one trigger per screen.
- Consumers should ignore temp files entirely. Only final files are part of the protocol.
- `StartRun` is now validated as the preferred setup path for live bridge testing; it is more reliable than clicking through title/menu UI when the goal is protocol validation.
- Neow/event flow is confirmed live for `SelectEventOption`, `ChooseACard`, and `Proceed`; the bridge can drive this opening without vision-based clicking.
- Event proceed used to end with `screen=EventClosed` after the map had already opened. That was a patch-order bug in `EventRoomProceedPatch`; current behavior keeps `screen=Map` and only clears the event payload.
- Reward consumption is now confirmed live: after `AfterRewardTaken`, gold and potion rewards are pruned immediately from `state.rewards[]`, and after `SelectCardOption` the rewards list can legitimately become `[]` while remaining on `screen=Rewards` until the overlay fully exits.
- Shop purchase flow is confirmed live: `Purchase` updates both `shop.playerGold` and `run.gold`, nulls out bought card entries in-place, and `LeaveShop` returns cleanly to `screen=Map` with populated `map.available[]`.
- Map travel remains asynchronous after `SelectMapNode`: a controller may briefly see `Map` or `MapClosed` before `AfterRoomEntered` / `BeforeCombatStart` establishes the next room. Treat `MapClosed` as a transitional screen, not a terminal navigation state.
- `EndTurn` is now live-validated with stale-state guardrails: the bridge accepts optional `expectedRevision`, `expectedScreen`, `expectedCurrentSide`, and `expectedRoundNumber` fields and returns an explicit mismatch error instead of misleading `ok` when a stale turn-end command is replayed.
- Important timing nuance: the immediate `PostDispatch:EndTurn` snapshot can still show `screen=Combat` and `currentSide=Player`. Controllers must wait for a newer revision reflecting enemy-turn / next settled ownership changes rather than assuming the first post-dispatch write already reflects the turn handoff.
- Relaunch nuance: if `commands.json` still contains an old command when the game boots, `BridgeCommandReader` may process that stale payload once during startup before the controller takes over. Controllers should clear or overwrite `commands.json` intentionally at session start.

## State payload shape cheatsheet (observed)
- `state.run.currentHp` / `state.run.maxHp` / `state.run.gold` — **at run level**, not under `state.run.player` (which can be null).
- `state.run.potions[]` — sparse array; empty slots serialize as empty object `{}` or null; filled slots have `id`, `title`, `rarity`, `targetType`.
- `state.run.relics[]` — entries have `title`; `description` is currently empty in exports.
- `state.run.deck[]` — each card has `id`, `title`, `type`, `rarity`, `energyCost`, `isUpgraded`, `currentUpgradeLevel`, `pile` (null outside combat).
- `state.combat.hand` — **wrapper object** `{ type, count, cards[] }`, not a flat array. Iterate `state.combat.hand.cards[]`.
- `state.combat.drawPile` / `discardPile` / `exhaustPile` — same wrapper shape as `hand`.
- `state.combat.player.powers[]` — each has `id`, `title`, `amount`, `type` (Buff/Debuff), `isVisible`.
- `state.combat.enemies[]` — each has `id`, `name` (not `title`!), `currentHp`, `maxHp`, `block`, `combatId`, `slotName`, `powers[]`, `intents[]`, `nextMoveId`.
- `state.combat.enemies[].intents[]` — array because enemies can telegraph multiple actions; each has `kind`, `intentType`, `title`, `prefix`, `damage` (nullable), `repeats` (nullable).
- `state.rewards[]` — entries have `kind` ("Gold" / "Card" / "Relic" / ...), `index` (use as `rewardIndex`), and kind-specific fields (`amount`, `cards[]`, `canSkip`, `canReroll`).

## Command parameter gotchas
- `PlayCard`: `handIndex` (required), `targetIndex` optional. For self-target/untargeted cards (Defend, powers), omit `targetIndex` entirely. Supplying a bogus `targetIndex` on a self-target card can cause `TryManualPlay returned false`.
- `SelectRestOption`: requires numeric **`optionIndex`**, not `optionId` (the string is informational only).
- `UsePotion`: `slotIndex` required. For self-affecting potions, either `targetSelf: true` or omit target. For targeted potions (attack potions), use `targetIndex`.
- `SelectMapNode`: `col` and `row` both required. Only nodes in `state.map.available[]` will succeed; others return error.
- Nested `command` object is required, and JSON key order should be `id` then `command` (some writers rely on order for atomicity).

## Refresh-lag quirks (observed, not yet fixed)
- **Potion effects do not trigger a state write.** After `UsePotion` returns `ok`, `state.json` can stay stale for several seconds. The dispatcher reports "enqueued", and the real hand/deck updates only on the next `PlayCard` (which forces `AfterCardPlayed` hook).
- **Bottled Potential specifically**: after drinking, the game's internal hand is shuffled but `state.combat.hand.cards[]` still shows the pre-shuffle cards. A subsequent `PlayCard handIndex=N` will operate on the **real** (shuffled) hand, not the one in state.json — the trace log's `card=<title>` confirms the true card. Controllers should play by **card title match** rather than trust `handIndex` after a potion until a fresh state write arrives.
- **Consumed potion slot** remains filled in `state.run.potions[]` until the next card-play refresh, but internally is empty — re-calling `UsePotion` on the slot returns `potion slot N is empty`.
- **Heart of Iron's Plating power** does not show in `state.combat.player.powers[]` until the next PlayCard triggers a refresh.
- **Vulnerable/Weak/Frail decay** happens at end-of-turn; they will still appear in the `powers[]` list on turn start until the first post-start state write.

## Auto-dismissed UI
- **GameOver screen**: bridge auto-dismisses via `ReturnToMainMenu` after ~3 seconds. Trace shows `GameOverScreen auto-dismissed via ReturnToMainMenu`. Controllers observing `screen.name == "GameOver"` should capture immediately; next write will be `MainMenu`.
- **Continue Run** loads a save state that may pre-date recently claimed rewards (observed: claimed reward + closed game + ContinueRun → reward re-offered). This is a game-level save behavior, not a bridge bug.

## Known bugs as of this snapshot
- `treasure.relicChoices[]` does not shrink after `PickRelic`.
- Curse/status cards report `isPlayable: true` even when they aren't.
- `Slimed` status disappears to neither exhaust nor discard pile.
- Merchant inventory click animation is bypassed by `Purchase` (visual-only issue).
