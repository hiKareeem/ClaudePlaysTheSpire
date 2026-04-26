# Blog draft outline — "Slay the Spire 2 as an LLM agent benchmark"

> **Status:** Draft outline. Body to be written *after* trial-v0 runs are
> complete (target: 25 runs, optionally 35). This file is the
> commit-yourself-now-to-the-claim document. If by the end of the trial
> we can't make these claims honestly, the post doesn't ship in this
> shape.

---

## Target venues

Primary: personal blog / GitHub repo README, syndicated to:

1. **Hacker News** — title: *"Slay the Spire 2 as an LLM agent benchmark"*.
   Tuesday-Thursday morning ET. Body should lead with the chart, link to
   repo at the bottom.
2. **r/LocalLLaMA** — title: *"Frontier vs mid-tier: I had 5 LLMs play
   Slay the Spire 2 to compare agentic capability"*. Text post; GitHub
   link in first comment.
3. **r/MachineLearning [R]** — only if results are clean and the post is
   structured as a methods paper. Otherwise skip.
4. **r/slaythespire** — different framing: *"I built an autoplay bridge
   for StS2 and ran a bunch of AIs through it — here's what they did
   wrong"*. Game-community angle, leads with the funny failure clips.
5. **arxiv preprint** — only after a second iteration with seeded runs,
   variance characterization, and at least 100 runs total.

Stagger the four reddits by 5-7 days each.

## The thesis

One sentence: **Long-horizon, hidden-information, deterministic-IPC video
games are an underused class of LLM agent benchmark, and Slay the Spire 2
is unusually well-suited to the role because of (compatibility constraints
that produced HermesBridge).**

Three claims the post must back with data:

1. **Frontier vs mid-tier separation is real and large.** Frontier models
   clear Act 1 routinely; mid-tier models die in the first 4 floors.
   Quantify with `death_floor` distribution across 25 runs.
2. **The failure modes are categorically different, not quantitatively
   different.** Mid-tier deaths cluster on `bridge_stall` and
   `combat_misplay`. Frontier deaths cluster on `boss_underprepped` and
   `map_routing`. Quantify with the death-cause histogram.
3. **The benchmark is contamination-resistant by construction.** StS2 is
   in active beta — v0.103 → v0.104 changed 308 game entries in one
   patch. Any benchmark calibrated against today's data is automatically
   re-randomized on the next patch. Show the changelog as evidence.

## Outline

### 1. Hook — one screenshot, one chart

- A clip / screenshot of a frontier model playing through a tough turn
  (with reasoning visible). Caption: "Claude Opus 4.7 deciding whether
  to spend its last energy on Defend or Inflame on floor 14."
- One bar chart: `death_floor` median per model, error bars. Five bars,
  ordered worst to best. Title: "Median floor reached, 5 runs per model,
  Slay the Spire 2 A0."

If we don't have a striking visual at the end of the trial, the post
isn't ready.

### 2. Why agentic benchmarks are stuck

~300 words. Make these specific points:

- SWE-bench Verified is at 70%+ for frontier; signal saturating.
- AgentBench / TerminalBench: synthetic tasks, low ecological validity.
- Most benchmarks ship without a real harness — they wrap a sandbox and
  call it agentic. The harness *is* the hard part.
- Long horizons (>500 tool calls) are basically untested.
- Hidden-information games would solve all of this — and nobody is
  shipping public benchmarks built on them.

### 3. Why Slay the Spire 2

~400 words. Lift from `protocol.md` §"Why HermesBridge is unusually good
benchmark substrate" / the table I wrote earlier in chat:

| Property | Why it matters |
|---|---|
| Hidden info (card draws, intent rolls) | No memorization shortcut |
| Stochastic | Forces real decisions, not playbook execution |
| Long horizon (200-3000 tool calls) | Tests compaction-survival |
| Real-time-ish state (refresh lags) | Forces observation, not just planning |
| Composable difficulty (A0 → A20) | Continuous curve, not pass/fail |
| Objective scoring | No LLM-judge needed |
| Active beta (v0.103 → v0.104 changed 308 entries) | Built-in contamination resistance |
| Deterministic IPC harness already exists | The hard part is done |

### 4. The harness — HermesBridge

~300 words. Brief tour:

- Mod injects into Godot game runtime, exposes `state.json`, accepts
  `commands.json`, returns `result.json`. Atomic file writes, monotonic
  revision counter.
- ~60 commands covering all in-game decisions.
- Stress-tested across 16h+ live gauntlet streams; quirks documented in
  `bridge-protocol-notes.md` and `SKILL.md`.
