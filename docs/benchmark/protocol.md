# SpireBench-Lite Protocol — Trial v0

> **Status:** Draft. This document freezes the agent contract for the first
> trial of SpireBench (the Slay the Spire 2 LLM-agent benchmark built on
> HermesBridge). It is derived from `SKILL.md` but stripped, frozen, and
> evaluation-oriented. While we are running the trial, **this file is the
> authoritative spec.** Changes to `SKILL.md` after the trial freeze date
> below do **not** retroactively change the contract for runs already
> completed.

**Trial freeze date:** 2026-04-26
**Bridge version:** v0.1.3 (or successor — see `Bridge build` in §Run record)
**Game version:** Slay the Spire 2 v0.104.0
**Spec version:** trial-v0
**Knowledge condition:** `A0-zero-shot` (no prior-run context, no persistent
memory across runs, no web search, no training-recall fallback). Future
trials may add `A0-coached` as a separate condition; runs from different
conditions are **not** comparable.

---

## What this benchmark measures

A frontier-or-mid-tier coding agent's ability to **autonomously play one life
of Slay the Spire 2** through a deterministic IPC, given a frozen reading
list and a fixed primary metric.

Primary metric: **floor reached at death** (or `victory_floor` on win).
Secondary metrics: command count, IPC error count, stall count, win/loss,
boss reached, ascension, character.

This is not a saturated benchmark. Frontier models (Claude Opus 4.7-class,
GPT-5.2-high, Gemini 3 Pro) consistently clear Act 1 (floor 17) on A0 in
informal runs; mid-tier models (Sonnet 4.5, GPT-5-mini, Gemini 3 Flash)
consistently die in the first 4 floors, usually inside the first combat,
to one of the failure modes catalogued in §Known mid-tier failure modes
below.

---

## Agent contract

You are an LLM agent. You have access to a shell tool that can execute
PowerShell. You have a frozen reading list (§Allowed reading) and a frozen
toolset (`autopilot-lib.ps1` + `tools/*.ps1`). You may **not** create new
scripts, modify existing ones, or read files outside the allowed list.

### Your job

1. From `MainMenu`, issue `StartRun` with the assigned character.
2. Drive the game through to either `GameOver` or `Victory`.
3. Halt on death, win, or stall — **do not auto-restart**.
4. Write one run record to the run log (§Run record).

### How you drive

**One decision at a time, one command per tool call.** This is mandatory
and non-negotiable. The same per-tick discipline that `SKILL.md` enforces
applies here verbatim — see `SKILL.md` §"CRITICAL: how you operate" for the
full rationale.

Every shell tool call may contain **at most one** `Send-BridgeCommand`.
Every game decision (which card to play, which target to pick, which Neow
option, which map node, which event option) must be reasoned about *after*
you have read the most recent state. **No game-logic loops, no script-side
strategy, no batched commands.**

If your first instinct is to write a `while` loop over `combat.hand.cards`,
you have already failed the benchmark. Stop and re-read the rule.

### Disallowed actions

You may not:

- Create any new file outside `docs/benchmark/runs/<run_id>.md` (the run record).
- Modify `autopilot-lib.ps1`, any `tools/*.ps1`, any C# file, or `SKILL.md`.
- Read other agents' run records (`docs/benchmark/runs/<other_run_id>.md`).
- Read `gauntlet-findings.md`. The file has been removed from the
  repository for the duration of trial-v0 (see §Amendments,
  trial-v0.1) — it does not exist on disk. All bridge-protocol
  findings live in `SKILL.md` and `docs/bridge-protocol-notes.md`.
  Do not attempt to recover the file from git history or any other
  source.
- Web-search for Slay the Spire 2 strategy, card stats, or anything else.
  All stat lookups must come from `docs/data/eng/*.json`.
- Recall stats from training data. If the JSON disagrees with your
  memory, the JSON wins.
- Issue commands not listed in §Command reference of `SKILL.md`.

### Disallowed tools

