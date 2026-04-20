# Large Capsule Neow Anomaly (2026-04-19)

## What happened

Fresh Ironclad run (ascension 0). Neow offered three Ancient-rarity options:
- Lead Paperweight (choose 1 of 2 Colorless cards)
- Arcane Scroll (random Rare card)
- **Large Capsule** (2 random Relics + 1 extra Strike + 1 extra Defend) — **chosen**

After `SelectEventOption optionIndex=2` → event turned `isFinished=true` with empty options (normal).
After `Proceed` → arrived at Map.

## Expected post-Proceed state

- `run.relics.Count == 3` (Burning Blood + 2 random)
- `run.deck.Count == 12` (5+4+1 starter + 1 Strike + 1 Defend)

## Observed post-Proceed state

- `run.relics.Count == 1` (only Burning Blood)
- `run.deck.Count == 10` (5 Strike + 4 Defend + 1 Bash — unchanged starter)

No `rewards` payload, no `cardRewardOptions`, no card-grid screen.

## Trace evidence (clean, no errors)

```
20:07:06 NEventRoom.BeforeOptionChosen option=Large Capsule
20:07:06 NEventRoom.RefreshEventState postfix fired
20:07:06 DispatchSelectEventOption idx=2 title=Large Capsule
20:07:26 NMapScreen.Open postfix fired
20:07:26 ClearTransientPayloads reason=MapScreenOpen
20:07:26 NEventRoom.Proceed postfix fired
20:07:26 DispatchProceed via NEventRoom.Proceed
```

No faults, no exceptions, no grant hooks visible.

## Hypotheses

1. **State-extraction lag** — relics + deck additions happen after our final snapshot; a deferred refresh (like the one Hermes added for UsePotion resolution) may be needed for Neow resolution.
2. **Game itself didn't grant** — StS2 Large Capsule may work differently than StS1 (e.g., grants *on first combat entry* or via a separate reward flow we didn't trigger).
3. **Grants applied but hidden** — relics stored on a different collection not reflected by `ExtractRelics`.

## To verify

- Enter first combat; re-check `run.relics.Count` and `run.deck.Count` after the starting snapshot.
- If still 1/10 after combat, this is a real bug — needs reflection into NGame/Neow grant pipeline.
- Lead Paperweight presumably opens a card-grid screen — could serve as a complementary repro to distinguish "grants need a screen" vs "grants happen silently".

## Follow-up observation (same session)

After entering the first Monster room (floor 2, map node 3,1) a fresh `AfterPlayerTurnStart` snapshot landed with:

- `run.relics.Count == 4` (Burning Blood + **Large Capsule** + **Paper Phrog** + **Strawberry**)
- `run.deck.Count == 12` (5 Strike + 4 Defend + 1 Bash + 1 Strike + 1 Defend)
- `run.maxHp == 87` (80 + 7 Strawberry bonus)

So the Large Capsule grants DID resolve; they simply weren't present in the `PostDispatch:Proceed` (rev 23) nor `MapScreenOpen` (rev 20-22) snapshots. They only became visible once combat started and a new snapshot was produced by the combat-entry hooks.

A similar deferred-grant pattern was observed on the next event: **Join Forces** (floor 2-Unknown = Jungle Maze Adventure). The gold reward (+53 gold: 110 → 163) only appeared in the post-combat snapshot on floor 3, not in the immediate `PostDispatch:SelectEventOption` nor `PostDispatch:Proceed` snapshots.

## Diagnosis

This is an **extraction-timing** issue, not a state-loss issue. The game applies Neow/event grants asynchronously (probably one or more frames after the event callback returns). Our immediate `PostDispatch:*` snapshot captures state before the game's async apply completes.

Hermes's recent `ScheduleDeferredStateRefresh` pattern (added for `UsePotion`/`DiscardPotion`) is exactly the fix shape. Extending it to:
- `DispatchSelectEventOption` (for in-event grants)
- `DispatchProceed` (for Neow-style proceed grants)

would eliminate the phantom "stale" window.

## Status

Confirmed as a bridge extraction-timing bug. Not a game bug. Not a data-loss bug — consumers who re-snapshot on any subsequent trigger see the correct state.

Suggested next handoff: extend `ScheduleDeferredStateRefresh` coverage to `SelectEventOption` and `Proceed` (at minimum when proceeding from an event room).
