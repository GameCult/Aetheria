using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShieldField
{
    public enum FracturePattern { Radial = 0, Star = 1, Lance = 2, Spiral = 3, Crystal = 4, Fizzle = 5 }

    public class ShieldPanel : MonoBehaviour
    {
        // ---------------- authoring ----------------
        [Header("Tiling")]
        public TilingKind tiling = TilingKind.PenroseP3;
        public float panelRadius = 5f;
        public float cellSize = 0.35f;
        public bool mergeRhombs = true;      // Penrose triangles -> P3 rhombs
        public bool takeDual = false;        // render the dual tiling instead
        public int seed = 1337;

        [Header("Assets")]
        public ComputeShader sim;
        public Shader panelShader;

        [Header("Materialisation")]
        public float growDuration = 0.35f;
        public float wavefrontSpeed = 8f;
        public float delayJitter = 0.06f;

        [Header("Wave")]
        [Tooltip("c^2. Raise for a faster shock; watch the CFL warning in the console.")]
        public float waveSpeed2 = 26f;
        public float damping = 1.6f;
        public float stressClamp = 4f;
        [Range(1, 16)] public int substeps = 6;
        public float fixedSubstepDt = 0.0025f;

        [Header("Through-thickness echo")]
        [Tooltip("Substeps for one round trip through the plate. Small = fast back-face spall.")]
        [Range(1, 15)] public int echoDelaySteps = 5;
        [Tooltip("Negative: a free back face returns the compressive pulse as tension.")]
        public float echoReflect = -0.65f;

        [Header("Material (tempered)")]
        public float tensileStrength = 0.9f;
        [Tooltip("Surface compression at full charge. This is the shield's hit budget.")]
        public float temperInit = 0.55f;
        public float temperErosion = 2.2f;
        [Tooltip("How much stored core energy a break dumps back into the field.")]
        public float storedEnergyGain = 0.8f;
        public float damageGain = 6f;
        [Range(0.05f, 1f)] public float breakAt = 0.75f;
        [Range(0f, 0.9f)] public float crackStart = 0.18f;
        [Tooltip("Breaks permitted per frame. Without this the cascade finishes in one frame and you see nothing.")]
        [Range(0.001f, 1f)] public float breakBudgetFraction = 0.02f;

        [Header("Shards")]
        public float radialKick = 1.6f;
        public float lateralKick = 2.2f;
        public float normalKick = 1.1f;
        public float spinKick = 7f;
        public float shardLife = 2.5f;
        public float shardDrag = 0.7f;
        [Range(2, 8)] public int maxShards = 6;

        [Header("Render")]
        public float conduitGain = 2.5f;

        // ---------------- runtime ----------------
        struct PendingHit
        {
            public Vector2 pos, dir;
            public float energy, radius, obliquity, spin;
            public FracturePattern pattern;
            public float starArms, focus, twist;
        }

        readonly Queue<PendingHit> pending = new();

        TilingData data;
        Material mat;
        MaterialPropertyBlock mpb;

        GraphicsBuffer bCells, bVerts, bEdges, bState;
        GraphicsBuffer bUA, bUB, bV, bEcho, bFlow, bVisible, bBudget;
        GraphicsBuffer argsFill;

        bool uUsingA = true;
        int kInit, kInject, kWave, kEcho, kFracture, kUpdate;
        int groups, echoSlot;
        float simTime;
        Bounds worldBounds;
        uint[] budgetScratch;   // cached to avoid a per-frame allocation (D12)

        const int EchoSlots = 16;
        static readonly int MaxV = Limits.MaxVerts;

        // shader ids
        static readonly int idCells = Shader.PropertyToID("_Cells");
        static readonly int idVerts = Shader.PropertyToID("_Verts");
        static readonly int idEdges = Shader.PropertyToID("_Edges");
        static readonly int idState = Shader.PropertyToID("_State");
        static readonly int idUIn = Shader.PropertyToID("_UIn");
        static readonly int idUOut = Shader.PropertyToID("_UOut");
        static readonly int idU = Shader.PropertyToID("_U");
        static readonly int idV = Shader.PropertyToID("_V");
        static readonly int idEcho = Shader.PropertyToID("_Echo");
        static readonly int idFlow = Shader.PropertyToID("_EdgeFlow");
        static readonly int idVisible = Shader.PropertyToID("_Visible");
        static readonly int idBudget = Shader.PropertyToID("_BreakBudget");

        // ==============================================================
        void OnEnable() { Rebuild(); }
        void OnDisable() { ReleaseAll(); }

        [UnityEngine.ContextMenu("Rebuild")]
        public void Rebuild()
        {
            ReleaseAll();
            if (sim == null || panelShader == null) return;

            var soup = Tilings.Generate(tiling, panelRadius, cellSize, seed);

            if (tiling == TilingKind.PenroseP3 && mergeRhombs)
                soup = TilingBuilder.MergeRhombs(soup);
            if (takeDual)
                soup = TilingBuilder.TakeDual(soup);

            data = TilingBuilder.Build(soup,
                weldEps: Mathf.Max(cellSize * 0.02f, 1e-4f),
                clipRadius: panelRadius,
                wavefrontOrigin: Vector2.zero,
                wavefrontSpeed: wavefrontSpeed,
                delayJitter: delayJitter,
                seed: seed);

            AllocateBuffers();
            CacheKernels();
            ResetSim();
            CheckCfl();
        }

        void AllocateBuffers()
        {
            int n = data.CellCount;
            groups = Mathf.CeilToInt(n / 64f);

            bCells = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, CellStatic.Stride);
            bVerts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.verts.Length, sizeof(float) * 2);
            bEdges = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(data.EdgeCount, 1), DualEdge.Stride);
            bState = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, sizeof(float) * 16);

            bUA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, sizeof(float));
            bUB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, sizeof(float));
            bV = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, sizeof(float));
            bEcho = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n * EchoSlots, sizeof(float));
            bFlow = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(data.EdgeCount, 1), sizeof(float));
            bBudget = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(uint));

            bVisible = new GraphicsBuffer(GraphicsBuffer.Target.Append | GraphicsBuffer.Target.Structured,
                                          n, sizeof(uint));

            bCells.SetData(data.cells);
            bVerts.SetData(data.verts);
            if (data.EdgeCount > 0) bEdges.SetData(data.dualEdges);

            // One args buffer, sized to the widest pass (Outline: n*2 segments * 6 verts).
            // RenderParams exposes no per-pass selector in 6000.3.24f1 (D2), and a multi-pass material
            // draws every pass from one indirect call anyway (D5/§2.1.4-5), so the three
            // per-pass buffers this shipped with were dead weight, not three draw paths.
            argsFill = MakeArgs((uint)(MaxV * 2 * 6));

            mat = new Material(panelShader) { hideFlags = HideFlags.DontSave };
            mpb = new MaterialPropertyBlock();

            var b = data.bounds;
            float pad = panelRadius * 1.5f + shardLife * 3f;
            worldBounds = new Bounds(transform.TransformPoint(b.center),
                                     b.size + Vector3.one * pad);

            budgetScratch ??= new uint[2];
        }

        static GraphicsBuffer MakeArgs(uint vertsPerInstance)
        {
            var a = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
                                       GraphicsBuffer.IndirectDrawArgs.size);
            a.SetData(new[] { new GraphicsBuffer.IndirectDrawArgs {
                vertexCountPerInstance = vertsPerInstance,
                instanceCount = 0, startVertex = 0, startInstance = 0 } });
            return a;
        }

        void CacheKernels()
        {
            kInit = sim.FindKernel("KInit");
            kInject = sim.FindKernel("KInject");
            kWave = sim.FindKernel("KWaveStep");
            kEcho = sim.FindKernel("KEcho");
            kFracture = sim.FindKernel("KFracture");
            kUpdate = sim.FindKernel("KUpdate");

            foreach (int k in new[] { kInit, kInject, kWave, kEcho, kFracture, kUpdate })
            {
                sim.SetBuffer(k, idCells, bCells);
                sim.SetBuffer(k, idEdges, bEdges);
                sim.SetBuffer(k, idState, bState);
                sim.SetBuffer(k, idV, bV);
                sim.SetBuffer(k, idEcho, bEcho);
                sim.SetBuffer(k, idFlow, bFlow);
                sim.SetBuffer(k, idBudget, bBudget);
            }
            sim.SetBuffer(kInit, idUOut, bUA);
            sim.SetBuffer(kWave, idFlow, bFlow);
            sim.SetBuffer(kUpdate, idVisible, bVisible);
        }

        [UnityEngine.ContextMenu("Reset Simulation")]
        public void ResetSim()
        {
            if (sim == null || data == null) return;
            simTime = 0f;
            echoSlot = 0;
            uUsingA = true;
            pending.Clear();

            sim.SetInt("_CellCount", data.CellCount);
            sim.SetFloat("_TemperInit", temperInit);
            sim.SetBuffer(kInit, idUOut, bUA);
            sim.Dispatch(kInit, groups, 1, 1);

            // second clear so both halves of the ping-pong start at rest
            sim.SetBuffer(kInit, idUOut, bUB);
            sim.Dispatch(kInit, groups, 1, 1);
            sim.SetBuffer(kInit, idUOut, bUA);
        }

        void CheckCfl()
        {
            float minArea = float.MaxValue;
            int maxDeg = 0;
            foreach (var c in data.cells)
            {
                minArea = Mathf.Min(minArea, c.area);
                maxDeg = Mathf.Max(maxDeg, (int)c.nbrCount);
            }
            // rough stability bound for the explicit leapfrog on an irregular graph
            float lim = waveSpeed2 * fixedSubstepDt * fixedSubstepDt * maxDeg / Mathf.Max(minArea, 1e-6f);
            if (lim > 1.8f)
                Debug.LogWarning($"[ShieldPanel] CFL estimate {lim:F2} exceeds ~1.8 and the wave " +
                                 $"will likely diverge. Lower waveSpeed2 or fixedSubstepDt, or raise cellSize. " +
                                 $"(smallest cell area {minArea:E2}, max degree {maxDeg})", this);
        }

        // ==============================================================
        /// <summary>Strike the panel. worldDir is the projectile's travel direction.</summary>
        public void Hit(Vector3 worldPos, Vector3 worldDir, float energy,
                        FracturePattern pattern = FracturePattern.Radial,
                        float radius = -1f)
        {
            if (data == null) return;

            Vector3 local = transform.InverseTransformPoint(worldPos);
            Vector3 ldir = transform.InverseTransformDirection(worldDir).normalized;

            Vector2 flat = new Vector2(ldir.x, ldir.y);
            float obliquity = Mathf.Clamp01(1f - Mathf.Abs(ldir.z));

            pending.Enqueue(new PendingHit
            {
                pos = new Vector2(local.x, local.y),
                dir = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector2.right,
                energy = energy,
                radius = radius > 0f ? radius : Mathf.Max(cellSize * 2.5f, panelRadius * 0.09f),
                obliquity = obliquity,
                pattern = pattern,
                starArms = Random.Range(3, 9),
                focus = Mathf.Lerp(0.15f, 0.55f, Random.value),
                twist = Random.Range(0.4f, 1.6f),
                spin = Random.value * Mathf.PI * 2f
            });
        }

        // ==============================================================
        void Update()
        {
            if (sim == null || data == null || mat == null) return;

            float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            simTime += dt;

            sim.SetInt("_CellCount", data.CellCount);
            sim.SetFloat("_Time", simTime);
            PushMaterialUniforms();

            // ---- break budget resets every frame; this is what paces the cascade
            uint cap = (uint)Mathf.Max(1, Mathf.RoundToInt(data.CellCount * breakBudgetFraction));
            budgetScratch[0] = 0u;
            budgetScratch[1] = cap;
            bBudget.SetData(budgetScratch);

            // ---- inject queued hits
            sim.SetFloat("_Dt", fixedSubstepDt);
            while (pending.Count > 0)
            {
                var h = pending.Dequeue();
                sim.SetVector("_HitPos", h.pos);
                sim.SetVector("_HitDir", h.dir);
                sim.SetFloat("_HitEnergy", h.energy);
                sim.SetFloat("_HitRadius", h.radius);
                sim.SetFloat("_HitObliquity", h.obliquity);
                sim.SetInt("_Pattern", (int)h.pattern);
                sim.SetFloat("_StarArms", h.starArms);
                sim.SetFloat("_PatternFocus", h.focus);
                sim.SetFloat("_SpiralTwist", h.twist);
                sim.SetFloat("_HitSpin", h.spin);
                sim.SetInt("_EchoSlot", echoSlot);
                sim.SetBuffer(kInject, idV, bV);
                sim.Dispatch(kInject, groups, 1, 1);
            }

            // ---- wave substeps (fixed dt: the CFL limit does not care about frame rate)
            sim.SetFloat("_WaveSpeed2", waveSpeed2);
            sim.SetFloat("_Damping", damping);
            sim.SetFloat("_StressClamp", stressClamp);
            sim.SetInt("_EchoDelay", echoDelaySteps);
            sim.SetFloat("_EchoReflect", echoReflect);

            for (int s = 0; s < substeps; s++)
            {
                var src = uUsingA ? bUA : bUB;
                var dst = uUsingA ? bUB : bUA;

                sim.SetBuffer(kWave, idUIn, src);
                sim.SetBuffer(kWave, idUOut, dst);
                sim.Dispatch(kWave, groups, 1, 1);
                uUsingA = !uUsingA;

                sim.SetInt("_EchoSlot", echoSlot);
                sim.Dispatch(kEcho, groups, 1, 1);
                echoSlot = (echoSlot + 1) % EchoSlots;
            }

            var cur = uUsingA ? bUA : bUB;

            // ---- fracture
            sim.SetFloat("_TensileStrength", tensileStrength);
            sim.SetFloat("_TemperErosion", temperErosion);
            sim.SetFloat("_StoredEnergyGain", storedEnergyGain);
            sim.SetFloat("_DamageGain", damageGain);
            sim.SetFloat("_BreakAt", breakAt);
            sim.SetFloat("_CrackStart", crackStart);
            sim.SetFloat("_RadialKick", radialKick);
            sim.SetFloat("_LateralKick", lateralKick);
            sim.SetFloat("_NormalKick", normalKick);
            sim.SetFloat("_SpinKick", spinKick);
            sim.SetBuffer(kFracture, idUIn, cur);
            sim.Dispatch(kFracture, groups, 1, 1);

            // ---- per-frame state advance + visibility
            sim.SetFloat("_Dt", dt);
            sim.SetFloat("_PanelSpawnTime", 0f);
            sim.SetFloat("_GrowDuration", growDuration);
            sim.SetFloat("_GlowDecay", 3.2f);
            sim.SetFloat("_ShardLife", shardLife);
            sim.SetFloat("_ShardDrag", shardDrag);

            bVisible.SetCounterValue(0);
            sim.Dispatch(kUpdate, groups, 1, 1);

            GraphicsBuffer.CopyCount(bVisible, argsFill, sizeof(uint));

            Draw(cur);
        }

        void PushMaterialUniforms()
        {
            mat.SetFloat("_CrackStart", crackStart);
            mat.SetFloat("_ShardLife", shardLife);
            mat.SetInt("_MaxShards", maxShards);
            mat.SetFloat("_ConduitGain", conduitGain);
        }

        void Draw(GraphicsBuffer curU)
        {
            mpb.Clear();
            mpb.SetBuffer(idCells, bCells);
            mpb.SetBuffer(idVerts, bVerts);
            mpb.SetBuffer(idEdges, bEdges);
            mpb.SetBuffer(idState, bState);
            mpb.SetBuffer(idU, curU);
            mpb.SetBuffer(idFlow, bFlow);
            mpb.SetBuffer(idVisible, bVisible);
            mpb.SetMatrix("_PanelToWorld", transform.localToWorldMatrix);
            mpb.SetFloat("_Time01", simTime);

            worldBounds.center = transform.position;

            var rp = new RenderParams(mat)
            {
                worldBounds = worldBounds,
                matProps = mpb,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer
            };

            // RenderParams exposes no per-pass selector in 6000.3.24f1 (D2), and a multi-pass
            // material draws every SubShader pass from one indirect call regardless
            // (D5) — Fill, Outline and Conduit all run here, pixel-identical to the
            // old three-call version (§2.1.5), off one args buffer sized to the
            // widest pass (Outline).
            Graphics.RenderPrimitivesIndirect(rp, MeshTopology.Triangles, argsFill, 1, 0);
        }

        // ==============================================================
        void ReleaseAll()
        {
            void R(ref GraphicsBuffer b) { b?.Release(); b = null; }
            R(ref bCells); R(ref bVerts); R(ref bEdges); R(ref bState);
            R(ref bUA); R(ref bUB); R(ref bV); R(ref bEcho); R(ref bFlow);
            R(ref bVisible); R(ref bBudget);
            R(ref argsFill);

            if (mat != null)
            {
                if (Application.isPlaying) Destroy(mat); else DestroyImmediate(mat);
                mat = null;
            }
            data = null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, panelRadius);
        }
    }
}
