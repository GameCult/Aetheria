using System.Reflection;
using CultMath.UnityBridge;
using ShieldField;
using UnityEditor;
using UnityEngine;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Batchmode probe for the shield-panel Cut 3 landing (docs/shield-panel-cut.md): the
    /// interceptor presenter (ShieldInterceptor) and the panel pool. Tests.asmdef cannot see
    /// Assembly-CSharp (the map's Q7), so this stands in for NUnit coverage exactly as Cut 1 and
    /// Cut 2's probes do, and it runs the rules those probes cannot reach: same-frame injection
    /// (D17/D20), reuse-strikes-the-existing-panel, pool exhaustion, and the D21 Penrose/Hex
    /// density ratio.
    ///
    /// Unlike Cut 1/2 this exercises actual MonoBehaviour simulation state (GraphicsBuffer
    /// dispatches), which needs GPU access -- run WITHOUT -nographics, unlike the two probes before
    /// it. ShieldPanel deliberately has no [ExecuteAlways] (Cut 1 removed it, D13), so Update()
    /// never runs on its own in edit mode; this probe steps the simulation itself by invoking the
    /// private Update() method directly; it is Application.isPlaying == false throughout, so
    /// ShieldPanel's own dt fallback (1/60s, fixed) is exactly what's exercised.
    ///
    /// Run via: Unity -batchmode -projectPath &lt;repo&gt; -executeMethod
    ///          Aetheria.EditorTools.ShieldPanelCut3Verify.Run -logFile &lt;path&gt;
    /// (no -quit while iterating: EditorApplication.Exit both reports the code and quits.)
    /// </summary>
    public static class ShieldPanelCut3Verify
    {
        static readonly MethodInfo PanelUpdate =
            typeof(ShieldPanel).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo PanelOnEnable =
            typeof(ShieldPanel).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo PanelOnDisable =
            typeof(ShieldPanel).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Run()
        {
            bool ok = true;
            ok &= CheckSpawnFrameInjectionAbsorbs();
            ok &= CheckReuseStrikesExistingPanel();
            ok &= CheckPoolExhaustion();
            ok &= CheckNullGuardsDoNotThrow();
            ok &= CheckPoolReuseDoesNotRebuild();
            ok &= CheckPenroseDensity();

            Debug.Log(ok
                ? "[ShieldPanelCut3Verify] PASS"
                : "[ShieldPanelCut3Verify] FAIL — see errors above");

            EditorApplication.Exit(ok ? 0 : 1);
        }

        // ==============================================================
        static void StepPanel(ShieldPanel panel) => PanelUpdate.Invoke(panel, null);

        static (GameObject envelopeGO, ShieldEnvelope envelope, GameObject protoGO, ShieldInterceptor interceptor)
            BuildRig(int maxLive = 12, float panelRadius = 2f, float reuseFraction = 1f)
        {
            var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/Shaders/Compute/ShieldField/ShieldSim.compute");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Shaders/Compute/ShieldField/ShieldPanel.shader");

            // Uniform-scale sphere at the origin: no MeshCollider/MeshFilter, so ShieldEnvelope
            // takes its documented no-mesh fallback (_localExtents = Vector3.one) rather than
            // needing a real hull mesh -- this probe is about the interceptor's plumbing, and
            // Cut 2's own probe already pins the mesh-extents/normal arithmetic.
            var envelopeGO = new GameObject("Cut3Probe_Envelope");
            envelopeGO.transform.localScale = Vector3.one * 5f;
            var envelope = envelopeGO.AddComponent<ShieldEnvelope>();

            var protoGO = new GameObject("Cut3Probe_PanelPrototype");
            var panel = protoGO.AddComponent<ShieldPanel>();
            // Assigned after AddComponent so the OnEnable this triggers (sim/panelShader still
            // null at that point) is a no-op -- Rebuild() below is the real, deliberate build,
            // exactly as Cut 4's rig depends on the prototype's authored fields being set before
            // its first real build, not on whatever OnEnable saw at GameObject-creation time.
            panel.tiling = TilingKind.Hex;
            panel.panelRadius = panelRadius;
            panel.cellSize = 0.5f;
            panel.sim = sim;
            panel.panelShader = shader;
            panel.Rebuild();
            var proto = protoGO.AddComponent<Prototype>();

            var interceptorGO = new GameObject("Cut3Probe_Interceptor");
            var interceptor = interceptorGO.AddComponent<ShieldInterceptor>();
            interceptor.Envelope = envelope;
            interceptor.PanelPrototype = proto;
            interceptor.MaxLivePanels = maxLive;
            interceptor.PanelRadius = panelRadius;
            interceptor.ReuseFraction = reuseFraction;

            return (envelopeGO, envelope, protoGO, interceptor);
        }

        static void DestroyRig((GameObject envelopeGO, ShieldEnvelope envelope, GameObject protoGO, ShieldInterceptor interceptor) rig)
        {
            // The prototype's own pooled instances are children of protoGO's parent (null here, i.e.
            // scene root) per Prototype.Instantiate's `SetParent(transform.parent, ...)` -- find and
            // destroy them too so one check's panels can't be mistaken for the next check's.
            foreach (var go in UnityEngine.Object.FindObjectsByType<ShieldPanel>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(go.gameObject);
            UnityEngine.Object.DestroyImmediate(rig.envelopeGO);
            UnityEngine.Object.DestroyImmediate(rig.interceptor.gameObject);
        }

        static AbsorbEvent Absorb(Vector3 worldPos, Vector3 worldDir, float energy = 5f,
                                  DamageType type = DamageType.Kinetic)
            => new AbsorbEvent(worldPos.ToCultMath(), worldDir.ToCultMath(), energy, type);

        // ==============================================================
        // D17/D20: a panel struck in its own spawn frame must absorb that hit, not swallow it.
        // ==============================================================
        static bool CheckSpawnFrameInjectionAbsorbs()
        {
            var rig = BuildRig();
            try
            {
                rig.interceptor.Absorb(Absorb(new Vector3(0, 0, 20), new Vector3(0, 0, -1)));
                if (rig.interceptor.LiveCount != 1)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] spawn-frame: expected 1 live panel, got {rig.interceptor.LiveCount}.");
                    return false;
                }

                var panel = rig.interceptor.DebugPanelAt(0);
                // The strike lands at the panel's own local origin (ShieldInterceptor poses the
                // panel's transform AT the intercept point before calling Hit with that same
                // point), so the nearest cell to local (0,0) is the one that must show injection.
                int nearest = NearestCell(panel, Vector2.zero);

                // Exactly one Update() call: the frame the hit was queued in.
                StepPanel(panel);

                var v = new float[panel.DebugCellCount];
                panel.DebugVField.GetData(v);
                if (Mathf.Abs(v[nearest]) < 1e-6f)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] D17 not fixed: cell {nearest}'s wave velocity is " +
                                    $"{v[nearest]:E3} after the spawn-frame hit -- the growth gate swallowed it.");
                    return false;
                }

                // D20: the panel's own arm clock must keep the gate open on the NEXT frame too, not
                // just the spawn frame. KWaveStep zeroes both _UOut and _V outright for any cell
                // whose growth is still < 0.6 (ShieldSim.compute), so under the pre-Cut-3 bug
                // (_PanelSpawnTime hardcoded to 0f) growth gets reset to 0 by frame 1's own KUpdate
                // and frame 2 is gated shut again -- a fresh injection landing on frame 2 can only
                // push v MORE negative (compression) than whatever frame 1 left behind; the bug
                // instead zeroes it outright. Decay alone (the fix, with no second hit) could only
                // shrink |v| toward zero, never make it more negative, so this is a clean
                // discriminator between "still gated" and "correctly still open".
                float vBeforeSecondHit = v[nearest];
                panel.Hit(panel.transform.position, new Vector3(0, 0, -1), 5f);
                StepPanel(panel);
                var v2 = new float[panel.DebugCellCount];
                panel.DebugVField.GetData(v2);
                if (!(v2[nearest] < vBeforeSecondHit - 1e-4f))
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] D20 not fixed: a second hit queued for frame 2 did not " +
                                    $"inject (v {vBeforeSecondHit:E3} -> {v2[nearest]:E3}) -- growth likely regressed " +
                                    "back below the gate between frame 1 and frame 2.");
                    return false;
                }

                Debug.Log($"[ShieldPanelCut3Verify] D17/D20 OK: frame 1 |v|={Mathf.Abs(v[nearest]):E3}, " +
                          $"frame 2 (after a second queued hit) v={v2[nearest]:E3} -- the gate stayed open.");
                return true;
            }
            finally { DestroyRig(rig); }
        }

        static int NearestCell(ShieldPanel panel, Vector2 localPoint)
        {
            int best = 0;
            float bestD2 = float.MaxValue;
            for (int i = 0; i < panel.DebugCellCount; i++)
            {
                float d2 = (panel.DebugCellCenter(i) - localPoint).sqrMagnitude;
                if (d2 < bestD2) { bestD2 = d2; best = i; }
            }
            return best;
        }

        // ==============================================================
        // Reuse: a second hit inside a living panel strikes it rather than spawning a new one, and
        // the strike actually reaches the material (temper falls), not merely "panel count stayed
        // the same" -- a silently dropped second event would pass a panel-count-only check too.
        // ==============================================================
        static bool CheckReuseStrikesExistingPanel()
        {
            var rig = BuildRig(maxLive: 4);
            try
            {
                var p1 = new Vector3(0, 0, 20);
                var p2 = new Vector3(0.3f, 0, 20);   // projects close to p1 -- inside PanelRadius(2)*ReuseFraction(1)
                var pFar = new Vector3(20, 0, 0);     // projects to (5,0,0) -- ~7 units from (0,0,5), well past the reuse radius

                rig.interceptor.Absorb(Absorb(p1, new Vector3(0, 0, -1)));
                if (rig.interceptor.LiveCount != 1)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] reuse step 1: expected 1 live panel, got {rig.interceptor.LiveCount}.");
                    return false;
                }
                var panel = rig.interceptor.DebugPanelAt(0);

                // Step until the struck region is past the growth threshold and the first hit's
                // temper erosion has had time to register (growDuration default 0.35s @ 1/60 ~= 21
                // frames; erosion is also gated to whichever frames catch the wave in tension --
                // KFracture's `if (u <= 0) return` skips compression frames entirely -- so give it
                // several oscillation cycles, not just enough frames to grow).
                for (int i = 0; i < 40; i++) StepPanel(panel);

                // The map's own wording: compare the panel's MINIMUM temper, not one hand-picked
                // cell's -- a single cell can sit in a compression phase at the instant we sample
                // even though the strike clearly reached the material (KFracture only erodes temper
                // on a tension frame), so pinning one cell is a false-negative trap. The minimum
                // across the whole panel is what the map's reuse test actually asks for.
                float temperAfterFirst = MinTemper(panel);

                rig.interceptor.Absorb(Absorb(p2, new Vector3(0, 0, -1)));
                if (rig.interceptor.LiveCount != 1)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] reuse step 2: a nearby second hit spawned a new panel " +
                                    $"(live count {rig.interceptor.LiveCount}) instead of reusing the existing one.");
                    return false;
                }
                if (rig.interceptor.DebugPanelAt(0) != panel)
                {
                    Debug.LogError("[ShieldPanelCut3Verify] reuse step 2: live count stayed 1 but the panel identity changed.");
                    return false;
                }

                for (int i = 0; i < 40; i++) StepPanel(panel);
                float temperAfterSecond = MinTemper(panel);

                if (!(temperAfterSecond < temperAfterFirst - 1e-5f))
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] reuse step 2: min temper did not fall from the second hit " +
                                    $"({temperAfterFirst:F4} -> {temperAfterSecond:F4}) -- the strike reached the panel " +
                                    "but not the material.");
                    return false;
                }

                rig.interceptor.Absorb(Absorb(pFar, new Vector3(-1, 0, 0)));
                if (rig.interceptor.LiveCount != 2)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] reuse step 3: a far strike should spawn a second panel " +
                                    $"(live count {rig.interceptor.LiveCount}, expected 2).");
                    return false;
                }

                Debug.Log($"[ShieldPanelCut3Verify] reuse OK: temper {temperAfterFirst:F4} -> {temperAfterSecond:F4} on reuse; " +
                          "a far strike opened a second panel.");
                return true;
            }
            finally { DestroyRig(rig); }
        }

        static float MinTemper(ShieldPanel panel)
        {
            var raw = new CellStateRaw[panel.DebugCellCount];
            panel.DebugStateBuffer.GetData(raw);
            float min = float.MaxValue;
            foreach (var s in raw) min = Mathf.Min(min, s.temper);
            return min;
        }

        // Mirrors ShieldField.CellState's layout for GraphicsBuffer.GetData -- kept local to the
        // probe rather than reusing ShieldField.CellState directly so a future field reorder in the
        // production struct (which IS this probe's business to catch) shows up as a stride/offset
        // mismatch here rather than the probe silently reading through the same accidental typo.
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct CellStateRaw
        {
            public Vector3 vel; public float spin; public Vector3 pos; public float rot;
            public float damage; public float breakTime; public float glow; public float growth;
            public float temper; public float released; public float peakTension; public float pad2;
        }

        // ==============================================================
        // Pool exhaustion: MaxLivePanels+3 widely-separated strikes must never exceed the cap, and
        // must not drop a strike -- the oldest live panel is recycled in place.
        // ==============================================================
        static bool CheckPoolExhaustion()
        {
            const int cap = 3;
            var rig = BuildRig(maxLive: cap, panelRadius: 1f);
            try
            {
                // Spread strikes around the equator, far enough apart (PanelRadius*3+) that none reuse.
                for (int i = 0; i < cap; i++)
                {
                    float a = i * 137.5f * Mathf.Deg2Rad; // irrational-ish spacing, no two ever coincide
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    rig.interceptor.Absorb(Absorb(dir * 20f, -dir));
                }
                if (rig.interceptor.LiveCount != cap)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] exhaustion: expected {cap} live panels before exhaustion, got {rig.interceptor.LiveCount}.");
                    return false;
                }

                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = cap; i < cap + 3; i++)
                {
                    float a = i * 137.5f * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    rig.interceptor.Absorb(Absorb(dir * 20f, -dir));
                }
                long after = System.GC.GetAllocatedBytesForCurrentThread();

                if (rig.interceptor.LiveCount != cap)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] exhaustion: live count is {rig.interceptor.LiveCount} " +
                                    $"after exceeding MaxLivePanels ({cap}); the cap did not hold.");
                    return false;
                }

                // Soft budget, not a hard zero: JIT/editor background allocation noise is real, but
                // three GraphicsBuffer.SetData-only re-arms plus list bookkeeping should be a small,
                // fixed number of bytes, not the kilobytes a Rebuild (or a per-hit array/list
                // allocation regression) would cost.
                long perStrike = (after - before) / 3;
                if (perStrike > 4096)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] exhaustion: ~{perStrike} B allocated per strike after " +
                                    "the pool filled -- §3's 'no allocation at interception' looks violated.");
                    return false;
                }

                Debug.Log($"[ShieldPanelCut3Verify] exhaustion OK: live count capped at {cap}, ~{perStrike} B/strike once full.");
                return true;
            }
            finally { DestroyRig(rig); }
        }

        // ==============================================================
        static bool CheckNullGuardsDoNotThrow()
        {
            var go = new GameObject("Cut3Probe_NullGuard");
            try
            {
                var interceptor = go.AddComponent<ShieldInterceptor>();
                try
                {
                    interceptor.Absorb(Absorb(Vector3.forward * 10, Vector3.back));
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] null-guard: Absorb with no Envelope/PanelPrototype threw {ex}.");
                    return false;
                }
                Debug.Log("[ShieldPanelCut3Verify] null-guard OK: Absorb with no Envelope/PanelPrototype did not throw.");
                return true;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ==============================================================
        // The pooling fix this cut depends on: a panel returned to the pool and reused must NOT
        // rebuild its tiling (§2.4's whole 123-2686x argument for prebuilding).
        // ==============================================================
        static bool CheckPoolReuseDoesNotRebuild()
        {
            var rig = BuildRig(maxLive: 4);
            try
            {
                rig.interceptor.Absorb(Absorb(new Vector3(0, 0, 20), Vector3.back));
                var panel = rig.interceptor.DebugPanelAt(0);
                int rebuildsAfterSpawn = panel.DebugRebuildCount;

                // ReturnToPool() is called on the INSTANCE (it hands itself to its own
                // _originalPrototype's pool); Instantiate() must be called on the ORIGINAL prototype
                // -- Prototype.Instantiate<T>'s free list lives on whichever object it's called on,
                // and only the original's is ever populated by ReturnToPool.
                panel.GetComponent<Prototype>().ReturnToPool();
                var reused = rig.interceptor.PanelPrototype.Instantiate<ShieldPanel>();

                if (!ReferenceEquals(reused, panel))
                {
                    Debug.LogError("[ShieldPanelCut3Verify] pool-reuse: Instantiate() after ReturnToPool returned a " +
                                    "different instance than the one just returned -- the free list isn't LIFO-of-one as expected.");
                    return false;
                }
                if (panel.DebugRebuildCount != rebuildsAfterSpawn)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] pool-reuse: RebuildCount went {rebuildsAfterSpawn} -> " +
                                    $"{panel.DebugRebuildCount} across a ReturnToPool/Instantiate cycle -- OnEnable is " +
                                    "rebuilding on every reuse instead of only once.");
                    return false;
                }

                // The check above is necessary but not sufficient: SetActive(false)/(true) is not
                // guaranteed to fire OnDisable/OnEnable synchronously within this same call stack in
                // every context this probe can run in (observed directly -- the very first spawn's
                // OnEnable does not run synchronously inside Object.Instantiate here, which is why
                // ResetSim() is self-healing rather than relying on it). So drive OnEnable/OnDisable
                // themselves, directly, to pin the rule those methods actually encode regardless of
                // when/whether Unity's own SetActive plumbing calls them in a given context.
                int rebuildsBeforeDirect = panel.DebugRebuildCount;
                PanelOnDisable.Invoke(panel, null);
                PanelOnEnable.Invoke(panel, null);
                if (panel.DebugRebuildCount != rebuildsBeforeDirect || panel.DebugCellCount == 0)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] pool-reuse: a direct OnDisable()/OnEnable() cycle " +
                                    $"changed RebuildCount ({rebuildsBeforeDirect} -> {panel.DebugRebuildCount}) or " +
                                    $"cleared the tiling (cellCount={panel.DebugCellCount}) -- OnEnable is rebuilding " +
                                    "(or OnDisable is releasing) on every reuse instead of only once.");
                    return false;
                }

                Debug.Log("[ShieldPanelCut3Verify] pool-reuse OK: no Rebuild across a return/reuse cycle.");
                return true;
            }
            finally { DestroyRig(rig); }
        }

        // ==============================================================
        // D21: Penrose's achievable cell counts are phi-quantised, but must land within a small
        // factor of Hex's at the same radius/cellSize, not an order of magnitude denser.
        // ==============================================================
        static bool CheckPenroseDensity()
        {
            const float cellSize = 0.35f;
            // The ruling (R5/D21) fixes the MECHANISM -- nearest achievable edge length, not
            // nearest achievable Hex-equivalent cell count -- and each Penrose substitution changes
            // density by phi^2 (~2.618x), so landing within that step of Hex's count at an arbitrary
            // radius/cellSize is already the honest ceiling for "nearest edge length", not a tuning
            // knob. Measured after the fix (see the Cut 3 report): 2.12x-4.70x across r in
            // {1.5,3,5,10} at cellSize 0.35, against the pre-fix 24x/8x order-of-magnitude bug this
            // rule replaces. 5x is comfortably past that measured range and still an order of
            // magnitude tighter than what D21 opened with.
            const float smallFactor = 5f;
            float[] radii = { 1.5f, 3f, 5f, 10f };
            bool ok = true;

            for (int i = 0; i < radii.Length; i++)
            {
                float r = radii[i];
                var pSoup = Tilings.Generate(TilingKind.PenroseP3, r, cellSize, 1337);
                var pMerged = TilingBuilder.MergeRhombs(pSoup);
                var pData = TilingBuilder.Build(pMerged, weldEps: Mathf.Max(cellSize * 0.02f, 1e-4f), clipRadius: r, seed: 1337);

                var hSoup = Tilings.Generate(TilingKind.Hex, r, cellSize, 1337);
                var hData = TilingBuilder.Build(hSoup, weldEps: Mathf.Max(cellSize * 0.02f, 1e-4f), clipRadius: r, seed: 1337);

                float ratio = (float)pData.CellCount / hData.CellCount;
                Debug.Log($"[ShieldPanelCut3Verify] D21 radius={r} cellSize={cellSize} " +
                          $"PenroseP3(merged) cells={pData.CellCount} Hex cells={hData.CellCount} ratio={ratio:F2}");

                if (ratio > smallFactor || ratio < 1f / smallFactor)
                {
                    Debug.LogError($"[ShieldPanelCut3Verify] D21 not within a small factor at radius {r}: " +
                                    $"Penrose/Hex ratio {ratio:F2} is outside [1/{smallFactor:F0}, {smallFactor:F0}].");
                    ok = false;
                }
            }

            if (ok) Debug.Log("[ShieldPanelCut3Verify] D21 OK: Penrose stays within a small factor of Hex at every radius.");
            return ok;
        }
    }
}
