# Decision record

Stable IDs identify decisions; keep superseded entries as history when choices
change. Accepted scope is not evidence of implementation. Detailed contracts live
in the [specification index](README.md).

| ID | Date | Status | Decision and rationale / alternative |
| --- | --- | --- | --- |
| D001 | 2026-10-25 | Accepted | Unity FPS; reuse `vibegame1` in a separate project and preserve its source. Audit/adapt existing systems before replacement. |
| D002 | 2026-10-25 | Accepted | Internet friend matches, four teams of up to two, uneven teams allowed; foundation precedes arena production. |
| D003 | 2026-10-25 | Superseded | Photon Fusion with dedicated servers was the earlier choice; D004 replaces its hosting topology. |
| D004 | 2026-10-25 | Accepted | Fusion Host Mode: one player hosts, managed direct/relay connectivity. Avoid rented game servers and custom backend administration; acknowledge host trust and advantage. |
| D005 | 2026-10-25 | Accepted | C3.1 / Wildstone Modern: soft lighting, modern mostly monochrome forms, simple humans. Earlier animal characters and alternative style boards are historical. |
| D006 | 2026-10-25 | Accepted | Keep Line Driver, Pulse Lobber, Rebound Disc and Push Lens concepts; include grenades, stun guns and laser swords. Exact stats remain proposed. |
| D007 | 2026-10-25 | Accepted | Exactly two initial special consumable types: short Blink teleport and temporary shield. Blink replaces the dash alternative and does not phase through walls; tuning remains proposed. |
| D008 | 2026-10-25 | Accepted | Moving capture zone remains the primary objective; weapon races support it. Geometry and scoring have separate spec owners. |
| D009 | 2026-10-25 | Proposed | Exact Fusion/Unity compatibility, managed PlayFab identity, service configuration and performance targets await the foundation gates. Dedicated deployment and paid plans are not authorized. |
| D010 | 2026-10-25 | Accepted | Maintain supporting docs/specs throughout development: one canonical contract per concern, current handoff and evidence tied to changes. |
| D011 | 2026-10-25 | Accepted | Simplify all four accepted weapons to fit the game: broad shapes, matte surfaces, restrained detail and soft shading; preserve their identities. |
| D012 | 2026-10-25 | Accepted | Use Blender and Unity tooling for maps, players and weapons. Audit `vibegame1` assets/systems for reuse first; build the multiplayer foundation separately from its single-player architecture. |
| D013 | 2026-10-25 | Accepted | Also inspect `ai_skelly_tool` for useful rigging, attachment, export and validation helpers. Reuse selectively after dependency/provenance checks; do not adopt its generation stack by default. |
| D014 | 2026-10-25 | Accepted | Use all four simplified `*-simple-v2.png` weapon designs as the approved shape/material references for Blender and Unity production. Keep their identities and soft modern treatment; implementation, numeric balance and performance still require validation. |
| D015 | 2026-10-25 | Historical pause | User requested all work stop and complete handoffs be saved. D016 resumes execution; the original pause packet remains unchanged. |
| D016 | 2026-10-25 | Accepted | User selected Photon Fusion after research and started an active goal for the complete working/tested MVP: multiplayer, full approved weapons/items/zone, three maps, player/weapon models, VFX and initial game flow. |
| D017 | 2026-10-25 | Accepted | Menus must be minimalist and good looking, matching the soft modern game presentation. |
| D018 | 2026-10-25 | Accepted | User is stepping away: complete all work possible without their account/input, then provide an exact remaining setup/test report. Do not fabricate account setup or internet test evidence. No paid services or purchases authorized. |
