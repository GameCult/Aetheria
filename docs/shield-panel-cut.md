# Shield Panel: GPU Fracture Simulation Cut Map

Date: 2026-09-18

Status: Imagination pass, cut map. Nothing here has landed. Repo anchors are against
`codex/item-provenance` HEAD `491811bd`. The six incoming sources are not in the repo
yet; they are anchored as `files/<name>:<line>` against the operator's drop at
`<scratch>/files/`.

Mechanism claims marked **(probe)** were run, not reasoned. The probe was a throwaway
Unity 6000.3.24f1 project in scratch (`<scratch>/probe`), holding only the six files, a
built-in port of the shader, and one editor script driven by
`-batchmode -executeMethod`. Nothing was built in the Aetheria tree and no Unity session
was opened against it; Hands was concurrently editing `Assets/Content`,
`Packages/manifest.json` and the Addressables surfaces. Logs: `<scratch>/probe{,2..6}.log`.
The probe project is scratch and will be removed with the session; §1 records what it
proved so no one has to re-run it to trust the map.

## 0. Target

Operator, 2026-09-18: **wire it up to real hits.** The absorb capability in the
`FieldShieldTest` scene drives the fracture panel; no standalone click-to-hit harness is
built first.

Ends:

- A `ShieldPanel` exists in `FieldShieldTest`, materialises on load, and fractures when
  the absorb capability publishes an `AbsorbEvent`.
- The event is the only input to the simulation. Presentation converts; it decides
  nothing about the rule, matching `Assets/Scripts/Gameplay/CapabilityPresenters.cs:8`
  and the fork-L contract in `Assets/Scripts/ServerShared/CapabilityEvents.cs:9-20`.
- The sources compile and render on the built-in render pipeline. Aetheria has no URP:
  `ProjectSettings/GraphicsSettings.asset:41` is `m_CustomRenderPipeline: {fileID: 0}`
  and `Packages/manifest.json` carries no render-pipeline package.

Invariants:

- **One owner of the hit.** `ShieldPanel.Hit` is the only entry point into the
  simulation. The presenter is the only caller. Nothing else reaches the panel.
- **One event, one hit.** An `AbsorbEvent` produces exactly one `Hit`, carrying the
  event's own world position and direction unmodified.
- **Presentation owns only presentation.** Damage-type-to-pattern and magnitude-to-energy
  are presentation mappings authored on the presenter. The absorb capability's data (the
  damage-type mask) stays out of scope, as ruled 2026-09-17.
- **No second render pipeline.** Nothing in this cut adds a package, a pipeline asset, or
  a URP include path.
- **No Unity.Mathematics.** The incoming sources use `UnityEngine.Vector2/Vector3` only
  (verified: no `Unity.Mathematics` or `using static Unity` in any of the six). The
  presenter converts `CultMath.float3` with `CultMath.UnityBridge`, the way
  `Assets/Scripts/Gameplay/FieldDriver.cs:125` already does.

## 1. Probe record

All results from Unity 6000.3.24f1, Direct3D12, `srp=builtin`, batchmode with graphics.

1. **The shader ports to built-in with three substitutions.** Removing
   `"RenderPipeline"="UniversalPipeline"` from the SubShader tags, replacing the URP
   `Core.hlsl` include with `UnityCG.cginc`, and renaming the three
   `TransformWorldToHClip(` calls to `UnityWorldToClipPos(` is sufficient:
   `shader supported=True, messages=0` (probe3 had one warning, see §1.7). No other URP
   symbol is used — `_ScreenParams`, `noperspective`, `clip`, `saturate` and
   `StructuredBuffer` at `#pragma target 4.5` are all built-in surface. **(probe)**
2. **Indirect drawing works on built-in.** `Graphics.RenderPrimitivesIndirect` with
   `RenderParams`, a `MaterialPropertyBlock` carrying `GraphicsBuffer`s, and an append
   buffer counter copied in by `GraphicsBuffer.CopyCount` rendered the panel:
   `argsFill vertsPerInstance=24 instanceCount=253`, 38036 of 65536 pixels lit. The cut
   does not have to change the drawing strategy. **(probe)**
