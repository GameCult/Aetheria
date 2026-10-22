using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CultMath;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Cut sim-rate (ruling lower-game-sim-rate, map docs/aetheria-arena-sim-rate-map.md): the game's tick has one owner,
// and content authored in seconds means the same at every tick, because a restarted timer keeps what the last step
// overshot and passive sensing decays exactly.
public sealed class SimRateClockTests
{
    [Fact]
    public void OneTickOwner()
    {
        var clock = SimClock.ForGame();
        Assert.Equal(1f / SimClock.TickRate, SimClock.GameStep);
        Assert.Equal(SimClock.GameStep, clock.Step);
        Assert.Equal(SimClock.TickRate, clock.StepsPerRealSecond);

        // Ten real seconds of irregular frames pay ten seconds of steps, so the game runs at real time.
        var frames = new[] { .004f, .031f, .016f, .0083f, .05f, .022f, .0125f, .027f };
        var real = 0f;
        for (var i = 0; real < 10f; i++)
        {
            var frame = Math.Min(frames[i % frames.Length], 10f - real);
            real += frame;
            clock.Advance(frame, _ => { });
        }
        Assert.InRange(clock.Steps, 10 * SimClock.TickRate - 1, 10 * SimClock.TickRate + 1);
    }
}

public sealed partial class ContactRevealTests
{
    private static readonly float[] CadenceSteps = { 1f / 60f, 1f / 30f, 1f / 20f, 1f / 15f };
    private static readonly float[] Cooldowns = { .25f, .13f };

    // A gun of its own cooldown, bottomless magazine, so only the cooldown paces it. Upserted by name into the open catalog.
    private string GunWithCooldown(float cooldown)
    {
        var name = $"Gun@{cooldown:R}";
        if (_openCache.GetByName<ItemData>(name) != null) return name;
        var gun = Armed(new InstantWeaponData { Count = Const(1), BurstTime = Const(0), Cooldown = Const(cooldown) }, 0);
        _openCache.Upsert(new WeaponItemData
        {
            Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Fuse = WeaponFuse.Proximity, BlastRadius = 30f,
            Behaviors = { gun }
        });
        return name;
    }

