# NPlayerHand in-hand select — verified 2026-04-19

Evidence archive for the session that first verified live in-hand card
selection through `NPlayerHand.SelectCards` + Harmony postfix on
`RefreshSelectModeConfirmButton` / `AfterCardsSelected`.

## What was verified

Command ids 541–545 (see `trace.log`):

- **Burning Pact (SimpleSelect, min=max=1, prompt "Choose a card to Exhaust")**
  - `HandSelectCard {handIndex: 1}` on Greed → auto-completed, Greed exhausted,
    drew 2 cards, payload cleared.
- **Armaments (UpgradeSelect, min=max=1, prompt "Confirm Card to Upgrade")**
  - `HandSelectCard {handIndex: 2}` on Defend → selected but did NOT
    auto-complete despite `requireManualConfirmation=false` in prefs.
  - `HandConfirmSelect` → Defend → Defend+.
  - This is why `canAutoComplete` was later changed to exclude `UpgradeSelect`.

## Files

- `trace.log` — full session trace at archive time.
- `state-final.json` — last snapshot (Round 5 of dying Phrog+Wrigglers fight).
- `result-last.json` — response to id=551 (EndTurn).
- `godot.log` — Harmony/runtime log; useful for cross-referencing patch fires.

## Notes

No temp-file-lock incidents observed in this trace; Hermes's `AtomicFileWriter`
is preventative hardening, not a fix for anything witnessed here.