3. **`RenderParams` has no `shaderPass`.** Full property list in 6000.3.24f1:
   `layer renderingLayerMask rendererPriority instanceID entityId worldBounds camera
   motionVectorMode reflectionProbeUsage material matProps shadowCastingMode
   receiveShadows lightProbeUsage lightProbeProxyVolume overrideSceneCullingMask
   sceneCullingMask forceMeshLod meshLodSelectionBias`. `files/ShieldPanel.cs:410,415,420`
   do not compile. **(probe)**
4. **A multi-pass material renders every pass from one call.** A two-pass probe shader
   (red triangle in pass 0, green in pass 1) drawn once with `Graphics.RenderPrimitives`
   produced `redPixels=40992 greenPixels=40983`. Pass selection is not available at this
   API, so the supplied "one args buffer per pass index" design cannot work as written.
   **(probe)**
5. **One args buffer sized to the widest pass is equivalent.** Setting every args buffer
   to `MaxVerts * 2 * 6 = 96` vertices per instance and issuing a single
   `RenderPrimitivesIndirect` produced pixel-identical output to the three-call version:
   `singleCall=False litPixels=43929` vs `singleCall=True litPixels=43929`. Each pass
   already culls its own surplus vertices (`files/ShieldPanel.shader:238` fill,
   `:287` outline, `:347` conduit), and additive `Blend One One` with `ZWrite Off` makes
   pass order irrelevant. **(probe)**
6. **`centroid` is a reserved word in the compute path.** `ShieldSim.compute` failed with
   `Error 18: syntax error: unexpected token 'centroid' at kernel KInit` — the
   `CellStatic.centroid` member at `files/ShieldCommon.hlsl:18`. Renaming it to `center`
   in the HLSL, compute and shader gives `compute supported=True, messages=0`. The
   fragment/vertex compiler tolerates it; the compute compiler does not. **(probe)**
7. **One live shader warning survives the port:** `Warning 172: use of potentially
   uninitialized variable (BuildLine)`, Conduit vertex program. Warning only; the pass
   renders. Worth a look while touching `BuildLine`, not worth a cut of its own.
8. **The supplied tuning never fractures anything.** Hex tiling, `panelRadius 5`,
   `cellSize 0.35`, 253 cells, a head-on Radial hit at the centre, 60 frames after:

   | energy | broken | maxDamage | maxPeakTension | minTemper |
   | --- | --- | --- | --- | --- |
   | 3.5 (the supplied "strong" hit) | 0/253 | 0 | 0.058 | 0.547 |
   | 35 | 0/253 | 0.0012 | 0.579 | 0.505 |
   | 60 | 0/253 | 0.0097 | 0.743 | 0.487 |
   | 90 | 0/253 | 0 | 0.076 | 0.548 |
   | 120 | 0/253 | 0 | 0 | 0.55 |
   | 160 | 0/253 | 0 | 0.084 | 0.549 |
   | 220 | 0/253 | 0 | 0 | 0.55 |
   | 350 | 0/253 | 0 | 0 | 0.55 |
   | 3500 | 0/253 | 0 | 0 | 0.55 |

   A break needs `u > temper + tensileStrength*toughness ≈ 1.45` **and** `damage >= 0.75`.
   Peak tension is non-monotonic in energy and collapses above ~60: the field saturates
   into the compression clamp and never swings back into tension. Causes named in §5
   (D9, D10). **(probe)** This is the reason Cut 4 exists and why it is the operator's
   first fork: wiring real hits to a simulation that cannot break is a pretty stress wave
   and nothing else.

## Cut 1. Land the sources, compiling, on built-in

- **Repo/branch:** Aetheria `codex/item-provenance` from `491811bd`. Depends on nothing.
  Touches no file Hands is holding (`Packages/manifest.json`, `Assets/Content`,
  `ServerShared` asset-path fields, `GameData/Aetheria.cc`, `ActionBarSlot.cs`,
  `UnityHelpers.cs`).
- **First:** confirm Hands' Addressables cuts have landed or are on files disjoint from
  `Assets/Shaders/`. Nothing here goes near `Assets/Resources`, which is mid-move to
  `Assets/Content`.

