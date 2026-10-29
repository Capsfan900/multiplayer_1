# Staged backlog

Updated: 2026-10-28. **Execution resumed in `mvp-2`, with a Photon-only Windows playtest.**
Local implementation spans the stages below; full visual and internet acceptance
remain open. Follow the
[implementation plan](superpowers/plans/2026-10-25-wildstone-mvp.md) and [handoff](HANDOFF.md).
Design documents and concept approvals do not complete implementation stages.
Acceptance detail belongs in the [canonical specs](README.md), not this checklist.

Delivery checkpoint: current-state snapshot `e9e091f` is published as public preview
`preview-20261028-e9e091f` at the user's request. Anonymous download and checksum verification passed. Build/startup/package evidence is in
[QA](QA.md). Ongoing development and remaining acceptance stages stay open.

Current work: playtest D026 expanded laser arsenal/balance and D025 first-person legs, arm/hand animation and pronounced
slide feel, then finish external-network acceptance and packaged input checks.
D026 adds laser pistol/shotgun/sniper pickups on all maps, rapid starter fire and distinct weapon presentation; native155 EditMode/51 PlayMode plus35 final checks pass.
D025 implementation passes 35 focused native checks, thirteen inspected pose
captures and fresh combat/reconnect regressions. Same-PC separate-process Photon gameplay, reconnect,
movement/fire deduplication, controlled stationary hits, late admission, eight-player
capacity, graceful migration and abrupt host-crash recovery pass.
The latest polish adds inspected static menu/all-map captures, focused UI/recovery
regressions and a five-minute eight-process gameplay soak with Windows memory
observations. Physical input, moving readability and long endurance remain open.
Private GitHub delivery was requested separately: repository and manual Windows
preview workflow uploaded, and a clean-source Windows build published privately.
Runner registration was blocked by automatic approval review; manual setup and
first Actions build remain pending in [QA](QA.md). This does not complete any
multiplayer acceptance stage.
Native tests, content preparation, all three map bakes and Windows compilation pass;
this is offline evidence from before SDK import. Fusion 2.1.3 build 2390 is now
imported, its App ID is configured locally and native compilation passes. Real
Photon tests now include separate host/client processes and graceful migration.
Abrupt-crash recovery also passes. External test machines and performance acceptance remain
open. See [QA](QA.md).

| Stage | Observed local progress; remaining gate |
| --- | --- |
| 1 | Native project/Windows build verified; real Fusion configured; separate-process private Host/Join/lobby/gameplay passed; external runtime acceptance pending. |
| 2 | Native motor, replay and shared-orb state checks pass; actual Fusion movement/world rollback implemented; remote reconciliation and network profiles pending. |
| 3 | Photon guest admission, lobby/team/load, late admission, retained-seat reconnect, eight-player capacity, graceful migration and abrupt-crash recovery pass through real Photon using separate processes; external acceptance remains open. |
| 4 | Host match/combat state and SDK historical hitscan integrated; native fixtures, real two-process duplicate-fire/events and controlled stationary hit replication pass; moving-target/adverse-network acceptance remains open. |
| 5 | Three maps and zone rules validated locally; online scoring/recovery and fairness measurement pending. |
| 6 | Full roster and two consumables authored/tested locally; moving counterplay/readability acceptance pending. |
| 7 | Editable models, native maps/lightmaps and polished menus/HUD are packaged; sixteen static menu/all-map captures inspected. Physical input, moving readability, feel and profiles remain pending. |
| 8 | Real external-network, eight-machine, endurance and performance acceptance not run. |

| Order | Next actionable stage | Evidence needed to advance |
| --- | --- | --- |
| 1 | Audit source reuse; plan and establish the separate Unity project; validate Fusion compatibility | Reuse inventory, pinned compatible versions and a compiling host/client target |
| 2 | Adapt the source motor to fixed ticks, restorable state and prediction | Movement parity and replay evidence under the foundation network profiles |
| 3 | Add temporary guest identity, private sessions, teams, reconnect and host migration | SDK peer binding, held-seat credential protection and recovery/failure cases with no duplicate seats |
| 4 | Build minimal combat fixtures: one hitscan, one projectile and required impulse/deflection interactions | Authoritative verdicts, deduplication, respawn and lag-compensation evidence |
| 5 | Add the moving capture zone and minimal arena routes | Host-owned scoring, moving-footprint coverage, uneven-team and recovery checks |
| 6 | Expand accepted roster and the two initial special consumables | Combat contract tests for each weapon/status, Blink and shield; readable counterplay |
| 7 | Produce three distinct maps, models, VFX and minimalist menus in the accepted art direction | Source reuse first; production assets, playable full flow, moving readability captures and measured graphics cost |
| 8 | Complete eight-player internet and endurance validation | External direct/relay sessions, host rotation, recovery and measured acceptance results |

Focused multi-client tests begin as soon as the SDK and service configuration are
available; internet/load validation is not a reason to defer networking failures.
D018 permits independent rules, assets and menus while user-dependent provisioning
is pending, with network acceptance kept explicitly open.
