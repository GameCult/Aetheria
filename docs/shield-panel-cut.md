# Shield Panel: Hard-Light Interceptor Cut Map

Date: 2026-09-18 (revised; supersedes the flat-panel map committed at `1fdf8151`)

Status: Imagination pass, cut map. Nothing here has landed. Repo anchors are against
`codex/item-provenance` HEAD `482229d1` — the Addressables cuts (`67cbf34b`, `482229d1`)
landed after the first draft, so every `file:line` below was re-checked against the current
tree. The six incoming sources are still not in the repo; they are anchored as
`files/<name>:<line>` against the operator's drop at `<scratch>/files/`.

Claims marked **(probe)** were measured in Unity 6000.3.24f1, Direct3D12, `srp=builtin`,
batchmode with graphics, in a throwaway project holding only the six files and a ported
shader. Nothing was built in the Aetheria tree and no Unity session was opened against it.
The probe project is scratch and goes with the session; §1 and §2 record what it proved so
nobody has to re-run it.

## 0. Operator rulings, 2026-09-18

These close four of the previous pass's forks and change the shape of the thing. The old
design — one permanent flat disc standing in front of the hull — is history; see §9.

**R1 (was Q2) — alongside, not instead.** "It's a different way of doing shields." The panel
effect runs next to `FieldDriver`'s absorb visual, not in place of it. Both subscribe to the
same `AbsorbEvent` stream; neither owns the other. The previous recommendation ("A for the
first landing, then B as a deliberate deletion") is withdrawn — B is not coming.

**R2 — the shield is an interceptor.** "It's a shield that intercepts hits by spawning those
hard-light panels. I'd set the normal of the panels to the surface of the collision
ellipsoid." So a panel is **short-lived and per-interception**: it does not exist until
something is intercepted, it is posed at the intercept point with its normal along the
ellipsoid surface normal there, and it dies. Not a permanent panel. Not a wrapped shell. Its
**size comes from the shield item, not the hull** — the hull decides where the panel appears,
the item decides how big it is.

**R3 — every shield effect is a drop-in behavioural replacement.** A shield presentation takes
absorb events, presents them, and decides nothing. The contract is behavioural: coverage is
expressed **through the envelope**, not through literal geometry. A presentation is allowed to
be a wrapped shell, a flat plate, or a swarm of spawned patches, as long as it answers the
same events over the same envelope.

**R4 — the ellipsoid envelope is standardised and authored on the hull prefab.** "The field
shield already has the ellipsoid, we'd just be standardizing that. Ship prefabs would need to
define their ellipsoid envelope."

**R5 (was Q3) — fix Penrose in this cut.** Operator: "definitely fix penrose". D18
(`Penrose` ignores `panelRadius`) is a Cut 1 defect, not a follow-up: R2 makes panel size the
item's to set, so a tiling that cannot be sized cannot ship. Hex remains the fallback while
the fix is verified, and D19 (TriHex yields no dual edges) and D7 (Voronoi is 15 s at r10)
stay recorded, not fixed.

**R6 (was Q2) — the envelope is derived from the existing transform**, not authored as
centre and radii. Operator: "derived; the simulation doesn't even use this collision, the
hits we end up showing will be 'fake'". Two consequences worth holding on to:
- The envelope has no second copy to drift from the `Shield` collider, which is the whole
  point of collapsing the two implicit ellipsoids.
- **Panel placement is cosmetic.** The simulation does not resolve hits against this
  geometry; fire control rolls decide what happens (`docs/three-gates-scope.md`). The
  intercept point is where the presentation says the shot arrived, and nothing downstream
  may read it back as truth.

Q1 (does the first landing have to fracture) is **still open** and still first; see §8.

> **Cut 1 landed 2026-09-18** (`a935b965` sources+port+defects, `ff8b2ff7` probe and its
> mutation harness). Batchmode: 0 C#, shader and compute errors; only D15's uninitialised-
> variable warning survives. D1, D3, D4, D6, D12, D18 fixed; D2/D5 collapsed the draw to one
> call; D7 and D19 recorded; D9/D10 to Cut 5, D17/D20 to Cut 3. The fix for D18 retired
> `penroseIterations` and `subdivisions`. A repo-specific trap the map missed:
> `UI/ContextMenu.cs` shadows `UnityEngine.ContextMenu`, so the attribute needs qualifying.
>
> **Open, found in Cut 1's own numbers (D21):** Penrose scales with radius now, but not with
> `cellSize` the way Hex does — at r1.5/cellSize 0.35 it yields 460 cells where Hex yields 19,
> and 8050 at r10 against Hex's 979. Iteration count is quantised (each substitution divides
> edge length by phi), so a panel can only land near the requested density, and it currently
> lands an order of magnitude denser. Cut 3 sizes panels from the item (R2) and pools them
> (§2.4), so settle this before pooling: either the count is wrong, or `cellSize` means
> something different for a quasiperiodic tiling and the item's knob must say which.

> **Cut 2 landed 2026-09-18** (`a454a5a7`, fallback fix `7848a8a8`). One owner,
> `Gameplay/ShieldEnvelope.cs`; the hand-rolled projections in `FieldDriver` and
> `ShieldManager` are gone. Radii are measured from the carrier's collider mesh, not assumed:
> the map's `lossyScale * 0.5` guess was wrong, and Shield.prefab's icosphere collider is not
> even isotropic. `SurfaceNormal` scales the gradient by the inverse of `lossyScale` (a normal
> is a covector); the first implementation used `TransformDirection` and a finite-difference
> probe caught it 5-20% out. It diverges from `normalize(p)` by dot 0.674 on the Longinus
> flank, which is why the panel needs it and the two old consumers did not.
>
> Carriers without the component behave exactly as before Cut 2, warning once per object.
> Self sent the first attempt back: its fallback degraded shield hits to a fixed direction
> until the operator edited prefabs, which is a regression on working behaviour.
>
> **Open (D22):** `FieldDriver`'s two branches are not interchangeable in general —
> `ProjectToSurface` returns a world point, the legacy fallback stays local because that is
> what the shader eats. They agree only for a carrier at the origin with identity rotation,
> which is the only `FieldDriver` carrier in the tree. Settle before a second one exists.

> **Cut 3 landed 2026-09-18** (`0ef88419`, `0e2fe6dc`, `5ae5c518`). `ShieldInterceptor`
> poses a pooled panel per absorb event on the envelope, reusing `Prototype`. D17/D20 fixed
> (a panel absorbs the hit that spawned it, and the gate stays open after), D21 fixed
> (`Round` not `Ceil+1` on the substitution count): Penrose is now 2.1-4.7x Hex's cell count
> at the same radius and cell size, down from ~24x, and that residual is the quantisation
> floor of "nearest edge length", not a knob. Reuse strikes a live panel within
> `PanelRadius * ReuseFraction` without resetting it, so temper erosion survives; at the cap
> (12) the oldest panel is recycled in place.
>
> Found beyond the map: `OnEnable`/`OnDisable` would have rebuilt the tiling and released
> every buffer on each pooled activation, which would have made pooling cost more than
> rebuilding and quietly voided §2.4's whole argument.
>
> Two yields with a background run still going cost a round trip each; the wait rule now
> names the mechanism (`~/.claude/skills/eureka/references/briefs.md`).

