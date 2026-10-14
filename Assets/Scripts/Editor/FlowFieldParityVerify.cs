using System;
using System.IO;
using GameCult.Caching;
using UnityEditor;
using UnityEngine;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Batchmode check for flow-shader-parity (campaign aetheria-fauna, cut fauna-flow-field): the HLSL globalFlow and
    /// slopeFlow of Assets/Shaders/Volumetric.cginc give the values of FlowField (ServerShared) for the same position,
    /// scroll, settings and gradient. It drives the real lowering: a Zone is stepped, VolumeSampling.LowerFlow sets the
    /// shader globals from the zone's Settings.Flow and FlowScroll, FlowParity.compute reads those globals, and the
    /// results are compared with FlowField. It also checks that Settings.asset carries the authored flow under
    /// PlanetSettings and that the shader got the zone's scroll (speed times zone time).
    ///
    /// Dispatches a compute kernel, so it needs a graphics device: run WITHOUT -nographics (as ShieldPanelCut3Verify).
    ///   Unity -batchmode -projectPath &lt;repo&gt; -executeMethod Aetheria.EditorTools.FlowFieldParityVerify.Run -logFile &lt;path&gt;
    /// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
    /// </summary>
    public static class FlowFieldParityVerify
    {
        const string Tag = "[FlowFieldParityVerify]";
        // Measured disagreement between the HLSL and the C# on this tree (float32 on both sides, positions to 10000,
        // scrolls to 100, through the keyword-gated flow()): max 2.9e-5 of amplitude (the "max error" line this run
        // prints). The tolerance is 5x that noise, so constant errors of 0.05% (y factor .501, swirl or amplitude
        // x1.0005: 4e-4 to 8e-4) fail; a 0.01% error is at the noise floor and unresolvable.
        const float RelativeTolerance = 1.5e-4f;
        static float _maxError;
        static bool _ok = true;

        static void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !message.StartsWith(Tag))
                Fail("logged-error", message.Split('\n')[0]);
        }

        static void Fail(string check, string detail)
        {
            Debug.LogError($"{Tag} FAIL {check}: {detail}");
            _ok = false;
        }

        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError($"{Tag} FAIL: no graphics device in this session; rerun batchmode without -nographics.");
                EditorApplication.Exit(1);
                return;
            }

            // The flow's error lines (a shader keyword that does not exist, a missing property) fail the check.
            Application.logMessageReceived += OnLog;
            var work = Path.Combine(Path.GetTempPath(), "flow-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset")
                    ?? throw new InvalidOperationException("No Settings.asset.");
                var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/Compute/FlowParity.compute")
                    ?? throw new InvalidOperationException("No FlowParity.compute.");

                CheckAuthoredFlow(settings.PlanetSettings.Flow);

                // The authored flow (global term only: its slope amplitudes are authored zero), then a flow with every
                // term live and a scroll speed that reaches scrolls up to 100.
                var authored = settings.PlanetSettings.Flow;
                CheckParity("authored", settings, work, compute, authored, new[] { 0.0, 3.1, 40.0, 100.0 });
                var wide = new FlowSettings
                {
                    GlobalScale = authored.GlobalScale, GlobalAmplitude = authored.GlobalAmplitude, Period = authored.Period,
                    GlobalScrollSpeed = 1f, SlopeAmplitude = 3f, SwirlAmplitude = 5f
                };
                CheckParity("wide", settings, work, compute, wide, new[] { 0.0, 1.0, 17.0, 60.0, 100.0 });
                CheckNoiseClock(compute);

                Debug.Log($"{Tag} max error {_maxError:G4} of amplitude (tolerance {RelativeTolerance:G4})");
                Debug.Log(_ok ? $"{Tag} PASS" : $"{Tag} FAIL: see errors above");
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAIL exception: {e}");
                _ok = false;
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                try { Directory.Delete(work, true); } catch { }
            }
            EditorApplication.Exit(_ok ? 0 : 1);
        }

        // Settings.asset carries the authored flow under PlanetSettings (it moved from DefaultEnvironment unchanged).
        static void CheckAuthoredFlow(FlowSettings flow)
        {
            if (flow == null) { Fail("authored-flow", "PlanetSettings.Flow is null"); return; }
            if (flow.GlobalScale != 512f || flow.GlobalAmplitude != 15f || flow.GlobalScrollSpeed != .025f ||
                flow.Period != 8f || flow.SlopeAmplitude != 0f || flow.SwirlAmplitude != 0f)
                Fail("authored-flow", $"PlanetSettings.Flow is {flow.GlobalScale}/{flow.GlobalAmplitude}/{flow.GlobalScrollSpeed}/" +
                                       $"{flow.Period}/{flow.SlopeAmplitude}/{flow.SwirlAmplitude}, authored 512/15/0.025/8/0/0");
        }

        internal static Zone BuildZone(GameSettings settings, string work, FlowSettings flow, double time = 0)
        {
            var cache = AetheriaStores.Open(Path.Combine(work, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.FlushAsync().Wait();
            var items = new ItemManager(cache, new ProvenanceLedger(), settings.GameplaySettings, _ => { });
            return new Zone(items, new PlanetSettings { Flow = flow }, new ZonePack { Time = time }, new GalaxyZone { Name = "Parity", Owner = null }, null);
        }

        static void CheckParity(string label, GameSettings settings, string work, ComputeShader compute, FlowSettings flow,
            double[] zoneTimes)
        {
            var zone = BuildZone(settings, work, flow);
            var kernel = compute.FindKernel("Evaluate");
            var rng = new System.Random(label.Length * 7919);
            const int positionCount = 256, gradientCount = 64;
            const int count = positionCount;

            var positions = new Vector3[count];
            for (var i = 0; i < positionCount; i++)
                positions[i] = new Vector3((float) (rng.NextDouble() * 20000 - 10000), (float) (rng.NextDouble() * 200 - 100),
                    (float) (rng.NextDouble() * 20000 - 10000));
            // Gradients of any direction and magnitude, plus one zero gradient.
            var gradients = new Vector2[count];
            for (var i = 1; i < gradientCount; i++)
            {
                var angle = rng.NextDouble() * Math.PI * 2;
                var magnitude = (float) Math.Exp(rng.NextDouble() * 8 - 4);
                gradients[i] = new Vector2((float) Math.Cos(angle) * magnitude, (float) Math.Sin(angle) * magnitude);
            }
            gradients[0] = Vector2.zero;

            var positionBuffer = new ComputeBuffer(count, 12);
            var gradientBuffer = new ComputeBuffer(count, 8);
            var globalBuffer = new ComputeBuffer(count, 12);
            var slopeBuffer = new ComputeBuffer(count, 12);
            var gatedBuffer = new ComputeBuffer(count, 12);
            ComputeBuffer noiseBuffer = null;
            // flow() reads the nebula's gravity height under FLOW_SLOPE. A ramp rising along the texture's x gives every
            // position inside the grid the gradient direction (1, 0), so the slope term is live where a flat height
            // would zero it (and the FLOW_SLOPE keyword and its pragma would not matter).
            const int rampSize = 64;
            var height = new Texture2D(rampSize, rampSize, TextureFormat.RFloat, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var ramp = new float[rampSize * rampSize];
            for (var i = 0; i < ramp.Length; i++) ramp[i] = (i % rampSize) / (float) (rampSize - 1);
            height.SetPixelData(ramp, 0);
            height.Apply();
            var rampGradient = new CultMath.float2(1f, 0f);
            try
            {
                positionBuffer.SetData(positions);
                gradientBuffer.SetData(gradients);
                compute.SetBuffer(kernel, "positions", positionBuffer);
                compute.SetBuffer(kernel, "gradients", gradientBuffer);
                compute.SetBuffer(kernel, "globalOut", globalBuffer);
                compute.SetBuffer(kernel, "slopeOut", slopeBuffer);
                compute.SetBuffer(kernel, "gatedOut", gatedBuffer);
                compute.SetTexture(kernel, "_NebulaSurfaceHeight", height);
                compute.SetVector("_NebulaSurfaceHeight_TexelSize", new Vector4(1f / rampSize, 1f / rampSize, rampSize, rampSize));
                // The grid spans 40000: every sampled position (|xz| to 10000) is inside the ramp's uv range.
                Shader.SetGlobalVector("_GridTransform", new Vector4(0f, 0f, 40000f, 0f));
                noiseBuffer = new ComputeBuffer(count, 4);
                compute.SetBuffer(kernel, "noiseOut", noiseBuffer);
                compute.SetInt("count", count);

                var elapsed = 0.0;
                foreach (var target in zoneTimes)
                {
                    // Step the zone to the target by an awkward step; the sum is the zone's time.
                    while (elapsed < target - 1e-9)
                    {
                        var dt = (float) Math.Min(0.37, target - elapsed);
                        zone.Update(dt);
                        elapsed += dt;
                    }
                    VolumeSampling.LowerFlow(zone.Settings.Flow, zone.ExactTime);

                    // The shader got the zone's scroll and cycle: the same zone time Zone.Flow evaluates at.
                    var zoneScroll = zone.FlowScroll;
                    var lowered = Shader.GetGlobalVector("_FlowScroll");
                    if (Math.Abs(lowered.x - zoneScroll.x) > 1e-6 || Math.Abs(lowered.y - zoneScroll.y) > 1e-6)
                        Fail($"{label}-lowered-scroll", $"at zone time {elapsed:F3} the shader got _FlowScroll ({lowered.x}, {lowered.y}), the zone's is ({zoneScroll.x}, {zoneScroll.y})");
                    if (Math.Abs(Shader.GetGlobalFloat("_FlowCycle") - zone.CyclePhase(flow.Period)) > 1e-6)
                        Fail($"{label}-lowered-cycle", $"at zone time {elapsed:F3} the shader got _FlowCycle {Shader.GetGlobalFloat("_FlowCycle")}, the zone's phase is {zone.CyclePhase(flow.Period)}");
                    if (Shader.GetGlobalFloat("_FlowScale") != flow.GlobalScale || Shader.GetGlobalFloat("_FlowAmplitude") != flow.GlobalAmplitude ||
                        Shader.GetGlobalFloat("_FlowSlopeAmplitude") != flow.SlopeAmplitude ||
                        Shader.GetGlobalFloat("_FlowSwirlAmplitude") != flow.SwirlAmplitude || Shader.GetGlobalFloat("_FlowPeriod") != flow.Period)
                        Fail($"{label}-lowered-settings", "a flow uniform does not equal the zone's FlowSettings");

                    compute.Dispatch(kernel, (count + 63) / 64, 1, 1);
                    var globalOut = new Vector3[count];
                    var slopeOut = new Vector3[count];
                    var gatedOut = new Vector3[count];
                    globalBuffer.GetData(globalOut);
                    slopeBuffer.GetData(slopeOut);
                    gatedBuffer.GetData(gatedOut);

                    var at = $"{label} zone time {elapsed:F2} scroll {zoneScroll.x:F4}";
                    for (var i = 0; i < positionCount; i++)
                    {
                        var p = positions[i];
                        var expected = FlowField.Global(flow, new CultMath.float3(p.x, p.y, p.z), zoneScroll);
                        Compare($"{label}-global", at, i, new Vector3(expected.x, expected.y, expected.z), globalOut[i], flow.GlobalAmplitude);
                    }
                    // flow() is the keyword-gated function the nebula and stardust call: the global term plus, under
                    // FLOW_SLOPE, the slope term of the ramp's gradient. A flow that is live must not read zero.
                    var live = 0;
                    var gatedScale = Math.Max(Math.Abs(flow.GlobalAmplitude), Math.Max(Math.Abs(flow.SlopeAmplitude), Math.Abs(flow.SwirlAmplitude)));
                    for (var i = 0; i < positionCount; i++)
                    {
                        var p = positions[i];
                        var expected = FlowField.At(flow, new CultMath.float3(p.x, p.y, p.z), zoneScroll, rampGradient);
                        var e = new Vector3(expected.x, expected.y, expected.z);
                        if (e.magnitude > .1f * gatedScale) live++;
                        Compare($"{label}-flow-keywords", at, i, e, gatedOut[i], gatedScale);
                    }
                    if (gatedScale != 0f && live < positionCount / 4)
                        Fail($"{label}-flow-keywords", $"{at}: only {live} of {positionCount} zone flows are live; the check is vacuous");
                    var slopeScale = Math.Max(Math.Abs(flow.SlopeAmplitude), Math.Abs(flow.SwirlAmplitude));
                    for (var i = 0; i < gradientCount; i++)
                    {
                        var g = gradients[i];
                        var expected = FlowField.Slope(flow, new CultMath.float2(g.x, g.y));
                        Compare($"{label}-slope", at, i, new Vector3(expected.x, expected.y, expected.z), slopeOut[i], slopeScale);
                    }
                }
            }
            finally
            {
                positionBuffer.Release(); gradientBuffer.Release(); globalBuffer.Release(); slopeBuffer.Release(); gatedBuffer.Release(); noiseBuffer?.Release(); UnityEngine.Object.DestroyImmediate(height);
            }
        }

        // The nebula noise the flow advects evolves on the flow clock VolumeSampling lowers as _NebulaNoiseTime, never on
        // _Time: with the speed non-zero, the noise changes when _NebulaNoiseTime does and not otherwise (a frame clock
        // would not follow the global set here).
        static void CheckNoiseClock(ComputeShader compute)
        {
            const int count = 64;
            var kernel = compute.FindKernel("Evaluate");
            var rng = new System.Random(4217);
            var positions = new Vector3[count];
            for (var i = 0; i < count; i++)
                positions[i] = new Vector3((float) (rng.NextDouble() * 4000 - 2000), (float) (rng.NextDouble() * 100 - 50), (float) (rng.NextDouble() * 4000 - 2000));
            var buffers = new[] { new ComputeBuffer(count, 12), new ComputeBuffer(count, 8), new ComputeBuffer(count, 12), new ComputeBuffer(count, 12), new ComputeBuffer(count, 12), new ComputeBuffer(count, 4) };
            var height = new Texture2D(1, 1, TextureFormat.RFloat, false);
            try
            {
                buffers[0].SetData(positions);
                buffers[1].SetData(new Vector2[count]);
                compute.SetBuffer(kernel, "positions", buffers[0]);
                compute.SetBuffer(kernel, "gradients", buffers[1]);
                compute.SetBuffer(kernel, "globalOut", buffers[2]);
                compute.SetBuffer(kernel, "slopeOut", buffers[3]);
                compute.SetBuffer(kernel, "gatedOut", buffers[4]);
                compute.SetBuffer(kernel, "noiseOut", buffers[5]);
                compute.SetTexture(kernel, "_NebulaSurfaceHeight", height);
                compute.SetInt("count", count);
                Shader.SetGlobalFloat("_NebulaNoiseSpeed", 1f);
                float[] Noise(float noiseTime)
                {
                    Shader.SetGlobalFloat("_NebulaNoiseTime", noiseTime);
                    compute.Dispatch(kernel, 1, 1, 1);
                    var result = new float[count];
                    buffers[5].GetData(result);
                    return result;
                }
                var a = Noise(0f);
                var b = Noise(3f);
                var again = Noise(3f);
                int moved = 0, repeated = 0;
                for (var i = 0; i < count; i++)
                {
                    if (Math.Abs(a[i] - b[i]) > 1e-4f) moved++;
                    if (b[i] == again[i]) repeated++;
                }
                if (moved < count / 2) Fail("nebula-noise-clock", $"only {moved} of {count} noise samples follow _NebulaNoiseTime");
                if (repeated != count) Fail("nebula-noise-clock", $"{count - repeated} of {count} noise samples changed with _NebulaNoiseTime held");
            }
            finally
            {
                foreach (var buffer in buffers) buffer.Release();
                UnityEngine.Object.DestroyImmediate(height);
            }
        }

        // Error is measured against the term's amplitude; a term with no amplitude must be exactly zero and never NaN.
        static void Compare(string check, string at, int index, Vector3 expected, Vector3 actual, float amplitude)
        {
            var error = (expected - actual).magnitude;
            if (float.IsNaN(error)) { Fail(check, $"{at} sample {index}: NaN (expected {expected}, shader {actual})"); return; }
            if (amplitude != 0f) _maxError = Math.Max(_maxError, error / Math.Abs(amplitude));
            var tolerance = RelativeTolerance * Math.Abs(amplitude);
            if (error > tolerance)
                Fail(check, $"{at} sample {index}: expected {expected}, shader {actual}, error {error:G4} > {tolerance:G4}");
        }
    }
}
