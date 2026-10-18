using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The hand-built zones of the tree tests: packs of orbits and bodies read by the production Zone constructor with the shipped
// well curves (c = 45.08 m^.2, eps = 7.32 m^.25), each in its own store under one temporary root.
internal sealed class ZoneWorlds : IDisposable
{
    public readonly string Root = Path.Combine(Path.GetTempPath(), "aetheria-orbitsfield-" + Guid.NewGuid().ToString("N"));
    public readonly List<CultCache> Caches = new List<CultCache>();

    public ZoneWorlds() => Directory.CreateDirectory(Root);

    public void Dispose()
    {
        foreach (var cache in Caches) cache.Dispose();
        Directory.Delete(Root, true);
    }

    private static GameplaySettings GameSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    public static PlanetSettings Curves() => new PlanetSettings
    {
        WellStrength = new ExponentialCurve { Exponent = .2f, Multiplier = 45.08f },
        RocheLimit = new ExponentialCurve { Exponent = .25f, Multiplier = 7.32f },
        BodyRadius = new ExponentialCurve { Exponent = .25f, Multiplier = 3f },
        LightRadius = new ExponentialCurve(),
        AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f }
    };

    public sealed class World
    {
        public CultCache Cache;
        public ItemManager Items;
        public ZonePack Pack = new ZonePack { Radius = 20000f, Mass = 10000f };

        public CultRecordKey Orbit(CultRecordKey parent, float distance, float phase = 0f)
        {
            var data = new OrbitData { Distance = distance, Phase = phase };
            if (parent.IsSet()) data.Parent = new CultRecordRef<OrbitData>(parent);
            var key = Cache.Upsert(data).Key;
            Pack.Orbits.Add(new CultRecordRef<OrbitData>(key));
            return key;
        }

        public CultRecordKey Body(CultRecordKey orbit, float mass)
        {
            var key = Cache.Upsert(new PlanetData { Name = "Body", Orbit = new CultRecordRef<OrbitData>(orbit), Mass = mass }).Key;
            Pack.Planets.Add(new CultRecordRef<BodyData>(key));
            return key;
        }

        // A belt of asteroids at the given distances riding `orbit`; returns the belt's body key.
        public CultRecordKey Belt(CultRecordKey orbit, params float[] distances)
        {
            var asteroids = distances.Select((d, i) => new Asteroid { Distance = d, Phase = frac(i * .6180339887f), Size = .5f, RotationSpeed = .1f }).ToArray();
            var key = Cache.Upsert(new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(orbit), Asteroids = asteroids }).Key;
            Pack.Planets.Add(new CultRecordRef<BodyData>(key));
            return key;
        }

        public Zone Build() => new Zone(Items, Curves(), Pack, new GalaxyZone { Name = "OrbitsFromField", Owner = null }, null);
    }

    public World New()
    {
        var dir = Path.Combine(Root, $"world{Caches.Count}");
        Directory.CreateDirectory(dir);
        var cache = AetheriaStores.Open(Path.Combine(dir, "Aetheria.cc"), Path.Combine(dir, "run.cc"), catalogWritable: true);
        Caches.Add(cache);
        return new World { Cache = cache, Items = new ItemManager(cache, new ProvenanceLedger(), GameSettings(), _ => { }) };
    }
}
