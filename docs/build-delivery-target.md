# Build Delivery Target

Date: 2026-09-17

Status: target. Operator direction only; no substrate map or cut map yet. This
document owns the ends. A cut map, written after an Imagination pass over the
Body, owns the means.

## Why

Aetheria has no player build pipeline and no way for anyone to play it. Builds
are Unity batchmode runs by hand, Windows 64-bit only, and the output never
leaves the machine (`Build/`). Nothing in GameCult publishes a downloadable
artifact: Idunn deploys services to Yggdrasil, not files.

## Operator direction (2026-09-17)

- **A build is downloadable from the GameCult site, served from Yggdrasil.**
  Operator: "deployment to a build downloadable from our site via Yggdrasil".
- **Public.** No login, no gate. Operator: "public, with a version feed and
  changelog pages".
- **A version feed and changelog pages come with it**, not as a later nicety.

## Ends

- **A build is an artifact with an identity**: a version, the commit it was
  built from, the platform, a size and a hash a downloader can check.
- **Publishing is one action**, and it publishes the artifact, the feed entry
  and the changelog entry together, or it publishes nothing.
- **The feed is the authority on what exists.** The site's download page and
  the changelog pages are projections of it, never separately edited truth.
- **A published version stays published.** Its bytes and its entry do not
  change under it; a mistake is a new version, not an edit.
- **The download page tells a visitor what they are getting**: version, date,
  platform, size, and what changed since the last one.
- **A build is reproducible from its commit**: the feed entry names it, and a
  build from that commit produces the same artifact contents.

## Invariants that must survive

- **State is CultCache, not JSON** (`F:\Projects\CLAUDE.md`). The release
  record and the changelog are typed documents. Anything a browser eats is a
  projection at the xenos boundary, and is derived, never authored.
- **Idunn owns *service* deployment to Yggdrasil**, and only that. Probed
  2026-09-17 (`docs/build-delivery-cut.md`): every Idunn unit is a supervised
  process that must publish signed health, and Idunn pulls rather than pushes, so
  it cannot carry a file artifact. Publishing uses the host's existing static-file
  path and does not invent a second *service* deployment authority.
- **The site's brand is the site's** (`F:\Projects\gamecult-site`), and the
  download and changelog pages follow it.
- **The build host is Windows.** Unity 6000.3.24f1 builds on Starfire; the
  serving host is Linux. Say which host produced an artifact.
- **Git LFS and repo size** are part of the problem: the checkout alone is
  1.7 GB. A build path that needs a fresh clone per release has to say so.

## Not in scope

- Auto-updating clients, launchers, patching, or delta updates.
- Platforms other than Windows 64-bit, until one is asked for.
- Accounts, entitlements, or telemetry. The download is public and anonymous.
- Selling anything.