The trial-v0 knowledge condition is `A0-zero-shot`. The following tools
are forbidden — operator pre-flight (§Operator responsibilities) ensures
they are unavailable, but the agent must also not invoke them if exposed:

- **Any `mempalace_*` tool** (semantic memory, knowledge graph, diary,
  drawers). MemPalace is the maintainer's persistent memory store and
  contains contamination from prior bridge work, prior runs, and
  unrelated projects. Off-limits absolutely.
- **Web fetch / web search** of any kind, including `webfetch`,
  `google_search`, `context7_*`. The only network egress permitted is
  whatever the bridge IPC requires (none, in practice).
- **Sub-agent spawning** (`task` tool, agent delegation). One agent, one
  context window, one run. Sub-agents = sub-contexts = leaked
  reasoning that can't be audited from the session transcript.
- **Any MCP server other than the bridge itself.** Filesystem MCP, git
  MCP, sequential-thinking MCP, all denied. The shell tool plus the
  in-session file-read/edit primitives are sufficient.
- **Reading outside the repo working directory.** No `~/.config/...`,
  no `~/.local/share/opencode/...`, no other repos.

If the agent finds any of these tools available in its environment, it
must refuse to use them and note the environment leak in §Notes for
maintainers of its run record.

### Allowed reading

This is an **exhaustive whitelist**. Any file not on this list is
off-limits, regardless of whether the operator's filesystem grants access.

You **must** read these at the start of every run, in order:

1. This document (`docs/benchmark/protocol.md`) — the agent contract.
2. `SKILL.md` — operational primitives, command reference, screen catalog,
   stall recovery. Read in full.
3. `docs/bridge-protocol-notes.md` — IPC quirks. Read in full.
4. `docs/data/README.md` — data schema and authority statement.

You **may** read on demand during play:

- `docs/data/eng/*.json` — stat lookups for cards, relics, potions,
  buffs, debuffs, characters, enemies. **Authoritative source.**
- `docs/cards-{character}.md`, `docs/relics.md`, `docs/potions.md`,
  `docs/buffs.md`, `docs/debuffs.md`, `docs/cards-index.md` — index files
  pointing into JSON.
- `docs/data/changelogs/*.json` — patch notes; useful if your training
  data predates the current game version.
- `HermesBridgeCode/BridgeCommandDispatcher.cs` and
  `HermesBridgeCode/BridgeStateExtractor.cs` — authoritative
  command/state shape, on demand when JSON or markdown are ambiguous.
- `AGENTS.md` — meta-instructions; redundant with this document but
  permitted.

You **may not** read anything else, including but not limited to:

