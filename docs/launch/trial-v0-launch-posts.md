# trial-v0 launch posts

Internal staging doc. Three drafts for HN, Bluesky/Twitter, r/slaythespire2.
Hold publication until Casey Yano replies.

URL canonical: https://spirebench.com/blog/trial-v0-results

---

## 1. Hacker News (Show HN)

**Title** (max 80 chars, no clickbait, technical):

```
Show HN: SpireBench - we tested 5 frontier LLMs on Slay the Spire 2; all died
```

Alternate title if the above feels too "we":

```
Show HN: SpireBench - LLM benchmark using Slay the Spire 2 as the eval harness
```

**Body** (markdown, posted as a comment after the link if HN strips the body
on link posts):

```
SpireBench is an open-source benchmark where LLM agents play Slay the
Spire 2 autonomously through a file-IPC mod. The mod exposes game state
as JSON and accepts typed commands; no screen scraping, no memory hacking.

trial-v0: 25 runs, 5 models (GPT-5.5, Claude Opus 4.7, DeepSeek V4 Pro,
Gemini 3.1 Pro Preview, GLM-5.1) x 5 characters, Ascension 0, zero-shot,
no game-specific training data or retrieval.

Headline: zero victories. Median floor 17 (Act 1 boss). Deepest run was
GPT-5.5 on Ironclad reaching floor 50 (Act 3 boss) before dying to Test
Subject #C14. Second deepest was Claude Opus 4.7 at floor 33 - it
correctly planned the lethal turn against Knowledge Demon but
mis-projected end-of-turn block resolution by a single Frost-orb passive
trigger. A 2-HP arithmetic error in a calculation the agent couldn't see
in the state schema.

Why I built this: existing LLM benchmarks (SWE-bench, MATH, GPQA, Arena)
don't measure extended sequential decision-making under partial
information with irreversible commitments and no gradient signal. StS2
is ~50-100 consequential decisions per run with no partial credit and no
way to back out. That's exactly where current frontier LLMs should
struggle, and do.

The full writeup has per-model breakdowns, a death-cause taxonomy across
all 25 runs, token economics, the failure modes I think generalize
beyond this game, and a planned trial-v1 design (k=3 per cell, A0 vs
B0-priors, 150 runs):
https://spirebench.com/blog/trial-v0-results

Protocol, agent prompt, run records, and CSV are all open:
https://spirebench.com/protocol
https://spirebench.com/runs

Bridge mod (C# Harmony patch + file IPC):
https://github.com/hiKareeem/ClaudePlaysTheSpire  [REPLACE before posting]

Happy to discuss the eval design, the bridge architecture, or specific
runs. The Act-3 boss attempt is on video:
https://youtu.be/tMehXd7C-_o
```

**Posting checklist:**
- [ ] Confirm Casey Yano reply received (or 7d elapsed since April 26 stream)
- [ ] Replace `<org>` with the public GitHub org/user
- [ ] Post Tuesday-Thursday 8-10am Pacific (peak HN US engagement)
- [ ] Submit as a Show HN with the spirebench.com/blog/trial-v0-results URL
- [ ] Paste body as the first comment within ~2 minutes
- [ ] Be online for 4-6 hours after posting; reply to every top-level comment

**If asked the obvious questions:**
- "Why not SWE-bench?" - SpireBench measures different things; complementary, not competitive.
- "Why not just train on it?" - That's exactly what trial-v1 will measure (A0 vs B0-priors).
- "Did you cherry-pick?" - n=25 is the full corpus, every run published, including the embarrassing ones.
- "Mega Crit endorse this?" - Casey Yano said "no objection" on the April 26 dev stream; courtesy heads-up sent.

---

## 2. Bluesky / Twitter chart thread

7 posts. Each post stands alone; chart-per-post; thread for readers who want
the full arc. Bluesky character limit 300; Twitter 280. Drafted to fit both.

Lead image on post 1: `floor_reach_distribution.png` (most visceral).

### Post 1 (the hook)

```
We ran 5 frontier LLMs through Slay the Spire 2.
GPT-5.5, Claude Opus 4.7, DeepSeek V4 Pro, Gemini 3.1 Pro Preview, GLM-5.1.
25 runs, all 5 characters, zero-shot, Ascension 0.

Zero wins.
Median floor reached: 17 (Act 1 boss).

[chart: floor_reach_distribution.png]

Thread.
```

### Post 2 (the death heatmap)

```
Where they died, by floor.

The Act 1 boss (floor 17) is a wall.
Three models never cleared it on any character.
Two cleared it sometimes; one cleared it usually.
Nobody cleared Act 3.

[chart: death_heatmap.png]
```

### Post 3 (per-model)

```
Per-model best run:

GPT-5.5         floor 50 (Act 3 boss, Ironclad)
Claude Opus 4.7 floor 33 (Act 2 boss, Defect)
DeepSeek V4 Pro floor 23 (Act 2 elite, Necrobinder)
Gemini 3.1 PP   floor 19 (Act 2 floor 1, Ironclad)
GLM-5.1         floor 17 (Act 1 boss, Regent)

[chart: hp_curve_overlay.png]
```

### Post 4 (the GPT-5.5 deepest run)

```
GPT-5.5 reached the Act 3 boss on Ironclad. Floor 50.

It died to Test Subject #C14 - a fight where the boss scales over three HP bars. 
GPT-5.5 didn't recognize the mechanic and spent its big-damage turn padding block instead of finishing.

Video of the attempt: https://youtu.be/tMehXd7C-_o
```

