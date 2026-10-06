# Signal Lost

A low-poly first-person action-adventure game for a Unity course, built by a six-person team. A stranded space mechanic scans an alien planet, fights creatures with a mining laser, and recovers four ship components.

## Open the project

1. Install Git LFS and run `git lfs install` before cloning. For an existing clone, run `git lfs pull` after installation.
2. Install **Unity 6000.6.2f1** through Unity Hub.
3. In Hub, add the **`signal-lost/` subfolder**, then open it. This is the Universal 3D/URP project (URP 17.6.0).
4. Open `Assets/Scenes/Prototype.unity`. The current project is a template foundation; gameplay is not implemented yet.

Keep Force Text serialization and Visible Meta Files enabled. Track `Assets/` with its `.meta` files, `Packages/`, and `ProjectSettings/`; generated caches and local settings are ignored.

## Teammates: start here

Follow the [teammate setup and commit guide](docs/workflow.md#teammate-onboarding). It covers cloning with Git LFS, opening the correct Unity folder, feature branches, selective commits, pull requests, and conflict handling.

Use feature branches and reviewed PRs into `main`. Coordinate shared scene/prefab ownership, include `.meta` files, and verify compilation and Play Mode before requesting a merge. Save and close Unity before switching branches, pulling, or merging.

## Team and documentation

See [docs](docs/README.md), [design summary](docs/design.md), [team](docs/team.md), and [collaboration workflow](docs/workflow.md). Read the [supplied game design document](docs/reference/Game%20Design%20Document.docx) before gameplay decisions. `docs/` can be opened as an optional Obsidian vault; no plugins are required.

Initial setup has been approved by the project lead. Use the documented branch and review workflow for subsequent development.
