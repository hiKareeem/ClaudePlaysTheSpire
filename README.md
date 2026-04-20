# ClaudePlaysTheSpire / HermesBridge

HermesBridge is a [Slay the Spire 2](https://store.steampowered.com/app/2868840/)
mod that exposes the running game's state over a file-based IPC protocol and
accepts structured commands back. It is designed to let an external process —
typically an LLM agent — observe and drive the game autonomously.

The repo ships two things:

- **The mod** (`HermesBridge.dll`) — drop-in under `%APPDATA%\SlayTheSpire2\mods\`.
- **The autopilot SKILL** (`SKILL.md`) — an instruction document you give your
  coding agent so it can drive the game via the bridge without writing
  wrapper scripts that fight the stateful nature of the game.

A full autonomous Act 1 run driven by Claude Opus 4.7 via this setup is
documented in [`docs/example-run-necrobinder.md`](docs/example-run-necrobinder.md).

> **Status:** v0.1.0, experimental. The bridge has been validated end-to-end
> for Necrobinder. Other characters likely work but are less exercised.
> The autopilot SKILL has only been validated on frontier-tier coding models
> (Claude Opus 4.7-class); smaller models have reliably failed the same way.

## How it works

The mod writes `state.json` — a structured snapshot of the current run, combat,
hand, map, rewards, screens, etc. — to a known IPC directory after every
meaningful state change. An external driver writes `commands.json` with a
monotonic id and a typed command (`PlayCard`, `EndTurn`, `SelectMapNode`,
`SelectEventOption`, `Purchase`, etc.); the mod dispatches the command and
writes a `result.json` response plus an updated `state.json`.

```
  %APPDATA%\SlayTheSpire2\hermesbridge\
    state.json      <- written by the mod (game state)
    commands.json   <- written by you (one command at a time)
    result.json     <- written by the mod (ok/error + diagnostic)
    trace.log       <- append-only bridge log
```

The full protocol is in [`docs/bridge-protocol-notes.md`](docs/bridge-protocol-notes.md).
The authoritative command list is in
[`HermesBridgeCode/BridgeCommandDispatcher.cs`](HermesBridgeCode/BridgeCommandDispatcher.cs)
(grep for `case "..."`).

## Install (users)

1. Install **[BaseLib](https://github.com/Alchyr/BaseLib-StS2)** — the StS2 mod
   loader. HermesBridge depends on it.
2. Download the latest `HermesBridge-vX.Y.Z.zip` from [Releases](../../releases).
3. Extract so you end up with a `HermesBridge` folder inside the game's
   `mods/` directory:

   ```
   <Steam>\steamapps\common\Slay the Spire 2\mods\HermesBridge\
       HermesBridge.dll
       HermesBridge.json
   ```

   (On Linux: `~/.local/share/Steam/steamapps/common/Slay the Spire 2/mods/`.
   On macOS: inside `SlayTheSpire2.app/Contents/MacOS/mods/`.)

4. Launch the game. Check `%APPDATA%\SlayTheSpire2\logs\godot.log` for
   `HermesBridge loaded` and `--- RUNNING MODDED! ---`.
5. Confirm `%APPDATA%\SlayTheSpire2\hermesbridge\state.json` appears once
   you reach the main menu.

## Using the autopilot

The bridge is driver-agnostic: anything that can read and write JSON files
on a local path can use it. The repo ships a PowerShell helper library and
an agent SKILL for the specific case of "a coding agent drives the game."

### PowerShell helper

[`autopilot-lib.ps1`](autopilot-lib.ps1) at the repo root is a thin library
of IPC primitives: `Read-State`, `Send-BridgeCommand`, `Wait-Revision`,
`Clear-Ipc`, `Reset-Session`, and logging helpers. It contains no decision
logic, no loops, and no game policy. It is meant to be dot-sourced by the
driver (human or agent) one call at a time.

```powershell
. E:\path\to\HermesBridge-StS2\autopilot-lib.ps1
Clear-Ipc
Reset-Session
(Read-State) | ConvertTo-Json -Depth 6 -Compress
# inspect, decide...
Send-BridgeCommand @{ type = 'StartRun'; character = 'NECROBINDER' }
```

### Agent SKILL

[`SKILL.md`](SKILL.md) is the document to hand to a coding agent (Claude Code,
Cursor, OpenCode, etc.) so it can drive the game autonomously. It spells out:

- The IPC contract and screen catalog.
- The authoritative command reference.
- Known refresh lags, state staleness, sub-screen traps.
- The **per-tick driving rule** and why wrapping the driver in a
  PowerShell loop is the reliable failure mode.

Point your agent at `SKILL.md` as a skill / instruction file. Typical flow:

```
You: "Load the skill at E:\...\HermesBridge-StS2\SKILL.md and do a
      Necrobinder run for stability testing."
Agent: <reads SKILL.md, launches game, drives the run tool-call-by-tool-call>
```

See [`docs/example-run-necrobinder.md`](docs/example-run-necrobinder.md) for
a transcript of what that looks like on a successful Act 1.

## Build from source

Requirements:

- **.NET 9 SDK**.
- **Godot 4.5** with Mono (only required if you want to pack `.pck` assets;
  not needed for the DLL-only build this mod currently ships).
- Slay the Spire 2 installed locally. The build scripts auto-discover the
  install via [`Sts2PathDiscovery.props`](Sts2PathDiscovery.props).

Build:

```powershell
# Stop the game first; the build copies the DLL into the mods folder.
Get-Process -Name 'SlayTheSpire2' -EA SilentlyContinue | Stop-Process -Force

dotnet build .\HermesBridge.csproj -c Release
```

The `AfterTargets="PostBuildEvent"` copy step drops
`HermesBridge.dll` + `HermesBridge.json` into
`<Sts2Path>\mods\HermesBridge\` automatically.

To produce a release zip:

```powershell
$mods = "E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\HermesBridge"
Compress-Archive -Path $mods -DestinationPath .\HermesBridge-v0.1.0.zip -Force
```

## Repo layout

| Path | Purpose |
|---|---|
| `HermesBridgeCode/` | Mod source. `BridgeCommandDispatcher.cs` and `BridgeStateExtractor.cs` are the authoritative protocol surface. |
| `HermesBridge/` | Mod assets folder (mostly empty; reserved for localization / future content). |
| `HermesBridge.csproj` | Build definition. Targets `Godot.NET.Sdk` + BaseLib. |
| `HermesBridge.json` | Mod manifest read by BaseLib. |
| `Sts2PathDiscovery.props` | Auto-detects the Steam install of StS2 for building. |
| `autopilot-lib.ps1` | IPC helper library (PowerShell). |
| `SKILL.md` | Agent instruction document for autonomous play. |
| `docs/` | Protocol notes, runbook, example run, card/relic/potion references. |
| `tests/` | Small test project. |
| `reflect_meta/` | Reflection-based helper project for schema exploration. |

## Acknowledgements

- [Alchyr](https://github.com/Alchyr) for **BaseLib** and the StS2 mod
  template that this project is built on.
- Mega Crit for Slay the Spire 2.

## License

MIT. See [LICENSE](LICENSE).
