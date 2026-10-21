using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using float2 = CultMath.float2;
using float4 = CultMath.float4;

// End-to-end proof of the belt draw (rulings asteroid-tops-at-layer, rock-meshes-by-size), measured where it fails: two belts are loaded
// into a ZoneRenderer carrying ARPG.unity's real AsteroidMeshes (meshes, stored radii) and Settings.asset, over a zone whose wells make the
// field differ rock to rock, and ZoneRenderer.DrawBelts runs as Update runs it. Every instanced matrix it hands to the GPU is read back with the
// mesh it is drawn with (ZoneRenderer.DrawInstanced), and for each drawn rock the verify measures, from the mesh's real vertices:
//   - the farthest vertex from the rock's centre after the matrix (the reach, a bound on the rock's top under any rotation) is the rock's
//     gameplay radius, and centre y + reach is the floater layer over the field at the rock's xz and the drawn time;
//   - the rock is drawn exactly once, on exactly one matrix of a real pose (no spare instance in a batch);
//   - within a belt a bigger rock never takes a smaller mesh (by the mesh's own farthest vertex), and the belt keeps min(AsteroidMeshCount, meshes) meshes.
// The arguments the renderer hands the draw list (radius field, belt, batch size, kept count) are not inspected: their consequence is.
//
// Reached: ZoneRenderer.LoadPlanet for each belt, DrawBelts. Not reached: LoadZone's other planets (their prefabs), the GPU draw itself.
// Needs a GPU device for MapPresenter's belt buffer: run without -nographics.
//
//   Unity -batchmode -projectPath <project> -executeMethod Aetheria.EditorTools.BeltFloaterLayerVerify.Run -logFile <log>
//
// Do not pass -quit: Run enters play mode (a domain reload), the check runs there, and the Editor exits itself, 0 on success and 1 on any failure.
namespace Aetheria.EditorTools
{
    [InitializeOnLoad]
    public static class BeltFloaterLayerVerify
    {
        private const string Tag = "[BeltFloaterLayerVerify]";
        private const string Flag = "BeltFloaterLayerVerify.running";
        private const string Wiring = "BeltFloaterLayerVerify.wiring";
        private const double Budget = 300; // real seconds
        private const float Lead = .008f, Layer = -1.5f, Tolerance = .02f;
        private static IEnumerator _check;
        private static double _started;
        private static string _work;

