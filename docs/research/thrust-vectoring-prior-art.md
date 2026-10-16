# Prior art: control allocation with tilting / gimballed thrusters

Serves the fork: vectored thruster rows in Aetheria (follow-ups `aetheria-release:follow_up:vectored-thruster-rows`, `aetheria-release:follow_up:gimballed-thrusters`). A thruster row tilts its thrust vector within a gimbal limit at a slew rate; the thrust allocator chooses the tilt; one gimbal per row, not per thruster.

Eyes pass, 2026-10-10, session self-2026-10-10-ag. Facts with evidence pointers. No recommendation. Anything inferred is marked "(inferred)". Items marked [not retrieved] were seen only through search snippets or citations in other papers, not read in full.

## 0. The allocator today (the thing the prior art is compared against)

Source: `Assets/Scripts/ServerShared/ThrustAllocator.cs` at HEAD `1fc98bdc` of branch `eureka/aetheria-release-thrust-allocator-core` (read via `git show`).

- Input: `ReadOnlySpan<float3> columns` (x starboard acceleration, y forward acceleration, z clockwise yaw rate per unit throttle), `float2 move`, `float turn`. Output: one throttle per thruster in [0,1].
- One `BoundedLeastSquares.Solve` per call over n throttles plus 4 error variables (unwanted/missing translation per half-axis, linear cost). Yaw row weight 1000, translation rows weight 100, ridge 0.1 on each throttle. Variable bounds: throttles in [0,1], errors in [0, 2].
- Columns are fixed inputs: the allocation matrix is constant with respect to the unknowns, so the problem is linear in the throttles.

## 1. Question (a): standard formulations for allocation with tilting actuators

### 1.1 Virtual actuators: force components as the linear unknowns, angle and magnitude recovered afterwards

- Voliro hexacopter (Kamel, Verling et al., ETH, arXiv 1801.04581, section "D. Control allocation"). The allocation matrix is nonlinear in the tilt angles. The paper states a nonlinear least squares solve was judged too costly on the onboard computer and proposes "a variable transformation": each rotor's force is split into a vertical and a lateral component, F_v,i = c n_i^2 cos(alpha_i) and F_l,i = c n_i^2 sin(alpha_i) (eqs 16-17). The 12-vector of components maps linearly to the wrench by a constant matrix, solved by pseudo-inverse. Recovery: n_i^2 = (1/c) sqrt(F_v,i^2 + F_l,i^2) (eq 22), alpha_i = atan2(F_l,i, F_v,i) (eq 23). The paper reports several hundred Hz on a Pixhawk.
- Same technique in the marine literature, named "extended thrust configuration" (Sørdalen 1997, Control Engineering Practice 5(9):1223-1231, [not retrieved]; described in Mukwege/Nguyen/Garone arXiv 2510.08119 intro and in Maneuvering-based Dynamic Thrust Allocation, arXiv 2507.18309, section 2: "separating the force components and using the rectangular configuration matrix is convenient as this leads to a constant configuration matrix"). In 2507.18309 fixed-direction thrusters stay in polar form (magnitude) while azimuth thrusters use rectangular components, in one allocation.
- Mukwege/Nguyen/Garone (arXiv 2510.08119, sections 2-4) write the whole problem as d = M F with F in R^(3m) (three force components per thruster, M constant 6 x 3m) and treat the orientation as F_j / |F_j|.

### 1.2 Nonlinear allocation, solved by sequential local linearisation

- Johansen, Fossen, Berge, "Constrained nonlinear control allocation with singularity avoidance using sequential quadratic programming", IEEE TCST 12(1):211-216, 2004 [not retrieved; abstract and the description in 2510.08119 section 1.1 and 4]. Each sample: a convex QP approximating the nonlinear program; actuator rate and position constraints; a singularity penalty on 1/det(J(alpha) J(alpha)^T) added to the cost.
- Per 2510.08119 section 4, the penalty (i) does not work when thrusters are fewer than the degrees of freedom (det is identically zero), and (ii) gives no systematic behaviour around zero demand d = 0. The same paper names the equality constraint d = J(alpha) T and the penalty as the source of non-convexity.
- Ryll/Cuniato line and the ETH Voliro follow-ups [not retrieved in full]: a search result describes Cuniato et al. (IEEE T-RO) as a differential allocation including servo and propeller dynamics, claimed free of singularities.

