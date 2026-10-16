using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GameCult.Caching;
using UnityEditor;
using CultMath;
using UnityEngine;
using float4 = CultMath.float4;
using Debug = UnityEngine.Debug;

// The map's belt cost per frame, CPU milliseconds and managed allocation: MapPresenter's belt upload and draw for belts of
// 1000, 4000 and 10000 asteroids (a generated belt holds 11 + sqrt(mass * distance), Settings.asset). Needs a GPU device.
//
//   Unity -batchmode -projectPath <project> -executeMethod MapBeltPerf.Run -logFile <log>
public static class MapBeltPerf
{
    private const int Warmup = 60, Frames = 600;

    public static void Run()
    {
        var work = Path.Combine(Path.GetTempPath(), $"map-belt-perf-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(work);
            foreach (var count in new[] { 1000, 4000, 10000 }) Measure(work, count);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Measure(string work, int count)
    {
        var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset");
        var cache = AetheriaStores.Open(Path.Combine(work, $"c{count}.cc"), Path.Combine(work, $"r{count}.cc"), catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var sunOrbit = cache.Upsert(new OrbitData()).Key;
        var sunKey = cache.Upsert(new PlanetData { Name = "Sun", Orbit = new CultRecordRef<OrbitData>(sunOrbit), Mass = 400f }).Key;
        var beltOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(sunOrbit), Distance = 900f, Phase = .42f }).Key;
        var asteroids = Enumerable.Range(0, count).Select(i => new Asteroid
            { Distance = 120f + i * .01f, Phase = CultMath.math.frac(i * .618f), Size = CultMath.math.frac(i * .37f + .1f), RotationSpeed = .15f + i * 1e-4f }).ToArray();
        var beltKey = cache.Upsert(new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(beltOrbit), Asteroids = asteroids }).Key;
        var pack = new ZonePack
        {
            Orbits = { new CultRecordRef<OrbitData>(sunOrbit), new CultRecordRef<OrbitData>(beltOrbit) },
            Planets = { new CultRecordRef<BodyData>(sunKey), new CultRecordRef<BodyData>(beltKey) },
            Radius = 5000f, Mass = 10000f, Time = 0
        };
        var items = new ItemManager(cache, new ProvenanceLedger(), settings.GameplaySettings, _ => { });
        var zone = new Zone(items, new PlanetSettings
        {
            BodyRadius = new ExponentialCurve(), WellStrength = new ExponentialCurve { Multiplier = 1f, Exponent = 1f },
            RocheLimit = new ExponentialCurve { Constant = 2f },
            AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f },
            AsteroidHitpoints = new ExponentialLerp { Minimum = 10f, Maximum = 300f, Exponent = 2f },
            AsteroidRespawnTime = new ExponentialLerp { Minimum = 5f, Maximum = 60f, Exponent = 1f }
        }, pack, new GalaxyZone { Name = "Perf", Owner = null }, null);

        var transforms = new float4[count];
        // BEGIN MEASURED SETUP
        var presenter = new MapPresenter(new Material[0], AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/MapAsteroid.mat"), 4, 4, 3f);
        presenter.BeltsOnMinimap = true;
        presenter.RegisterBelt(beltKey, transforms);
        // END MEASURED SETUP
        var stopwatch = new Stopwatch();
        long allocated = 0;
        for (var frame = 0; frame < Warmup + Frames; frame++)
        {
            zone.EvaluateBelt(beltKey, transforms, 0.016 * frame);
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            if (frame >= Warmup) stopwatch.Start();
            // BEGIN MEASURED FRAME
            presenter.Draw(zone, 0);
            // END MEASURED FRAME
            if (frame >= Warmup) stopwatch.Stop();
            if (frame >= Warmup) allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
        }
        Debug.Log($"[MapBeltPerf] asteroids={count} cpu_ms_per_frame={stopwatch.Elapsed.TotalMilliseconds / Frames:F4} gc_bytes_per_frame={allocated / Frames}");
    }
}
