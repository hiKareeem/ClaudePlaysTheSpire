# Changelog

## v0.1.5 — 2026-04-27

SpireBench trial-v0 prep follow-up: deterministic per-run isolation for `floor-history.jsonl`.

### Bridge

- **Floor-history file is auto-truncated on game startup.** `BridgeFloorHistory`'s static constructor clears `%APPDATA%/SlayTheSpire2/hermesbridge/floor-history.jsonl` exactly once per process. Game restart between runs (already in protocol.md §Per-run setup) is now sufficient to guarantee a fresh file — operator no longer has to remember to delete the runtime file in the teardown step. Truncation failures are trapped and traced; they never block append. The teardown step in `agent-prompt.md` is reworded to "snapshot before next launch" rather than "snapshot then delete."

### SpireBench

- **trial-v0.2 / v0.3 / v0.4 amendments.** See `docs/benchmark/protocol.md` §Amendments. v0.2: per-run `opencode.benchmark.json` sandbox config formalized. v0.3: tools/maintainer split — runs.csv schema bumped to 34 fields with token / wall / cost / step_finish / command_count columns; `tools/maintainer/append-run-csv.ps1` helper merges run-record YAML frontmatter with OpenCode session DB token data and appends a typed CSV row. v0.4: `rate_limit_pause` formalized as non-terminal note (provider-side token pause; agent resumes on its own); distinct from `stall` (terminal halt). The `agent-prompt.md` teardown obligation is unchanged.
- **`tools/maintainer/append-run-csv.ps1`** — frontmatter→runs.csv append helper. Validates `run_id` matches, refuses duplicate appends, force-quotes `seed` to avoid scientific-notation rounding, calls `get-session-tokens.ps1` for any null token / wall / cost / step_finish fields, optionally patches the .md frontmatter back so `.md` and `.csv` stay in lockstep.
- **First 9 trial-v0 runs archived.** `docs/benchmark/runs/` holds run01-run08 (glm-5.1, gemini-3.5-pro, qwen-3.5-coder, kimi-k2.5) plus run11 (gpt-5.5 ironclad). 5-model lineup: glm-5.1, gemini-3.5-pro, qwen-3.5-coder, kimi-k2.5, gpt-5.5; claude-opus-4.7, deepseek-v3.5 reserved for later. Initial chart pass at `docs/benchmark/charts/`.

### Tooling

- **Maintainer tools split.** `tools/append-run-csv.ps1` and `tools/get-session-tokens.ps1` moved to `tools/maintainer/`. The `tools/` glob in the release bundler picks up the agent-facing inspector scripts only; maintainer tools stay in the repo for benchmark operators but don't ship in the public zip.
- **`.run` mining + retro-enrichment** (post-release; doesn't affect shipped DLL). `tools/maintainer/parse-run-history.py` matches each SpireBench run record to its StS2 save-game journal at `%APPDATA%\SlayTheSpire2\steam\<steam_id>\profile1\saves\history\<unix>.run` (UTF-8 pretty JSON, ~10–90 KB per run) by `seed` (or `start_time + character` fallback), archives the raw `.run` next to the `.md` / `.jsonl` as `docs/benchmark/runs/<run_id>.run`, and patches 21 derived stat fields into the run record's frontmatter (act_reached, total_floors, total_card_picks, total_card_skips, total_relics_picked, total_potions_used / bought, total_damage_taken, total_gold_*, total_hp_healed, elites_fought, rests_taken, shops_visited, events_visited, rest_choice_heal / smith, killed_by, was_abandoned, run_time_seconds). Backfills truth that agents can't easily self-report (full pick / skip history, rest-site choices, gold flow, killed_by_event). All 9 trial-v0 runs enriched. Run06 + run08 had `seed: null` originally (gemini didn't surface seed in self-reports); the parser injected the real seeds via the start_time fallback. `tools/maintainer/regenerate-runs-csv.py` rebuilds `runs.csv` from frontmatter so the CSV stays in lockstep with the extended schema (35 → 55 columns).

### Packaging

