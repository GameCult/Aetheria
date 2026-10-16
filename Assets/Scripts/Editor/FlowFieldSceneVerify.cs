using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Batchmode check for the flow as the scene runs it (campaign aetheria-fauna, cut fauna-flow-field; target
    /// invariants flow-one-owner and flow-shader-parity). FlowFieldParityVerify proves the HLSL against FlowField with
    /// no scene loaded; this opens ARPG.unity, lets VolumeSampling and Stardust initialise as the game does, drives
    /// their own Start and Update with a zone attached, and asserts what the presenters did:
    ///  - nothing threw and nothing logged an error (a type that cannot initialise in a scene, as VolumeSampling once
    ///    could when it created its keywords in a static field initializer, throws on its first use);
    ///  - FLOW_GLOBAL and FLOW_SLOPE are enabled, _FlowAmplitude is the zone's, and the nebula noise clock is the zone's;
    ///  - the stardust particle buffer changes with the flow and freezes when the zone pauses;
    ///  - CloudShader pass 0 carries the FLOW_* keywords.
    /// The scene is opened before any Aetheria type is touched: run this method alone in its own Unity session, never
    /// after FlowFieldParityVerify (which would initialise VolumeSampling outside a MonoBehaviour constructor).
    /// Dispatches compute and renders, so it needs a graphics device: run WITHOUT -nographics.
    ///   Unity -batchmode -projectPath &lt;repo&gt; -executeMethod Aetheria.EditorTools.FlowFieldSceneVerify.Run -logFile &lt;path&gt;
    /// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
    /// </summary>
    public static class FlowFieldSceneVerify
    {
        const string Tag = "[FlowFieldSceneVerify]";
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
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError($"{Tag} FAIL: no graphics device in this session; rerun batchmode without -nographics.");
                EditorApplication.Exit(1);
                return;
            }

            Application.logMessageReceived += OnLog;
            try
            {
                // Before anything else touches the project's types: the scene constructs its MonoBehaviours here.
                EditorSceneManager.OpenScene("Assets/Scenes/ARPG.unity", OpenSceneMode.Single);
                Check();
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
            }
            EditorApplication.Exit(_ok ? 0 : 1);
        }

        static void Check()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset")
                ?? throw new InvalidOperationException("No Settings.asset.");
            var agm = UnityEngine.Object.FindObjectsByType<ActionGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            var volume = UnityEngine.Object.FindObjectsByType<VolumeSampling>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            var dust = UnityEngine.Object.FindObjectsByType<Stardust>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (agm == null || volume == null || dust == null)
            {
                Fail("scene-objects", $"ARPG.unity has ActionGameManager={agm != null}, VolumeSampling={volume != null}, Stardust={dust != null}");
                return;
            }

            // Every flow term live, so FLOW_SLOPE is on; and the same flow with every amplitude zero.
            var on = new FlowSettings
            {
                GlobalScale = 512f, GlobalAmplitude = 15f, GlobalScrollSpeed = .025f, Period = 8f, SlopeAmplitude = 3f, SwirlAmplitude = 5f
            };
            var off = new FlowSettings { GlobalScale = on.GlobalScale, GlobalScrollSpeed = on.GlobalScrollSpeed, Period = on.Period };
            var work = Path.Combine(Path.GetTempPath(), "flow-scene-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var zone = FlowFieldParityVerify.BuildZone(settings, work, on, 86400.0 * 3 + 12.3);
                typeof(ActionGameManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { agm });
                typeof(ActionGameManager).GetProperty("Zone").GetSetMethod(true).Invoke(agm, new object[] { zone });
                if (agm.Settings == null) agm.Settings = settings;

                Call(volume, "Start");
                Call(volume, "Update");
                if (!Shader.IsKeywordEnabled("FLOW_GLOBAL")) Fail("scene-keywords", "FLOW_GLOBAL is not enabled after VolumeSampling.Update");
                if (!Shader.IsKeywordEnabled("FLOW_SLOPE")) Fail("scene-keywords", "FLOW_SLOPE is not enabled after VolumeSampling.Update");
                if (Shader.GetGlobalFloat("_FlowAmplitude") != on.GlobalAmplitude)
                    Fail("scene-amplitude", $"_FlowAmplitude is {Shader.GetGlobalFloat("_FlowAmplitude")}, the zone's flow has {on.GlobalAmplitude}");
                var scroll = Shader.GetGlobalVector("_FlowScroll");
                if (scroll.x != zone.FlowScroll.x || scroll.y != zone.FlowScroll.y)
                    Fail("scene-scroll", $"_FlowScroll ({scroll.x}, {scroll.y}) is not the zone's ({zone.FlowScroll.x}, {zone.FlowScroll.y})");
                var noiseTime = Shader.GetGlobalFloat("_NebulaNoiseTime");
                if (noiseTime != (float) zone.ExactTime)
                    Fail("scene-noise-clock", $"_NebulaNoiseTime is {noiseTime}, the zone's time is {(float) zone.ExactTime}");

                CheckCloudKeywords();

                // Stardust follows the flow through its own dispatch: the particle buffer read back each frame.
                Call(dust, "Start");
                var bufferField = typeof(Stardust).GetField("_particlesBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
                float[] Frame()
                {
                    Call(dust, "Update");
                    var buffer = (ComputeBuffer) bufferField.GetValue(dust);
                    var data = new float[buffer.count * 7];
                    buffer.GetData(data);
                    return data;
                }
                var flowing = Frame();
                VolumeSampling.LowerFlow(off, VolumeSampling.FlowClock);
                var still = Frame();
                if (Differing(flowing, still) == 0) Fail("scene-stardust", "the particle buffer is the same with the zone's flow and with no flow");

                // A paused zone freezes the particles and the nebula noise clock; a running one moves both.
                Call(volume, "Update");
                zone.Update(0f);
                Call(volume, "Update");
                if (Differing(flowing, Frame()) != 0) Fail("scene-stardust-pause", "the particle buffer moved while the zone was paused");
                if (Shader.GetGlobalFloat("_NebulaNoiseTime") != noiseTime) Fail("scene-noise-clock", "_NebulaNoiseTime moved while the zone was paused");
                zone.Update(.05f);
                Call(volume, "Update");
                if (Differing(flowing, Frame()) == 0) Fail("scene-stardust-run", "the particle buffer did not move while the zone ran");
                if (Shader.GetGlobalFloat("_NebulaNoiseTime") == noiseTime) Fail("scene-noise-clock", "_NebulaNoiseTime did not move while the zone ran");

                CheckStardustUncullable(dust, bufferField);
                CheckStardustUnderFog(dust, bufferField);
            }
            finally
            {
                try { Directory.Delete(work, true); } catch { }
            }
        }

        // The dust is a command buffer on the target camera, which no culling sees: with every particle 10000 below the
        // camera (far past any box a hand could inflate round it) a render still shows dust, and shows none when the
        // same particles have no size.
        static void CheckStardustUncullable(Stardust dust, FieldInfo bufferField)
        {
            var camera = dust.TargetCamera;
            if (camera == null) { Fail("scene-stardust-uncullable", "Stardust has no TargetCamera"); return; }
            var attached = camera.GetCommandBuffers(CameraEvent.BeforeImageEffectsOpaque).Count(buffer => buffer.name == "Stardust");
            if (attached != 1) Fail("scene-stardust-uncullable", $"{attached} Stardust command buffers on the target camera, expected 1");
            var late = camera.GetCommandBuffers(CameraEvent.BeforeForwardAlpha).Count(buffer => buffer.name == "Stardust");
            if (late != 0) Fail("scene-stardust-uncullable", $"{late} Stardust command buffers at BeforeForwardAlpha, expected 0");

            var particles = (ComputeBuffer) bufferField.GetValue(dust);
            float[] Layout(float size, Vector3 centre, float below) => DustLayout(particles.count, size, centre, below);

            var transform = camera.transform;
            var (position, rotation) = (transform.position, transform.rotation);
            var (far, flags, background, mask, target) = (camera.farClipPlane, camera.clearFlags, camera.backgroundColor, camera.cullingMask, camera.targetTexture);
            var texture = RenderTexture.GetTemporary(64, 64, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            // Image effects on the camera would write their own frame over the dust; only the dust draw is under test.
            var effects = camera.GetComponents<MonoBehaviour>().Where(behaviour => behaviour.enabled).ToArray();
            Color32[] Render(float size, Vector3 centre, float below)
            {
                Call(dust, "Update"); // on a bounded draw this queues the draw for the camera; here it dispatches the compute
                particles.SetData(Layout(size, centre, below));
                camera.Render();
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                readback.Apply();
                return readback.GetPixels32();
            }
            try
            {
                foreach (var effect in effects) effect.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1;
                camera.targetTexture = texture;
                // control: the camera where the scene put it, dust 100 below, inside any box a hand-set extent would give
                transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
                camera.farClipPlane = 1000f;
                var nearWith = Render(5f, position, 100f); var nearWithout = Render(0f, position, 100f);
                var nearDiff = 0; for (var i = 0; i < nearWith.Length; i++) if (!nearWith[i].Equals(nearWithout[i])) nearDiff++;
                Debug.Log($"{Tag} stardust near-control differing pixels={nearDiff}");
                if (nearDiff == 0) Fail("scene-stardust-uncullable", "control: dust 100 below the camera drew no pixels");
                transform.SetPositionAndRotation(new Vector3(0f, -40000f, 0f), Quaternion.Euler(90f, 0f, 0f));
                camera.farClipPlane = 30000f;
                var withDust = Render(400f, transform.position, 10000f); // the material scales a quad by distance^.33, so at this range it needs this size to cover pixels
                var without = Render(0f, transform.position, 10000f);
                var differing = 0;
                for (var i = 0; i < withDust.Length; i++)
                    if (!withDust[i].Equals(without[i])) differing++;
                if (differing == 0) Fail("scene-stardust-uncullable", "the dust 10000 below the camera drew no pixels");
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (var effect in effects) effect.enabled = true;
                camera.targetTexture = target;
                camera.cullingMask = mask;
                camera.backgroundColor = background;
                camera.clearFlags = flags;
                camera.farClipPlane = far;
                transform.SetPositionAndRotation(position, rotation);
                RenderTexture.ReleaseTemporary(texture);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        // The nebula is an [ImageEffectOpaque] that fogs the whole ray to a depth the dust never writes (B49): dust drawn
        // before it is attenuated by the fog, dust drawn after it sits on top at full strength. The dust's contribution to a
        // pixel (render with dust minus render without) is read with the cloud renderer off and on, in an HDR target.
        static void CheckStardustUnderFog(Stardust dust, FieldInfo bufferField)
        {
            const int Size = 128;
            var camera = dust.TargetCamera;
            var cloud = camera == null ? null : camera.GetComponent<VolumeCloudRenderer>();
            if (cloud == null) { Fail("scene-stardust-under-fog", "the target camera has no VolumeCloudRenderer"); return; }
            var particles = (ComputeBuffer) bufferField.GetValue(dust);

            var transform = camera.transform;
            var (position, rotation) = (transform.position, transform.rotation);
            var (far, flags, background, mask, target, hdr) = (camera.farClipPlane, camera.clearFlags, camera.backgroundColor, camera.cullingMask, camera.targetTexture, camera.allowHDR);
            var texture = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGBHalf);
            var readback = new Texture2D(Size, Size, TextureFormat.RGBAHalf, false);
            var previous = RenderTexture.active;
            var effects = camera.GetComponents<MonoBehaviour>().Where(behaviour => behaviour.enabled).ToArray();
            var fields = new System.Collections.Generic.List<Texture2D>();
            float[] Render(bool fog, float size)
            {
                cloud.enabled = fog;
                for (var frame = 0; frame < 2; frame++) // the cloud's first frame forces ULTRA and fills its history; the second is the settled one
                {
                    Call(dust, "Update");
                    particles.SetData(DustLayout(particles.count, size, position, 100f));
                    camera.Render();
                }
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readback.Apply();
                return readback.GetPixels().Select(pixel => pixel.r * .2126f + pixel.g * .7152f + pixel.b * .0722f).ToArray();
            }
            try
            {
                foreach (var effect in effects) effect.enabled = false;
                camera.allowHDR = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1;
                camera.targetTexture = texture;
                camera.farClipPlane = 1000f;
                transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));

                // The scene's own nebula fields are textures the game renders; here a uniform fill of white fog stands in for them.
                foreach (var (name, format) in new[] { ("_NebulaSurfaceHeight", TextureFormat.RFloat), ("_NebulaPatchHeight", TextureFormat.RHalf), ("_NebulaPatch", TextureFormat.RHalf) })
                    Shader.SetGlobalTexture(name, Uniform(format, Color.black, fields));
                Shader.SetGlobalTexture("_NebulaTint", Uniform(TextureFormat.RGBA32, Color.white, fields));
                Shader.DisableKeyword("NOISE_SLOPE");
                Shader.SetGlobalFloat("_NebulaFillDensity", .01f);
                Shader.SetGlobalFloat("_NebulaFillDistance", 120f);
                Shader.SetGlobalFloat("_NebulaFillExponent", 0f);
                Shader.SetGlobalFloat("_NebulaFillOffset", 0f);
                Shader.SetGlobalFloat("_NebulaPatchDensity", 0f);
                Shader.SetGlobalFloat("_NebulaLuminance", 1f);
                Shader.SetGlobalFloat("_ExtinctionCoefficient", .5f);
                Shader.SetGlobalFloat("_SafetyDistance", 30f);
                Shader.SetGlobalFloat("_TintLodExponent", 0f);

                var clearBare = Render(false, 0f);
                var clearDust = Render(false, 5f);
                var fogBare = Render(true, 0f);
                var fogDust = Render(true, 5f);

                var fogShown = 0f;
                var (clearSum, fogSum, drawn) = (0f, 0f, 0);
                for (var i = 0; i < clearBare.Length; i++)
                {
                    fogShown += Mathf.Abs(fogBare[i] - clearBare[i]) / clearBare.Length;
                    if (clearDust[i] == clearBare[i]) continue;
                    drawn++; clearSum += clearDust[i] - clearBare[i]; fogSum += fogDust[i] - fogBare[i];
                }
                var (clearMean, fogMean) = (drawn == 0 ? 0f : clearSum / drawn, drawn == 0 ? 0f : fogSum / drawn);
                Debug.Log($"{Tag} stardust under fog: fog control mean |difference|={fogShown}, dust pixels={drawn}, dust contribution cloud off={clearMean}, cloud on={fogMean}, ratio={(clearMean == 0f ? 0f : fogMean / clearMean)}");
                if (fogShown < 1e-3f) Fail("scene-stardust-under-fog", "control: the cloud renderer changed no pixels, so no fog is in view");
                else if (drawn == 0 || clearMean <= 0f) Fail("scene-stardust-under-fog", "the dust drew no pixels with the cloud renderer off");
                else if (fogMean > .9f * clearMean) Fail("scene-stardust-under-fog", "the dust is not attenuated by the fog in front of it: it was drawn after the nebula composites");
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (var effect in effects) effect.enabled = true;
                camera.allowHDR = hdr;
                camera.targetTexture = target;
                camera.cullingMask = mask;
                camera.backgroundColor = background;
                camera.clearFlags = flags;
                camera.farClipPlane = far;
                transform.SetPositionAndRotation(position, rotation);
                RenderTexture.ReleaseTemporary(texture);
                UnityEngine.Object.DestroyImmediate(readback);
                foreach (var field in fields) UnityEngine.Object.DestroyImmediate(field);
            }
        }

        static Texture2D Uniform(TextureFormat format, Color colour, System.Collections.Generic.List<Texture2D> owner)
        {
            var texture = new Texture2D(1, 1, format, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            owner.Add(texture);
            return texture;
        }

        // A square patch of particles 4 apart, `below` under `centre`.
        static float[] DustLayout(int count, float size, Vector3 centre, float below)
        {
            var side = Mathf.RoundToInt(Mathf.Sqrt(count));
            var data = new float[count * 7];
            for (var i = 0; i < count; i++)
            {
                data[i * 7 + 0] = centre.x + ((i % side) - side / 2f) * 4f;
                data[i * 7 + 1] = centre.y - below;
                data[i * 7 + 2] = centre.z + ((i / side) - side / 2f) * 4f;
                data[i * 7 + 3] = data[i * 7 + 4] = data[i * 7 + 5] = 1f;
                data[i * 7 + 6] = size;
            }
            return data;
        }

        // CloudShader's pass 0 compiles with the flow keywords (it must #include_with_pragmas the shared include).
        static void CheckCloudKeywords()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Raymarching/CloudShader.shader");
            if (shader == null) { Fail("scene-cloud-keywords", "No CloudShader.shader."); return; }
            var keywords = ShaderUtil.GetPassKeywords(shader, new PassIdentifier(0, 0), UnityEditor.Rendering.ShaderType.Fragment)
                .Select(keyword => keyword.name).ToArray();
            foreach (var name in new[] { "FLOW_GLOBAL", "FLOW_SLOPE" })
                if (!keywords.Contains(name)) Fail("scene-cloud-keywords", $"CloudShader pass 0 does not carry {name}");
        }

        static int Differing(float[] a, float[] b)
        {
            var count = 0;
            for (var i = 0; i < a.Length; i++)
                if (Math.Abs(a[i] - b[i]) > 1e-4f) count++;
            return count;
        }

        // The presenters' own Start and Update, as the game calls them; an exception is a failure that names the type.
        static void Call(object target, string method)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (info == null) { Fail("scene-presenter-exception", $"{target.GetType().Name}.{method} not found"); return; }
            try { info.Invoke(target, null); }
            catch (TargetInvocationException e)
            {
                Fail("scene-presenter-exception", $"{target.GetType().Name}.{method} threw {e.InnerException?.GetType().Name}");
            }
        }
    }
}
