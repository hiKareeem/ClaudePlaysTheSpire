# SKILL — Autonomous Slay the Spire 2 play-through via HermesBridge

You are an agent tasked with driving Slay the Spire 2 through the HermesBridge
mod to stress-test bridge stability across a full run. This document is the
single source of truth for how the bridge works and what you are expected
to do.

> **Compatibility note.** This workflow has only been validated end-to-end
> on frontier-tier coding models (Claude Opus 4.7-class). Smaller / mid-tier
> agents have repeatedly failed the same way (see §Known failure modes of
> prior agents). If you are not a frontier-tier model, read that section
> first and take its warnings literally.

## Your job

Run the game autonomously via IPC and **surface instability**, not bugs in
game mechanics.

A "run" = one life, character-agnostic, from `MainMenu → StartRun → GameOver`.
You do not need to win. You need to:

1. **Keep the bridge responsive.** Every command must receive a `result.json`.
2. **Never get permanently stuck on a screen.** If a screen has no legal
   action, log it and issue `Proceed` or `AbandonRun` rather than looping.
3. **Log stability issues** to `docs/autopilot-session-<YYYY-MM-DD>.md`:
   - Death floor, last screen, last command id.
   - Any IPC error (timeout, `status != "ok"`, file missing, malformed JSON).
   - Any screen you hit that isn't in §Screen catalog below.
   - Any command that returned `ok` but produced no state change when one
     was expected.
4. **Halt on death or stall.** Do not auto-restart. Leave the game running
   and write the session log. A stall = no state revision change for
   >30 seconds after a command.

You are **not** trying to fix bugs. You are the load generator. Bug triage
is a separate workflow (`docs/session-bugs-*.md` — read-only for your
purposes).

## CRITICAL: how you operate

**Read this section before anything else. Prior agents have ignored it and
the runs have failed the same way every time.**

You are expected to drive the game **one decision at a time**, using your
own agent tool calls. The canonical loop is:

1. Call your shell/bash tool to run **one** `Read-State` (via `autopilot-lib.ps1`).
2. Read the returned JSON yourself. Think about what screen you are on,
   what the legal commands are, and what you want to do.
3. Call your shell tool again to run **one** `Send-BridgeCommand` with the
   command you just decided on.
4. Read the result, re-read state, decide again.
5. Repeat.

Each tick in the game corresponds to (at least) one tool call you make.
Over a run this will be hundreds of tool calls. **That is expected and
correct.** It is the only way we get the coverage we want.

### What you must NOT do

- **Do not write new `.ps1` files.** The repo already has `autopilot-lib.ps1`
  and that is the only script you need. Do not create `fight.ps1`,
  `collect-rewards.ps1`, `new-run.ps1`, `neow.ps1`, or any other task
  runner. If you find yourself reaching for `Write-File foo.ps1`, stop.
  You are about to make the same mistake every prior agent has made.
- **Do not put `while` / `foreach` / `for` loops over game logic inside a
  pwsh block.** A multi-round combat loop inside a single tool call means
  you have stopped driving and started scripting. You lose the per-tick
  inspection that the task actually requires. The game dies while your
  script blindly sorts cards.
- **Do not encode strategy in PowerShell.** "Sort attacks before defends,"
  "target lowest-HP enemy," "skip the potion at index 2," "pick Neow
  option 1" — these are **decisions you make per tick**, informed by the
  current state, not constants baked into a helper script.
- **Do not invoke the library inside your own wrapper functions** like
  `function Send-Cmd($c) { Clear-Ipc; Send-BridgeCommand $c; Start-Sleep 600 }`
  wrapped around a loop. That is a script. Call the library primitives
  directly, one per tool call.
- **Do not batch multiple game commands in one shell invocation.** One
  tool call = at most one `Send-BridgeCommand`.

### Why

The bridge exposes a stateful game with asynchronous refresh quirks,
sub-screens that steal focus (Snap → handSelect), and cards whose legality
depends on live energy / discounts / buffs. Any decision you make more than
one tick in advance of execution is almost certainly wrong. If you script
the loop, you will:

