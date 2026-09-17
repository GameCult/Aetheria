# Build Delivery: Cut Map

Date: 2026-09-17

Status: Imagination pass, cut map. Nothing here has landed. Anchors are against
`codex/item-provenance` HEAD `6dc18607`. Ends are owned by
`docs/build-delivery-target.md`; this document owns the means.

Claims marked **(probe)** were run, not read off names. What was run:

- A real batchmode player build of the main tree, Unity closed, output to a
  scratch directory outside the repo:
  `Unity.exe -batchmode -quit -nographics -silent-crashes -projectPath F:\Projects\Aetheria -buildWindows64Player <scratch>\playerbuild\Aetheria.exe -logFile <scratch>\unity-player-build.log`.
  No Editor script was added; `-buildWindows64Player` needs none. Exit code 0.
  It dirtied one tracked file, `ProjectSettings/UnityConnectSettings.asset`,
  which was restored to `6dc18607`; the other 16 modified files in the worktree
  predate this pass. Build output and the zip were deleted afterwards.
- A headless run of the produced player:
  `Aetheria.exe -batchmode -nographics -logFile <scratch>\player-run.log`, killed
  at 25s.
- Reads of `gamecult-ops` inventory, runbooks, `nginx/`, and its deploy scripts;
  Idunn `src/` and `docs/`; `gamecult-site`, `AetheriaLore` and `GameCult-Quartz`
  configs and workflows; the `CultLib-semver` worktree.

No remote host was contacted. No `ssh` was run.