**Location.** All six files go to `Assets/Shaders/Compute/ShieldField/`, matching the
repo's own convention for compute-driven effects: `Assets/Shaders/Compute/Lightning/`
holds `Lightning.compute`, `Lightning.shader`, `LightningVertex.cginc` and
`LightningCompute.cs` together; `Slime/` does the same. Colocation is also load-bearing —
`#include "ShieldCommon.hlsl"` resolves relative to the including file (probe). No import
settings are needed: `.compute` and `.hlsl` import with no configurable state.

**Assembly.** No asmdef. The files land in `Assembly-CSharp` with everything else in
`Assets/Shaders/Compute/*.cs` and `Assets/Scripts/Gameplay/`. The `ShieldField` namespace
collides with nothing (`rg "class ShieldPanel\b|class Tilings\b|class Limits\b|enum
TilingKind|class PolygonSoup|class VertexWelder|enum FracturePattern"` over
`Assets/Scripts` and `Assets/Shaders` returns nothing). See Q7 for the testability cost.

**Deletes first:**

- `files/ShieldPanel.cs:450-487` — `ShieldPanelTester`. It is the standalone click harness
  the operator ruled against, and it cannot run anyway:
  `ProjectSettings/ProjectSettings.asset:797` is `activeInputHandler: 1` (new Input System
  only), so `Input.GetMouseButtonDown` and `Input.mousePosition` throw at runtime. Delete
  it rather than port it; the absorb path replaces it.
- `files/ShieldPanel.cs:90` — `argsOutline`, `argsConduit` and their `MakeArgs` calls
  at `:172-173`, plus the `drawFill`/`drawOutline`/`drawConduits` bools at `:68-70` and
  their branches at `:408-422`. Replaced by one args buffer and one draw (probe §1.5).
  Per-pass toggles come back as a fork (Q6) if the operator wants them, and then as
  shader uniforms, not as draw calls.

**Per-file changes (against the supplied files):**

- `ShieldCommon.hlsl:18` — `float2 centroid;` → `float2 center;`. Every `.centroid` in
  `ShieldSim.compute` and `ShieldPanel.shader` follows. Required to compile (§1.6).
- `ShieldCommon.hlsl:31` — add `float pad2;` after `pad1`, taking `CellStatic` to 16
  floats.
- `TilingBuilder.cs:27` — add `public float pad2;` and initialise it at `:242`. Today the
  C# struct is 15 floats (60 bytes) while `Stride` at `:28` claims `4 * 16` (64), and
  `SetData` throws: *"One of C# data stride (60 bytes) and Buffer stride (64 bytes) should
  be multiple of other"* (probe). Padding to 16 keeps the 16-byte-aligned stride the
  comment intends; the alternative, `Stride = 4 * 15`, also works and is smaller. Either
  fixes it — do not fix only one side.
- `TilingBuilder.cs:214` — rename the inner `dist` to `nbrDist` (and `:221`). CS0136: it
  shadows the `dist` declared at `:225` in the enclosing scope. The file does not compile
  as delivered (probe).
- `ShieldPanel.shader:27` — drop `"RenderPipeline"="UniversalPipeline"`, add
  `"IgnoreProjector"="True"`.
- `ShieldPanel.shader:33` — `#include "UnityCG.cginc"` in place of the URP `Core.hlsl`.
- `ShieldPanel.shader:162,163,244` — `TransformWorldToHClip(` → `UnityWorldToClipPos(`.
- `ShieldPanel.cs:171-173` — one `argsFill = MakeArgs((uint)(MaxV * 2 * 6));`.
- `ShieldPanel.cs:369-371` — one `GraphicsBuffer.CopyCount(bVisible, argsFill, sizeof(uint));`.
- `ShieldPanel.cs:408-422` — one `Graphics.RenderPrimitivesIndirect(rp,
  MeshTopology.Triangles, argsFill, 1, 0);`, no `rp.shaderPass`.
- `ShieldPanel.cs:296-298` — hoist the `new uint[] { 0u, cap }` to a cached field. It
  allocates every frame today.
- `ShieldPanel.cs:9` — drop `[ExecuteAlways]` (Q5). With it, `Update` runs the whole
  simulation and three GPU dispatch groups every editor frame in every open scene view,
  and `OnEnable` rebuilds the tiling on every domain reload.
