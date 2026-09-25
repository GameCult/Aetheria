/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;
using static CultMath.math;

// Cut 2 (docs/mining-cut.md): the dead per-tick "hit it with a tool" behavior and its per-tick accumulator are
// gone, along with the scanner's dead survey half, and chunk wear gets one owner: Zone. These tests pin that
// owner's behaviour -- accumulation, breaking, lazy respawn, and the persistence round trip through ZonePack's
// new nullable key 6 -- never by reaching into AsteroidBelt (it no longer holds wear at all).
public sealed class MiningCut2Tests : IDisposable
{
    private int _fixtureCount;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-miningcut2-" + Guid.NewGuid().ToString("N"));

    public MiningCut2Tests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static readonly MessagePackSerializerOptions PackOptions =
        CultDocumentMessagePackSerialization.OptionsFor(typeof(ZonePack).Assembly);

    private static GameplaySettings GameSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    // Authored-shape curves, not flat defaults: hitpoints and respawn time both vary with size over the
    // fixture's range, so a test that pins "breaks at the authored hitpoints" or "respawns at the authored
    // curve" is exercising real curvature, not a constant that any wrong formula would also satisfy.
    private static PlanetSettings Settings() => new PlanetSettings
    {
        OrbitPeriod = new ExponentialCurve { Multiplier = 0.6f, Exponent = 1.3f, Constant = 40f },
        AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f },
        AsteroidHitpoints = new ExponentialLerp { Minimum = 20f, Maximum = 220f, Exponent = 1.6f },
        AsteroidRespawnTime = new ExponentialLerp { Minimum = 8f, Maximum = 96f, Exponent = 1.2f }
    };

    private sealed class Fixture
    {
        public Zone Zone;
        public ItemManager Items;
        public PlanetSettings Settings;
        public CultRecordKey BeltA;
        public CultRecordKey BeltB;
        public Asteroid[] AsteroidsA;
        public Asteroid[] AsteroidsB;
    }

    // Two belts of different sizes, asteroids at distinct sizes within each -- so a bug that keys wear by field
    // alone (ignoring index), or by index alone (ignoring field), shows up as cross-talk between chunks that
    // share an index or a belt.
    private Fixture BuildFixture()
    {
        var dir = Path.Combine(_root, $"cat{_fixtureCount++}");
        Directory.CreateDirectory(dir);
        var cache = AetheriaStores.Open(Path.Combine(dir, "Aetheria.cc"), Path.Combine(dir, "run.cc"), catalogWritable: true);
        var items = new ItemManager(cache, new ProvenanceLedger(), GameSettings(), _ => { });
        var settings = Settings();

        var rootOrbit = new OrbitData { Distance = 3000f, Phase = 0.05f };
        var rootKey = cache.Upsert(rootOrbit).Key;

        CultRecordKey MakeBelt(int count, float baseDistance, float sizeStart)
        {
            var beltOrbit = new OrbitData { Parent = new CultRecordRef<OrbitData>(rootKey), Distance = baseDistance, Phase = 0.11f };
            var beltOrbitKey = cache.Upsert(beltOrbit).Key;
            var asteroids = new Asteroid[count];
            for (var i = 0; i < count; i++)
            {
                asteroids[i] = new Asteroid
                {
                    Distance = 100f + i * 9.7f,
                    Phase = frac(i * 0.618f),
                    Size = frac(sizeStart + i * 0.29f),
                    RotationSpeed = 0.1f + i * 0.02f
                };
            }
            var beltData = new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(beltOrbitKey), Asteroids = asteroids };
            return cache.Upsert(beltData).Key;
        }

        var beltAKey = MakeBelt(9, 700f, 0.05f);
        var beltBKey = MakeBelt(5, 1600f, 0.61f);

        var beltAData = (AsteroidBeltData) cache.Get(beltAKey);
        var beltBData = (AsteroidBeltData) cache.Get(beltBKey);

        var pack = new ZonePack
        {
            Orbits = { new CultRecordRef<OrbitData>(rootKey) },
            Planets = { new CultRecordRef<BodyData>(beltAKey), new CultRecordRef<BodyData>(beltBKey) },
            Radius = 5000f,
            Mass = 10000f,
            Time = 0
        };
        // Both belt orbits share the root as parent and are already registered against the cache; the Zone
        // constructor discovers each belt's own orbit key through AsteroidBeltData.Orbit, but it also needs
        // every orbit key it will walk listed in pack.Orbits.
        pack.Orbits.Add(new CultRecordRef<OrbitData>(beltAData.Orbit.Key));
        pack.Orbits.Add(new CultRecordRef<OrbitData>(beltBData.Orbit.Key));

        var zone = new Zone(items, settings, pack, new GalaxyZone { Name = "MiningCut2", Owner = null }, null);
        return new Fixture
        {
            Zone = zone, Items = items, Settings = settings,
            BeltA = beltAKey, BeltB = beltBKey,
            AsteroidsA = beltAData.Asteroids, AsteroidsB = beltBData.Asteroids
        };
    }

    [Fact]
    public void WearAccumulatesAndDoesNotBreakUntilStrictlyOverHitpoints()
    {
        var f = BuildFixture();
        const int index = 3;
        var chunk = new ChunkId(f.BeltA, index);
        var hp = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsA[index].Size);

        // Several partial hits under the threshold: still there, still shrinking.
        Assert.False(f.Zone.Wear(chunk, hp * 0.3f));
        Assert.True(f.Zone.ChunkExists(chunk));
        Assert.False(f.Zone.Wear(chunk, hp * 0.3f));
        Assert.True(f.Zone.ChunkExists(chunk));

        var before = f.Zone.ChunkRadius(chunk);
        Assert.True(before < f.Zone.AsteroidBelts[f.BeltA].UndamagedSize(index, f.Settings),
            "accumulated but unbroken damage must have already shrunk the chunk");

        // The remaining exact amount lands accumulated damage exactly on the hitpoint threshold: pins the
        // break condition as strictly greater-than, not greater-or-equal (today's behaviour, kept deliberately
        // -- see Zone.Wear's own comment). Equal-to-hitpoints must not break the chunk.
        var remaining = hp - hp * 0.6f;
        Assert.False(f.Zone.Wear(chunk, remaining));
        Assert.True(f.Zone.ChunkExists(chunk), "damage exactly equal to hitpoints must not break the chunk");

        // One more point of damage tips it over.
        Assert.True(f.Zone.Wear(chunk, 0.01f));
        Assert.False(f.Zone.ChunkExists(chunk));
        Assert.Equal(0f, f.Zone.ChunkRadius(chunk));
    }

    [Fact]
    public void ABrokenChunkReturnsAtItsRespawnTimeFullyHealed()
    {
        var f = BuildFixture();
        const int index = 1;
        var chunk = new ChunkId(f.BeltA, index);
        var hp = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsA[index].Size);
        var respawn = f.Settings.AsteroidRespawnTime.Evaluate(f.AsteroidsA[index].Size);

        Assert.True(f.Zone.Wear(chunk, hp + 1f));
        Assert.False(f.Zone.ChunkExists(chunk));

        // Uneven steps that sum to just under the authored respawn time: still broken.
        var dts = new float[] { respawn * 0.2f, respawn * 0.31f, respawn * 0.1f, respawn * 0.15f };
        foreach (var dt in dts) f.Zone.Update(dt);
        Assert.False(f.Zone.ChunkExists(chunk), "must still be broken just under the authored respawn time");

        // The remaining sliver plus a margin crosses the respawn time.
        f.Zone.Update(respawn * 0.3f);
        Assert.True(f.Zone.ChunkExists(chunk), "must have respawned once zone time reaches the authored respawn time");
        Assert.Equal(f.Zone.AsteroidBelts[f.BeltA].UndamagedSize(index, f.Settings), f.Zone.ChunkRadius(chunk), 4);
    }

    [Fact]
    public void BrokenThenRespawnedThenWornAgainStartsFromZeroDamage()
    {
        var f = BuildFixture();
        const int index = 2;
        var chunk = new ChunkId(f.BeltB, index);
        var hp = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsB[index].Size);
        var respawn = f.Settings.AsteroidRespawnTime.Evaluate(f.AsteroidsB[index].Size);

        Assert.True(f.Zone.Wear(chunk, hp * 1.5f));
        f.Zone.Update(respawn + 1f);
        Assert.True(f.Zone.ChunkExists(chunk));

        // A hit well under hitpoints, on the freshly respawned chunk, must not break it -- if the old damage
        // (or the old broken state) leaked through, this hit alone would already be over any stale threshold.
        Assert.False(f.Zone.Wear(chunk, hp * 0.4f));
        Assert.True(f.Zone.ChunkExists(chunk));
        var radius = f.Zone.ChunkRadius(chunk);
        Assert.True(radius > 0f && radius < f.Zone.AsteroidBelts[f.BeltB].UndamagedSize(index, f.Settings));
    }

    [Fact]
    public void WearIsIndependentPerChunk()
    {
        var f = BuildFixture();
        // Same index in both belts, and a different index in the same belt, so a key collision on either half
        // of ChunkId would show up as cross-talk.
        var chunkA0 = new ChunkId(f.BeltA, 0);
        var chunkB0 = new ChunkId(f.BeltB, 0);
        var chunkA1 = new ChunkId(f.BeltA, 1);

        var hpA0 = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsA[0].Size);
        Assert.True(f.Zone.Wear(chunkA0, hpA0 + 1f));

        Assert.False(f.Zone.ChunkExists(chunkA0));
        Assert.True(f.Zone.ChunkExists(chunkB0), "a different field at the same index must be unaffected");
        Assert.True(f.Zone.ChunkExists(chunkA1), "a different index in the same field must be unaffected");
        Assert.Equal(f.Zone.AsteroidBelts[f.BeltB].UndamagedSize(0, f.Settings), f.Zone.ChunkRadius(chunkB0), 4);
    }

    [Fact]
    public void WearSurvivesPackAndUnpack()
    {
        var f = BuildFixture();
        var damaged = new ChunkId(f.BeltA, 4);
        var broken = new ChunkId(f.BeltB, 3);
        var untouched = new ChunkId(f.BeltA, 5);

        var hpDamaged = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsA[4].Size);
        var hpBroken = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsB[3].Size);

        Assert.False(f.Zone.Wear(damaged, hpDamaged * 0.45f));
        Assert.True(f.Zone.Wear(broken, hpBroken + 5f));
        f.Zone.Update(1.3f);

        var damagedRadiusBefore = f.Zone.ChunkRadius(damaged);
        var pack = f.Zone.PackZone();
        Assert.NotNull(pack.ChunkWear);
        Assert.True(pack.ChunkWear.Count >= 2);

        var reloaded = new Zone(f.Items, f.Settings, pack, new GalaxyZone { Name = "MiningCut2Reload", Owner = null }, null);

        Assert.True(reloaded.ChunkExists(untouched));
        Assert.False(reloaded.ChunkExists(broken), "a still-broken chunk must stay broken across save/continue");
        Assert.True(reloaded.ChunkExists(damaged));
        Assert.Equal(damagedRadiusBefore, reloaded.ChunkRadius(damaged), 4);
    }

    [Fact]
    public void APackOmitsExpiredWear()
    {
        var f = BuildFixture();
        var chunk = new ChunkId(f.BeltA, 6);
        var hp = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsA[6].Size);
        var respawn = f.Settings.AsteroidRespawnTime.Evaluate(f.AsteroidsA[6].Size);

        Assert.True(f.Zone.Wear(chunk, hp + 1f));
        f.Zone.Update(respawn + 2f);
        Assert.True(f.Zone.ChunkExists(chunk), "sanity: this chunk must have respawned before packing");

        var pack = f.Zone.PackZone();
        var stillPacked = pack.ChunkWear?.Exists(w => w.Field.Equals(chunk.Field) && w.Index == chunk.Index) ?? false;
        Assert.False(stillPacked, "a fully healed, no-longer-broken entry carries no live information and must be pruned");
    }

    // Persistence rule (docs/mining-cut.md): ZonePack key 6 is nullable so a record from before it existed --
    // built here as raw MessagePack bytes with only the six original keys, not a default-constructed ZonePack
    // -- deserializes with ChunkWear == null and loads as a zone with no wear, rather than throwing or reading
    // some default non-null instance.
    [Fact]
    public void ARunWithoutKeySixLoadsWhole()
    {
        var f = BuildFixture();
        var oldShapePack = new ZonePack
        {
            Planets = { new CultRecordRef<BodyData>(f.BeltA), new CultRecordRef<BodyData>(f.BeltB) },
            Orbits = f.Zone.Orbits.Keys.ConvertToRefList(),
            Entities = new List<EntityPack>(),
            Radius = 5000f,
            Mass = 10000f,
            Time = 12.5
        };

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(6); // the pre-Cut-2 shape: keys 0-5 only, no slot for key 6 at all.
        MessagePackSerializer.Serialize(ref writer, oldShapePack.Planets, PackOptions);
        MessagePackSerializer.Serialize(ref writer, oldShapePack.Orbits, PackOptions);
        MessagePackSerializer.Serialize(ref writer, oldShapePack.Entities, PackOptions);
        MessagePackSerializer.Serialize(ref writer, oldShapePack.Radius, PackOptions);
        MessagePackSerializer.Serialize(ref writer, oldShapePack.Mass, PackOptions);
        MessagePackSerializer.Serialize(ref writer, oldShapePack.Time, PackOptions);
        writer.Flush();

        var loadedPack = MessagePackSerializer.Deserialize<ZonePack>(buffer.WrittenMemory, PackOptions);
        Assert.Null(loadedPack.ChunkWear);

        var zone = new Zone(f.Items, f.Settings, loadedPack, new GalaxyZone { Name = "MiningCut2Old", Owner = null }, null);
        Assert.True(zone.ChunkExists(new ChunkId(f.BeltA, 0)));
        Assert.Equal(zone.AsteroidBelts[f.BeltA].UndamagedSize(0, f.Settings), zone.ChunkRadius(new ChunkId(f.BeltA, 0)), 4);
    }

    // Stryker gap (2026-09-25 pass on this cut): ChunkExists's range check reads `chunk.Index < 0 ||
    // chunk.Index >= length`; nothing exercised either side of that OR alone, or the >= boundary at exactly
    // `length`, so a mutant weakening it to AND (which can never be true, since an index cannot be both
    // negative and past the end at once) and a mutant weakening >= to > both survived.
    [Fact]
    public void OutOfRangeChunkIndicesDoNotExist()
    {
        var f = BuildFixture();
        Assert.False(f.Zone.ChunkExists(new ChunkId(f.BeltA, -1)), "a negative index alone must already fail");
        Assert.False(f.Zone.ChunkExists(new ChunkId(f.BeltA, f.AsteroidsA.Length)),
            "the index exactly at the asteroid count is already past the end (0-based), not the first one out");
    }

    // Stryker gap: ChunkId.Equals was reachable only through the wear dictionary, whose GetHashCode already
    // separates a same-field-different-index or same-index-different-field pair into different buckets, so a
    // mutant weakening Equals from `&&` to `||` never got exercised by a lookup. This tests the value equality
    // directly, the way the operator's ruling actually describes chunk identity: same field AND same index.
    // Bug (found dormant, confirmed by Soul, on Cut 4's path): Wear did not check whether the chunk was already
    // broken. A later hit computed newDamage from a fresh 0f (ChunkWear's default Damage after breaking), stayed
    // under hitpoints, and wrote BrokenUntil = null -- un-breaking a chunk mid-respawn. The rule: damage to a
    // currently-broken chunk (zone time < BrokenUntil) changes nothing. It stays broken until its original
    // respawn time regardless of whether the re-hit is above or below hitpoints, and its wear starts from zero
    // only once it actually respawns.
    [Fact]
    public void HittingABrokenChunkDoesNotUnbreakItEitherBelowOrAboveHitpoints()
    {
        var f = BuildFixture();
        const int index = 4;
        var chunk = new ChunkId(f.BeltA, index);
        var hp = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsA[index].Size);
        var respawn = f.Settings.AsteroidRespawnTime.Evaluate(f.AsteroidsA[index].Size);

        Assert.True(f.Zone.Wear(chunk, hp + 1f));
        Assert.False(f.Zone.ChunkExists(chunk));

        // A hit well under hitpoints, inside the respawn window: must not break it (it is already broken) and
        // must not touch BrokenUntil.
        f.Zone.Update(respawn * 0.2f);
        Assert.False(f.Zone.Wear(chunk, hp * 0.1f), "a hit on an already-broken chunk did not break it");
        Assert.False(f.Zone.ChunkExists(chunk), "must still be absent -- the old bug un-broke it here");
        Assert.Equal(0f, f.Zone.ChunkRadius(chunk));

        // A second hit, this one alone well over hitpoints, inside the same window: still must not break it
        // (it is already broken) and must not push BrokenUntil further out.
        f.Zone.Update(respawn * 0.3f);
        Assert.False(f.Zone.Wear(chunk, hp + 50f), "a hit on an already-broken chunk did not break it, even when large");
        Assert.False(f.Zone.ChunkExists(chunk));

        // Just under the original respawn time (measured from the original break, not from either re-hit):
        // still broken.
        f.Zone.Update(respawn * 0.45f);
        Assert.False(f.Zone.ChunkExists(chunk), "must still be broken just under the ORIGINAL respawn time");

        // Crossing the original respawn time: present again, and unworn -- the re-hits left no residue.
        f.Zone.Update(respawn * 0.1f);
        Assert.True(f.Zone.ChunkExists(chunk), "must respawn at its original time despite the re-hits");
        Assert.Equal(f.Zone.AsteroidBelts[f.BeltA].UndamagedSize(index, f.Settings), f.Zone.ChunkRadius(chunk), 4);

        // Worn again from fresh: breaks on the normal threshold, not one warped by the earlier no-op hits.
        Assert.False(f.Zone.Wear(chunk, hp * 0.9f));
        Assert.True(f.Zone.ChunkExists(chunk));
        Assert.True(f.Zone.Wear(chunk, hp * 0.2f));
        Assert.False(f.Zone.ChunkExists(chunk));
    }

    // Same rule, checked across a save/reload: a re-hit taken while a chunk is broken must not move BrokenUntil,
    // and that must survive the pack/unpack round trip -- not just live in the same Zone instance.
    [Fact]
    public void ReHitDuringRespawnSurvivesSaveRoundTripAtTheSameAbsoluteTime()
    {
        var f = BuildFixture();
        const int idx = 3;
        var chunk = new ChunkId(f.BeltB, idx);
        var hp = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsB[idx].Size);
        var respawn = f.Settings.AsteroidRespawnTime.Evaluate(f.AsteroidsB[idx].Size);

        Assert.True(f.Zone.Wear(chunk, hp + 1f));
        f.Zone.Update(respawn * 0.4f);
        Assert.False(f.Zone.Wear(chunk, hp * 0.5f), "re-hit inside the window did not break an already-broken chunk");

        var pack = f.Zone.PackZone();
        var reloaded = new Zone(f.Items, f.Settings, pack, new GalaxyZone { Name = "MiningCut2ReHitReload", Owner = null }, null);

        Assert.False(reloaded.ChunkExists(chunk), "still broken immediately after reload");

        // Just under the original respawn time (measured from the original break at zone time 0): still broken.
        reloaded.Update(respawn * 0.59f);
        Assert.False(reloaded.ChunkExists(chunk), "must still be broken just under the ORIGINAL respawn time after reload");

        reloaded.Update(respawn * 0.02f);
        Assert.True(reloaded.ChunkExists(chunk), "must respawn at its original absolute time after reload, unmoved by the re-hit");
        Assert.Equal(f.Zone.AsteroidBelts[f.BeltB].UndamagedSize(idx, f.Settings), reloaded.ChunkRadius(chunk), 4);
    }

    [Fact]
    public void ChunkIdEqualityRequiresBothFieldAndIndex()
    {
        var f = BuildFixture();
        var a0 = new ChunkId(f.BeltA, 0);
        var a0Again = new ChunkId(f.BeltA, 0);
        var a1 = new ChunkId(f.BeltA, 1);
        var b0 = new ChunkId(f.BeltB, 0);

        Assert.Equal(a0, a0Again);
        Assert.NotEqual(a0, a1);
        Assert.NotEqual(a0, b0);
    }

    // Stryker gap: the broken-until comparisons (ChunkExists, ChunkRadius) and the pack-time pruning rule
    // (IsExpired) all compare BrokenUntil against zone time at their own boundary, and no existing test landed
    // exactly on it (ABrokenChunkReturnsAtItsRespawnTimeFullyHealed and APackOmitsExpiredWear both advance well
    // past it). Breaking at time 0 and stepping by exactly the authored respawn time lands zone time on
    // BrokenUntil bit-for-bit (0.0 + respawnTime, then 0.0 += respawnTime): the chunk must already read as
    // respawned at that exact instant, not one tick later, and packing at that instant must already prune it.
    [Fact]
    public void RespawnAndPruneBoundaryIsInclusiveOfExactlyNow()
    {
        var f = BuildFixture();
        const int index = 7;
        var chunk = new ChunkId(f.BeltA, index);
        var hp = f.Settings.AsteroidHitpoints.Evaluate(f.AsteroidsA[index].Size);
        var respawn = f.Settings.AsteroidRespawnTime.Evaluate(f.AsteroidsA[index].Size);

        Assert.True(f.Zone.Wear(chunk, hp + 1f));
        f.Zone.Update(respawn); // zone time now equals BrokenUntil exactly, not "respawn plus a margin".

        Assert.True(f.Zone.ChunkExists(chunk), "must read as respawned the instant zone time reaches BrokenUntil, not strictly after it");
        Assert.Equal(f.Zone.AsteroidBelts[f.BeltA].UndamagedSize(index, f.Settings), f.Zone.ChunkRadius(chunk), 4);

        var pack = f.Zone.PackZone();
        var stillPacked = pack.ChunkWear?.Exists(w => w.Field.Equals(chunk.Field) && w.Index == chunk.Index) ?? false;
        Assert.False(stillPacked, "at exactly the respawn instant the entry already carries no live information");
    }
}

internal static class MiningCut2TestExtensions
{
    public static List<CultRecordRef<OrbitData>> ConvertToRefList(this IEnumerable<CultRecordKey> keys)
    {
        var list = new List<CultRecordRef<OrbitData>>();
        foreach (var key in keys) list.Add(new CultRecordRef<OrbitData>(key));
        return list;
    }
}