> **Ruling R7 (operator, 2026-09-18): a panel's rim is held, not free.** "held is probably
> nicer even if free is more accurate". The emitters projecting the panel hold its edge, so the
> rim stops being a tension source (`ShieldSim.compute`'s `openW` ghost term) and the only
> spall left comes from the shot itself through the plate. Cracks radiate from the impact and
> shards leave where the hit landed, and panel size stops being a fragility multiplier.
> **A broken cell's faces stay free**: that reflection is the dicing cascade and it survives.
>
> Operator play evidence, 2026-09-18, on the rigged scene: a panel spawns, is reused, and
> spiderwebs, but only shatters when hits are spammed near the rim — which is the free edge
> reflecting compression back as tension exactly as designed, and confirms the wave, the
> reflection and the temper erosion all work. The interior spall source (the through-thickness
> echo) is too weak or mistimed to compete. That, D9 (velocity unclamped, so hard hits saturate
> in compression) and D10 (fracture evaluated once per frame while the wave integrates several
> times) are Cut 5's targets.

## 1. What the repo actually has today

The re-probe found the thing the first pass missed: **the ellipsoid envelope already exists
twice, in two unconnected places, and neither is typed.**

**Authority A — `FieldDriver`'s transform.** The ellipsoid is implicit in the field object's
scale. `Assets/Scripts/Gameplay/FieldDriver.cs:125` projects a hit onto it with
`normalize(transform.InverseTransformPoint(position)) * transform.localScale`; `:201` feeds
`_InverseScale` to the shader; `:297-299` scales the tendril vectors by it. The only carrier
in the whole tree is the `cubesphere Variant` prefab instance in
`Assets/Scenes/FieldShieldTest.unity`, whose localScale is **(4, 3, 12)** at
`Assets/Scenes/FieldShieldTest.unity:948-959`. `FieldDriver` is on **no ship prefab at all**
— its script guid appears in exactly one asset file, that scene, at `:1042`.

> Correction to the previous pass: the old map said this object was scaled `(10, 10, 20)`
> (old §"Cut 3"). It is `(4, 3, 12)`. Every geometry number in the old Cut 3 was wrong.

**Authority B — the `Shield` prefab instance on each ship.** `Assets/Content/Prefabs/Shield.prefab`
has base localScale `(100,100,100)` at `:212` and a convex `MeshCollider` at `:218-231`. Each
ship overrides it non-uniformly, and *that override is the ship's envelope today*:

| Ship | localScale override | localPosition | effective radii |
| --- | --- | --- | --- |
| Longinus (`Assets/Content/Prefabs/Ships/Longinus.prefab:1832-1843`, pos `:1847-1858`) | (395.35, 235.47, 990.38) | (0, 0.73, 1.32) | ≈ (3.95, 2.35, 9.90) |
| Djinni (`Assets/Content/Prefabs/Ships/Djinni.prefab:7378-7389`) | (857.93, 424.00, 1036.26) | (0, 1.75, -1.06) | ≈ (8.58, 4.24, 10.36), pitched 10° |

`ShieldManager.ShowHit` (`Assets/Scripts/Gameplay/ShieldManager.cs:73-78`) already does the
**same normalise-and-project** as `FieldDriver.cs:125`, against this transform instead:
`normalize(shield.transform.InverseTransformPoint(point))`. Two implementations of one idea,
neither aware of the other. The test scene's (4,3,12) is visibly an eyeball of Longinus's
(3.95, 2.35, 9.90) — which is the tell that this was always one concept.

So the standardisation R4 asks for is **not a new invention. It is a collapse of two existing
duplicate authorities**, and the panel is its third consumer rather than its reason.

**No data-side size exists.** `HullData` (`Assets/Scripts/ServerShared/ItemData.cs:493-533`)
carries `Hardpoints`, `Prefab`, `HullType`, `GridOffset`, `Armor`, `Drag`, `CanTow`, `Shape`
— no radius, no extents. Nothing in `Assets/Scripts` ever assigns a shield or field object's
`localScale`; `ZoneRenderer.cs:295` instantiates the hull prefab and applies no scale at all.
The prefab is the only place the envelope can live today. **For the record, and as a
follow-up rather than a fork:** this belongs in the catalog next to `HullData`, for the same
reason hardpoints do — `HardpointData.Transform`
(`Assets/Scripts/ServerShared/ItemData.cs:541`) is a *GameObject name string* matched against
prefab child names at runtime (`EntityInstance.cs:242,257,268`, `ShipInstance.cs:69-70`),
which the operator has called unfortunate. The envelope would inherit the same unfortunateness
by living on the prefab. It should move when hardpoints move, in that cut, not this one.

**The real absorb publisher does not exist, but its hook does.** `PublishAbsorb` has exactly
one caller and it is test-only: `Assets/Scripts/UI/FieldTester.cs:76`, from a
`ClickableCollider.OnClick`. The real damage path is entirely separate — `Entity.IncomingHit`
/ `HullDamage` (`Assets/Scripts/ServerShared/Entity.cs:126,131`), published from
`EntityInstance.cs:312,333`. But the real *shield-hit visual* path already converges on one
method: `ShieldManager.ShowHit`, called from `ShieldManager.cs:65` (collision) and six
weapons — `ConstantLaser.cs:83`, `ConstantLightning.cs:62`, `GuidedProjectile.cs:162`,
`HitscanEffect.cs:40`, `Laser.cs:49`, `Lightning.cs:44`. **That is where a real
`PublishAbsorb` belongs**, and it is a later cut, not this one. Naming it here so nobody
invents a second bridge.

**The pool already exists.** `Assets/Scripts/Prototype.cs` is the repo's pooling helper:
`Instantiate<T>()` at `:41`, `ReturnToPool()` at `:80`, an `OnReturnToPool` event at `:14`.
`ShieldManager.ShowHit` uses it (`ShieldManager.cs:75`) for exactly the short-lived
per-hit visual this design needs. **Do not write a pool.** Use this one.

**The materialisation wavefront exists and is dead.** `CellStatic.spawnDelay`
(`files/ShieldCommon.hlsl:20`) is baked per cell at build time from a `wavefrontOrigin` and
`wavefrontSpeed` (`files/TilingBuilder.cs:225,230`), and `KUpdate` reads it as
`s.growth = saturate((_Time - _PanelSpawnTime - c.spawnDelay) / _GrowDuration)`
(`files/ShieldSim.compute:346`). But `files/ShieldPanel.cs:360` hardcodes
`sim.SetFloat("_PanelSpawnTime", 0f)`, so the wavefront runs once at sim time zero and never
again. For the interceptor it becomes the panel's arm time — **this is the mechanism R2
already wanted and it needs one line changed, not a new system.**

The bake is also, by luck, already correct: an interceptor panel is *centred on* its intercept
point, and the tiling is built with `wavefrontOrigin: Vector2.zero`
(`files/ShieldPanel.cs:136`), which is the panel centre. So **one tiling serves every panel**
and the growth wavefront still radiates from the strike. Nothing about the bake needs to
become per-interception.

## 2. Probe record

All measured, not reasoned.

### 2.1 Port and drawing (carried forward from the first pass, unchanged)

1. **The shader ports to built-in with three substitutions**: drop
   `"RenderPipeline"="UniversalPipeline"` from the SubShader tags, replace the URP `Core.hlsl`
   include with `UnityCG.cginc`, rename three `TransformWorldToHClip(` to
   `UnityWorldToClipPos(`. Result `shader supported=True, messages=0`. No other URP symbol is
   used. **(probe)**
2. **Indirect drawing works on built-in.** `Graphics.RenderPrimitivesIndirect` with
   `RenderParams`, a `MaterialPropertyBlock` carrying `GraphicsBuffer`s, and an append-buffer
   counter copied in by `GraphicsBuffer.CopyCount` rendered the panel. **(probe)**
3. **`RenderParams` has no `shaderPass`** in 6000.3.24f1. `files/ShieldPanel.cs:410,415,420`
   do not compile. **(probe)**
4. **A multi-pass material renders every pass from one call**, and pass selection is not
   available at this API. **(probe)**
5. **One args buffer sized to the widest pass is equivalent** — pixel-identical output to the
   three-call version (`litPixels=43929` both ways). **(probe)**
6. **`centroid` is reserved in the compute compiler.** `CellStatic.centroid`
   (`files/ShieldCommon.hlsl:18`) must become `center`. **(probe)**
7. One surviving shader warning: `Warning 172: potentially uninitialized variable (BuildLine)`,
   Conduit vertex program. Warning only. **(probe)**
8. **The supplied tuning never fractures anything.** Hex, `panelRadius 5`, `cellSize 0.35`,
   253 cells, head-on Radial hit, 60 frames: `broken 0/253` at every energy from 3.5 to 3500.
   Peak tension is non-monotonic and collapses above ~60. Causes are D9/D10. **(probe)**

### 2.2 Tiling build cost — new, and it settles prebuild-vs-build-on-demand

`Tilings.Generate` + `TilingBuilder.Build`, `cellSize 0.35`, single-threaded, warm JIT. **(probe)**

| kind | radius | cells | edges | gen ms | build ms | **total ms** |
| --- | --- | --- | --- | --- | --- | --- |
| Hex | 3 | 85 | 444 | 0.07 | 0.84 | **0.91** |
| Hex | 5 | 253 | 1404 | 0.20 | 2.10 | **2.30** |
| Hex | 10 | 979 | 5640 | 0.72 | 20.26 | **20.98** |
| PenroseP3+merge | 1.5 / 3 / 5 / 10 | 460 (all) | 1750 | 1.7–5.8 | 1.9–4.3 | **3.6–10.1** |
| PenroseP3 | 1.5 / 3 / 5 / 10 | 890 (all) | 2610 | 0.3–1.7 | 2.8–3.6 | **3.1–5.3** |
| TriHex | 3 | 211 | **0** | 0.30 | 1.44 | **1.74** |
| TriHex | 5 | 577 | **0** | 0.84 | 8.57 | **9.41** |
| TriHex | 10 | 2275 | **0** | 3.48 | 105.84 | **109.32** |
| Voronoi | 3 | 251 | 1390 | 131.31 | 2.58 | **133.89** |
| Voronoi | 5 | 733 | 4200 | 1014.35 | 11.65 | **1026.01** |
| Voronoi | 10 | 2940 | 17246 | 14941.02 | 85.64 | **15026.67** |

Three findings fall straight out of this table and are written up as D17–D19:

- **Penrose ignores `panelRadius` entirely** — identical cell count at radius 1.5 and radius 10.
- **TriHex produces zero dual edges at every radius.** No adjacency means no wave. It is not
  "probably fine", it is inert.
- **Voronoi is 15 seconds at radius 10.** D7 quantified. It is not shippable at any radius
  where you would want it.

Build cost is not the argument either way on its own — 0.91 ms for a small Hex patch is about
5% of a 60 Hz frame, which is survivable but spiky. §2.4 is what actually settles it.

### 2.3 Per-frame cost of N live panels

N panels each stepped once per frame. `cpuIssueMs` is dispatch issuance only; `syncedMs`
forces a GPU drain each frame via a small readback, so it is an upper bound that a real frame
does not pay. Hex, `cellSize 0.35`. **(probe)**

| cells/panel | N | cpu issue ms/frame | synced ms/frame | synced ms **per panel** |
| --- | --- | --- | --- | --- |
| 85 | 1 | 0.101 | 0.756 | 0.756 |
| 85 | 4 | 0.584 | 1.752 | 0.438 |
| 85 | 16 | 2.139 | 6.146 | 0.384 |
| 85 | 32 | 5.262 | 10.120 | 0.316 |
| 253 | 1 | 0.106 | 0.509 | 0.509 |
| 253 | 4 | 0.451 | 1.449 | 0.362 |
| 253 | 16 | 1.958 | 4.716 | 0.295 |
| 253 | 32 | 4.438 | 11.605 | 0.363 |

**The load-bearing result: per-frame cost is flat in cell count and linear in panel count.**
85 cells and 253 cells cost the same per panel. That is because a panel's frame is ~17 compute
dispatches, one `SetData`, one `CopyCount` and one indirect draw *regardless of how many cells
it holds* — the cell count rides inside the dispatches, where it is nearly free at these sizes.

So the budget is a **panel-count budget, not a cell budget**, and the constant is roughly
**0.12 ms of CPU issuance per live panel** at either size. Sixteen live panels is ~2.0 ms of
CPU every frame, before the GPU does anything — about 12% of a 60 Hz frame for one ship's
shield. That is the number the pool cap has to respect.

### 2.4 Recycle vs rebuild — this settles the pool

Re-arming an existing panel (`ResetSim`, two dispatches) against building one from scratch
(`Rebuild`: tiling + 12 `GraphicsBuffer`s + a `Material`). Hex, 20 iterations each. **(probe)**

| cells | Rebuild ms | ResetSim ms | ratio |
| --- | --- | --- | --- |
| 85 | 1.32 | 0.011 | **123×** |
| 253 | 3.33 | 0.008 | **428×** |
| 979 | 20.91 | 0.008 | **2686×** |

Recycling is **free and flat** — 8 microseconds, independent of cell count, because it is two
GPU dispatches and nothing else. Building is 1.3–21 ms and scales with cells.

**Decision, from the number and not from taste: prebuild.** One tiling per shield item, built
once at load; a preallocated pool of armed `ShieldPanel` instances; interception costs a
`ResetSim` and a transform write. Building a tiling at interception time would drop a 1–21 ms
spike into the exact frame the player is being shot in, which is the worst possible frame to
spend it, and buys nothing — every panel of one shield item is the *same tiling*.

## 3. Target

A shield item's presentation is an interceptor. On each absorbed hit it spawns a hard-light
panel at the intercept point on the ship's ellipsoid envelope, normal along the envelope's
surface normal there, sized by the item. The panel materialises outward from the strike, takes
that hit and any subsequent hit that lands inside it, and dies.

Invariants:

- **One owner of the envelope.** `ShieldEnvelope` is the only thing that answers "where is the
  surface, and which way does it face". `FieldDriver`, `ShieldManager` and the panel presenter
  all read it and none of them recompute it.
- **One owner of the hit.** `ShieldPanel.Hit` is the only entry into a panel's simulation, and
  its presenter is the only caller.
- **One event, one strike.** An `AbsorbEvent` produces exactly one strike — on a new panel or
  on a living one, never both, never neither.
- **Presentation owns only presentation.** The presenter converts an event into a pose and an
  energy. It decides nothing about the rule. (`CapabilityPresenters.cs:8`, and the fork-L
  contract at `CapabilityEvents.cs:9-20`.)
- **Presentations do not see each other.** `FieldDriver` and the panel presenter both subscribe
  and neither reads the other (R1). The binder already supports this without change:
  `CapabilityPresentationBinder.cs:32-36` subscribes every `IAbsorbPresenter` it finds.
- **No second render pipeline, no `Unity.Mathematics`.** Unchanged from the first pass;
  `ProjectSettings/GraphicsSettings.asset:41` is still `m_CustomRenderPipeline: {fileID: 0}`.
- **No allocation at interception.** The pool is preallocated and the tiling is prebuilt. An
  interception costs a `ResetSim` and a transform write (§2.4).

## Cut 1. Land the sources, compiling, on built-in

Unchanged in substance from the committed pass, minus the parts R2 made pointless. Depends on
nothing; touches no file the Addressables cuts moved (nothing here goes near
`Assets/Content` or `Assets/Resources`).

**Location.** `Assets/Shaders/Compute/ShieldField/`, matching
`Assets/Shaders/Compute/Lightning/` and `Slime/`, which each hold their `.compute`,
`.shader`, `.cginc` and driver `.cs` together. Colocation is load-bearing:
`#include "ShieldCommon.hlsl"` resolves relative to the including file **(probe)**. No asmdef
— the files land in `Assembly-CSharp` with everything else in `Assets/Shaders/Compute/*.cs`.
The `ShieldField` namespace collides with nothing.

**Deletes first:**

- `files/ShieldPanel.cs:450-487` — `ShieldPanelTester`. The standalone click harness the
  operator ruled against, and it cannot run anyway: `ProjectSettings/ProjectSettings.asset:797`
  is `activeInputHandler: 1` (new Input System only), so `Input.GetMouseButtonDown` throws.
- `files/ShieldPanel.cs:90` — `argsOutline`, `argsConduit` and their `MakeArgs` calls at
  `:172-173`; the `drawFill`/`drawOutline`/`drawConduits` bools at `:68-70` and their branches
  at `:408-422`. Replaced by one args buffer and one draw (§2.1.5).
- `files/ShieldPanel.cs:9` — `[ExecuteAlways]`. With it, `Update` runs the whole simulation and
  every dispatch in every open scene view, and `OnEnable` rebuilds the tiling on every domain
  reload. Keep `Rebuild` on the context menu.

**Per-file changes:**

- `ShieldCommon.hlsl:18` — `float2 centroid;` → `center`. Every `.centroid` in
  `ShieldSim.compute` and `ShieldPanel.shader` follows. Required to compile (§2.1.6).
- `ShieldCommon.hlsl:31` — add `float pad2;`, taking `CellStatic` to 16 floats.
- `TilingBuilder.cs:27` — add `public float pad2;`, initialise at `:242`. Today the struct is
  15 floats (60 bytes) while `Stride` at `:28` claims `4 * 16`, and `SetData` throws on the
  mismatch **(probe)**. Fix both sides or neither.
- `TilingBuilder.cs:214,221` — rename the inner `dist` to `nbrDist`. CS0136 against the `dist`
  at `:225`; the file does not compile as delivered **(probe)**.
- `ShieldPanel.shader:27` — drop the URP tag, add `"IgnoreProjector"="True"`.
- `ShieldPanel.shader:33` — `#include "UnityCG.cginc"`.
- `ShieldPanel.shader:162,163,244` — `TransformWorldToHClip(` → `UnityWorldToClipPos(`.
- `ShieldPanel.cs:171-173` — one `argsFill = MakeArgs((uint)(MaxV * 2 * 6));`.
- `ShieldPanel.cs:369-371` — one `CopyCount(bVisible, argsFill, sizeof(uint))`.
- `ShieldPanel.cs:408-422` — one `RenderPrimitivesIndirect`, no `rp.shaderPass`.
- `ShieldPanel.cs:296-298` — hoist the per-frame `new uint[] { 0u, cap }` to a cached field.
- Defect fixes D3–D8 from §7, each named in the commit.

**Authority map:**

- Owner: `ShieldPanel` owns one panel's simulation and its GPU buffers. `TilingBuilder` owns
  adjacency, areas and ghost weights. `Tilings` owns geometry generation.
- Inputs: authored fields; `Hit(worldPos, worldDir, energy, pattern, radius)` from outside.
- Outputs: one indirect draw's worth of pixels; GPU state readable for verification.
- Derived: `worldBounds`, `groups`, `echoSlot`, `simTime`.
- Forbidden writers: nothing outside `ShieldPanel` touches its buffers. `ShieldPanelTester` is
  deleted rather than kept as a second hit source.
- Deletion line: `ShieldPanelTester`, two args buffers, three draw bools, the `shaderPass`
  writes, and `[ExecuteAlways]` are gone before Cut 2 is written.

**Verification:** batchmode compile of the Aetheria project, zero errors.
`ShaderUtil.GetShaderMessages` / `GetComputeShaderMessages` report zero errors (the §2.1.7
warning may remain). Negative:
`rg "UniversalPipeline|TransformWorldToHClip" Assets/Shaders/Compute/ShieldField` is empty;
`rg "shaderPass" Assets` is empty. No operator look yet.

## Cut 2. `ShieldEnvelope` — collapse the two implicit ellipsoids into one

This is the R4 cut, and it is a **teardown, not an addition**. Two authorities (§1 A and B)
become one reader, and both existing consumers are converted to it before the panel exists.
Depends on nothing; can land in parallel with Cut 1.

**Adds:** `Assets/Scripts/Gameplay/ShieldEnvelope.cs`, one component.

**Shape, and why this shape.** The component is a typed **reader of its own transform**, not a
new store of numbers:

```csharp
public class ShieldEnvelope : MonoBehaviour
{
    // Radii and centre come from this transform. They are not authored twice.
    public Vector3 Center => transform.position;
    public Vector3 Radii  => transform.lossyScale * 0.5f;   // see note on the 100x base

    public Vector3 ProjectToSurface(Vector3 worldPoint);    // local, normalise, scale, back
    public Vector3 SurfaceNormal(Vector3 worldSurfacePoint); // gradient, see below
}
```

The operator asked for "a `ShieldEnvelope` component with centre + three radii, **or whatever
the Body argues for**". The Body argues for **derived, not authored**, and the reason is
ownership: the two ships already have their envelope authored, as the `Shield` prefab
instance's transform (`Longinus.prefab:1832-1858`, `Djinni.prefab:7378-7389`), and the convex
`MeshCollider` on that same object (`Shield.prefab:218-231`) is *already* driven by it.
Authoring centre and radii as separate serialized fields next to a transform that also decides
the collider would create a second truth about the same surface and a silent way for them to
disagree — the split this map exists to avoid. One authority, three readers.

The cost of that choice, stated plainly: the envelope is only as expressive as a transform, so
it is an ellipsoid with a rotation and no more. That is exactly what R2 asked for, and if a
hull ever needs a non-ellipsoid envelope this component is the place that changes.

`Radii` must account for `Shield.prefab`'s `(100,100,100)` base (`Shield.prefab:212`) and for
whether the mesh at `Shield.prefab:218-231` is a unit-diameter or unit-radius sphere. **Hands
must read that mesh's bounds and derive the factor rather than assuming `0.5`** — get it wrong
and every panel is posed at twice or half the right distance, which looks like a bug in the
panel and is not.

**`SurfaceNormal` is not `normalize(p)`.** For an ellipsoid with radii `(a,b,c)` centred at
the origin, the outward normal at a surface point `p` is
`normalize(p.x/a², p.y/b², p.z/c²)`, then back to world through the transform's rotation.
`normalize(p)` is only correct for a sphere, and Longinus's envelope is 4:1 in aspect, so the
error is large and visible — panels would visibly tilt wrong along the flanks. Both existing
consumers get away with `normalize` today because they use it to pick a *direction on the
surface*, not a *normal to it*. The panel needs the normal (R2) and this is where the
distinction is paid for.

**Deletes / demotions — the part the model will want to skip:**

- `FieldDriver.cs:125` stops computing its own projection. It reads
  `envelope.ProjectToSurface(position)`. **`FieldDriver.transform.localScale` is no longer an
  owner of the envelope; it is display geometry for the field mesh only.**
- `FieldDriver.cs:201` and `:297-299` read `envelope.Radii` rather than
  `transform.localScale`. If a `ShieldEnvelope` is absent, `FieldDriver` must log once and
  fall back to its own transform — explicitly a compatibility path, and it delegates rather
  than keeping its own opinion.
- `ShieldManager.cs:76` stops computing `normalize(InverseTransformPoint(point))` and reads
  the envelope. `ShieldAnimation.Direction` keeps its existing meaning.

**Scene/prefab changes:** add `ShieldEnvelope` to the `Shield` object on `Longinus.prefab` and
`Djinni.prefab`, and to the `cubesphere Variant` instance in `FieldShieldTest.unity`. No
transform value changes — that is the point; the numbers already there become the typed
authority instead of being re-entered somewhere else.

**Authority map:**

- Owner: `ShieldEnvelope` owns the answer to "where is the surface and which way does it
  face". The transform it sits on owns the numbers.
- Inputs: its own transform. Nothing else.
- Outputs: `Center`, `Radii`, `ProjectToSurface`, `SurfaceNormal`.
- Derived state: all of it. The component stores nothing.
- Forbidden writers: nothing writes a `ShieldEnvelope`. After this cut, **no other code may
  compute a projection onto the shield surface** — `FieldDriver.cs:125` and
  `ShieldManager.cs:76` are the two that must stop, and the negative check below pins it.
- Shared paths: collision hits (`ShieldManager.cs:65`), the six weapon call sites, the test
  scene's clicks, and the panel presenter all resolve the surface through this one method.
- Deletion line: the two hand-rolled projections go before the panel presenter is written.

**Verification:**

- test: `ShieldEnvelope` is pure math on a transform, so this one *can* be unit-tested —
  but `Tests.asmdef` cannot reference `Assembly-CSharp` (see Q7), so it is a batchmode probe:
  for each of Longinus's and Djinni's envelopes, assert `ProjectToSurface` of a point outside
  lands on the ellipsoid (`Σ(pᵢ/rᵢ)² == 1` within epsilon) and that `SurfaceNormal` at that
  point is within 1e-3 of the finite-difference gradient of the implicit surface. **The
  sphere-vs-ellipsoid distinction above is exactly what this test pins**, so it must run on
  Longinus's 4:1 envelope and not on a sphere.
- negative: `rg "InverseTransformPoint" Assets/Scripts/Gameplay` returns no shield-surface
  projection outside `ShieldEnvelope.cs` — specifically that `FieldDriver.cs:125` and
  `ShieldManager.cs:76` no longer contain one.
- operator: `FieldShieldTest` behaves exactly as it does today. This cut is a no-op to look at,
  and if anything changes visually, something is wrong.

## Cut 3. The interceptor presenter and the panel pool

Depends on Cuts 1 and 2. This is where R2 lives.

**Adds:** `Assets/Scripts/Gameplay/ShieldInterceptor.cs`, one component,
`MonoBehaviour, IAbsorbPresenter`. **Adds no pool** — `Assets/Scripts/Prototype.cs` is the
pool (`Instantiate<T>()` at `:41`, `ReturnToPool()` at `:80`).

**Shape:**

```csharp
public class ShieldInterceptor : MonoBehaviour, IAbsorbPresenter
{
    public ShieldEnvelope Envelope;
    public Prototype PanelPrototype;      // a prefab carrying one ShieldPanel
    public int MaxLivePanels = 12;        // see the budget in §2.3
    public float PanelRadius = 2f;        // the SHIELD ITEM's size, not the hull's (R2)
    public float EnergyScale = 1f;
    public FracturePattern[] PatternByDamageType;

    public void Absorb(AbsorbEvent e) { /* project, reuse-or-spawn, strike */ }
}
```

**The event is enough, and here is exactly what it carries.**
`AbsorbEvent` (`Assets/Scripts/ServerShared/CapabilityEvents.cs:21-35`) has `float3 Position`,
`float3 Direction`, `float Magnitude`, `DamageType DamageType`. The publisher
(`FieldTester.cs:76`) passes `hit.point` and `ray.direction` — both **world space**, position
being the raycast's contact point on the field's `MeshCollider`. So:

- **Intercept point** = `Envelope.ProjectToSurface(e.Position.ToUnity())`. The projection is
  needed and is not optional: `hit.point` lands on whatever collider was struck, which is the
  field mesh in the test scene and will be the hull or the `Shield` collider in the real path
  — none of which is guaranteed to be on the envelope.
- **Which projection**: normalise-and-scale, matching `FieldDriver.cs:125` and
  `ShieldManager.cs:76`, not ray-ellipsoid. Two reasons. It is what the two existing consumers
  already do, so the panel appears where the existing ripple appears — and the two visuals run
  together under R1, so a disagreement between them would read as a bug. And ray-ellipsoid
  needs a ray origin the event does not carry; `Direction` alone does not give one. **If the
  operator later wants true ray-ellipsoid, `AbsorbEvent` has to grow an origin**, which is a
  capability-contract change and not a presentation change. Noted, not forked.
- **Panel normal** = `Envelope.SurfaceNormal(interceptPoint)` (R2).
- **Panel pose** = `position = interceptPoint`, `rotation = Quaternion.LookRotation(normal)`.
  `ShieldPanel` builds its disc in local XY with its normal on `+Z` and draws through
  `mpb.SetMatrix("_PanelToWorld", transform.localToWorldMatrix)`
  (`files/ShieldPanel.cs:394`), so **posing the transform is the entire pose plumbing and no
  code changes for it.**
- **Scale must be 1 and the panel must not be parented to the envelope object.** The
  `Shield` instance's scale is non-uniform (395, 235, 990) — parenting under it would ride
  that shear into `_PanelToWorld` and skew every shard. Parent to the ship root, or to the
  interceptor's own object if that carries unit scale.
- **Size** = `PanelRadius`, authored on the interceptor, which stands in for the shield item
  until items own presentation data. Explicitly *not* derived from the envelope (R2).
- **Energy** = `e.Magnitude * EnergyScale`; **pattern** from `PatternByDamageType` indexed by
  `(int)e.DamageType`, falling back to `Radial`.

**Reuse — the rule that makes temper erosion visible.**

A second hit landing inside a living panel must strike *that* panel, not spawn a new one.
Without this the temper mechanism (`files/ShieldSim.compute:286-298`) is invisible: every hit
would land on a fresh, fully-tempered panel and the "survive two, die on the third" behaviour
that the whole material model exists for could never happen.

Rule: keep the live panels in a list. On a strike, find the live panel whose centre is nearest
the intercept point; if that distance is `< PanelRadius * ReuseFraction` **and** the panel is
grown, call `Hit` on it and spawn nothing. Otherwise spawn.

`ReuseFraction` starts at 1.0 (anywhere inside the disc) and is a tuning field, not a fork.
The comparison is in world space against the panel's transform position; no need to go into
panel space, and going into panel space would be wrong for a hit near the rim of a strongly
curved envelope.

**The test that pins it** (batchmode, and it is the one test in this map that must not be
skipped):

1. Build an envelope, an interceptor with `MaxLivePanels >= 2`, and a pool.
2. Publish an `AbsorbEvent` at point P. Assert exactly one panel is live.
3. Step frames until that panel is grown (`growth >= 0.6` on the cells near the centre —
   read back `bState`).
4. Publish a second `AbsorbEvent` at P + a small offset well inside `PanelRadius`.
   **Assert the live-panel count is still one**, and assert the struck panel's minimum
   `temper` has fallen below what one hit produced — i.e. the second strike reached the same
   material, which is the thing that matters and is stronger than counting panels alone.
5. Publish a third at P + `PanelRadius * 3`. Assert the live count is now two.

Step 4's temper assertion is what separates "did not spawn" from "actually struck". A panel
count alone would pass if the second event were silently dropped.

**Lifecycle — spawn, grow, take hits, die, release.**

- **Spawn:** `PanelPrototype.Instantiate<ShieldPanel>()`, pose it, `ResetSim()`, set
  `_PanelSpawnTime` to the panel's own arm time (D21), `Hit(...)`. Cost: 8 µs plus a transform
  write (§2.4).
- **Grow:** the existing per-cell wavefront, now actually driven — `spawnDelay` is baked from
  the panel centre (`files/ShieldPanel.cs:136`, `TilingBuilder.cs:225`) and the panel centre is
  the intercept point, so it radiates outward from the strike for free.
- **Die:** when every cell is either broken-and-past-`shardLife` or has decayed. `KUpdate`
  already declines to append dead cells (`files/ShieldSim.compute:352`), so the panel's
  visible count reaching zero is the signal. Reading that back every frame would stall the
  GPU, so **use a timer**: `growDuration + shardLife + a margin`, started at arm time. It is a
  presentation, and a fixed lifetime is honest here; the readback would cost more than it buys.
- **Release:** `ReturnToPool()`, remove from the live list.
- **Pool exhausted** (`MaxLivePanels` reached): **recycle the oldest live panel** — return it
  and immediately re-arm it at the new intercept point. Do not drop the strike (that breaks
  "one event, one strike") and do not grow the pool (that breaks the §2.3 budget). Oldest-first
  is right because the oldest is the one furthest through its own fade.

**Authority map:**

- Owner: `ShieldInterceptor` owns the live-panel set, the spawn/reuse decision, and each
  panel's pose. It owns nothing about the material.
- Inputs: one `AbsorbEvent`; one `ShieldEnvelope`; its authored fields.
- Outputs: `Prototype.Instantiate` / `ReturnToPool` calls, transform writes, `ShieldPanel.Hit`
  calls.
- Derived state: the live list and each panel's arm time. No per-hit state beyond that —
  `ShieldPanel` already queues hits (`files/ShieldPanel.cs:82`).
- Forbidden writers: **nothing else may spawn, pose, or return a `ShieldPanel`.** The
  interceptor never touches `CapabilityEvents` (the binder owns subscription,
  `CapabilityPresentationBinder.cs:32-36`), never reads `FieldDriver` (R1), never writes the
  envelope, and never computes a surface projection itself (Cut 2).
- Shared paths: the test scene's clicks and the eventual real publisher (`ShieldManager.ShowHit`'s
  six weapon call sites, §1) reach this through the same `Absorb` method, because both go
  through `CapabilityEvents.Absorb`. **A panel spawned by a click and a panel spawned by a
  laser must be the same code path, posed by the same method** — if a later cut adds a separate
  "real hit" entry point, this invariant is gone.
