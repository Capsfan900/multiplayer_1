# Multiplayer foundation — design for review

Date: 2026-10-25. Status: proposed design; no implementation or performance proof yet.

## 1. Intent, decisions and scope

Build the base systems before the arena content. The eventual Windows game is a
first-person arena battler for four teams of up to two players, with uneven teams,
weapon races and a moving capture zone. It reuses `vibegame1` in a separate project
at `C:\Users\tyler\Main Storage\multiplayer_1`. The user requires internet play
with friends and competitive-quality responsiveness, latency handling and performance.

Confirmed decisions:

- Foundation first: networking, sessions, player data and measurable reliability.
- **Photon Fusion Host Mode**, replacing the earlier dedicated-server decision
  after the user requested peer-to-peer play without operating hosting infrastructure.
  One player's game runs the authoritative simulation; no rented game server,
  self-maintained server or custom backend API is required.
- Preserve the source project's developed systems and level-making capability.
- First-person shooter perspective is explicitly confirmed. Graphics should be
  stylized/cartoon-like, attractive and economical to render, with VHOLUME as an
  architectural reference. **C / Ink & Concrete is the selected base art direction**.
  **C3.1 / Wildstone Modern is the accepted map visual direction**, with sleek,
  mostly monochrome modernism in the [map brief](2026-10-25-wildstone-modern-map-design.md).
  Its simple human silhouette character direction is also accepted. Exact assets,
  collision, layout and balance remain proposals. Production checks have a
  [separate workflow](2026-10-25-characters-weapons-design-workflow.md). All four
  [weapon concepts](../../art-direction/weapon-concepts.md) are now accepted, with
  simpler surfaces and softer shading to match the game.
  The original animal character/equipment theme does not carry forward. Graphics
  must respect the foundation performance budgets.
- The initial content scope includes those four weapons plus grenades, stun guns
  and laser swords, and exactly two special consumables: short wall-blocked Blink
  teleport and temporary shield. The moving capturable zone remains the primary
  objective. The [combat/items/zone spec](2026-10-25-arena-combat-items-zone-design.md)
  owns their proposed behavior and acceptance cases.
- Unity rendering must combine simple reusable forms with soft modern lighting;
  the [rendering spec](2026-10-25-unity-rendering-design.md) owns its strategy.
  Use Blender and Unity authoring tooling, auditing source reuse first, under the
  [asset pipeline](../../ASSET-PIPELINE.md). Multiplayer authority, state and replay
  remain new foundation work, not inherited single-player assumptions.
- Maintain supporting specs, decisions, handoff and evidence through the
  [documentation index](../../README.md).

Proposed choices to approve with this design:

- Fusion 2.1 stable, pinned to an exact compatible SDK version during compatibility
  validation; existing Unity 6000.5.10f1 and URP retained unless that validation fails.
- A player-hosted authoritative simulation with predicted remote-client movement,
  reconciliation and bounded hitbox rewind. The host runs the same gameplay rules,
  but its machine is trusted by the other players and has a latency advantage.
- Managed Photon sessions/NAT traversal/relay and proposed managed PlayFab identity
  and minimal profiles. No bespoke allocator, admission service or result backend.
  Start with private friend sessions in one common Photon region.
- Initial target: 120 Hz simulation, 60 Hz authoritative snapshots, uncapped rendering
  with a 144 FPS reference target. These are budgets to prove, not measured results.

"Bulletproof" is operationalized as explicit invariants, failure handling and passing
tests. No design promises zero internet delay, perfect cheat prevention or zero bugs.
The desired experience is immediate local control, consistent adjudication and bounded
correction under the declared network conditions.

## 2. Approaches and source constraints

**Selected: Fusion Host Mode.** One participating PC acts as server for the others.
Fusion attempts a direct connection using NAT punch-through and can fall back to
Photon relay. Players should not need to configure port forwarding in the normal
supported flow; connection failure still needs a usable error/retry path. This is
player-hosted networking with managed services, not a service-free peer mesh.
[Fusion connection process](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/connection-process)

