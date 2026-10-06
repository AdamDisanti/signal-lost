# Setup and collaboration

## Git and Unity

The project lead has approved the initial Unity project and repository setup. Subsequent development follows the feature-branch and pull-request workflow below.

Install Git LFS using the [official instructions](https://github.com/git-lfs/git-lfs#installing). On macOS with Homebrew: `brew install git-lfs`. Each contributor then runs `git lfs install` before cloning; existing clones should also run `git lfs pull`. The repository's `.gitattributes` sends binary model, image, audio, video, font, PDF, and DOCX files to LFS, including the preserved design document. Scenes, prefabs, `.asset` and `.meta` files remain normal Git text. If a future `.asset` is truly binary, add a specific path rule after reviewing it.

Track `signal-lost/Assets/`, `signal-lost/Packages/` (including `packages-lock.json`), and `signal-lost/ProjectSettings/`. Ignore `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Obj/`, and builds inside the project. Always include asset and folder `.meta` files.

Unity must use **6000.6.2f1**, Force Text serialization, and Visible Meta Files. These settings were confirmed from the project files. See [Unity version-control settings](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/editor-settings-reference/comp-manager-group/class-version-control-settings).

## Teammate onboarding

1. Get repository access from the project lead. Install Git, Git LFS, Unity Hub, and **Unity 6000.6.2f1**. Install build support for the team's agreed target platform.
2. Configure Git identity and clone the repository. Replace the example name, email, and repository URL below with your own values; run these commands in a terminal:

   ```bash
   git config --global user.name "Your Name"
   git config --global user.email "Your GitHub-associated email"
   git lfs install
   git clone <repository-url>
   cd signal-lost
   git lfs pull
   ```

3. In Unity Hub, add the **inner `signal-lost/` folder** containing `Assets`, `Packages`, and `ProjectSettings`. Do not create another project. Wait for asset imports to finish.
4. Read the root README, AGENTS.md, [design summary](design.md), and original design document before gameplay work. Open `Assets/Scenes/Prototype.unity`, check the Console, and enter Play Mode.
5. Confirm Force Text and Visible Meta Files remain enabled. Run `git status` after opening the project; investigate unexpected changes rather than committing them automatically.

If models/textures/audio fail to import, close Unity, confirm `git lfs version` works, and run `git lfs pull` from the repository root. If that fails, check repository access and share the error with the lead before modifying assets.

## Start a new task

The following is the recommended team workflow. Agree on it with the team; remote enforcement is not configured yet. Keep `main` working and submit changes through feature branches and reviewed pull requests.

Save and close Unity before switching branches, pulling, or merging. Start with a clean working tree (`git status`); finish or commit existing work on its current branch before switching. Do not discard work simply to make the tree clean.

From the repository root:

```bash
git switch main
git pull --ff-only origin main
git switch -c feature/player-movement
```

Use a descriptive branch name for one focused task. If the pull fails, investigate rather than force-pushing or resetting. Tell the team on Discord which shared scenes, prefabs, and systems you will edit.

## Unity collaboration rules

- Assign one editor at a time to each shared scene or prefab. Separate feature test scenes and reusable prefabs reduce collisions; agree on who integrates them into the main scene.
- Move and rename assets through Unity's Project window. Commit assets and their `.meta` files together, including folder metadata. Never regenerate metadata to fix a merge conflict: GUID changes can break references.
- Coordinate Unity/package upgrades, asset pack imports, input settings, and project-wide settings with the lead. Keep everyone on the same editor version.
- Keep generated folders out of commits. Inspect unexpected scene/settings changes and exclude unrelated edits.
- Keep branches short-lived and commits focused. Branches do not prevent integration bugs; reviews and Unity checks are still required.

See [Unity asset metadata](https://docs.unity.com/en-us/engine/6000.7/manual/assets-and-media/import-assets/asset-metadata) and [Unity version control / Smart Merge](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/project-configuration/version-control).

## Check and commit your work

Save scenes and prefabs. Verify scripts compile, your feature works in Play Mode, affected existing systems still work, and Inspector references are intact. Record imported asset sources/licenses in [assets.md](assets.md).

Review the change list and stage only intended files, using your IDE's Source Control panel or the terminal. Include every new/changed asset's corresponding metadata. Replace the placeholder below with actual paths; do not type the angle brackets literally.

```bash
git status
git diff
git lfs status
git add <changed-files-and-their-meta-files>
git diff --cached
git diff --cached --check
git commit -m "Add basic first-person movement"
git push -u origin feature/player-movement
```

Before committing, confirm staged files contain no caches, builds, missing metadata, or unrelated settings changes. Binary assets use LFS automatically through `.gitattributes` when staged with LFS installed. `git diff` cannot show every binary/scene change meaningfully; inspect those in Unity too.

## Pull request and integration

Open a pull request targeting `main`. Describe the resulting behavior, tests performed, affected scenes, any required setup, and known limitations. Have another teammate review it; scene/prefab/binary changes should be inspected in Unity.

If `main` changed while you worked, save and close Unity, commit your current work on the feature branch, then run:

```bash
git fetch origin
git merge origin/main
```

Resolve conflicts before committing the merge. For scene/prefab conflicts, coordinate with the other editor; do not blindly choose an entire side. If unsure, stop and ask the lead. To cancel an in-progress merge, use `git merge --abort` (start the merge with a clean working tree).

After merging, reopen Unity, wait for import/compilation, verify affected features and references, and push the feature branch. New commits may require another review. Merge the PR only after review and relevant checks pass. Merge one PR at a time so each integration can be checked.

After your PR is merged, close Unity and start the next task from updated `main` using the commands above. Do not reuse the merged feature branch for another task.

## Project lead checklist

Add teammates with appropriate repository access. Where available, protect `main` by requiring pull requests and at least one approval, and block force pushes and branch deletion. These are recommendations; no GitHub settings have been changed by this setup. See [GitHub branch protection](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches).

Confirm each teammate can open and run the project. Make a build from a fresh clone at each milestone to catch missing tracked files and local-only dependencies. If a merge breaks `main`, coordinate a fix or revert PR rather than rewriting shared history.

## Setup review

- Ignore rules account for the nested Unity project.
- Force Text and Visible Meta Files are enabled; existing assets have metadata.
- Build list points to the existing `Prototype.unity` scene.
- Binary assets have LFS rules; Git LFS is installed and repository-local filters/hooks are initialized. Each teammate still needs their own Git LFS installation.
- README, contributor instructions, original design reference, summary, and team placeholders are prepared.
- Gameplay systems, collaborator permissions, and branch protections remain to be set up.

Static checks passed for ignored generated folders, unignored source files, text/LFS attributes, asset metadata presence, matching build-scene GUID, and an identical copy of the source document. Unity compilation, Play Mode, and a playable build have not been run. The project lead approved publishing this setup on October 6, 2026.