- Defect fixes D3-D8 from §5, each named in the commit.

**Authority map:**

- Owner: `ShieldPanel` owns the simulation and its GPU buffers. `TilingBuilder` owns
  adjacency, areas and the free-surface ghost weights. `Tilings` owns geometry generation.
- Inputs: authored fields on the component; `Hit(worldPos, worldDir, energy, pattern,
  radius)` from outside.
- Outputs: three indirect draws' worth of pixels, and GPU state readable for verification.
- Derived state: `worldBounds`, `groups`, `echoSlot`, `simTime` are derived from the data
  and the frame clock; none is authored.
- Forbidden writers: nothing outside `ShieldPanel` touches `bState`, `bUA/bUB`, `bV`,
  `bEcho`, `bFlow`, `bVisible`, `bBudget` or the args buffer. `ShieldPanelTester` is
  deleted rather than kept as a second hit source.
- Shared paths: edit-mode and play-mode take the same `Update` body (once `ExecuteAlways`
  is settled); `Rebuild` is the only path that allocates, and always releases first.
- Deletion line: `ShieldPanelTester`, the two surplus args buffers, the three draw bools,
  and the `rp.shaderPass` writes are gone before the presenter is written.

**Verification:**

- builds: Unity batchmode compile of the Aetheria project (`-quit -batchmode
  -projectPath . -logFile`), zero errors. `ShaderUtil.GetShaderMessages` on
  `ShieldPanel.shader` and `GetComputeShaderMessages` on `ShieldSim.compute` report zero
  errors (the §1.7 warning may remain).
- test: none. The rule worth pinning is in Cut 2; there is nothing here a unit test would
  hold that the compiler does not.
- negative: `rg "UniversalPipeline|render-pipelines\.universal|TransformWorldToHClip"
  Assets/Shaders/Compute/ShieldField` is empty. `rg "shaderPass" Assets` is empty
  (verified: no legitimate use of that identifier in the tree today).
- operator: none yet. Cut 3 is the first thing to look at.

## Cut 2. The absorb presenter

- **Repo/branch:** same. Depends on Cut 1.
- **Adds:** `Assets/Scripts/Gameplay/ShieldPanelPresenter.cs`, one component,
  `MonoBehaviour, IAbsorbPresenter`.
- **Deletes:** nothing. `FieldDriver.Absorb` (`Assets/Scripts/Gameplay/FieldDriver.cs:158`)
  stays or goes by operator ruling Q2; the binder already supports both presenters
  subscribing to the same stream
  (`Assets/Scripts/Gameplay/CapabilityPresentationBinder.cs:32-36` iterates every
  `IAbsorbPresenter` in the children).

**Shape:**

```
public class ShieldPanelPresenter : MonoBehaviour, IAbsorbPresenter
{
    public ShieldPanel Panel;                       // sibling object, not a child
    public float EnergyScale = 1f;                  // AbsorbEvent.Magnitude -> panel energy
    public float HitRadius = -1f;                   // -1 = the panel's own default
    public FracturePattern[] PatternByDamageType;   // indexed by (int)DamageType, length 6

    public void Absorb(AbsorbEvent e)
    {
        if (Panel == null) return;
        Panel.Hit(e.Position.ToUnity(), e.Direction.ToUnity(),
                  e.Magnitude * EnergyScale, PatternFor(e.DamageType), HitRadius);
    }
}
```

`AbsorbEvent.Position` is the world hit point and `Direction` is the incoming ray
direction (`Assets/Scripts/UI/FieldTester.cs:76` publishes `hit.point` and
`ray.direction`), which is exactly what `Hit` wants — `files/ShieldPanel.cs:257-282`
transforms both into panel space itself. No projection, no normalisation, no
reinterpretation on the presentation side: that is the "one event, one hit" invariant.

`PatternFor` reads the serialized table and falls back to `FracturePattern.Radial` when
the array is short or unset, so an unauthored component still works. The mapping itself is
Q3.

**Authority map:**

- Owner: `ShieldPanelPresenter` owns the event-to-hit conversion, and nothing else.
- Inputs: one `AbsorbEvent`; three authored fields.
- Outputs: one `ShieldPanel.Hit` call.
- Derived state: none. The presenter holds no per-hit state, no queue, no timers — the
  panel already queues hits (`files/ShieldPanel.cs:82`).
