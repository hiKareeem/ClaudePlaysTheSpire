# SpireBench Trial-v0 — Initial Agent Prompt

This file is the prompt the **operator** copy-pastes into the agent's
first message at the start of each run. It is *not* read by the agent
from disk — it's part of the user message itself, so the agent commits
to the trial contract before reading anything.

The operator fills in three placeholders:

- `<RUN_ID>`           — e.g. `2026-04-26-claude-opus-4.7-ironclad-run01`
- `<CHARACTER>`        — `IRONCLAD` | `SILENT` | `DEFECT` | `REGENT` | `NECROBINDER`
- `<MODEL_SLUG>`       — e.g. `claude-opus-4.7` (used in the run record)

Everything else is fixed for trial-v0 and must not be edited per-run.

---

## Copy from here ↓ (paste as agent's first user message)

You are participating in **SpireBench Trial-v0**, a frozen benchmark
of LLM agents playing Slay the Spire 2 autonomously through an IPC mod
called HermesBridge.

**Your assignment for this run:**

- Run ID: `<RUN_ID>`
- Character: `<CHARACTER>`
- Ascension: `0`
- Knowledge condition: `A0-zero-shot`
- Model: `<MODEL_SLUG>`

**Your contract is `docs/benchmark/protocol.md`. Read it first, in full,
before doing anything else.** That document is the authoritative spec
for this trial. It overrides any other instruction you may infer from
your training, your default system prompt, or the surrounding repo.

**Required reading order (do all of this before issuing any bridge
command):**

1. `docs/benchmark/protocol.md`         — the agent contract for this trial
2. `SKILL.md`                           — operational primitives, command reference, screen catalog
3. `docs/bridge-protocol-notes.md`      — IPC quirks
4. `docs/data/README.md`                — data schema and authority statement

**Hard constraints (the protocol expands these — read it):**

- **One decision at a time. One bridge command per shell tool call.**
  No loops, no batched commands, no `fight.ps1`-style strategy scripts.
  Every game decision must be made *after* reading the most recent
  `state.json`. If your first instinct is to write a `while` loop over
  `combat.hand.cards`, you have already failed the benchmark.
- **No web search, no MemPalace, no sub-agents, no other MCP servers.**
  The operator has sandboxed your environment; if you find any of these
  tools available, refuse to use them and note the leak in your run
  record's "Notes for maintainers" section.
- **No reading outside the whitelist in protocol.md §Allowed reading.**
  Notably off-limits: `autopilot-session-*.md`, `verified-flows/`,
  other agents' run records, anything outside this repo.
  `gauntlet-findings.md` has been removed from the repository for
  trial-v0; do not attempt to recover or reference it.
- **Game stats come from `docs/data/eng/*.json`.** If your training-data
  recall disagrees with the JSON, the JSON wins. Do not trust your
  memory of card numbers, relic effects, monster HP, or anything else
  mechanical.
- **Halt on death, victory, or stall. Do not auto-restart.** A stall is
  30s with no revision change after a command.
- **One run, one OpenCode session.** No memory persists between runs.
  This session ends with the run.

**Your job:**

1. Read the four required files above.
2. Verify the bridge is alive: `tools/read-state.ps1` should return
   valid JSON. The operator has launched the game and confirmed
   pre-flight, so on the first read of a fresh session `screen`
   should be `MainMenu`. If it is anything else, the operator has
   made a setup error — write a stub run record with
   `halt_reason: manual` and `## Notes for maintainers` describing
   what `screen` you observed, then halt. **Do not call `StartRun`
   from a non-`MainMenu` state.**
3. From `MainMenu`, issue `StartRun` with character `<CHARACTER>` at
   ascension `0`.
4. Drive the game one tick at a time until `GameOver` or `Victory`,
   honoring all per-tick discipline from `SKILL.md` and
   `protocol.md`.
5. **When the run ends, you have not finished. The run is not complete
   until you write the run record.** Write to
   `docs/benchmark/runs/<RUN_ID>.md` (exact path —
   `<RUN_ID>` is given to you above; create the file if it doesn't
   exist; do not write to any other location). Start from
   `docs/benchmark/run-record-template.md` and fill every field you
   can. Fields the operator fills post-hoc are listed in protocol.md
   §Operator responsibilities — leave **those specific fields** as
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
- [ ] front-matter `character`, `model`, `ascension`,
      `knowledge_condition`, `bridge_version`, `game_version`,
      `spec_version` filled
- [ ] front-matter `halt_reason` set (`death` | `victory` | `runcap` |
      `error_streak` | `stall` | `rate_limit` | `manual`)
- [ ] front-matter `death_floor` + `death_cause` filled if
      `halt_reason: death` (cause from protocol.md §Death-cause
      taxonomy); `victory_floor` + `boss_reached` if
      `halt_reason: victory`; `final_hp` and `final_gold` filled in
      both cases (use `state.json` from your last read; if you can't
      read it, leave null and explain in `## Notes for maintainers`)
