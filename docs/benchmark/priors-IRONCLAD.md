# SpireBench priors — Trial v1 (B0 condition) — Ironclad

> **Status:** Draft. Frozen at trial-v1 first-run kickoff (TBD). Editing this file mid-trial invalidates all prior B0 Ironclad runs.
>
> **Spec version:** `priors-v1` (matches `priors_version: v1.0` in v1 run records)
> **Protocol:** governed by `docs/benchmark/protocol-v1.md`. This file is read **only** by agents in the `B0-priors` knowledge condition who have been assigned the `IRONCLAD` character. Other characters have their own `priors-<CHARACTER>.md` and you must not read theirs.

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

A single `read-combat.ps1` surfaces every character-distinguishing resource v1 cares about. For Ironclad specifically: there is no character-unique resource beyond standard combat — no stars, no orb queue, no allies. Your one read covers everything. (For reference: Regent uses `combat.stars`, Defect uses `combat.orbs` + `combat.orbCapacity`, Necrobinder uses `combat.allies[]` for the Osty ally — none of these apply to you.)

In v0 several runs (notably weaker models on Defect and Regent) made decisions without consulting these resources because the v0 helper script did not display them. v1's `read-combat.ps1` shows them in the same single read as everything else; **use that single read**, do not assume a follow-up "reflection dump" is required.

## Rule 3 — Pre-elite and pre-boss, read the encounter JSON

Before traveling to any elite or act boss node, read the relevant entry in `docs/data/eng/encounters_*.json` for the floor's elite pool / boss. Specifically:

- Act 1 elites: `encounters_act1.json`. Boss: `encounters_act1_boss.json`.
- Act 2 elites: `encounters_act2.json`. Boss: `encounters_act2_boss.json`.
- Act 3: same pattern.

The act boss is **not** randomized at run start — it is fixed when the act's map is generated and is exposed by the bridge as `state.map.bossId` (and `bossName` when available). Run `tools\read-map.ps1` once on entry to a new act; the `Boss:` line shows the identity, and that determines which entry to look up. Do not assume the boss from prior runs of the same character.

You are looking for: HP, attack patterns, statuses applied, and any "phase" / "transform" trigger conditions. This is the single behavioral pattern that distinguishes Opus 4.7 (run21) from every other v0 model. The encounters where v0 agents died most often (Hunter Killer, Bygone Effigy, Decimillipede, Test Subject #C14, Vantom, Kin Priest) all have answers visible in the JSON: scaling Strength threats reward burst; Slippery-stack bosses reward Lightning/Frost; multi-turn buff bosses reward Vulnerable application.

Do this **before** the fight, not during. The combat tick budget is for plays, not for opening files.

## Rule 4 — Block density is non-negotiable for Ironclad and Necrobinder

Ironclad dies when the deck has too few `Defend`-class cards relative to incoming damage. The v0 audit (§4) flagged this as the second-largest death cluster (~4 boss-underprepped deaths).

Concretely:

- A starter deck has 4 `Defend` cards out of 10. If you remove a `Defend` (e.g. via a card-removal event) and have not replaced its block contribution with another block source (Iron Wave, Body Slam tech, Toric Toughness, etc.), expect to fold to any boss whose attack pattern includes a 20+ unblocked turn.
- Treat "block density" as a number you track explicitly: rough rule of thumb, you want **at least 30% of the deck to be block-or-block-equivalent** before the Act 1 boss, more before Act 2.
- If you skip a block card pickup to keep the deck thin, you have made a deliberate trade and should be able to name the burst-damage answer that justifies it.

(For reference, the same logic applies to Necrobinder via Osty soaking; Silent and Defect have alternate block lines — Defect via Frost orbs and Block-on-channel; Silent via card-draw discard plays — but those don't apply to you on this run.)

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

## Character notes — Ironclad

Block-and-burst character. Strength scales attacks; the deck wants enough block to survive long enough for Strength gains and Heavy Blade / Bludgeon to land lethal. The two failure modes seen most in v0 are (a) under-blocking against 20+ unblocked turns at boss, and (b) hand-index drift when chaining 0-cost or attack-doubling effects (One-Two Punch, Pommel Strike). Re-read state after every card in long turns. Tender and Vulnerable are first-class concerns — they modify damage math both ways and `state.combat.player.powers[]` is where you check.

---

## What this file deliberately does not contain

- **Card-pick advice.** "Take Inflame over Strike+" is strategy contamination.
- **Specific seed knowledge.** The agent does not know the seed-to-Act-1 mapping; that's in `seeds-v1.md` (operator-only).
- **Bridge IPC quirks beyond Rule 1.** Those live in `docs/bridge-protocol-notes.md` and `SKILL.md`.
- **Cost / time guidance.** Already in `agent-prompt.md` resource-calibration block.
- **Priors for other characters.** They live in `priors-SILENT.md`, `priors-DEFECT.md`, `priors-REGENT.md`, `priors-NECROBINDER.md` and are out-of-whitelist for an Ironclad run.

---

Source documents:
- `docs/benchmark/trial-v0-findings-audit.md` §3, §4 (the patterns this file generalizes).
- `docs/benchmark/runs/2026-05-04-claude-opus-4.7-ironclad-run21.md` (the gold-standard discipline this file aims to make explicit for weaker models).
- `docs/benchmark/runs/2026-04-28-glm-5.1-necrobinder-run04.md`, `runs/2026-04-28-glm-5.1-defect-run05.md`, `runs/2026-04-27-glm-5.1-regent-run03.md`, `runs/2026-05-04-deepseek-v4-pro-defect-run20.md` (concrete examples of the failure modes Rules 1–5 address).
- `docs/benchmark/protocol-v1.md` §Knowledge conditions (this file is the B0 addition), §Bridge changes required #6 (character-resource paths), §Open items #8 (Necrobinder-meter status).
