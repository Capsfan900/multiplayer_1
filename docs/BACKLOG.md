# Staged backlog

Updated: 2026-10-31 (feel pass). **Execution resumed in `mvp-2`, with a published Photon-only Windows preview.**
Local implementation spans the stages below; full visual and internet acceptance
remain open. Follow the
[implementation plan](superpowers/plans/2026-10-25-wildstone-mvp.md) and [handoff](HANDOFF.md).
Design documents and concept approvals do not complete implementation stages.
Acceptance detail belongs in the [canonical specs](README.md), not this checklist.

Delivery checkpoint: current-state game source `190bb5e` (health packs, D040 playtest
fixes, zone waypoint) is packaged as public preview `preview-20261031-190bb5e` at the
user's request. [QA](QA.md#2026-10-31-current-state-public-preview-release) records the
build, checks and limits. Later work and remaining acceptance stages stay open.

Current work (2026-10-30): user playtest of the D028–D031 quality pass. Its
integration evidence is complete in [QA](QA.md#2026-10-30-quality-pass-integration-d028d031):
- native full suites;
- Photon gunplay at baseline and adverse150, and core regressions;
- four packaged visual modes;
- a same-PC 1080p frame-time sample.
The 2026-10-30 presentation follow-up has its own evidence in
[QA](QA.md#2026-10-30-presentation-follow-up-spawn-view-shot-reads-hud-slide-body):
spawn facing, shot reads, the HUD, the laser kit and the slide body. D032 music
(ACE-Step loops, packaged streaming verified, not yet heard) is in
[QA](QA.md#2026-10-30-music-ace-step-chill-house-loops-d032). D033 recorded weapon and
movement audio (packaged playback verified, not yet heard) is in
[QA](QA.md#2026-10-30-recorded-weapon-and-movement-audio). D034 maps, weapon energy
art and fire feedback (packaged captures verified, not yet played) are in
[QA](QA.md#2026-10-30-maps-weapons-and-fire-feedback). D036 online held-fire cadence
(two-process gunplay verified, not yet played) is in
[QA](QA.md#2026-10-30-online-held-fire-cadence-d036). The user-reported slide bug
(own body in view while sliding) is fixed by hiding the visible legs during slides
([QA](QA.md#2026-10-30-no-legs-in-view-while-sliding)). D037 Push Lens deflection
passes a real two-process check
([QA](QA.md#2026-10-30-push-lens-deflection-d037)), and rewind age is now measured
([QA](QA.md#2026-10-30-measured-rewind-age)).
Human feel, balance and in-motion team recognition remain the gate.
Earlier: playtest D026 expanded laser arsenal/balance and D025 first-person legs, arm/hand animation and pronounced
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
| 3 | Photon guest admission, lobby/team/load, late admission, retained-seat reconnect, same-room online rematch, eight-player capacity, graceful migration and abrupt-crash recovery pass through real Photon using separate processes; external acceptance remains open. |
| 4 | Host match/combat state and SDK historical hitscan integrated; native fixtures, real two-process duplicate-fire/events and controlled stationary hit replication pass; moving-target/adverse-network acceptance remains open. |
| 5 | Three maps and zone rules validated locally; online scoring/recovery and fairness measurement pending. |
| 6 | Full roster and two consumables authored/tested locally; moving counterplay/readability acceptance pending. |
| 7 | Editable models, native maps/lightmaps and polished menus/HUD are packaged; sixteen static menu/all-map captures inspected. Physical input, moving readability, feel and profiles remain pending. |
| 8 | Real external-network, eight-machine, endurance and performance acceptance not run. |

The implementation stages above are substantially authored. The following is the
active acceptance backlog, replacing the old bootstrap implementation order.

| Order | Next actionable work | Evidence needed to advance |
| --- | --- | --- |
| 1 | Reproduce user-reported disconnect/menu return after joining and selecting a team | Other player confirmed in Texas on another ISP; exact builds/error wording still needed. Actual four-team switching passes. New local pre-game Requested disconnect lacked host admission-rejection detail. Hosts now log a reason-only `WILDSTONE HOST REMOVED PEER` line for every removal ([QA](QA.md#2026-10-30-host-removal-reasons)); five local Photon runs did not reproduce it. Next: the host's Player.log from the next failing external session. It is not confirmed as the same external symptom. Reconnect cycles then found and fixed a join race: the SDK can report a peer before its user ID, and the host removed it as `invalid-peer` ([QA](QA.md#2026-10-30-reconnect-cycles-and-late-peer-identity)). It is a candidate cause of the startup disconnect, unconfirmed |
| 2 | Extend live combat acceptance to moving projectile counterplay, deflection and measured rewind | Pulse and grenade flight/blast/cover plus disc ricochet/damage deduplication and stun timing pass baseline/adverse150; three moving lasers pass regional80/adverse150. Adverse150 laser moving-cover shotgun misses trace to the 150 ms rewind cap ([QA](QA.md#laser-moving-cover-investigation)). Rewind age is now measured: requested view age is about 80 ms on the same PC, 184 ms mean at regional80 and 283 ms at adverse150, so the 150 ms cap clamped every regional80 shot ([QA](QA.md#2026-10-30-measured-rewind-age)). D040 raised it to 250 ms as a proposal for the user to tune: regional80 shots are no longer clamped, and adverse150 shots clamp at 250 ms against about 255–293 ms requested ([QA](QA.md#2026-10-31-playtest-fixes-d040)). D037 Push Lens deflection passes host- and client-push online at baseline/adverse150 ([QA](QA.md#2026-10-30-push-lens-deflection-d037)). A host-side stun probe shows the stunned target's replicated position holding under forward input, then moving once released; a predicting client target, natural moving counterplay and the full latency matrix remain. Under adverse150 loss a resent attack RPC can arrive past the 150 ms window and is dropped; the smoke fixtures now press again ([QA](QA.md#2026-10-30-smoke-harness-peer-reads-stun-probe-and-late-attacks)) |
| 3 | Accept moving gameplay/remote weapon readability | Continuous first-person starter slide/fire/reload sample passes and 32 frames inspected; remote shot/reload checks pass. `-Firefight` frame sequences of four hitscan headshots on a strafing soldier and an incoming sniper rail are inspected ([QA](QA.md#2026-10-30-firefight-frame-sequences)); the grenade in hand, heavier sniper rail and crescent sword slash are captured ([QA](QA.md#2026-10-30-weapon-read-fixes-grenade-in-hand-sniper-rail-sword-slash-title-fit)). Natural first-/third-person movement, other weapon transitions and human aim/slide/audio acceptance remain |
| 4 | Exercise full standalone loop and map traversal | Plain-movement reachability of every pickup and zone point from every start now gates the arena build, and per-team walking distances are measured ([QA](QA.md#2026-10-30-map-traversal-and-travel-distance)). Outer walls are now out of jump reach (map revision 4, 8 m). Terrace Steps' northern starts walk 29-55% farther than team 0; a layout fix awaits the user's direction. Still needed: all maps/weapons/items, UI/cancel/retry, death/respawn, zone completion and travel time/exposure |
| 5 | Measure movement/combat under adverse network conditions | Initial two-player same-PC SDK injected delay/jitter/loss combat checks pass. Host plus two clients, mixed/asymmetric paths, prediction corrections, rewind measurement and authority/recovery remain |
| 6 | Add measured performance and endurance evidence | A same-PC 1080p frame-time sample exists (`Invoke-PerfSample.ps1`: 8 soldiers plus effects, Ultra p99 2.04 ms, RTX 4070 Ti). The capacity soak now accepts 1800 s and reports per-peer bandwidth and memory growth; its first 1800 s run passes with +4.5 to +8 MB working set per peer ([QA](QA.md#2026-10-30-30-minute-capacity-soak)), and `Invoke-ReconnectCycles.ps1` runs fresh reconnect sessions ([TOOLING](TOOLING.md)): 100 of 100 pass after the late-identity fix ([QA](QA.md#2026-10-30-reconnect-cycles-and-late-peer-identity)). Online rematch now lets a soak harness chain real matches in one room. `capacity -Gameplay` (up to 7200 s) makes all eight peers move, fire and rematch; a 100 s run passes with 0.4 GC/s and +14–17 MB working set, and the perf sample reports 560 bytes managed allocation per frame after the HUD fix ([QA](QA.md#2026-10-31-hud-string-allocations)). A two-hour gameplay run failed at about 46 min when seven peers dropped together during a likely machine-wide stall from overlapping Unity batch work; the runner now logs peer removals and the largest frame gap and names a stall ([QA](QA.md#2026-10-31-gameplay-soak)). Still needed: lower-end GPU, separate-machine eight-player matrix, a passing two-hour run |
| 7 | Complete independent-network acceptance | At least two external networks, direct/relay paths, rotate hosts, graceful/crash recovery; capture exact builds and logs |

User playtest reports on 2026-10-31 (settings stuck, hitbox and no-reg, pickup while
running, matches closing, bouncy recoil, red marker, sniper press, Blink, gun ammo,
hot zone visibility) have root causes and proposed fixes in
[PLAYTEST-FIX-LATER](PLAYTEST-FIX-LATER.md). At the user's request they were
implemented on 2026-10-31 with the report's recommended values, recorded as
proposals under [D040](DECISIONS.md) ([QA](QA.md#2026-10-31-playtest-fixes-d040)).
Still open:
- the user's tuning of the 250 ms rewind and attack window, the two-magazine ammo
  and the 0.2 s fire buffer;
- which red mark the tester meant;
- a human playtest of a build with these fixes.

The 2026-10-31 feel pass ([D041](DECISIONS.md), [QA](QA.md#2026-10-31-feel-pass-d041))
implements the feel audit's air strafing, predicted local shots, eased recoil,
hit-marker sizing, speed lens, wind thresholds and bloom ring as proposals in
build `80f56a7c…`. Open: the user's playtest, and the deferred air carry decay
(0.8 → 0.5) if strafing still builds too little speed.

User feedback on 2026-10-30, after a first local play of the 16:06 build
(`d838ac03…`): "so far that is really good and like those improvements, keep going".
This is an early, general approval of the quality pass and the afternoon's additions.
It is not acceptance of any single item, of online play or of balance.

User feedback on 2026-10-29: the game works and feels "pretty good", with a few
connection issues at the beginning. Follow-up identifies join/team selection and
another player in Texas on a different ISP. The symptom is disconnect/menu return;
exact builds and error wording remain unconfirmed. This is human external-play
evidence, not completion of the external matrix.
The prior firewall inspection found application exceptions for old diagnostic
players. System firewall changes are separate from gameplay work and remain unapplied.

Focused multi-client tests begin as soon as the SDK and service configuration are
available; internet/load validation is not a reason to defer networking failures.
D018 permits independent rules, assets and menus while user-dependent provisioning
is pending, with network acceptance kept explicitly open.