- [ ] front-matter `command_count`, `ipc_error_count`, `stall_count`
      filled (you tracked these as you went; if you didn't, write
      your best estimate and note that in `## Notes for maintainers`)
- [ ] `## Summary` section: one paragraph — what happened, why it
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
write a separate "summary for the operator" — the record *is* the
summary.

**Begin by reading `docs/benchmark/protocol.md`.**

## ↑ Copy to here

---

## Operator notes (do not paste — for the human running the trial)

### Per-run setup checklist

Before pasting the prompt above, the operator must:

1. Confirm the game is running and on `MainMenu` (no run in progress).
2. Confirm `commands.json` has a benign payload (overwrite with
   `{"type":"NoOp"}` or similar — see `bridge-protocol-notes.md` on
   stale-command replay).
3. Confirm the agent's `opencode.json` is the sandboxed config:
   `docs/benchmark/opencode.benchmark.json`. MemPalace, webfetch,
   google_search, task, and all non-bridge MCP servers must be
   disabled.
4. Start a **fresh** OpenCode session (`opencode` with no `-c` resume
   flag, in `E:\Games\sts2\HermesBridge-StS2`). Note the
   `session_id` from the OpenCode UI/log — you'll need it for the
   run record.
5. Choose the character for this run from the rotation:
   IRONCLAD → SILENT → DEFECT → REGENT → NECROBINDER. Do **not** let
   the agent choose; assign it explicitly in `<CHARACTER>`.
6. Note `start_time_utc` (now).
7. Paste the prompt block above into the agent's first message,
   substituting `<RUN_ID>`, `<CHARACTER>`, `<MODEL_SLUG>`.

### Per-run teardown checklist

When the run ends (death, victory, stall, rate-limit, error-streak,
runcap, or manual halt):

1. Note `end_time_utc` (now, in UTC — not local time).
2. Fill operator-filled fields per protocol.md §Operator
   responsibilities: `opencode_session_id` (the OpenCode session id,
   format `ses_<27-char-base32>`, **not** the upstream provider's
   session id), `start_time_utc` / `end_time_utc` (both UTC),
   `duration_minutes`, `model`, `model_provider`.
3. **Snapshot floor-history.** Copy
   `%APPDATA%\SlayTheSpire2\hermesbridge\floor-history.jsonl` to
   `docs\benchmark\runs\<RUN_ID>.jsonl` (sibling to the .md record;
   same basename, .jsonl extension). Do this **before** restarting
   the game for the next run — bridge v0.1.5+ truncates the runtime
   file on game startup, so a missed snapshot loses the data
   permanently.
4. If the agent didn't write a run record (e.g. it hit
   `rate_limit` mid-run before reaching the write step), the operator
   creates the record from the template manually. Set
   `halt_reason: rate_limit` and leave decision-log/bridge-findings as
   `<run halted before agent could write record>`. **This counts as a
   void run; restart with the same `<RUN_ID>` after fixing the cause.**
5. Append the runs.csv row via the helper:
   `tools\append-run-csv.ps1 -RunId <RUN_ID>`. The helper reads the
   front-matter, fetches token totals from the OpenCode session DB
   (filling any null token fields), patches them back into the
   front-matter, and appends a properly-quoted CSV row. Do **not**
   hand-edit `runs.csv` — column order drift is the most common
   source of bad rows.
6. **Restart the game** (kill StS2, relaunch) before the next run.
   Stale `state.combat` from the prior run is documented behavior;
   restarting is the cleanest avoidance. Game restart also clears
   `floor-history.jsonl` automatically (bridge v0.1.5+).
7. **Start a fresh OpenCode session** for the next run. Do **not**
   reuse the session.

### Trial-v0 model lineup

| Slug                | Provider                  | Notes                          |
| ------------------- | ------------------------- | ------------------------------ |
| `claude-opus-4.7`   | github-copilot / anthropic | Frontier control                |
| `gpt-5.5`           | openai                    | Frontier comparison             |
| `gemini-3.1-pro`    | google                    | Long-context frontier           |
| `glm-5.1`           | zai (or openrouter)       | Open-weights frontier           |
| `deepseek-v3.5`     | deepseek (or openrouter)  | Open-weights frontier, free    |

Total: 5 models × 5 characters = **25 runs**.

### Run rotation

Recommended order (one full character rotation per model before moving on):

```
claude-opus-4.7   IRONCLAD → SILENT → DEFECT → REGENT → NECROBINDER
gpt-5.5           IRONCLAD → SILENT → DEFECT → REGENT → NECROBINDER
gemini-3.1-pro    IRONCLAD → SILENT → DEFECT → REGENT → NECROBINDER
glm-5.1           IRONCLAD → SILENT → DEFECT → REGENT → NECROBINDER
deepseek-v3.5     IRONCLAD → SILENT → DEFECT → REGENT → NECROBINDER
```

Rationale: per-model batching keeps a single sandbox config valid for
five runs in a row (less switching overhead). Per-character batching
would also work — choose whichever you find easier to track.