    // Holds the trigger for `seconds`, topping up the capacitor so only the cooldown paces the gun. Returns the zone
    // time of each round.
    private List<float> HoldTrigger(Ship ship, EquippedItem item, float dt, float seconds)
    {
        var gun = (InstantWeapon) item.Behaviors.Single(b => b is InstantWeapon);
        var capacitor = (InputCapacitor) typeof(InstantWeapon).GetField("_capacitor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gun);
        var rounds = new List<float>();
        gun.OnFire += _ => rounds.Add(_zone.Time);
        ship.SetTarget(TargetRef.None);
        for (var step = 0; step < (int) MathF.Round(seconds / dt); step++)
        {
            capacitor.AddCharge(1000f);
            gun.Activate();
            Tick(dt);
        }
        return rounds;
    }

    [Fact]
    public void CooldownCadenceIsPerSecond()
    {
        foreach (var cooldown in Cooldowns)
            foreach (var dt in CadenceSteps)
            {
                var ship = Add(float3(0, 0, 0), new[] { GunWithCooldown(cooldown) }, new[] { "Targeting", "Reactor" });
                var rounds = HoldTrigger(ship, Guns(ship)[0], dt, 30f);
                var expected = 30f / cooldown;
                Assert.True(abs(rounds.Count - expected) <= 1.01f, $"cooldown {cooldown} at dt {dt}: {rounds.Count} rounds, expected {expected} +-1");
            }
    }

    [Fact]
    public void CooldownNeverFastFires()
    {
        foreach (var cooldown in Cooldowns)
            foreach (var dt in CadenceSteps)
            {
                var ship = Add(float3(0, 0, 0), new[] { GunWithCooldown(cooldown) }, new[] { "Targeting", "Reactor" });
                var item = Guns(ship)[0];
                for (var i = 0; i < 20; i++) Tick(dt); // idle: the finished timer banks at most one step
                var rounds = HoldTrigger(ship, item, dt, 10f);
                Assert.True(rounds.Count > 3);
                for (var i = 1; i < rounds.Count; i++)
                    Assert.True(rounds[i] - rounds[i - 1] >= cooldown - dt - 1e-4f, $"cooldown {cooldown} at dt {dt}: rounds {rounds[i - 1]} and {rounds[i]}");
            }
    }

    private int PingsHeld(float pingCooldown, float dt, float seconds)
    {
        var name = $"Pinger@{pingCooldown:R}";
        if (_openCache.GetByName<ItemData>(name) == null)
        {
            var sensor = SensorBehavior(sensitivity: 2f);
            sensor.PingCooldown = Const(pingCooldown);
            _openCache.Upsert(new GearData
            {
                Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                Behaviors = { sensor }
            });
        }
        var ship = Add(float3(0, 0, 0), new[] { name });
        var pings = 0;
        SensorOf(ship).OnPingStart += () => pings++;
        for (var step = 0; step < (int) MathF.Round(seconds / dt); step++)
        {
            SensorOf(ship).Ping();
            Tick(dt);
        }
        return pings;
    }

    [Fact]
    public void PingCooldownCarriesItsOvershoot()
    {
        foreach (var cooldown in new[] { .5f, .26f })
            foreach (var dt in CadenceSteps)
            {
                var pings = PingsHeld(cooldown, dt, 30f);
                Assert.True(abs(pings - 30f / cooldown) <= 1.01f, $"ping cooldown {cooldown} at dt {dt}: {pings} pings, expected {30f / cooldown} +-1");
            }
    }

    [Fact]
    public void CooldownBehaviourCarriesItsOvershoot()
    {
        foreach (var cooldown in new[] { .5f, .26f })
            foreach (var dt in CadenceSteps)
            {
                var name = $"Metronome@{cooldown:R}";
                if (_openCache.GetByName<ItemData>(name) == null)
                    _openCache.Upsert(Tool(name, new CooldownData { Cooldown = Const(cooldown) }));
                var ship = Add(float3(0, 0, 0), null, new[] { name });
                var clock = ship.Equipment.SelectMany(i => i.Behaviors).OfType<Cooldown>().Single();
                var fired = 0;
                for (var step = 0; step < (int) MathF.Round(30f / dt); step++)
                {
                    Tick(dt);
                    if (clock.Execute(dt)) fired++;
                }
                Assert.True(abs(fired - 30f / cooldown) <= 1.01f, $"cooldown {cooldown} at dt {dt}: {fired} restarts, expected {30f / cooldown} +-1");
            }
    }

    [Fact]
    public void AmmoIntervalCarriesItsOvershoot()
    {
        _openCache.Upsert(new SimpleCommodityData { Name = "Slug", Shape = new Shape(), Price = 1, MaxStack = 100 });
        foreach (var interval in new[] { .5f, .26f })
            foreach (var dt in CadenceSteps)
            {
                var name = $"Stream@{interval:R}";
                if (_openCache.GetByName<ItemData>(name) == null)
                {
                    var beam = (ConstantWeaponData) Armed(new ConstantWeaponData(), 100000);
                    beam.AmmoInterval = interval;
                    beam.AmmoType = _openCache.RefOf(_openCache.GetByName<SimpleCommodityData>("Slug"));
                    _openCache.Upsert(new WeaponItemData
                    {
                        Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                        MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                        Fuse = WeaponFuse.Proximity, BlastRadius = 30f,
                        Behaviors = { beam }
                    });
                }
                var ship = Add(float3(0, 0, 0), new[] { name }, new[] { "Targeting", "Reactor" });
                var weapon = ship.Equipment.SelectMany(i => i.Behaviors).OfType<ConstantWeapon>().Single();
                typeof(ConstantWeapon).GetField("_ammo", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, 100000);
                ship.SetTarget(TargetRef.None);
                weapon.Activate();
                for (var step = 0; step < (int) MathF.Round(30f / dt); step++) Tick(dt);
                var spent = 100000 - weapon.Ammo;
                Assert.True(abs(spent - 30f / interval) <= 1.01f, $"ammo interval {interval} at dt {dt}: {spent} spent, expected {30f / interval} +-1");
            }
    }

    // A fixed target under passive sensing settles where Entity.PassiveInfo says (G / k), at any step.
    [Fact]
    public void PassiveInfoSettlesAtTheDocumentedLevel()
    {
        foreach (var dt in new[] { 1f / 60f, 1f / 15f })
        {
            var observer = Add(float3(0, 0, 0), new[] { "Eye" });
            var target = Add(float3(0, 0, 100));
            Tick(.1f); // brings the signatures and sensors up
            var distance = 100f;
            while (observer.PassiveInfo(float2(0, distance), target.Visibility) > .5f)
            {
                distance *= 2;
                target.Position = float3(0, 0, distance);
            }
            var documented = observer.PassiveInfo(target.Position.xz, target.Visibility);
            Assert.InRange(documented, 1e-4f, .5f);
            for (var step = 0; step < (int) MathF.Round(40f / dt); step++) Tick(dt);
            Assert.InRange(Track(observer, target), documented - 1e-3f, documented + 1e-3f);
        }
    }
}
