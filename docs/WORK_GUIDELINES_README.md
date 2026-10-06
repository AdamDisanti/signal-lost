# How we build Signal Lost

Start with the [setup and Git workflow](workflow.md) to get the project running. Read [the design summary](design.md) and original game design document before gameplay decisions. Use Unity **6000.6.2f1** and the existing URP project.

## Work in small playable steps

Take one focused task on a feature branch. Agree on its outcome, acceptance checks, and the scenes/prefabs you will edit before starting. As teammates join, assign tasks they can develop independently. One designated integration owner connects reviewed work in the shared prototype scene; agree on that person through Discord.

| Sequence | Playable outcome | Design-document owner / coordination |
| --- | --- | --- |
| 1 | Walk, look, sprint, and jump in a graybox area | Amanda + Lawson; initial foundation started by Adam |
| 2 | Mining laser damages an enemy; enemy damages player | Christian |
| 3 | Scanner direction, strength, and sound locate a target | Amanda |
| 4 | Health, flask healing, death, and respawn | Amanda; clarify checkpoint/flask rules first |
| 5 | Scrap collection, HUD, and objective feedback | Christian + Nicole |
| 6 | Three encounters reveal a scannable boss; boss drops a component | Team integration |
| 7 | Basic laser upgrade, quest/shop, settings, and tutorial flow | Assign as teammates become available |

This is an implementation sequence toward the document's prototype goals, not a new scope commitment. Use simple shapes and placeholder UI until the gameplay works. Tune provisional numbers through testing. Resolve ambiguous design rules with the lead rather than having an LLM invent them.

## Work with an LLM

Give your assistant repository access and one concrete outcome. Tell it to read README.md, AGENTS.md, and the relevant docs; some tools do not load AGENTS.md automatically.

Example task prompt:

> Read AGENTS.md and the design docs. Implement the assigned feature on a feature branch using the existing Unity version and packages. Coordinate shared assets, use a separate test scene where practical, and explain any Unity editor steps I must perform. Run available checks and report anything you could not verify. Do not commit or push yet.

1. Have the assistant inspect existing code, input actions, and relevant assets before editing.
2. Let it implement the requested change and explain how to run it.
3. In Unity, wait for compilation/import, follow any Inspector setup instructions, and test in Play Mode.
4. Share Console errors as text and describe the actual and expected behavior. Fix and retest until the acceptance checks pass.
5. Review the changed files, then explicitly authorize a commit/push and open a pull request. Another teammate reviews the result before integration into `main`.

Repo access does not guarantee the assistant can install Unity, authenticate to GitHub, operate the editor, or verify gameplay. Treat its validation report literally; static checks are not Play Mode checks.

## Coordinate editor and assistant work

- Save editor changes before asking the assistant to inspect or edit the same assets. Avoid simultaneous edits to the same file.
- Prefer Unity-generated scenes/prefabs and preserve their metadata. Move/rename assets through Unity.
- Save and close Unity before branch switches, pulls, or merges. If the assistant runs Unity automation, coordinate the editor session or use an isolated project copy.
- Use separate feature test scenes and reusable prefabs. Assign one editor at a time to a shared scene/prefab and announce ownership in Discord.
- Merge small reviewed changes, then test their interaction in the shared scene. Do not commit caches, missing metadata, or unrelated settings.

Example: movement uses `MovementGraybox.unity`; combat can have its own test scene. Both use the player prefab once that dependency is merged. The integration owner combines reviewed systems in `Prototype.unity` and checks them together.

## Done means checked

A task is ready for review when scripts compile, its acceptance checks pass in Unity, affected existing behavior still works, Inspector references are intact, and required source/metadata/docs are included. Record checks and limitations in the PR. Follow [workflow.md](workflow.md) for exact Git commands and conflict handling.

For the first task, see [movement prototype instructions](movement-prototype.md).