- Manifest bumped to `v0.1.5`.
- Release zip now includes the full `docs/benchmark/` set: `protocol.md`, `agent-prompt.md`, `run-record-template.md`, `trial-v0-summary.md`, `runs.csv`, `opencode.benchmark.json`, and `charts/`. Drops `docs/benchmark/runs/*` (per-run maintainer records — `runs.csv` is the public summary) and `docs/benchmark/blog-draft.md` (in-progress write-up).

## v0.1.4 — 2026-04-27

SpireBench trial-v0 prep: per-floor metric capture + trial-v0.1 amendment.

### Bridge

- **`floor-history.jsonl` writer.** `BridgeStateExtractor.ExtractRun` now appends one JSONL row to `%APPDATA%/SlayTheSpire2/hermesbridge/floor-history.jsonl` whenever `state.TotalFloor` advances. New `BridgeFloorHistory` class holds the static cursor and serializes the row by hand (no JSON dep needed for one line). Schema: `{t, floor, act, hp, maxHp, gold, deckSize, relicCount, potionCount, roomType}`. Idempotent within a floor (repeated extracts at the same floor are no-ops). New `BridgePaths.FloorHistoryPath` constant. Append failures are trapped and traced — never break state extraction. Used by SpireBench post-trial analysis for HP/gold/deck curves.

### SpireBench

- **trial-v0.1 amendment.** See `docs/benchmark/protocol.md` §Amendments for the full record. Three changes: (1) bridge bumped to v0.1.4 for per-floor capture; (2) `agent-prompt.md` hardened — run record is now a terminating obligation with explicit completion checklist, halt-without-record is a benchmark failure; (3) `docs/gauntlet-findings.md` removed from the repo for trial-v0 duration (recoverable from git commit `9cc1dc4`; will be restored after trial-v0 completes). Pre-amendment runs (run01-run02 from 2026-04-26) discarded; trial-v0 restarts from run01 against v0.1.4.

## v0.1.3 — 2026-04-26

StS2 v0.104.0 compat patch + game-data refresh + SpireBench scaffolding.

### Bridge fixes

- **StS2 v0.104.0 compatibility.** Game v0.104.0 broke `Creature.CombatState` access (returned `null`/threw). Added `BridgeStateExtractor.SafeGetCombatState(room, player)` which prefers `room?.CombatState` and swallows `MissingMethodException` / `NullReferenceException` from the legacy `player?.Creature?.CombatState` fallback. All three call sites in `BridgeCommandDispatcher` (EndTurn guard, PlayCard `targetIndex` resolution, UsePotion `targetIndex` resolution) and `BuildCombatState` in the extractor route through it. Smoke-tested end-to-end: StartRun → Map → Combat → PlayCard → EndTurn against retail v0.104.0.

### Reference

- **Game data refreshed for v0.104.0.** Re-vendored `docs/data/eng/` from spire-codex upstream against game v0.104.0. 308 changed entries vs v0.103.2; reworks include Conflagration, Drum of Battle, Parry/Sovereign Blade. New badges and ancient buff scaling. Buff count 206 → 207. Per-patch diff archived at `docs/data/changelogs/0.104.0.json` for forward-looking agents.
- **Mega Crit non-objection on record.** New `docs/data/megacrit-statement.md` archives an on-record statement from Casey Yano (co-founder, Mega Crit Games) on the `ClaudePlaysTheSpire` Twitch channel (2026-04-26): "I can't deny robots from playing the game." `docs/data/ATTRIBUTION.md` adds a non-objection paragraph. This is a non-objection, not a license, partnership, or endorsement.

### Agent guidance

