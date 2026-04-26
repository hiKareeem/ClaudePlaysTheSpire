# Reference Index

This directory holds two layers of agent reference:

## 1. Mechanical ground truth — [`data/eng/`](data/eng/)

Structured JSON vendored from [spire-codex](https://github.com/ptrlrd/spire-codex) (see [`data/ATTRIBUTION.md`](data/ATTRIBUTION.md)). Authoritative for every stat: costs, damage, HP, intents, scaling, descriptions, upgrades, powers applied, keywords.

**Current game version:** 0.104.0 (2026-04-23). See [`data/changelogs/0.104.0.json`](data/changelogs/0.104.0.json) for the v0.103.2 → v0.104.0 patch notes (308 changed entries; reworks: Conflagration, Drum of Battle, Parry/Sovereign Blade; new badges; ancient buff scaling).

Read [`data/README.md`](data/README.md) for a file-by-file schema guide.

## 2. Curated strategy & runbook

Hand-authored agent guidance. Use these for decisions, synergies, edge cases, and in-run confirmed behavior — not for looking up numbers.

| File | Content |
|---|---|
| [`reference-ironclad.md`](reference-ironclad.md) | In-run confirmed Ironclad card/mechanic notes |
| [`reference-relics.md`](reference-relics.md) | Relic strategy notes |
| [`reference-potions.md`](reference-potions.md) | Potion usage notes |
| [`example-run-necrobinder.md`](example-run-necrobinder.md) | Annotated Necrobinder playthrough |
| [`bridge-protocol-notes.md`](bridge-protocol-notes.md) | HermesBridge RPC protocol quirks |
| [`hermes-sts2-runbook.md`](hermes-sts2-runbook.md) | Run operation runbook |
| [`gauntlet-findings.md`](gauntlet-findings.md) | Gauntlet test observations |

## Rule of thumb

**Numbers → JSON. Decisions → markdown.**

If JSON and curated markdown disagree on a stat, JSON wins (it is regenerated from the game DLL). If they disagree on strategy, markdown wins — strategy files record in-run observed behavior that may differ from raw data.

## History

The previous `cards-*.md`, `relics.md`, `potions.md`, `buffs.md`, `debuffs.md` files were wiki-scraped stat dumps (slaythespire.wiki.gg, 2026-04-20). They have been superseded by `data/eng/` JSON, which is complete, structured, and sourced from the decompiled game DLL rather than wiki edits. Stub files remain at the old paths and point here.
