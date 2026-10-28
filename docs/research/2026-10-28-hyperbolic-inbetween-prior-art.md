# Hyperbolic in-between space: prior art and Aetheria's Body

Serves the operator's musing about hyperbolic inter-zone travel (Flatland zone links, in-between place as H², danger = getting lost), 2026-10-28. Destined for Aetheria `docs/research/`. Facts only; no conclusions or recommendations.

Marks: [V] verified in the source text I read (PDF text, raw source, or git). [S] summary or secondary (search-result snippet, or a WebFetch summary of a page, or a source I did not read in full). [M] my own derivation or standard-math restatement, not read in a source.

Reading ledger: PDFs read via pdftotext: Kopczyński and Celińska-Kopczyńska 2020 (arXiv 2002.09533), Bridges 2017 HyperRogue paper, Cariñena-Rañada-Santander 2005 (arXiv math-ph/0504016), Coulon et al. (arXiv 2010.15801, selected passages), Skrodzki survey (arXiv 2008.01363), HyperEngine.pdf. HyperRogue C++ source read from raw.githubusercontent.com/zenorogue/hyperrogue/master/ on 2026-10-28 (a snapshot; line numbers are that snapshot's). Not read: Hyperbolica devlog videos, Zeno's blog, Diacu's equations, the Ballesteros et al. Bertrand papers, the Child et al. paper body.

Existing note (do not redo): `F:\Projects\Aetheria` origin/master `docs/research/gravity-tail-taming.md`, Question 2.
- 0.1 [V] Line 37: on H² (K=-1) the Green's function is (1/2π)·ln coth(r/2); flux through a geodesic circle is the enclosed strength; circumference 2π·sinh r; field c_enc/sinh r; the 1/sinh factor is common to parent and enclosed siblings. Caveat in the same line: Aetheria positions are Euclidean, so the kernel on flat distances is not harmonic (screening halo, global length). Line 89: "Hyperbolic: −c·ln coth(r/2ℓ)". Line 75: probe [10].

---

## 1. Rendering H²

1.1 [V] Kopczyński & Celińska-Kopczyńska, "Real-Time Visualization in Non-Isotropic Geometries", arXiv 2002.09533, §1: Fig. 1 shows the binary tiling of H² in Poincaré disk, Beltrami-Klein, Poincaré half-plane and horospherical projections. The text says there is a trade-off between comprehensibility and the ease of finding the shortest straight line; half-plane and horocyclic projections "seem easier to understand" but misperceive a straight line as the shortest route; geodesic-based azimuthal projections make the shortest path easy. https://arxiv.org/pdf/2002.09533

1.2 [V] Same paper §3.1 "Homogeneous coordinates": hyperbolic translation along axis 1 is the matrix [[cosh x, sinh x],[sinh x, cosh x]] acting on (x1, x_{n+1}); rotations about the origin are the same matrices in all three isotropic geometries. The authors write that beginners tend to use the Poincaré model, and that the Minkowski hyperboloid model "is easier to understand by analogy to spherical geometry and also due to its much better numerical properties".

1.3 [S] HyperRogue models page (WebFetch summary): default screen model is the Poincaré disk (page gives no stated reason); disk models are all perspective projections of the hyperboloid (Poincaré, Beltrami-Klein, Gans orthogonal, inverted Poincaré); half-plane (conformal, "poor for playing" but useful for horocycle/equidistant lands); band (conformal, hyperbolic Mercator, one line maps isometrically; restricted to game-over or cheat mode because it could be used to cheat); spiral, polygonal, Joukowsky, formula, azimuthal equidistant/equal-area, cylindrical, pseudocylindrical, two-point equidistant, Hypersian Rug (embedded surfaces), paper model, Poincaré ball, fisheye. The hyperboloid is "used internally" for computation. https://roguetemple.com/z/hyper/models.php

1.4 [V] Kopczyński, Celińska, Čtrnáct, "HyperRogue: Playing with Hyperbolic Geometry", Bridges 2017, section "Models of Hyperbolic Geometry": "The game displays its world in the Poincaré model by default." Minkowski hyperboloid is "the model of hyperbolic geometry used internally by HyperRogue". Poincaré, Beltrami-Klein and Gans are obtained by viewing the hyperboloid from (0,0,-p) with p = 1, 0, and infinity respectively. Band model: a chosen line maps isometrically, so the player's whole path can be seen as one straight band; half-plane with L as a vertical line lets the whole game be seen by zooming. Hypersian Rug: approximate isometric embedding in R³ (crochet-like). https://archive.bridgesmathart.org/2017/bridges2017-9.pdf

1.5 [V] HyperRogue renders in GLSL on the GPU with hyperboloid-coordinate vertices: `shaders.cpp` lines 140-162 (`rel_log` using asinh, sqrt of Minkowski norm), 304-310 (band model shader: asinh, atan(tanh)), 437 and 477 (acosh), 392-393, 520-536 (sinh/acosh in fog/distance code). Line 477 sets the distance function to `acosh(t[3])`. Also `glhr.cpp` 308 and 678-681 bind an inverse-exp lookup 3D texture (`tInvExpTable`). https://raw.githubusercontent.com/zenorogue/hyperrogue/master/shaders.cpp

1.6 [M] On the hyperboloid z² − x² − y² = 1 the geodesic distance between points p, q is acosh(z_p z_q − x_p x_q − y_p y_q); the Poincaré disk point is (x, y)/(1+z). Standard identities; not taken from one cited page. The shader evidence for the acosh distance function is 1.5.

1.7 [S] Child, Osudin, He, "Rendering Non-Euclidean Space in Real-Time Using Spherical and Hyperbolic Trigonometry", ICCS 2019 pp. 543-550: 2D spherical and hyperbolic space; object parameters stored in polar coordinates; azimuthal equidistant projection onto the screen. Abstract only, via https://openaccess.city.ac.uk/id/eprint/22608

1.8 [V] Coulon, Matsumoto, Segerman, Trettel, "Ray-marching Thurston geometries", arXiv 2010.15801: ray-marching framework for all eight Thurston geometries, built on geodesic flow; contents list "8.1 Models of S2 and H2"; §2.4.1: in some models coordinates grow exponentially with distance so floats lose precision (listed for H³, H²×E, Sol, SL(2,R)), mitigable by choice of model. The paper's related-work section says HyperRogue uses a triangle-rasterization strategy and restricts what the viewer can see. https://arxiv.org/pdf/2010.15801

1.9 [V] HyperEngine (CodeParade, Hyperbolica backend), `HyperEngine.pdf`: coordinate systems are Unit (tile -1..1), Klein, Poincaré ("primary coordinate system, and the one used in GyroVectors"), and Tile strings; every object needs a `HyperObject`; shaders set per renderer by a `SetTextures` component; "Forced Euclidean objects" exist for UI. https://github.com/HackerPoet/HyperEngine (README states it is not the complete Hyperbolica source and lists devlogs #1-#6, #3 "Rendering Hyperbolic Spaces", #4 "Projecting Space").

1.10 [S] Hyperbolica devlog videos: titles only, not watched or transcribed. https://github.com/HackerPoet/HyperEngine (README)

1.11 [V] Kopczyński 2020 §1 says that for 3D geometries the projection trade-off is mitigated by first-person perspective (render what an observer inside would see; light along geodesics). Same paper reports its renderer methods outperform ray marching for shapes generated in a complex way (3D models).

## 2. Motion and numerics

2.1 [V] Kopczyński 2020 §3.1: "it is easier to move and rotate the world than to move and rotate the camera"; in isotropic geometries the world can be moved; camera forward motion = follow the geodesic, parallel-transport the frame, so the front direction stays forward.

2.2 [V] HyperRogue `hyperpoint.cpp`: `lorentz(a,b,v)` at line 698 builds a cosh/sinh boost in coordinates a,b; `rgpushxto0(H)` at 1057 "a translation matrix which takes 0 to H"; `cspin` at 691 for rotation. https://raw.githubusercontent.com/zenorogue/hyperrogue/master/hyperpoint.cpp

2.3 [V] Same file lines 1065-1115: `fixmatrix(T)` with the comment "The nature of hyperbolic geometry makes the computations numerically unstable. The numerical errors tend to accumulate, eventually destroying the projection. This function fixes this problem by replacing T with a 'correct' isometry." It calls `orthonormalize(T)`, a Gram-Schmidt-style loop with the metric signature `sig(z)` (lines 1103-1112); Euclidean has `fixmatrix_euclid`; product geometries rescale by exp(-z) first.

2.4 [V] My grep of the other downloaded HyperRogue files (cell.cpp, graph.cpp, game.cpp, models.cpp, heptagon.cpp, hyper.cpp, drawing.cpp, glhr.cpp, shaders.cpp) found no `fixmatrix` call. Those are a subset of the repo, so call sites are not established.

2.5 [V] Kopczyński 2020 §3.2: tessellations serve as landmarks, and "due to the exponential expansion of Hd ... representations based on floating point coordinates are very prone to precision errors. Tessellations can be represented in a discrete way, providing a way to avoid this issue."

2.6 [V] HyperEngine.pdf: `HighPrecision` flag on a HyperObject uses double precision, "only recommended for moving objects very far from the center of the map"; tile coordinates are pre-generated offline in a lookup table "allows double-precision computations and fast placement for large maps"; `AllowCulling` disables objects beyond a culling radius.

2.7 [V] HyperEngine.pdf: a `GyroVector` is "an isometry of non-euclidean space that represent an object's location and rotation", composed with the `+` operator; "function very similarly to Unity's Transform". No gyrovector literature (Ungar) read by me.

2.8 [V] Cariñena, Rañada, Santander, J. Math. Phys. 46, 052702 (2005), arXiv math-ph/0504016: Kepler problem solved on S² and H² with curvature κ as a parameter. Reduced radial effective potential W_κ(r) = −k/T_κ(r) + J²/(2 S_κ²(r)) with T, S = tan/sin (κ=1), r (κ=0), tanh/sinh (κ=-1) (PDF text lines 440-446; the fraction is garbled in extraction). https://arxiv.org/pdf/math-ph/0504016

2.9 [V] Same paper, H² case (lines ~495-506, 799-830): if J²/k < 1 the motion is bounded and periodic for energies W_min ≤ E < −k and unbounded for E ≥ −k; if J²/k ≥ 1 all trajectories are unbounded open curves; a circular orbit exists at tanh r = J²/k with J bounded above (in the Euclidean case J is not bounded for circular orbits); bounded orbits are "hyperbolic ellipses" with a focus at the potential origin; the abstract also covers conics on H² and orbits via a curvature-dependent Binet equation.

2.10 [M] Differentiating W_{-1}'s potential −k·coth r gives a force k/sinh² r. The Aetheria note's Gauss-law field on H² (0.1) is c/sinh r. These are two different radial laws; I read the first in 2.8 only as a potential with the stated sign convention and did not verify the force myself against the paper's own text.

2.11 [S] Bertrand's theorem on curved space: Ballesteros, Enciso, Herranz, Ragnisco (arXiv 0803.3430; Commun. Math. Phys. 2009, s00220-009-0793-5; arXiv 1304.4544) — search snippets say only Kepler-type and oscillator-type systems satisfy the Bertrand conditions on spherically symmetric curved spaces, and that the Kepler and oscillator potentials exist on the three constant-curvature spaces including hyperbolic. I did not read these papers. https://arxiv.org/pdf/0803.3430

2.12 [S] Diacu, Pérez-Chavela, Reyes Victoria, "An intrinsic approach in the curved n-body problem: the negative curvature case", arXiv 1109.2652: n bodies on the hyperbolic sphere; equations in the Poincaré disk (elliptic relative equilibria) and upper half-plane (hyperbolic, parabolic); the abstract does not state the potential's form. A search summary calls the interaction a cotangent potential (cot, coth) in the 3D case; unconfirmed for 2D. Related: Alhowaity-Diacu-Pérez-Chavela on kite-shaped relative equilibria in the hyperbolic case. https://arxiv.org/abs/1109.2652

2.13 [M] In the hyperboloid model a body's state is a Lorentz-group element plus a tangent velocity; a geodesic is exp of a tangent vector (cosh, sinh); this is the same structure Kopczyński 2020 uses for camera motion (2.1). Not read as a game-physics source; no source found that integrates Newtonian motion under a potential on H² for a game.

## 3. Variable curvature

3.1 [V] Coulon et al. §1.3: "A numerical integration approach is unavoidable in some cases, for example in generic inhomogeneous geometries"; such algorithms "must take many steps along each ray to maintain accuracy, and so may be slow", acceptable for "dense" scenes; for scenes where rays travel large distances accuracy is lost (sentence continues past my excerpt). The same section lists others' integrators for geodesic rays: Berger RK4; HyperRogue RK4 plus lookup tables (for the non-isotropic geometries); Novello et al. Euler; MagmaMcFry second-order RK. https://arxiv.org/pdf/2010.15801

3.2 [V] Kopczyński 2020 §3.3.2-3.3.3: Sol geodesics have no simple closed form; exp is found by solving the geodesic equation, log by an iterative method that can return a non-shortest geodesic; a family of Sol-like metrics g11 = exp(a1 z), g22 = exp(a2 z) is covered. These geometries are homogeneous, not variable-curvature.

3.3 [S] HyperRogue and RogueViz: search found no variable-curvature or mixed-curvature region feature. HyperRogue offers uniform tilings with different curvature per game setting (heptagon/hexagon/pentagon tilings, grids added in release 111) and non-isotropic geometries. I did not read the RogueViz source for this. https://zenorogue.itch.io/hyperrogue/devlog/86195/hyperrogue-111-released ; Bridges paper §"Models" and later text.

3.4 [S] No game or demo with ray-integrated variable curvature or piecewise constant-curvature gluing was found in my searches. Candidates surfaced only as papers: arXiv 2504.14598 (light tracing on curved surfaces with a transformation medium; not read), creeping-wave ray tracing on NURBS with Runge-Kutta geodesics (title only).

3.5 [M] For a 2D metric written conformally in the screen coordinates, g = e^{2φ(x,y)}(dx²+dy²), Gaussian curvature is K = −e^{−2φ}Δφ; the Poincaré disk is φ = ln(2/(1−r²)) with K = −1. Standard identity, not read in a source. Geodesics of a conformal metric satisfy a 2D ODE with Christoffel symbols from ∇φ.

## 4. Content scaffolding

4.1 [V] Bridges 2017 §"What Can We Learn": about 50 lands each with a theme; "Regular straight lines are usually employed as borders between the lands", curves of large radius (circles, equidistants, horocycles) in some cases; Camelot is a large circle without markings; in Temple of Cthulhu every sixth horocycle is marked and descent feels endless; Ivory Tower uses equidistant levels with artificial gravity pushing creatures to the bordering line. (The roguetemple site text found by search says 72 lands now; Bridges says about 50.)

4.2 [V] Same paper: tile counts at distance d from a heptagon 1, 7, 14, 28, 49, 84, 147, 252, 434, 749...; recurrence a_{d+4} = a_{d+3} + a_{d+2} + a_{d+1} − a_d for d ≥ 5; growth ratio λ ≈ 1.72208; the basic unit triangle gives a hexagon:heptagon ratio of 7:3 in any pattern that tiles the plane. (The text names no Schläfli symbol; {7,3} is not stated in what I read.)

4.3 [V] Same paper, generators: periodic "encodings" (e.g. Zebra, Windy Plains from one encoding; Palace from circles of radius 2; Emerald), "fractal landscapes" via a hyperbolic-tiling analogue of diamond-square (stated as an efficient algorithm, ref [19]) used for chasms and for Galápagos environmental factors, and combinations (Reptiles).

4.4 [V] Same paper: Galápagos tiles carry 21 binary environmental factors that vary smoothly; matching a baby tortoise to an adult is easy in H², hopeless in Euclidean space.

4.5 [V] Consistency of far content: Kopczyński 2020 §3.2 (discrete tessellation avoids float drift, 2.5) and HyperEngine's offline double-precision tile lookup (2.6). How HyperRogue lazily generates distant cells, and where it re-centers the view matrix, was not established: `game.cpp:943, 1755` show a `centerover` cell and `View` matrix exist, nothing more read.

## 5. Getting lost as play

5.1 [V] Bridges 2017: players "quickly learn that it is practically impossible to reach a location they have been before, unless some method of marking the way has been used"; two reasons: exponential tile count and curvature — A→B→C→A leaves the map rotated because the exterior angles of the triangle do not sum to 360°. Yendor Quest requires returning to a place 100 steps away (about 100 possible directions); on a large-radius curve a promising-looking direction returns you to the edge; going back toward the centre of such a curve is when you do return; path stays close to a straight line except backtracking.

5.2 [V] Same paper: Camelot (unmarked large circle) and Yendor Quest "would be trivial in Euclidean"; marked concentric horocycles make direction easy in Temple of Cthulhu.

5.3 [V] Skrodzki, "Illustrations of non-Euclidean geometry in virtual reality", arXiv 2008.01363 (a survey; authored by Skrodzki, not Kopczyński): in VR H³ and H²×E, rays diverge ("geodesic divergence"), distant objects need squinting, so "all objects appear to be relatively close by" (quoting Hart et al.); software also renders in a Euclidean model to preserve depth cues, at a cost; footnote 36: in the {4,3,6} cubical tiling, a loop of six "turn 90° and walk to the next cell centre" moves returns the virtual user to the start cell while the physical body is two squares away. Hart et al. 2017 are cited, not read. https://arxiv.org/pdf/2008.01363

5.4 [V] Kopczyński 2020 §3.2: tessellations "can be used as landmarks or milestones to help the observer navigate and measure the space"; §1 says non-Euclidean geometries "may be perceived as unnatural and confusing to navigate".

5.5 [S] Hyperbolica reception: an aggregator (vaporlens.app) lists motion sickness, vertigo and nausea as reported complaints; an essay at archania.org argues a flat minimap would be misleading and the game pushes landmark/route knowledge (interpretive, not a player study). No formal player-experience study of getting lost in Hyperbolica or HyperRogue was found. https://vaporlens.app/app/1256230/hyperbolica ; https://archania.org/p/the-symbolic-world/symbolic-works/video-games/hyperbolica

5.6 [S] HyperRogue 1.11 devlog (first-person mode): per search summary it has an ASCII minimap and notes hyperbolic stars are visible only from a small part of the world, so star navigation doesn't work. Not read in full. https://zenorogue.itch.io/hyperrogue/devlog/86195/hyperrogue-111-released

## 6. Aetheria's Body (origin/master, read with git show; fetched 2026-10-28)

All paths under `F:\Projects\Aetheria`.

6.1 [V] `Assets/Shaders/Gravity.cginc` is generated by `Assets/Scripts/ServerShared/GravityShader.cs` (header line 4; generator template lines 31-129; class doc lines 4-11; `GravityShaderTests` fails on any difference, `tests/Aetheria.Shared.Tests/GravityShaderTests.cs`).

6.2 [V] `Gravity.cginc:10-18` `GravityTerm { float2 position; float strength; float softening; float inner; float outer; float reference; }`; `:20` `StructuredBuffer<GravityTerm> _GravityWells`; `:21-22` `_GravityWellCount`, `_HeightExaggeration`.

6.3 [V] `Gravity.cginc:83-94` `gravityHeight(float2 p)`: loops terms, `float r = length(p - term.position)` (line 89, Euclidean distance), skips terms with `r >= term.outer` (line 90), sums `strength * gravityTermPotential(...) - reference`, divides by exaggeration (line 93). `gravityTermPotential` (lines 68-80) takes only r: global term `0.5*log(s+e2)` (line 72, 2D log potential), band terms from a smoothstep-profile closed form or series (lines 29-65).

6.4 [V] `Assets/Shaders/Brushes/GravityField.shader:1-3` (depth = −Zone.GetHeight at world xz per texel), `:7` `Blend One One`, `:28` `float2 world`, `:35` `o.world = mul(unity_ObjectToWorld, v.vertex).xz`, `:41` `return (-gravityHeight(i.world)).xxxx`. The gravity field is a closed-form sum per texel over the term buffer, drawn by one quad; it is not ray-marched.

6.5 [V] `Assets/Scripts/Zone Display/GravityLowering.cs`: `:13` `FieldLayer = 9`, `:15` `FieldExtent = 100000f`, `:17-27` `TermRecord` with `Vector2 Position`, `:40-70` `Lower` fills `ComputeBuffer` per frame from `zone.Wells.Terms`; `:54` term position is `DrawAhead.Orbit(zone, term.Node, lead)` or `zone.Wells.Centre`; `:65-69` sets globals `_GravityWells`, `_GravityWellCount`, `_HeightExaggeration`; `:73-84` `CreateField`: one Quad, `rotation = Quaternion.Euler(90f,0f,0f)` (flat in xz at y 0), `localScale = FieldExtent`.

6.6 [V] Cameras: `Assets/Scripts/Editor/GravityFieldParityVerify.cs:194-207` renders the field through a downward orthographic camera that culls layer 9 (`camera.orthographic = true; orthographicSize = half`); `:377` names the targets "Fog Surface Displacement", "Slime Gravity", "MinimapGravity". The header comment of `GravityField.shader` names Surface Displacement, Slime Gravity and both Map Gravity cameras. The production camera setup lives in scene/prefab assets, not read. `AmbientEdgeVerify.cs:193-206` likewise uses a downward orthographic camera ("as the Tint cameras see it").

6.7 [V] Authoritative CPU law: `Assets/Scripts/ServerShared/Zone.cs:785-789` `GetHeight(float2 position, double time) => Wells.Potential(position, _orbitPositionAt, time) / Settings.HeightExaggeration`; `:808-811` `GetForce(float2 position) => Wells.Force(...)`, "the gradient of the potential GetHeight draws"; `:13` `using float2 = CultMath.float2`; `:482` world positions are 3D with the plane at (x, z).

6.8 [V] `Assets/Scripts/ServerShared/WellTree.cs:360-378` `Force`: `toZone = Centre - p`, `force += toZone / rz * PullOf(...)`; `ForceFrom` recursion uses `positionAt(_key[node], time) - p`, `length(offset)`, `offset / r * PullOf(node, r)`, and skips a subtree when `length(positionAt(_key[kid], time) - p) < _reach[kid]` fails (line 377, Euclidean reach test). `:381` `Potential` walks the same way. `:13-24` `WellNode` has `float2 Anchor`, float Distance/Phase/Mass/Extent; `:83` `float2 Centre`.

6.9 [V] `Assets/Scripts/ServerShared/GravityField.cs:89-90`: pull is `r * Profile(inner, outer, s) / (s + softening²)` with `s = r*r` (a function of r only); `:94-` `Potential(softening, inner, outer, r)`.

6.10 [V] Orbit rails are Euclidean offsets: `Zone.cs:231-234` node anchor `data.FixedPosition + OrbitData.Evaluate(frac(data.Phase)) * data.Distance`; `:565-625` `GetOrbitPosition`, `OrbitPositionAt` return `float2`; `:739` `GetSecurityLevel(float2 pos)`; `:791-802` `GetLight` uses `length(position - sun.Orbit.Position)`.

6.11 [V] Bodies move by Euclidean integration: `Assets/Scripts/ServerShared/FloatingBodies.cs:25-41` `KinematicBody { float2 Position, Velocity, Drift }` with `Drift += force * s.FloatingBodyGravity * dt; Position += (Velocity + Drift) * dt`; `Zone.cs:511, 537, 551` call `Body.Step(deltaTime, GetForce(body.Position), settings)` for floating items, clouds, mines. Ship integration was not read.

6.12 [V] The raymarching shaders are volumetric and 3D-Euclidean, separate from the gravity field: `Assets/Shaders/Raymarching/CloudShader.shader:114-120` marches `rayPos = startPos + dir * raymarchDistance`; `VolumeSun.shader:116` `elevation = length(rayPos)`; `VolumeJupiter.shader` also present. Other consumers of the gravity terms: `Zone.Flow` takes `GetForce(position.xz)` (`Zone.cs:815-816`).

6.13 [V] Files referencing the gravity lowering (git grep): Editor/AmbientEdgeVerify.cs, Editor/GravityFieldParityVerify.cs, ServerShared/{GravityField, GravityShader, Settings, WellTree, Zone, ZoneGenerator}.cs, Zone Display/{GravityLowering, ZoneRenderer}.cs, Shaders/Gravity.cginc, Shaders/Brushes/GravityField.shader.
