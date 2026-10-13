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
        const float RelativeTolerance = 1e-3f;
        static float _maxError;
        static bool _ok = true;

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

        static Zone BuildZone(GameSettings settings, string work, FlowSettings flow)
        {
            var cache = AetheriaStores.Open(Path.Combine(work, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.FlushAsync().Wait();
            var items = new ItemManager(cache, new ProvenanceLedger(), settings.GameplaySettings, _ => { });
            return new Zone(items, new PlanetSettings { Flow = flow }, new ZonePack(), new GalaxyZone { Name = "Parity", Owner = null }, null);
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
            try
            {
                positionBuffer.SetData(positions);
                gradientBuffer.SetData(gradients);
                compute.SetBuffer(kernel, "positions", positionBuffer);
                compute.SetBuffer(kernel, "gradients", gradientBuffer);
                compute.SetBuffer(kernel, "globalOut", globalBuffer);
                compute.SetBuffer(kernel, "slopeOut", slopeBuffer);
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
                    var expectedScroll = flow.GlobalScrollSpeed * elapsed;
                    var scrollTolerance = 1e-4 * Math.Max(1.0, expectedScroll);
                    if (Math.Abs(zone.FlowScroll - expectedScroll) > scrollTolerance)
                        Fail($"{label}-zone-scroll", $"at zone time {elapsed:F3} FlowScroll {zone.FlowScroll} but speed x time is {expectedScroll}");

                    VolumeSampling.LowerFlow(zone.Settings.Flow, zone.FlowScroll);

                    var lowered = Shader.GetGlobalFloat("_FlowScroll");
                    if (Math.Abs(lowered - expectedScroll) > scrollTolerance)
                        Fail($"{label}-lowered-scroll", $"at zone time {elapsed:F3} the shader got _FlowScroll {lowered}, speed x time is {expectedScroll}");
                    if (Shader.GetGlobalFloat("_FlowScale") != flow.GlobalScale || Shader.GetGlobalFloat("_FlowAmplitude") != flow.GlobalAmplitude ||
                        Shader.GetGlobalFloat("_FlowSlopeAmplitude") != flow.SlopeAmplitude ||
                        Shader.GetGlobalFloat("_FlowSwirlAmplitude") != flow.SwirlAmplitude || Shader.GetGlobalFloat("_FlowPeriod") != flow.Period)
                        Fail($"{label}-lowered-settings", "a flow uniform does not equal the zone's FlowSettings");

                    compute.Dispatch(kernel, (count + 63) / 64, 1, 1);
                    var globalOut = new Vector3[count];
                    var slopeOut = new Vector3[count];
                    globalBuffer.GetData(globalOut);
                    slopeBuffer.GetData(slopeOut);

                    var zoneScroll = zone.FlowScroll;
                    var at = $"{label} zone time {elapsed:F2} scroll {zoneScroll:F4}";
                    for (var i = 0; i < positionCount; i++)
                    {
                        var p = positions[i];
                        var expected = FlowField.Global(flow, new CultMath.float3(p.x, p.y, p.z), zoneScroll);
                        Compare($"{label}-global", at, i, new Vector3(expected.x, expected.y, expected.z), globalOut[i], flow.GlobalAmplitude);
                    }
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
                positionBuffer.Release(); gradientBuffer.Release(); globalBuffer.Release(); slopeBuffer.Release();
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