Host Mode retains prediction/resimulation, interpolation and authoritative hitbox
rewind. Shared Mode is not selected for this shooter. The host's home PC must render
and simulate the match, and its uplink serves the remote players. A malicious host
can alter state and results; managed authentication cannot prevent that. Photon
remains a service dependency with CCU/bandwidth limits and potential charges,
especially when relayed. Managed profile service billing is separate. No paid plan
or service setup is authorized by this design.
[Fusion topology tradeoffs](https://doc.photonengine.com/fusion/v2/fusion-choose)

**Considered: FishNet plus dedicated servers.** Its prediction/reconcile support is
credible, but session/backend integration remains ours; built-in collider rollback
is a Pro feature. [Prediction](https://fish-networking.gitbook.io/docs/guides/features/prediction/creating-code/controlling-an-object),
[collider rollback](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/components/colliderrollback)

The earlier dedicated-server foundation, Unity Netcode/Relay and owner-driven
movement proposals are retired. Dedicated fairness and trusted competitive results
are not claims of the player-hosted design.
Netcode for Entities would change the source's GameObject programming model; a
custom prediction framework would spend effort on infrastructure Fusion provides.
No multi-provider networking abstraction is part of this project.

The source inspected at commit `09e10da3fb1fd64003aa14ec6597acb936470740` has:

- `FirstPersonMotor.Update()` reading global input and player delta, with many
  mutable timers/flags and calls into stamina, camera feedback and physics.
- Frame-driven `PlayerStamina`; `WaterVolume` callbacks refresh motor state.
- Single-player `GameManager`, `GameEvents`, pause/hitstop and player presentation.
- Firearm traces resolving enemy health rather than network player hitboxes.
- Reusable level data, Level Studio, `LevelPieceFactory` and the separated
  `LevelDefinitionBuilder.BuildWorldContent` path.
- Local settings in `SettingsStore`/PlayerPrefs, which are appropriate for preferences
  but cannot become authority for identity, health, inventory or results.

A network-transform component alone cannot preserve this motor through replay.
Keep its movement laws, tuning, assets and authoring; adapt its execution and state
ownership. Copy only committed project inputs, preserve asset GUIDs, give the fork
separate save/product identifiers, and leave source/user-owned files untouched.
The source reuse inventory remains in the historical arena proposal.

## 3. Smallest complete foundation deliverable

A packaged Windows game able to host or join, a plain host-session/join-code interface,
recoverable login and minimal profile, and a generated test course with walls,
ramps, water, a launch orb, target dummies and player spawn points. It supports eight
players total (one host plus seven remote clients), team assignment, late join,
reconnect, bounded host migration, one hitscan test gun, one projectile, a
deflection/impulse case, a pickup and a moving scoring volume.

These gameplay objects exercise difficult networking cases; they are not the final
weapon roster or arena. No rank system, persistent power progression, inventory
economy, skins, bots, voice chat, spectators or collaborative level editor yet.
Automated input clients are test machinery, not gameplay bots.

## 4. Authority and process boundaries

| Component | Owns | Must never own |
| --- | --- | --- |
| Remote client / host's local player input | Input sampling, prediction where applicable, camera, presentation, local settings | Supplying final damage, health, ammo, team or score through player commands |
| Player host | Fixed-tick simulation, movement resolution, collisions, combat, pickups, teams, zone, match summary and seat reservations | Other players' login credentials, service administrator secrets, blocking service calls on the simulation thread |
| Managed identity/profile service | Verified account identity and display profile | Frame-by-frame gameplay or certification of host-reported scores |
| Photon Cloud | Session discovery, authenticated service connections, NAT/relay and migration coordination | Independently verifying gameplay run on the player's PC |

The host is also a combatant and the room organizer. Local inputs pass through the
same rule validation as remote inputs, but the host process can bypass that code.
Clients can reject malformed network data; they cannot force a malicious host to
simulate honestly or keep a session alive. Scope is responsive friend matches,
not ranked integrity or dedicated-server competitive fairness. Host departure
invokes the bounded migration/failure behavior in section 7.

Client requests carry bounded inputs and identifiers. Validate authenticated sender,
membership, ownership, tick age, action rate, enums, ranges, finite numbers and packet
size. Never trust damage values or movement delta supplied by a client. Use Fusion
and provider-supported transport/security features; no bespoke encryption protocol.
Service administrator credentials remain outside every game build and repository.
Player login tokens go only to the intended managed provider, never another player.
Do not treat a connection token or caller-selected account ID as proof of identity.

## 5. Simulation, input and movement replay

Use Fusion's fixed network tick on clients and the player host (the server in
Fusion terminology). Render/input sampling runs independently. Local look responds
each rendered frame; gameplay movement, cooldowns,
stamina and weapon decisions advance only with the fixed simulation step. Preserve
press edges between ticks without duplicating a press over multiple simulation ticks.

Each input command carries tick/sequence, movement axes, quantized aim, held/pressed
actions and requested weapon slot. The SDK's input scheduling/redundancy is used.
Stale/duplicate/out-of-window commands cannot execute an attack twice. After missing
input, continuous movement may be held for at most 100 ms before neutral input;
one-shot actions are never synthesized. Exact handling must fit Fusion's scheduling
and be verified in the fault tests, rather than competing with it.

Refactor the copied movement simulation to consume explicit input, fixed delta and
restorable state. State must include everything affecting the next tick:

- Position, velocity, facing, stance/capsule dimensions and grounded/contact state.
- Dash direction/speed, air charges, dash/slide/coyote/jump-buffer timers and flags.
- Stamina amount, regeneration delay, wall-run budget, normals, grace windows and
  wall-jump history.
- Water/traversal contact IDs, launch state and external impulses with their tick.
- Weapon ammo/cooldown/reload state, action sequences, life/spawn generation.

Use tick-relative gameplay timers; do not read wall-clock time, render delta, global
input or mutable camera state during replay. Seed any gameplay randomness from
authoritative match/entity/action identifiers. Snapshots need not serialize visual
springs, audio, particles or other state that cannot change simulation.

On receiving authority, restore the appropriate state and replay unacknowledged
inputs using Fusion. Correct the simulation immediately; smooth only the rendered
presentation. Never blend the authoritative collider through a wall to hide an error.
Respawn/teleport/life changes reset incompatible prediction history and interpolation.
Record correction magnitude before smoothing so visual hiding cannot pass a bad motor.

Collision/traversal queries must return the appropriate state during replay. Convert
water, launch and surface triggers into reproducible simulation queries or SDK-safe
events. Do not rely on Unity callbacks firing again automatically after rollback.
Start against static geometry and disable player-to-player physical pushing; players
still have authoritative combat hitboxes. Moving platforms/destruction are later scope.

Keep the existing CharacterController only if the replay tests pass. If its hidden
collision state causes unacceptable divergence, use Fusion's supported kinematic
controller approach and port the existing movement laws/tuning. This is an explicit
compatibility decision, not permission to substitute generic movement unnoticed.
Measure jump/dash/slide/wall-run trajectories against the source before accepting it.

Predicted effects use stable action IDs and are not re-emitted on replay. Confirmed
damage/death/score notifications come from authority. Remote-player events never
drive the local player's HUD, camera, game state or input.

## 6. Combat, lag compensation and fairness

The player host owns weapon selection, ammo, cooldown, muzzle origin, spread seed, damage,
health, knockback and death. Clients can predict muzzle flash/recoil immediately;
confirmed hit markers and damage require the server verdict. Static world geometry
and player hitboxes are both considered, preventing hits through cover.

Hitscan uses Fusion's server-side historical hitbox queries with the SDK's view-time
information, not an arbitrary timestamp chosen by the client. Initial maximum rewind
is 150 ms; memory buffers can be larger, but allowed compensation is bounded. High
ping does not earn unlimited historical hits. Record accepted shot tick, rewind age,
weapon/sequence and hit verdict for diagnosis. Sub-tick query capability is useful,
but it is not a promise that every input action is simulated at sub-tick precision.
[Fusion lag compensation](https://doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation)

Projectile collision, explosions, pulls and deflection execute on the authoritative
timeline. Predict projectile visuals with stable IDs and reconcile to server objects;
do not turn travelling projectiles into rewound hitscan. Deflection tests the server's
accepted active window, facing and collision at that simulation tick. A late request
does not resurrect a dead player or undo a confirmed explosion. Reflections transfer
attack ownership once; a projectile cannot apply damage twice after replay.

External impulses are authoritative and included in movement replay. Cosmetic
physics cannot push the authoritative capsule. Camera recoil and impact feedback do
not freeze shared time. A local settings/pause screen never pauses the server.

Rewind helps the shooter but can produce damage shortly after a target reaches cover.
The test report must expose that tradeoff, including asymmetric latency; do not call
one client's flattering capture evidence of fairness for both sides.

The host's local player does not take the remote-client round trip to authority.
Measure host-versus-client as well as client-versus-client fights, with host roles
swapped across machines. Prediction and rewind improve remote responsiveness but
do not erase host advantage, protect against a dishonest host, or make a slow home
uplink equivalent to a dedicated server. No esports integrity claim follows from
passing the feel/performance gates.

## 7. Sessions, seats and recovery

Session states: `Connecting -> Lobby -> Loading -> Warmup -> Running -> Completed
-> Closed`, with `Reconnecting`, `Migrating`, `Failed` and `Aborted` paths. Use one
logical match ID and a new authority/connection generation after migration; reject
late callbacks and commands from a retired generation. A retry cannot duplicate a
seat or start a second local runner. UI distinguishes joining, full team,
incompatible build, reconnecting, migrating and failed recovery.

Flow: authenticate with managed providers -> create or join a private Photon room
by code in the same region -> host assigns a seat -> load matching content ->
exchange readiness -> spawn. The code locates the session; it is not account
identity. Use Photon-supported authentication and session APIs; no fleet allocation,
custom admission tickets, server leases or self-hosted endpoints. A host is an
ordinary authenticated player, not a certified trusted server. Validate the SDK's
mapping from authenticated account to connected peer during compatibility work;
never accept a claimed identity from a gameplay RPC or connection token alone.

Capacity is four teams by two slots, including the host. The host serializes seat
assignment with reconnect holds in authoritative match state. Two simultaneous joins
cannot claim the same last slot in an honest session. Transport room capacity and
gameplay seats must both respect the eight-player limit, including reconnect cases.
Empty and one-player teams are legal; teams are selectable in the lobby and locked
in a match. Starting requires at least two occupied teams. Clients pin protocol,
build, map and ruleset versions; another player cannot supply executable content.

On a remote-client disconnect, neutralize inputs, remove zone presence and reserve
the account's seat for 30 seconds. Keep its avatar vulnerable for five seconds
before despawning. Reconnect must bind the verified account and its session-scoped
reconnect credential to the same seat; invalidate the old connection generation
before restoring input authority. A second connection cannot control it concurrently.
An already active account is rejected rather than silently taking over. A despawned
avatar uses ordinary respawn and starter gear. No pickup-ammo restoration,
invulnerability or score reset. An expired seat is released. Connection tokens help
restore ownership but are not authentication; keep tokens out of room metadata and
logs. [Fusion reconnect sample](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect)

Set bounded load/connect timeouts and cancel on leave. If fewer than two teams
remain connected, pause scoring for one 30-second absence deadline; recover if
opposition returns, otherwise abort without awarding a win. Reconnect loops must
not extend the deadline. Room cleanup uses Photon session lifecycle behavior;
application state must release failed admissions and ignore obsolete callbacks.

### Host migration contract

Include migration in the foundation recovery gate. Fusion coordinates host
selection and provides the latest uploaded state, but the application must shut
down the old runner, create a new runner and reconstruct scene/spawned network
objects. Only the latest cloud snapshot is retained; changes after it can be lost.
Migration is an interruption, not guaranteed seamless continuity.
[Host Migration API](https://doc.photonengine.com/fusion/v2/manual/advanced/host-migration),
[Host Migration sample](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-host-migration)

Proposed application budgets, subject to measured SDK feasibility:

- Upload a migration snapshot every two seconds while running. Record its simulation
  tick and successful publication; upload/send overhead is included in host budgets.
  Accept at most three seconds of lost simulated progress at migration entry. Measure
  age against the last observed authoritative tick, not time spent reconnecting. If
  age cannot be bounded or the snapshot exceeds this limit, abort visibly.
- Graceful host leave freezes gameplay and scoring, attempts a final snapshot, then
  leaves through the SDK migration path. Allow five seconds for that publication;
  failure uses the bounded prior-snapshot path or aborts. Do not claim zero-loss
  handoff merely because a local upload call completed; verify the successor's
  restored snapshot tick. A host choosing to quit is never trapped in the app.
- Crash, process kill or network loss uses the latest available snapshot. From local
  loss detection, each peer allows at most 30 seconds for recovery; gameplay and
  scoring remain suspended. Recovery needs a connected replacement host and usable
  Photon connectivity. No successor, missing/incompatible state, stale snapshot,
  second host loss during the attempt or timeout returns to the session screen as
  aborted, without a win. No custom host election or peer consensus service.

Snapshot state must include the logical match ID/epoch, map/ruleset, match phase,
clock, team scores, zone path/phase and scoring timers; every player's verified
account-to-seat mapping, reconnect binding, team, connection generation, life/spawn
generation, health, complete motor state from section 5, weapon/ammo/cooldowns and
input/action sequence counters; all projectiles with owner/lifetime/deflection and
hit-deduplication state; pickups and their respawn timers; gameplay random state and
remaining reservation/absence deadlines. Secret login/service tokens are excluded.
Rebuild local lookup tables from restored state; do not depend on old process memory
or transient RPC history. SDK identifiers that change must be rebound to stable
account/entity identities before granting input authority.

Restore the whole snapshot consistently: a rollback may undo a recent kill, pickup
or score, and UI must say that play resumed from a checkpoint. Clear obsolete input,
prediction, interpolation and hitbox histories; never accept pre-migration shots
against new history. Resume after a short synchronized warmup sufficient to rebuild
the 150 ms hitbox window. Do not replay past VFX or award damage twice. Reconnect
holds and opponent absence deadlines retain elapsed absence through migration
rather than restarting; test this explicitly. One active generation owns the match;
old-host recovery may only rejoin as a client after verification. Unsupported network
partition recovery aborts rather than claiming independently resumed games agree.

Tests cover graceful departure, hard kill, uplink loss, absent/stale snapshots,
repeated migration, successor failure, and disconnect during join, respawn, pickup,
projectile deflection or scoring. Compare every gameplay field before/after the
checkpoint, then verify actions still work. A failed recovery must leave no live
orphan runner, duplicate avatar, stuck seat or misleading completed result.

Photon Cloud connection loss is a separate failure from loss of the player host.
Explicitly configure and test Fusion's supported quick-rejoin behavior. Proposed
initial setting is `QuickRejoin`, with a bounded visible retry/failure path; automatic
room transfer is deferred to avoid changing join-code semantics in the foundation.
Existing direct connections may survive a cloud interruption; relayed clients and
new joins cannot be promised the same. Test both and apply the same absence/abort
rules. [Photon connection loss](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/lost-connection-handling)

## 8. Player data and persistence

Proposed managed PlayFab authentication integrates with Photon; no custom auth API
is deployed. A recoverable provider account and validated display name are the
persistent foundation scope. The proposed initial login uses PlayFab's SDK
email/password and provider-supported recovery flow; anonymous development identities stay in test
configuration. Disable anonymous Photon admission in the authenticated configuration.
Passwords/tokens go to the intended provider only and are redacted from diagnostics;
never ship service secret keys or save credentials in PlayerPrefs. Steam linking is
not assumed.
[Photon/PlayFab integration](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/authentication/playfab)

Keep these records separate:

| Record | Minimal fields | Authority/lifetime |
| --- | --- | --- |
| Account/profile | Stable provider account ID, validated display name | Managed provider; recoverable across reinstall |
| Local settings | Sensitivity, binds, video/audio/accessibility preferences | Local only; separate product/key namespace, clamped values |
| Session member | Account ID, team/seat, match ID, connection generation, reconnect/absence state | Host network state; migratable, expires with match |
| Player simulation | Entity/spawn IDs, motor state, health, weapon/ammo | Host network state; never loaded from a player save |
| Match summary | Match ID, build/ruleset, participants, final scores, completion/abort state | Session display only; host-reported and untrusted |

Use the provider's profile validation, ownership and error handling. Keep no extra
profile database or bespoke revision/migration service for one display name. Serialize
local writes and re-read provider state after an uncertain response. Concurrent
changes from another device follow the provider's documented behavior, verified in
tests. Local settings upgrades remain versioned and fixture-tested if needed.
Missing profile data must not fabricate rewards or gameplay power.

There is no trusted persistent result pipeline, durable submission queue, ranking,
currency or reward ledger. Final summaries are replicated for the session only and
clearly described as host-reported. A host can forge a win or omit a result; neither
managed storage nor signing with a key shipped in the game would make it trustworthy.
An interrupted match with no terminal state is aborted/unknown, never silently a win.
If saved match history is requested later, it requires an explicitly unverified
format and retention decision, not an implied competitive record.

Provider outages block new login/admission when identity cannot be verified. Existing
validated matches may continue while transport permits; profile writes show a retry
state without blocking simulation. No per-tick service reads/writes. These managed
services still require account/configuration work, connectivity and usage limits;
they remove server administration, not service dependency or possible fees.

## 9. Performance targets and evidence

These are proposed acceptance budgets for eight active players in the foundation
course with 32 simultaneous test projectiles and a defined VFX load. Capture exact
hardware, OS, SDK, build hash, resolution and graphics settings in every report.
The current machine's hardware was not verified: read-only WMI inspection was denied.
Reference client and player-host hardware, home uplink capacity and direct/relay
paths must be fixed before performance acceptance. The host PC renders its local
player while simulating the full match and exchanging traffic with seven remote
clients. Timed acceptance uses separate remote machines, not seven extra game
processes competing for the measured host's CPU/GPU.

| Measure | Initial budget |
| --- | --- |
| Simulation/snapshot rates | 120 Hz fixed simulation, 60 Hz server snapshots; verify SDK configuration |
| Player-host simulation work | p99 CPU <= 4 ms; p99.9 <= 8.33 ms; no sustained tick backlog while rendering |
| Client rendering, reference 1080p competitive settings | p95 frame time <= 6.94 ms; p99 <= 8.33 ms after warmup |
| Local response | Camera on the next rendered frame; movement/fire predicted by the next simulation tick, never waiting for RTT |
| Static-course correction at <=80 ms RTT/1% loss | p99 pre-smoothing position error <=5 cm; corrections >25 cm <=1 per player-minute |
| Correction at 150 ms RTT/2% loss | p99 <=15 cm; no stuck states, duplicate actions or lasting divergence |
| Gameplay/network hot path | No recurring managed allocations in steady-state simulation; profile SDK allocations separately |
| Remote-client bandwidth | <=32 KiB/s upstream and <=64 KiB/s downstream at p95 over 1-second windows, actual transport traffic |
| Aggregate host bandwidth | <=512 KiB/s upstream and <=256 KiB/s downstream at p95 over 1-second windows, including migration/control traffic; measure bursts separately |
| Host migration | Proposed 2-second snapshot interval, <=3 seconds of lost simulated progress, <=30 seconds recovery after loss detection; otherwise clean abort |

Budgets are deliberately falsifiable and may expose that 120 Hz costs too much. Run
60/120 Hz comparison on the same content; do not silently reduce the accepted target.
If a target fails, report the limiting subsystem and decide the tradeoff before
claiming readiness. Frame rate is not measured input-to-photon latency; hardware
measurement is required for any such claim.

Profile direct, mixed direct/relay and all-relayed sessions separately, including
uplink contention and host rendering at 30/60/144/240 FPS. Report usable uplink and
headroom, not just advertised ISP speed. Relay overhead and migration upload bursts
must not be hidden in averages. If a prospective host cannot sustain the session,
show the limitation and let friends recreate it with a better host; automatic
latency-based host optimization is outside this foundation.

Network profiles, explicitly expressed as round-trip latency:

- Baseline: local network, no injected faults.
- Regional: 40/80 ms RTT, up to +/-10 ms one-way jitter, 0/1% independent loss in
  each direction, plus mixed player latencies within the same match.
- Adverse: 150 ms RTT, +/-20 ms jitter, 2% loss; asymmetric uplink/downlink included.
- Recovery: 250 ms RTT, 5% loss, packet duplication/reordering, 500 ms burst stalls,
  and a 5-second disconnect. This tests bounded degradation/recovery, not esports feel.

Test across 30/60/144/240 render FPS with the same 120 Hz simulation. Include wall
seams, ceilings, fast slides, water exits, launch chains, predicted stamina depletion,
external impulses, death during replay and respawn. Separate intended impulses and
teleports from uncommanded corrections; report both categories, not a blended average.
Remote interpolation is measured separately from local prediction.

Record RTT/jitter/loss, tick overruns, frame-time percentiles, command age, prediction
error, replay steps/cost, interpolation underruns, bytes/sec, GC, active entities,
join/reconnect success, migration duration/rollback age and provider failures. Use
bounded diagnostic buffers; flush outside critical paths and redact identity tokens. Warmed and cold-load tests
are distinct. No screenshot polling during timed measurement.

## 10. Foundation acceptance order

1. **Compatibility and source baseline:** pin Fusion/Unity versions, compile the
   Windows host/client build, record original movement trajectories, verify managed
   identity-to-peer binding and SDK migration APIs, settle client/host test hardware.
2. **Movement proof:** a player host plus two remote clients, fixed-tick source motor
   adaptation, state restore/replay, remote interpolation, all representative traversal
   cases under the network matrix. Failure here stops content work.
3. **Combat proof:** one hitscan and one projectile, rewind/cover behavior, deflection,
   ammo/cooldown rejection, duplicate suppression, authoritative knockback and respawn.
4. **Session and data proof:** recoverable identity, private room code, serialized
   team assignment, mixed-version rejection, late join, authenticated reconnect,
   duplicate-identity rejection, expiry, provider interruption, profile-write failure,
   all section 7 host migration/abort cases and truthful unverified match summaries.
5. **Load and endurance:** one rendering host plus seven remote clients with
   continuous representative actions;
   30-minute profile runs and a two-hour soak. At least 100 join/reconnect cycles and
   simultaneous final-seat claims; repeated migration across host machines; no
   unbounded memory growth, duplicate players or orphaned seats. Include direct,
   mixed and all-relayed sessions and home-uplink contention.
6. **External internet proof:** players on at least two independent external networks
   host/join through the intended Photon region, complete a session, reconnect and
   exercise graceful and crash host migration. Verify relay fallback from a network
   where direct connectivity fails; rotate the host between external networks.
   Local multi-process success is not internet or multi-human acceptance.

Each gate records commands/builds, raw logs, failures and limitations. Successful
compile, average FPS or vendor marketing are insufficient. Capture moving Windows
client evidence and have the user judge movement/combat feel before arena production.

## 11. Design review

An earlier multiplayer-engineering review covered the dedicated-server version.
A second independent review of this Host Mode revision found no consequential
contradictions in host trust, recovery, player data or the no-custom-server boundary.
Both reviews checked document consistency only. This revision applies the user's
no-server-administration request. The art direction now records C / Ink & Concrete
with the accepted C3.1 / Wildstone Modern map and simple human character directions.
Weapon identities and added categories are accepted; detailed behavior and simplified
production art are specified separately. SDK compatibility, identity binding, migration budgets,
performance, internet behavior and final asset/layout choices remain unverified or pending.
No runtime readiness is inferred from documentation.

## 12. Implementation boundaries and remaining decisions

Source imports remain separate from new foundation modules. Retain existing level
tools, geometry and visual resources; disable campaign progression and global pause
in the multiplayer runtime. Preserve original movement tests and add replay/network
tests that exercise behavior, not merely serialized field equality.

New responsibilities are player simulation/presentation separation, Fusion session
and admission integration inside the game, match/team state, managed identity/profile,
session-only summaries, host migration, the generated test course and failure/performance
harness. Use the selected SDK directly; avoid adapters for unselected frameworks.

The builder may choose file layout, serialization packing, pool capacities, precise
test tooling and presentation details within these contracts. It may not downgrade
host authority rules, remove prediction or rewind, weaken acceptance tests, spend on services,
or substitute a generic motor without reporting the effect on source movement.

Before internet setup, select the managed Photon/PlayFab accounts, common region,
exact plans and spending caps. No game-server hosting provider or custom backend
deployment is part of the plan. Before acceptance, fix hardware/uplink, login UX and
any budget changes openly. No cloud accounts, subscriptions, servers or game project
have been created by this design work. The next deliverable is a staged implementation
plan beginning with compatibility and movement proof, then recovery and measured
eight-player internet play.