- Permission-clean: ATTRIBUTION, spire-codex granted use.
- Link to the repo here (not in the HN title).

One paragraph on the per-tick discipline rule and *why* it's load-bearing.
This is the punchy methodological point: scripted agents fail
catastrophically because the harness is genuinely real-time.

### 5. The trial

~400 words.

- 5 models × 5 characters × A0 = 25 runs (+10 Opus variance add).
- Frozen reading list (`docs/benchmark/protocol.md`), no other agent's
  run record visible to any agent.
- One run per character per model. Halt on death/win/runcap. No coaching.
- Primary metric: `death_floor`.
- Secondary: command count, IPC error rate, stall rate, death-cause
  histogram.

Show the protocol file. Show the run-record schema. Transparency is the
selling point — anyone with the repo can replicate.

### 6. Results

~500 words + 2-3 figures.

- **Figure 1**: `death_floor` per model (bar chart).
- **Figure 2**: Death-cause histogram, stacked by model.
- **Figure 3** (if available): tokens-per-floor-cleared (efficiency).
- A table of every run with `run_id`, `model`, `character`, `death_floor`,
  `halt_reason`, `death_cause`, link to run record.

Three honest paragraphs:
- What the data shows.
- What it doesn't show (small sample, single ascension, no seed control).
- One unexpected finding (there's almost always one — could be a
  model-specific failure mode, a character-specific edge case, etc.
  Leave the placeholder in until the data is in).

### 7. Failure-mode tour

~500 words. Three subsections, one per illustrative failure:

- **The fight.ps1 instinct** — show a real transcript of a mid-tier model
  reaching for a script despite explicit prohibition. Quote the prompt.
- **The handSelect trap** — show a real STALL transcript. Explain why
  a non-frontier model can't escape it.
- **The boss-under-prep death** — show a frontier model losing on the
  Act 1 boss because it took a bad relic and an extra curse. The
  *interesting* failure: the model played correctly given its deck; the
  deck was the mistake, two acts earlier.

This is the section that gets shared. It's the "human-readable" product
of a benchmark that scores numerically.

### 8. What this isn't

~200 words. Caveats up front:

- Not a saturated benchmark — frontier doesn't get 100%; we're starting
  in the easy end and we'll add A20 later.
- Not seeded — the same model can get different floors on identical
  inputs. The signal is in the distribution, not any single run.
- Small sample. 5 runs/model. Statistical confidence is "hand-wavy."
- StS2 is in beta. The benchmark may break with the next patch. We will
  re-run the trial on every minor version and publish patch deltas.
- Token cost is non-trivial. A full run costs ~$X for Opus, ~$Y for GPT,
  ~$Z for Gemini. Not free to run.

### 9. What's next

~200 words.

- A20 tier (currently no model is expected to win — that's the point).
- Seeded runs for variance reduction.
- Multi-agent: coach mode, head-to-head.
- Per-tick reasoning corpus released as a HuggingFace dataset.
- Submission API: send us your model endpoint, we'll run the trial.
- Spin out `spirebench/` as a clean repo separate from HermesBridge.

### 10. Acknowledgments + repo

- spire-codex (peter / ptrlrd).
- Mega Crit for tolerating community tooling.
- Repo link.
- "PRs welcome on the protocol — open an issue if you find a way to
  game it."

---

## Things to write *before* publishing

1. The harness page in the repo README — what it is, how to install,
   how to run a single benchmark run yourself.
2. A "submit your model" form (Google Form is fine for v0).
3. The HuggingFace dataset of trial-v0 transcripts (anonymized if needed).
4. A static leaderboard page — even if it's just a Markdown table for
   v0, the URL needs to exist.

## Things that will probably bite

- Mega Crit reaction. Reach out *before* publishing. Frame as research
  partner, not scrape.
- Models the post discusses by name — make sure the comparison is fair
  (same prompt, same toolset, same model versions documented).
- "But you didn't seed the runs!" — yes, we know; address it head-on
  in §8.
- "But Claude is just better at writing PowerShell!" — partially true;
  could be a confound. Mention. Future work: harness shimmed in Python.

## Style notes

- No marketing language. No "revolutionary." No "groundbreaking."
- Past tense for results, present tense for the harness.
- Charts must have axis labels and units. No screenshots of unlabeled
  matplotlib defaults.
- Failure-mode anecdotes get one paragraph each. Don't make this post
  3000 words.
- Total target: 2000-2500 words + 3 figures + 1 table.
