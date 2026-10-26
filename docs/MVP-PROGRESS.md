# MVP execution ledger

Plan: `docs/superpowers/plans/2026-10-25-wildstone-mvp.md`.
Goal active; user explicitly resumed and delegated all work possible while away.

## Current position

- Task 1 in progress: source audit and installed-tool discovery complete; project setup next.
- Tasks 2-8 pending. No Unity build or gameplay/network test has passed yet.
- Actual source HEAD: `2cb6e5760bccc0733f34b367be2f18c90b802766`; pinned selective reuse must record this newer source instead of assuming the old design audit commit.
- Unity 6000.5.10f1 is installed. Unity MCP currently reports no connected Editor. No source Editor will be used for destination mutations.
- Official Fusion 2.1.3 SDK download requires signed-in access; no local SDK found. User needs help creating the free app after returning. No account has been created, no credentials requested and no paid action performed.
- Blender executable absent; source isolated Python has bpy 4.2 metadata. Execution/axis roundtrip remains to be verified.

## Rulings

- Ruling: work directly in the user's separate `multiplayer_1` project, with a new local MVP branch if permitted; no source worktree is needed because this is already the intended isolated destination.
- Ruling: user approval to build the discussed game supersedes the pause and avoids re-asking design/approach approval. Preserve prior visual and game decisions.
- Ruling: D018 permits independent rules/assets/UI work while SDK/account input is unavailable; this does not waive actual Fusion, identity or internet acceptance.
- Ruling: source laws/prefab tuning and pure geometry are adaptation inputs; do not copy source `Library`, entire manifest, campaign/player prefabs, untracked audio or mutable global managers.

## Preflight interface/conflict scan

| Tasks | Boundary checked | Result |
| --- | --- | --- |
| 1 | project root/package/tool outputs and its own compile gate | Explicit local outputs, no source mutation; SDK is a visible dependency |
| 2 | state/input/tuning and replay tests | State restoration is not source Teleport; includes private traversal timers |
| 3 | session seats/identity/recovery tests | Local identity cannot satisfy verified online admission |
| 4 | action state/phase ordering/test cases | Stable generations/action IDs and published phase order |
| 5 | rig/export/presentation tests | Effects cannot own gameplay, single axis convention |
| 6 | map definitions/build/validators | Three distinct IDs and supported routes, no campaign builder dependency |
| 7 | UI/settings/player flow | Local settings are separate from authoritative state; practice labeled |
| 8 | acceptance/evidence/packaging | Local/offline/online/external gates remain distinct |
| 1 -> 2-7 | Unity packages and runtime assembly boundary | Pin minimal dependencies; confirm APIs in installed Unity |
| 2 -> 3,4 | MotorInput/MotorState/collision | One simulation path consumed by local test driver and actual Fusion |
| 3 -> 4,7 | ownership/session events/match state | UI sends intent; no trusted client damage/team assignments |
| 4 -> 5,6,7 | combat events, catalog IDs, objective state | Presentation and map assets consume stable IDs only |
| 5 -> 6,7 | material/model/socket conventions | Validate roundtrip before expanding assets |
| 6 -> 3,4,7 | map ID, route/spawns, selection | Content compatibility checked before online spawn |
| 2-7 -> 8 | build/log/test artifacts | Capture precise identity and real limits; no inferred passes |

## Source audit evidence

Independent read-only audits completed for movement/dependencies, art/authoring and requirements/QA. Detailed reuse inventory will be saved alongside the first imports. No source files changed.
