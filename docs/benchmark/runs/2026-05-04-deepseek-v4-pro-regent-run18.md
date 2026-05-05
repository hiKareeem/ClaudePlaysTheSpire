---
run_id: 2026-05-04-deepseek-v4-pro-regent-run18
spec_version: trial-v0.2
knowledge_condition: A0-zero-shot
bridge_version: v0.1.5
game_version: 0.104.0
model: deepseek-v4-pro
model_provider: deepseek
opencode_session_id: ses_20c457cbfffeg4RBwYS83dUrjx
character: REGENT
ascension: 0
seed: 10778436819454081436
start_time_utc: 2026-05-04T12:04:00Z
end_time_utc: 2026-05-04T13:00:00Z
duration_minutes: 56.0
command_count: 132
ipc_error_count: 14
stall_count: 1
halt_reason: death
death_floor: 17
death_screen: Combat
death_cause: boss_underprepped
victory_floor: null
boss_reached: act1_boss
final_hp: 0
final_gold: 161
tokens_in: 132868
tokens_out: 54889
tokens_cache_read: 52215168
tokens_cache_write: 0
tokens_reasoning: 29587
tokens_total: 52432512
cost_usd: 8.0964
wall_seconds: 3508
step_finish_count: 394
# --- .run-derived stats (parse-run-history.py) ---
act_reached: 1
total_floors: 17
total_card_picks: 6
total_card_skips: 46
total_relics_picked: 3
total_potions_used: 5
total_potions_bought: 0
total_damage_taken: 138
total_gold_gained: 163
total_gold_spent: 101
total_gold_lost: 0
total_hp_healed: 138
elites_fought: 1
rests_taken: 4
shops_visited: 2
events_visited: 2
rest_choice_heal: 3
rest_choice_smith: 1
killed_by: ENCOUNTER.VANTOM_BOSS
was_abandoned: false
run_time_seconds: 3343
---

## Summary

The Regent run reached floor 17 (Act 1 boss Vantom) with a deck built around star economy: Celestial Might+, Falling Star, Crescent Spear, Glow, Hidden Cache, and the key power Child of the Stars. Neow choice was Precise Scissors (remove a Strike), which provided early deck thinning. The run cleared 6 normal combats, 1 elite (Byrdonis), 2 shops, 2 rest sites, 1 treasure room, and 2 events (Whispering Hollow + Aroma of Chaos). Key acquisitions included Gremlin Horn (energy + draw on kill), Comet (0E 33 damage Rare), and Child of the Stars (2 block per star spent). The deck reached the Act 1 boss at 59/75 HP but Vantom's 173 HP pool and Slippery x8 mechanic (negating the first 8 instances of HP loss) proved too much for the deck's damage output in the face of the boss's scaling attacks. The agent died at 5 HP on round 10 after exhausting all potions and running out of block.

## Bridge findings

- **Reflect card consistently unplayable** across multiple combats after being drawn. The card would show `isPlayable: false` even with sufficient energy. Attempted multiple times; commands returned `TryManualPlay returned false` errors. May be a card-specific bridge serialization issue or a game-mechanic precondition not reflected in `state.json`.
- **Falling Star `isPlayable` field unreliable:** Sometimes showed `isPlayable: false` when stars were available, then became playable on a subsequent re-read. Consistent with the known refresh-lag quirks in bridge-protocol-notes.md.
- **Potion-Shaped Rock from Petrified Toad** appeared in `run.potions[]` at the start of each combat as expected, and `UsePotion` on it returned `ok`, but the damage was not reflected in `state.combat.enemies[].currentHp` until after the next `PlayCard` state write (known refresh-lag, consistent with documented behavior).
- **STALL warning at StartRun (id=1409):** The first `StartRun` logged a stall (30s no revision change), but the result showed `status=ok` and screen progressed to `Room:Event` (Neow). The stall log showed `preRev=20` and the returned state had `revision=15`, which appears to be a revision counter reset on new run â€” bridge behavior but unexpected monotonicity break.
- **Crescent Spear+ `isPlayable: false` after card upgrades:** After upgrading Crescent Spear at Aroma of Chaos, the card showed `isPlayable: false` in subsequent combat despite sufficient energy. May be related to star-cost tracking across state refreshes.

## Decision log highlights

- **Neow: Precise Scissors (remove Strike)** â€” safe pick to thin basic Strikes early; the deck benefited from better card draw quality throughout the run.
- **Floor 3 Shop: Celestial Might (24g on sale) + Crescent Spear (51g)** â€” the Celestial Might proved to be a workhorse attack (6x4 after upgrade), and Crescent Spear provided star-scaling damage. Left 41g for future shops.
- **Floor 7 Shop: Crush Under (26g on sale)** â€” cheap AOE pick that proved useful against multi-enemy fights and provided Strength reduction.
- **Elite Byrdonis: Survived at 56 HP, took Comet (Rare) as card reward** â€” 0E 33-damage card with Weak/Vulnerable application, though its high star cost meant it was rarely playable in later fights.
- **Event: Aroma of Chaos â€” chose Upgrade (Maintain Control)** and upgraded Crescent Spear, but the upgrade may have been wasted as Crescent Spear+ showed `isPlayable: false` in subsequent combats.
- **Boss Vantom: Failed to clear Slippery fast enough** â€” the deck's multi-hit cards (Celestial Might+ at 4 hits) were helpful but not sufficient; the boss's 173 HP pool required more sustained damage than the deck could output while also blocking 21-26 damage attacks.

## Notes for maintainers

- `command_count` (132) and `ipc_error_count` (14) are agent best-estimates from counting `Send-BridgeCommand` calls and `CMD_ERROR` log lines during the session. The operator should validate against `trace.log`.
- `stall_count` of 1 reflects the StartRun stall warning (id=1409). The revision counter appeared to reset from 20 to 15 on new run start, which may be expected behavior but violated the assumed monotonicity.
- `duration_minutes` (56) is an approximation. The operator should fill `start_time_utc`, `end_time_utc`, and recalculate from the session logs.
- `model_provider: deepseek` is a guess based on the model slug `deepseek-v4-pro`. The operator should correct to the actual provider.
- The Reflect card bug (consistently unplayable across multiple combats) may warrant a bridge investigation. The card's star cost or other preconditions may not be serialized in `state.json`.
