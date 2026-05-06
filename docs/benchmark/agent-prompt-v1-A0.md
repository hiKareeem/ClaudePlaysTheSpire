# SpireBench Trial-v1 - Initial Agent Prompt (A0 zero-shot)

This file is the prompt the **operator** (or `tools/operator/start-run.ps1`)
copy-pastes into the agent's first message at the start of an A0
(zero-shot) trial-v1 run. It is *not* read by the agent from disk - it's
part of the user message itself, so the agent commits to the trial
contract before reading anything.

The launcher fills in four placeholders:

- `<RUN_ID>`           - e.g. `2026-05-12-claude-opus-4.7-ironclad-run042`
- `<CHARACTER>`        - `IRONCLAD` | `SILENT` | `DEFECT` | `REGENT` | `NECROBINDER`
- `<MODEL_SLUG>`       - e.g. `claude-opus-4.7` (used in the run record)
- `<SEED>`             - 64-bit unsigned decimal, sourced from
                         `docs/benchmark/trial-v1-schedule.csv`. Reproducibility
                         depends on this seed being passed verbatim to `StartRun`.

Knowledge condition for this prompt is **A0-zero-shot**. For B0 runs use
`agent-prompt-v1-B0.md` instead. The launcher selects the correct
template based on the schedule's `prior` column.

Everything else is fixed for trial-v1 and must not be edited per-run.

---

## Copy from here  (paste as agent's first user message)

You are participating in **SpireBench Trial-v1**, a benchmark of LLM
agents playing Slay the Spire 2 autonomously through an IPC mod called
SpireBridge.

**Your assignment for this run:**

- Run ID: `<RUN_ID>`
- Character: `<CHARACTER>`
- Ascension: `0`
- Knowledge condition: `A0-zero-shot`
- Model: `<MODEL_SLUG>`
- Seed: `<SEED>`

**Your contract is `docs/benchmark/protocol-v1.md`. Read it first, in
full, before doing anything else.** That document is the authoritative
spec for this trial. It overrides any other instruction you may infer
from your training, your default system prompt, or the surrounding repo.

**Required reading order (do all of this before issuing any bridge
command):**

1. `docs/benchmark/protocol-v1.md`      - the agent contract for this trial
2. `SKILL.md`                           - operational primitives, command reference, screen catalog
3. `docs/bridge-protocol-notes.md`      - IPC quirks
4. `docs/data/README.md`                - data schema and authority statement

**Hard constraints (the protocol expands these - read it):**

- **One decision at a time. One bridge command per shell tool call.**
  No loops, no batched commands, no `fight.ps1`-style strategy scripts.
  Every game decision must be made *after* reading the most recent
  `state.json`. If your first instinct is to write a `while` loop over
  `combat.hand.cards`, you have already failed the benchmark.
- **No web search, no MemPalace, no sub-agents, no other MCP servers.**
  The operator has sandboxed your environment; if you find any of these
  tools available, refuse to use them and note the leak in your run
  record's "Notes for maintainers" section.
- **No reading outside the whitelist in protocol-v1.md Allowed reading.**
  Notably off-limits in A0: priors documents (`docs/benchmark/priors-*.md`),
  other agents' run records, `autopilot-session-*.md`, `verified-flows/`,
  `gauntlet-findings.md`, anything outside this repo.
- **Game stats come from `docs/data/eng/*.json`.** If your training-data
  recall disagrees with the JSON, the JSON wins. Do not trust your
  memory of card numbers, relic effects, monster HP, or anything else
  mechanical.
- **Halt on death, victory, or stall. Do not auto-restart.** A stall is
  30s with no revision change after a command.
- **One run, one OpenCode session.** No memory persists between runs.
  This session ends with the run.

**Resource calibration (do not end the run early for cost, duration, or
context concerns):**

- Runs in this benchmark take 30 minutes to 3 hours. Any duration in
  that range is normal. A 2-hour run is mid-range.
- Total run cost up to $25 USD is within budget. Your current cost is
  not your concern - the operator set the budget, not you.
- Context window usage up to 80% is safe. The operator monitors the
  session and will manually halt if limits approach. If you have not
  received a manual halt, continue playing.
