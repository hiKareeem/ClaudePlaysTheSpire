# Hermes STS2 runbook

## Purpose
Operational runbook for launching, verifying, controlling, and debugging the Slay the Spire 2 Hermes bridge.

## Launch sequence
1. Launch through Steam URI, not raw EXE:
   - `Start-Process 'steam://rungameid/2868840'`
2. Verify process:
   - `Get-Process -Name SlayTheSpire2 | Select Id,ProcessName,MainWindowTitle`
3. Verify mod load in `%APPDATA%\SlayTheSpire2\logs\godot.log`:
   - BaseLib loaded
   - HermesBridge loaded
   - `--- RUNNING MODDED! ---`
4. Verify bridge files exist under `%APPDATA%\SlayTheSpire2\hermesbridge\`:
   - `state.json`
   - `commands.json`
   - `result.json`
   - `trace.log`

## Control loop
1. Read `state.json`
2. Decide action from structured state first
3. Write command payload to `commands.json`
4. Wait for `result.json`
5. Re-read `state.json`
6. Use window screenshot only when state is ambiguous or a visual confirmation is needed

## Command/result protocol
Input:
- `commands.json`: `{ "id": <monotonic int>, "command": { "type": "...", ... } }`

Output:
- `result.json`: `{ "id", "status", "message", "timestampUtc", "revision" }`

Authoritative command list: `HermesBridgeCode/BridgeCommandDispatcher.cs` (search `case "..."`).
Commonly-guessed-wrong names — do NOT use these, they return `unknown command type`:
- `SelectGold`, `SelectPotionReward`, `SelectRelicReward`, `SelectCardReward` — all rewards use the unified `SelectReward` / `SkipReward` / `SkipAllRewards` surface. The dispatcher branches on the reward's runtime type. Addressing: prefer `rewardPosition` (array index, always unique) over `rewardIndex` (game `RewardsSetIndex`, not guaranteed unique for multi-reward events).
- `SelectCardReward` vs `SelectCardOption`: after `SelectReward` on a card reward, the follow-up is `SelectCardOption {cardIndex}` (positional 0/1/2).
- `SelectEventOption` / `SelectRestOption` / `SelectCardsInGrid` use `optionIndex` / `optionIndex` / `cardIndices` respectively — not `index`, `optionId`, or `indices`.

## Verified stable actions
- Continue run
- Start run (`StartRun`) direct from the bridge
- Play card
- End turn
  - including guarded `EndTurn` with `expectedRevision`, `expectedScreen`, `expectedCurrentSide`, and `expectedRoundNumber`
  - stale replay now returns diagnostic error instead of misleading `ok`
- Select event option during Neow/event flow
- Choose a card from `ChooseACardSelection`
- Proceed from event and rewards screens
- Purchase shop entries
- Leave shop back to map
- Main menu -> singleplayer -> character select via UI / keypress

## Failure modes
### 0. Commands silently dropped (session ID mismatch)
Symptom:
- Commands time out with no result
- Bridge trace.log shows no new dispatch entries
- Game is running, state.json has a stale timestamp
Cause:
- autopilot-lib.ps1 resets `$script:NextId = 1` on each dot-source
- Bridge's `_lastProcessedId` is higher (from a previous session or manual command)
- Bridge silently drops commands with `id <= _lastProcessedId`
Recovery:
- Check session file at `%APPDATA%\SlayTheSpire2\hermesbridge\autopilot-session.json`
- Ensure `nextId` is higher than the bridge's last processed ID
- Use `Reset-Session -StartingId <high_value>` or seed manually
- The fix: autopilot-lib now persists session state across invocations

### 0.5. Bridge command reader thread dies silently (BRIDGE_SILENCE) — theoretical risk
Note:
- This was suspected but RETRACTED — all observed "silence" was session ID mismatch
- However, code analysis found 5 discarded Tasks and silent catches that COULD cause issues
- Consider adding heartbeat/lastProcessedId to state.json as a health indicator

### 1. Locked mod DLL during build
Symptom:
- `MSB3021` / `MSB3027` copying `HermesBridge.dll`
Cause:
- SlayTheSpire2 keeps loaded mod DLL locked
Recovery:
- Stop `SlayTheSpire2.exe`
- Rebuild
- Relaunch

### 2. Temp-file contention on bridge output
Previous symptom:
- `state.json.tmp` in use by another process
Cause:
- Shared fixed temp file path for atomic-ish writes
Fix:
- Writer now uses unique per-write temp filenames plus replace/move to destination
Implication:
- Controllers should read `state.json` / `result.json`, not temp files

### 3. Visual capture mismatch
Symptom:
- Screenshot shows another window or stale frame
Recovery:
- Capture specific game window, not the whole desktop
- Confirm process and rect before click automation

### 4. Screen-state ambiguity
Symptom:
- Screen names change before UI visually settles
Recovery:
- Trust `trace.log` and `state.json` first
- Use screenshot only as verification
- Treat `MapClosed`, `RewardsClosed`, and similar `*Closed` screens as transitional unless they persist after room-entry hooks fail to arrive

### 5. Transitional map-close after travel
Symptom:
- `SelectMapNode` returns `ok`, then the next snapshot is `screen=MapClosed`
Cause:
- Map travel closes the map before the next room/ combat hooks publish the real destination state
Recovery:
- Wait for `AfterRoomEntered` / `BeforeCombatStart` / destination room hooks instead of treating `MapClosed` as terminal
- Use `trace.log` ordering to confirm whether the room transition is still in flight before declaring a snag

### 6. EndTurn pacing / stale replay
Symptom:
- A repeated `EndTurn` based on an old snapshot now returns an explicit mismatch error instead of `ok`
Cause:
- The bridge now validates optional stale-state guards: `expectedRevision`, `expectedScreen`, `expectedCurrentSide`, `expectedRoundNumber`
Recovery:
- Send `EndTurn` with the latest tuple from `state.json`
- After `EndTurn`, wait for a newer revision before issuing another action
- Do not assume the immediate `PostDispatch:EndTurn` snapshot already reflects enemy-turn ownership changes

### 7. Stale commands on relaunch
Symptom:
- Immediately after relaunch, `BridgeCommandReader` processes an old command from `commands.json`
Cause:
- The command channel persists across runs; the bridge reads the latest file on startup
Recovery:
- Clear or overwrite `commands.json` deliberately before taking control of a freshly launched session

## Verification checklist after bridge changes
- Build succeeds
- HermesBridge DLL copied into game mod folder
- Game launches and mod initializes
- `state.json` revision increments during screen/combat transitions
- `result.json` updates after commands
- `last-error.txt` remains empty or absent during normal play

## Stall detection (BRIDGE_STALL)

When the game enters a sub-screen UI state (e.g., Snap's hand-selection prompt),
ALL bridge commands return `ok=True` but silently no-op. No error is returned.

**Detection**: revision doesn't advance despite `ok=True` for 3+ consecutive commands.

**Recovery commands** (try in order):
1. `HandSelectCard handIndex=0` — resolve pending hand-selection prompt
2. `HandConfirmSelect` — confirm a multi-select
3. `HandCancelSelect` — cancel a pending selection
4. `Proceed` — advance past a blocking screen

**Cards known to trigger this**: Snap (retain sub-prompt), any card with
a "choose a card in hand" secondary effect.

## Bash-based control pattern (for autonomous runs)

```powershell
# Per-command invocation:
. E:\Games\sts2\HermesBridge-StS2\autopilot-lib.ps1
Clear-Ipc
Send-BridgeCommand @{ type = 'CommandType'; param = 'value' }
Start-Sleep -Milliseconds 500
Wait-Revision -TimeoutSec 5
Read-State
```

Key rules:
- Always `Clear-Ipc` before each command batch
- `Reset-Session -StartingId N` at session start to prevent stale replay
- Track revision number for stall detection
- PTY sessions degrade over time; Bash invocations are more stable for long runs

## Notes from current live test
- Real run successfully started with Ironclad
- `StartRun` is reliable enough to use as the default setup path for live bridge validation
- Neow flow worked through `SelectEventOption` -> `ChooseACard` -> `Proceed`
- Combat control worked through bridge commands
- Reward pruning is now validated live: gold/potion rewards disappear immediately after `AfterRewardTaken`, and card rewards can collapse to `[]` while the rewards screen remains open until final proceed
- Shop flow is now validated live: buying a card updates both `shop.playerGold` and `run.gold`, nulls the bought entry, and `LeaveShop` returns to a clean map state
- `MapClosed` after `SelectMapNode` is transitional rather than a bug if room-entry/combat hooks follow immediately in trace order
- Guarded `EndTurn` is now validated live on the deployed bridge:
  - fresh tuple returns `ok`
  - stale replay fails with revision mismatch
  - bad screen assumption fails with screen mismatch
- Immediate `PostDispatch:EndTurn` can still show `screen=Combat` and `currentSide=Player`; controller pacing must wait for a later settled revision rather than assuming turn ownership changed in the first post-dispatch snapshot
- Relaunch can consume a stale `commands.json` payload once during startup if the file is not cleared first
- First robustness snag was temp-file contention, now addressed in source
- Next likely content work: command taxonomy doc and per-class strategic references