- Deletion line: none in this cut, but it is gated on Cut 2 having already deleted the two
  hand-rolled projections. Writing this against the un-collapsed envelope would create a third.

**Verification:**

- test: the five-step reuse probe above. Plus: a strike with `Envelope == null` or
  `PanelPrototype == null` does not throw. Plus: publishing `MaxLivePanels + 3` widely-separated
  events leaves exactly `MaxLivePanels` live and issues no allocation after the first
  `MaxLivePanels` spawns.
- negative: `rg "ShieldPanel" Assets/Scripts --glob '!ShieldInterceptor.cs'` finds no second
  caller of `Hit`, `Instantiate` or `ReturnToPool` on a panel. `rg "localScale" ` over the
  interceptor is empty — it never scales a panel.
- operator: nothing to look at yet; Cut 4 rigs it.

## Cut 4. The scene rig

Depends on Cuts 1–3. Touches `Assets/Scenes/FieldShieldTest.unity` and one new prefab.

**Geometry, re-anchored.** The field is the `cubesphere Variant` instance at scene root,
localScale **(4, 3, 12)** (`FieldShieldTest.unity:948-959`), localPosition zero, camera at
`(0, 0, -50)`. A real `Longinus` hull already sits inside it at `:1189`. So the rig is:

- `ShieldEnvelope` on the field object (Cut 2 already adds it).
- `ShieldInterceptor` on the **field object**, next to `FieldDriver`, `ClickableCollider`
  (`:1077-1088`) and `CapabilityPresentationBinder` (`:1089-1100`), because the binder finds
  presenters with `GetComponentsInChildren` on its own object
  (`CapabilityPresentationBinder.cs:32`). Both `FieldDriver` and `ShieldInterceptor` are then
  subscribed — which is R1, achieved with no code change.
