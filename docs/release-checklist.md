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
   release, builds win64 then linux64, and only then pushes the `Release X.Y.Z` commit and tag `vX.Y.Z` and publishes.
3. Check the forge release: the notes match the CHANGELOG section, and `Aetheria-X.Y.Z-win64.zip` and `-linux64.zip` are
   attached. Unpack both and launch. The zips are made on Windows, so on Linux run `chmod +x Aetheria.x86_64` first.
4. Mirroring to GitHub is by hand if wanted; there is no GitHub automation.

## One-time setup (operator acts)

- Register the runner on Starfire with label `starfire` in host mode, capacity 1, running as the operator's Windows user
  (follow-up `starfire-forge-runner`); install knope on that user's PATH.
- Repo secret `RELEASE_TOKEN` (write:repository); repo variable `UNITY_EDITOR` (Unity.exe, 6000.3.24f1); optional variables
  `CULTLIB_ROOT` and `CULTMATH_ROOT` when the default CultLib checkouts do not hold the pinned revisions.
- Ruling `sbsar-lfs-objects`: prune the 41 `.sbsar` LFS objects from Forgejo's LFS store and the Raven LFS mirror. Git
  history keeps its pointers. This is an operator act, not the workflow's.