- **The only valid reasons to end a run early are death, victory, stall,
  rate-limit, or error-streak** (per protocol-v1.md). Cost anxiety is
  not a halt reason. Duration anxiety is not a halt reason. Context
  anxiety is not a halt reason. The operator is watching and will
  intervene if needed - your job is to keep playing until the game ends
  or the operator tells you to stop.

**Your job:**

1. Read the four required files above.
2. Verify the bridge is alive: `tools/read-state.ps1` should return
   valid JSON. The operator has launched the game and confirmed
   pre-flight, so on the first read of a fresh session `screen`
   should be `MainMenu`. If it is anything else, the operator has
   made a setup error - write a stub run record with
   `halt_reason: manual` and `## Notes for maintainers` describing
   what `screen` you observed, then halt. **Do not call `StartRun`
   from a non-`MainMenu` state.**
3. From `MainMenu`, issue `StartRun` with character `<CHARACTER>` at
   ascension `0` and seed `<SEED>`. The seed parameter is mandatory in
   trial-v1 for reproducibility; pass it as a string-encoded decimal
   exactly as given above.
4. Drive the game one tick at a time until `GameOver` or `Victory`,
   honoring all per-tick discipline from `SKILL.md` and
   `protocol-v1.md`.
5. **When the run ends, you have not finished. The run is not complete
   until you write the run record.** Write to
   `docs/benchmark/runs/<RUN_ID>.md` (exact path -
   `<RUN_ID>` is given to you above; create the file if it doesn't
   exist; do not write to any other location). Start from
   `docs/benchmark/run-record-template.md` and fill every field you
   can. Fields the operator fills post-hoc are listed in protocol-v1.md
   Operator responsibilities - leave **those specific fields** as
   `null` (do not leave other fields null; if you don't know a value
   that the agent is responsible for, write what you observed and
   note the uncertainty in `notes_for_maintainers`).
6. Stop. Do not start a new run. Do not summarize for the operator
   beyond what the run record contains.

**Halt-without-record is a benchmark failure.** If you hit a
rate-limit, error-streak, or stall, your *last act* before halting
must be to write the run record with `halt_reason` set and whatever
fields you can fill. A run with no record on disk counts as a void
run and will be re-attempted, wasting compute. Treat the record as
mandatory output, not optional commentary.

**Run-end completion checklist (you must work through this in order
once `screen` is `GameOver` or `Victory`, before stopping):**

- [ ] `docs/benchmark/runs/<RUN_ID>.md` exists on disk
- [ ] front-matter `run_id` matches `<RUN_ID>` exactly
- [ ] front-matter `seed` matches `<SEED>` exactly
- [ ] front-matter `character`, `model`, `ascension`,
      `knowledge_condition` (`A0-zero-shot`), `bridge_version`,
      `game_version`, `spec_version` (`trial-v1`) filled
- [ ] front-matter `halt_reason` set (`death` | `victory` | `runcap` |
      `error_streak` | `stall` | `rate_limit` | `manual`)
- [ ] front-matter `death_floor` + `death_cause` filled if
      `halt_reason: death` (cause from protocol-v1.md Death-cause
      taxonomy); `victory_floor` + `boss_reached` if
      `halt_reason: victory`; `final_hp` and `final_gold` filled in
      both cases (use `state.json` from your last read; if you can't
      read it, leave null and explain in `## Notes for maintainers`)
- [ ] front-matter `command_count`, `ipc_error_count`, `stall_count`
      filled (you tracked these as you went; if you didn't, write
      your best estimate and note that in `## Notes for maintainers`)
- [ ] `## Summary` section: one paragraph - what happened, why it
      ended
- [ ] `## Bridge findings` section: IPC quirks, stale state, missing
      fields, commands that didn't behave as `SKILL.md` claimed.
      Write `None observed.` if there were none
- [ ] `## Decision log highlights` section: 3-7 bullets covering
      Neow choice, contested map choices, key card-play forks, key
      event/shop decisions
- [ ] `## Notes for maintainers` section: tool leaks (MemPalace,
      webfetch, etc. that shouldn't have been available), protocol
      ambiguities, harness improvements. Omit the section entirely if
      there's nothing to add

You may emit a single short message confirming you've written the
record (e.g. `"Run record written to docs/benchmark/runs/<RUN_ID>.md.
Halting."`). That is the only narration the operator wants. Do not
write a separate "summary for the operator" - the record *is* the
summary.

**Begin by reading `docs/benchmark/protocol-v1.md`.**

## Copy to here