- Forbidden writers: the presenter never touches `CapabilityEvents`; the binder owns
  subscription. It never reads `FieldDriver`. Nothing writes `ShieldPanel` fields at
  runtime except its own inspector values.
- Shared paths: the operator's click and any later real absorb capability reach the panel
  through the same `Absorb` method, because both go through `CapabilityEvents.Absorb`.
- Deletion line: none; this cut is purely additive, and buys the only wiring the operator
  asked for.

**Verification:**

- builds: project compiles.
- test: nothing in `Assets/Scripts/Tests` can reach `Assembly-CSharp` (`Tests.asmdef`
  references only `UnityEngine.TestRunner`, `UnityEditor.TestRunner` and
  `Aetheria.Shared.Unity`, and `autoReferenced: false`). So the rule is pinned by a
  batchmode probe instead, and the probe checks it at the layer where it would break:
  build a `CapabilityEvents`, a `CapabilityPresentationBinder`, the presenter and a real
  panel; publish one `AbsorbEvent` at a known off-centre position; step the sim; read
  `bState` back and assert the injected region is centred where the event said and that a
  second identical publish doubles it. A presenter with `Panel == null` publishes and does
  not throw. See Q7 for the alternative that buys NUnit coverage.
- negative: `rg "ShieldPanel" Assets/Scripts --glob '!ShieldPanelPresenter.cs'` finds no
  second caller of `Hit`.

## Cut 3. The scene rig

- **Repo/branch:** same. Depends on Cuts 1-2.
- **Touches:** `Assets/Scenes/FieldShieldTest.unity` only.

**Geometry.** The existing field is `Assets/Prefabs/cubesphere Variant.prefab`, a unit
cubesphere scaled `(10, 10, 20)` at the origin, with the camera at `(0, 0, -50)` looking
`+Z`. `ShieldPanel` builds a flat disc in local XY with its normal on `+Z`. So the panel
becomes a **top-level scene object** at `(0, 0, -10)` with identity rotation and unit
scale, `panelRadius = 10` — the near face of the field, same silhouette, undistorted. It
must not be parented to the field: the field's non-uniform `(10,10,20)` scale would ride
into `_PanelToWorld` and shear every shard.

Clicks already land on the field's `MeshCollider` and the absorb event carries that world
point, whose X/Y is what the panel consumes. The panel therefore needs no collider of its
own and no change to `ClickableCollider` or the click catcher.

**Wiring.** The presenter component goes on the **field** GameObject, next to
`FieldDriver`, `ClickableCollider` and `CapabilityPresentationBinder`, because the binder
finds presenters with `GetComponentsInChildren` on its own object
(`CapabilityPresentationBinder.cs:32`). Its `Panel` field points at the separate panel
object. One cross-reference, no scale contamination.

**How the scene is changed.** Hands cannot click, and hand-editing the YAML means
inventing fileIDs next to a prefab instance with an override list — the exact shape that
produced `1428ada3` ("Repair the Addressables map after a bad sed prefixed every line").
So: a throwaway editor script under `Assets/Scripts/Editor/`, run once by
`-batchmode -executeMethod`, that opens the scene, creates the panel object, adds and
configures `ShieldPanel` (assigning the shader and compute assets by
`AssetDatabase.LoadAssetAtPath`), adds the presenter to the field object, assigns the
reference, and `EditorSceneManager.SaveScene`s. **The script is deleted in the same
commit; the scene diff is the artifact.** Keeping it would mean a second authority over
the rig that nobody would remember to re-run. Q4 offers the alternatives.

**Authority map:**

- Owner: the scene owns the rig. The editor script is a one-shot actuator with no
  standing authority and no life after the commit.
- Inputs: the two prefab/asset GUIDs and the transform numbers above.
- Outputs: one new GameObject, one new component on the field object, one reference.
- Derived state: none.
- Forbidden writers: nothing may create a `ShieldPanel` at runtime; the scene is the only
  source. `FieldTester` is not modified — it already publishes the absorb events, and
  giving it knowledge of the panel would re-split the authority fork L just merged.
