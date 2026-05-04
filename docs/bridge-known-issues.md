# HermesBridge — Known Issues

Live tracking doc for bridge-side bugs surfaced during SpireBench trial-v0.
Each issue documents repro steps, observed `state.json` evidence, agent-visible
symptom, and proposed fix path. File one issue per stable repro; collapse
duplicates into the same entry.

Format:
- **id**: short stable handle for cross-referencing in run records / commits
- **severity**: agent-blocking | agent-confusing | cosmetic
- **status**: open | investigating | mitigated | fixed-in-v0.X.Y

---

## BKI-001 — Treasure Proceed strands map state

- **id**: BKI-001
- **severity**: agent-blocking (run-ending)
- **status**: fixed (2026-05-04, BridgeCommandDispatcher.cs DispatchProceed treasure branch)
- **first-observed**: run17 (`2026-05-03-deepseek-v4-pro-silent-run17`), floor 10

### Repro

1. Agent enters Treasure room (`currentRoom.roomType == "Treasure"`).
2. Agent dispatches `OpenChest` (chest opens, relic offered).
3. Agent dispatches `Proceed` *without* `SelectTreasureRelic`.
4. Bridge invokes `NTreasureRoom.OnProceedButtonReleased(null)` via reflection.
5. Map screen opens at the same floor with **zero travelable children** even
   though `map.grid` clearly lists reachable children of the current coord.

### Observed state.json (run17, revision 603, floor 10)

    "screen":           { "name": "MapClosed" }
    "currentRoom":      { "id": "<null>", "roomType": "Treasure" }
    "treasure":         null
    "map.currentCoord": { "col": 3, "row": 9 }
    "map.available":    []
    "map.grid":         [ … includes (2,10), (3,10), (4,10) as children of (3,9) … ]

### Why it stranded the agent

`map.available` is the agent's only legal-moves source. With it empty and
no active room context, the agent cannot dispatch `SelectMapNode`, cannot
return to the treasure screen, and cannot dispatch any other room-exit
command. Agent halts with `bridge_stall`.

### Root cause (confirmed in-game 2026-05-04)

The treasure room has **no Proceed button** in the in-game UI. After
`OpenChest`, the player has exactly two exits:

1. Click a relic → `NTreasureRoomRelicCollection.PickRelic(holder)` → room
   auto-advances.
2. Click the Skip button → some path that records map-point history AND
   opens the map screen with travel enabled.