Rulings (operator, 2026-09-17): **F2, the Aetheria site** (`aetheria.gamecult.org`,
repo `AetheriaLore`) owns the download and changelog pages ("That's the Aetheria site, of
course"); `gamecult.org` links to them. **F1 no** (Idunn stays service-only; the artifact
uses the host's existing static path), **F3** semver for builds, commit recorded beside it,
**F4** hand-written changelog, **F5** agent builds, hashes and records while the operator runs
the remote publish, **F6** zip, **F7** a new `aetheria.release` document: Self's defaults,
stated to the operator, who did not object. Reopen any on request.

## 0. What the probes changed about the target

Four findings move the design. Read these before the cuts.

### 0.1 A player build already works **(probe)**

The target says Aetheria "has no player build pipeline"
(`docs/build-delivery-target.md:11`). Half true: there is no build *code* — zero
`BuildPipeline`/`BuildPlayer`/`BuildTarget` references in any tracked `.cs`, and
`Build/` holds only Eve asset bundles and logs, never a player. But the project
itself builds clean today with the stock command-line switch.

- Scripts compiled for StandaloneWindows64 with warnings only (`CS0067` on
  `Assets/Scripts/ServerShared/Behaviors/LockWeapon.cs:44-46`).
- ~9 minutes wall on Starfire with a warm `Library/`.
- Output 212 files, **352.4 MB** on disk; **189.8 MB** as a zip
  (`Compress-Archive -CompressionLevel Optimal`, 35s).
- Scenes come from `ProjectSettings/EditorBuildSettings.asset:8-13`:
  `Assets/Scenes/Main Menu.unity`, `Assets/Scenes/ARPG.unity`, both enabled.
- Scripting backend is Mono, not IL2CPP: `ProjectSettings/ProjectSettings.asset:660-661`
  sets `scriptingBackend` for Android only, so Standalone takes the default.
- `Aetheria_BurstDebugInformation_DoNotShip/` is emitted beside the exe and must
  not ship.

So Cut 1 is not "make a build possible". It is "make a build repeatable,
identified, and complete", which is a much smaller cut.

### 0.2 The catalog gap is silent, not loud **(probe)**

`ActionGameManager.cs:38-41` resolves `GameDataDirectory` as
`new DirectoryInfo(Application.dataPath).Parent.CreateSubdirectory("GameData")`,
and `:45` builds `CatalogPath` from it. In a player that is
`<build>\GameData\Aetheria.cc`. Nothing in the repo copies the catalog there.

The dangerous part is what happens next. `AetheriaStores.cs:13-15` promises the
open "Throws when a `[CultGlobal]` type routed to the catalog has no record", and
`:26-31` implements exactly that. But none of the seven catalog types at
`AetheriaStores.cs:9` is `[CultGlobal]`, so the guard selects nothing and the
open succeeds over a catalog file that does not exist.

Proven, not inferred: the built player launched, reached Main Menu, ran for 25s,
and wrote `<build>\GameData\player.cc` (1,964 bytes) — while `Aetheria.cc` was
absent the whole time. No exception, no log line about the catalog. The only
errors were `Stardust` compute-shader nulls, which are `-nographics` artefacts.

A player shipped without its catalog therefore does not refuse. It boots to a
menu and fails later, in the ARPG scene, with an empty world. That is worse than
a crash, and Cut 1 has to close it.

### 0.3 Idunn structurally cannot carry a file artifact

The target invariant reads "Idunn owns deployment to Yggdrasil"
(`docs/build-delivery-target.md:44`). Idunn cannot own this one.

- `Idunn/src/deployment.rs:33` — `TargetDeclaration.service` is a plain
  `ServiceDeclaration`, not an `Option`. Every deployment unit is a service.
- `Idunn/src/deployment.rs:110-141` — a `ServiceDeclaration` requires an
  `executable_artifact`, a transport, and a mandatory `health` declaration;
  `:851` refuses a plan whose service executable is not a declared artifact.
- Admission does not complete until the workload publishes its own signed
  presence and health (`Idunn/docs/deployment-authority.md:84-92`). A zip cannot
  sign presence.
- The only workload drivers are `SystemdTransient` and `HostActuator`
  (`Idunn/src/deployment.rs:442-450`) — both process supervisors. There is no
  oneshot path.
- The only route drivers are `NginxStreamTcp`/`NginxStreamUdp`
  (`Idunn/src/deployment.rs:698-701`), writing stream fragments to
  `/etc/nginx/idunn-stream-routes/`. Idunn has no concept of an HTTP `root`,
  `try_files`, or a web root anywhere in `src/`.
- Idunn has no push transport at all. It pulls: `GitSourceDriver`
  (`Idunn/src/drivers.rs:682,714`) fetches the admitted ref on the host. Even the
  remote Windows actuator fetches its own source
  (`Idunn/docs/host-actuator.md:27-47`). Bytes never travel *from* Idunn.

`ArtifactOutput` (`Idunn/src/deployment.rs:91-101`) can carry an arbitrary blob
with an `expected_sha256`, and `required_adjacent_artifacts` (`:115`) ships
non-executable files — but only alongside a supervised executable. There is no
unit shaped like "a file at a URL".

Meanwhile Yggdrasil already serves static files, twice, through a different and
proven authority (see 0.4). The target invariant should be narrowed to "Idunn
owns *service* deployment to Yggdrasil"; static bytes already have an owner, and
it is not a second deployment authority invented for this migration. See **F1**.

### 0.4 The site is GitHub Pages; only the bytes can live on Yggdrasil

`gamecult.org` is GitHub Pages out of `GameCult/gamecult-site`
(`gamecult-ops/inventory.md:728-738`, `runbooks/gamecult-pages-cutover.md:1-19`),
built by the shared engine `GameCult/GameCult-Quartz` through a reusable workflow
(`gamecult-site/.github/workflows/deploy-quartz.yml`,
`GameCult-Quartz/.github/workflows/quartz-pages.yml:24-72`, ending in
`actions/deploy-pages@v4`). `aetheria.gamecult.org` is the same mechanism out of
`GameCult/AetheriaLore` (`AetheriaLore/site/quartz.config.ts:66`,
`gamecult-ops/inventory.md:801-806`).

A 190 MB zip cannot go in either repo: GitHub blocks files over 100 MB, and
Pages has its own soft limits. So the split is forced and is exactly what the
operator asked for — the *page* is on the site, the *bytes* are on Yggdrasil.

What Yggdrasil already has:

- nginx with real static vhosts: `gamecult-ops/nginx/erycina.org.conf:4`
  (`root /srv/erycina-site/site/current`), `nginx/velvet.erycina.org.conf:4`.
- An immutable-release store pattern:
  `scripts/deploy-erycina-site-yggdrasil.sh:33-50` unpacks into
  `/srv/<site>/site/releases/<UTC-stamp>` and moves `current` atomically.
- A generic TLS publisher: `scripts/publish-static-site-tls-yggdrasil.sh:1-60`,
  parameterised by `SITE_HOST`/`SITE_ROOT`, refusing to run unless DNS already
  resolves to this host (`:40-56`). certbot/Let's Encrypt throughout.
- Transport: `scp ... ygg:` then `ssh ygg 'sudo -n bash /tmp/...'`
  (`runbooks/erycina-org-site-deploy.md:78-81`).

One thing does **not** transfer. The site-release pattern rotates: a `current`
symlink plus `KEEP_RELEASES=5` pruning
(`scripts/deploy-erycina-site-yggdrasil.sh:50-56`). The target requires the
opposite — "A published version stays published"
(`docs/build-delivery-target.md:32`). The artifact store is append-only, with no
`current` symlink and no pruning. Cut 4 says so explicitly, because copying the
erycina script wholesale would silently delete published downloads.

## 1. Authority map (whole migration)

- **Owner of the release record:** a new CultCache store
  `Releases/aetheria.releases.cc` in this repo, one `aetheria.release` document
  per published build. The Aetheria repo owns the facts about Aetheria builds.
- **Inputs:** the built artifact's bytes (size, sha256), the commit it was built
  from, the platform, the build host, the version, and a hand-authored changelog
  entry.
- **Outputs:** the artifact zip; the release document; a derived feed and derived
  changelog pages in the site repo.
- **Derived state, never authored:** the browser-facing JSON feed, the download
  page, and every changelog page. All are projections of the release store,
  regenerated wholesale. Editing them by hand is the failure the target names at
  `docs/build-delivery-target.md:30-31`.
- **Forbidden writers:**
  - The *game* must never read or write the release store. It is not added to
    `AetheriaStores.cs:9-11`, and `ActionGameManager.cs:56` keeps opening exactly
    three stores. A release record is publishing state, not game state.
  - Idunn does not write the artifact, the feed, or the pages (0.3).
  - The site repos never author release facts; their content files for downloads
    and changelog are generated and overwritten.
  - `Build/` stays gitignored (`.gitignore:8`) and never becomes an artifact
    store.
- **Shared paths:** there is exactly one publish primitive. A manual publish, a
  re-publish, and any future automated publish all go through the same
  `release publish` verb; none of them writes the feed or the pages directly.
- **Deletion line:** the vacuous catalog guard at `AetheriaStores.cs:26-31` is
  replaced before Cut 1 adds any build code, and
  `docs/build-delivery-target.md:11-14` is corrected once Cut 1 lands.

**Atomicity, honestly.** The target asks that publishing "publishes the artifact,
the feed entry and the changelog entry together, or it publishes nothing"
(`:28-29`). Three destinations — a remote filesystem, a git-tracked store, and a
second git repo — admit no real two-phase commit. The invariant that actually
protects a visitor is narrower and is the one to enforce: **no feed entry exists
without verified bytes already fetchable at its URL.** Order is artifact → verify
by re-downloading and re-hashing → release document → projection → site push.
A failure before the release document leaves unreferenced bytes, which are inert
and which the next publish of the same version may overwrite only when the hash
matches. A failure after it leaves a pending page, which the next site build
fixes. Neither produces a download link that 404s.

## 2. Cuts

### Cut 1. A player build that is repeatable, identified, and carries its catalog

- **Repo/branch:** Aetheria, `codex/build-delivery` from `6dc18607`. Depends on
  nothing.
- **First:** keep the probe numbers in 0.1 as the baseline — 212 files,
  352.4 MB, 189.8 MB zipped, ~9 min warm. A first landed build that differs
  wildly means the build options diverged from the stock switch.
- **Deletes first:**
  - `Assets/Scripts/ServerShared/AetheriaStores.cs:13-15` (the comment promising
    a guard that does not fire) and the guard body at `:26-31`, replaced rather
    than supplemented.
- **Adds:**
  - `Assets/Editor/AetheriaBuild.cs` — one static entry point for
    `-executeMethod`, taking version and output root from the command line. It
    goes under the existing `Assets/Editor/` (whose only current file is
    `Epiphany/EpiphanyEditorBridge.cs`) and compiles into the existing
    `Assembly-CSharp-Editor`; no new asmdef, no new assembly.
  - A catalog staging step in that entry point.
- **Per-file changes** (against `6dc18607`):
  - `Assets/Scripts/ServerShared/AetheriaStores.cs:16-32`: `Open` refuses a
    `catalogPath` that does not exist, unless `catalogWritable` (the seed case
    already exempted at `:24-25`). Keep the existing global check as a second,
    narrower guard; it is not wrong, it is merely inert for today's type set, and
    it will bite the moment a catalog type is marked `[CultGlobal]`. The new
    check is the one that protects a shipped player.
  - `Assets/Editor/AetheriaBuild.cs` (new): `BuildPlayerOptions` with
    `scenes` read from `EditorBuildSettings.scenes` filtered on `enabled` — the
    same two scenes at `ProjectSettings/EditorBuildSettings.asset:8-13`;
    `target = BuildTarget.StandaloneWindows64`. Sets
    `PlayerSettings.bundleVersion` from the passed version, superseding the
    stale `0.1` at `ProjectSettings/ProjectSettings.asset:152`. After the build:
    copy `GameData/Aetheria.cc`, `GameData/Narrative/**` and
    `GameData/SoundbanksInfo.json` next to the exe; delete
    `Aetheria_BurstDebugInformation_DoNotShip/`; fail the run on a non-empty
    `BuildReport` error list.
- **Authority map:**
  - Owner: `AetheriaBuild` owns what a player build contains.
  - Inputs: `EditorBuildSettings.scenes`, `GameData/`, a version string, an
    output root.
  - Outputs: a complete player directory, and the build report.
  - Derived: `PlayerSettings.bundleVersion` is now set by the build, not authored
    in `ProjectSettings.asset`.
  - Forbidden writers: nothing else sets `bundleVersion`; nothing else decides
    the scene list.
  - Deletion line: the inert-guard comment goes before the entry point is added.
- **Verification:**
  - builds: the entry point produces a player; size within ~10% of 352.4 MB.
  - negative: `git grep -n "Aetheria_BurstDebugInformation"` finds only the
    deletion in `AetheriaBuild.cs`.
  - test: a `Aetheria.Shared.Tests` case pins that `AetheriaStores.Open` throws
    on a nonexistent read-only catalog path, and still opens a writable empty one
    (the seed case at `AetheriaStores.cs:24-25`). This is the regression that 0.2
    proves is live today.
  - operator: launch the staged build by double-click, reach Main Menu, start a
    game, confirm the world is populated. Then rename `GameData/` beside the exe
    and relaunch: it must refuse loudly, not boot to a menu.

### Cut 2. The release record

- **Repo/branch:** Aetheria, same branch. Depends on Cut 1 for the fields it
  records.
- **Adds:**
  - An `aetheria.release` `[CultDocument]` type, declared in the tools assembly,
    not in `ServerShared` — the game must not link it.
  - `Releases/aetheria.releases.cc`, tracked in git. Small: a few KB per release.
- **Shape.** Model the fields on the one existing GameCult type that already
  describes a versioned build artifact,
  `CultLib/src/GameCult.Mesh/CultMeshCdn.cs:130-171`
  (`gamecult.mesh.cdn_artifact_manifest`): `Version` (`:145`), `ContentHash`
  sha256 (`:151`), `SizeBytes` (`:156`), `MimeType` (`:160`), `CreatedAtUtc`
  (`:164`). Note `CultMeshCdnArtifactKinds.Build = "build"` at `CultMeshCdn.cs:34`
  is documented as "Executable build or install/update payload" — this exact case.
  Add what that type lacks and the target requires: `Commit`, `Platform`,
  `BuildHost`, `Url`, and the changelog body. See **F7** on reuse vs a new type.
- **Authority map:**
  - Owner: the release store is the authority on what exists
    (`docs/build-delivery-target.md:30`).
  - Inputs: Cut 3's publish verb only.
  - Derived: everything a browser sees.
  - Forbidden writers: the game (`AetheriaStores.cs:9-11` unchanged,
    `ActionGameManager.cs:56` unchanged); the site repos; any hand edit.
  - Deletion line: none — nothing exists to replace.
- **Verification:**
  - negative: `git grep -n "aetheria.release"` must not match anything under
    `Assets/Scripts/ServerShared/`. Checked against the `aetheria.*` schema names
    already in that tree so it cannot collide.
  - test: reopening the store yields the same documents; a published version is
    refused on re-publish with different bytes.

### Cut 3. One publish primitive

- **Repo/branch:** Aetheria, same branch. Depends on Cuts 1 and 2.
- **Keeps and reuses:** `tools/AetherDb` — the existing headless .NET console over
  Aetheria's CultCache stores, with store-path resolution already written at
  `tools/AetherDb/AetherDb.cs:15-32`. A `release` verb group there adds **zero**
  build targets and reuses the csproj, the `Aetheria.Shared` reference, and the
  path walk. A second console would add a target for nothing. See **F5** on who
  runs it.
- **Adds:** `release build`, `release publish`, `release project` verbs, and
  `CHANGELOG.md` at the repo root in the Keep-a-Changelog shape CultLib just
  adopted (`CultLib-semver/unity/org.gamecult.cultlib/CHANGELOG.md:1`).
- **Per-file changes:**
  - `tools/AetherDb/Program.cs`: new verb dispatch. `release build` shells the
    Unity entry point from Cut 1 and zips the result, excluding nothing further
    (Cut 1 already removed the Burst directory). `release publish` computes
    sha256 and size, reads the `CHANGELOG.md` section for the version, writes the
    `aetheria.release` document, and emits the projection. `release project`
    regenerates the projection alone from the store, so the pages can be rebuilt
    without a build.
  - Follow the deterministic-packaging precedent in
    `CultLib-semver/scripts/build-unity-package.ps1:41-52`, which clears
    intermediates and disables source-revision stamping so a rebuild is
    byte-comparable. The target asks for reproducibility from a commit
    (`docs/build-delivery-target.md:36-37`); say plainly in the doc what Unity
    does *not* guarantee there rather than claim byte-identity that Cut 1 has not
    demonstrated.
- **Authority map:**
  - Owner: `release publish` is the only writer of the release store.
  - Derived: the feed JSON and the markdown pages, regenerated wholesale, never
    merged into.
  - Shared paths: a first publish and a re-publish use the same verb.
  - Forbidden writers: no script writes the feed or pages directly.
- **Verification:**
  - test: `release project` run twice produces byte-identical output.
  - negative: nothing outside `tools/AetherDb` opens `aetheria.releases.cc`.
  - operator: none yet; Cut 5 carries the smoke.

### Cut 4. The artifact host on Yggdrasil

- **Repo/branch:** `gamecult-ops`, its own branch. Depends on Cut 3 producing a
  zip. Independent of Cuts 1-3 landing.
- **Adds:**
  - `nginx/<host>.conf` modelled on `nginx/erycina.org.conf`, keeping the ACME
    location (`:11-14`) and the security headers (`:7-9`), but with
    `root /srv/aetheria-builds`, `autoindex off`, and **no** `try_files`
    fallback and **no** `error_page` rewrite — a missing version must 404.
  - `scripts/deploy-aetheria-build-yggdrasil.sh`. Modelled on
    `scripts/deploy-erycina-site-yggdrasil.sh` but with the rotation removed:
    the release path is `/srv/aetheria-builds/<version>/`, it **refuses** to
    write into a version directory that already exists, there is **no** `current`
    symlink, and there is **no** pruning. Copying `:50-56` unchanged would delete
    published downloads and break
    `docs/build-delivery-target.md:32`.
  - A runbook alongside `runbooks/erycina-org-site-deploy.md`.
- **Reuses unchanged:** `scripts/publish-static-site-tls-yggdrasil.sh` for the
  vhost install and certbot run — it is already parameterised by
  `SITE_HOST`/`SITE_ROOT` (`:1-60`) and already refuses to run before DNS
  resolves to this host (`:40-56`).
- **Hazard to carry into the runbook:** five existing `*.gamecult.org` records in
  `gamecult-ops/inventory.md` still say `A -> 94.130.223.131`
  (`:746, :764, :771, :777, :785`) while Yggdrasil is at `159.195.114.249`
  (`:363, :387`). The new record must use the current address, and the stale rows
  are a follow-up outside this migration.
- **Authority map:**
  - Owner: the release directory tree on Yggdrasil is append-only and root-owned.
  - Inputs: a zip and its sha256, scp'd by the operator.
  - Forbidden writers: nothing overwrites a published version directory; Idunn
    does not touch this tree (0.3); no pruning job exists.
  - Deletion line: the rotation and pruning logic is removed when the script is
    adapted, not left in and disabled.
- **Verification:**
  - operator: `curl -I` the artifact URL returns 200 with the right
    `Content-Length`; TLS valid; the parent directory does not list;
    a nonexistent version returns 404.
  - negative: re-running the deploy script for an existing version exits nonzero
    and changes nothing.
  - budget: ~190 MB per release on a ~2 TB root
    (`gamecult-ops/inventory.md:355-392`). Ten releases is ~1.9 GB. Retention
    owner is the operator; append-only is the default and nothing prunes.

### Cut 5. The download page and the changelog pages

- **Repo/branch:** the chosen site repo (see **F2**), its own branch. Depends on
  Cut 3's projection and Cut 4's URL.
- **Adds:** generated content files under the site's content dir — one downloads
  page, one page per release — plus the feed JSON under `static/`.
- **Mechanism.** Do not write a Quartz emitter plugin. The established precedent
  in these repos is pre-build generation into committed files:
  `gamecult-site/scripts/generate-vn-repo-doc-tree.mjs` and
  `generate-vn-eve-surface.mjs` write JSON into `GameCult/static/...`, invoked by
  the local launcher (`scripts/quartz/quartz.ps1:38-52`) and **not** by CI, so the
  outputs are committed artifacts. Cut 3's `release project` fills that same role.
  The site's existing workflow then publishes with no change at all.
- **Brand.** Follow `gamecult-site/docs/brand-design-language.md` and the config
  it describes, not invention: Montserrat 100-300 for titles, Ubuntu 300 body,
  IBM Plex Mono uppercase and letter-spaced for the structural labels a download
  page is mostly made of (version, date, platform, size, hash). Ground `#07111a`,
  panels `#16212c`, body `#b7c7d9`, headings `#eef5ff`, accent `#ff8a2a`, links
  `#59b7ff`. Reuse the existing component classes rather than adding a page-type
  variant: `.gamecult-repo-facts` / `-fact-label` / `-fact-value`
  (`gamecult-site/site/quartz/styles/custom.scss:953-1008`) is already a
  label/value fact table, and `.gamecult-action`
  (`custom.scss:2246-2360+`) is already the button. Note the brand defines no
  success/warning/error colour (`brand-design-language.md:135-138`); a download
  page needs none.
- **Authority map:**
  - Owner: Cut 3's projector owns every byte of these files.
  - Derived: all of them. A hand edit is overwritten on the next projection and
    that is the intended behaviour.
  - Forbidden writers: no human edits the generated pages; changelog prose is
    authored in the Aetheria repo's `CHANGELOG.md` and flows through Cut 3.
  - Shared paths: the site's existing Pages workflow is untouched.
- **Verification:**
  - builds: the site's Quartz build succeeds with the generated pages present.
  - negative: re-running `release project` leaves the working tree clean.
  - **operator, the real check:** from a machine that has never held the repo —
    open the download page on the public site, click the link, let it download,
    verify the sha256 shown on the page against the file, unzip it, run
    `Aetheria.exe`, start a game, confirm the world is populated. Then confirm
    the changelog page for that version says what actually changed. This is the
    only check that proves the whole path; every other verification above is a
    component test.

## 3. Operator forks

**F1. Does Idunn carry the artifact?** *Most blocking — it decides Cut 4.*
- **A:** Extend Idunn with a static-artifact deployment unit (optional `service`,
  a file workload driver, an HTTP root route driver).
- **B:** Use the existing `gamecult-ops` static release path — scp, a root-owned
  append-only release script, an nginx vhost, certbot — the same authority shape
  that already serves `erycina.org` and `velvet.erycina.org`.
- **Recommended: B.** A is a large change in Rust to the one daemon whose whole
  design premise is that it depends on no managed target and supervises signed,
  health-reporting processes; it would mean making `service` optional
  (`Idunn/src/deployment.rs:33`) and inventing an HTTP-root route driver, to make
  Idunn do the thing it is deliberately not shaped for. B is not a second
  deployment authority — it is the *existing* file-serving authority on that
  host. This does mean narrowing the target invariant at
  `docs/build-delivery-target.md:44` to name service deployment; that edit
  belongs to whoever lands Cut 4.

**F2. Which site owns the download page?**
- **A:** `gamecult.org` (`gamecult-site`) — what the target's invariant names
  (`docs/build-delivery-target.md:46`).
- **B:** `aetheria.gamecult.org` (`AetheriaLore`) — the game's own live site,
  same engine, same brand family.
- **Recommended: B, with A linking to it.** A download for Aetheria belongs on
  Aetheria's site, and per-release changelog pages will accumulate; parking
  dozens of them in the studio site's tree is noise there and natural there. The
  honest counter: `AetheriaLore` is a *lore vault* (`Aetheria/Lore`, `Fiction`,
  `Worldbuilding`) and release notes are not lore, so B needs a new top-level
  section and a masthead entry
  (`AetheriaLore/site/quartz/components/AetheriaMasthead.tsx:19-39` is a
  hardcoded route list). This one is close; the target's wording points at A and
  only the operator can say whether that wording was deliberate.

**F3. Versioning scheme for game builds.**
- **A:** semver, next is `0.2.0`, commit recorded as a separate field.
- **B:** CalVer, `2026.09.17`.
- **C:** commit-derived, `0.1.0+g6dc1860`.
- **Recommended: A.** It reuses the policy work already in flight:
  `CultLib-semver/scripts/check-changelog-semver.mjs` demands exactly one
  major/minor/patch step per release (`:39,:62`), refuses a version with no
  changelog entry (`:79`), and treats `0.y.z` minor as the breaking lane
  (`:98,:139`) — which is exactly right for a game whose saves will break.
  Caveat the operator should know: that script is **untracked** on its branch,
  and the `docs/semver-policy.md` it cites at `:4` does not exist in any CultLib
  worktree. It is package-only and says nothing about builds, so adopting it here
  is a decision, not an inheritance. B tells a player nothing about whether their
  save survives. C puts a commit in the version string where the target already
  wants the commit as its own field (`docs/build-delivery-target.md:26-27`).

**F4. Hand-written or commit-derived changelog?**
- **A:** Hand-written per release in `CHANGELOG.md`, ingested at publish.
- **B:** Derived from commit subjects between tags.
- **Recommended: A.** B produces a build log, not release notes — and GameCult
  already has that experiment running by hand:
  `gamecult-site/GameCult/Projects/VoidBot.md:53-57` and `Ghostlight.md:56-57`
  are dated commit-subject lists, and they read like machine output. A also
  matches what CultLib just adopted for its own packages. The cost is that
  publishing blocks on someone writing prose, which is the correct thing for it
  to block on.

**F5. Agent-run or operator-run publishing?**
- **A:** Fully agent-run, including the Yggdrasil push.
- **B:** Agent builds, hashes, writes the record and the projection, and opens
  the PRs; the operator runs the two remote scripts.
- **Recommended: B.** `gamecult-ops` already draws this exact line, in a comment
  worth quoting: "putting a name on the public internet is a separate decision
  from having bytes on disk"
  (`scripts/deploy-erycina-site-yggdrasil.sh:9-11`). A published version is
  permanent by the target's own rule, so the irreversible step is the one a human
  should take. Everything expensive and mechanical stays automated.

**F6. Zip or installer?**
- **A:** Zip. **B:** An installer (NSIS/Inno/MSI).
- **Recommended: A.** Measured at 189.8 MB **(probe)**. No installer toolchain
  exists anywhere in GameCult, and B drags in code signing, an uninstaller, and a
  new build dependency for no player benefit while launchers and auto-update are
  already out of scope (`docs/build-delivery-target.md:55`). Revisit when there
  is a reason, not before.

**F7. Reuse `gamecult.mesh.cdn_artifact_manifest` or declare `aetheria.release`?**
- **A:** Reuse the CultLib type (`CultLib/src/GameCult.Mesh/CultMeshCdn.cs:130`),
  which already has version, sha256, size, mime, created-at, and a `Build` kind.
- **B:** A new `aetheria.release` type that borrows those field names.
- **Recommended: B, citing A.** The CultLib type is CDN-shaped: its `Chunks` list
  (`:168`) is the point of it, and content-addressed chunking
  (`CultMeshCdn.cs:96-120`) is machinery this migration does not use and should
  not fake. A release also needs `Commit`, `BuildHost`, and changelog prose,
  which do not belong bolted onto a CDN manifest consumed elsewhere. Borrow the
  field shape, not the type. If a CultMesh CDN path ever carries these builds, A
  becomes the transport and B stays the record.

## 4. Subtraction and build budget

This migration is net additive and cannot honestly be made otherwise: it buys a
capability that does not exist today — nobody outside Starfire can play the game.
What it must not do is buy it twice.

| Cut | Removes | Adds | Targets |
|---|---|---|---|
| 1 | inert guard + stale comment, `AetheriaStores.cs:13-15,26-31`; the shipped Burst debug dir | one Editor file under the existing `Assets/Editor/` | +0 assemblies (no asmdef; joins `Assembly-CSharp-Editor`) |
| 2 | — | one document type, one tracked `.cc` | +0 |
| 3 | — | verbs inside the existing `tools/AetherDb`; one root `CHANGELOG.md` | **+0 executable targets** |
| 4 | the rotation and pruning logic while adapting the erycina script | one nginx conf, one shell script, one runbook | +0 (no compilation in `gamecult-ops`) |
| 5 | — | generated markdown/JSON in a site repo | +0 (no workflow change) |

Explicit budget statements:

- **No new executable target anywhere.** Cut 3 reuses `tools/AetherDb` precisely
  so the repo does not gain a second console. If that reuse is rejected, the
  budget changes and the fork should be raised.
- **No new Unity assembly**, no new package dependency, no Addressables
  dependency — `docs/addressables-cut.md` has not landed and this migration does
  not wait on it. The build probed in 0.1 is the pre-Addressables `Resources`
  path, and it works.
- **Build host is Starfire, Windows, Unity 6000.3.24f1**
  (`ProjectSettings/ProjectVersion.txt:1`). The serving host is Debian 13. The
  artifact is Windows-only and produced only on Windows; nothing about Cut 4
  proves anything about the player, and nothing about Cut 1 proves anything about
  the server.
- **Disk.** ~190 MB per release on Yggdrasil, append-only, no pruning. The
  operator owns retention.
- **A fresh clone is not required per release.** The checkout is 1.78 GiB of
  `.git` plus a 2.6 GB `Library/`; the ~9 min build in 0.1 was warm, and a cold
  `Library/` import is far worse. Publishing builds from the working Starfire
  checkout is the assumption; if that ever moves to a clean builder, the import
  cost has to be budgeted then, not assumed away now.

## 5. Open

- Every fork in section 3 is unruled.
- `docs/build-delivery-target.md:11-14` and `:44` both need correcting once F1
  and Cut 1 are ruled; the target currently says builds happen by hand into
  `Build/` (they do not — nothing produces a player) and that Idunn owns this
  deployment (it structurally cannot).
- Follow-up outside this migration: the five stale `A -> 94.130.223.131` DNS rows
  in `gamecult-ops/inventory.md:746,764,771,777,785`.
- Follow-up outside this migration: `CultLib-semver/scripts/check-changelog-semver.mjs`
  is untracked and cites a `docs/semver-policy.md` that does not exist. F3 leans
  on that work; someone should land it there.