### 1.3 Sequential / cascaded (forces first, then angles)

- TU Delft thesis on dual-axis tilt-rotor quadplanes (repository.tudelft.nl, search snippet only) reports a nonlinear SQP allocator and states that a cascaded scheme (linear then angular accelerations) was chosen over a combined one because of large roll and pitch changes. [not retrieved in full]
- A 2021 tilt-rotor quadrotor paper (search snippet, not retrieved) splits the effectiveness matrix into two parts (yaw torque and forward velocity first, then all dynamics) and iterates to convergence.
- Voliro's solution (1.1) is itself two-stage: linear solve for components, then closed-form angle recovery.

### 1.4 Rate and gimbal limits entering the allocation

- Per-tick bounds from current state and rate (box on the actuator variable): Tiltrotor NMPC (Nguyen/Garone et al., arXiv 2406.06130, section 4.1): "we use the current state of actuators and their rate of change to update u_min and u_max" every sample, and constrain the controller so its virtual command lies in the image h(u) of the admissible actuator box. There the unknowns are thrust and tilt angle themselves (u = thrust_i, tilt_i) with the rate-updated box; the paper's allocator in its experiments is a pseudo-inverse.
- Mukwege et al. 2510.08119 section 4: thrust saturation as F_i magnitude bound, thrust rate as |F_i - F_i0| <= bound reusing the previous solution F_i0, pointing and angular-rate constraints as convex cones C_i with F_i in C_i (conic combination of 3 vectors, or half-space form A F <= b). Resulting problem class: QCQP at most. For real time it states thrust saturation can be rewritten as three box constraints per actuator (F_x, F_y, F_z min/max), "at the cost of extra conservativeness".
- Marine DP (MARIN, search snippet [not retrieved]): physical limits modelled are saturation, azimuth turn rate and RPM change rate, solved with SQP.
- Maneuvering-based Dynamic Thrust Allocation (arXiv 2507.18309, abstract and section 3.4): a nonlinear reference filter driven by a control Lyapunov/barrier function tracks the optimal allocation with rate limitation of the thruster forces; the optimal allocation itself is computed unconstrained in the manifold p + Q*lambda (particular solution plus nullspace).
- Härkegård and active-set methods (Bodson & Frost, NASA, quoted in a search result [not retrieved]): Härkegård's active-set algorithms for constrained least-squares allocation; "Resolving actuator redundancy - optimal control vs. control allocation", Automatica 41(1):137-144, 2005 (bibliographic fact only, [not retrieved]). Dynamic allocation with rate limits as constraint on the increment is a standard formulation of this literature (inferred from the 2406.06130 and 2510.08119 descriptions; the Härkegård paper was not read).
- Voliro paper states the limitation of the linear trick directly (section IV-C text near eq 23): "the tilting angles and the angular velocities are nonlinearly coupled. Therefore, it is difficult to constrain them separately. This means, that this allocation doesn't account for the slower dynamics of the tilting angles." Example given: hovering at 90 deg roll, two rotors on the vertical axis must "constantly rotate 180 back and forth at a high rate"; tilt servos cannot follow, so forces and moments appear in directions not commanded. The authors state this motivated a further step (text beyond the retrieved excerpt).

## 2. Question (b): virtual-actuator trick inside a least-squares / QP allocator

### Bound shapes

