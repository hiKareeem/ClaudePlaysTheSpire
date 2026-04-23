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

## Licensing Note

Spire Codex does not ship an explicit LICENSE file at the time of this snapshot, but the project advertises itself as an open-source, free API database (per [spire-codex.com/about](https://spire-codex.com/about)). This snapshot is included here under that stated openness, with prominent attribution and a link back to the source. Any party with an objection should open an issue and we will remove or replace.

Game data itself is ultimately owned by Mega Crit Games (Slay the Spire 2, Steam App ID 2868840). This data is included solely as reference material to support modding and accessibility tooling for the game.

## Refresh Procedure

To update the snapshot:

```powershell
git clone --depth 1 https://github.com/ptrlrd/spire-codex.git _tmp\spire-codex
Copy-Item _tmp\spire-codex\data\eng\*.json docs\data\eng\ -Force
git -C _tmp\spire-codex rev-parse HEAD  # update commit SHA above
```

Then update the **Upstream commit** and **Snapshot date** fields in this file and commit.
