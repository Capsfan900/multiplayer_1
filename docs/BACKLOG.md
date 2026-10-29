# Staged backlog

Updated: 2026-10-29. **Execution resumed in `mvp-2`, with a published Photon-only Windows preview.**
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

The implementation stages above are substantially authored. The following is the
active acceptance backlog, replacing the old bootstrap implementation order.

| Order | Next actionable work | Evidence needed to advance |
| --- | --- | --- |
| 1 | Reproduce user-reported disconnect/menu return after joining and selecting a team | Other player confirmed in Texas on another ISP; exact builds/error wording still needed. Actual four-team switching diagnostic added; same-PC success is not a reproduction of the external symptom |
| 2 | Extend live combat acceptance to moving targets, cover and projectiles | Three-laser stationary production pickup/equip/fire RPC verdicts now pass; remaining cases must preserve authoritative ammo/damage/event identities |
| 3 | Accept moving gameplay/remote weapon readability | Remote shot/reload presentation and lifecycle/dedup checks now pass; moving first-/third-person captures and human aim/slide/audio acceptance remain |
| 4 | Exercise full standalone loop and map traversal | All maps/weapons/items, UI/cancel/retry, death/respawn, zone completion, pickup reachability and route fairness |
| 5 | Measure movement/combat under adverse network conditions | Host plus two clients, moving targets/cover/projectiles, prediction corrections and authority/recovery under canonical profiles |
| 6 | Add measured performance and endurance evidence | Frame/tick/bandwidth/allocation measurements, separate-machine eight-player matrix, 30-minute profiles, two-hour soak and 100 reconnect cycles |
| 7 | Complete independent-network acceptance | At least two external networks, direct/relay paths, rotate hosts, graceful/crash recovery; capture exact builds and logs |

User feedback on 2026-10-29: the game works and feels "pretty good", with a few
connection issues at the beginning. Follow-up identifies join/team selection and
another player in Texas on a different ISP. Builds and exact visible symptom remain
unconfirmed; this is human external-play evidence, not completion of the external matrix.
The prior firewall inspection found application exceptions for old diagnostic
players. System firewall changes are separate from gameplay work and remain unapplied.

Focused multi-client tests begin as soon as the SDK and service configuration are
available; internet/load validation is not a reason to defer networking failures.
D018 permits independent rules, assets and menus while user-dependent provisioning
is pending, with network acceptance kept explicitly open.