- A real tilt limit is a cone (3D) or a wedge (2D) on the force vector: |F_lateral| <= tan(theta_max) * F_vertical, F_vertical >= 0. 2510.08119 section 4 states pointing constraints are "convex cones C_i", yielding at most a QCQP; the linear-programming-friendly approximation is a box on each component (eqs 40-42), explicitly conservative.
- In a 2D plane with a one-sided (non-reversible) thruster, the cone degenerates to two half-spaces on (F_v, F_l) (inferred from the cone statement above; Aetheria's columns are planar and throttles are in [0,1]). Whether `BoundedLeastSquares` accepts general linear inequality constraints or only variable box bounds was not examined by this pass; the allocator file shows only variable bounds [lo, hi].
- Marine azimuth with a full circle: the Valčić/Prpić-Oršić result is that removing a forbidden sector from the azimuth circle leaves a non-convex feasible region ("Pacman shape") causing large azimuth swings and slower allocation (CROSBI 899099 and related entries, search results only).

### Conditioning

- Mukwege et al. 2510.08119 section 1 and 3 state that pseudo-inverse allocations are "sensible to the singularities" and produce "discontinuous actuator orientations references"; because servos have finite turning rate, crossing a singularity leaves "a period of time where the system is not able to fulfill the demands." They note singular configurations are "also usually related to energy-efficient configurations."
- The linear virtual formulation raises the number of unknowns from one scalar per actuator to two or three (Voliro: 12 variables for 6 rotors). Over-actuation (more unknowns than DOF) means the solution is not unique (Voliro section D; 2406.06130 section 1 mention of ill-conditioned effectiveness matrix).
- 2510.08119 adds a rest-configuration nullspace term to the convex cost (q1 b(d) K_b^T F - q2 ||...||^2, eq 38) which they say preserves convexity and keeps a minimum force on each actuator; they present it as the fix for the orientation discontinuity.

### Warm start

- No source retrieved states warm-start behaviour of a bounded least-squares/QP solver specifically under virtual-actuator allocation. 2510.08119 section 4 states the rate constraint reuses the previous solution F_i0 as the centre of the rate box (so the previous solution is part of the constraint, not only an initial guess). Voliro: the pseudo-inverse is memoryless. Orbiter forum simplex thread (section 4): solve time 0-15 ms for 22 thrusters, no warm-start detail.

### Singularity near zero thrust

- Angle atan2(F_l, F_v) is undefined at F = 0 and discontinuous near it: Voliro eq 23; 2510.08119 intro and section 3 (Lipschitz constant of the orientation is bounded only if |F_j| stays above a minimum, their condition C2: |F_j + s_j(d)| >= epsilon_2, enforced by the nullspace smoothing term).
- 2510.08119 section 5 numerical examples (marine vessel with 3 azimuth thrusters; quadcopter) report orientation jumps when the pseudo-inverse solution crosses the singular point, and smoothing removing them. Their Figure 4 text: "the smoothing action appears when a discontinuity in azimuth orientation is close."
- The plain virtual-actuator formulation has the zero-thrust ambiguity because the unknown F carries the angle implicitly; the output angle command is a function of the solution. (Inferred from eqs 22-23 plus the above.)

## 3. Question (c): games

Retrieval for games was by web search of wiki and forum pages; no engine source code was read.

- Kerbal Space Program: engines have a `ModuleGimbal` with configurable range (typically a few degrees; kerbal.wiki Part_modules example LV-909 shows about 4 degrees each way in pitch and yaw), per-axis response toggles (kOS docs: gimbal responds to pitch, yaw, roll controls separately; LIMIT suffix; LOCK snaps to zero), a response-speed field whose effect is unclear (a forum thread, topic 46553, reports no observable difference between speeds 1 and 100; a reply says the speed field is used only when `useGimbalResponseSpeed` is true). Roll participation of off-axis engines was added in 0.24 (forum summary). Gimbal acts only while the engine burns; RCS and reaction wheels cover the rest (forum posts). KerbCom Avionics mod added a solver balancing engine gimbals, reaction wheels and RCS (forum announcement; algorithm not retrieved). KSP's own internal allocation was not found in any retrieved source.
- Space Engineers: without mods, thrust is applied through the grid's centre of mass so offset thrusters make no torque (Keen support thread "Realistic Thrusters // Thruster Torque & Thrust Allocation", forum answers). Vectoring is done by mounting thrusters on rotors/sub-grids and scripting (Whip's, Vector Thrust OS named by players). The "Realistic Thrusters" mod/suggestion is the only torque-from-offset treatment found.
- Avorion: Directional Thruster block with nozzles on two opposing sides, aimed by rotating the block, affecting only that axis, 2.5x thrust along it (wiki; players report 3x); regular thrusters have nozzles on all six sides; thruster output scales against ship mass; turning authority comes from distance to centre of mass (wiki, developer post). No continuous gimbal found.
- Starsector: movement stats only (wiki: acceleration, turn acceleration, max turn rate as hull-mod multipliers); flux does not gate movement. No thrust allocation.
- Children of a Dead Earth: store/feature text says moment of inertia, delta-v and acceleration are computed; searches found no description of gimbals or RCS allocation. Nothing established.
- Orbiter (simulator, not shipped game with gimbals evidence): forum "Simplex-based thruster control" (thread 25232) describes an RCS class using the simplex method on arbitrary thruster layouts "no groups", simultaneous rotation and translation, commands scaled linearly when out of reach; 0-15 ms on 22 thrusters; occasional instability with unrealistically large torques; no mention of gimbals. ESA ATV uses simplex-based jet selection precomputed into a lookup table (search result [not retrieved]).
- No retrieved source describes a shipped game doing a physically allocated vectored thrust with an optimiser. (A negative result of limited searching, not proof of absence.)

