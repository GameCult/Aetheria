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

// The map's belt cost per frame, CPU milliseconds and managed allocation, for belts of 1000, 2250 (the generated
// maximum, Settings.asset) and 4000 asteroids: the base revision's AsteroidBeltUI.Update (MapBeltPerfBase, a verbatim copy)
// and MapPresenter's belt upload and draw, each as the median and interquartile range of 21 runs of 300 frames after
// a warm-up, in this one Unity job. CPU time only: GPU cost is not covered. Needs a GPU device.
//
//   Unity -batchmode -projectPath <project> -executeMethod MapBeltPerf.Run -logFile <log>
public static class MapBeltPerf
{
    private const int Warmup = 60, Frames = 300, RunCount = 21;

    public static void Run()
    {
        var work = Path.Combine(Path.GetTempPath(), $"map-belt-perf-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(work);
            foreach (var count in new[] { 1000, 2250, 4000 }) Measure(work, count);
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
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), settings.GameplaySettings, _ => { });
        var zone = new Zone(items, new PlanetSettings
        {
            BodyRadius = new ExponentialCurve(), WellStrength = new ExponentialCurve { Multiplier = 1f, Exponent = 1f },
            RocheLimit = new ExponentialCurve { Constant = 2f },
            AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f },
            AsteroidHitpoints = new ExponentialLerp { Minimum = 10f, Maximum = 300f, Exponent = 2f },
            AsteroidRespawnTime = new ExponentialLerp { Minimum = 5f, Maximum = 60f, Exponent = 1f }
        }, pack, new GalaxyZone { Name = "Perf", Owner = null }, null);

        var transforms = new float4[count];
        var presenter = new MapPresenter(new Material[0], AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/MapAsteroid.mat"), 4, 4, 3f);
        presenter.RegisterBelt(beltKey, transforms);
        var baseBelt = new MapBeltPerfBase(zone, zone.AsteroidBelts[beltKey], 4, 4, 3f);

        Report("base", count, Runs(zone, beltKey, transforms, () => baseBelt.Update(transforms, 0f)));
        Report("head", count, Runs(zone, beltKey, transforms, () => presenter.Draw(zone, 0)));
        presenter.Clear();
    }

    // Each run is Frames frames after Warmup frames; the figure is one run's CPU ms per frame and managed bytes per frame.
    private static (double ms, long bytes)[] Runs(Zone zone, CultRecordKey beltKey, float4[] transforms, Action frame)
    {
        var results = new (double, long)[RunCount];
        var stopwatch = new Stopwatch();
        var time = 0.0;
        for (var run = -1; run < RunCount; run++)
        {
            stopwatch.Reset();
            long allocated = 0;
            for (var i = 0; i < Warmup + Frames; i++)
            {
                zone.EvaluateBelt(beltKey, transforms, time += 0.016);
                var bytes = GC.GetAllocatedBytesForCurrentThread();
                if (i >= Warmup) stopwatch.Start();
                frame();
                if (i >= Warmup) stopwatch.Stop();
                if (i >= Warmup) allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
            }
            if (run >= 0) results[run] = (stopwatch.Elapsed.TotalMilliseconds / Frames, allocated / Frames);
        }
        return results;
    }

    private static double Quantile(double[] sorted, double q)
    {
        var at = q * (sorted.Length - 1);
        var low = (int)Math.Floor(at);
        var high = Math.Min(low + 1, sorted.Length - 1);
        return sorted[low] + (sorted[high] - sorted[low]) * (at - low);
    }

    private static void Report(string revision, int count, (double ms, long bytes)[] runs)
    {
        var ms = runs.Select(r => r.ms).OrderBy(v => v).ToArray();
        var bytes = runs.Select(r => (double)r.bytes).OrderBy(v => v).ToArray();
        var units = Process.GetProcessesByName("Unity").Length;
        Debug.Log($"[MapBeltPerf] revision={revision} asteroids={count} runs={runs.Length} frames_per_run={Frames} " +
                  $"cpu_ms_median={Quantile(ms, .5):F4} iqr={Quantile(ms, .25):F4}-{Quantile(ms, .75):F4} " +
                  $"gc_bytes_median={Quantile(bytes, .5):F0} unity_processes={units} cpus={Environment.ProcessorCount}");
    }
}
