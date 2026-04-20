# SelectCardOption disposed-Control fix — verified 2026-04-19

Evidence archive for the fix that lets `SelectCardOption` resolve the correct
`NCardRewardSelectionScreen` instance after the overlay has been closed and
reopened (or closed mid-dispatch).

## Symptom

Prior to this fix, sequences like:

1. Open CardReward overlay (rare card grid)
2. Close the overlay (back button / misclick)
3. Reopen the same CardReward overlay
4. Issue `SelectCardOption { optionIndex: N }`

...threw `System.ObjectDisposedException: Cannot access a disposed object.
Object name: 'Godot.Control'.` The dispatcher was holding a reference to the
first (now-disposed) `NCardRewardSelectionScreen` instance captured at
`CardRewardSelectionShown`.

A related path hit the same bug: dispatching `SelectCardOption` while the
CardReward overlay was in the **process of closing** (close animation in
flight, postfix not yet fired) because reward acceptance was racing the close
animation.

## Fix

`BridgeCommandDispatcher.DispatchSelectCardOption` now:

1. Reads `StateStore.LastScreen` fresh at dispatch time (captured in all
   `CardRewardSelectionScreenReadyPatch` / `...ShownPatch` / `...OpenedPatch`
   postfixes, not just the first).
2. Calls `FindLiveCardRewardScreen` which validates the cached reference via
   `GodotObject.IsInstanceValid(...)` before use.
3. On invalid reference, falls back to `FindCardRewardScreenRecursive` which
   walks the active scene tree and returns the first live
   `NCardRewardSelectionScreen`.

Patch files touched:
- `HermesBridgeCode/Patches/CardRewardSelectionScreenReadyPatch.cs`
- `HermesBridgeCode/Patches/CardRewardSelectionShownPatch.cs`
- `HermesBridgeCode/Patches/CardRewardSelectionOpenedPatch.cs`
- `HermesBridgeCode/BridgeCommandDispatcher.cs` (new `FindLiveCardRewardScreen`
  and `FindCardRewardScreenRecursive` helpers).

## What was verified

See `trace-excerpt.log` for the raw lines. Command id 818 (SelectCardOption,
optionIndex=0) was dispatched at T+17:20:53.6041 — **before** the overlay's
`AfterOverlayClosed` postfix fired at T+17:20:53.6217 (~18 ms later). The
dispatcher still resolved the card correctly:

```
DispatchSelectCardOption idx=0 card=Primal Force
RequestWrite trigger=PostDispatch:SelectCardOption currentScreen=Rewards
WroteState revision=24 trigger=PostDispatch:SelectCardOption screen=Rewards
BridgeCommandReader wrote result id=818 status=ok
```

No `ObjectDisposedException` was raised; `currentScreen` advanced from
`CardReward` → `CardRewardClosed` → `Rewards` cleanly across the same id.

## Files

- `trace-excerpt.log` — 26-line slice of the trace covering id=817..818.
- Full session trace available in the sibling
  `2026-04-19-the-kin-boss/trace.log` archive (this run continued directly
  into The Kin fight).

## Notes

- This race also pops up for `SelectReward` (reward click firing before the
  screen ready postfix); existing fallback logic there already covered it, so
  no change was required.
- The `IsInstanceValid` check is cheap and defensively correct — keeping it on
  all dispatch paths that hold Godot Node references is recommended.
