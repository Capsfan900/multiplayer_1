# Staged backlog

Updated: 2026-10-27. **Execution resumed in `mvp-2`, with a native offline Windows build.**
Local implementation spans the stages below; full visual and internet acceptance
remain open. Follow the
[implementation plan](superpowers/plans/2026-10-25-wildstone-mvp.md) and [handoff](HANDOFF.md).
Design documents and concept approvals do not complete implementation stages.
Acceptance detail belongs in the [canonical specs](README.md), not this checklist.

Current work: finish packaged visual/input validation and integrate real networking.
Private GitHub delivery was requested separately: repository and manual Windows
preview workflow uploaded, and a clean-source Windows build published privately.
Runner registration was blocked by automatic approval review; manual setup and
first Actions build remain pending in [QA](QA.md). This does not complete any
multiplayer acceptance stage.
Native tests, content preparation, all three map bakes and Windows compilation pass;
this is offline evidence from before SDK import. Fusion 2.1.3 build 2390 is now
imported, its App ID is configured locally and native compilation passes. Live
service validation, verified identity configuration and external test machines
remain open. See [QA](QA.md).

| Stage | Observed local progress; remaining gate |
| --- | --- |
| 1 | Native project/offline Windows build verified; real Fusion imported and compiles with local App ID; service/runtime compatibility pending. |
| 2 | Native motor/local replay passes; actual Fusion wire mapping and movement behaviour added; real client reconciliation and network profiles pending. |
| 3 | Private runner lifecycle and recoverable PlayFab client added; provider setup/live authentication, host seat binding, lobby and whole-match recovery pending. |
| 4 | Native hitscan/projectile/Blink/cover fixtures pass; real rewind and online verdicts pending. |
| 5 | Three maps and zone rules validated locally; online scoring/recovery and fairness measurement pending. |
| 6 | Full roster and two consumables authored/tested locally; moving counterplay/readability acceptance pending. |
| 7 | Editable models, native maps/lightmaps, menus and offline build exist; full packaged visual/feel/profile pass pending. |
| 8 | Real external-network, eight-machine, endurance and performance acceptance not run. |

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

Focused multi-client tests begin as soon as the SDK and service configuration are
available; internet/load validation is not a reason to defer networking failures.
D018 permits independent rules, assets and menus while user-dependent provisioning
is pending, with network acceptance kept explicitly open.
