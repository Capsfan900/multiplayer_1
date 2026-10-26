# Arena combat, items and moving zone

Date: 2026-10-25. Status: confirmed content scope with proposed gameplay rules;
no numeric balance or runtime behavior is approved or proved by this document.
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
| Pulse Lobber | Visible travelling orb with a bounded blast and impulse | Slow identifiable projectile, localized detonation cue; dodge, take cover or use supported deflection |
| Rebound Disc | Travelling disc with bounded ricochets and lifetime | Visible flight/bounce cue; change route or put cover between yourself and its next segment |
| Push Lens | Short-range directional displacement attack with a committed windup | Clearly indicated facing/reach; leave its cone or break sightline before resolution |
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
