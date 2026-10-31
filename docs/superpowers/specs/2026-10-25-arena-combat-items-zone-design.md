# Arena combat, items and moving zone

Date: 2026-10-25. Status: confirmed content scope with proposed gameplay rules;
no numeric balance or runtime behavior is approved or proved by this document.

Local SDK verification extension (2026-10-29): diagnostic `laser-combat` runs pistol,
shotgun and sniper through production pickup/equip/fire RPCs in separate host/client
players. It checks one ammo spend, authoritative damage, pellet/event counts and
matching replicated event identities. Host teleports and health resets explicitly
construct a stationary fixture; this does not establish moving-target, cover,
adverse-latency or human balance acceptance. Exact runs belong in [QA](../../QA.md).
The separate `laser-moving-cover` scenario retains explicit initial placement,
then moves the host target through production input while the client fires each
laser. Both peers must observe displacement and velocity before fire, agree on
ammo/damage/event identities, and observe a second shot blocked by a diagnostic
world collider with no target damage. This is controlled moving-target/cover
evidence, not measured rewind age, injected latency, projectiles or balance proof.
Diagnostic `projectile-combat` extends this path to Pulse Lobber: production pickup,
selection and fire must produce a projectile observed travelling on both peers,
one matching terminal explosion, its removal, one ammo spend and correct falloff
damage. A second shot hits explicit world cover with its protected target still
inside the explosion radius; target damage must remain zero. Initial placement,
health resets and temporary cover are fixtures. This does not exercise or accept
deflection (D037, below), other projectile categories or natural moving-target counterplay.
Additional local diagnostics exercise actual grenade gravity/bounce/fuse and
within-radius blast cover, disc ricochets/return-path damage deduplication and
removal before expiry, and three legitimate stun shots spanning suppression,
immunity and renewed stun. Peers must agree on actions, events, ammo and timers.
Synthetic colliders and pre-shot placement isolate these cases. Suppression-flag
observations do not prove motor/input response, nor do these checks exercise
deflection or accept natural counterplay; [QA](../../QA.md) owns observed results.
The [foundation design](2026-10-25-multiplayer-foundation-design.md) governs Unity
URP, Fusion Host Mode, prediction, recovery and implementation order.

## 1. Confirmed scope and proposed defaults

The user accepts **Line Driver, Pulse Lobber, Rebound Disc and Push Lens** and adds
**grenades, stun guns and laser swords**. Exactly two initial special consumable
types are requested: **a short blink teleport** and **a temporary shield**. Character/map
directions remain accepted separately; weapon appearance does not fix its statistics.

The game remains first-person, with four teams of up to two players, incomplete
teams allowed. Players race for weapons in the center and other valuable areas
while contesting a **moving capturable scoring zone**. The WARDOGS reference expresses
that objective intent; the scoring proposal below does not assert that game's rules.
This is a travelling objective, not a shrinking battle-royale circle.

All behavior below is proposed unless identified as confirmed. Damage, ranges,
timings, ammo, cooldowns, zone speed, points, match duration and inventory bindings
are tuning choices to document and test, not silently approved balance. The
mobility identity is confirmed as a short blink teleport; its exact range and costs
remain proposed tuning.

## 2. Weapon behaviors and counterplay