- `docs/sessions/*` — historical session logs (other agents' play traces).
- `docs/handoffs/*`, `docs/handoff-replies/*` — out of scope.
- `docs/verified-flows/*` — playthrough narratives that may encode
  strategy lessons.
- `docs/gauntlet-findings.md` — removed from repo for trial-v0
  duration (see §Amendments, trial-v0.1). Does not exist on disk.
- `docs/autopilot-session-*.md` — prior gauntlet logs.
- `docs/reference-{ironclad,potions,relics}.md` — historical reference,
  may be stale; deferred to JSON entirely for trial-v0.
- `docs/benchmark/runs/*.md` — other agents' run records. You may write
  your own, you may not read others'.
- `docs/benchmark/blog-draft.md` — out of scope.
- Anything under `tools/`, `_stage_*/`, `_bmad-*`, project root files
  other than those listed above.
- Anything outside the repo working directory.

---

## Trial parameters

For the v0 trial, runs are configured along these axes:

| Axis | Trial-v0 values |
|---|---|
| **Character** | `IRONCLAD`, `SILENT`, `DEFECT`, `REGENT`, `NECROBINDER` |
| **Ascension** | `0` (only) |
| **Knowledge condition** | `A0-zero-shot` (only) |
| **Seed** | unseeded (game default — record the run-seed if exposed in `state.run`) |
| **Run-cap (commands)** | `500` |
| **Stall threshold** | `bridge_stall`: 30s no revision change after a command. `agent_stall`: 120s no `step_finish` event from the OpenCode session despite an actionable game state. Both halt the run as `halt_reason: stall`. Provider rate-limit pauses where the agent resumes without operator intervention are tracked separately as `rate_limit_pause` and are **not** terminal — see trial-v0.4 amendment. |
| **Halt conditions** | `GameOver`, `Victory`, run-cap reached, 5 consecutive `status=error` on distinct commands, stall, **terminal rate-limit from model provider** (session does not resume — `halt_reason: rate_limit`), manual abort by operator |
| **Mid-run model rotation** | **Prohibited.** If the assigned model hard-fails (provider returns terminal error or session does not resume after a reasonable pause) mid-run, the operator halts and logs `halt_reason: rate_limit`. Do **not** swap to a fallback model and continue — the run is over. A non-terminal rate-limit pause where the agent resumes on its own is logged in `## Notes for maintainers` as `rate_limit_pause` with the gap duration; the run continues. |
| **Session isolation** | One fresh OpenCode session per run (operator pre-flight enforced). No session state, file-system state, or memory store carries between runs. |

Each model is expected to complete **one run per character at A0** for the
v0 trial = 5 runs/model. Models under evaluation in trial-v0:

- `claude-opus-4.7` (frontier, control — closed-weights)
- `gpt-5.5` (frontier — closed-weights)
- `gemini-3.1-pro` (frontier — closed-weights)
- `glm-5.1` (frontier — open-weights)
- `deepseek-v3.5` (frontier — open-weights, free tier)

Total: 5 models × 5 characters = 25 runs. Lineup is 3 closed-weights vs.
2 open-weights to support the closed-vs-open frontier comparison; the
mid-tier hypothesis (frontier clears Act 1, mid-tier dies F1-4) is
deferred to trial-v1.

Optional add: 10 additional Opus 4.7 runs (2 per character, distinct
sessions) to characterize within-model variance for the control. Total
with add: 35 runs.

---

## Run record

Every run produces exactly one markdown file at
`docs/benchmark/runs/<run_id>.md`.

`<run_id>` format: `<YYYY-MM-DD>-<model_slug>-<character_lower>-run<NN>`
e.g. `2026-04-26-claude-opus-4.7-ironclad-run01`.

The run record **must** start with this YAML front-matter:

```yaml
---
run_id: 2026-04-26-claude-opus-4.7-ironclad-run01
spec_version: trial-v0
knowledge_condition: A0-zero-shot
bridge_version: v0.1.3
game_version: 0.104.0
model: claude-opus-4.7
model_provider: github-copilot
opencode_session_id: ses_xxxxxxxxxxxxxxxxxxxxxx
character: IRONCLAD
ascension: 0
seed: null
start_time_utc: 2026-04-26T18:00:00Z
end_time_utc: 2026-04-26T18:14:32Z
duration_minutes: 14.5
command_count: 287
ipc_error_count: 0
stall_count: 1
halt_reason: death          # one of: death | victory | runcap | error_streak | stall | rate_limit | manual
death_floor: 14             # null if victory
death_screen: Combat        # null if victory
death_cause: combat_misplay # see §Death-cause taxonomy below
victory_floor: null         # null if death; floor of final boss kill if win
boss_reached: act1_boss     # null | act1_boss | act2_boss | act3_boss | heart
final_hp: 0
final_gold: 247
tokens_in: null             # raw input tokens, OpenCode SQLite step-finish.tokens.input summed by session_id
tokens_out: null            # raw output tokens, .tokens.output summed
tokens_cache_read: null     # .tokens.cache.read summed (Anthropic cache reads ≈ 10% of new-input price)
tokens_cache_write: null    # .tokens.cache.write summed
tokens_reasoning: null      # .tokens.reasoning summed (reasoning models only)
tokens_total: null          # .tokens.total summed
cost_usd: null              # .cost summed (when populated by provider)
wall_seconds: null          # max(time_updated) - min(time_created) across session parts
step_finish_count: null     # number of step-finish parts in the session
---
```

After the YAML, free-text body sections:

```markdown
## Summary

One paragraph: what happened, why the run ended.

## Bridge findings

Anything that looked like a bridge/IPC bug. Format each as:

- **<short label>** at command id N, screen X, revision R.
  What I saw. What I expected. What I did to recover.
  trace.log excerpt:
  ```
  ...
  ```

If none: write "None observed."

## Decision log highlights

3-7 decisions worth recording: tough card-play forks, contested map
choices, Neow choice, key event, key shop. One-line per decision.

## Notes for maintainers

Anything actionable for the harness or this protocol. If none: omit.
```

### Death-cause taxonomy

Pick the **most proximate** cause; if ambiguous, pick the earliest in the
list:

- `bridge_stall` — bridge/IPC failure caused the death (handSelect trap,
  PlayCard misfire, etc.). Always counts as a finding.
- `combat_misplay` — wrong card order, wrong target, missed defense, etc.
  Combat-tactics error.
- `map_routing` — chose a node that was statistically wrong (elite at low
  HP with no recovery, skipped rest before boss, etc.).
- `reward_greed` — took a reward that hurt the deck (curse, bad card pick,
  potion when full).
- `neow_trap` — Neow choice put run in unwinnable state.
- `event_misread` — picked an event option whose downside wasn't read.
- `shop_misallocation` — gold spent on wrong purchase.
- `boss_underprepped` — deck/HP simply not ready for the act boss; no
  single proximate misplay.
- `unknown` — used only when nothing else fits; explain in §Summary.

### Bridge findings vs. strategic findings

Only **bridge findings** belong in §Bridge findings. Examples of bridge
findings:

- A command returned `ok=true` but no state change.
- A screen name not in `SKILL.md` §Screen catalog.
- A `handSelect.cards[]` shape that didn't match the documented schema.
- An IPC timeout, a malformed JSON response, a missing `result.json`.
- Any case where `state.json` revision advanced but the visible game state
  contradicted the new payload.

Strategy findings (e.g. "Ironclad with Inflame is strong on floor 4")
belong nowhere in the run record. They are out of scope.

---

## Scoring

For trial-v0, scoring is **descriptive, not aggregated**. We are not
producing a single SpireBench score yet; we are producing a distribution
of `death_floor` values per model, plus failure-mode breakdowns.

Reported per-model:

- Mean and median `death_floor` across the 5 character runs (or 25, with
  variance add).
- Win count (`halt_reason: victory`).
- IPC error rate (`ipc_error_count` summed / `command_count` summed).
- Stall rate (`stall_count` summed / `command_count` summed).
- Death-cause histogram.
- Token efficiency, if `tokens_in/out` were captured: tokens per floor
  cleared (= `tokens_total / death_floor` for losses, `/ victory_floor`
  for wins).

A model is considered to have **passed Act 1** if any of its 5 runs reaches
`death_floor >= 17` or wins. This is the v0 separation threshold between
"viable benchmark target" and "harness noise."

---

## Operator responsibilities

The human running the trial (not the agent) is responsible for:

### Pre-flight (before each run)

1. **Fresh OpenCode session.** Start the run in a brand-new `opencode`
   session. Do not resume a prior session, do not start in a directory
   that has prior session state cached.
2. **Sandboxed `opencode.json`.** The agent's `opencode.json` must have
   the `mempalace` MCP server **disabled** (commented out or removed),
   `webfetch` disabled, `google_search` disabled, `task` (sub-agent
   spawning) disabled, and any other MCP server other than what the
   bridge requires disabled. Use `docs/benchmark/opencode.benchmark.json`
   as a reference template; copy it over the maintainer's normal config
   for the duration of a trial run, with the normal config backed up.
3. **Clean filesystem state.** No untracked files in the working
   directory other than what the bridge runtime needs. `tools/_stream/`
   and similar tooling outputs from the maintainer's day-to-day use
   should be wiped or hidden.
4. **Fresh game launch.** Game restarted between runs (a stale
   `state.combat` from the prior run is documented behavior; cleanest
   to avoid it entirely).
5. **Bridge alive.** `tools/read-state.ps1` (or equivalent) returns
   valid JSON before issuing the first command.
6. **Clear `commands.json`.** The bridge can replay a stale command on
   game launch (see `bridge-protocol-notes.md`); overwrite
   `commands.json` with a benign payload before the agent starts.

### During the run

1. **No coaching.** Do not interject in the agent's session. Do not
   answer questions the agent asks. Do not show it any other run record
   or any external information.
2. **Halt only on hard failures.** Game crash, mod reload required,
   bridge process dead, OS-level interruption. A bridge bug the agent
   *can't* recover from is a halt; one it *won't* recover from is not —
   record the death.
3. **Rate-limit halt.** If the model provider rate-limits the agent
   mid-run, halt with `halt_reason: rate_limit`. Do **not** restart on
   a different model. Do **not** wait out the rate limit and resume.
   The run is over.

### Post-run record-keeping

The operator fills these fields in the YAML front-matter (the agent
does not have reliable access):

1. `opencode_session_id`, `start_time_utc`, `end_time_utc`,
   `duration_minutes`, `model`, `model_provider`.
2. `tokens_in`, `tokens_out`, `tokens_cache_read`, `tokens_cache_write`,
   `tokens_reasoning`, `tokens_total`, `cost_usd`, `wall_seconds`,
   `step_finish_count` — read directly from the OpenCode SQLite session
   DB at `~/.local/share/opencode/opencode.db`. Aggregate
   `part.data.tokens.{input,output,reasoning,total}` plus
   `tokens.cache.{read,write}` over all `step-finish` parts for the
   run's `session_id`. Helper:
   `tools/maintainer/get-session-tokens.ps1 -SessionId <ses_xxx>` outputs the
   nine YAML lines directly.
3. `command_count`, `ipc_error_count`, `stall_count` — count from
   `trace.log` or the agent's session transcript.
4. **Append a row to `docs/benchmark/runs.csv` via the helper.** Run

   ```
   tools\maintainer\append-run-csv.ps1 -RunId <run_id>
   ```

   The helper reads the run record's YAML front-matter, calls
   `get-session-tokens.ps1` to fill any null token / wall_seconds /
   cost_usd / step_finish_count fields from the OpenCode session DB,
   patches those values back into the front-matter (so the `.md` and
   `.csv` stay in lockstep), and appends a properly-quoted row to
   `runs.csv` in the exact column order of the existing header.
   The `seed` field is force-quoted to dodge spreadsheet
   scientific-notation rounding. Empty / null fields are written as
   a bare comma; `tools/spirebench-summary.py` treats empty cells as
   `NaN`. The helper refuses to append a duplicate `run_id`. Do **not**
   hand-edit `runs.csv` — column order drift is the most common
   source of bad rows.
5. **Enrich the record with `.run`-derived stats.** After the run
   ends, StS2 writes a save-game journal at
   `%APPDATA%\SlayTheSpire2\steam\<steam_id>\profile1\saves\history\<unix>.run`
   (UTF-8 pretty JSON, ~10–90 KB). This contains per-floor truth that
   the agent can't easily self-report (full card-pick / skip history,
   relic choices, rest-site choices, gold flow, damage taken,
   elites_fought, killed_by_encounter / killed_by_event, etc.). After
   appending the CSV row, run

   ```
   python tools\maintainer\parse-run-history.py --run-id <run_id> --write
   ```

   The parser matches the run record to its `.run` file by `seed`
   (preferred) or by `start_time_utc + character` (fallback for
   records whose seed was not surfaced by the agent), archives the
   raw `.run` next to the `.md` / `.jsonl` as
   `docs/benchmark/runs/<run_id>.run`, and patches 21 derived stat
   fields into the run record's frontmatter under the marker comment
   `# --- .run-derived stats (parse-run-history.py) ---`. The set of
   stat fields is documented at the top of the parser. After
   patching, run `python tools/maintainer/regenerate-runs-csv.py`
   to rebuild `runs.csv` with the extended schema. Both helpers are
   idempotent.

