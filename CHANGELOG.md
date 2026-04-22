# Changelog

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
