# Working in multiplayer_1

Before work, read [docs/README.md](docs/README.md), [HANDOFF](docs/HANDOFF.md) and
the relevant canonical spec. For continuation, use [BACKLOG](docs/BACKLOG.md) to
find the next actionable stage; confirm current evidence before treating a stage
as complete. Unity MVP implementation is active; consult the progress ledger for
the distinction between authored files, passing local checks and online evidence.

## Preserve the project boundary

- Audit `vibegame1` code, assets, dependencies and licenses before reuse. Prefer
  adapting established movement and level-authoring systems over replacing them.
- Inspect `ai_skelly_tool` selectively for useful rig, socket, export and validation
  helpers. Reuse only understood dependencies; do not default to its GPU generation
  pipeline or run scripts with source-project output paths.
- Use Blender and Unity authoring tooling under [ASSET-PIPELINE](docs/ASSET-PIPELINE.md)
  for production maps, humans and weapons. Keep editable source assets and reproducible
  exports; generated concept images are references. Build multiplayer authority,
  synchronization and prediction independently of the source's single-player globals.
- Keep the original source project and unrelated user work untouched. Imports belong
  in this separate project and must preserve required asset GUIDs and dependencies.
- Do not import EnemyForgeLab rules, another checkout's Unity instance identifiers,
  commands, credentials or release assumptions. Establish this project's actual
  tooling before documenting runnable commands.

## Keep changes maintainable

Use the smallest complete change. For every feature or behavior change, update its
canonical contract, relevant verification evidence and handoff in the same work.
Update the backlog and decision record when status or an accepted decision changes.
Extend an existing spec when it owns the behavior; avoid one document per tiny edit.

A feature spec needs intent, ownership/authority, inputs and state, behavior and
edge cases, and acceptance criteria. Include effects/readability and performance
budgets when relevant. Clearly distinguish accepted requirements, proposed values,
implemented behavior and measured results. Do not silently promote a proposal.

Use the ownership table in the index. Reference another spec instead of copying
its timing constants, balance rules or test totals. Galleries are visual evidence,
not balance specifications. Label retired proposals historical and link replacements.

Add meaningful checks for new nontrivial behavior and regressions; test observable
outcomes and failure cases rather than duplicating implementation. Run checks
appropriate to the change, recording commands, build identity, results and limits
when available. Documentation-only work needs consistency/link checks, not a Unity
test run. Do not claim runtime success from documents, concept images or compilation.

Keep secrets out of files and builds. Follow the user's existing authorization;
the design does not authorize paid accounts, service deployment or subscriptions.
