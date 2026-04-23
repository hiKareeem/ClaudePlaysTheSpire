# Changelog

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