### Forbidden operator actions

- Coaching the agent during a run.
- Showing the agent any other agent's run record.
- Restarting a stalled run "to give it a fair shake" — log the stall and
  count it.
- **Sending a second message into a stalled OpenCode session** ("continue",
  "keep going", or re-pasting the agent-prompt). The second turn cannot
  honour the pre-flight `screen: MainMenu` check and will either misread
  it as terminal (run08) or pollute the trial with operator-driven
  recovery prompts. A stalled run is over.
- Swapping models mid-run.
- Re-using an OpenCode session across runs.
- Backfilling from training-data / web-search to "complete" a run record.

---

## Known mid-tier failure modes

These are documented from informal pre-trial gauntlet runs. They are
*expected* observations for the trial, not bugs. They are listed here so
the operator can categorize deaths quickly:

1. **`fight.ps1` instinct** — agent tries to write a combat driver script
   despite the SKILL.md prohibition. Almost always crashes inside its own
   loop (no per-tick state inspection). Halt and record as
   `death_cause: combat_misplay` with a §Notes for maintainers entry
   noting the SKILL.md violation.
2. **`enemy.index` confusion** — agent passes `targetIndex` from
   `enemy.combatId` or invents a numeric index. Card play silently
   misfires as self-play, agent doesn't notice.