### Post 5 (the Claude epistemic failure)

```
Claude Opus 4.7's deepest run died on the Act 2 boss because of a 2-HP
arithmetic error.

It correctly planned the lethal turn against Knowledge Demon. It did the
math. It just couldn't see one Frost-orb passive trigger in the state
schema, so its end-of-turn block projection was off by one tick.

Lethal-line arithmetic is its own failure mode. Adding it to trial-v1.
```

### Post 6 (token economics)

```
Token economics. Per-floor cost across the 25 runs:

[chart: tokens_cost_per_floor.png]

Floor cost grows roughly linearly with deck size. The deepest runs are
also the most expensive - GPT-5.5's floor-50 run cost ~5x more than a
median Act-1 death.

This matters for trial-v1: 150 runs at full depth would cost real money.
```

### Post 7 (the call)

```
Full writeup, per-model breakdowns, death-cause taxonomy, failure-mode
analysis, trial-v1 design:
https://spirebench.com/blog/trial-v0-results

Open data. Every run record, every transcript, the protocol, the agent
prompt:
https://spirebench.com/protocol
https://spirebench.com/runs

Findings? Hit me. trial-v1 designs improvements based on what trial-v0
exposed.
```

**Posting checklist:**
- [ ] Bluesky first (better organic reach for technical content as of 2026)
- [ ] Mirror to Twitter 6-12h later
- [ ] Charts: ensure all 6 PNGs are in `public/charts/` on deployed site
- [ ] Verify https://youtu.be/tMehXd7C-_o is unlisted->public before post 4
- [ ] Reply to QTs and comments for ~24h after posting

---

## 3. r/slaythespire2 (courtesy post)

Different audience than HN. Lean into "watch frontier AI fail at a game you
know well." StS players will spot agent mistakes faster than any reviewer.

**Title:**

```
We had 5 frontier LLMs play StS2 zero-shot. None won. Here's where they died.
```

**Flair:** Discussion (or whatever the sub's "stats/data" flair is)

**Body:**

```
Hey r/slaythespire2. I run an open-source LLM benchmark project called
SpireBench. Short version: a mod exposes the game's state as JSON, an LLM
agent reads it and sends commands back, no screen reading or memory hacks.

We just finished trial-v0: 25 runs across 5 frontier models (GPT-5.5,
Claude Opus 4.7, DeepSeek V4 Pro, Gemini 3.1 Pro Preview, GLM-5.1) on all
five characters, Ascension 0, zero-shot - meaning the model has no
StS-specific training data, no wiki access, no examples, no memory
between runs. Just the rules in the prompt and the live game state.

Zero victories.

Best results:
- GPT-5.5 on Ironclad reached floor 50 (Test Subject #C14, Act 3 boss).
  Video: https://youtu.be/tMehXd7C-_o
- Claude Opus 4.7 on Defect reached floor 33 (Knowledge Demon). It
  planned a correct lethal but mis-projected EOT block by 2 HP because
  one Frost orb passive triggered in a way it couldn't see in the state.

Common death causes:
- Get punished by the Act 1 boss (Hexaghost / Slime Boss / Champ)
- Greedy combat lines with no escape clause
- Bad pathing (ignoring elite-rich routes when behind on damage)
- Card-pick decisions that look fine in isolation but starve key turns

I'd love feedback from people who actually play this game. The full
writeup has per-character breakdowns, every death cause, full transcripts:

https://spirebench.com/blog/trial-v0-results

Run records (every decision the agents made):
https://spirebench.com/runs

This is open data, open code. Mostly posting because I think StS players
will spot patterns reviewers won't.

Not a leaderboard. Not promoting anything. Just the result of a benchmark
trial, presented honestly.
```

**Posting checklist:**
- [ ] Account has at least some sub-relevant comment history (avoid auto-spam-flag)
- [ ] Confirm flair is allowed; check sub rules for promotion/blogspam policy
- [ ] DO NOT cross-post to r/slaythespire (mod-removed prior; respect their rule)
- [ ] Be present for 4-6 hours to answer comments
- [ ] If a mod removes it: do not appeal aggressively; thank them and move on

---

## Skip list (do not post)

- r/slaythespire (mod-removed prior post; respect)
- r/ClaudeAI (mod-removed prior)
- r/LocalLLaMA (anti-spam-filtered no-karma account; defer)
- r/MachineLearning (heavy academic karma gate; defer)

## Direct outreach (separate, post-launch)

Send 24-48h after the public posts go up, only after at least one
external reference exists:

- METR (eval team) - "long-horizon eval that isn't saturating; data is open"
- Apollo - same framing, emphasize epistemic-failure case (run25)
- AI2 evals - same framing
- Mega Crit - courtesy heads-up (only if/when a victory ships)

Template (90 words):

```
Subject: SpireBench - long-horizon LLM eval, open data, no saturation yet

Hi [team] -

I run SpireBench, an open benchmark where LLM agents play Slay the
Spire 2 through a file-IPC mod. trial-v0 finished last week: 25 runs,
5 frontier models, zero victories, median floor 17.

The interesting cases are the deep ones - GPT-5.5 reached the Act 3
boss; Claude Opus 4.7 died on the Act 2 boss to a 2-HP arithmetic
error in a lethal projection. Both are documented end-to-end.

Open data, open protocol. If long-horizon evals fit your roadmap I'd
welcome feedback.

https://spirebench.com/blog/trial-v0-results

[name]
```
