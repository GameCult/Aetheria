/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Visibility cut 1 (ruling visibility-per-capability-reveal): every light signature names the channel it travels
// on and the item that produced it, through the one writer Entity.SetSignature, and Visibility stays the sum. No
// number changes. Each fixture is the production path the rule is about: a real ship, real gear, real ticks.
public sealed class SignatureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-signature-" + Guid.NewGuid().ToString("N"));
    private readonly List<CultCache> _caches = new List<CultCache>();

    public SignatureTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var c in _caches) c.Dispose();
        Directory.Delete(_root, true);
    }

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    // Per-test authoring of the numbers a rule is a function of (all default to the values the other tests rely on).
    private float _weaponVisibility = 100f;
    private float _radiationMultiplier = 0f; // the hull radiates nothing unless a test says so
    private float _radiationExponent = 1f;

    private GameplaySettings Settings(float visibilityDecay) => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .1f,
        TargetGearInfoThreshold = .8f,
        FiringArc = 170,
        CommitHorizon = .5f,
        SchematicCellSize = 2f,
        UnaidedAccuracy = .05f,
        UnaidedTracking = 10f,
        UnaidedPrecision = 1000f,
        AgentMinHitProbability = .2f,
        BeamResolveInterval = .1f,
        VisibilityDecay = visibilityDecay,
        // The hull radiates nothing by default, so every sum below is the sum of the authored constants.
        HeatRadiationMultiplier = _radiationMultiplier,
        HeatRadiationExponent = _radiationExponent
    };

    private static GearData Gear(string name, BehaviorData behavior) => new GearData
    {
        Name = name, Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 10,
        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
        Behaviors = { behavior }
    };

    private enum Armament { None, Gun, Beam }

    private sealed class Rig
    {
        public Zone Zone;
        public Ship Ship;
        public EquippedItem WeaponItem;
        public Weapon Weapon;
    }

    private static Shape Solid(int w, int h)
    {
        var shape = new Shape(w, h);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        return shape;
    }

    // A 5x5 hull with a reactor and, optionally, a fused gun or beam (it fires at no target: Range 40 outruns its
    // arming distance 30), then the given gear. A thruster goes on the centre column so it needs no torque.
    private Rig Build(Armament armament, float visibilityDecay, ThrusterData thruster, params BehaviorData[] gear)
    {
        var cache = AetheriaStores.Open(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc"), catalogWritable: true);
        _caches.Add(cache);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var hull = new HullData
        {
            Name = "Skiff", HullType = HullType.Ship, Shape = Solid(5, 5), Durability = 1000000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };
        cache.Upsert(hull);
        cache.Upsert(Gear("Reactor", new ReactorData { Charge = Constant(1000), Efficiency = Constant(1), OverloadEfficiency = Constant(1), ThrottlingFactor = Constant(2) }));
        cache.Upsert(Gear("Targeting", new TargetingSystemData { Accuracy = Constant(1), Resolution = Constant(1000f), Precision = Constant(1000f), Tracking = Constant(1000000f) }));
        if (armament != Armament.None)
        {
            WeaponData behavior = armament == Armament.Beam
                ? new ConstantWeaponData()
                : new InstantWeaponData { Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1) };
            behavior.Damage = Constant(100);
            behavior.Range = Constant(40);
            behavior.MinRange = Constant(0);
            behavior.Velocity = Constant(0);
            behavior.Spread = Constant(0);
            behavior.DamageSpread = Constant(0);
            behavior.Penetration = Constant(0);
            behavior.Energy = Constant(50);
            behavior.Heat = Constant(0);
            behavior.Visibility = Constant(_weaponVisibility);
            behavior.MagazineSize = armament == Armament.Gun ? 5 : 0;
            behavior.DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } };
            cache.Upsert(new WeaponItemData
            {
                Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Fuse = WeaponFuse.Proximity, BlastRadius = 30f,
                Behaviors = { behavior }
            });
        }
        if (thruster != null) cache.Upsert(Gear("Thruster", thruster));
        for (var i = 0; i < gear.Length; i++) cache.Upsert(Gear("Gear" + i, gear[i]));
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 100; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, Settings(visibilityDecay), _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Signatures", Owner = null }, null);
        var lot = 1;
        EquippableItem Make(string name) => new EquippableItem
        {
            Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<ItemData>(name)), Durability = 10, Lot = lot++
        };

        var ship = new Ship(items, zone, new EquippableItem { Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Skiff")), Durability = 1000000, Lot = lot++ }, new EntitySettings());
        var rig = new Rig { Zone = zone, Ship = ship };
        if (armament != Armament.None)
        {
            var gun = Make("Gun");
            Assert.True(ship.TryEquip(gun, new int2(0, 0)));
            rig.WeaponItem = ship.Equipment.Single(x => x.EquippableItem == gun);
            rig.Weapon = (Weapon) rig.WeaponItem.Behaviors.Single(b => b is Weapon);
        }
        if (thruster != null) Assert.True(ship.TryEquip(Make("Thruster"), new int2(2, 1)));
        foreach (var name in new[] { "Targeting", "Reactor" }.Concat(gear.Select((_, i) => "Gear" + i)))
        {
            var item = Make(name);
            Assert.True(ship.TryFindSpace(item, out var at));
            Assert.True(ship.TryEquip(item, at));
        }

        zone.Entities.Add(ship);
        ship.Activate();
        ship.Aim = float3(0, 0, 1);
        zone.Update(1f);
        return rig;
    }

    private static void Fire(Rig r)
    {
        if (r.Weapon is InstantWeapon gun)
        {
            var capacitor = (InputCapacitor) typeof(InstantWeapon).GetField("_capacitor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gun);
            capacitor.AddCharge(1000f);
        }
        r.Ship.SetTarget(TargetRef.None);
        r.Weapon.Activate();
        for (var i = 0; i < 5; i++) r.Zone.Update(.1f);
    }

    private static Signature Of(Ship ship, object source) => ship.VisibilitySources[source];

    private static T Single<T>(Ship ship) where T : Behavior => ship.GetBehavior<T>();

    // ---- EveryWriterNamesItsChannel ----

    // The channel is the writer's meaning, not the strength's: a strong flash and one below any plausible threshold
    // travel on the same channel.
    [Theory]
    [InlineData(100f)]
    [InlineData(.5f)]
    public void AFiredGunShowsALaunchFlashWithItsItem(float visibility)
    {
        _weaponVisibility = visibility;
        var r = Build(Armament.Gun, 0f, null);
        Fire(r);
        var s = Of(r.Ship, r.Weapon);
        Assert.Equal(SignatureChannel.LaunchFlash, s.Channel);
        Assert.Same(r.WeaponItem, s.Item);
        Assert.Equal(visibility, s.Strength, 3);
    }

    [Theory]
    [InlineData(100f)]
    [InlineData(.5f)]
    public void ABurningBeamShowsALaunchFlashWithItsItem(float visibility)
    {
        _weaponVisibility = visibility;
        var r = Build(Armament.Beam, 0f, null);
        Fire(r);
        var s = Of(r.Ship, r.Weapon);
        Assert.Equal(SignatureChannel.LaunchFlash, s.Channel);
        Assert.Same(r.WeaponItem, s.Item);
        Assert.Equal(visibility, s.Strength, 3);
    }

    private static ThrusterData Drive(float visibility) =>
        new ThrusterData { Thrust = Constant(100), Visibility = Constant(visibility), Heat = Constant(0), EnergyUsage = Constant(10) };

    private static void Burn(Rig r, float input)
    {
        r.Ship.MovementDirection = float2(0, -input);
        r.Zone.Update(.1f);
    }

    [Theory]
    [InlineData(10f)]
    [InlineData(.5f)]
    public void ABurningThrusterShowsADrivePlumeWithItsItem(float visibility)
    {
        var r = Build(Armament.None, 0f, Drive(visibility));
        var thruster = Single<Thruster>(r.Ship);
        thruster.Item.UpdatePerformance();
        Burn(r, 1f);
        var s = Of(r.Ship, thruster);
        Assert.Equal(SignatureChannel.DrivePlume, s.Channel);
        Assert.Same(thruster.Item, s.Item);
        Assert.Equal(visibility, s.Strength, 3);
    }

    // The radiator's light is HeatRadiationMultiplier x Emissivity at a radiation exponent of 0 (the temperature
    // drops out), so the strength is a function of two authored inputs: .25 x 3.
    [Fact]
    public void ARadiatorShowsAThermalSignatureWithItsItem()
    {
        _radiationMultiplier = .25f;
        _radiationExponent = 0f;
        var r = Build(Armament.None, 0f, null, new RadiatorData
        {
            Emissivity = Constant(3), PumpedHeat = Constant(0), TemperatureFloor = 0, WasteHeat = Constant(0), EnergyUsage = Constant(0), ThermalMass = Constant(1000)
        });
        var radiator = Single<Radiator>(r.Ship);
        var s = Of(r.Ship, radiator);
        Assert.Equal(SignatureChannel.Thermal, s.Channel);
        Assert.Same(radiator.Item, s.Item);
        Assert.Equal(.75f, s.Strength, 3);
    }

    // At a radiation exponent of 2 the strength is T^2 x multiplier x emissivity of the radiator's own temperature,
    // at two different temperatures: a signature that ignores temperature cannot match both.
    [Fact]
    public void ARadiatorsStrengthFollowsItsTemperature()
    {
        _radiationMultiplier = .001f;
        _radiationExponent = 2f;
        var r = Build(Armament.None, 0f, null, new RadiatorData
        {
            Emissivity = Constant(3), PumpedHeat = Constant(0), TemperatureFloor = 0, WasteHeat = Constant(0), EnergyUsage = Constant(0), ThermalMass = Constant(1000)
        });
        var radiator = Single<Radiator>(r.Ship);
        var seen = new System.Collections.Generic.List<double>();
        for (var round = 0; round < 2; round++)
        {
            var t = radiator.RadiatorTemperature;
            seen.Add(t);
            Assert.True(t > 1f, "fixture: the radiator is warm");
            radiator.Update(0f); // no cooling: the entry is the formula at exactly t
            var expected = Math.Pow(t, 2) * .001 * 3;
            Assert.InRange(Of(r.Ship, radiator).Strength, expected * .999, expected * 1.001);
            r.Zone.Update(1f); // the radiator cools
        }
        Assert.True(Math.Abs(seen[0] - seen[1]) > 1e-3, "fixture: the two temperatures differ");
    }

    // A tick that cools the radiator publishes the strength of the temperature it started the tick at, not the
    // cooled one (follow-up radiator-sampling-instant-pin).
    [Fact]
    public void ARadiatorsSignatureIsSampledBeforeTheTicksCooling()
    {
        _radiationMultiplier = .001f;
        _radiationExponent = 2f;
        var r = Build(Armament.None, 0f, null, new RadiatorData
        {
            Emissivity = Constant(3), PumpedHeat = Constant(0), TemperatureFloor = 0, WasteHeat = Constant(0), EnergyUsage = Constant(0), ThermalMass = Constant(1000)
        });
        var radiator = Single<Radiator>(r.Ship);
        var before = radiator.RadiatorTemperature;
        Assert.True(before > 1f, "fixture: the radiator is warm");

        radiator.Update(1f);

        var cooled = radiator.RadiatorTemperature;
        Assert.True(cooled < before - 1e-3, "fixture: this tick cooled the radiator");
        var expected = Math.Pow(before, 2) * .001 * 3;
        Assert.InRange(Of(r.Ship, radiator).Strength, expected * .999, expected * 1.001);
    }

    [Fact]
    public void AReflectorShowsAReflectionWithItsItem()
    {
        var r = Build(Armament.None, 0f, null, new ReflectorData { CrossSection = Constant(3) });
        var reflector = Single<Reflector>(r.Ship);
        var s = Of(r.Ship, reflector);
        Assert.Equal(SignatureChannel.Reflection, s.Channel);
        Assert.Same(reflector.Item, s.Item);
    }

    [Fact]
    public void TheHullRadiatesOnTheThermalChannelWithNoItem()
    {
        var r = Build(Armament.None, 0f, null);
        var s = Of(r.Ship, r.Ship);
        Assert.Equal(SignatureChannel.Thermal, s.Channel);
        Assert.Null(s.Item);
    }

    // Emission is active emission, never light: SetSignature refuses it before writing, and a Visibility gear
    // authored on it throws on the Zone.Update that first executes it.
    [Theory]
    [InlineData(true, 5f)]
    [InlineData(false, 5f)]
    [InlineData(true, 0f)]
    [InlineData(false, 0f)]
    public void SetSignatureRefusesEmission(bool withItem, float strength)
    {
        var r = Build(Armament.Gun, 0f, null);
        Fire(r);
        var before = r.Ship.VisibilitySources.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.NotEmpty(before);

        Assert.Throws<ArgumentException>(() => r.Ship.SetSignature(new object(), SignatureChannel.Emission, strength, withItem ? r.WeaponItem : null));

        Assert.Equal(before.Count, r.Ship.VisibilitySources.Count);
        foreach (var kv in before) Assert.Equal(kv.Value, r.Ship.VisibilitySources[kv.Key]);
        Assert.Throws<ArgumentException>(() => Build(Armament.None, 0f, null,
            new VisibilityData { Visibility = Constant(2), VisibilityDecay = Constant(0), Channel = SignatureChannel.Emission }));
    }

    // A catalog-authored Visibility behaviour travels on the channel its data names, thermal unless it says more.
    [Theory]
    [InlineData(SignatureChannel.Thermal)]
    [InlineData(SignatureChannel.DrivePlume)]
    public void AVisibilityBehaviourTravelsOnItsAuthoredChannel(SignatureChannel channel)
    {
        var r = Build(Armament.None, 0f, null, new VisibilityData { Visibility = Constant(7), VisibilityDecay = Constant(0), Channel = channel });
        var behaviour = Single<Visibility>(r.Ship);
        var s = Of(r.Ship, behaviour);
        Assert.Equal(channel, s.Channel);
        Assert.Same(behaviour.Item, s.Item);
        Assert.Equal(7f, s.Strength, 3);
    }

    [Fact]
    public void AVisibilityBehaviourDefaultsToThermal()
    {
        Assert.Equal(SignatureChannel.Thermal, new VisibilityData().Channel);
    }

    // ---- VisibilityIsStillTheSum ----

    // Every channel lit at once: a fired gun (100 on LaunchFlash), Visibility gear on Thermal (7), Reflection (4),
    // DrivePlume (3) and a second Reflection (2), a reflector under no sun (0) and the non-radiating hull (0).
    // Visibility is the sum of the strengths, 116, the value the single-number build gave on this fixture; the same
    // total splits by channel over VisibilitySources, and none of it is Emission.
    [Fact]
    public void VisibilityIsStillTheSum()
    {
        var r = Build(Armament.Gun, 0f, null,
            new VisibilityData { Visibility = Constant(7), VisibilityDecay = Constant(0) },
            new VisibilityData { Visibility = Constant(4), VisibilityDecay = Constant(0), Channel = SignatureChannel.Reflection },
            new VisibilityData { Visibility = Constant(3), VisibilityDecay = Constant(0), Channel = SignatureChannel.DrivePlume },
            new VisibilityData { Visibility = Constant(2), VisibilityDecay = Constant(0), Channel = SignatureChannel.Reflection },
            new ReflectorData { CrossSection = Constant(3) });
        Fire(r);
        var ship = r.Ship;

        Assert.Equal(116f, ship.Visibility, 2);
        Assert.Equal(ship.VisibilitySources.Values.Sum(s => s.Strength), ship.Visibility, 3);
        float On(SignatureChannel channel) => ship.VisibilitySources.Values.Where(s => s.Channel == channel).Sum(s => s.Strength);
        Assert.Equal(100f, On(SignatureChannel.LaunchFlash), 2);
        Assert.Equal(7f, On(SignatureChannel.Thermal), 2);
        Assert.Equal(6f, On(SignatureChannel.Reflection), 2);
        Assert.Equal(0f, On(SignatureChannel.Emission), 2);
        Assert.Equal(3f, On(SignatureChannel.DrivePlume), 2);
    }

    // ---- DecayKeepsChannelAndItem ----

    [Fact]
    public void DecayKeepsChannelAndItem()
    {
        const float lambda = .5f;
        var r = Build(Armament.Gun, lambda, null);
        Fire(r);
        var before = Of(r.Ship, r.Weapon);
        Assert.Equal(SignatureChannel.LaunchFlash, before.Channel);

        r.Zone.Update(1f);
        var after = Of(r.Ship, r.Weapon);
        Assert.Equal(decay(before.Strength, lambda, 1f), after.Strength, 3);
        Assert.True(after.Strength < before.Strength);
        Assert.Equal(SignatureChannel.LaunchFlash, after.Channel);
        Assert.Same(r.WeaponItem, after.Item);

        for (var i = 0; i < 30; i++) r.Zone.Update(1f);
        Assert.False(r.Ship.VisibilitySources.ContainsKey(r.Weapon), "a signature below 0.1 is gone");
    }

    // Decay rebuilds every entry from its own. A gun and a drive that has stopped burning wrote once and are never
    // rewritten, so after a long tick the only thing touching them is decay; with a Visibility gear (which rewrites
    // its light each tick) each still names the item of the source that wrote it, and the three items differ.
    [Fact]
    public void DecayKeepsEachSourcesOwnItem()
    {
        var r = Build(Armament.Gun, .5f, Drive(10),
            new VisibilityData { Visibility = Constant(5f), VisibilityDecay = Constant(0), Channel = SignatureChannel.Reflection });
        var thruster = Single<Thruster>(r.Ship);
        var sensor = Single<Visibility>(r.Ship);
        thruster.Item.UpdatePerformance();
        Burn(r, 1f);
        Burn(r, 0f); // the drive stops: its entry is no longer rewritten
        Fire(r);

        var sources = new Behavior[] { thruster, sensor, r.Weapon };
        // What each writer authors, named here and not read back from the fixture's own ticks.
        var writes = new[] { SignatureChannel.DrivePlume, SignatureChannel.Reflection, SignatureChannel.LaunchFlash };
        var before = sources.Select(b => Of(r.Ship, b)).ToArray();
        Assert.Equal(3, before.Select(e => e.Item).Distinct().Count());
        Assert.All(before, e => Assert.True(e.Strength > 1f, "fixture: each source has a live entry"));

        r.Zone.Update(1f);
        for (var i = 0; i < sources.Length; i++)
        {
            var after = Of(r.Ship, sources[i]);
            if (sources[i] != sensor) Assert.True(after.Strength < before[i].Strength, "fixture: decay touched the entry");
            Assert.Same(sources[i].Item, after.Item);
            Assert.Equal(writes[i], after.Channel);
        }
    }

    // The exposed view is not the private dictionary: a cast back to Dictionary cannot write around SetSignature.
    [Fact]
    public void TheVisibilityViewIsNotTheWritableDictionary()
    {
        var r = Build(Armament.None, 0f, null);
        var view = r.Ship.VisibilitySources;
        Assert.False(view is Dictionary<object, Signature>);
        Assert.True(ReferenceEquals(view, r.Ship.VisibilitySources), "one view object per entity"); // not Assert.Same: its failure message formats the entity graph and aborts the test host
        Assert.Throws<NotSupportedException>(() => ((IDictionary<object, Signature>) view)[r.Ship] = new Signature(SignatureChannel.Thermal, 1f, null));
    }

    // ---- APlumeKeepsTheLargerBurn ----

    [Fact]
    public void APlumeKeepsTheLargerBurn()
    {
        var r = Build(Armament.None, 0f, Drive(10));
        var thruster = Single<Thruster>(r.Ship);
        thruster.Item.UpdatePerformance();

        Burn(r, 1f);
        var full = Of(r.Ship, thruster).Strength;
        Burn(r, .5f);
        Assert.Equal(full, Of(r.Ship, thruster).Strength, 3);
        Assert.True(full > 1f, "fixture: the full burn lit the plume");
    }

    [Fact]
    public void APlumeRisesToALargerBurn()
    {
        var r = Build(Armament.None, 0f, Drive(10));
        var thruster = Single<Thruster>(r.Ship);
        thruster.Item.UpdatePerformance();

        Burn(r, .5f);
        var half = Of(r.Ship, thruster).Strength;
        Burn(r, 1f);
        var full = Of(r.Ship, thruster).Strength;
        Assert.True(full > half && half > 0f, $"half {half}, full {full}");
    }

    // ---- The persisted field ----

    private static MessagePackSerializerOptions Options => CultDocumentMessagePackSerialization.OptionsFor(typeof(ZonePack).Assembly);

    // The channel is Key 3 of VisibilityData (union 22): the golden bytes, not a round trip against itself.
    [Fact]
    public void TheChannelIsKey3OfUnion22()
    {
        var data = new VisibilityData { Visibility = Constant(1f), VisibilityDecay = Constant(2f), Channel = SignatureChannel.Emission };
        var bytes = MessagePackSerializer.Serialize<BehaviorData>(data, Options);

        var reader = new MessagePackReader(bytes);
        Assert.Equal(2, reader.ReadArrayHeader());
        Assert.Equal(22, reader.ReadInt32());
        Assert.Equal(4, reader.ReadArrayHeader());
        reader.Skip(); // Group
        Assert.Equal(1f, MessagePackSerializer.Deserialize<PerformanceStat>(ref reader, Options).Min);
        Assert.Equal(2f, MessagePackSerializer.Deserialize<PerformanceStat>(ref reader, Options).Min);
        Assert.Equal(4, reader.ReadInt32());
    }

    // Append-only: a record written before the field existed (three slots) reads as Thermal.
    [Fact]
    public void ARecordWithoutTheChannelReadsAsThermal()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        writer.Write(22);
        writer.WriteArrayHeader(3);
        writer.Write(0);
        writer.Flush();
        MessagePackSerializer.Serialize(buffer, Constant(7f), Options);
        MessagePackSerializer.Serialize(buffer, Constant(0f), Options);

        var data = Assert.IsType<VisibilityData>(MessagePackSerializer.Deserialize<BehaviorData>(buffer.WrittenMemory, Options));
        Assert.Equal(SignatureChannel.Thermal, data.Channel);
        Assert.Equal(7f, data.Visibility.Min);
    }

    // Persisted ordinals: reordering the enum would silently re-label every authored record.
    [Fact]
    public void TheChannelOrdinalsAreFixed()
    {
        Assert.Equal(0, (int) SignatureChannel.Thermal);
        Assert.Equal(1, (int) SignatureChannel.Reflection);
        Assert.Equal(2, (int) SignatureChannel.DrivePlume);
        Assert.Equal(3, (int) SignatureChannel.LaunchFlash);
        Assert.Equal(4, (int) SignatureChannel.Emission);
        Assert.Equal(5, Enum.GetValues(typeof(SignatureChannel)).Length);
    }

    [Fact]
    public void TheChannelIsTheJsonPropertyChannel()
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(new VisibilityData { Channel = SignatureChannel.Emission }));
        Assert.Equal(4, (int) json["channel"]);
    }

    // Through the real catalog store: authored channels come back from a reopened file, the unauthored one Thermal.
    [Fact]
    public void AnAuthoredChannelSurvivesTheCatalogFile()
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".cc");
        using (var cache = AetheriaStores.Open(path, catalogWritable: true))
        {
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.Upsert(Gear("Plume", new VisibilityData { Visibility = Constant(1), Channel = SignatureChannel.DrivePlume }));
            cache.Upsert(Gear("Plain", new VisibilityData { Visibility = Constant(1) }));
            cache.FlushAsync().Wait();
        }

        using var reopened = AetheriaStores.Open(path, catalogWritable: true);
        VisibilityData Read(string name) => Assert.IsType<VisibilityData>(reopened.GetByName<GearData>(name).Behaviors.Single());
        Assert.Equal(SignatureChannel.DrivePlume, Read("Plume").Channel);
        Assert.Equal(SignatureChannel.Thermal, Read("Plain").Channel);
    }
}
