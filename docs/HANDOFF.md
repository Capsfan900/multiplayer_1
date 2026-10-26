# Handoff

Updated: 2026-10-25. **Status: ACTIVE MVP GOAL, resumed by the user.**
The user selected Fusion, requested a complete MVP with three maps and minimalist
finished menus, and authorized all work possible while they are away. Follow the
[implementation plan](superpowers/plans/2026-10-25-wildstone-mvp.md) and
[live progress/evidence](MVP-PROGRESS.md).

Read the [complete pause handoff](handoffs/2026-10-25-paused-project.md) for the
full workstream inventory, approved references, source-audit findings, open choices,
verification limits and copyable resume prompt. A local archive and SHA-256 manifest
are saved under `handoffs/` at the project root. This packet is historical and remains unchanged.

## Current state

The workspace contains supporting docs, specs and saved concept images. No Unity
project import, gameplay code, playable build or runtime verification exists here
yet. The documents describe intended behavior and proposed validation, not proof.

Accepted choices are recorded in [DECISIONS](DECISIONS.md). Foundation first;
player-hosted Fusion Host Mode; Unity with separate-project `vibegame1` reuse;
C3.1 soft-lit modern monochrome architecture and simple human characters.
All four weapon concepts are accepted, alongside grenades, stun guns and laser
swords. The user also approved all four simplified `*-simple-v2.png` designs as
the production shape/material references (D014); preserve that selection during
Blender/Unity work. Initial special consumable types are exactly Blink and temporary shield.
Blink is a short teleport blocked by walls, not a dash or wall-phasing ability.
Detailed statistics and interaction proposals remain in the combat spec.

## Available deliverables

- Foundation, map and character/weapon workflow specs, plus rendering and
  combat/items/zone design work; navigate through the [index](README.md).
- The C3.1 concept and earlier art comparisons, retained with prompts.
- Four accepted weapon identities with new simplified images: W01 Line Driver,
  W02 Pulse Lobber, W03 Rebound Disc and W04 Push Lens. The
  [gallery](art-direction/weapon-concepts.md) links both passes and saved prompts.
- A [Blender/Unity asset pipeline](ASSET-PIPELINE.md) grounded in source tooling;
  source bevel geometry, material/shader and export leads are documented for reuse.
  The secondary `ai_skelly_tool` audit identified rig binding, hand/socket, FBX export
  and deformation-validation helpers; nothing in either source project was executed
  or edited. Generator scripts' output paths/dependencies still require adaptation.
- Maintenance entrypoints, staged backlog and decision record.

These images are concept illustrations, not meshes, Unity renders or performance
evidence. Older arena/topology proposals are historical.

Latest documentation checks: independent combat/rendering consistency review found
no consequential unresolved question; local links and saved image hashes are checked
before handoff. This verifies documents/artifact copies only. The read-only rendering
audit found lightmap sampling in the source shader but missing generated UV2 and an
unverified material bake contribution path; do not treat source reuse as bake proof.

## Current implementation and user-dependent work

The staged implementation plan and fresh source audits are complete. Establish the
separate compiling Unity target, adapt the source movement state/laws and verify rules.
Proceed with independent assets and menus while SDK/account provisioning is pending,
under the user's explicit D018 instruction. Do not confuse this with passing the
Fusion/internet gates. Source HEAD is now `2cb6e5760bccc0733f34b367be2f18c90b802766`.

Photon's official Fusion 2.1.3 download requires a signed-in account; no local SDK
was found. The user needs help creating a free Fusion app and will return later.
Leave exact SDK/App ID/identity setup and external-machine test steps in the final
report; do not request passwords, purchase services or claim unrun internet tests.

Still open: exact compatible SDK version, managed Photon/PlayFab account and plan
configuration, region, reference host/client hardware and uplink, numeric combat
balance, production assets and map layout. No paid service setup is authorized.
Future verification reports must identify what ran, its result and any limits;
there are no runtime test counts or performance measurements to carry forward.