- **AGENTS.md hardened.** `docs/data/README.md` is now required reading step 4 ahead of `verified-flows/` and `gauntlet-findings.md`. New explicit rule: always check JSON before quoting a number; field names are `snake_case` (`description_raw`, `hit_count`, `power_type`); powers use `type: "Buff" | "Debuff"`. When curated markdown predates the current changelog entry, JSON wins on stats and the markdown is advisory only.
- **SKILL.md choice-screen potion flow corrected.** Replaced the misleading "Skill Potion silently adds a random Skill card" line with the real flow: choice-screen potions (Skill / Fire / Power / Colorless / Orobic Acid) open `chooseACardScreen`, follow up with `ChooseACard cardIndex=N`. Occasional wedge requires `DiscardPotion`. Pre-flight check `state.chooseACardScreen.active`. Expanded `Purchase` category list to include all real type strings the dispatcher accepts.
- **bridge-protocol-notes.md** new bullets in §Refresh-lag quirks: choice-screen wedge detection, stale `state.combat` after `StartRun`, mid-combat upgrade numeric-field lag (trust `currentUpgradeLevel`/`isUpgraded` over `damage`/`block`). §Combat lifecycle: gate `EndTurn` on `combat != null && screen.name == "Combat"`.
- **gauntlet-findings.md frozen as off-limits archive.** The file is now off-limits to trial agents and is no longer shipped in the release zip. The accumulated runbook is the dependent variable in any benchmark trial; including it under required reading contaminates zero-shot conditions. Trial-v0 agents are pointed at `SKILL.md`, `bridge-protocol-notes.md`, and the SpireBench protocol's allowed-reading list instead.

### SpireBench (new)

- **`docs/benchmark/protocol.md`** — full benchmark spec for autonomous LLM agents playing unmodified retail StS2 via HermesBridge. Trial-v0 measures the **A0-zero-shot** knowledge condition: each run is a fresh OpenCode session, no MemPalace, no sub-agent spawning, no web search, no training-recall coaching. Strategy and accumulated learning are off-limits to the agent. Covers: agent contract, trial parameters table, run-record YAML schema (with `tokens_in/out/cache_read/cache_write` and `opencode_session_id`), halt-reason enum (`death | victory | runcap | error_streak | stall | rate_limit | manual`), operator responsibilities pre/during/post-run, forbidden operator actions, allowed/disallowed reading and tool whitelists.
- **`docs/benchmark/opencode.benchmark.json`** — reference sandbox `opencode.json` for trial-v0 runs. Strips all MCP servers (mempalace, context7, sequential-thinking, vscode-mcp, VibeUE), denies `webfetch` and `task` (sub-agents). Operator copies this over the normal config (with backup) for benchmark sessions.
- **`docs/benchmark/runs.csv`** — 29-column run-record CSV header (one row per completed run).
- **`tools/maintainer/get-session-tokens.ps1`** — aggregates `step-finish` token counts from OpenCode's local SQLite session DB into 4 YAML-ready lines for the run record. Tries `System.Data.SQLite` first, falls back to `sqlite3.exe` on PATH.

### Tooling

- **Tooling triage.** Lands the dump-state / dump-hand / dump-map state-inspection scripts agents have been using during gauntlets. `run-cmd.ps1` now uses `$PSScriptRoot` instead of a hardcoded absolute path (works for anyone cloning). Removed stale `dump-combat.ps1` (used the obsolete state shape — `state.combat.hand` instead of `hand.cards`, `enemy.hp` instead of `currentHp`). `__pycache__/` and `*.pyc` gitignored.

### Packaging

- Manifest bumped to `v0.1.3`.
- Release zip adds `docs/data/changelogs/`, `docs/data/megacrit-statement.md`, `docs/benchmark/`, and `tools/maintainer/get-session-tokens.ps1`. Drops `docs/gauntlet-findings.md` and `docs/autopilot-session-*.md` (off-limits / maintainer-only).

## v0.1.2 — 2026-04-23

Documentation and reference-material release. No code changes.

### Reference

