# Relic reference

## Purpose
Reference relics by decision impact, not by encyclopedia completeness. Entries marked **[confirmed]** were observed in-run; **[conjecture]** carry StS1 behavior pending verification.

## Per relic template
### Name
### Immediate effect
### What it changes in play
- damage tempo
- block planning
- pathing greed
- rest-site value
- shop value
- elite tolerance

### Synergies
### Anti-synergies
### Early pick / late pick notes
### Common trap interpretation
### Bridge notes
- exact exported name/id if different from UI

## Initial categories
- survivability relics
- damage tempo relics
- scaling relics
- economy relics
- route-shaping relics
- high-variance relics

---

## Observed relics (this session)

### Lead Paperweight
- **Effect**: Neow reward relic taken through live bridge flow; exact mechanical text still needs explicit confirmation.
- **Impact**: immediate early-run relic that should be logged as a live-tested event reward path, not just a static export.
- **Bridge notes**:
  - confirmed in live state under `state.run.relics[]` after `SelectEventOption` + `ChooseACard`
  - useful as a regression anchor for Neow/event reward sequences because it proved event reward acquisition updates the run payload correctly

### Burning Blood (starter)
- **Effect**: Heal 6 HP after each combat. **[confirmed]**
- **Impact**: Sustains greedy pathing through Act 1; pairs with HP-trade cards (Offering/Hemokinesis/Reaper-style) when available.
- **Bridge notes**: Appears in `state.run.relics[]` with `title: "Burning Blood"`. `description` field currently empty in exports — rely on title.

### Heart of Iron
- **Effect**: Grants **Plating 7** at the start of each combat (7 free block per turn). **[confirmed]**
- **Category**: survivability + block planning.
- **Impact**:
  - Every turn starts with 7 free block, dramatically reducing need for Defend in hand.
  - Frees deck slots for damage/scaling.
  - Makes Breakthrough/Rupture self-damage plays safer since incoming pressure is softened.
- **Synergies**: any HP-trade engine (Rupture, Hemokinesis); Dark Embrace / exhaust because less pressure to block.
- **Anti-synergies**: Frail (−25% block applies to Plating too — **needs verification**).
- **Bridge notes**:
  - Plating power does NOT appear in `state.combat.player.powers[]` until the first PlayCard refresh of the turn. Controllers should assume Plating is active even if state.json omits it on turn start.
  - Power id presumed `PLATING_POWER` (matches StS1 naming convention); confirm via trace on next run.

### (relics seen in shop/card grid this run but not taken — names TBD)
- **Letter Opener** — uncommon. "Every time you play 3 Skills in a single turn, deal 5 damage to ALL enemies." **[wiki-confirmed]**
- **Paper Phrog** — uncommon Ironclad-specific. "Enemies with Vulnerable take 75% more damage rather than 50%." **[wiki-confirmed]**
- Arcane Scroll — effect TBD (not found on wiki by that exact name; may be misremembered).

### Regal Pillow (2026-04-19 run)
- **Effect**: Rest action at RestSite heals an additional **+15 HP** (base rest 24 HP  39 HP total). **[confirmed]**
- **Category**: survivability / economy.
- **Impact**: Makes Rest the dominant choice over Smith at most RestSites unless a high-value upgrade is waiting.
- **Bridge notes**: Passive - no power in `combat.player.powers`. Applies at the moment `SelectRestOption optionIndex=<rest>` resolves. Heal amount visible via `run.currentHp` delta.

### Kusarigama (2026-04-19 run)
- **Effect**: When you play an Attack, there is a chance to draw an extra card (observed hand growth mid-turn after Strike plays). Exact proc rule TBD - may be "every 3rd attack" StS1-style or a per-attack chance. **[confirmed it draws; exact trigger TBD]**
- **Category**: damage tempo + card advantage.
- **Impact**: Low-cost Attack spam (Strike, Iron Wave, Pillage) becomes a draw engine. Very good with Ironclad's cheap-attack fillers.
- **Synergies**: Strike-heavy decks; Pummel/Twin Strike style multi-hit.
- **Anti-synergies**: Skill/Power-heavy decks waste the draw.
- **Bridge notes**: Draws manifest as new entries in `combat.hand.cards[]` with new `handIndex`. Watch for handIndex shuffles when planning multi-step plays.

### Phial Holster (2026-04-19 run, Neow pick)
- **Effect**: +2 potion slots (observed: `run.maxPotionCount = 4` instead of default 2). **[confirmed]**
- **Category**: economy + survivability.
- **Impact**: Extra potion storage enables stockpiling for bosses and surviving elites. Especially valuable with cards like Alchemize or enemies that drop extra potions.
- **Bridge notes**: `run.potions[]` is length 4 with `null` entries for empty slots; `slotIndex` is the array position.

### Fairy in a Bottle (event reward, 2026-04-19 run)
- **Effect**: Auto-consumed on fatal damage: heals player to ~30% max HP (observed heal to 25/85 = ~29%). **[confirmed]**
- **Category**: survivability insurance.
- **Impact**: One-time death prevention. Valuable in Act 1 before HP scaling.
- **Bridge notes**: Appears in `run.potions[]` until consumed; disappears on trigger. State revision hooks fire normally around the heal.

## Open questions
- Does Frail reduce Plating block (consistent with Defend −25%)?
- What is the exact Plating block amount from Heart of Iron on higher ascension / after upgrades? (Observed: 7.)
- Boss relic pool for Ironclad in StS2 — unknown.
- Act 1 elite relic pool — unknown (this run's elite-relic reward serialized as `relic: null, rarity: "None"` — known bridge bug).
