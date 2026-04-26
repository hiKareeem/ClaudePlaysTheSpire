# Data Attribution

The JSON files under `docs/data/eng/` are a vendored snapshot of game data from the **Spire Codex** project, used as the mechanical ground truth for HermesBridge agent reference.

## Source

- **Project**: [ptrlrd/spire-codex](https://github.com/ptrlrd/spire-codex)
- **Live site**: [spire-codex.com](https://spire-codex.com)
- **Upstream commit**: `85f852e6bf37aa94f5022dacdf3b8e1fb2bb29b7`
- **Snapshot date**: 2026-04-23
- **Language**: `eng` only (upstream ships 14; we vendor English)

## How It Was Built (upstream)

Spire Codex reverse-engineers Slay the Spire 2 by:

1. Extracting the Godot `.pck` with GDRE Tools
2. Decompiling `sts2.dll` with ILSpy into C# sources
3. Running ~22 Python regex parsers over the decompiled C# to produce per-language JSON
4. Resolving SmartFormat localization templates into human-readable descriptions

See upstream `README.md` for full pipeline detail.

## Licensing

Spire Codex does not ship an explicit LICENSE file. Permission to vendor and redistribute this data within HermesBridge was granted directly by the project author (peter / ptrlrd) on 2026-04-23 via Discord, in response to a project-info request for the Spire Codex showcase page. The project advertises itself as an open-source, free API database (per [spire-codex.com/about](https://spire-codex.com/about)).

Game data itself is ultimately owned by Mega Crit Games (Slay the Spire 2, Steam App ID 2868840). This data is included solely as reference material to support modding and accessibility tooling for the game.

## Mega Crit Non-Objection

On 2026-04-26, Casey Yano (co-founder, Mega Crit Games) publicly stated on the `ClaudePlaysTheSpire` Twitch channel that Mega Crit has no objection to autonomous AI agents playing the retail Slay the Spire 2 client. Verbatim:

> "I can't deny robots from playing the game. It would upset our future AI overlords." — `caseyyano`, 2026-04-26

The full transcript and context are archived in [`megacrit-statement.md`](./megacrit-statement.md). This is a non-objection, not a license, partnership, or endorsement. All Slay the Spire 2 content, code, art, and trademarks remain the property of Mega Crit Games; HermesBridge is an independent third-party tool.

## Refresh Procedure

To update the snapshot:

```powershell
git clone --depth 1 https://github.com/ptrlrd/spire-codex.git _tmp\spire-codex
Copy-Item _tmp\spire-codex\data\eng\*.json docs\data\eng\ -Force
git -C _tmp\spire-codex rev-parse HEAD  # update commit SHA above
```

Then update the **Upstream commit** and **Snapshot date** fields in this file and commit.
