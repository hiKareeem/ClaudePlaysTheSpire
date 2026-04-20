# Potion reference

## Purpose
Working reference for potions driven through HermesBridge. Entries marked **[confirmed]** were observed in-run; **[conjecture]** carry StS1 intuition pending verification.

## Bridge mechanics (all potions)
- **Command**: `UsePotion` with `slotIndex` (0-indexed into `state.run.potions[]`).
- **Targeting**:
  - Self-affecting / untargeted potions: either `targetSelf: true` or omit target.
  - Single-target attack potions: `targetIndex` required.
- **Known refresh lag** (see `bridge-protocol-notes.md`):
  - `UsePotion` returns `ok` but no state write fires on its own.
  - `state.run.potions[]` still shows the slot filled until the next PlayCard refresh.
  - Internally the slot IS empty — re-calling `UsePotion` returns `potion slot N is empty`.
  - Hand/deck effects (Bottled Potential shuffle) land internally but state.json stays stale; play by card title match until refresh.

## Observed potions

### Energy Potion
- **Effect**: Gain **+2 energy** this turn. **[confirmed]**
- **Targeting**: self / none.
- **Impact**: Tempo spike for burst turns; pairs with 2-cost bombs (Primal Force, Bash+).
- **When to save**: boss turn 1 or 2 for a setup+burst combo; otherwise use freely on elite spikes.
- **Bridge notes**: `targetType: "None"` in state; `UsePotion slotIndex=N` with no target.

### Heart of Iron (potion)
- **Effect**: Grants **Plating** this combat (stack amount TBD). **[confirmed exists; amount unconfirmed]**
- **Targeting**: self / none.
- **Distinct from Heart of Iron relic** (which grants 7 Plating every turn). The potion is a one-combat version — likely stacks additively with the relic if both are present. **[conjecture]**
- **When to use**: elite/boss combats where incoming damage outpaces available block.

### Bottled Potential
- **Effect**: Discard your hand and draw 5 new cards. **[confirmed]**
- **Targeting**: self / none.
- **Impact**: Mulligan for bad opening hands or dig for a specific card.
- **Bridge refresh bug**: `state.combat.hand.cards[]` still shows pre-shuffle cards after `UsePotion` returns ok. The real hand IS shuffled; `PlayCard handIndex=N` operates on the true hand (trace log confirms via `card=<title>`). Controllers should scan the refreshed hand by title-match on the next PlayCard, or issue a cheap PlayCard first to force `AfterCardPlayed` refresh.

### (other potions seen in shop/rewards but not taken — names TBD)
Fill in as encountered. Log each as:
- name (exact UI title and state.json `title`)
- targetType from state.run.potions[].targetType
- effect
- single-use magnitude

### Fruit Juice (2026-04-19 run)
- **Effect**: Gain **+5 Max HP**. **Applied at the start of the next combat entered** (delayed, NOT instant). **[confirmed - drank Fruit Juice pre-Effigy; MaxHP 80  85 became visible when combat resolved]**
- **Targeting**: self / none.
- **Impact**: Permanent MaxHP boost; best used early (acts 1-2) to compound with rest heals.
- **Bridge notes**: State shows `run.maxHp` updated post-combat, not at `UsePotion` time.

### Fairy in a Bottle (2026-04-19 run)
- Technically a potion in slot inventory. See `reference-relics.md` for effect (auto-heal on fatal damage, ~30% max HP). Consumed automatically; does not require `UsePotion`.

### Speed Potion (2026-04-19 run)
- Observed in inventory, drank but didn't log effect rigorously. StS1 behavior: gain 5 Dexterity this turn (decays at turn end, giving +5 block per Defend). Unverified in StS2. **[conjecture]**

### Touch of Insanity (2026-04-19 run, BUGGED)
- **Effect (wiki)**: Draw 3 cards. The next 3 cards drawn are **Shivs** (or a class-themed equivalent). Mechanism in StS2 TBD. **[conjecture]**
- **Targeting**: self. `targetType: "Self"`.
- **Bridge bug F**: `UsePotion slotIndex=N` silently fails for this potion - returns ok but the potion is not consumed and no state change occurs. Root cause likely: the bridge dispatcher doesn't route Self-with-card-effect potions correctly (requires ChooseACard or post-use grid flow that the dispatcher skips).

## Pickup priorities (tentative)
- **Energy Potion** — always take; trivially useful.
- **Heart of Iron potion** — strong with Burning Blood / Heart of Iron relic.
- **Bottled Potential** — strong mid-fight fix, but refresh bug makes controller logic fragile; acceptable for manual play, caution for scripted play.

## Slot management notes
- Potion slot count starts at 3 (confirmed default).
- `state.run.potions[]` is a sparse array; empty slots serialize as `{}` or null, not omitted.
- When a slot is empty internally but state.json shows full, trust the trace log over state.json until the next PlayCard refresh.

## Open questions
- Heart of Iron potion exact Plating amount.
- Full Act 1 potion drop pool.
- Rare potion pool (boss drops, White Beast statue scaling).
- Sacred Bark / Toy Ornithopter equivalents (potion-scaling relics) — unknown in StS2.