The bridge had been mapping `Proceed` to `NTreasureRoom.OnProceedButtonReleased(null)`
(the field is `_proceedButton` with `IsSkip=true` in the skip-relic state),
but invoking that handler via reflection does **not** route through the
in-game Skip button's actual codepath. Confirmed by Kareem: clicking the
in-game Skip button works correctly ("the map appears with travelable nodes
yes"); the reflection invoke produces the stranded state every time.

### Fix (2026-05-04)

Apply the **`MerchantRoom.Exit` pattern** instead of invoking the broken
button handler. In `BridgeCommandDispatcher.cs:DispatchProceed`'s treasure
branch:

1. Guard preconditions:
   - `_hasChestBeenOpened == true` (else: chest not yet opened — error).
   - `_relicCollection._holdersInUse.Count > 0` (else: no relic to skip — error).
2. Resolve `RunManager.Instance.State` as `IRunState` via reflection.
3. Call `treasure.Exit(runState)` — fire-and-forget, records map-point history.
4. Call `NMapScreen.Instance.SetTravelEnabled(true)` then `Open(false)`.

Verified in-game on 2026-05-04 (DLL 278016 bytes):

- Closed-chest Proceed → rejected with chest-not-opened error.
- OpenChest → ok, Strawberry relic offered.
- Proceed-to-skip → `available.Count=1` with `(3,10) Monster Travelable`.

### Agent-side guidance (protocol-level)

In treasure rooms, after `OpenChest`:
- Take the relic with `SelectTreasureRelic` (preferred whenever a useful
  relic is offered).
- Skip with `Proceed` only when the relic is undesired.
- Never dispatch `Proceed` before `OpenChest` — the bridge will reject.

---

## BKI-002 — Post-Act-boss `available` is empty despite legitimate moves

- **id**: BKI-002
- **severity**: agent-blocking (run-ending)
- **status**: fixed (2026-05-04, BridgeStateExtractor.cs available builder + BridgeCommandDispatcher.cs validator)
- **first-observed**: multiple trial-v0 runs after Act 1 boss completion

### Repro

1. Agent defeats Act 1 boss.
2. Boss reward screen resolves; agent dispatches `Proceed`.
3. New map opens with `screen=Map`, `currentRoom=null`, `currentCoord=null`,
   `map.available=[]` despite `map.grid` containing a `Travelable` row-0
   Ancient node.

### Observed state.json (live capture 2026-05-04)

    "screen":              { "name": "Map" }
    "currentRoom":         null
    "map.currentCoord":    null
    "map.available":       []
    "map.bossCoord":       { "col": 3, "row": 15 }
    "map.grid": [
      { "col": 3, "row": 0,  "pointType": "Ancient", "state": "Travelable",   "offGrid": true, "children": [] },
      { "col": 1, "row": 1,  "pointType": "Monster", "state": null,           "children": [{...}] },
      …
      { "col": 3, "row": 15, "pointType": "Boss",    "state": "Untravelable", "children": [] }
    ]

The Ancient at (3,0) has `state: "Travelable"` and `offGrid: true`. Probing
with `SelectMapNode {col:3, row:0}` was accepted by the bridge AND by the
game (transitioned to the Tezcatara/Ancient event correctly). So the move
was legitimate — but `available` reported `[]`.

### Root cause (diagnosed in-game 2026-05-04)

`NMapPoint` exposes two travelability signals:

- `MapPointState State` (enum: `None | Travelable | Traveled | Untravelable`) —
  set by the game's own bookkeeping when a node becomes available; stable
  across frames.
- `bool IsTravelable` (predicate) — additionally checks adjacency to the
  player's `currentCoord`, ascent rules, hover state, etc. Returns false
  transiently when `currentCoord == null` (post-Act-boss transition window).

The bridge's `available`-list builder (`BridgeStateExtractor.cs` L2350) and
the `SelectMapNode` validator (`BridgeCommandDispatcher.cs` L1286) both
gated solely on the `IsTravelable` predicate. Post-boss, the predicate
returns false for the row-0 Ancient even though the State enum says
`Travelable` and `TravelToMapCoord` accepts the move.

### Fix (2026-05-04)

In both spots, accept the node when **either** `IsTravelable == true` **OR**
`State == MapPointState.Travelable`. The state enum is the more reliable
signal for off-grid entry nodes (Ancient, post-act transitions); when the
predicate disagrees with the enum, the predicate is the buggy/transient one
(both `TravelToMapCoord` and the in-game UI accept these moves).

Files:

- `BridgeStateExtractor.cs` ~L2350: added `else if (state == "Travelable")
  available.Add(...)` branch.
- `BridgeCommandDispatcher.cs` ~L1286: validator accepts if predicate true OR
  state enum is Travelable; error message now reports both signals when
  rejecting (`IsTravelable=false, state=…`).

### Verification

Pre-fix snapshot: `available.Count=0`, grid contained Ancient (3,0)
state=Travelable.

Post-fix expected: `available.Count >= 1` containing the row-0 Ancient with
`pointType=Ancient, state=Travelable`. Verified post-deploy in-game (TBD —
test with fresh post-Act-1-boss state once current Ancient event is consumed).

---

## Reporting format for new issues

When a new bridge issue is found during a run:

1. Capture full `state.json` via `Copy-Item $env:APPDATA\SlayTheSpire2\hermesbridge\state.json …`
2. Note the `revision`, the screen, the currentRoom, and the relevant array
   that's empty/wrong.
3. Add an entry here with the next BKI-NNN id.
4. Reference the BKI id in the run record's notes field, not in the
   `halt_reason` (halt_reason stays as the stall taxonomy term).
