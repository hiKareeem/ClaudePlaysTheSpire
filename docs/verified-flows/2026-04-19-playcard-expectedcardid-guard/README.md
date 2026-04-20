# Verified: PlayCard expectedCardId Guard (2026-04-19)

Verifies Hermes's fix for bug #3 (stale handIndex) from the 2026-04-19 full bug triage handoff.

## Setup

Fresh Ironclad run, turn 1 of first combat (Fuzzy Wurm Crawler, 56 HP). Starting hand:

| handIndex | card |
|---|---|
| 0 | Strike |
| 1 | Defend |
| 2 | Defend |
| 3 | Strike |
| 4 | Strike |

## Test 1 — Match path (command 863)

Command:
```json
{"id": 863, "command": {"type": "PlayCard", "handIndex": 0, "expectedCardId": "CARD:STRIKE_IRONCLAD", "targetIndex": 0}}
```

Result:
```
status: ok
message: played Strike
```

Post-state: energy 3→2, enemy HP 56→50 (6 dmg ✓), hand compacted to 4 entries, handIndex reassigned starting at 0.

## Test 2 — Mismatch path (command 864)

After hand compacted, handIndex 0 is now Defend. Re-sending with the **same** `expectedCardId: "CARD:STRIKE_IRONCLAD"` (as would happen if a controller replayed a stale command or didn't refresh its view of the hand):

Command:
```json
{"id": 864, "command": {"type": "PlayCard", "handIndex": 0, "expectedCardId": "CARD:STRIKE_IRONCLAD", "targetIndex": 0}}
```

Result:
```
status: error
message: handIndex 0 mismatch: expected card id CARD:STRIKE_IRONCLAD, live card is CARD:DEFEND_IRONCLAD
```

Post-state (unchanged, confirming no side-effect):
- energy: still 2/3
- enemy HP: still 50
- hand count: still 4

## Verdict

✅ Guard works as designed. Diagnostic message names both expected and live card ids, preserving enough information for the controller to resync. No silent wrong-card plays. No state mutation on mismatch.

## Controller guidance

Any PlayCard dispatcher should:
1. Always include `expectedCardId` (or `expectedTitle`) matching the card it *intends* to play.
2. On mismatch error, re-read `state.combat.hand.cards[]` and recompute the handIndex for the desired card before retrying.
3. Treat a mismatch error as a consistency bug in the controller, not a game failure.