3. **HandSelect trap** — Snap (Necrobinder) or Armaments (Ironclad) opens
   the modal; agent issues `PlayCard` against the unmodified hand,
   commands return `ok` but no revision advance. STALL.
4. **Reward greed at full inventory** — picks a potion reward when potion
   inventory is full. Returns `error` (after Rw1 fix); mid-tier agents
   sometimes don't read the error and re-issue.
5. **Map node off `available[]`** — agent reads the full map and tries to
   route N steps ahead, picking a node that isn't currently legal.
   Returns `error` or stalls.
6. **`Proceed` spam** — when stuck, agent issues `Proceed` repeatedly with
   no state inspection. Hits the 5-error halt or the run-cap.

---

## Trial freeze and amendment policy

This document is **frozen** as of 2026-04-26. Any change before all 25
trial-v0 runs are complete:

- Must be tagged with a `trial-v0.N` sub-version.
- Must be noted in the run record's `spec_version` field for any run after
  the change.
- Must not invalidate prior runs' comparability — i.e. if a change makes
  the task strictly harder or easier, prior runs are tagged "incomparable"
  rather than re-run.

After all trial-v0 runs are complete, this protocol may be revised freely
into trial-v1 for the next iteration.

### Amendments

**trial-v0.1** (2026-04-26, before run01 ships in the dataset):

