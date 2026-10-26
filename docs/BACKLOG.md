# Staged backlog

Updated: 2026-10-25. **Execution resumed by the user for a complete MVP.**
Stage 1 is in progress; later stages are not yet implemented. Follow the
[implementation plan](superpowers/plans/2026-10-25-wildstone-mvp.md) and [handoff](HANDOFF.md).
Design documents and concept approvals do not complete implementation stages.
Acceptance detail belongs in the [canonical specs](README.md), not this checklist.

| Order | Next actionable stage | Evidence needed to advance |
| --- | --- | --- |
| 1 | Audit source reuse; plan and establish the separate Unity project; validate Fusion compatibility | Reuse inventory, pinned compatible versions and a compiling host/client target |
| 2 | Adapt the source motor to fixed ticks, restorable state and prediction | Movement parity and replay evidence under the foundation network profiles |
| 3 | Add managed identity, private sessions, teams, reconnect and host migration | Authenticated peer binding and recovery/failure cases with no duplicate seats |
| 4 | Build minimal combat fixtures: one hitscan, one projectile and required impulse/deflection interactions | Authoritative verdicts, deduplication, respawn and lag-compensation evidence |
| 5 | Add the moving capture zone and minimal arena routes | Host-owned scoring, moving-footprint coverage, uneven-team and recovery checks |
| 6 | Expand accepted roster and the two initial special consumables | Combat contract tests for each weapon/status, Blink and shield; readable counterplay |
| 7 | Produce three distinct maps, models, VFX and minimalist menus in the accepted art direction | Source reuse first; production assets, playable full flow, moving readability captures and measured graphics cost |
| 8 | Complete eight-player internet and endurance validation | External direct/relay sessions, host rotation, recovery and measured acceptance results |

Focused multi-client tests begin in the early stages; internet/load validation is
not a reason to defer networking failures until the end. Concept and spec refinement
may proceed alongside foundation work without starting arena asset production.