## 4. Question (d): shared-angle actuators (one gimbal per row)

- No retrieved source formulates allocation for several thrusters sharing one tilt angle with individual magnitudes. This was searched directly (control allocation, shared tilt, common tilt, group of thrusters one gimbal) and returned nothing matching.
- Nearest found: TU Delft thesis snippet on a quadcopter with a single gimbal-style common tilt (Zheng et al.): two extra servos and spherical joints tilt the whole rotor set so the combined thrust vector is steerable and thrust is attitude-independent. The retrieved excerpt does not describe the allocation. [not retrieved in full]
- Structure of the problem as stated by the established formulations (inferred): with one common angle alpha per row, row member i produces force t_i (cos alpha, sin alpha). In virtual-actuator terms every member's component pair (F_v,i, F_l,i) must have the same ratio tan(alpha). The linear-component trick's advantage (angle free per actuator, constant matrix) is lost because the ratio equality across members is bilinear in (t_i, alpha); the formulation with per-thruster free components would overcount freedom. If the row's members have equal nominal directions, the row's total force and torque are (sum of t_i) and (sum of t_i * lever_i) (inferred), which makes the row's angle a single nonlinear variable shared by several linear throttles.
- Related structural facts in sources: 2507.18309 section 2 mixes fixed-direction thrusters (polar form, magnitude only) and azimuth thrusters (rectangular form, two components) in one allocation; this covers per-thruster angle plus fixed thrusters, not shared angle.
- Sequential treatment of coupled problems exists in the literature: the tilt-rotor papers' cascaded or iterated split (1.3). A row-shared angle solved as an outer scalar search with an inner bounded linear solve is not described in any retrieved source (inferred as one possible shape; no source).

## 5. Converged / disagrees

Converged (stated by two or more retrieved sources):

1. Treating each tilting actuator as 2 or 3 linear force components with a constant allocation matrix, then recovering magnitude and angle by norm and atan2, is a standard approach: Voliro 1801.04581 (eq 16-23), Sørdalen extended thrust as cited in 2510.08119 and 2507.18309, Mukwege 2510.08119 (F in R^(3m)).
2. The recovered angle is discontinuous or undefined near zero component magnitude: Voliro (eq 23 and the 90 degree roll example), 2510.08119 (intro, section 3, section 5), 2507.18309 (azimuth penalty motivation).
3. Servo slowness is not represented by the linear trick alone: Voliro (explicit statement) and 2510.08119 (finite turning rate discussion) agree; solutions in the sources add a rate constraint (2510.08119 section 4), a rate-updated bound (2406.06130 section 4.1), a nullspace smoothing term (2510.08119), or a rate-limited reference filter (2507.18309).
4. Rate limits enter as per-sample bounds centred on the previous actuator state or previous solution: 2406.06130 (u_min/u_max updated from current state and rate), 2510.08119 (|F_i - F_i0| <= bound).
5. Tilt or pointing limits are cones in force space and convex; box approximations of the components are the cheap, conservative form: 2510.08119 (cone C_i, eqs 40-42).
6. Redundancy yields non-unique solutions chosen by a secondary objective (energy, nullspace, rest configuration): Voliro, 2510.08119, 2507.18309, 2406.06130.

Disagrees / differs across sources:

