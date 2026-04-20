# Ironclad reference

## Purpose
Working reference for Ironclad runs driven through HermesBridge. Entries marked **[confirmed]** were observed in-run via state.json / trace.log; **[conjecture]** are carried from StS1 intuition and not yet verified in StS2.

## Starting identity
- 80 max HP **[confirmed]**
- Burning Blood: heal +6 HP after combat **[confirmed]** (observed post-combat HP deltas)
- Starter deck: 5 Strike, 4 Defend, 1 Bash, 1 Ascender's Bane **[confirmed]** (deck export at run start)
- 3 energy per turn **[confirmed]**

## Card mechanics (confirmed in-run)

### Attacks
- **Strike** — 6 damage single target; upgrade +3 → 9 dmg. **[confirmed]**
- **Bash** — 8 damage + 2 Vulnerable. Upgrade: 10 damage + 3 Vulnerable. `energyCost` in StS2 state is **2 E** (state.json consistent; earlier session note suggesting 3E was wrong). **[confirmed]**
- **Bash + Sharp enchantment (+2 dmg)** — 10 damage + 2 Vulnerable for 2 E. **[confirmed 2026-04-19 run]**
- **Iron Wave** — 5 dmg + 5 block for 1 E. Iron Wave+: 7 dmg + 7 block. **[confirmed 2026-04-19 run]**
- **Hemokinesis** — 1 E Uncommon Attack. 15 base dmg, lose 2 HP, Exhausts. Observed 22-24 dmg vs Vuln target. Did NOT trigger Feel No Pain's "when you exhaust" rider in this run - possibly FnP triggers only on involuntary exhaust, unverified. **[confirmed dmg; FnP interaction tentative]**
- **Pillage** — 1 E Uncommon Attack. Observed 9 dmg and 6 dmg in different fights; consistent with StS1 rule "9 dmg + steal gold if enemy has block; 6 dmg base otherwise." **[tentative - needs explicit block-on-target A/B test]**
- **Flame Barrier** — 2 E Uncommon Skill. 12 block + 4 thorns this turn. Thorns proc per enemy attack. Does not persist past turn end. **[confirmed 2026-04-19 run]**
- **Crimson Mantle** — 1 E Rare Power. Playing it: adds `POWER:CRIMSON_MANTLE_POWER` stack=8. On each of your turn starts AFTER the first play: lose 1 HP (unblockable), gain 8 block. **Warning: playing this at HP=1 guarantees death next turn start.** **[confirmed - caused run death F14]**
- **Armaments** — 1 E Common Skill. +5 block, upgrades a card in hand for remainder of combat (permanent upgrade if used at a Smith? - StS2 behavior unclear; in combat it's a combat-only upgrade per StS1 rules). **[partially confirmed; persistence TBD]**
- **Shrug It Off** — 1 E Common Skill. 8 block + draw 1. Shrug It Off+: 11 block + draw 1. **[confirmed]**
- **Feel No Pain** — 1 E Uncommon Power. When a card is exhausted, gain N block (2 base, 3 upgraded). See Hemokinesis note above; interaction unclear. **[mechanics from StS1; amounts from state.json card text needed]**
- **Twin Strike** — 5+5 damage to single target (two hits). Each hit is a separate damage instance (relevant for Thorns/Plating interactions). **[confirmed]**
- **Thunderclap** — 4 AoE damage + 1 Vulnerable to all enemies. **[confirmed]** (Vulnerable lands, decays at enemy turn end.)
- **Heavy Blade** — scales with Strength (multiplier unconfirmed in StS2; was x3 / x5 in StS1). **[conjecture]**
- **Bully** — deals damage only if player has no Strength/power debuff on enemy; exact rider unconfirmed in StS2. **[conjecture]**
- **Havoc** — play top card of draw pile, exhaust it. 1 energy. **[confirmed]** (played; random top card consumed.)
- **Anger** — played against Kin Priest twice. Exact damage value and "adds a copy to discard pile" rider still TBD for StS2. **[seen, mechanics TBD]**
- **Offering** — played turn 3 of Kin fight; followed immediately by two Primal Force casts, consistent with StS1 effect (lose 6 HP, gain 2 energy, draw 3, exhaust). Exact StS2 numbers unverified. **[seen, mechanics conjectured from StS1]**
- **Rupture** — power. Whenever you lose HP from a card this turn, gain Strength. **Only procs during your own turn**, not from enemy damage. **[confirmed]** (observed: self-damage from Breakthrough triggered +Str; enemy hits did not.)
- **Breakthrough** — 9 damage to ALL enemies, lose 1 HP. **[confirmed]**
  - Self-damage does **not** seem to be lethal — played at 2 HP, stayed at 2 HP. StS1 lethality protection likely carries. **[tentative]**
- **Primal Force** — costs X? energy; generates **3 Giant Rocks** which are placed in hand (or drawn same turn). Needs re-verification for exact cost. **[confirmed card exists; cost TBD]**
  - **Giant Rock** — first rock observed as **16 damage single target + 5 splash** to other enemies; subsequent rocks appeared to be single-target only. Splash may be a first-rock-only rider or a misread. **[needs re-verification]**

### Skills (block and utility)
- **Defend** — 5 block; upgrade +3 → 8. **[confirmed]**
- **Inflame** — power, +2 Strength (+3 when upgraded). **Upgraded version draws 2 cards** when played. **[confirmed]**
- **Slow** — enemy power: damage dealt by enemy increases by +10% per card played against them this turn. Decays at end of enemy turn. **[confirmed via state.json enemy powers]**
- **Plating** — power granting **7 block at start of turn** (observed amount with Heart of Iron relic). **[confirmed]** (exact source: Heart of Iron — see relics)

### Status effects (confirmed mechanics)
- **Vulnerable** — +50% damage taken (confirmed by wiki: Paper Phrog relic text states "75% more damage rather than 50%", implying 50% is baseline); decays 1 stack per turn. **[confirmed]**
- **Weak** — damage dealt **−25%** (confirmed: Strike 6 → 4 with Weak). **[confirmed]**
- **Frail** — block gained **−25%** (confirmed: Defend 5 → 3 with Frail). **[confirmed]**
- **Strength** — +1 damage per stack on attacks. **[conjecture, StS1-identical]**

## Potions (confirmed)
See `reference-potions.md` for the full list. Key runtime quirks:
- **Bottled Potential** — discards hand, draws 5; known bridge refresh bug (see protocol notes).
- **Energy Potion** — +2 energy this turn. **[confirmed]**
- **Heart of Iron potion** — grants Plating (separate from relic); amount unconfirmed.

## Relics (confirmed in this run)
See `reference-relics.md` for the roster. Key entries from the Kin run:
- **Burning Blood** (starter) — +6 HP post-combat. **[confirmed]**
- **Heart of Iron** — grants **Plating 7** at combat start. Plating does not appear in `player.powers[]` until first refresh. **[confirmed]**

## Enemies encountered

### Act 1 hallway (tentative, needs more runs)
Incomplete — only a handful of fights observed this session. Record enemy `name`, `intents[]`, and damage values as they appear.

### Act 1 monsters and elites (2026-04-19 Ironclad run)
- **Vine Shambler** (monster): 61 HP. MultiAttack 6x2 = 12 dmg and a `CardDebuffIntent` that adds a curse/status card to hand. **[confirmed]**
- **Nibbit x2** (monster): Front Nibbit 44 HP, back Nibbit 42 HP. Front does Aggressive (SingleAttack 6 dmg) or Defensive (DefendIntent). Back does Empower (BuffIntent grants STR+2, presumably to ally and/or self) and Defensive (attack+defend combo, observed 8 dmg + defend). Priority: kill back first to stop STR stacking. **[confirmed]**
- **Bygone Effigy** (elite): 127 HP. Starts Asleep with Slow(1) power. First two turns: BuffIntent (+5 STR each, total +10 STR). Then wakes and does SingleAttackIntent 23 dmg repeatedly. Slow(1) passive effect unclear - user described as "+1 dmg per hit" but observed damages didn't match consistently; possibly a status marker only. **[confirmed HP / STR pattern / 23 dmg; Slow semantics unclear]**

### Act 1 boss: The Kin
Three-enemy boss fight. Observed turn-6 defeat; Kin Priest brought to 52/190 HP but Followers barely scratched.
- **Kin Priest** — 190 max HP. Support/damage; high HP pool. **[confirmed stats]**
- **Kin Follower** ×2 — ~58–59 max HP each (HP rolls within a small range on spawn). **[confirmed stats]**
- **Recommended priority**: burst a **Follower** first (one Primal Force + Giant Rock sequence one-shots them), cutting incoming damage by ~33% before committing to the 190-HP Priest.
- Full turn-by-turn in `verified-flows/2026-04-19-the-kin-boss/`.

## Early draft priorities (updated from this run)

### Premium
- **Inflame+** — +3 Str plus draw 2 is both tempo and scaling; single best early power. **[confirmed value]**
- **Primal Force** — Giant Rock generator is a one-card AoE finisher in the Kin fight; MVP against multi-target bosses. **[confirmed MVP]**
- **Thunderclap** — AoE + Vuln on turn 1 solves multi-enemy rooms.
- **Bash** upgrades early (Vulnerable amp on elites/bosses).

### Fine
- **Twin Strike** — two hits play nicely with Strength scaling; fine filler.
- **Hemokinesis** — attractive early shop pickup once Burning Blood and high starting HP can absorb the self-damage; worth documenting as a live-tested purchase path for Ironclad. **[confirmed bought in shop; combat value still to be verified]**
- **Defend+** — 8 block for 1 is the baseline.
- **Havoc** — cheap tempo, but mills a card.

### Situational
- **Breakthrough** — 9 AoE is strong, but the self-damage clause makes it risky without Rupture. Better in a Rupture deck.
- **Rupture** — needs self-damage enablers to be worth the slot.

### Trap candidates
- Any expensive (3+) non-scaling card without energy support.
- Exhaust payoffs without exhaust enablers.

## Upgrade priorities (tentative)
- **Inflame → Inflame+** first (draw 2 rider is transformative).
- **Bash → Bash+** for +2 dmg / +1 Vuln.
- **Defend → Defend+** early if taking hallway damage.
- Strikes last unless Heavy Blade / Perfected Strike support exists.

## Pathing heuristics
- Burning Blood + 80 HP supports two elites per act with healthy pathing.
- Rest site at (5,15) used this run for 21→45 HP — no smith available at that node in this seed. Confirm smith availability before committing to a rest path.
- Boss fight at (3,16) reached with ~45 HP and no major scaling; Kin punished the under-powered deck by turn 5.

## Bridge-specific notes
- Card IDs in `state.run.deck[]` are stable within a run; log them alongside titles when documenting flows (e.g. Primal Force id=818 in this run's reward grid).
- Enemy names live at `state.combat.enemies[].name` (not `title`).
- Intents are an array; multi-attack telegraphs show multiple entries.
- `MapClosed` after `SelectMapNode` is not necessarily a failure. Wait for `BeforeRoomEntered` / `AfterRoomEntered` / `BeforeCombatStart` before deciding the bridge stalled.
- Powers like Plating / Rupture may lag one state write behind the game's internal state (see protocol notes).
- Shop purchases are now confirmed reliable enough to include in a future controller/reference doc:
  - `Purchase { category, index }` updates both `shop.playerGold` and `run.gold`
  - bought merchant-card entries can become `null` in-place rather than disappearing from the array
  - `LeaveShop` cleanly returns to map via `NMapScreen.Open(false)`
- Reward handling notes now belong in both protocol docs and any future controller skill/reference:
  - `AfterRewardTaken` is a trustworthy hook for pruning consumed rewards immediately
  - `screen=Rewards` with `rewards=[]` is a legitimate interim state after a final card pick; the controller should `Proceed` rather than treating it as stale data

## Open questions for future runs
- Primal Force exact energy cost and Giant Rock splash rules (first-rock-only?).
- Heavy Blade Strength multiplier in StS2.
- Bully exact rider (Strength check? any debuff?).
- Third Kin member's name and intents.
- Full list of Kin empower sequencing.
- Does Rupture trigger on Hemokinesis / Offering / Reaper style self-damage? (No such cards seen yet.)
