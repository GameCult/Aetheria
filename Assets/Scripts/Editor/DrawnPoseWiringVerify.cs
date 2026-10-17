using System;
using System.Collections;
using System.IO;
using System.Linq;
using GameCult.Caching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Batch proof that the presenters read the drawn pose through the clock: DrawnPoseTests pins the pose maths headlessly,
// and this is what pins that each presenter passes ActionGameManager.Clock.Lead into it. A zone is loaded into a fresh
// ZoneRenderer with the pickup and mine prefabs ARPG.unity's ZoneRenderer names; a loose Projectile is launched on the
// zone. The zone is stepped once, then the clock is set to leads 0, 0.008 and 0.015 s with no step, and each presenter's
// own Update places its transform. A body is drawn at its last step plus its step velocity times the lead, spelled here
// from the body's fields, so a presenter that reads lead 0 (or half the lead) sits on the step and fails.
//
// Reached: ItemPickup, MineInstance, Projectile.Draw. Not reached: ZoneRenderer.Update (Lower needs a ComputeBuffer, a
// GPU device) and the ActionGameManager markers (they need the ARPG scene running); a Lower split so its record fill is
// pure, or a run with a GPU and ARPG.unity in play mode, would reach them.
//
//   Unity -batchmode -nographics -projectPath <project> -executeMethod Aetheria.EditorTools.DrawnPoseWiringVerify.Run -logFile <log>
//
// Do not pass -quit: Run enters play mode (a domain reload), the check runs there, and the Editor exits itself, 0 on
// success and 1 on any failure.
namespace Aetheria.EditorTools
{
    [InitializeOnLoad]
    public static class DrawnPoseWiringVerify
    {
        private const string Tag = "[DrawnPoseWiringVerify]";
        private const string Flag = "DrawnPoseWiringVerify.running";
        private const string Wiring = "DrawnPoseWiringVerify.wiring";
        private const double Budget = 300; // real seconds
        private const float RoundSpeed = 100f, Tolerance = 1e-3f;
        private static IEnumerator _check;
        private static double _started;
        private static string _work;

        static DrawnPoseWiringVerify()
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
            _work = Path.Combine(Path.GetTempPath(), $"drawn-pose-wiring-{Guid.NewGuid():N}");
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

        private static IEnumerator Check()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset")?.GameplaySettings
                ?? throw new InvalidOperationException("No Settings.asset.");
            settings.FloaterLayerHeight = -1.5f; // nonzero, so a presenter that drops the floater layer fails

