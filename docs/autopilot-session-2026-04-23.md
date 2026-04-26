# Autopilot Session — 2026-04-23

## Run 14 — Ironclad ✅ ACT 1 WIN, ❌ A2F29 DEATH

| Stat | Value |
|------|-------|
| Character | Ironclad |
| Run # | 14 |
| Ascension | 0 |
| Seed | 15201716916004802547 |
| Death Floor | A2F29 (Decimillipede Elite, 3 segments) |
| Death HP | 41→0 (28 incoming, 24 HP, no block) |
| Relics | Burning Blood, Neow's Talisman, Kusarigama, Empty Cage, Pantograph |

### Highlights
- **First Act 1 boss win** in gauntlet (Ceremonial Beast 252HP)
- Neow: Talisman (upgrade Strike+Defend)
- Key cards: PS+, Bludgeon+, Demon Form+, Dominate+, Body Slam+, Bully, Spite++
- Blessing of Forge potion upgraded many cards to ++
- Amalgamator event: combined 2 Strikes → Ultimate Strike
- Empty Cage removed Poor Sleep + Taunt (delayed but confirmed)
- Death: Decimillipede Elite 3 segments (Reattach revive), too much incoming damage

### Key New Findings
- `ChooseACard` command for ChooseACardSelection screen (Skill Potion)
- `Purchase` needs `category` + `index` params
- `SelectTreasureRelic` with `index` param CONFIRMED WORKING
- Empty Cage card removal is delayed — confirmed on next state transition
- Act 2 map row-0 off-grid nodes not in `available[]` — must use row/col directly

## Run 15 — Silent ❌ A1F7 DEATH

| Stat | Value |
|------|-------|
| Character | Silent |
| Run # | 15 |
| Ascension | 0 |
| Seed | 15539101412812734214 |
| Death Floor | A1F7 (Twig Slime M, 11 dmg vs 9 HP) |
| Death HP | 9→0 |
| Relics | Ring of the Snake, Golden Pearl |

### Highlights
- Neow: Golden Pearl (+150 gold)
- Key cards: Well-Laid Plans (Retain 1/turn), Skewer (X-attack), Backflip, Anticipate
- F6 4-slime marathon left HP at 8
- Death: 9 HP, Twig M attacking for 11, no block

### Key New Findings
- `AnyEnemy` target cards require `targetIndex` even with 1 enemy
- Skewer uses ALL remaining energy (8 dmg/1E)
- SkipAllRewards skips uncollected gold — collect gold FIRST
- WLP Retain triggers handSelect every EndTurn (SimpleSelect, auto-confirms)
- Slimed status: 1E cost, exhaust, 1 self-damage
- Silent has NO Burning Blood — HP management critical