- Shared paths: the scene load path and the editor script write the same objects, because
  the script is what writes the scene.
- Deletion line: the editor script, before the commit closes.

**Verification:**

- builds: project compiles; the scene opens with no missing-script warnings.
- test: a batchmode headless open of `FieldShieldTest.unity` asserting that exactly one
  `ShieldPanel` exists, that its `sim` and `panelShader` are non-null, and that the field
  object's presenter resolves to it.
- negative: `rg "ShieldPanelTester" Assets` is empty.
- operator: enter play mode in `FieldShieldTest`; the panel materialises as a lattice
  filling the field silhouette; clicking the field produces a visible stress wave
  travelling out from the click and reflecting off the rim; the properties panel is
  unchanged. Whether anything shatters is Cut 4.

## Cut 4. Make a hit actually break the panel

- **Repo/branch:** same. Depends on Cut 3 and on ruling Q1.
- **Blocked:** this cut is a tuning and mechanism pass, not a wiring pass. §1.8 shows no
  energy fractures the panel at the supplied defaults. Do not tune by eye first; the two
  mechanism defects below bound what tuning can reach.

**Per-file changes:**

- `ShieldSim.compute:226-232` — `u` is clamped to `±_StressClamp` while `v` is not. A hard
  hit drives `v` far past what the clamp permits, `u` pins at `-_StressClamp`, and the
  field never swings back into tension: that is the collapse above energy 60 in §1.8, and
  why 3500 behaves like 120. Clamp `v` as well, or normalise the injection so energy maps
  to a bounded velocity.
- `ShieldPanel.cs:301` and `:359` — `_Dt` is set to `fixedSubstepDt` before the injection
  loop and not restored until after `KFracture` has been dispatched, so `KFracture`
  integrates damage and temper erosion with a substep `dt` while running **once per
  frame**. Damage accumulates roughly six times slower than the wave it is reading
  (`substeps = 6`). Either dispatch `KFracture` inside the substep loop, or hand it the
  frame `dt`. The first is physically honest and costs five more dispatches per frame; the
  second is one line. Pick one and say which in the commit — this is the "which clock owns
  fracture" question, not a constant.
- `ShieldPanel.cs:34,53,56` and the presenter's `EnergyScale` — the tuning itself, once
  the two above are settled.

**Verification:**

- test: a batchmode probe that sweeps energy across the authored range, reads `bState`
  back, and asserts that the weak energy leaves `broken == 0` while the strong energy
  breaks a bounded, non-zero fraction of cells — the same readback shape as §1.8, which is
  cheap and already written once.
- negative: no energy in the authored range leaves `maxPeakTension == 0` with
  `minTemper == temperInit`, the saturation signature.
- operator: strong clicks dice the panel; repeated weak clicks eventually break through
  (the temper mechanism at `ShieldSim.compute:286-298` is the whole point of the design and
  is untested until a break is reachable).

## 5. Defects in the supplied code

Confirmed by compiling and running them, not by reading. D1-D4 are fatal; the cut does not
exist without them.