            var wired = SessionState.GetString(Wiring, "").Split('|').Select(entry => entry.Split('=')).ToDictionary(pair => pair[0], pair => pair[1]);
            var pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wired["SimpleCommodityPickup"])?.GetComponent<ItemPickup>();
            var minePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wired["MinePrefab"])?.GetComponent<MineInstance>();
            Require(pickupPrefab != null, "ZoneRenderer.SimpleCommodityPickup is not an ItemPickup in ARPG.unity.");
            Require(minePrefab != null, "ZoneRenderer.MinePrefab is not a MineInstance in ARPG.unity.");

            var oneCell = new Shape(1, 1);
            oneCell[new CultMath.int2(0, 0)] = true;
            var cache = AetheriaStores.Open(Path.Combine(_work, "wiring.cc"), Path.Combine(_work, "wiring-run.cc"), catalogWritable: true);
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.Upsert(new SimpleCommodityData { Name = "Wiring Ore", Shape = oneCell, MaxStack = 100 });
            cache.FlushAsync().Wait();
            var ledger = new ProvenanceLedger();
            var items = new ItemManager(cache, ledger, settings, _ => { });

            // A sun and a planet on a moving rail round it, both near the bodies below: the field under a floater changes with the
            // drawn time, so a presenter that samples it at the step's time, drops it, or reads lead 0 for the height draws a different y.
            var sunOrbit = cache.Upsert(new OrbitData { FixedPosition = new float2(-60, -40) }).Key;
            var planetOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(sunOrbit), Distance = 40f, Phase = .125f }).Key;
            var pack = new ZonePack { Radius = 5000f, Mass = 10000f };
            pack.Orbits.Add(new CultRecordRef<OrbitData>(sunOrbit));
            pack.Orbits.Add(new CultRecordRef<OrbitData>(planetOrbit));
            pack.Planets.Add(new CultRecordRef<BodyData>(cache.Upsert(new PlanetData { Name = "Wiring Sun", Orbit = new CultRecordRef<OrbitData>(sunOrbit), Mass = 400f }).Key));
            pack.Planets.Add(new CultRecordRef<BodyData>(cache.Upsert(new PlanetData { Name = "Wiring Planet", Orbit = new CultRecordRef<OrbitData>(planetOrbit), Mass = 100f }).Key));
            var curves = new PlanetSettings
            {
                BodyRadius = new ExponentialCurve(),
                WellStrength = new ExponentialCurve { Multiplier = 1f, Exponent = 1f },
                RocheLimit = new ExponentialCurve { Constant = 2f }
            };
            var zone = new Zone(items, curves, pack, null, null);

            var rootObject = new GameObject("Wiring Zone Root");
            var rendererObject = new GameObject("Wiring Zone Renderer");
            rendererObject.SetActive(false); // inactive: ZoneRenderer.Update needs a GPU and is not part of this check
            var renderer = rendererObject.AddComponent<ZoneRenderer>();
            renderer.ZoneRoot = rootObject.transform;
            renderer.SimpleCommodityPickup = pickupPrefab;
            renderer.MinePrefab = minePrefab;
            renderer.ItemManager = items;
            renderer.SectorBrushes = new GameObject("Wiring Brushes").transform;
            renderer.AmbientLight = AmbientEdgeVerify.NewAmbientLight();
            renderer.SlimeGravityCamera = new GameObject("Wiring Gravity Camera").AddComponent<Camera>();
            var slimeObject = new GameObject("Wiring Slime");
            slimeObject.SetActive(false);
            renderer.SlimeRenderer = slimeObject.AddComponent<Slime>();
            renderer.LoadZone(zone);

            var ore = new SimpleCommodity { Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<SimpleCommodityData>("Wiring Ore")), Quantity = 7 };
            var floating = zone.Release(ore, new float2(3, 4), new float2(10, 0));
            var mine = new Mine
            {
                Body = new KinematicBody { Position = new float2(-20, 8), Velocity = new float2(0, 14) },
                ArmingDelay = 1e6f, FuseDelay = 1f, Lifetime = 1e6f, BlastRadius = 1f
            };
            zone.Lay(mine);

            // A round on its frozen line, no target and no outcome known: drawn at FireOrigin + direction * speed * (T - FireTime).
            var shot = new PendingShot
            {
                ShotId = 1, FireOrigin = new float3(0, 0, 0), FireTime = 0f, TravelDirection = new float2(1, 0), Speed = RoundSpeed,
                MaxRange = 1e6f, FireRange = 1e6f, CommitTime = 1e5f, ArrivalTime = 2e5f
            };
            var projectile = new GameObject("Wiring Round").AddComponent<Projectile>();
            projectile.Launch(zone, shot, new Vector3(0, 0, 0));
            yield return null;

            zone.Update(.5f);

            var pickup = rootObject.GetComponentsInChildren<ItemPickup>(true).Single(p => p.Item == ore);
            var mineInstance = UnityEngine.Object.FindObjectsByType<MineInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();

            var pickupVelocity = floating.Body.TotalVelocity;
            var mineVelocity = mine.Body.TotalVelocity;
            Require(Mathf.Abs(pickupVelocity.x) > 1f && Mathf.Abs(mineVelocity.y) > 1f, "The fixture's bodies stopped: a lead would not move them.");

            // The field under each floater really is a function of the drawn time here, by more than the tolerance at the largest lead.
            const float LargestLead = .015f;
            foreach (var at in new[] { floating.Body.Position + pickupVelocity * LargestLead, mine.Body.Position + mineVelocity * LargestLead })
                Require(Mathf.Abs(DrawAhead.Height(zone, at, LargestLead) - zone.GetHeight(at, zone.ExactTime)) > 5 * Tolerance,
                    "The fixture's field is flat under a floater: a drawn time would not move its height.");

            Vector2 baseline = default;
            foreach (var leadSeconds in new[] { 0f, .008f, .015f })
            {
                ActionGameManager.Clock.Reset();
                if (leadSeconds > 0) ActionGameManager.Clock.Advance(leadSeconds, _ => throw new InvalidOperationException("a step ran"));
                var lead = ActionGameManager.Clock.Lead;
                Require(Mathf.Abs(lead - leadSeconds) < 1e-6f, $"The clock's lead is {lead}, not {leadSeconds}.");
                yield return null;

                var seen = pickup.transform.position;
                var want = floating.Body.Position + pickupVelocity * lead;
                Require(Mathf.Abs(seen.x - want.x) < Tolerance && Mathf.Abs(seen.z - want.y) < Tolerance,
                    $"ItemPickup at lead {lead}: drawn x/z {seen.x},{seen.z}, the step plus velocity times lead {want.x},{want.y}.");

                Require(Mathf.Abs(seen.y - DrawAhead.FloaterHeight(zone, settings, new float2(want.x, want.y), lead)) < Tolerance,
                    $"ItemPickup y {seen.y} is not on the floater layer.");

                seen = mineInstance.transform.position;
                want = mine.Body.Position + mineVelocity * lead;
                Require(Mathf.Abs(seen.x - want.x) < Tolerance && Mathf.Abs(seen.z - want.y) < Tolerance,
                    $"MineInstance at lead {lead}: drawn x/z {seen.x},{seen.z}, the step plus velocity times lead {want.x},{want.y}.");
                Require(Mathf.Abs(seen.y - DrawAhead.FloaterHeight(zone, settings, new float2(want.x, want.y), lead)) < Tolerance,
                    $"MineInstance y {seen.y} is not on the floater layer.");

                // The round's line is a function of the drawn time: it moves RoundSpeed * lead along its direction between leads.
                seen = projectile.transform.position;
                if (leadSeconds == 0f) baseline = new Vector2(seen.x, seen.z);
                var moved = new Vector2(seen.x, seen.z) - baseline;
                Require(Mathf.Abs(moved.x - RoundSpeed * lead) < Tolerance && Mathf.Abs(moved.y) < Tolerance,
                    $"Projectile at lead {lead}: drawn {moved.x},{moved.y} past its lead-0 pose, expected {RoundSpeed * lead},0.");
            }
            ActionGameManager.Clock.Reset();
            Debug.Log($"{Tag} ok: ItemPickup, MineInstance and Projectile draw at the clock's lead (0, 0.008, 0.015 s)");
        }

        // What ARPG.unity's ZoneRenderer names in the pickup and mine fields, read from the saved scene before the play session
        // (a scene cannot be opened inside one) and carried across the domain reload as asset paths.
        private static string ReadWiring()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ARPG.unity", OpenSceneMode.Additive);
            try
            {
                var found = UnityEngine.Object.FindObjectsByType<ZoneRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(r => r.gameObject.scene == scene).ToArray();
                Require(found.Length == 1, $"ARPG.unity has {found.Length} ZoneRenderers.");
                var wired = found[0];
                return string.Join("|", new (string, UnityEngine.Object)[]
                {
                    ("SimpleCommodityPickup", wired.SimpleCommodityPickup), ("MinePrefab", wired.MinePrefab)
                }.Select(f => f.Item1 + "=" + (f.Item2 == null ? "" : AssetDatabase.GetAssetPath(f.Item2))));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
