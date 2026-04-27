---
run_id: YYYY-MM-DD-<model_slug>-<character_lower>-runNN
spec_version: trial-v0.2
knowledge_condition: A0-zero-shot
bridge_version: v0.1.5
game_version: 0.104.0
model: <model_slug>            # claude-opus-4.7 | gpt-5.5 | glm-5.1 | gemini-3.1-pro | deepseek-v3.5
model_provider: <provider>     # github-copilot | openai | zai | google | deepseek (or openrouter for the last 3)
opencode_session_id: ses_xxxxxxxxxxxxxxxxxxxxxx
character: IRONCLAD            # IRONCLAD | SILENT | DEFECT | REGENT | NECROBINDER
ascension: 0
seed: null
start_time_utc: YYYY-MM-DDTHH:MM:SSZ
end_time_utc: YYYY-MM-DDTHH:MM:SSZ
duration_minutes: 0.0
command_count: 0
ipc_error_count: 0
stall_count: 0
halt_reason: death             # death | victory | runcap | error_streak | stall | rate_limit | manual
death_floor: null
death_screen: null
death_cause: null              # see protocol.md §Death-cause taxonomy
victory_floor: null
boss_reached: null             # null | act1_boss | act2_boss | act3_boss | heart
final_hp: null
final_gold: null
tokens_in: null                # fill from tools/get-session-tokens.ps1
tokens_out: null
tokens_cache_read: null
tokens_cache_write: null
tokens_reasoning: null
tokens_total: null
cost_usd: null
wall_seconds: null
step_finish_count: null
---

## Summary

<One paragraph: what happened, why the run ended.>

## Bridge findings

<Per protocol.md §Bridge findings vs. strategic findings.
 If none observed: write "None observed.">

## Decision log highlights

<3-7 bullet points covering tough card-play forks, contested map choices,
 Neow choice, key event, key shop. One line each.>

## Notes for maintainers

<Optional. Anything actionable for the harness or this protocol.
 Omit the section entirely if there's nothing to add.>