- **Mechanical ground truth from spire-codex.** Vendored a snapshot of [ptrlrd/spire-codex](https://github.com/ptrlrd/spire-codex) English JSON data (commit `85f852e`) under `docs/data/eng/` — 22 files covering cards, relics, potions, powers, monsters, events, encounters, acts, characters, enchantments, achievements, ascensions, keywords, and more. Sourced from decompiled `sts2.dll` rather than wiki edits; authoritative for costs, damage, HP, intents, scaling, upgrade deltas, and resolved descriptions. See `docs/data/ATTRIBUTION.md` and `docs/data/README.md`.
- **Wiki-scraped stat dumps retired.** `cards-*.md`, `relics.md`, `potions.md`, `buffs.md`, `debuffs.md` replaced with stub files pointing at the JSON for stats and at `reference-*.md` for strategy. Net: -1,482 markdown lines, +complete JSON coverage.
- **Navigation rewritten.** `docs/cards-index.md` now the reference hub with the `numbers → JSON, decisions → markdown` rule.
- **AGENTS.md updated** to list `docs/data/eng/*.json` as the first preferred source for mechanical stats.

### Protocol

- **Session-log workflow clarified.** `docs/next-agent-prompt.md` now requires three artifacts per run: live `autopilot-session-<YYYY-MM-DD>.md` (via `Write-SessionLog`), `gauntlet-findings.md` summary between character swaps, and a `verified-flows/<date>-<slug>/` subdir for reproducible bug/fix evidence. Prompted by the 2026-04-22 Twitch gauntlet where Rw7/Ev1 verification was captured in gauntlet-findings only, skipping the live log and per-flow evidence trail.
- **`docs/sessions/` marked legacy.** Added a README explaining it is no longer canonical; corrected stale reward-addressing guidance in `session-ironclad-run2-2026-04-22.md` to reference `rewardPosition` (the v0.1.1 fix) instead of `rewards[i].index`.
- **Rw7/Ev1 v0.1.1 fixes confirmed.** New `docs/verified-flows/2026-04-22-rw7-ev1-confirmed/README.md` records the Twitch gauntlet verification across the 5-character rotation.

### Packaging

- **Manifest version corrected.** `HermesBridge.json` was still at `v0.1.0` after the v0.1.1 release — now bumped to `v0.1.2`.
- **Release zip bundler (`README.md`)** now also ships `docs/data/` (attribution, README, 22 JSON files).

## v0.1.1 — 2026-04-22

### Bridge fixes

- **Rw7 — sticky rewards panel closes on last reward.** `DispatchSkipAllRewards` now invokes the rewards-panel close path when the reward list is empty but the panel is still `Visible`. Previously the panel could remain on screen with no entries after skipping the final reward, trapping the agent on `Rewards`. (`HermesBridgeCode/BridgeCommandDispatcher.cs`)
- **Ev1 — event option preview text.** `BridgeStateExtractor.ExtractEventOption` + `BuildEventOptionPreview` now reflect over `EventOption` subclass fields (gold, hpLoss, hpGain, maxHpChange, potion, card outcomes) instead of returning a blank preview. (`HermesBridgeCode/BridgeStateExtractor.cs`)

### Tooling

- **`tools/read-combat.ps1` surfaces potions.** Previously the curated combat view omitted `run.potions` entirely, so agents effectively never used potions unless they manually re-ran `read-state.ps1`. Now prints a `POTIONS` section with slot index, title, targetType, and `canUse` flag. Command reminder: `UsePotion { slotIndex, [targetIndex | targetSelf=true] }`.
- **`tools/read-state.ps1` surfaces treasure.** Added a treasure block showing `hasChestBeenOpened`, `isRelicCollectionOpen`, and `relicChoices[].relic.title` keyed by `index`, with command hints for `SelectTreasureRelic {index}`, `OpenChest`, and `Proceed`. Potion line now also shows `targetType` when present. Shop presence gets a pointer to `read-shop.ps1`.
- **New inspectors:** `tools/read-shop.ps1`, `tools/read-treasure.ps1`, `tools/peek-handselect.ps1`.

### Removed

- **Stream overlay feature.** Deleted `Set-OverlayText`, `Clear-Overlay`, `New-OverlaySrt`, `overlay.txt`, `overlay.log`, `tools/set-overlay.ps1`, and associated SKILL/AGENTS/next-agent-prompt sections. In practice agents dropped narration after the first context compression, producing silent VODs while still paying the per-tick token cost on every `Set-OverlayText` call. Removing the surface keeps the distributable clean and stops agents from routing around a broken incentive.

## v0.1.0 — initial release

- File-based IPC (`state.json` / `commands.json` / `result.json` / `trace.log`).
- Authoritative command dispatcher and state extractor.
- PowerShell helper library (`autopilot-lib.ps1`) and agent SKILL (`SKILL.md`).
- Validated end-to-end on Necrobinder Act 1.
