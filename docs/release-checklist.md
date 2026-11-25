# Release checklist

A release is `.changeset/*.md` files plus `VERSION`, turned into a tag and a forge release by
`.forgejo/workflows/release.yml`, and by nothing else. A by-hand build for testing calls `AetheriaBuild` directly and publishes
nothing. A published version is never edited; a mistake is a new version.

## Around every change

- A player-visible change lands with a change file, written by whoever lands it (agents included): `knope document-change`,
  or a file `.changeset/<name>.md` holding front matter `default: patch|minor|major` and one player-facing line.
- Below 1.0 knope bumps one step lower than the level named: `patch` and `minor` give 0.x.(y+1), `major` gives 0.(x+1).0.
  Choose `major` when the release should be a new minor line.

## Cutting a release

1. Check the change files on the branch read as the changelog you want.
2. Dispatch the `release` workflow on that branch (Actions tab). It runs on Starfire, one job at a time. It prepares the
   release, builds win64 then linux64, and only then pushes the `Release X.Y.Z` commit and tag `vX.Y.Z` (one atomic push)
   and publishes. The first step checks every host prerequisite and fails before any build or push. The first release this
   pipeline makes is v0.2.1.
3. Check the forge release: the notes match the CHANGELOG section, and `Aetheria-X.Y.Z-win64.zip` and `-linux64.zip` are
   attached. Unpack both and launch. The zips are made on Windows and lose the exec bit, so on Linux run
   `chmod +x Aetheria.x86_64 ModTools/AetherDb` first (those are the only executables in the linux64 player).
4. If the run fails after the push (the forge release step), the commit and tag are already on the forge and the build
   directory `../release-<run id>` is kept on Starfire. Do not rerun: the tag exists and the change files are consumed.
   Create the release by hand on tag `vX.Y.Z`, paste the CHANGELOG section as notes, attach the two zips from that
   directory's `dist/`, then delete the directory. A failure before the push needs only a rerun (delete the leftover build
   directory).
5. Mirroring to GitHub is by hand if wanted; there is no GitHub automation.

## One-time setup (operator acts)

- Register the runner on Starfire with label `starfire` in host mode, capacity 1, running as the operator's Windows user
  (follow-up `starfire-forge-runner`). Runner prerequisites, all checked by the workflow's preflight: Git for Windows at
  `C:\Program Files\Git` (the workflow pins its `bash.exe`), and on PATH `knope`, `jq` and `curl` (both needed by the forge
  release action), `git`, `dotnet` (publishes AetherDb) and `tar`; `C:\Windows\System32	ar.exe` makes the zips.
- Repo secret `RELEASE_TOKEN` (write:repository); repo variables `UNITY_EDITOR` (Unity.exe, 6000.3.24f1), and `CULTLIB_ROOT`
  and `CULTMATH_ROOT` (checkouts holding the pinned CultLib and CultMath revisions). All are required.
- Ruling `sbsar-lfs-objects`: prune the 41 `.sbsar` LFS objects from Forgejo's LFS store and the Raven LFS mirror. Git
  history keeps its pointers. This is an operator act, not the workflow's.