- Bridge bumped to v0.1.4: `BridgeStateExtractor.ExtractRun` now appends
  one JSONL row to `%APPDATA%/SlayTheSpire2/hermesbridge/floor-history.jsonl`
  every time `state.TotalFloor` advances. Schema:
  `{t, floor, act, hp, maxHp, gold, deckSize, relicCount, potionCount, roomType}`.
  This is a passive observer — no agent-visible behavior change. Used by
  the post-trial analysis script for HP/gold/deck curves.
- Agent prompt hardened: `docs/benchmark/agent-prompt.md` now treats the
  run record as a terminating obligation with an explicit completion
  checklist. Halt-without-record is a benchmark failure and triggers
  re-attempt.
- `docs/gauntlet-findings.md` deleted from the repo for the duration of
  trial-v0. Pre-amendment evidence (run01 skipped its run record;
  run02's agent ignored the off-limits whitelist and appended a "Run
  29" entry to gauntlet-findings.md instead of writing
  `runs/<run_id>.md`) showed the file was an attractive nuisance even
  with an off-limits header. Removing it deterministically prevents the
  failure mode. The file is recoverable from git history
  (commit 9cc1dc4) and will be restored after trial-v0 completes.
- Pre-amendment runs (run01-run02 from 2026-04-26) are **discarded**, not
  tagged "incomparable", because the floor-history data is needed for
  the planned charts and reconstruction from `trace.log` would be lossy.
  Trial-v0 restarts from run01 once v0.1.4 ships.

