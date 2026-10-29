# Documentation index

**Project resumed for the full MVP.** The [implementation plan](superpowers/plans/2026-10-25-wildstone-mvp.md)
tracks execution. The [complete pause handoff](handoffs/2026-10-25-paused-project.md)
preserves the historical design snapshot.

Read [HANDOFF](HANDOFF.md) for current evidence and next work,
[BACKLOG](BACKLOG.md) for stages, and [DECISIONS](DECISIONS.md) for accepted choices.
The root [README](../README.md) describes the workspace; [AGENTS](../AGENTS.md)
defines contributor practice.
Latest local polish includes sixteen inspected packaged captures and a five-minute
eight-process Photon soak; [QA](QA.md#2026-10-28-unattended-polish-and-current-build)
records the current build, exact results and remaining external/input gates.
Use [QA](QA.md) for evidence limits and remaining acceptance commands,
[TOOLING](TOOLING.md) for local tools and [USER-SETUP](USER-SETUP.md) for service setup.

## Sources of truth

| Document | Owns |
| --- | --- |
| [Multiplayer foundation](superpowers/specs/2026-10-25-multiplayer-foundation-design.md) | Authority, identity, sessions, movement replay, reconnect/migration timing and runtime/network acceptance budgets |
| [Arena combat, items and zone](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md) | Weapon behavior, damage/status interactions, consumables, pickups and moving-zone scoring |
| [Unity rendering](superpowers/specs/2026-10-25-unity-rendering-design.md) | Soft-light stylization, materials, lighting, effects and graphics cost strategy within foundation budgets |
| [Wildstone Modern map](superpowers/specs/2026-10-25-wildstone-modern-map-design.md) | Architectural language, geometry, routes and spatial validation; not zone scoring rules |
| [Character/weapon workflow](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md) | Reuse audit, production sheets, rigs, readability and first-/third-person presentation checks |
| [Asset pipeline](ASSET-PIPELINE.md) | Blender sources, source-project reuse, exports, Unity authoring/import and round-trip validation |
| [Menus and game flow](superpowers/specs/2026-10-25-ui-gameflow-design.md) | Minimalist presentation, screen transitions, local settings, HUD and UI acceptance |
| [Art gallery](art-direction/README.md) and [weapon concepts](art-direction/weapon-concepts.md) | Saved visual references and prompts; not balance, collision or performance authority |
| [Historical arena proposal](superpowers/specs/2026-10-25-multiplayer-arena-design.md) | Retired planning context and reuse leads only; current contracts above supersede it |

Accepted intent does not imply every detail in a spec is accepted. Proposed stats,
technical feasibility and test outcomes must remain visibly distinguished. If
documents conflict, reconcile the owning spec with the recorded user decision;
do not silently implement an older proposal. Detailed rules live once, in their owner.

Keep actual code, focused tests and evidence linked from the
owning spec or handoff. Add new documentation only when a distinct contract or
reusable procedure needs a home. Keep historical rationale without presenting
retired topology or art options as active requirements.
