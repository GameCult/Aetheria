using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using CultMath;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Batchmode check for campaign aetheria-gravity, cut grid-lowering: what the Gravity Field pass draws is the sim's
    /// height. It builds a zone from Settings.asset's PlanetSettings with three bodies, runs the real lowering
    /// (GravityLowering.Lower and CreateField), renders the field through an orthographic camera that culls layer 9 into
    /// a float target, and compares every texel with -Zone.GetHeight at the texel centre's world position. It then checks
    /// that the texture's gradient points where Zone.GetForce does, and that ARPG.unity and the body prefabs are wired
    /// the way the cut leaves them (the gravity cameras cull layer 9 alone, nothing but the run-time field sits on it).
    ///
    /// Renders, so it needs a graphics device: run WITHOUT -nographics (as FlowFieldParityVerify).
    ///   Unity -batchmode -projectPath &lt;repo&gt; -executeMethod Aetheria.EditorTools.GravityFieldParityVerify.Run -logFile &lt;path&gt;
    /// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
    /// </summary>
    public static class GravityFieldParityVerify
    {
        const string Tag = "[GravityFieldParityVerify]";
        const int Size = 256;
        // Measured on this tree (the "max error" line this run prints): the largest |texel + GetHeight| over the windows of the three
        // frames and windows is 0.0065 u, in the last frame's planetoid window (float32 on both sides, the GPU's log against the CPU's, the
        // quad's interpolated world position). The tolerance is 4.6x that. A 0.1% error in the tail constant (.5 -> .501)
        // moves the drawn height by 0.053 u where all three tails reach (the planetoid and zone windows), 1.8x the
        // tolerance; a reference of zero moves it by about 150 u.
        const float Tolerance = 0.03f;
        // The worst angle between the texture's central difference and GetForce (B28: 1.0 degrees median zone, 2.9 worst).
        const float GradientDegrees = 3f;
        static float _maxError, _maxAngle;
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
            var work = Path.Combine(Path.GetTempPath(), "gravity-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            ComputeBuffer wells = null;
            GameObject field = null;
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset")
                    ?? throw new InvalidOperationException("No Settings.asset.");
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Brushes/Gravity Field.mat")
                    ?? throw new InvalidOperationException("No Gravity Field.mat.");

                var zone = BuildZone(settings, work, out var orbits);
                GravityLowering.Lower(zone, 0f, ref wells);
                field = GravityLowering.CreateField(material);

                // Three frames through the one buffer, as ZoneRenderer.Update runs it: the bodies as built, then two moves
                // (an orbit's Position is what Zone.GetHeight reads). A lowering that mirrors an axis or stops refreshing
                // the upload agrees with the sim in the first frame only where it is symmetric, and not after a move.
                CheckFrame("built", zone, orbits);
                orbits[1].Position = new float2(-420f, 560f);
                orbits[2].Position = new float2(900f, -700f);
                GravityLowering.Lower(zone, 0f, ref wells);
                CheckFrame("moved", zone, orbits);
                orbits[0].Position = new float2(210f, -95f);
                orbits[2].Position = new float2(-1800f, 1300f);
                GravityLowering.Lower(zone, 0f, ref wells);
                CheckFrame("moved-again", zone, orbits);
                Debug.Log($"{Tag} max error {_maxError:G4} u (tolerance {Tolerance:G4}); max gradient angle {_maxAngle:F2} degrees (limit {GradientDegrees:F0})");

                CheckWiring(settings);
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
                if (field != null) UnityEngine.Object.DestroyImmediate(field);
                wells?.Release();
                try { Directory.Delete(work, true); } catch { }
            }
            EditorApplication.Exit(_ok ? 0 : 1);
        }

        static float Core(Zone zone, float mass) => zone.Settings.RocheLimit.Evaluate(mass);

        static Vector2 At(Orbit orbit) => new Vector2(orbit.Position.x, orbit.Position.y);

        // Orbits 0-2 are the sun, the planet and the planetoid, each window centred where the body is now; orbit 3 is the
        // body of no strength. The zone window holds all four.
        static void CheckFrame(string frame, Zone zone, Orbit[] orbits)
        {
            CheckTexels(frame + "-planetoid", zone, At(orbits[2]), 4f * Core(zone, 29.2f));
            CheckTexels(frame + "-sun", zone, At(orbits[0]), 4f * Core(zone, 14152f));
            CheckTexels(frame + "-zone", zone, Vector2.zero, 1.05f * zone.Pack.Radius);
            CheckGradient(zone, orbits.Select(At).ToArray());
        }

        // A sun (14152 at (-130, 85)), a planet (273.9 at (610, -240)), a planetoid (29.2 at (670, -270)) and a body of
        // mass 0, whose well has no strength (a legal body; its curves give c = 0 and R = 0), at (1700, 300): parentless,
        // fixed, off both axes and asymmetric, so the zone's wells sit where the verify says before any Update and a
        // mirrored or swapped axis cannot agree with the sim.
        static Zone BuildZone(GameSettings settings, string work, out Orbit[] orbits)
        {
            var cache = AetheriaStores.Open(Path.Combine(work, "catalog.cc"), Path.Combine(work, "run.cc"), catalogWritable: true);
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.FlushAsync().Wait();
            var items = new ItemManager(cache, new ProvenanceLedger(), settings.GameplaySettings, _ => { });
            var positions = new[] { new Vector2(-130f, 85f), new Vector2(610f, -240f), new Vector2(670f, -270f), new Vector2(1700f, 300f) };
            var masses = new[] { 14152f, 273.9f, 29.2f, 0f };
            var keys = new CultRecordKey[positions.Length];
            var pack = new ZonePack { Radius = 4182f };
            for (var i = 0; i < positions.Length; i++)
            {
                var orbit = keys[i] = cache.Upsert(new OrbitData { FixedPosition = new float2(positions[i].x, positions[i].y) }).Key;
                var body = cache.Upsert(new PlanetData
                    { Name = "Body " + i, Orbit = new CultRecordRef<OrbitData>(orbit), Mass = masses[i] }).Key;
                pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
                pack.Planets.Add(new CultRecordRef<BodyData>(body));
            }
            var zone = new Zone(items, settings.PlanetSettings, pack, new GalaxyZone { Name = "Gravity Parity", Owner = null }, null);
            orbits = keys.Select(k => zone.Orbits[k]).ToArray();
            return zone;
        }

        // Renders the field through a downward orthographic camera that culls layer 9 only, centred on `centre` with
        // half-size `half`, and returns the depth of every texel with the world xz of the texel's centre, which is asked
        // of the camera rather than assumed.
        static void Render(Vector2 centre, float half, out float[] depth, out Vector2[] world)
        {
            var rt = new RenderTexture(Size, Size, 0, RenderTextureFormat.RFloat) { filterMode = FilterMode.Point };
            var cameraObject = new GameObject("Gravity Parity Camera");
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
                camera.cullingMask = 1 << GravityLowering.FieldLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = rt;
                cameraObject.transform.SetPositionAndRotation(new Vector3(centre.x, 100f, centre.y), Quaternion.Euler(90f, 0f, 0f));
                camera.Render();

                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                read.Apply();
                depth = read.GetPixelData<float>(0).ToArray();
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

        static void CheckTexels(string window, Zone zone, Vector2 centre, float half)
        {
            Render(centre, half, out var depth, out var world);
            var worst = 0f;
            var live = 0;
            for (var i = 0; i < depth.Length; i++)
            {
                var expected = -zone.GetHeight(new float2(world[i].x, world[i].y));
                if (float.IsNaN(depth[i])) { Fail($"{window}-texels", $"texel {i}: NaN"); return; }
                var error = Math.Abs(depth[i] - expected);
                worst = Math.Max(worst, error);
                if (Math.Abs(expected) > 1f) live++;
                if (error > Tolerance)
                {
                    Fail($"{window}-texels", $"texel {i} at ({world[i].x:F2}, {world[i].y:F2}): expected {expected:G6}, drawn {depth[i]:G6}, error {error:G4} > {Tolerance:G4}");
                    return;
                }
            }
            _maxError = Math.Max(_maxError, worst);
            // A window that draws nothing agrees with a zone that is flat: the check must be live.
            if (live < depth.Length / 2) Fail($"{window}-texels", $"only {live} of {depth.Length} texels are deeper than 1 u; the check is vacuous");
            Debug.Log($"{Tag} {window}: {depth.Length} texels, max error {worst:G4} u");
        }

        // The texture's central difference (over two 4 u texels, mid-zone) points where GetForce does: depth = -h, so its
        // gradient is the force over the exaggeration. This is what the slope readers (the shader's slopeFlow, the nebula
        // surface normal, the drawn grid) take from the texture.
        static void CheckGradient(Zone zone, Vector2[] bodies)
        {
            Render(new Vector2(1500f, 0f), 512f, out var depth, out var world);
            var compared = 0;
            for (var y = 1; y < Size - 1; y++)
            for (var x = 1; x < Size - 1; x++)
            {
                var here = world[y * Size + x];
                if (bodies.Any(b => (b - here).magnitude < 16f)) continue;
                // The world step of each texel axis is read from the camera, so a flipped or rotated target is still measured.
                var au = world[y * Size + x + 1] - world[y * Size + x - 1];
                var av = world[(y + 1) * Size + x] - world[(y - 1) * Size + x];
                var du = depth[y * Size + x + 1] - depth[y * Size + x - 1];
                var dv = depth[(y + 1) * Size + x] - depth[(y - 1) * Size + x];
                var det = au.x * av.y - au.y * av.x;
                var g = new Vector2((du * av.y - dv * au.y) / det, (dv * au.x - du * av.x) / det);
                var f = zone.GetForce(new float2(here.x, here.y));
                var angle = Vector2.Angle(g, new Vector2(f.x, f.y));
                if (float.IsNaN(angle) || angle > GradientDegrees)
                {
                    Fail("gradient", $"at ({here.x:F1}, {here.y:F1}) the drawn slope points {angle:F2} degrees from the force (limit {GradientDegrees:F0})");
                    return;
                }
                _maxAngle = Math.Max(_maxAngle, angle);
                compared++;
            }
            if (compared < Size * Size / 2) Fail("gradient", $"only {compared} texels were compared");
        }

        // Main Menu.unity has no ZoneRenderer, so no Gravity Field and no lowered potential: its gravity cameras draw the
        // menu's own brush objects (Boundary on layer 9, the Waves on layer 10) and are presentation by spec (follow-up
        // menu-grid-brushes), not a reader of the potential. This pins that: a ZoneRenderer in the menu would make its
        // cameras readers of the field, and they would have to cull layer 9 alone like ARPG's.
        static void CheckTestSceneWiring()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity", OpenSceneMode.Single);
            try
            {
                if (CheckPatchCameras("TestScene.unity") < 1) Fail("wiring", "no Fog Patch Displacement camera in TestScene.unity");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static void CheckMenuWiring()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity", OpenSceneMode.Single);
            try
            {
                if (UnityEngine.Object.FindFirstObjectByType<ZoneRenderer>(FindObjectsInactive.Include) != null)
                    Fail("wiring", "Main Menu.unity has a ZoneRenderer: its gravity cameras now read the Gravity Field and must cull layer 9 alone");
                var seen = 0;
                foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (camera.targetTexture == null || camera.targetTexture.name != "Fog Surface Displacement") continue;
                    seen++;
                    const int menuBrushLayers = (1 << 9) | (1 << 10);
                    if (camera.cullingMask != menuBrushLayers)
                        Fail("wiring", $"Main Menu camera {camera.name} culls mask {camera.cullingMask}, not the menu brush layers 9 and 10");
                }
                if (seen != 1) Fail("wiring", $"{seen} Fog Surface Displacement cameras in Main Menu.unity, expected 1");
                if (CheckPatchCameras("Main Menu.unity") < 1) Fail("wiring", "no Fog Patch Displacement camera in Main Menu.unity");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        // Every scene's Fog Patch Displacement camera: cloudDensity adds the R32 gravity height to the patch texture, so
        // a patch camera that culls the gravity layer counts the field twice. Returns how many it found in the open scene.
        static int CheckPatchCameras(string sceneName)
        {
            var found = 0;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.targetTexture == null || camera.targetTexture.name != "Fog Patch Displacement") continue;
                found++;
                if ((camera.cullingMask & 1 << GravityLowering.FieldLayer) != 0)
                    Fail("wiring", $"camera {camera.name} (target Fog Patch Displacement) in {sceneName} culls layer {GravityLowering.FieldLayer}, the gravity layer");
            }
            return found;
        }

        static void CheckWiring(GameSettings settings)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ARPG.unity", OpenSceneMode.Single);
            try
            {
                var gravityTargets = new[] { "Fog Surface Displacement", "Slime Gravity", "MinimapGravity" };
                var seen = 0;
                foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (camera.targetTexture == null || !gravityTargets.Contains(camera.targetTexture.name)) continue;
                    seen++;
                    if (camera.cullingMask != 1 << GravityLowering.FieldLayer)
                        Fail("wiring", $"camera {camera.name} (target {camera.targetTexture.name}) culls mask {camera.cullingMask}, not layer {GravityLowering.FieldLayer} alone");
                }
                if (seen < gravityTargets.Length) Fail("wiring", $"only {seen} gravity cameras found in ARPG.unity");

                // The patch texture is half-float: it holds the patch layers only, the nebula reads gravity from the R32 surface.
                if (CheckPatchCameras("ARPG.unity") != 1) Fail("wiring", "ARPG.unity does not have exactly 1 Fog Patch Displacement camera");

                // The map screen's gravity camera has no target until MapRenderer gives it one at run time.
                var map = UnityEngine.Object.FindFirstObjectByType<MapRenderer>(FindObjectsInactive.Include);
                if (map == null || map.GravityCamera == null) Fail("wiring", "no MapRenderer.GravityCamera in ARPG.unity");
                else if (map.GravityCamera.cullingMask != 1 << GravityLowering.FieldLayer)
                    Fail("wiring", $"MapRenderer.GravityCamera culls mask {map.GravityCamera.cullingMask}, not layer {GravityLowering.FieldLayer} alone");

                var renderer = UnityEngine.Object.FindFirstObjectByType<ZoneRenderer>(FindObjectsInactive.Include);
                if (renderer == null) Fail("wiring", "no ZoneRenderer in ARPG.unity");
                else if (renderer.GravityField == null || renderer.GravityField.shader.name != "Brushes/Gravity Field")
                    Fail("wiring", "ZoneRenderer.GravityField is not a Brushes/Gravity Field material");

                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (r.gameObject.layer == GravityLowering.FieldLayer)
                        Fail("wiring", $"renderer {r.name} in ARPG.unity sits on layer {GravityLowering.FieldLayer}");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            CheckMenuWiring();
            CheckTestSceneWiring();

            foreach (var path in new[]
            {
                "Assets/Prefabs/RPG/Planets/Planet.prefab", "Assets/Prefabs/RPG/Planets/Gas Giant.prefab",
                "Assets/Prefabs/RPG/Planets/Sun.prefab", "Assets/Prefabs/RPG/Wormhole 1.prefab"
            })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Fail("wiring", $"no prefab at {path}"); continue; }
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.layer == GravityLowering.FieldLayer)
                        Fail("wiring", $"renderer {r.name} in {path} sits on layer {GravityLowering.FieldLayer}");
            }
        }
    }
}
