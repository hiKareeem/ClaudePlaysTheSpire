# docs/data/eng/ — Mechanical Ground Truth

Structured JSON for every game entity, vendored from [spire-codex](https://github.com/ptrlrd/spire-codex). See `ATTRIBUTION.md` for provenance.

**Game version:** 0.104.0 (2026-04-23). Sourced from `data-beta/v0.104.0/eng/`. Patch summary in [`changelogs/0.104.0.json`](changelogs/0.104.0.json).

## When to read JSON vs. markdown

- **JSON (here)** — authoritative stats: costs, damage, HP, intents, scaling, exact descriptions, upgrade deltas, keywords, powers applied. Machine-parseable.
- **Markdown (`../cards-*.md`, `../relics.md`, `../potions.md`, `../buffs.md`, `../debuffs.md`)** — strategy, play tips, synergies, agent heuristics. Curated.

Agents should treat JSON as truth for numbers and markdown as advice for decisions.

## Files

| File | Contents |
|---|---|
| `cards.json` | All cards: cost, type, rarity, damage, block, vars, upgrade deltas, powers applied, description_raw + resolved description |
| `relics.json` | Relics with rarity, pool, resolved descriptions |
| `potions.json` | Potions with rarity, effects |
| `powers.json` | Buffs/debuffs: PowerType, StackType, DynamicVars, descriptions |
| `monsters.json` | HP ranges, ascension scaling, move state machines, intents, attack patterns, innate powers |
| `encounters.json` | Monster compositions per room/act |
| `events.json` | Event decision trees with choices, outcomes, preconditions |
| `acts.json` | Act layout: boss order, room counts, encounter/event/ancient pools |
| `characters.json` | Starting HP/gold/energy, starting deck, starting relics |
| `enchantments.json` | Enchantment restrictions, stackability, scaling |
| `potions.json` / pools | Per-character potion pools |
| `achievements.json` | Unlock conditions + thresholds |
| `ascensions.json` | A0–A10 descriptions |
| `afflictions.json` | Run-modifying conditions |
| `modifiers.json` | Run modifier descriptions |
| `badges.json` | UI badges |
| `epochs.json` | Timeline/unlock progression |
| `stories.json` | Story/narrative unlocks |
| `orbs.json` | Passive/evoke values |
| `keywords.json` | Card keyword definitions (Exhaust, Ethereal, Innate, …) |
| `intents.json` | Monster intent icons/descriptions |
| `glossary.json` | Term definitions |
| `translations.json` | UI strings + filter maps |

## Schema notes

- Cards use `description_raw` with SmartFormat template vars like `{DexterityPower:diff()}` alongside resolved `description`. `vars{}` holds current values, `upgrade{}` holds deltas, `upgrade_description` holds the resolved upgraded text.
- Powers use `type: "Buff" | "Debuff"` (not `PowerType`). 207 buffs / 51 debuffs in v0.104.0.
- Monster `moves` include intent, damage, hit count, powers applied (target + amount), and ascension variants.
- Events preserve `preconditions` (gold/HP/act/deck/relic/potion conditions) and runtime-computed values.
- Field naming is `snake_case` throughout (e.g. `description_raw`, `power_type`, `hit_count`).