| # | Where | Finding |
| --- | --- | --- |
| D1 | `files/TilingBuilder.cs:214` | CS0136: `dist` shadows the `dist` at `:225`. The file does not compile. **(probe)** |
| D2 | `files/ShieldPanel.cs:410,415,420` | `RenderParams.shaderPass` does not exist in 6000.3.24f1. **(probe)** |
| D3 | `files/TilingBuilder.cs:28` vs `:12-27` | `Stride = 4 * 16` but the struct is 15 floats; `SetData` throws on the stride mismatch. **(probe)** |
| D4 | `files/ShieldCommon.hlsl:18` | `centroid` is reserved in the compute compiler; `ShieldSim.compute` does not compile. **(probe)** |
| D5 | `files/ShieldPanel.cs:411-421` + probe §1.4 | Pass selection does not exist at this API, so the three per-pass args buffers cannot work as designed, independent of D2. |
| D6 | `files/ShieldSim.compute:38` | Comment says `_BreakBudget` is a "single element"; the C# allocates two (`ShieldPanel.cs:162`) and `:304` reads index 1. **Not a bug — a lying comment.** The earlier reading of this as an overrun is refuted. |
| D7 | `files/Tilings.cs:302-333` | `VoronoiCells` is O(n² log n): every site scans every other site and sorts the whole list, then `Voronoi` runs it `lloydPasses + 1` times (4 for `TilingKind.Voronoi`). Confirmed by reading; not a blocker for the Hex/Penrose default, a real wait at a few thousand sites. Use a partial selection (`nth_element`-style) or reuse the grid `ChaosGame` already builds at `:212`. |
| D8 | `files/Tilings.cs:286-288` | `Vector2.zero` is both the "invalid cell" sentinel and a legal centroid, so a cell centred on the origin is silently refused relaxation. Use a nullable or a parallel validity flag. `idx` at `:292,297` is dead. |
| D9 | `files/ShieldSim.compute:226-232` | `u` is clamped, `v` is not; hard hits pin the field in compression and no tension ever reflects. The measured collapse in §1.8. **(probe)** |
| D10 | `files/ShieldPanel.cs:301,356,359` | `KFracture` runs once per frame but integrates with the substep `dt`, so damage and temper erosion accrue ~6× slow. |
| D11 | `files/ShieldSim.compute:326-330` | `_V[e.nbr] +=` from many threads is a non-atomic read-modify-write; the cascade is nondeterministic run to run. Acceptable for an effect, worth knowing before anyone tries to write a test that pins an exact shard count. |
| D12 | `files/ShieldPanel.cs:296-298` | A two-element `uint[]` is allocated every frame for the break budget. Cache it. |
| D13 | `files/ShieldPanel.cs:9` | `[ExecuteAlways]` runs the full simulation, all dispatches and all draws every editor frame, and rebuilds the tiling on every domain reload. See Q5. |
| D14 | `files/ShieldPanel.cs:426-440` | `ReleaseAll` does not clear `pending`, so hits queued before a rebuild are injected into the new tiling. Harmless today; wrong in principle. |
| D15 | `files/ShieldPanel.shader` Conduit pass | `Warning 172: use of potentially uninitialized variable (BuildLine)`. Warning only. **(probe)** |
| D16 | `files/Tilings.cs:91-116` | The TriHex interstitial triangles are placed by a separate polar construction rather than from the hex corners they should share, so welding is not guaranteed and the dual graph can come out with spurious free surfaces. Untested; `Hex` and `PenroseP3` are the safe defaults until someone looks. |

Buffer lifetime, `SetCounterValue` and `CopyCount` were checked specifically and are
**correct**: `bVisible` is created `Append | Structured`
(`files/ShieldPanel.cs:164`), reset at `:366` before the dispatch that appends, copied at
`:369` into offset `sizeof(uint)` — which is `instanceCount` in
`GraphicsBuffer.IndirectDrawArgs` — and every buffer is released by `ReleaseAll` on both
`OnDisable` and the head of `Rebuild`. The probe's readback of
`argsFill instanceCount=253` against `cells=253` confirms the counter path end to end.
**(probe)**

Per-frame cost, measured shape: 253 cells → 1 inject dispatch per queued hit, 12 dispatches
for six wave substeps plus their echoes, 1 fracture, 1 update, 1 `CopyCount`, 1 buffer
upload, 1 indirect draw. At the scene's panel radius of 10 with `cellSize 0.35` expect
roughly four times the cells and the same dispatch count. Nothing here is close to a
budget problem in a test scene; the only per-frame CPU waste is D12.

## 6. Subtraction ledger

| Cut | Removed | Added | Deps/targets |
| --- | --- | --- | --- |
| 1 | `ShieldPanelTester` (~38 lines), 2 args buffers, 3 draw bools, 2 draw calls, `ExecuteAlways` | 6 files (~5.4k lines) | none added; no package, no pipeline, no asmdef |
| 2 | — | 1 file (~35 lines) | none |
| 3 | the one-shot editor script, in the same commit | 1 scene object, 1 component instance | none |
| 4 | — | ~5 lines changed | none |

Net: strongly additive, and it should be. The capability bought is a simulation that does
not exist in the tree in any form; nothing currently in the repo can be deleted in exchange
for it, and the only existing machine it might replace — `FieldDriver`'s absorb visual — is
the operator's call in Q2, not a subtraction this map may assume.