        static BeltFloaterLayerVerify()
        {
            if (SessionState.GetBool(Flag, false)) EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void Run()
        {
            try
            {
                SessionState.SetString(Wiring, ReadWiring());
                SessionState.SetBool(Flag, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            _work = Path.Combine(Path.GetTempPath(), $"belt-floater-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_work);
            _started = Time.realtimeSinceStartupAsDouble;
            _check = Check();
            EditorApplication.update += Step;
        }

        private static void Step()
        {
            try
            {
                if (Time.realtimeSinceStartupAsDouble - _started > Budget) throw new InvalidOperationException($"The check did not finish in {Budget} seconds.");
                if (_check.MoveNext()) return;
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Step;
            SessionState.SetBool(Flag, false);
            try { if (Directory.Exists(_work)) Directory.Delete(_work, true); } catch (Exception) { /* a temp directory */ }
            EditorApplication.Exit(code);
        }

        private static void Require(bool condition, string what)
        {
            if (!condition) throw new InvalidOperationException(what);
        }

        // A belt of rocks of every size, its sizes in the given order, round an orbit about the planet.
        private static Asteroid[] Rocks(int count, Func<int, float> size, float start) => Enumerable.Range(0, count).Select(i => new Asteroid
        {
            Distance = start + i * 9.7f, Phase = Mathf.Repeat(i * .6180339887f, 1f), Size = size(i), RotationSpeed = .1f + i * .013f
        }).ToArray();

        private static IEnumerator Check()
        {
            var game = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset") ?? throw new InvalidOperationException("No Settings.asset.");
            var settings = game.GameplaySettings;
            settings.FloaterLayerHeight = Layer; // in memory, never saved back to the asset: a draw that drops the layer fails

            var meshes = SessionState.GetString(Wiring, "").Split('|').Select(entry => entry.Split('~')).Select(p => new InstancedMesh
            {
                Mesh = AssetDatabase.LoadAllAssetsAtPath(p[0]).OfType<Mesh>().First(m => m.name == p[1]),
                Material = p[2] == "" ? null : AssetDatabase.LoadAssetAtPath<Material>(p[2]),
                Radius = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture)
            }).ToArray();
            Require(meshes.Length > game.AsteroidMeshCount, $"ARPG.unity has {meshes.Length} rock meshes for a kept count of {game.AsteroidMeshCount}: nothing to prune.");

            var cache = AetheriaStores.Open(Path.Combine(_work, "belts.cc"), Path.Combine(_work, "belts-run.cc"), catalogWritable: true);
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.FlushAsync().Wait();
            var items = new ItemManager(1u, cache, new ProvenanceLedger(), settings, _ => { });

            CultRecordKey Orbit(CultRecordKey parent, float distance, float phase, float2 fixedPosition = default)
            {
                var data = new OrbitData { Distance = distance, Phase = phase, FixedPosition = fixedPosition };
                if (parent.IsSet()) data.Parent = new CultRecordRef<OrbitData>(parent);
                return cache.Upsert(data).Key;
            }

            // A heavy sun and a planet on a moving rail, the belts round the planet: the field under a rock differs from rock to rock.
            var sunOrbit = Orbit(default, 0f, 0f, new float2(700f, -300f));
            var planetOrbit = Orbit(sunOrbit, 600f, .3f);
            var beltOrbitA = Orbit(planetOrbit, 150f, .1f);
            var beltOrbitB = Orbit(planetOrbit, 300f, .6f);
            var beltA = cache.Upsert(new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(beltOrbitA), Asteroids = Rocks(24, i => (i * 7 % 24) / 23f, 40f) }).Key;
            var beltB = cache.Upsert(new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(beltOrbitB), Asteroids = Rocks(24, i => (i * 5 % 24) / 23f, 60f) }).Key;
            var pack = new ZonePack { Radius = 5000f, Mass = 10000f };
            foreach (var orbit in new[] { sunOrbit, planetOrbit, beltOrbitA, beltOrbitB }) pack.Orbits.Add(new CultRecordRef<OrbitData>(orbit));
            pack.Planets.Add(new CultRecordRef<BodyData>(cache.Upsert(new PlanetData { Name = "Belt Sun", Orbit = new CultRecordRef<OrbitData>(sunOrbit), Mass = 400f }).Key));
            pack.Planets.Add(new CultRecordRef<BodyData>(cache.Upsert(new PlanetData { Name = "Belt Planet", Orbit = new CultRecordRef<OrbitData>(planetOrbit), Mass = 400f }).Key));
            pack.Planets.Add(new CultRecordRef<BodyData>(beltA));
            pack.Planets.Add(new CultRecordRef<BodyData>(beltB));
            var curves = new PlanetSettings
            {
                BodyRadius = new ExponentialCurve(),
                WellStrength = new ExponentialCurve { Multiplier = 1f, Exponent = 1f },
                RocheLimit = new ExponentialCurve { Constant = 10f },
                AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f },
                AsteroidHitpoints = new ExponentialLerp { Minimum = 10f, Maximum = 300f, Exponent = 2f },
                AsteroidRespawnTime = new ExponentialLerp { Minimum = 5f, Maximum = 60f, Exponent = 1f }
            };
            var zone = new Zone(items, curves, pack, null, null);
            zone.Update(.5f);

            // The renderer, configured as the scene configures it for belts: Awake builds the map, LoadPlanet reads Zone, Settings and AsteroidMeshes.
            var rendererObject = new GameObject("Belt Zone Renderer");
            rendererObject.SetActive(false);
            var renderer = rendererObject.AddComponent<ZoneRenderer>();
            renderer.Settings = game;
            renderer.AsteroidMeshes = meshes;
            renderer.ItemManager = items;
            renderer.LODHandler = new GameObject("Belt LOD").AddComponent<LODHandler>();
            const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            typeof(ZoneRenderer).GetProperty("Zone").GetSetMethod(true).Invoke(renderer, new object[] { zone });
            typeof(ZoneRenderer).GetMethod("Awake", Any).Invoke(renderer, null);
            var load = typeof(ZoneRenderer).GetMethod("LoadPlanet", Any);
            foreach (var belt in new[] { beltA, beltB }) load.Invoke(renderer, new object[] { belt, zone.Planets[belt] });

            var planetAt = DrawAhead.Orbit(zone, planetOrbit, Lead);
            var camera = new GameObject("Belt Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(planetAt.x, 4000f, planetAt.y);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.farClipPlane = 20000f;
            renderer.MainCamera = camera;
            yield return null;

            ActionGameManager.Clock.Reset();
            ActionGameManager.Clock.Advance(Lead, _ => throw new InvalidOperationException("a step ran"));
            var lead = ActionGameManager.Clock.Lead;
            Require(Mathf.Abs(lead - Lead) < 1e-6f, $"The clock's lead is {lead}, not {Lead}.");

            var drawn = new List<(Mesh mesh, Matrix4x4[] matrices)>();
            renderer.DrawInstanced = (mesh, _, matrices) => drawn.Add((mesh, (Matrix4x4[]) matrices.Clone()));
            renderer.DrawBelts(lead);

            // The poses the sim defines at the drawn time: every rock of both belts.
            var rocks = new List<(CultRecordKey belt, int index, float4 pose)>();
            foreach (var belt in new[] { beltA, beltB })
            {
                var poses = new float4[((AsteroidBeltData) zone.Planets[belt]).Asteroids.Length];
                zone.EvaluateBelt(belt, poses, DrawAhead.Time(zone, lead));
                for (var i = 0; i < poses.Length; i++) rocks.Add((belt, i, poses[i]));
            }
            Require(drawn.Count > 0, "DrawBelts drew nothing: the belts are out of the camera or have no rocks.");

            // The field differs between rocks, so a draw that drops it or samples it at the wrong place fails.
            var layers = rocks.Select(r => DrawAhead.FloaterHeight(zone, settings, r.pose.xy, lead)).ToArray();
            Require(layers.Max() - layers.Min() > 1f, $"The fixture's field is flat under the rocks: layer heights span {layers.Max() - layers.Min()}.");

            var seen = new Dictionary<(CultRecordKey, int), float>(); // rock -> its mesh's own farthest vertex
            var vertices = new Dictionary<Mesh, Vector3[]>();
            foreach (var (mesh, matrices) in drawn)
            {
                if (!vertices.TryGetValue(mesh, out var v)) vertices[mesh] = v = mesh.vertices;
                var own = Mathf.Sqrt(v.Max(x => x.sqrMagnitude));
                foreach (var matrix in matrices)
                {
                    var centre = (Vector3) matrix.GetColumn(3);
                    var at = rocks.Where(r => Mathf.Abs(r.pose.x - centre.x) < .01f && Mathf.Abs(r.pose.y - centre.z) < .01f).ToArray();
                    Require(at.Length == 1, $"An instance of {mesh.name} sits at {centre.x},{centre.z}, where {at.Length} rocks are (a spare or misplaced instance).");
                    var rock = at[0];
                    Require(seen.TryAdd((rock.belt, rock.index), own), $"Rock {rock.index} of {rock.belt} is drawn twice.");
                    var reach = v.Max(x => (matrix.MultiplyPoint3x4(x) - centre).magnitude);
                    Require(Mathf.Abs(reach - rock.pose.w) < Tolerance * Mathf.Max(1f, rock.pose.w),
                        $"Rock {rock.index} of {rock.belt} on {mesh.name}: its farthest vertex is {reach} from its centre, its gameplay radius {rock.pose.w}.");
                    var top = centre.y + reach;
                    var want = DrawAhead.FloaterHeight(zone, settings, rock.pose.xy, lead);
                    Require(Mathf.Abs(top - want) < Tolerance,
                        $"Rock {rock.index} of {rock.belt} on {mesh.name}: its top is {top}, the floater layer over the field there {want}.");
                }
            }
            Require(seen.Count == rocks.Count, $"{seen.Count} of {rocks.Count} rocks were drawn.");

            foreach (var belt in new[] { beltA, beltB })
            {
                var mine = rocks.Where(r => r.belt.Equals(belt)).ToArray();
                var radii = mine.Select(r => seen[(belt, r.index)]).Distinct().OrderBy(x => x).ToArray();
                Require(radii.Length >= 2, $"Belt {belt} is drawn on {radii.Length} distinct mesh radii: the size match is not exercised.");
                var kept = drawn.Where(d => d.matrices.Any(m => mine.Any(r => Mathf.Abs(r.pose.x - m.m03) < .01f && Mathf.Abs(r.pose.y - m.m23) < .01f))).Select(d => d.mesh).Distinct().Count();
                Require(kept == Mathf.Min(game.AsteroidMeshCount, meshes.Length), $"Belt {belt} is drawn on {kept} meshes, not the kept count {game.AsteroidMeshCount}.");
                var size = mine.ToDictionary(r => r.index, r => zone.AsteroidBelts[belt].UndamagedSize(r.index, zone.Settings));
                foreach (var a in mine)
                    foreach (var b in mine)
                        if (size[a.index] < size[b.index])
                            Require(seen[(belt, a.index)] <= seen[(belt, b.index)],
                                $"Belt {belt}: rock {a.index} (size {size[a.index]}) is smaller than rock {b.index} ({size[b.index]}) and takes a bigger mesh ({seen[(belt, a.index)]} against {seen[(belt, b.index)]}).");
                var biggest = mine.OrderByDescending(r => size[r.index]).First();
                Require(Mathf.Approximately(seen[(belt, biggest.index)], radii[^1]), $"Belt {belt}: its biggest rock is not on its biggest mesh.");
            }
            ActionGameManager.Clock.Reset();
            Debug.Log($"{Tag} ok: {rocks.Count} rocks of 2 belts drawn at the floater layer, big rocks on big meshes ({drawn.Count} batches)");
        }

        // The AsteroidMeshes ARPG.unity's ZoneRenderer names, read before the play session (a scene cannot be opened inside one) and carried across the
        // domain reload as asset paths: mesh path, mesh name, material path, stored radius.
        private static string ReadWiring()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ARPG.unity", OpenSceneMode.Additive);
            try
            {
                var found = UnityEngine.Object.FindObjectsByType<ZoneRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(r => r.gameObject.scene == scene).ToArray();
                Require(found.Length == 1, $"ARPG.unity has {found.Length} ZoneRenderers.");
                var meshes = found[0].AsteroidMeshes ?? throw new InvalidOperationException("ARPG.unity's ZoneRenderer has no AsteroidMeshes.");
                return string.Join("|", meshes.Select(m => string.Join("~", AssetDatabase.GetAssetPath(m.Mesh), m.Mesh.name,
                    m.Material == null ? "" : AssetDatabase.GetAssetPath(m.Material), m.Radius.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