- A new `ShieldPanel` prefab carrying a `Prototype` and a `ShieldPanel`, with `sim` and
  `panelShader` assigned. `PanelRadius` on the interceptor, not on the prefab — the interceptor
  pushes it, because it stands in for the item.
- `PanelRadius` starting value: the envelope's smallest radius is 1.5 (y = 3/2), so a panel
  radius around **1.5–2** reads as a patch rather than a wall. At `cellSize 0.35` that is
  roughly 19–50 Hex cells, well inside budget.

**Note for the record, not a cut:** the flat-disc panel the first pass designed is still a
perfectly good shape for a *different* shield item — a "panel array" that presents a fixed
plate rather than spawning patches. R3 says a presentation is a drop-in behavioural
replacement, so that item is a later `IAbsorbPresenter` beside this one, reusing the same
`ShieldPanel` and the same envelope, with no pool and no reuse rule. Nothing in this map
should be shaped to prevent it, and nothing here does.

**How the scene is changed.** A throwaway editor script under `Assets/Scripts/Editor/`, run
once by `-batchmode -executeMethod`, that opens the scene, adds the components, builds the
prefab, assigns references by `AssetDatabase.LoadAssetAtPath`, and saves. **The script is
deleted in the same commit; the scene diff is the artifact.** Hand-editing scene YAML next to
a prefab instance with an override list is the exact shape that produced `1428ada3` ("Repair
the Addressables map after a bad sed prefixed every line"). Q4 keeps the alternatives open.

**Authority map:**

- Owner: the scene owns the rig. The editor script is a one-shot actuator with no standing
  authority and no life after the commit.
- Forbidden writers: nothing creates a `ShieldInterceptor` or an envelope at runtime.
  `FieldTester` is **not** modified — it already publishes the events, and giving it knowledge
  of the panel would re-split the fork-L authority.
- Deletion line: the editor script, before the commit closes.

**Verification:**

- test: batchmode headless open of `FieldShieldTest.unity` asserting exactly one
  `ShieldInterceptor`, one `ShieldEnvelope`, a non-null prototype, and that the binder resolves
  both `FieldDriver` and the interceptor as `IAbsorbPresenter`.
- **operator — this is the first look, and here is exactly what to look at:**
  1. Enter play mode. Nothing is visible until you click. **If a panel is visible on load, the
     pool is arming itself and Cut 3's lifecycle is wrong.**
  2. Click the field near the **nose** (far +Z). A lattice patch materialises at the click,
     growing outward from it, lying flat against the surface. The existing field ripple runs at
     the same time (R1) — both, not one.
  3. Click near the **flank** (far +X, where the envelope is 4:1). **The panel must lie flat
     against the flank, not tilt toward the ship's centre.** This is the single thing that
     falsifies the `SurfaceNormal` gradient — a `normalize(p)` normal is visibly wrong here and
     correct at the nose, so checking only the nose proves nothing.
  4. Click the **same spot twice**, about a second apart. The second click must **not** produce
     a second panel; it must strike the one already there.
  5. Click **many times, far apart, quickly**. Count never exceeds `MaxLivePanels`; the oldest
     patch is replaced rather than the newest being dropped.
  6. Let them all fade. Every panel disappears; nothing is left behind.
  7. Whether anything **shatters** is Cut 5, and at this point the honest answer is no.

## Cut 5. Make a hit actually break the panel

Depends on Cut 4 and on ruling Q1. A tuning and mechanism pass, not a wiring pass. §2.1.8
shows no energy fractures the panel at the supplied defaults; do not tune by eye first,
because two mechanism defects bound what tuning can reach.

- `ShieldSim.compute:226-232` (D9) — `u` is clamped to `±_StressClamp`, `v` is not. A hard hit
  drives `v` past what the clamp permits, `u` pins in compression, and the field never swings
  back into tension. That is the measured collapse above energy 60. Clamp `v` too, or normalise
  the injection so energy maps to a bounded velocity.
- `ShieldPanel.cs:301,359` (D10) — `_Dt` is set to `fixedSubstepDt` before the injection loop
  and not restored until after `KFracture` is dispatched, so `KFracture` integrates damage and
  temper erosion with a substep `dt` while running **once per frame**. Damage accrues ~6× slow.
  Either dispatch `KFracture` inside the substep loop (physically honest, five more dispatches
  per frame — and §2.3 says dispatch count is what costs, so this is not free at 16 live panels)
  or hand it the frame `dt` (one line). Say which in the commit; this is "which clock owns
  fracture", not a constant.
- `ShieldSim.compute:160,191,276` (D17) — **the interceptor's own worst defect.** `KInject`,
  `KWaveStep` and `KFracture` all skip cells with `growth < 0.6`. A panel spawned by an
  interception is at `growth = 0` in the frame it is struck, so **the hit that created the
  panel injects nothing at all.** Every panel would materialise inert and only respond to the
  *second* hit. Fix by one of: arm the panel with `_PanelSpawnTime` back-dated so the centre is
  already grown; hold the strike in `pending` until growth passes the gate (the queue at
  `files/ShieldPanel.cs:82` already exists for this); or drop the gate for the injecting cell.
  **This must be settled before the operator look in Cut 4 step 2, or that step shows a
  growing-but-dead patch and reads as a tuning problem when it is a gating problem.** It is
  listed here rather than in Cut 3 only because the fix lives in the sim.
- Then the tuning itself: `ShieldPanel.cs:34,53,56` and the interceptor's `EnergyScale`.

**Verification:**

- test: batchmode energy sweep, readback of `bState`, asserting weak energy leaves
  `broken == 0` while strong energy breaks a bounded non-zero fraction — the same readback
  shape as §2.1.8, already written once.
- negative: no energy in the authored range leaves `maxPeakTension == 0` with
  `minTemper == temperInit` (the saturation signature). And: a panel struck in its **spawn
  frame** shows non-zero `|v|` in the cells near the centre — the D17 negative.
- operator: strong hits dice the patch. **Repeated hits on the same spot break through on a
  later hit than the first** — this is the payoff of the Cut 3 reuse rule and the temper
  mechanism together, and it is the whole reason the reuse rule exists.

## 6. Cost and budget

**Build budget.** One tiling per shield item, prebuilt at load. Hex at the interceptor's
working size costs **0.91 ms (85 cells, r3)** to **2.30 ms (253 cells, r5)**, once, ever
(§2.2). The pool's `ShieldPanel` instances are preallocated: 32 instances at 253 cells cost
111 ms to construct (§2.3), which is a load-time cost and must not happen during play.

**Frame budget.** ~0.12 ms CPU dispatch issuance per live panel, flat in cell count (§2.3).
`MaxLivePanels = 12` is ~1.4 ms/frame; 16 is ~2.0 ms. **Cap at 12 for one ship.** The cap is
the real budget knob; cell count is not, and raising `cellSize` to "optimise" would only cost
visual density for nothing.

**Interception cost.** `ResetSim` + transform write = **8–11 µs** (§2.4), flat. Free.

**Static-buffer sharing, named and deliberately deferred.** `bCells`, `bVerts` and `bEdges`
are byte-identical across every panel of one shield item, so the pool currently holds N copies
of the same data — at 253 cells that is ~40 KB × N of pure duplication and N redundant
uploads at load. Hoisting them into one shared owner is the obvious subtraction. It is **not**
in this map because it changes `ShieldPanel`'s buffer ownership, which §2.3 says buys no frame
time (the cost is dispatch count, not data), and Cut 1's authority map says `ShieldPanel` owns
its buffers. Do it as its own cut when the pool is real and the duplication is measurable,
not speculatively.

## 7. Defects in the supplied code

Confirmed by compiling and running, not by reading. D1–D4 are fatal to compilation; D17 is
fatal to the interceptor design specifically.

| # | Where | Finding |
| --- | --- | --- |
| D1 | `files/TilingBuilder.cs:214` | CS0136: `dist` shadows the `dist` at `:225`. Does not compile. **(probe)** |
| D2 | `files/ShieldPanel.cs:410,415,420` | `RenderParams.shaderPass` does not exist in 6000.3.24f1. **(probe)** |
| D3 | `files/TilingBuilder.cs:28` vs `:12-27` | `Stride = 4 * 16` but the struct is 15 floats; `SetData` throws. **(probe)** |
| D4 | `files/ShieldCommon.hlsl:18` | `centroid` is reserved in the compute compiler. **(probe)** |
| D5 | `files/ShieldPanel.cs:411-421` | Pass selection does not exist at this API; the three per-pass args buffers cannot work as designed, independent of D2. **(probe)** |
| D6 | `files/ShieldSim.compute:38` | Comment says `_BreakBudget` is a "single element"; the C# allocates two and reads index 1. **A lying comment, not a bug.** |
| D7 | `files/Tilings.cs:302-333` | `VoronoiCells` is O(n² log n). **Quantified in §2.2: 134 ms at r3, 1.03 s at r5, 15.0 s at r10.** Unusable. **(probe)** |
| D8 | `files/Tilings.cs:286-288` | `Vector2.zero` is both the invalid-cell sentinel and a legal centroid. `idx` at `:292,297` is dead. |
| D9 | `files/ShieldSim.compute:226-232` | `u` clamped, `v` not; hard hits pin the field in compression and no tension reflects. The measured collapse in §2.1.8. **(probe)** |
| D10 | `files/ShieldPanel.cs:301,356,359` | `KFracture` runs once per frame but integrates with the substep `dt`; damage accrues ~6× slow. |
| D11 | `files/ShieldSim.compute:326-330` | `_V[e.nbr] +=` is a non-atomic read-modify-write across threads; the cascade is nondeterministic. Fine for an effect; fatal to any test pinning an exact shard count. |
| D12 | `files/ShieldPanel.cs:296-298` | A two-element `uint[]` allocated every frame. **Worse under the pool: this is now N allocations per frame, not one.** Cache it. |
| D13 | `files/ShieldPanel.cs:9` | `[ExecuteAlways]` runs the full sim in every editor scene view and rebuilds on every domain reload. **Incompatible with a pool** — `OnEnable → Rebuild` would run a 1–21 ms tiling build every time `Prototype.Instantiate` wakes an instance (§2.4). Must go; arming becomes explicit. |
| D14 | `files/ShieldPanel.cs:426-440` | `ReleaseAll` does not clear `pending`, so hits queued before a rebuild are injected into the new tiling. **Now a real bug, not a principle:** a recycled pool panel would fire the previous victim's queued hit. |
| D15 | `files/ShieldPanel.shader` Conduit pass | `Warning 172: potentially uninitialized variable (BuildLine)`. Warning only. **(probe)** |
| D16 | `files/Tilings.cs:91-116` | TriHex interstitial triangles are placed by a separate polar construction rather than from the hex corners they share, so welding is not guaranteed. **Superseded by D19 — it is worse than suspected.** |
| **D17** | `files/ShieldSim.compute:160,191,276` | **`growth < 0.6` gates injection, wave and fracture. A panel spawned at the intercept point is at `growth = 0` when struck, so the hit that created it does nothing.** Fatal to the interceptor design. See Cut 5. |
| **D18** | `files/Tilings.cs` Penrose path | **Penrose ignores `panelRadius`** — 460 cells (merged) / 890 (unmerged) at radius 1.5 *and* radius 10, identically. R2 says panel size comes from the shield item, so Penrose cannot express size at all. Hex can. **(probe)** |
| **D19** | `files/Tilings.cs:91-116` | **TriHex produces zero dual edges at every radius** (0 edges at r1.5, 3, 5, 10). No adjacency means no wave, no fracture, no anything. It is inert, not merely suspect. **(probe)** |
| **D20** | `files/ShieldPanel.cs:360` | `_PanelSpawnTime` is hardcoded `0f`, so the materialisation wavefront fires once at sim time zero and never again. Must become the panel's arm time — **this is the one line that turns the dead wavefront into the interceptor's spawn animation.** |
| D21 | `files/TilingBuilder.cs:9,119` | `Limits.MaxVerts = 8`, and polygons with more vertices are **silently dropped** at `:119`. A tiling that quietly loses cells looks like a tiling bug. Log it. |

Buffer lifetime, `SetCounterValue` and `CopyCount` were checked specifically and are
**correct**: `bVisible` is `Append | Structured` (`files/ShieldPanel.cs:164`), reset at `:366`
before the appending dispatch, copied at `:369` into offset `sizeof(uint)` (= `instanceCount`
in `IndirectDrawArgs`), and every buffer is released by `ReleaseAll` on both `OnDisable` and
the head of `Rebuild`. A probe readback of `argsFill instanceCount=253` against `cells=253`
confirms the counter path end to end. **(probe)**

## 8. Operator questions

Most blocking first.

**Q1 — Does the first landing have to fracture?** (Carried over; still first, now with a
second reason.) §2.1.8 says no energy breaks a cell at the supplied defaults, and D9/D10 bound
what tuning alone can reach. D17 adds that the spawning hit currently injects nothing at all.
**A:** land Cuts 1–4, ship an interceptor that spawns, poses and materialises panels correctly
with a visible stress wave and no fracture, and take Cut 5 as its own pass. **B:** hold
everything until Cut 5 lands too. **Recommended: A**, with one amendment to the previous
pass's reasoning — **D17 must be fixed in A, not deferred to Cut 5**, because without it the
Cut 4 operator look shows an inert patch and proves nothing about the wiring it exists to
prove. Fracture can wait; injection cannot. Wiring is the thing ruled on and it is verifiable
on its own.

**Q2 — Is the envelope derived from the transform, or authored as explicit fields?** Cut 2
recommends **derived**, because the `Shield` prefab instance transform already owns the
numbers and also drives the collider, and a second authored copy would be a second truth.
The cost is that the envelope can only ever be a rotated ellipsoid. **A: derived (recommended).
B: explicit centre + radii fields, transform ignored.** B is the operator's literal phrasing
and would be right if the envelope is meant to diverge from the collider — if it is, say so,
because that is a different machine.

**Q3 — Which tiling?** D18 and D19 narrow this hard. **Penrose cannot express panel size**
(identical cell count at every radius) and R2 requires size to come from the item. **TriHex is
inert** (zero adjacency). **Voronoi is 15 s at r10 and 1 s at r5.** That leaves **Hex** as the
only kind that is both correct and sized. **Recommended: Hex, and treat D18 as a real defect
to fix later if Penrose's look is wanted** — Penrose is the better-looking tiling and losing it
is a genuine cost, but it cannot ship as the interceptor's tiling until it respects
`panelRadius`.

**Q4 — Which fracture pattern per damage type, and where authored?** Six of each, no natural
ordering. Proposed: Kinetic→Radial, Optical→Lance, Electric→Star, Thermal→Crystal,
Corrosive→Fizzle, Ionizing→Spiral. **A:** a serialized `FracturePattern[]` on the interceptor
indexed by `(int)DamageType`. **B:** a switch in code. **C:** item data. **Recommended: A** —
presentation choice belongs with the presentation and retunes without a compile. C is wrong
today: the absorb capability owns no data yet (`CapabilityEvents.cs:17-20` parks it explicitly).

**Q5 — How is the scene edited?** **A:** one-shot editor script run in batchmode, deleted in
the same commit. **B:** hand-edited YAML. **C:** the operator clicks it together.
**Ruled C (operator, 2026-09-18): "screw it, I'll do any editor shenanigans we need for
now."** Cut 4 therefore ships no editor script and no YAML edit: it ships an exact rig recipe
for the operator (object names, parents, components, transform values, which asset goes in
which field) plus a batchmode check that reads the saved scene back and refuses a rig that is
wrong. Agent-driven editor commands through Brokkr are parked
(`F:\Projects\Brokkr\docsgent-access-cut.md`).

Superseded reasoning: **Recommended: A.** B is the failure mode that produced `1428ada3`. If the operator would
rather keep the script as a menu item, say so — that makes it a standing second authority over
the rig and should be deliberate, not leftover.

**Q6 — Per-pass draw toggles?** `drawFill`/`drawOutline`/`drawConduits` die with the
single-call draw (§2.1.5). **A: gone. B: back as shader uniforms zeroing each pass's alpha.**
**Recommended: A**, until someone wants them. No consumer today.

**Q7 — Buy NUnit coverage with an asmdef?** `Tests.asmdef` references only
`UnityEngine.TestRunner`, `UnityEditor.TestRunner` and `Aetheria.Shared.Unity`, with
`autoReferenced: false`, so it cannot see `Assembly-CSharp`. **A:** no asmdef; pin the rules
with batchmode probes. **B:** add `Aetheria.ShieldField.asmdef`. **Recommended: A**, but
weaker than last pass: `ShieldEnvelope` (Cut 2) is pure testable math and is exactly the kind
of thing an asmdef would buy real coverage for. If the operator wants B, **scope it to
`ShieldEnvelope` alone**, not to the six sources — the envelope is the piece whose arithmetic
has a wrong answer that looks plausible (sphere vs ellipsoid normal), and it is three lines of
reference edge rather than a compile boundary across 5.4k lines.

## 9. Superseded: the flat-panel design

Kept because it explains why several defects are recorded the way they are, and because the
shape itself is still wanted for a different item (Cut 4's note).

The committed pass at `1fdf8151` designed **one permanent flat disc** parented near the hull:
a `ShieldPanel` living in `FieldShieldTest` from scene load, materialising once at sim time
zero, with a `ShieldPanelPresenter` converting each `AbsorbEvent` into one `Hit` and doing
nothing else. It had no pool, no lifecycle, no envelope, and no pose problem — the panel's
transform was authored in the scene and never moved.

R2 retired it. What changed and why it matters:

- **No pool → a pool.** Panels are per-interception, so `Prototype` enters the map and
  `[ExecuteAlways]` / `OnEnable → Rebuild` (D13) goes from "wasteful" to "incompatible", and
  D12/D14 go from "principle" to "real bug under recycling".
- **Fixed pose → derived pose.** The panel's normal now comes from the envelope, which is what
  forced the discovery that two implicit ellipsoids already exist (§1) and that
  `normalize(p)` is the wrong normal for a 4:1 ellipsoid (Cut 2).
- **Materialise once → materialise per interception.** `_PanelSpawnTime` goes from an ignored
  constant to the mechanism (D20), and the wavefront bake turns out to already be correct.
- **No reuse rule → a reuse rule.** Without it the temper model is unobservable (Cut 3).
- **Q2 "replace or alongside" is answered alongside (R1)**, so `FieldDriver.Absorb`
  (`FieldDriver.cs:158`) and its hit-buffer machinery (`:76,105-132,204-217`) are **not**
  deleted. The old map's recommendation to delete them later is withdrawn.

Its geometry numbers were also simply wrong: it placed the panel against a field scaled
`(10, 10, 20)`, and the scene says `(4, 3, 12)` (`FieldShieldTest.unity:948-959`).

## 10. Subtraction ledger

| Cut | Removed | Added | Deps/targets |
| --- | --- | --- | --- |
| 1 | `ShieldPanelTester` (~38 lines), 2 args buffers, 3 draw bools, 2 draw calls, `[ExecuteAlways]` | 6 files (~2.1k lines) | none; no package, no pipeline, no asmdef |
| 2 | 2 hand-rolled ellipsoid projections (`FieldDriver.cs:125`, `ShieldManager.cs:76`); `FieldDriver.transform.localScale` demoted from envelope owner to mesh geometry | 1 file (~40 lines), 3 component instances | none |
| 3 | — (**no pool written**: `Assets/Scripts/Prototype.cs` is reused) | 1 file (~120 lines) | none |
| 4 | the one-shot editor script, same commit | 1 prefab, 2 component instances | none |
| 5 | — | ~10 lines changed | none |

Net: additive, and it should be. The capability bought is a simulation that exists nowhere in
the tree. The honest subtractions are Cut 2's — two duplicate projections collapsed into one
owner, which is a real reduction in authority count and the only part of this map that makes
the *existing* machine simpler — and Cut 3's refusal to write a pool that already exists.
Cut 1's line count is the liability; it is bought with an explicitly requested capability and
nothing currently in the repo can be deleted in exchange for it. The `FieldDriver` absorb
visual is **not** available as a subtraction: R1 rules it stays.
