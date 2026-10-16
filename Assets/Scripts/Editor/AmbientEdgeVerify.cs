using System;
using System.IO;
using GameCult.Caching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Batchmode check for campaign aetheria-gravity, cut ambient-follows-populated-area: the ambient tint falls softly to
    /// dark and never hits an edge (ruling dark-beyond-populated-area). It opens ARPG.unity, takes the Ambient Fog Light the
    /// scene wires into ZoneRenderer, sizes it for a generated zone with ZoneRenderer.FitAmbientLight (the call LoadZone
    /// makes, not a copy), renders it through an orthographic camera that culls its layer into a float target, and checks the
    /// radial profile against exp(-(r/P)^2 ln(1/_EdgeBrightness)) times the centre out to 2 P (P is Zone.PopulatedRadius),
    /// that it is lit at Pack.Radius, that the quad's rim is at most 2e-5 of the centre, and that no texel is more than half
    /// a decade from its neighbour.
    ///
    /// Renders, so it needs a graphics device: run WITHOUT -nographics.
    ///   Unity -batchmode -projectPath &lt;repo&gt; -executeMethod Aetheria.EditorTools.AmbientEdgeVerify.Run -logFile &lt;path&gt;
    /// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
    /// </summary>
    public static class AmbientEdgeVerify
    {
        const string Tag = "[AmbientEdgeVerify]";
        const int Size = 512;
        const int AmbientLayer = 13;
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
            var work = Path.Combine(Path.GetTempPath(), "ambient-edge-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(work, "probe"));
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset")
                    ?? throw new InvalidOperationException("No Settings.asset.");
                // Generated zones have the populated radius at about .78 of the pack radius (p50 over 1500 zones): this one's
                // pack radius is set so, which puts Pack.Radius where it sits in play, inside the quad's reach.
                var probe = GravityFieldParityVerify.BuildZone(settings, Path.Combine(work, "probe"), out _);
                var zone = GravityFieldParityVerify.BuildZone(settings, work, out _, probe.PopulatedRadius / .78f);
                EditorSceneManager.OpenScene("Assets/Scenes/ARPG.unity", OpenSceneMode.Single);
                try
                {
                    var renderer = UnityEngine.Object.FindFirstObjectByType<ZoneRenderer>(FindObjectsInactive.Include);
                    if (renderer == null || renderer.AmbientLight == null) throw new InvalidOperationException("ARPG.unity wires no ZoneRenderer.AmbientLight.");
                    Check(renderer, zone);
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
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

        // A bare ambient quad for the verifies that run LoadZone on a renderer built in code: the scene's material, no scene.
        public static Transform NewAmbientLight()
        {
            var quad = new GameObject("Ambient Fog Light") { layer = AmbientLayer };
            quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            quad.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Brushes/Sector Tint.mat");
            return quad.transform;
        }

        static void Check(ZoneRenderer renderer, Zone zone)
        {
            var light = renderer.AmbientLight;
            if (light.parent != null) Fail("wiring", $"the ambient quad sits under {light.parent.name}, whose scale is the pack radius's");
            if (light.gameObject.layer != AmbientLayer) Fail("wiring", $"the ambient quad is on layer {light.gameObject.layer}, not {AmbientLayer}");

            ZoneRenderer.FitAmbientLight(light, zone);
            var meshRenderer = light.GetComponent<MeshRenderer>();
            var material = meshRenderer.sharedMaterial;
            var edge = material.GetFloat("_EdgeBrightness");
            var centre = material.GetFloat("_Depth") * material.GetColor("_Color").linear.r;
            var populated = zone.PopulatedRadius;
            var half = light.localScale.x / 2f;
            Debug.Log($"{Tag} populated radius {populated:F1} u, pack radius {zone.Pack.Radius:F1} u, quad half-extent {half:F1} u, edge brightness {edge:G3}, centre {centre:G4}");
            if (!(populated > 0f) || !(half > populated)) { Fail("sizing", $"the quad half-extent {half:F1} u does not reach beyond the populated radius {populated:F1} u"); return; }

            Render(light, half * 1.05f, out var value, out var world);
            var logFall = Mathf.Log(1f / edge);
            foreach (var k in new[] { 0f, .5f, 1f, 1.5f, 2f })
            {
                var drawn = Sample(value, world, new Vector2(k * populated, 0f));
                Debug.Log($"{Tag} r = {k} P: drawn {drawn:G5}, expected {centre * Mathf.Exp(-k * k * logFall):G5}");
            }
            var worstProfile = 0f;
            var compared = 0;
            var rim = 0f;
            for (var i = 0; i < value.Length; i++)
            {
                if (float.IsNaN(value[i])) { Fail("texels", $"texel {i}: NaN"); return; }
                var r = world[i].magnitude;
                if (r <= 2f * populated)
                {
                    var expected = centre * Mathf.Exp(-(r / populated) * (r / populated) * logFall);
                    var error = Mathf.Abs(value[i] - expected) / expected;
                    worstProfile = Mathf.Max(worstProfile, error);
                    compared++;
                    if (error > .01f) { Fail("profile", $"at r = {r / populated:F3} P: expected {expected:G5}, drawn {value[i]:G5}, off by {error * 100f:F2}%"); return; }
                }
                // The rim: the texels in the quad's outermost ring (within two texels of its edge).
                var edgeDistance = half - Mathf.Max(Mathf.Abs(world[i].x), Mathf.Abs(world[i].y));
                if (edgeDistance >= 0f && edgeDistance < 2f * (half * 1.05f * 2f / Size)) rim = Mathf.Max(rim, value[i]);
            }
            Debug.Log($"{Tag} profile: {compared} texels within 2 P, worst error {worstProfile * 100f:F3}%; rim {rim / centre:E2} of the centre");
            if (compared < Size * Size / 8) Fail("profile", $"only {compared} texels were within 2 P; the check is vacuous");
            if (rim > 2e-5f * centre) Fail("rim", $"the quad's rim is {rim / centre:E2} of the centre, above 2e-5");

            var atPack = Sample(value, world, new Vector2(zone.Pack.Radius, 0f));
            Debug.Log($"{Tag} at Pack.Radius: {atPack / centre:E2} of the centre");
            if (!(atPack > 0f)) Fail("pack-radius", "the ambient is zero at Pack.Radius");

            var worstJump = 0f;
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var here = value[y * Size + x];
                if (here <= 0f) continue;
                foreach (var next in new[] { x + 1 < Size ? value[y * Size + x + 1] : 0f, y + 1 < Size ? value[(y + 1) * Size + x] : 0f })
                {
                    if (next <= 0f) continue;
                    worstJump = Mathf.Max(worstJump, Mathf.Abs(Mathf.Log10(here / next)));
                }
            }
            Debug.Log($"{Tag} largest jump between neighbouring lit texels: {worstJump:F3} decades (limit 0.5)");
            if (worstJump > .5f) Fail("jump", $"neighbouring texels differ by {worstJump:F2} decades");
            var lit = 0;
            foreach (var v in value) if (v > 0f) lit++;
            if (lit < Size * Size / 2) Fail("jump", $"only {lit} texels are lit; the check is vacuous");
        }

        static float Sample(float[] value, Vector2[] world, Vector2 at)
        {
            var best = 0;
            for (var i = 1; i < world.Length; i++)
                if ((world[i] - at).sqrMagnitude < (world[best] - at).sqrMagnitude) best = i;
            return value[best];
        }

        // Renders the ambient layer through a downward orthographic camera centred on the origin, as the Tint cameras see it,
        // and returns the value of every texel with the world xz of its centre, which is asked of the camera.
        static void Render(Transform light, float half, out float[] value, out Vector2[] world)
        {
            var rt = new RenderTexture(Size, Size, 0, RenderTextureFormat.RFloat) { filterMode = FilterMode.Point };
            var cameraObject = new GameObject("Ambient Edge Camera");
            var read = new Texture2D(Size, Size, TextureFormat.RFloat, false);
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = half;
                camera.aspect = 1f;
                camera.nearClipPlane = .3f;
                camera.farClipPlane = 1000f;
                camera.cullingMask = 1 << light.gameObject.layer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = rt;
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 100f, 0f), Quaternion.Euler(90f, 0f, 0f));
                camera.Render();

                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                read.Apply();
                value = read.GetPixelData<float>(0).ToArray();
                world = new Vector2[Size * Size];
                for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                {
                    var p = camera.ViewportToWorldPoint(new Vector3((x + .5f) / Size, (y + .5f) / Size, 100f));
                    world[y * Size + x] = new Vector2(p.x, p.z);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(read);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
