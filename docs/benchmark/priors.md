# SpireBench priors — Trial v1 (B0 condition)

> **Status:** Draft. Frozen at trial-v1 first-run kickoff (TBD). Editing this file mid-trial invalidates all prior B0 runs.
>
> **Spec version:** `priors-v1` (matches `priors_version: v1.0` in v1 run records)
> **Protocol:** governed by `docs/benchmark/protocol-v1.md`. This file is read **only** by agents in the `B0-priors` knowledge condition.

---

## What this file is

A short, hand-curated rules sheet given to B0 agents at the start of every run, **in addition to** the standard reading list. It encodes the small set of patterns that v0's strongest model (Opus 4.7) consistently followed and weaker models consistently skipped. The goal is to close the "tooling-induced misplay" gap — not to teach strategy.

This file does **not**:

- Recommend specific card picks. (That crosses into deck-construction strategy — out of scope.)
- Reveal seed contents. (Per protocol-v1 §Seed strategy, the seed-to-Act-1 mapping is operator-only.)
- Replace `docs/data/eng/*.json`. The JSON is still authoritative for every numeric value.
- Mark its own rules as optional. The B0 contract is: read this file, then play.

If a rule here conflicts with `docs/benchmark/protocol-v1.md`, **the protocol wins** — file a `## Notes for maintainers` entry and proceed by the protocol.

---

## Rule 1 — Re-read state after every meaningful action

Three classes of action invalidate the most recent `state.json` you read:

- **Any `PlayCard`.** Hand re-packs by `handIndex` after every play. A `handIndex` you computed before the play is stale. Across v0, the single largest source of `status=error` responses was stale `handIndex` use (run03 5 errors, run05 ~15 errors, run20 25 errors — all the same root cause).
- **Any reward claim.** Reward arrays shift to position 0 after each `SelectReward`; the index of the next reward is **always 0** until the screen closes. (Documented in run04 bridge findings.)
- **Any `UsePotion` that opens a modal.** Power Potion, Skill Potion, Attack Potion, Colorless Potion all open `ChooseACardScreen`. The modal sometimes appears one tick later — if your next read does not show `chooseACardScreen.visible=true`, read again before issuing `PlayCard`. (run04 bridge finding; run20 bridge finding.)