## 7. Operator questions

Most blocking first.

**Q1 — Does the first landing have to fracture?** §1.8 says no energy breaks a cell at the
supplied defaults, and two mechanism defects (D9, D10) bound what tuning alone can reach.
A: land Cuts 1-3 now, ship a visibly-materialising panel with a real stress wave and no
fracture, and take Cut 4 as its own pass with its own probe. B: hold the whole thing until
Cut 4 lands too, so the first thing seen is the effect as designed. **Recommended: A.**
Wiring is the thing ruled on, it is verifiable on its own, and separating it means the
tuning pass gets judged against a rig that already works instead of against itself. The
cost is one operator look that ends in "nothing broke," which is the honest state.

**Q2 — Does the panel replace the field shield's absorb visual or sit alongside it?**
`FieldDriver.Absorb` (`FieldDriver.cs:158`) feeds the existing ellipsoid ripple, and the
binder subscribes every `IAbsorbPresenter` it finds, so both can run from the same event
with no code change. A: both, for comparison. B: panel only — remove `IAbsorbPresenter`
from `FieldDriver` and let its hit buffer machinery (`:76,105-132,204-217`) go with it.
**Recommended: A for the first landing, then B as a deliberate deletion once the panel has
been looked at.** Keeping both permanently would be two absorb visuals with no rule about
which owns the read; that is exactly the split this map should not create.

**Q3 — Which fracture pattern does each damage type get, and where is it authored?** Six
of each, no natural ordering. Proposed table: Kinetic→Radial, Optical→Lance,
Electric→Star, Thermal→Crystal, Corrosive→Fizzle, Ionizing→Spiral. Authoring: A: a
serialized `FracturePattern[]` on the presenter, indexed by `(int)DamageType`. B: a switch
in code. C: gameplay settings or item data. **Recommended: A.** It is presentation choice,
it belongs with the presentation, and the operator can retune it without a compile. C is
wrong today: the absorb capability owns no data yet, and `CapabilityEvents.cs:17-20`
explicitly parks that.

**Q4 — How is the scene edited?** A: a one-shot editor script run in batchmode and deleted
in the same commit. B: hand-edited scene YAML. C: the operator clicks it together in the
editor. **Recommended: A.** B is the failure mode that produced `1428ada3`; C costs the
operator's hands for something deterministic. If the operator would rather keep the script
as a menu item, say so — that turns it into a standing second authority over the rig and
should be a deliberate choice, not a leftover.

**Q5 — `[ExecuteAlways]`?** A: drop it; the panel is inert until play mode. B: keep it, so
the lattice is visible while authoring the scene. **Recommended: A**, with `Rebuild` kept
on the context menu so a deliberate editor preview is still one click. Keeping it runs a
full GPU simulation in every open scene view forever, for authoring convenience that a
context menu already provides.

**Q6 — Per-pass draw toggles?** The supplied `drawFill`/`drawOutline`/`drawConduits` bools
die with the single-call draw (probe §1.5). A: gone. B: back as shader uniforms that zero
each pass's alpha. **Recommended: A**, until someone wants them; B is two minutes' work
the day that happens, and the toggles have no consumer today.

**Q7 — Buy NUnit coverage with an asmdef?** `Tests.asmdef` cannot reference
`Assembly-CSharp`, so no unit test can see the presenter as mapped. A: no asmdef; pin the
rule with the batchmode probe in Cut 2, which checks the whole path from published event
to GPU state. B: add `Aetheria.ShieldField.asmdef` over the six files plus the presenter,
referencing `Aetheria.Shared.Unity`, and let `Tests` reference it. **Recommended: A.** B
buys a test of the arithmetic in a one-line method while adding a compile boundary and a
reference edge to a project that has two test files total; the probe checks the thing that
would actually break, at the layer it would break.

**Q8 — Which tiling is the default?** The component ships `PenroseP3` with
`mergeRhombs`. `Hex` is what the probe exercised and the only one whose adjacency was
verified. `Voronoi` costs D7 at init and `TriHex` carries D16. A: Penrose, as authored. B:
Hex for the first landing, Penrose once the rig is looked at. **Recommended: B**, then move
to whatever looks right — this is a look-at-it decision, not a correctness one.