- Target enemies that died last tick.
- Play attack cards against missing `enemy.index` (field doesn't exist),
  silently misfire as self-plays, and lose combat without noticing.
- Sit at a `handSelect` sub-screen issuing `PlayCard` commands that all
  return `ok=true` and do nothing — the STALL pattern documented in
  `docs/session-bugs-2026-04-20.md`.
- Skip rewards by hardcoded index and grab a potion when your inventory
  is full (bug V).

All of these have happened in prior sessions. The only reliable fix is
per-tick state inspection by the agent, not by a script.

## Known failure modes of prior agents

Every non-frontier agent that has attempted this task has failed in one
of the following ways. Do not repeat them.

1. **The `fight.ps1` / `fight2.ps1` pattern.** Wrote a combat driver as a
   single pwsh file: read state, sort hand by cost, loop over cards,
   `PlayCard` each, `EndTurn`. Targeted enemies via `enemy.index`
   (doesn't exist; target is the array position). Sorted by
   `effectiveEnergyCost` ascending, so Defends played before Strikes and
   the player died blocking while enemies healed through defense.
   **Root error: encoding combat policy in a script.**
2. **The `new-run.ps1` / `neow.ps1` pattern.** Launched game, dispatched
   `StartRun`, hardcoded `SelectEventOption optionIndex=1`, hardcoded
   `CardGridApply cardIndices=@(0,5)`. No inspection of what Neow actually
   offered this run. **Root error: Neow options and grid contents are
   run-seeded; hardcoding indices is a coin flip.**
3. **The `collect-rewards.ps1` / `skip-potion-proceed.ps1` pattern.**
   Hardcoded `SkipReward rewardIndex=2` and `SelectCardOption cardIndex=0`.
   Did not check potion inventory capacity before selecting potions (bug V).
   Did not read the actual reward list before deciding.
   **Root error: treating a sampled state as a universal shape.**
4. **The session-ID persistence mistake.** Early autopilot resets
   `NextId=1` on every dot-source; bridge drops commands whose id is
   `<= _lastProcessedId`. Documented and resolved — library now persists
   `autopilot-session.json`. Do not reintroduce it by resetting the
   counter manually.

If the task you are about to write starts to look like any of the above,
stop and ask the user.

## Orientation

Start here:

| File | Why |
|---|---|
| `docs/bridge-protocol-notes.md` | IPC file layout, screen names, triggers, state-payload shape, command gotchas, known refresh lags. **Read in full.** |
| `docs/hermes-sts2-runbook.md` | Launch sequence, failure modes, recovery. |
| `docs/session-bugs-2026-04-20.md` | Current known-bug inventory. Skim; do not try to fix. |
| `autopilot-lib.ps1` | Helper library (IPC primitives only — no policy, no loop). Dot-source it and drive yourself. |
| `HermesBridgeCode/BridgeCommandDispatcher.cs` | Authoritative command list + parameter names — grep `case "..."` near line 47-80. |
| `HermesBridgeCode/BridgeStateExtractor.cs` | Authoritative state-field list. |

When in doubt about a command's parameters, **grep the dispatcher first**,
not this doc.

## Environment

- OS: Windows, PowerShell 7 (`pwsh`).
- Game install: `E:\SteamLibrary\steamapps\common\Slay the Spire 2\`
- Mod repo: `E:\Games\sts2\HermesBridge-StS2\`
- IPC dir: `%APPDATA%\SlayTheSpire2\hermesbridge\` i.e.
  `C:\Users\<user>\AppData\Roaming\SlayTheSpire2\hermesbridge\`
- Launch: `Start-Process 'steam://rungameid/2868840'`. Wait ~15-18s for
  main menu.
- Kill: `Get-Process -Name 'SlayTheSpire2' -EA SilentlyContinue | Stop-Process -Force`

## IPC contract

Four files in the IPC dir:

- `state.json` — written by the bridge. Read-only for you. Contains a
  top-level `revision` number that monotonically increases; use it to
  detect fresh writes.
- `commands.json` — written by you. One command at a time.
  `{ "id": N, "command": { "type": "...", ...params } }`. `id` must be
  strictly monotonic across the entire session; the bridge dedupes by id.
- `result.json` — written by the bridge in response to your command.
  `{ "id", "status", "message", "timestampUtc", "revision" }`.
  `status` is `"ok"` or `"error"`.
- `trace.log` — append-only bridge log. Tail for diagnostics.

**Protocol:**

1. Delete `commands.json` and `result.json` before each command (stale
   payloads on startup can double-apply — see runbook §Relaunch nuance).
   `Clear-Ipc` does this; `Send-BridgeCommand` calls it for you.
2. Write `commands.json` atomically. The bridge polls; it will pick up
   your file within ~100 ms.
3. Wait for `result.json` with matching `id`. Timeout 10 s.
4. Wait for `state.json` revision to advance. Timeout 30 s (stall
   threshold). Some commands (potions especially — see §Refresh lag)
   legitimately don't bump revision until the next tick; `Send-BridgeCommand`
   will flag that as a stall finding without erroring — you decide whether
   to halt or keep going.
5. Re-read `state.json`. Decide next action.

Key principles (from `docs/bridge-protocol-notes.md`, expanded):

- **MapClosed is transitional, not terminal.** If you see `MapClosed`,
  poll; it will become `Combat`/`Event`/`Shop`/`Rest` as the next room
  loads. Don't treat it as an error.
- **Post-EndTurn snapshots can lie.** The immediate `PostDispatch:EndTurn`
  write often still shows `screen=Combat, currentSide=Player`. Wait for
  a **newer revision**, don't assume the first post-command state is
  settled.
- **Reward `index` is `rewardIndex` not array position.** `rewards[i].index`
  is the `RewardsSetIndex` the game uses internally. Use that value as
  the `rewardIndex` param, not `i`.
- **Enemy target is the array position in `combat.enemies[]`, not a
  field on the enemy.** There is no `enemy.index`. Filter out dead
  enemies (`currentHp <= 0`) and use the index into the filtered-or-raw
  array per the dispatcher's expectation (grep `targetIndex` in
  `BridgeCommandDispatcher.cs`).
- **Card reward is two-step.** `SelectReward rewardIndex=N` opens the
  3-card overlay (screen transitions to `CardReward`). Then
  `SelectCardOption cardIndex=K` (0/1/2) picks. You can also
  `SkipReward` before the overlay, or `SkipAllRewards` to drop
  everything.
- **Self-target cards reject `targetIndex`.** Defend, any Power card,
  Bodyguard, Afterlife, etc. Omit the param entirely; a bogus target
  causes `TryManualPlay returned false`.
- **Potion inventory may be full.** Selecting a potion reward when
  `run.potions[]` has no null slots silently no-ops (bug V). Check
  capacity before `SelectReward` on a potion.
- **`StartRun` wants the bare character id.** `"NECROBINDER"`,
  `"IRONCLAD"`, `"SILENT"`, `"DEFECT"`, `"REGENT"` — not
  `"CHARACTER:NECROBINDER"`.
- **Sub-screens exist.** Playing Snap (Necrobinder) opens `handSelect`;
  some events open `cardGrid` or `chooseACardScreen`; shop and chest
  have their own. Each has its own commit command — see §Command
  reference. If `handSelect.active=true`, **only** `HandSelectCard` /
  `HandConfirmSelect` / `HandCancelSelect` are legal. Any other command
  will return `ok=true` and do nothing (the STALL pattern).

## Screen catalog

These are the `screen.name` values your state machine must handle.

| Screen | What it means | Primary commit command(s) |
|---|---|---|
| `MainMenu` | Title screen. | `StartRun` or `ContinueRun` |
| `SingleplayerSubmenu` | Between MainMenu and character select. | (usually auto-handled by `StartRun`) |
| `CharacterSelect` | — | (`StartRun` bypasses this) |
| `Combat` | Your turn or animating. | `PlayCard`, `EndTurn`, `UsePotion`, `DiscardPotion` |
| `Rewards` | Post-combat reward list. | `SelectReward`, `SkipReward`, `SkipAllRewards`, `Proceed` |
| `CardReward` | 3-card overlay after picking a card reward. | `SelectCardOption`, `SelectCardAlternative` (Choose rarity) |
| `RewardsClosed` / `CardRewardClosed` | Transitional after reward commit. | Poll for next screen. |
| `Map` | Map open, choose next room. | `SelectMapNode` |
| `MapClosed` | Transitional, next room loading. | Poll. |
| `Event` / `Room:Event` | Story event. | `SelectEventOption` (param: `optionIndex`), sometimes `ChooseACard`, `Proceed` |
| `Room:Shop` | Merchant. | `Purchase`, `PurchaseCardRemoval`, `LeaveShop` |
| `Rest` / rest-site screen | Campfire. | `SelectRestOption` (param: `optionIndex`) |
| `CardGridSelection` | Card-remove / upgrade / transform grid. | `SelectCardsInGrid` (param: `cardIndices`) |
| `HandSelect` sub-screen (`handSelect.active=true`) | E.g. Snap, Retain chooser. | `HandSelectCard` (`handIndex`), `HandConfirmSelect`, `HandCancelSelect` |
| `Chest` / treasure room | Chest unopened or relic picker. | `OpenChest`, `SelectTreasureRelic` |
| `GameOver` | Dead. Bridge auto-dismisses ~3 s. | Log and halt; do not command. |

Any screen name not in this table = **surface in the session log** as
`UNKNOWN_SCREEN=<name>`. That's exactly the kind of stability finding
we want.

## Command reference (minimum viable set)

Authoritative list: `HermesBridgeCode/BridgeCommandDispatcher.cs:47-80`.
This is what you'll need to finish a run.

| Command | Required params | Notes |
|---|---|---|
| `StartRun` | `character` (bare id) | e.g. `{"type":"StartRun","character":"NECROBINDER"}` |
| `ContinueRun` | — | Only from MainMenu. Save may be slightly stale. |
| `AbandonRun` | — | Use as stuck-recovery last resort. |
| `ReturnToMenu` | — | From GameOver (usually auto). |
| `PlayCard` | `handIndex`; `targetIndex` if card targets an enemy | OMIT `targetIndex` on self/untargeted. |
| `EndTurn` | — | Optionally `expectedRevision`/`expectedScreen`/`expectedCurrentSide`/`expectedRoundNumber` for guarded replay safety. |
| `UsePotion` | `slotIndex`; `targetIndex` for attack potions | `targetSelf:true` or omit for self. |
| `DiscardPotion` | `slotIndex` | |
| `SelectReward` | `rewardIndex` (= `rewards[i].index`) | |
| `SkipReward` | `rewardIndex` | |
| `SkipAllRewards` | — | |
| `SelectCardOption` | `cardIndex` (0/1/2 in overlay) | Param name is `cardIndex`, NOT `index`. |
| `SelectMapNode` | `col`, `row` | Must be in `state.map.available[]`. |
| `Proceed` | — | Safe escape from rewards, events, stuck CardGridSelection. |
| `SelectEventOption` | `optionIndex` | NOT `index`. |
| `SelectRestOption` | `optionIndex` | |
| `SelectCardsInGrid` | `cardIndices` (array of ints) | NOT `indices`. |
| `ChooseACard` | `cardIndex` | |
| `HandSelectCard` | `handIndex` | Active when `handSelect.active=true`. |
| `HandConfirmSelect` / `HandCancelSelect` | — | |
| `Purchase` | `category` ("character"/"colorless"/"potion"/"relic"), `index` | |
| `PurchaseCardRemoval` | — | |
| `LeaveShop` | — | |
| `OpenChest` | — | |
| `SelectTreasureRelic` | `index` (0..N-1 among relicChoices) | |

When in doubt: **grep the dispatcher for the command name** and read
the `Dispatch<Type>` method. Parameter names there are authoritative.

## State-payload quick reference

Only the fields you actually need for play-through stability. Full
schema is in `BridgeStateExtractor.cs`. `docs/bridge-protocol-notes.md`
§State payload shape cheatsheet is the shortest canonical ref.

- `revision` — int, monotonic. Use to detect fresh writes.
- `screen.name` — see §Screen catalog.
- `run.currentHp`, `run.maxHp`, `run.gold`, `run.totalFloor`, `run.actFloor`.
- `run.potions[]` — positional, with nulls; see known-bug W.
- `combat.hand.cards[]` — wrapper shape; iterate `.cards[]`.
- `combat.hand.cards[i].isPlayable` — trust this for legal-move filter
  (exception: curses/statuses can lie; see bugs §Known bugs in
  protocol notes).
- `combat.hand.cards[i].targetType` — `NoTarget` / `Self` / `AnyEnemy` /
  `AllEnemies` / etc. `AnyEnemy` needs `targetIndex`.
- `combat.enemies[]` — has `combatId`, `currentHp`, `maxHp`, `block`,
  `intents[]`, `powers[]`. **No `index` field** — use array position.
- `combat.enemies[i].intents[].label` / `.description` — human-readable
  telegraphed action including block/buff amounts (added 2026-04-20,
  see INTENTBLOCK in session-bugs).
- `combat.energy`, `combat.maxEnergy`.
- `rewards[]` — entries with `kind`, `index`, `canSkip`, `canReroll`,
  etc. `index` is what goes into `rewardIndex`.
- `map.available[]` — only nodes in this list are travelable. Use
  their `col`/`row`/`pointType` for `SelectMapNode`.
- `handSelect.active` — if `true`, you are in a sub-screen; only
  `HandSelectCard` / `HandConfirmSelect` / `HandCancelSelect` are legal.
- `cardGrid.active` — if `true`, use `SelectCardsInGrid` or `Proceed`
  to escape. Note: `cardGrid.cards[i].card.title` (nested under `.card`),
  same for `cardRewardOptions.cards`.

## Stall & stuck recovery

Defensive defaults:

- **Stall (no revision change for 30 s after command)** → log
  `STALL screen=<name> lastCmd=<type> id=<N>`, dump `trace.log` last
  200 lines into session log, halt.
- **Screen unchanged after reasonable command** (e.g. `SelectMapNode`
  returned `ok` but screen stays `Map` for 30 s) → treat as stall.
- **`CardGridSelection` with empty options or unclear prompt** →
  `Proceed` to escape; log `STUCK_GRID_ESCAPED`.
- **Unknown screen** → log `UNKNOWN_SCREEN=<name>`, try `Proceed`;
  if that errors, halt.
- **GameOver** → log `DEATH floor=<N>` with summary, halt.
- **5 consecutive `status=error` on distinct commands** → halt.
- **BRIDGE_STALL (handSelect trap)** → if commands return `ok=true`
  but revision doesn't advance, check `handSelect.active`. If true,
  issue `HandSelectCard handIndex=0` (or a better choice informed by
  `handSelect.prompt`) to unstick.

## Session log format

Write to `docs/autopilot-session-<YYYY-MM-DD>.md`. Append one markdown
section per run. Example:

```markdown
## Run 2026-04-20T14:32Z — NECROBINDER

- Start: floor 0, hp 66/66.
- End: DEATH floor 11 (elite), last screen GameOver, last cmd id 284.
- Duration: ~18 min, 284 commands, 0 IPC errors.

### Stability findings
- UNKNOWN_SCREEN=WildflowerBloom at cmd 142 — not in SKILL.md catalog.
  Issued Proceed, recovered to Event. trace.log excerpt:
  ```
  ...
  ```
- STALL at cmd 201 screen=Combat after EndTurn. Revision stuck at
  18923 for 34 s. trace.log tail shows:
  ```
  ...
  ```
  Issued `Proceed`; screen advanced. Logged but did not halt.

### Notes for maintainers
- None / or: "CardGridSelection on Floor 7 (event=...) accepted
  cardIndices=[0] but deck unchanged post-command."
```

## Helper library — `autopilot-lib.ps1`

`autopilot-lib.ps1` at the repo root is a **pure helper library**, not a
runner. It contains no decision logic, no loops, and no screen dispatch.
**You** are the decision-maker, tick by tick, using your own tool calls.

Dot-source it once at the start of each shell tool invocation and use the
primitives it exposes:

| Primitive | Purpose |
|---|---|
| `Read-State` | Parse `state.json` with retry. Returns the state object (pscustomobject). |
| `Wait-Revision -AfterRevision N [-TimeoutSec 30]` | Block until `state.revision > N`, or timeout. Returns fresh state or `$null`. |
| `Send-BridgeCommand @{...}` | Write command, await result, await revision bump. Returns `{result, state, ok, stalled, id}`. Auto-logs `IPC_TIMEOUT`, `CMD_ERROR`, `STALL` findings. |
| `Clear-Ipc` | Delete stale `commands.json` / `result.json`. Called internally by `Send-BridgeCommand`. |
| `Log-Finding "msg"` | Record a stability finding for the session log. |
| `Get-Findings` | Return current list of findings. |
| `Write-SessionLog -Character X -HaltReason Y -FinalState $s` | Append a per-run section to `docs/autopilot-session-<date>.md`, including trace.log tail. |
| `Reset-Session [-StartingId N]` | Reset counters between runs. Uses `Max(requestedId, currentNextId)` — safe. |
| `Get-IpcPaths` | Inspect the paths the library is using. |

### The shape of a correct tool call

Each of your tool calls should be small. A good one looks like this
(one command, one `Read-State`, no loops):

```
. E:\Games\sts2\HermesBridge-StS2\autopilot-lib.ps1
$r = Send-BridgeCommand @{ type='PlayCard'; handIndex=2; targetIndex=0 }
$r.state | ConvertTo-Json -Depth 6 -Compress
```

Then you read the JSON yourself, decide the next move, and make another
tool call with the next single command.

### The shape of a WRONG tool call

The following is what prior agents have done and what you must **not**
do. Loops and policy live in the agent (you), not in PowerShell.

```
# DO NOT WRITE THIS
. .\autopilot-lib.ps1
while ($true) {
    $s = Read-State
    if ($s.screen.name -ne 'Combat') { break }
    foreach ($card in ($s.combat.hand.cards | Sort-Object effectiveEnergyCost)) {
        if (-not $card.isPlayable) { continue }
        Send-BridgeCommand @{ type='PlayCard'; handIndex=$card.handIndex }
    }
    Send-BridgeCommand @{ type='EndTurn' }
}
```

If you are thinking "I'll just write one small helper loop so I don't
have to prompt-myself for every card" — **stop**. That is the failure
mode. Your prompt-yourself IS the loop. The cost is token-weight; the
benefit is coverage, and coverage is the entire point of this task.

### First-turn bootstrap (example tool calls, not a script)

A concrete walkthrough of the first few of **your** tool calls:

1. Shell tool: `. .\autopilot-lib.ps1; Clear-Ipc; Reset-Session; (Read-State) | ConvertTo-Json -Depth 4 -Compress`
   → You read the JSON; confirm `screen.name=MainMenu`.
2. Shell tool: `. .\autopilot-lib.ps1; $r = Send-BridgeCommand @{type='StartRun';character='NECROBINDER'}; $r.state | ConvertTo-Json -Depth 6 -Compress`
   → You read the JSON; confirm `screen.name=Event` (Neow), look at `event.options[]`, pick one based on what's offered, not a hardcoded index.
3. Shell tool: `. .\autopilot-lib.ps1; $r = Send-BridgeCommand @{type='SelectEventOption';optionIndex=<n>}; $r.state | ConvertTo-Json -Depth 6 -Compress`
   → You read the JSON; if `cardGrid.active=true`, inspect the grid and choose indices based on **the actual cards shown**.
4. …and so on.

Each step is one tool call. You own the decisions between them.

## Ready-check before starting

1. Game not running: `Get-Process -Name 'SlayTheSpire2' -EA SilentlyContinue` returns nothing (unless you're resuming an existing session).
2. Bridge build is current: `HermesBridge.csproj` built without errors.
3. IPC dir exists and is writable.
4. `docs/autopilot-session-<today>.md` does not exist (or you're appending intentionally).

Launch:

```powershell
Start-Process 'steam://rungameid/2868840'
Start-Sleep -Seconds 18
```

Then issue your first `Read-State` tool call and begin driving.

## What NOT to do (summary)

- **Do not edit `HermesBridgeCode/`.** You are not fixing bugs.
- **Do not add new commands to the dispatcher.** If you need one, log
  it as a stability finding.
- **Do not auto-restart on death.** User wants to inspect each halt.
- **Do not clear `trace.log`.** It's valuable post-mortem data.
- **Do not modify `docs/session-bugs-*.md`** — that's a separate,
  human-curated workflow.
- **Do not commit** unless explicitly told to.
- **Do not write new `.ps1` files.** Use `autopilot-lib.ps1` as-is.
- **Do not put game-logic loops inside a single pwsh tool call.**
  One tool call, one command (roughly). You are the loop.