**trial-v0.2** (2026-04-27, before run01 ships in the dataset):

- Bridge bumped to v0.1.5: `BridgeFloorHistory` now truncates a
  pre-existing `floor-history.jsonl` exactly once per process at
  startup (static constructor). Game restart between runs (already
  required by §Per-run setup) is now sufficient to guarantee a
  per-run file; the operator no longer has to delete the runtime
  file in the teardown checklist. No agent-visible behavior change.
- `agent-prompt.md` teardown checklist updated: snapshot step
  (copy `%APPDATA%/...` → `runs/<RUN_ID>.jsonl`) is now ordered
  **before** the game-restart step, and the explicit-delete step
  is removed. Snapshotting after restart would lose the data.
- `run-record-template.md` bumped: `spec_version` trial-v0.1 →
  trial-v0.2, `bridge_version` v0.1.4 → v0.1.5. No structural
  changes to the schema; trial-v0.1 and trial-v0.2 records are
  fully comparable.

**trial-v0.3** (2026-04-29, after run01-run07 ship; first run
covered: run08 `2026-04-28-gemini-3.1-pro-preview-regent-run08`):

- **Stall taxonomy split into `bridge_stall` and `agent_stall`.**
  The pre-amendment definition ("30s no revision change after a
  command") only covered the bridge-side case. Run07 + run08
  exposed an agent-side failure mode: gemini-3.1-pro-preview
  stopped *generating* mid-run while the bridge was healthy.
  - `bridge_stall`: 30s no revision change after a command issued.
    Same as before. Counts toward `stall_count`.
  - `agent_stall`: agent's OpenCode session has not produced a new
    `step_finish` event in **120s** despite the game state being
    actionable. Counts toward `stall_count`.
  - Both share `halt_reason: stall`. The taxonomy difference is
    recorded in `## Notes for maintainers`.
- **Stall handling formalised.** Stalls are terminal: the operator
  records the run as `halt_reason: stall` at the point of stall,
  with whatever `final_hp`/`final_gold`/`boss_reached` the bridge
  last observed. The operator does **not**:
  - send a "continue" or "keep going" message into the same OpenCode
    session (this contaminates the run with operator coaching);
  - inject the agent-prompt into the same session a second time
    (the new turn will misread mid-run pre-flight checks — this
    happened on run08, where a second turn read step 2's
    `screen: MainMenu` requirement and self-aborted at floor 19);
  - start a fresh OpenCode session and "continue" the run (counts
    as a new run, but the game state is not a clean `MainMenu`).
  Two stalls within a run are extraordinarily unlikely (the first
  is already terminal); the "after 2 occurrences" wording in the
  amendment options refers to the lifetime model-budget — i.e. if
  a model stalls on multiple distinct runs in succession, consider
  excluding it from the lineup and noting the exclusion in the
  trial summary.
- **Agent-prompt step 2 reworded** to remove the "halt if not
  MainMenu" loophole that run08's second turn fell into. Step 2
  now says: pre-flight is the operator's responsibility, agent
  observes `MainMenu` on first read or writes a stub record with
  `halt_reason: manual` describing what was observed instead. No
  recovery path for non-`MainMenu` state — that's a setup error.
- **Run08 disposition.** Run08 is preserved as
  `halt_reason: stall` despite the second-turn confusion that
  produced an initial draft labelling it `halt_reason: manual`.
  The first OpenCode turn (ses_226bcfd55ffe9hdrh7KvVM5XZK,
  2026-04-29T12:42:20Z–13:54:39Z, 350 commands, reached f19
  Act 2) is the canonical run. The second turn's "manual halt"
  draft was overwritten before commit.
- `tools/list-cards.ps1` field-name fix: `$c.cardType` →
  `$c.type` and `$c.energyCost` guard for negative sentinel
  values. The script was reading from a card schema that doesn't
  exist; the bridge serialises card type as `type`. Unrelated to
  the trial protocol but bundled into the same commit.
- No bridge bump, no template bump (`spec_version` stays
  trial-v0.2 — schema unchanged, only operator workflow tightened).

**trial-v0.4** (2026-04-30, after run08 ships and run09-run10 are
reserved-but-unfilled; first run covered: run11
`2026-04-30-gpt-5.5-ironclad-run11`):

- **`rate_limit_pause` formalised as non-terminal.** Run11 exposed
  a third stall-shaped failure mode that the trial-v0.3 taxonomy
  did not cover: the GitHub Copilot-hosted gpt-5.5 session paused
  for ~147.6 minutes between floor 43 and floor 44 (and a separate
  ~55.7 minute pause between floor 42 and floor 43), then **resumed
  on its own without operator intervention** and continued to
  act3_boss death at floor 50. This is mechanically distinct from
  an `agent_stall`:
  - `agent_stall`: agent has stopped generating and will not resume
    on its own; the operator must abandon. Terminal.
  - `rate_limit_pause`: provider has paused token issuance (rate
    limiting); the OpenCode session is still alive and will resume
    once the provider's rate-limit window clears. Non-terminal.
  The 120s `agent_stall` threshold remains correct for declaring
  a run dead in real-time. After 120s of no `step_finish`, the
  operator opens the OpenCode session and inspects: if the session
  shows a provider rate-limit message and is waiting, that is a
  `rate_limit_pause` and the operator **does not abandon**; if the
  session shows no progress and no rate-limit indicator, that is
  an `agent_stall` and is terminal per trial-v0.3.
- **Recording.** A `rate_limit_pause` is recorded in
  `## Notes for maintainers` of the run record with: pause
  duration, floor range over which it occurred, and a note that
  `wall_seconds` includes the pause. The `halt_reason` is whatever
  the run actually ended on (typically `death` or `victory`),
  **not** `rate_limit`. There is no separate halt_reason value for
  pauses that resolved.
- **Analysis impact.** `wall_seconds` for runs containing a
  `rate_limit_pause` is not directly comparable to runs without
  one. Analysis tooling and the trial summary should flag affected
  runs and exclude them from wall-clock means or report a
  pause-corrected wall-time alongside the raw value. Token counts,
  step_finish counts, and gameplay metrics are unaffected and
  remain comparable.
- **Run07 reclassification deferred.** Run07
  (`gemini-3.1-pro-preview-silent`, 0 tokens, 50 step_finish in
  3500s) is currently labelled `halt_reason: death` but exhibits
  pause-shaped data. Without provider-side telemetry it is not
  possible to retrospectively determine whether the gemini stall
  was a terminal `agent_stall` or an unrecoverable
  `rate_limit_pause` (where the provider pause exceeded the
  operator's patience). Run07 is left as-is; future gemini retries
  for the unfilled DEFECT and NECROBINDER cells should monitor
  for the same pattern.
- **Numbering policy clarified.** Run-id numbering does **not**
  need to be contiguous. Run09 and run10 remain reserved for the
  gemini DEFECT and NECROBINDER cells, planned as retries after
  the rest of the lineup completes. If gemini cannot complete
  those runs, the slots stay vacant or are filled by a different
  model with the run-id renumbered accordingly. Pandas/charts
  treat run-id as a string key; gaps are inert.
- **Run11 protocol violation noted.** Run11's agent had MemPalace
  and sub-agent tools exposed during the session, contrary to the
  benchmark's tool restriction. The run record's
  `## Notes for maintainers` flags this. Operators should verify
  `opencode.benchmark.json` is the active config before pre-flight.
  Run11 is preserved in the dataset because the gameplay decision
  log shows no spire2-specific external knowledge retrieval; the
  violation is a tool-exposure issue, not a knowledge-condition
  contamination.
- No bridge bump, no template bump (`spec_version` stays
  trial-v0.2 — schema unchanged, only operator workflow and
  taxonomy clarified).