Heuristic: if your previous tool call wrote to game state (anything that isn't a pure read), your next tool call is `read-state.ps1` or `read-combat.ps1`. **Do not chain two state-mutating commands without an intervening read.**

## Rule 2 — Character-specific resources are at documented stable paths

A single `read-combat.ps1` surfaces every character-distinguishing resource v1 cares about. Specifically:

| Character | Resource | Field | Helper output |
|---|---|---|---|
| **Regent** | Stars | `combat.stars` (int, ≥0 when relevant; hidden as `-1` for non-Regent) | `Stars: N` line in `read-combat.ps1` output |
| **Defect** | Orb queue + capacity | `combat.orbs` (array), `combat.orbCapacity` (int) | `Orbs (N/M):` block listing `[i] <orb> passive:X evoke:Y` |
| **Necrobinder** | Osty (the summoned ally) | `combat.allies[]` entry — Osty has its own HP/Block/Powers like any ally | Rendered in the `Allies:` block of `read-combat.ps1` |
| Ironclad / Silent | none beyond standard combat | — | — |

**Necrobinder mechanic — Osty soaks unblocked damage.** Necrobinder has no separate player-side meter; the entire character mechanic is **Osty**. Osty takes any unblocked damage **before** the player does. Practical consequences: (1) Osty's HP is your effective HP buffer, not a side resource — read `combat.allies[]` every turn, treat low Osty HP the same way you'd treat low player HP; (2) when Osty dies, the player takes the full unblocked hit on the next attack — do not enter a turn with Osty at 1-2 HP and zero block planning unless you have a way to heal/replace Osty or block the incoming damage; (3) "block density" for Necrobinder is calculated against player+Osty combined HP, and a fight that looks survivable while Osty is up flips on the turn Osty drops.

In v0 several runs (notably weaker models on Defect and Regent) made decisions without consulting these resources because the v0 helper script did not display them. v1's `read-combat.ps1` shows them in the same single read as everything else; **use that single read**, do not assume a follow-up "reflection dump" is required.

## Rule 3 — Pre-elite and pre-boss, read the encounter JSON

Before traveling to any elite or act boss node, read the relevant entry in `docs/data/eng/encounters_*.json` for the floor's elite pool / boss. Specifically:

- Act 1 elites: `encounters_act1.json`. Boss: `encounters_act1_boss.json`.
- Act 2 elites: `encounters_act2.json`. Boss: `encounters_act2_boss.json`.
- Act 3: same pattern.

You are looking for: HP, attack patterns, statuses applied, and any "phase" / "transform" trigger conditions. This is the single behavioral pattern that distinguishes Opus 4.7 (run21) from every other v0 model. The encounters where v0 agents died most often (Hunter Killer, Bygone Effigy, Decimillipede, Test Subject #C14, Vantom, Kin Priest) all have answers visible in the JSON: scaling Strength threats reward burst; Slippery-stack bosses reward Lightning/Frost; multi-turn buff bosses reward Vulnerable application.

Do this **before** the fight, not during. The combat tick budget is for plays, not for opening files.

## Rule 4 — Block density is non-negotiable for Ironclad and Necrobinder

These two characters die when their deck has too few `Defend`-class cards relative to incoming damage. The v0 audit (§4) flagged this as the second-largest death cluster (~4 boss-underprepped deaths).

Concretely:

- A starter deck has 4 `Defend` cards out of 10. If you remove a `Defend` (e.g. via a card-removal event) and have not replaced its block contribution with another block source (Iron Wave, Body Slam tech, Toric Toughness, etc.), expect to fold to any boss whose attack pattern includes a 20+ unblocked turn.
- Treat "block density" as a number you track explicitly: rough rule of thumb, you want **at least 30% of the deck to be block-or-block-equivalent** before the Act 1 boss, more before Act 2.
- If you skip a block card pickup to keep the deck thin, you have made a deliberate trade and should be able to name the burst-damage answer that justifies it.

Silent and Defect have alternate block lines (Defect via Frost orbs and Block-on-channel; Silent via card-draw discard plays); the same logic applies via different cards.

## Rule 5 — Scaling threats need scaling answers

A "scaling threat" is any enemy whose damage grows over time — Strength gains per turn, Ritual stacks, multi-attack with a multiplier that scales, etc. Examples in v0: Hunter Killer (Tender debuff scales damage taken), Kin Priest (Strength buff stacks), Decimillipede (Str gain per round), Vantom (Slippery stacks).

A scaling fight you don't kill quickly is a fight you lose. Two viable plans:

- **Burst:** end the fight in 3–4 turns with concentrated damage. Requires energy/draw fixing or a finisher card.
- **Reset:** apply Vulnerable / Weak / Strength-down before the scaling tips lethal, AND have block to bridge until you tip the math back. Pure block without a damage answer just delays the loss.

If the deck has neither, do not take the elite path; take the rest path and pray the boss is in the burst-friendly half of the pool. (Boss pool per `encounters_act1_boss.json` etc.)

## Rule 6 — Halt cleanly on death/victory/stall; one tick at a time

This rule is in the protocol but bears repeating because v0 had three runs that nearly broke it:

- **One bridge command per shell tool call.** No `while` loops over `combat.hand.cards`. No batched `PlayCard` calls. No "I'll just play three cards and then read state." Read between every play.
- **Stall = bridge `revision` field hasn't changed in 30s after your command.** If you observe this, your last command is not landing. Re-read state once; if still stuck, halt with `halt_reason: stall`. Do not retry-spam.
- **Run cap is 1000 commands** (was 500 in v0). If you approach 900 and you're still in Act 1, you are likely in a non-progressing state — re-evaluate before spending the remaining budget.
- **Halt without a record is a benchmark failure.** Write the run record before stopping, even on rate-limit / error-streak. (Per `agent-prompt.md`.)

## Character notes (one paragraph each, deliberately neutral on card picks)

### Ironclad

Block-and-burst character. Strength scales attacks; the deck wants enough block to survive long enough for Strength gains and Heavy Blade / Bludgeon to land lethal. The two failure modes seen most in v0 are (a) under-blocking against 20+ unblocked turns at boss, and (b) hand-index drift when chaining 0-cost or attack-doubling effects (One-Two Punch, Pommel Strike). Re-read state after every card in long turns. Tender and Vulnerable are first-class concerns — they modify damage math both ways and `state.combat.player.powers[]` is where you check.

### Silent

Discard / poison / dexterity character. Dies when the agent forgets that discarding cards from hand can change what's available to play next turn — discard-pile shuffle isn't intuitive. Poison scaling is the alternative damage path; if your deck has neither block density (Dex stacking + Defends) nor poison scaling, you have no plan against an Act 1 boss. The hand-index drift problem is amplified by frequent draw / discard effects. Re-read after every meaningful card play, not just between turns.

### Defect

Orb-pilot character. Channel orbs into the queue (`combat.orbs`, capacity `combat.orbCapacity`); each orb has `passiveVal` (per-turn effect) and `evokeVal` (effect on evoke). Frost = block, Lightning = damage, Dark = scaling damage, Plasma = energy. The decisions Defect agents missed in v0 were almost all "what is currently in the orb queue" — the v0 helper script didn't display it; v1's does. **Read it.** The most reliable Act 1 boss answer is Frost density + a Lightning burst card; alternatives exist but they require setup turns the boss may not give you.

### Regent

Star-spending character. `combat.stars` is your primary resource; it's a small int that you spend on premium effects. Several Regent cards generate stars (e.g. Solar Strike), some events refund stars on certain choices, and the boss rewards usually include star-cost amplifiers. The v0 Regent runs that died early (run03, run13, run18) all spent stars greedily early without reading the elite/boss pool first; the Regent runs that lasted longer treated stars as "save for the encounter you've identified the answer for." Stars at 0 is a **valid game state** — `read-combat.ps1` displays the line — and means your next turn is starless, not buggy.

### Necrobinder

Summon character. The Osty ally is the entire character mechanic — Osty soaks any unblocked damage **before** the player does, so Osty's HP is functionally part of your HP pool, not a side resource. Read `combat.allies[]` every turn; treat low-Osty-HP the same way you'd treat low player HP. When Osty dies, the next unblocked attack hits the player at full force — never end a turn with Osty at 1-2 HP and no plan to either block the incoming damage, heal Osty, or re-summon. The starter deck has low burst — long fights against Illusion-revive enemies (Eye With Teeth, Wrigglers) snowball badly without Vulnerable / Weak application or burst draw. The pre-elite encounter-JSON read is especially important for this character because its damage profile is "consistent low" — fights that look winnable on turn 1 can grind through Osty + player HP over 7+ rounds (run04 lost this way to Fogmog at full HP, then died next floor on residual).

---

## What this file deliberately does not contain

- **Card-pick advice.** "Take Inflame over Strike+" is strategy contamination.
- **Specific seed knowledge.** The agent does not know the seed-to-Act-1 mapping; that's in `seeds-v1.md` (operator-only).
- **Bridge IPC quirks beyond Rule 1.** Those live in `docs/bridge-protocol-notes.md` and `SKILL.md`.
- **Cost / time guidance.** Already in `agent-prompt.md` resource-calibration block.

---

Source documents:
- `docs/benchmark/trial-v0-findings-audit.md` §3, §4 (the patterns this file generalizes).
- `docs/benchmark/runs/2026-05-04-claude-opus-4.7-ironclad-run21.md` (the gold-standard discipline this file aims to make explicit for weaker models).
- `docs/benchmark/runs/2026-04-28-glm-5.1-necrobinder-run04.md`, `runs/2026-04-28-glm-5.1-defect-run05.md`, `runs/2026-04-27-glm-5.1-regent-run03.md`, `runs/2026-05-04-deepseek-v4-pro-defect-run20.md` (concrete examples of the failure modes Rules 1–5 address).
- `docs/benchmark/protocol-v1.md` §Knowledge conditions (this file is the B0 addition), §Bridge changes required #6 (character-resource paths), §Open items #8 (Necrobinder-meter status).