| Accepted weapon/category | Proposed behavior | Readable threat and counterplay |
| --- | --- | --- |
| Line Driver | Precise direct-fire attack using the foundation's bounded hitscan compensation | Clear emission direction and shot cue; break sightline, move or use cover |
| Pulse Lobber | Visible travelling orb with a bounded blast and impulse | Slow identifiable projectile, localized detonation cue; dodge, take cover or deflect it with a Push Lens ([D037](#2026-10-30-push-lens-deflection-d037)) |
| Rebound Disc | Travelling disc with bounded ricochets and lifetime | Visible flight/bounce cue; change route or put cover between yourself and its next segment |
| Push Lens | Short-range directional displacement attack with a committed windup; at resolution it also reflects one enemy projectile in its cone ([D037](#2026-10-30-push-lens-deflection-d037)) | Clearly indicated facing/reach; leave its cone or break sightline before resolution |
| Grenade | Thrown projectile with a readable fuse and bounded explosion | Visible projectile and danger cue; move away or reach blast-blocking cover |
| Stun gun | Single-target control interruption with readable commitment | Distinct windup and shot; dodge the proposed travelling bolt or break sightline |
| Laser sword | Bounded melee swing with windup, active window and recovery | Readable stance/arc; stay outside reach or punish recovery |

Grenades are a **tactical weapon category using ammo**, not a third special
consumable type. Begin with one grenade behavior, one stun-gun behavior and one
laser-sword behavior; variant families are not implied by the plural categories.

Stun interrupts attacks and temporarily suppresses movement actions for a short,
tunable interval. It never locks camera look, settings/UI input or the ability to
leave a session. Apply a visible post-stun immunity window; a hit during stun or
immunity does not extend, queue or refresh the stun. Damage during immunity is a
separate unselected tuning decision. No unlimited chain-stun. Stun does not make
the target invulnerable or automatically remove it from zone occupancy.

Laser-sword damage uses a host-authoritative swept shape during an explicit attack
timeline, with range, facing, world obstruction and per-swing target deduplication.
The cosmetic trail cannot cause damage. Prototype melee on the authoritative
timeline before considering separate lag compensation; never silently turn it into
unbounded rewind. Local impact feedback cannot change global time scale or freeze
network simulation. Grenade/explosion cover checks and projectile ownership follow
the foundation's authoritative combat rules.

## 3. Exactly two special consumables

**Mobility — confirmed short blink teleport.** On activation, request a
short displacement in the chosen direction. The host sweeps the player capsule
along the path and clamps travel before obstruction; it cannot phase through walls,
ceilings or other blocking world geometry. The final capsule must fit a supported
playable location under the traversal rules. An invalid endpoint or negligible
legal displacement rejects the action without consuming a charge. A visible departure
and arrival cue identifies what happened. This wall-blocking behavior is the stated
implementation contract; the exact blink range and presentation remain tunable.
The native offline adapter checks the final floor-adjusted displacement against
the same minimum as match admission, so a vertical request cannot succeed by
finding support back at its original feet position.
Mark a successful blink as a position discontinuity: do not interpolate the body or
melee sweep through the gap. Retain legitimate pre-blink hit history only under the
foundation's bounded rewind policy; teleport is not retroactive invulnerability.

**Temporary shield — proposed personal overshield.** Grant finite damage absorption
until depleted or its expiry tick. Remaining damage spills into health when an
attack exceeds absorption. It is not invulnerability, a world barrier, or assumed
crowd-control immunity. Do not stack or refresh an active shield; rejected reuse
consumes nothing. Activation, remaining protection, break and expiry have distinct
readable cues. The wearer may contest the zone normally and may still be displaced
or stunned under the ordinary rules.

Proposed inventory: two dedicated consumable slots, one charge per type, with no
extra backpack. Full-slot pickups are rejected without deleting the world pickup.
This cap and the bindings are proposals. A successful activation spends exactly
one charge; a rejected request spends none. Movement, shield, stun and item timers
use simulation ticks. Death clears active effects and carried temporary items;
respawn/reconnect must not restore spent charges or expired protection.

## 4. Pickup, ownership and action contract

Proposed carrying model: a starter weapon, one replaceable special-weapon slot and
one finite grenade-ammo slot, plus the two consumable slots. Stun gun and laser sword
use the special-weapon slot in this initial proposal. Replacing a special weapon
discards the previous one without creating a new dropped pickup; no unlimited
inventory or duplication path. Starter choice, weapon-slot count, ammo caps and
death/respawn kit require explicit tuning review before implementation.

The host resolves a pickup as one atomic transition: validate authenticated sender,
living avatar and connection/life generation, pickup identity/generation, availability,
interaction distance, capacity and action sequence; then change ownership/inventory
and remove or cool down the pickup together. A simultaneous loser keeps its prior
inventory and receives a clear rejection. Clients never supply trusted ammo, damage,
shield strength or final position. Shooting, throwing and consumable use validate
cooldown, state, ownership and available resources before committing costs.

Define one published host-tick phase order: expire effects; accept validated actions
and reserve their costs; simulate movement/projectiles and resolve combat; apply
deaths; evaluate zone occupancy and scoring. For competing actions in one phase,
use a deterministic order with a stable participant identifier and action sequence,
not RPC arrival order or a special host-first branch. Reject actions from a player
already dead at acceptance; document whether accepted simultaneous attacks trade.
Test same-tick shield/impact, stun/fire, blink/melee and pickup/death outcomes.
This consistent ordering cannot eliminate Host Mode latency advantage or host cheating.

Predicted effects carry stable action IDs. Reconciliation cannot spend another charge,
spawn a second grenade or apply an impulse twice. Migration snapshots extend the
foundation contract with inventory/ammo, pickup generation and respawn tick,
consumable charges, accepted action counters, shield amount/expiry, stun/immunity
end ticks, melee phase/hit targets, grenade fuse, projectile owner/bounce state and
mobility resolution state. Restore a coherent checkpoint and discard incompatible
old-generation requests; migration rollback may restore earlier state only under
the foundation's explicit rollback policy, not through ad hoc client saves.

## 5. Moving zone and match flow

Propose a continuous authored circuit through central and side spaces, announced
publicly through world cues and a compact route/next-segment display. The host
replicates path identity, phase and simulation-clock reference; clients derive smooth
presentation. The scoring volume is gameplay state, not a free-moving physics object.
Speed and footprint stay tunable and its route must remain traversable.

Proposed scoring: at least one living connected player from exactly one team inside
the footprint makes that team the current captor and awards points at a fixed
**team** rate. A second teammate adds no multiplier. Two or more teams contest it
and award no points; no occupants award no points. Ownership follows current valid
occupancy without an assumed capture dwell. Dead/disconnected players never count.
Shielded or stunned living players count if their valid position remains inside.

Use a planar XZ footprint plus a bounded vertical tolerance relative to the authored
supporting route surface, including ramps. Validate the player's feet/support surface,
not camera position. Clamp the footprint to accessible walkable layout and prevent
scoring from a different floor above or below. A gap, bridge crossing or incline
must not cause a sudden scoring jump or admit an inaccessible occupancy location.

Start requires at least two occupied teams, not eight players or four full teams.
Apply the foundation's opponent-absence grace and abort behavior; disconnecting
cannot fabricate a win. Propose a tunable score target with a match-clock limit;
highest score at expiry wins and equal highest scores draw initially, without
inventing an overtime system. Late join does not reset team scores. Pickups outside
the center preserve alternate races; the zone route must not make one spawn or
perch control every useful reward.

## 6. Acceptance and implementation order

### Initial MVP tuning (builder defaults, 2026-10-25 resume)

The user delegated implementation of the complete MVP. These are starting values
for testing, not previously approved balance or a claim of competitive fairness.
Keep one versioned catalog in code, with values copied into data assets only when
the authoring flow needs them. Distances are Unity meters, times simulation seconds.

| Item | Initial behavior and cost |
| --- | --- |
| Line Driver (starter, revision 2) | 13 damage; 70 m hitscan; 0.15 s shot interval; 24-round clip; hold to repeat; 1.2 s reload; unlimited starter reserve |
| Pulse Lobber | 60 center / 20 edge damage over 4 m; 18 m/s visible orb; 0.90 s interval; 10 rounds; bounded 5 s flight; cover blocks blast |
| Rebound Disc | 35 damage; 28 m/s; 0.60 s interval; 10 rounds; at most 2 wall ricochets / 4 s life; one hit per target per flight |
| Push Lens | No direct damage; 8 m range, 55-degree total cone; 0.18 s windup / 0.85 s total cycle; 6 uses; horizontal impulse 14 m/s plus 4 m/s up; world cover blocks; reflects the nearest enemy projectile in the same cone ([D037](#2026-10-30-push-lens-deflection-d037)) |
| Grenade | 90 center / 15 edge damage over 5.5 m; 1.6 s fuse; 0.70 s throw interval; carry 2; 16 m/s forward plus 6 m/s up; cover blocks blast |
| Stun gun | 15 damage, 0.45 s stun, then 1.75 s stun immunity; 45 m/s visible bolt, 35 m range; 1.20 s interval; 8 rounds; hits during stun/immunity may damage but never extend control loss |
| Laser sword | 45 damage; 2.3 m reach; 0.12 s windup / 0.13 s active / 0.40 s recovery; bounded swept melee and per-swing deduplication; no ammo |
| Blink | One charge; 6 m maximum short teleport; sweep/clamp before obstruction and require a supported fitting destination; no charge on invalid use |
| Shield | One charge; 60 absorption for 6 s; overflow reaches health; reject active-shield reuse without spending |

Players have 100 health and respawn after 3 s. Death clears specials, grenade ammo,
consumables and active statuses; respawn restores starter clip only. Initial grenade
ammo is zero and comes from pickups. One starter plus one replaceable special slot;
grenade has a dedicated action, and Blink/shield each have one dedicated charge slot.
Pickups initially respawn after 15 s; simultaneous claims commit atomically. Friendly
damage/stun is disabled; self-explosion damage is enabled. Push Lens affects enemies
only. Friendly collision may not trap players in doorways.

Default controls: WASD move, mouse look, Space jump, Shift source movement dash,
Ctrl slide/crouch, left mouse attack, right mouse aim (ADS, revision 4), R reload,
1/2 weapon slots, G grenade (hold, release to throw; revision 4), Q Blink, E shield, F pickup/interact, Tab scoreboard, Escape pause/menu. The existing
movement dash is a traversal action, distinct from the confirmed consumable Blink.
An empty trigger never fires into nothing (2026-10-30; implemented, a proposal pending
playtest).
- Pressing or holding attack on an empty starter clip sends a reload request instead
  of the shot. It retries at most every 0.25 s until the reload starts.
- On a spent special it selects the starter. The still-held trigger does not fire
  the starter until it is pressed again.
- The sword has no ammunition and is unaffected.
- These are client requests through the same actions as R and 1. The host validates
  them unchanged.
Bindings must be displayed and remain remappable through local settings.

Match defaults: 120 points to win, one point per second to the sole occupying team,
8-minute limit, 4 m zone radius and 1.25 m floor-height tolerance; initial zone speed
2 m/s along authored supported routes. Equal top scores at time limit draw. Accepted
same-tick attacks may trade, with costs reserved before resolution; dead-at-acceptance
commands fail. The existing five-phase order remains authoritative. These defaults
do not alter the foundation's 30-second opponent-absence/abort contract.

Pass foundation movement, minimal combat, pickup and scoring fixtures before adding
the roster incrementally. Required cases include:

- Simultaneous pickup claims, full slots, stale requests, wrong owner/life generation,
  repeated use and reconnect: no duplicate item, cost or attack.
- Shield break/expiry and same-tick impact; repeated stun and immunity; melee through
  cover, multiple sweep samples and replay: one correct host verdict.
- Mobility against walls, ceilings, corners, invalid landings and latency corrections:
  no clipping, free successful use, double charge or cross-gap melee hit. Historical
  hits must obey the foundation's time/life-generation limits across the discontinuity.
- Empty, single-team and contested zones; uneven teams; ramps and stacked floors;
  death, disconnect, blink and knockback across the boundary on a scoring tick.
- Late join and migration during each active effect, fuse, swing and scoring change:
  coherent restored state, no duplicate effects or immortal/locked players.
- Moving captures and eight-player host/client measurements under the foundation's
  latency, loss and direct/relay matrix. Graphics and network budgets remain unproved
  until measured; summaries remain host-reported rather than trusted ranked results.


A committed same-life attack recovered from a migration snapshot retains its original
attack identity, including the earlier connection generation. Snapshot validation
accepts that historical generation only when positive and no newer than the player's
current generation. New attack requests still require the current generation; a stale
connection cannot spend ammo or enqueue another attack. The committed attack resolves
once under the restored host state. Native and managed regression evidence is in [QA](../../QA.md).

### 2026-10-28 weapon expression continuation (D026)

The user requests substantially more developed, creative guns, laser blasts and
strong VFX. Builder direction: expand the existing sci-fi arena roster, preserving
its distinct plasma, ricochet, displacement, stun and melee mechanics. Visual-only
polish would leave starter firing unchanged; a new alternate-fire/weapon roster
would expand inventory/network scope unnecessarily. Chosen implementation combines
held rapid starter fire with distinct presentation for the existing roster. This
is a delegated builder choice, not a claim the user approved individual stats.

Line Driver revision 2 defaults: **13 damage, 0.15 s interval, 24-round clip**;
range70m/reload1.2s unchanged. This preserves nominal damage per second and total
clip damage while delivering rapid laser fire. Held fire uses the same shared input
path in Practice and Fusion, bounded by simulation-tick cadence; press/release taps
remain valid. Menus/focus loss/life changes/reconnect clear repeat state; no per-frame
RPC flooding. Host cooldown/ammo/action generation checks remain final authority.
Special weapons retain their current explicit click/timeline behavior.

Presentation identity: cyan core laser with impact sparks; amber plasma core/trail
and expanding blast; coral spinning disc/ricochet flare; violet charge/release
shockwave; electric cyan stun forks/arcs; broad cyan sword sweep. Authored muzzle
positions, distinct recoil/weapon-part motion and distinct [audio](2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30) make
each weapon legible. Events/real timers drive effects; no visual hit detection,
extra damage, global slow motion or new network-owned entities. Eye-based gameplay
traces retain their original authority even when a local cosmetic beam starts at
the weapon muzzle. Keep cover endpoints correct and don't draw through near cover.

Use fixed effect/voice pools, shared materials, capped geometry and explicit expiry.
Reduced motion suppresses flourish/strobe and large recoil while retaining attack
identity. Fire feedback (signal flare, heat glow, cosmetic view punch) is
specified in the [weapon workflow](2026-10-25-characters-weapons-design-workflow.md#weapon-energy-pass-2026-10-30);
it never moves the aim. Death/disconnect/equip/teleport/teardown clears stale local effects.
Acceptance: native hold/release/cooldown/clip/reload/focus regressions; bounded VFX
lifecycle and no simulation mutation; packaged weapon/effect captures; real Photon
combat/reconnect regression and fresh normal player. Human feel/audio listening,
full eight-machine/performance and adverse-network acceptance stay separate.

User clarification: sci-fi arena direction **plus recognizable laser pistol,
shotgun and sniper**. Append these weapons to the special-slot roster; preserve
existing enum IDs and increase catalog revision to 3. New builder defaults:

| Weapon | Initial behavior |
| --- | --- |
| Laser Pistol | 32 damage, 65 m hitscan, 0.28 s interval, 18 shots; compact precise sidearm |
| Laser Shotgun | Eight deterministic 9-damage rays, 5-degree cone, 24 m range, 0.85 s interval, 8 shells; one ammo cost per trigger |
| Laser Sniper | 80 damage, 110 m hitscan, 1.2 s interval, 6 shots; long white laser beam |

Each ray uses existing authoritative bounded hitscan/rewind and cover checks.
Shotgun pellet sub-identities prevent presentation dedup from dropping valid hits;
a trigger emits one shot cue and spends one shell. Revision 3 added no headshot
multiplier, scope input or special reload/reserve rules (historical; revision 4
below adds ADS, spread and head hits; reload/reserve rules remain unchanged). These weapons use the
existing finite special-ammo inventory and replacements, with pickups on all three
maps (map revision 2). Models must have distinct compact/wide/long silhouettes,
editable Blender sources, original palette and validated grip/muzzle/support sockets.
Shotgun rendering may use up to twelve transient lines per trigger (nine initially)
within the unchanged 96-line global pool. Other triggers retain the six-line budget.

### 2026-10-29 competitive gunplay (catalog revision 4)

Status: delegated builder proposal, implemented in code with managed rule tests.
Every number below is a **proposed starting value**, not user-approved balance.
Native PlayMode, packaged and real-Photon results belong in [QA](../../QA.md); none
are claimed here. Map revision 3 and weapon enum/pickup additions are append-only.

Intent: reward aim discipline on hitscan guns through aim-down-sights (ADS),
deterministic spread with bloom, head hits, an automatic Laser SMG and a
hold-and-release grenade, without moving any verdict off the host.

Ownership and authority. The host `ArenaMatch` alone decides ADS progress, spread,
bloom, head classification and damage; Practice runs the same rules locally.
Clients send only intent: the `Aim` bit (`MotorButtons.Aim`, value 8) inside the
existing replicated movement input, and ordinary Fire actions. Clients never send
spread, seeds or head flags. `MotorInput.ForMotor()` strips Aim before every motor
path (Practice and Fusion scheduling), so movement is unaffected by aiming.

State. `CombatState` adds `AimHeld` (from the latest accepted input; the host clears
it after 100 ms without fresh input or on connection reset), `AdsProgress` and
`Bloom` (both finite in [0,1]) and `BloomRecoverTick`. Checkpoint restore rejects
out-of-range or nonfinite values, dead players with nonzero ADS and recovery ticks
more than 0.1 s ahead. Fusion state grows 16 bytes per player; each replicated
combat event gains a 4-byte `Headshot` flag (128-event ring).

ADS. After combat each tick, `AdsProgress` moves toward its target at
1/(ADS time) per second, snapping to exact 0/1 at the ends. The target is held aim
while alive, connected, not reloading and holding a hitscan gun; projectile, melee
and grenade weapons never aim. Weapon switch, reload start, grenade throw, special
pickup equip and death reset it to zero. A shot uses the progress at fire time, so
the next tick sees this tick's change. Sighted guns hide the hip crosshair past half
ADS; only the sniper scopes (overlay at 0.9 or more). Presentation owns the camera
zoom: vertical FOV satisfies tan(v/2) = tan(hip/2) / lerp(1, zoom, ADS).

Spread. Cone half-angle = (hip + bloom cone x Bloom) x lerp(1, ADS scale, ADS).
Each ray direction is deterministic from an integer hash of participant, life,
sequence, tick and authority generation, never `Random`; non-shotgun rays use
square-root radius for uniform density inside the cone, and zero spread is the exact
aim. The shotgun keeps its fixed centre plus seven-ray ring at the cone angle, so
ADS narrows the ring. The Shot event's `Amount` carries the spread used, so peers
replay the authoritative ray (`WeaponCatalog.ShotDirection`). Bloom adds the
per-shot value on each accepted shot (capped at 1), holds for 0.1 s, then decays at
2.5 per second.

| Weapon | Hip cone | Bloom cone at 1 | Bloom per shot | ADS cone scale | ADS time | Zoom | Head multiplier |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Line Driver | 1.1 deg | +1.2 deg | 0.18 | 0.15 | 0.20 s | 1.35 | 1.5 |
| Laser SMG | 1.8 deg | +1.5 deg | 0.12 | 0.30 | 0.15 s | 1.2 | 1.5 |
| Laser Pistol | 0.9 deg | +1.2 deg | 0.35 | 0.20 | 0.15 s | 1.15 | 1.5 |
| Laser Sniper | 6 deg | +1.2 deg | 1.0 | 0 (exact when scoped) | 0.30 s | 4 | 2 |
| Laser Shotgun | 5 deg fixed ring | none | none | 0.85 | 0.20 s | 1 (no sights) | 1 |

Head hits. The head is a 0.17 m sphere centred 0.18 m below the capsule top, so it
drops with slide height. The body shape is the revision 5 torso box
([below](#2026-10-31-playtest-fixes-catalog-revision-5-d040)). A body hit still selects the target; the hit is a head
hit when the ray crosses that sphere before cover. Practice/Unity uses the same
analytic sphere; Fusion uses a lag-compensated sphere `Hitbox` child ("Head hitbox")
whose offset follows the authoritative capsule each tick. Only weapons with a
multiplier above 1 emit `Headshot`; examples are pistol 32 to 48 and a sniper
headshot (160) that defeats full shield plus health.

Laser SMG (enum 11, pickup `laser-smg`): 10 damage, 35 m hitscan, 0.08 s interval,
32 rounds, automatic like the Line Driver. Held fire shares the Practice/Fusion
input path; the host interval and ammo checks stay final. Map revision 3 adds one
SMG pickup per arena (positions in `ArenaLayouts`).

Grenade input: pressing G marks it held (presentation may preview); releasing queues
exactly one throw, so a tap still throws. Focus loss, menus and a new life cancel a
held grenade without throwing. Host grenade rules are unchanged.

Acceptance. Managed tests cover reproducible, bounded, identity-bound spread; ADS
tightening and exact scoped sniper; ADS progress and every drop; bloom growth, hold,
decay and saturation; head classification by capsule height plus multipliers; SMG
pickup, ammo and cover; invalid aim checkpoints; Aim stripped before the motor and
Fusion scheduling; and allocation-free 120 Hz simulation. Native checks still needed:
grenade release and aim input (`PracticeInputTests`), HUD scope and crosshair, FOV
parity, the Fusion head hitbox, and the diagnostic `gunplay` smoke. That smoke tests
sniper no-scope against scoped spread, pistol body 32 against head 48, and SMG
held fire: at least 5 accepted shots, at least interval spacing, growing spread and
peer-matched event identities (the SMG verdict is now the [D036](#2026-10-30-held-automatic-cadence-online-d036) one). It needs `gunplay` added to the network smoke
harness. Human feel, recoil and balance are not claimed.

### 2026-10-30 held automatic cadence online (D036)

Intent: a held Line Driver or Laser SMG fires at its interval for a Fusion client, as it
already did offline. Before this change the client took its request tick from the 20 Hz
model copy, which moves in six-tick steps, so the 10-tick SMG repeated every 12 ticks.
It also gated each repeat on the replicated `FireReadyTick`, which trails the request
clock by the round trip.

Client (`PracticeInput`, shared by Practice and Fusion). A held repeat keeps its own
schedule. If it goes out late by no more than the fire slack, the next repeat is
still one interval after the scheduled tick. If it goes out later than that, the next
one is one interval after the actual send. The replicated `FireReadyTick` only holds a repeat back when
it is more than one interval ahead, which happens after a slower weapon fired before
a switch. A starter reload that ended at least the slack ago counts as refilled, so the
next shot goes out. An empty trigger asks for a reload only while none has started
(`ReloadUntil` is 0), so a finished reload not yet replicated never triggers a second
request. Fusion clients stamp requests with `FusionMatch.NextActionTick`: Fusion's
predicted tick mapped through the replicated `Header.Tick`/`OrbSnapshotRunnerTick`
pair. It holds drops smaller than one second, because during `RecoveryWarmup` the host
keeps `Header.Tick` but advances `OrbSnapshotRunnerTick`. The host keeps
`Model.Tick + 1`.

Host (`ArenaMatch`, final authority). `WeaponCatalog.FireSlackSeconds` is 0.025 s
(3 ticks at 120 Hz, 2 at 60), a proposed default. The host accepts an automatic shot
up to that slack before `FireReadyTick`. A shot within the slack on either side
advances `FireReadyTick` by exactly one interval from the previous ready tick. Any
other shot restarts the schedule from the shot itself. The sustained rate never
exceeds one shot per interval:
- a run of shots is at most one slack faster in total than the interval allows;
- a single gap can shrink by at most twice the slack, when a late shot is followed by an early one.

Per-press weapons and grenades get no slack. `FireReadyTick` is shared across weapons,
so switching from a per-press weapon to an automatic one can fire up to one slack
(25 ms) before the per-press cooldown ends, once. A per-press shot after a carried late
automatic shot can come up to one slack early in the same way. Both are bounded and
never sustained, so no extra replicated state tracks them. A request arriving earlier
than the slack is still rejected. That is a known limit
under large arrival jitter, and host-side deferral would be the later fix.

Acceptance:
- `CombatRulesTests.StarterCadenceClipReloadAndRevisionStayConsistent` covers slack,
  carry, restart and the no-slack pistol.
- `PracticeInputTests.HeldRepeatsKeepTheirScheduleAgainstReplicatedCooldown` covers the
  replicated gate, schedule carry and reload refill.
- The `gunplay` smoke holds a synthetic key on a real `PracticeInput`, scheduled on
  `NextActionTick`, for 0.6 s. It requires:
  - 7–8 accepted SMG shots;
  - no gap below the interval minus twice the slack;
  - a total span no shorter than one slack under the interval rate;
  - a mean spacing within one tick of the interval, at baseline and adverse150.

Feel acceptance remains the user's.

### 2026-10-30 Push Lens deflection (D037)

Status: builder decision under the user's "keep going" direction (deflection itself
was not requested), implemented for playtest with
managed rule tests. Feel and balance are pending the user's acceptance. No native,
packaged or Photon result is claimed here; [QA](../../QA.md) owns observed runs.

Intent: give travelling projectiles the skill-based answer the roster promises
(Pulse Lobber: dodge, take cover or deflect). The Push Lens already commits a
facing and a windup, so its resolution doubles as the deflection window. No new
weapon, input, ammo or tuning value is added.

Ownership and authority. Only the host `ArenaMatch` deflects, inside
`ResolvePending` on the push's resolve tick, before projectiles advance that tick.
Practice runs the same rule locally. Clients mirror the result through the
replicated projectile state and draw the replicated `Deflected` event. They never
predict or request a deflection.

Inputs and state.
- The window, facing and cover test are the push's own:
  - the resolve tick after the 0.18 s windup;
  - the committed `pending.Direction`;
  - the 8 m range and 55-degree cone (dot ≥ 0.8870108);
  - `world.Visible` from the pusher's eye to the projectile.
- `ArenaProjectile.Deflected` is the only new state. It is replicated as
  `FusionProjectileState.Deflected` (one word per projectile slot) and restored
  through `TryRestore`.
- `CombatEventKind.Deflected` is appended at the end of the enum, so earlier
  serialized values keep their numbers. It is a presentation event: Action is the
  push, Target the original shooter, Position the projectile, Vector the push
  direction, Weapon the projectile's weapon.

Behaviour.
- On resolution the push also takes the nearest travelling projectile that is in
  its range and cone, visible, enemy-owned (`OwnerTeam` differs from the pusher's
  team) and not yet deflected. Pulse orbs, discs, stun bolts and grenades all
  qualify. Hitscan, blasts and melee have no projectile, so nothing else is
  reflectable.
- The projectile is relaunched along the push direction:
  - orb, disc and bolt at their own constant speed;
  - a grenade with the ordinary throw velocity along that direction
    (`ArenaMatch.GrenadeLaunch`), so it arcs like a throw.
- Ownership transfers once. `Action` becomes the push action, but keeps the launch
  `Tick` that anchors the lifetime. `OwnerTeam` becomes the pusher's team and
  `Deflected` is set. Every later sweep, blast and hit then follows the existing
  owner rules:
  - it ignores the pusher, not the shooter, so it can hit its original shooter;
  - Hit, Explosion and Impulse events credit the pusher;
  - the pusher's teammates are spared;
  - the pusher can still take self-explosion damage.
- Fuse, `ExpireTick`, travel and range budget, disc bounce budget and hit mask all
  carry on unchanged. Deflection never extends a lifetime, refills bounces or lets
  a disc hit someone again.
- Players in the cone still receive the push impulse as before.

Edge cases.
- A push deflects at most one projectile. Projectile identity is its owning action:
  `TryRestore` requires one live projectile per action, and presentation keys are
  derived from it. Two eligible projectiles at the same resolve tick are rare. A
  wider catch would need a per-projectile identity on the wire.
- A deflected projectile can never be deflected again, so there is no ping-pong and
  no second credit transfer.
- A cancelled push (stun, Blink, death or disconnect before resolution) deflects
  nothing, because `ResolvePending` already drops it. A late request cannot
  resurrect a dead player.
- A projectile that has already exploded or expired has left the list. A fuse ending
  on the resolve tick is committed: that projectile is skipped and detonates for its
  thrower.
- Replay and migration: the flag, owner and hit mask are snapshot state, so a
  restored host or client continues the same deflected flight. One blast or one hit
  per target applies, and no second Deflected event is emitted.
- Presentation-only limit: a deflected projectile's events share the push's action
  identity. Its blast Impulse and the push's own Impulse on the same target, on the
  same tick, collapse to one cosmetic ring in `PresentationEventHistory`. Gameplay
  impulses come from the model and are unaffected.

Presentation (small, existing assets). `ArenaEffects` draws a Push-material ring
along the new direction, a flash and a spark burst tinted by the projectile. It
also re-points the push's remembered context at the projectile's weapon and
direction, so later hits and the blast still read as an orb, disc, bolt or grenade.
`ArenaAudio` plays the `DeflectorParry` bank at pitch 1.3, apart from the shield
break (0.9) and the derez (0.75).

Acceptance (managed, `CombatRulesTests`):
- `PushReflectsEnemyOrbOnceAndItsBlastCreditsThePusherAgainstTheShooter`:
  - an enemy orb in flight is relaunched at 18 m/s along the push;
  - its owner, team and flag change and its fuse holds;
  - it survives a replica `TryRestore`;
  - on host and replica alike it explodes once on its original shooter (60 damage),
    credited to the pusher, with no second deflection.
- `PushReflectsOnlyAVisibleEnemyProjectileInItsConeOnce`: an enemy grenade is
  re-thrown along the push with its fuse unchanged. A projectile outside the cone,
  out of range, behind cover, friendly or already deflected is untouched.
- `LatePushNeitherResurrectsNorRecreditsAConfirmedBlast`: a push resolving on, or
  one tick after, a grenade's fuse tick neither deflects it nor re-credits its
  single blast.
- `PresentationEventHistoryTests.DeflectionHasItsOwnParryCue` and
  `FusionCombatEventTests.ProjectileCuesReachClients` cover the cue and its
  publication.
- The wire field is covered by the existing `FusionCombatMappingTests` round trip.

Online Photon acceptance and the user's feel check remain open.

### 2026-10-31 health packs (D039)

Requested by the user; the amounts are proposals.

- **Item.** `health`, a pickup like `blink` and `shield`, claimed with the same host
  pickup action and checks (living avatar, generation, availability, 2.5 m reach,
  line of sight).
- **Effect.** Health rises by `WeaponCatalog.HealthPack` (50, proposed) up to
  `WeaponCatalog.Health` (100). Shield is untouched. There is no other health
  regeneration; death still resets health on respawn.
- **Full health.** The claim is rejected ("Health full."), the pack keeps its
  generation and stays available, as with a full consumable slot.
- **Respawn.** The shared 15 s `PickupSeconds`.
- **Placement.** Four per arena (map revision 6), owned by the
  [map spec](2026-10-25-wildstone-modern-map-design.md).
- **Network.** The item travels in the existing pickup state; no new wire fields.
  Map revision 6 makes peers on older builds refuse the map.
- **Tests.** `CombatRulesTests.HealthPackRestoresUpToFullHealth` (20 → 70, 70 → 100)
  and `FullHealthLeavesTheHealthPackInPlace`.

Playtest feel, the heal amount and the respawn time remain open.

### 2026-10-31 playtest fixes (catalog revision 5, D040)

The user asked for the root-cause fixes in
[PLAYTEST-FIX-LATER](../../PLAYTEST-FIX-LATER.md) to be implemented. Every number
below is a builder proposal for the user to tune
([D040](../../DECISIONS.md)). Rewind and RPC windows are in the
[foundation spec](2026-10-25-multiplayer-foundation-design.md).

- **Hit shape (items 2, 4, 11).** Both worlds use one shape: the head sphere above,
  and below it a torso box 0.27 m in half-width (`CombatRules.BodyHalfWidth`),
  turned with the avatar's yaw. The box runs from the feet to the bottom of the head
  sphere (`CombatRules.BodyTop`, 1.45 m standing, 0.55 m sliding). A ray past the
  ear, or beside the torso, now misses.
  - Fusion resizes the body `Hitbox` from the authoritative capsule height each tick.
  - In Practice, a ray that crosses the 0.4 m motor capsule must also cross the box
    or the head sphere, or it passes through.
  - Sphere sweeps (travelling projectiles) still use the capsule.
- **Scoped accuracy (item 8).** A scoped weapon's ADS factor is
  `ads / ScopeThreshold` (0.9), clamped to 1. The sniper is exact as soon as the
  scope overlay shows; before that it keeps its no-scope cone.
- **Early semi-automatic press (item 8).** The client holds a press made up to
  `FireBufferSeconds` (0.2 s) before its expected ready tick and sends it once, at
  ready. An earlier press is sent as before, and the host refuses it. A weapon
  switch, a new life or a menu clears the held press. The host cooldown stays
  final. Local shot cosmetics are predicted since the
  [feel pass](#2026-10-31-feel-pass-d041).
- **Blink (item 9).** The host uses the horizontal part of the aim, so looking up
  or down still lands up to 6 m ahead. Aiming straight up or down is refused, and
  the charge is kept. The prompt and refusal cue are in the
  [UI spec](2026-10-25-ui-gameflow-design.md).
- **Special ammo (item 10).** A special weapon pickup gives `SpecialMagazines` (2)
  magazines as one count (`WeaponCatalog.PickupAmmo`), so the sniper gets 12 and
  the SMG 64. There is still no reserve and no special reload. Picking up the same
  weapon refills it to that count. `CombatRules.ValidPlayer` bounds `SpecialAmmo`
  by `PickupAmmo`. The starter and grenades are unchanged. Catalog revision 5 makes
  peers on older builds refuse snapshots.
- **Pickup reach (item 3).** The client prompts and chooses a pickup within
  `PickupReach` (2.5 m) of its predicted feet. The host accepts an extra
  `PickupLagMargin` (0.5 m), because its copy of a running player trails by about
  a round trip.
- **Tests.**
  - `CombatRulesTests`:
    - `TorsoBoxStopsUnderTheHeadAndTurnsWithTheAvatar`
    - `BlinkFollowsTheHorizontalAim`
    - `HostAcceptsAPickupWithinTheLagMargin`
    - scope-threshold asserts in `AdsTightensEveryConeAndOnlyTheScopedSniperIsExact`
    - two-magazine pickup and restore in `LaserPickupEquipsFiniteSpecialAndRejectsOverfullSnapshot`
  - `CombatWorldTests.NativeRaysUseTheOnlineTorsoAndHeadInsideTheCapsule`
  - `PracticeInputTests.EarlySemiAutomaticPressFiresOnceAtReady`

Hit feel, the window size, the buffer length and the ammo count all need playtest.

### 2026-10-31 feel pass (D041)

The gun-feel and movement-feel audit
(`Artifacts/Audit/2026-10-31-feel-audit.md`) was implemented at the user's
direction ([D041](../../DECISIONS.md)). No rule, damage, spread, cooldown or hit
shape changes; the catalog stays at revision 5. Every number is a builder proposal.
Out of scope by decision: the slower air carry decay, surf ramps, accuracy
penalties, recoil patterns, hitstop and the swap delay.

- **Predicted local shot (online).** When the client sends a hitscan `Fire`, the
  coordinator raises a predicted `Shot` carrying the action identity it sent
  (`AuthorityGeneration`, `Participant`, `LifeGeneration`, `Sequence`), the
  predicted motor's eye, the aim and spread 0. Presentation plays it at once:
  muzzle flash, beam, fire sound, viewmodel kick and camera punch. The host's
  `Shot` with the same identity plays nothing again; if its ray ends more than
  `ArenaPresentation.BeamCorrectionMetres` (0.25 m) from the predicted end, the
  beam alone is redrawn along the host ray. A `Rejected` with that identity, or
  `PredictionTimeout` (0.3 s) without a host `Shot`, drops the guess and plays
  nothing more; online peers never receive `Rejected`, so the timeout is the
  refusal path. Hits, hit markers, ammo, kills and the near-miss stay host events.
  Travelling weapons, the sword and grenades are not predicted. Practice is
  unchanged: its rules run locally. The client only guesses what the host will
  confirm; an empty magazine or a press the host refuses shows one false flash and
  sound, then nothing.
- **Movement, recoil, lens, wind, HUD.** Air control is in the
  [foundation spec](2026-10-25-multiplayer-foundation-design.md#5-simulation-input-and-movement-replay);
  recoil, the speed lens and the wind in the
  [weapon spec](2026-10-25-characters-weapons-design-workflow.md); the hit marker
  size, bloom ring and Practice speed readout in the
  [UI spec](2026-10-25-ui-gameflow-design.md).
- **Tests.** `NetworkPresentationBridgeTests.PredictedShotPlaysOnceAndTheHostShotDoesNotRepeatIt`,
  `RejectedPredictedShotPlaysNothingFurther`, `PredictedBeamIsCorrectedToTheHostRay`.

Evidence and limits: [QA](../../QA.md#2026-10-31-feel-pass-d041). Feel needs the
user's playtest.