1. Linear vs nonlinear: Voliro rejects nonlinear least squares for compute cost; the marine SQP line (Johansen et al. 2004, MARIN) accepts a local QP per sample with a singularity penalty; 2510.08119 states the penalty fails when the thruster count is below the DOF count and around d = 0.
2. Where limits live: 2406.06130 puts the actuator box in the controller (NMPC) and uses a plain pseudo-inverse allocator; 2510.08119 puts constraints in the allocator QP/QCQP; 2507.18309 puts rate limitation in a downstream reference filter.
3. Singularity handling: penalty on det(J J^T) (Johansen 2004) vs nullspace rest configuration (2510.08119) vs differential allocation avoiding the algebraic map (Cuniato, snippet only) vs forbidden-zone removal (marine; Valčić says it causes non-convexity and large swings and proposes modelling interaction as losses instead).
4. Solvers: simplex/LP with scaling of out-of-reach commands (Orbiter thread) vs QP/active set (Härkegård, Bodson) vs SQP (Johansen, TU Delft) vs closed-form pseudo-inverse (Voliro, 2406.06130).
5. Games: Space Engineers applies thrust through the centre of mass by default (no torque from offset); Orbiter and the ATV approach allocate by simplex from true positions; KSP gimbals are small-range controls fed by control inputs per axis. They do not agree on whether positional torque is physically computed.

## 6. Sources

Read in full or in relevant part (text extracted):
- Kamel, Verling et al., "Voliro: An Omnidirectional Hexacopter With Tiltable Rotors", arXiv 1801.04581. https://arxiv.org/pdf/1801.04581
- Mukwege, Nguyen, Garone, "General formulation of an analytic and Lipschitz continuous control allocation for thrust-vectored controlled rigid-bodies", arXiv 2510.08119v3 (Apr 2026). https://arxiv.org/pdf/2510.08119
- Maneuvering-based Dynamic Thrust Allocation for Fully-Actuated Vessels, arXiv 2507.18309. https://arxiv.org/pdf/2507.18309
- Nonlinear Model Predictive Control of Tiltrotor Quadrotors with Feasible Control Allocation, arXiv 2406.06130. https://arxiv.org/abs/2406.06130
- Sorge, Ciresola, Michieletto, Cenedese, "Dynamic Control Allocation for Dual-Tilt UAV Platforms", arXiv 2604.05677 (text extracted, only keyword-checked; no claim above rests on it). https://arxiv.org/pdf/2604.05677
- Constrained Dynamic Control Allocation in the Presence of Singularity and Infeasible Solutions, arXiv 1607.05209 (abstract only). https://arxiv.org/abs/1607.05209
- Aetheria ThrustAllocator.cs, branch eureka/aetheria-release-thrust-allocator-core, commit 1fc98bdc.

Cited, not retrieved (bibliographic or snippet only):
- Johansen & Fossen, "Control allocation - A survey", Automatica 49(5):1087-1103, 2013, DOI 10.1016/j.automatica.2013.01.035. Full text not found (NTNU URLs returned HTML stubs).
- Johansen, Fossen, Berge, IEEE TCST 12(1):211-216, 2004.
- Sørdalen, "Optimal thrust allocation for marine vessels", Control Engineering Practice 5(9):1223-1231, 1997.
- Härkegård & Glad, "Resolving actuator redundancy - optimal control vs. control allocation", Automatica 41(1):137-144, 2005; Härkegård active-set allocation as cited in Bodson & Frost (NASA NTRS 20100024149 appeared in search results).
- Cuniato et al., IEEE T-RO, differential allocation for tiltable-rotor platforms (search summary).
- Valčić & Prpić-Oršić, forbidden-zone handling in thrust allocation (CROSBI 838045, 899099).
- MARIN, "An advanced thrust allocation algorithm for DP applications" (search result).
- TU Delft thesis on dual-axis tilt-rotor quadplanes, https://repository.tudelft.nl/file/File_3e64f087-21b4-458c-8fc6-6ea6559a1bbe
- KSP: https://kerbal.wiki/index.php/Part_modules, https://forum.kerbalspaceprogram.com/topic/46553-engine-gimbal-reaction-speed, https://ksp-kos.readthedocs.io/en/latest/structures/vessels/gimbal.html (the wiki Gimbal page was blocked by bot protection).
- Space Engineers: https://support.keenswh.com/spaceengineers/pc/topic/45350-realistic-thrusters-thruster-torque-thrust-allocation
- Avorion: https://avorion.fandom.com/wiki/Directional_Thruster
- Starsector: https://starsector.wiki.gg/wiki/Auxiliary_Thrusters
- Orbiter: https://orbiter-forum.com/threads/simplex-based-thruster-control.25232
