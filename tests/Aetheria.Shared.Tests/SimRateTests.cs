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
            var marker = new object();
            void Glow() => target.SetSignature(marker, SignatureChannel.Thermal, 5f, null); // held as bright as a thruster
            Glow();
            Tick(0f);
            var distance = 100f;
            while (observer.PassiveInfo(float2(0, distance), target.Visibility) > .5f)
            {
                distance *= 2;
                target.Position = float3(0, 0, distance);
            }
            var documented = observer.PassiveInfo(target.Position.xz, target.Visibility);
            Assert.InRange(documented, 1e-4f, .5f);
            for (var step = 0; step < (int) MathF.Round(40f / dt); step++)
            {
                Glow();
                Tick(dt);
            }
            Assert.InRange(Track(observer, target), documented - 1e-3f, documented + 1e-3f);
        }
    }

    // An idle timer banks at most one step: after ten idle seconds the first two restarts are still a cooldown apart.
    [Fact]
    public void AnIdleSensorBanksAtMostOneStep()
    {
        foreach (var dt in CadenceSteps)
        {
            var name = "Pinger@.5";
            if (_openCache.GetByName<ItemData>(name) == null)
            {
                var sensor = SensorBehavior(sensitivity: 2f);
                sensor.PingCooldown = Const(.5f);
                _openCache.Upsert(new GearData
                {
                    Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                    MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                    Behaviors = { sensor }
                });
            }
            var ship = Add(float3(0, 0, 0), new[] { name });
            var times = new List<float>();
            SensorOf(ship).OnPingStart += () => times.Add(_zone.Time);
            for (var step = 0; step < (int) MathF.Round(10f / dt); step++) Tick(dt);
            for (var step = 0; step < (int) MathF.Round(3f / dt); step++)
            {
                SensorOf(ship).Ping();
                Tick(dt);
            }
            Assert.True(times.Count >= 4);
            for (var i = 1; i < times.Count; i++)
                Assert.True(times[i] - times[i - 1] >= .5f - dt - 1e-4f, $"dt {dt}: pings at {times[i - 1]} and {times[i]}");
        }
    }

    [Fact]
    public void AnIdleCooldownBanksAtMostOneStep()
    {
        foreach (var dt in CadenceSteps)
        {
            var name = "Metronome@.5";
            if (_openCache.GetByName<ItemData>(name) == null)
                _openCache.Upsert(Tool(name, new CooldownData { Cooldown = Const(.5f) }));
            var ship = Add(float3(0, 0, 0), null, new[] { name });
            var clock = ship.Equipment.SelectMany(i => i.Behaviors).OfType<Cooldown>().Single();
            for (var step = 0; step < (int) MathF.Round(10f / dt); step++) Tick(dt);
            var times = new List<float>();
            for (var step = 0; step < (int) MathF.Round(3f / dt); step++)
            {
                Tick(dt);
                if (clock.Execute(dt)) times.Add(_zone.Time);
            }
            Assert.True(times.Count >= 4);
            for (var i = 1; i < times.Count; i++)
                Assert.True(times[i] - times[i - 1] >= .5f - dt - 1e-4f, $"dt {dt}: restarts at {times[i - 1]} and {times[i]}");
        }
    }

    // A magazine of three, cooldown .1 s and a .9 s reload is three rounds a 1.1 s cycle, at any step.
    [Fact]
    public void MagazineCadenceIsPerSecond()
    {
        foreach (var dt in CadenceSteps)
        {
            var name = "Magazine3";
            if (_openCache.GetByName<ItemData>(name) == null)
            {
                var gun = Armed(new InstantWeaponData { Count = Const(1), BurstTime = Const(0), Cooldown = Const(.1f), ReloadTime = .9f }, 3);
                _openCache.Upsert(new WeaponItemData
                {
                    Name = name, Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
                    MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
                    Fuse = WeaponFuse.Proximity, BlastRadius = 30f,
                    Behaviors = { gun }
                });
            }
            var ship = Add(float3(0, 0, 0), new[] { name }, new[] { "Targeting", "Reactor" });
            var rounds = HoldTrigger(ship, Guns(ship)[0], dt, 33f);
            Assert.True(abs(rounds.Count - 90f) <= 3.01f, $"dt {dt}: {rounds.Count} rounds in 33 s, expected 90 +-3");
        }
    }
}

public sealed partial class RunStartTests
{
    private static readonly string EquivalenceTable = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "simrate-equivalence.txt");

    // The two-AI fight over 40 match seeds at the old 60 Hz and at the game's step: the same fight to within noise. The
    // win rate (east's hull above west's) differs by less than a 95% two-proportion interval, and the mean survivor hull
    // and the mean time to first kill each move by less than 10%. The table is written beside the test for the report.
    [Fact, Trait("Category", "Slow")]
    public void SimRateEquivalence()
    {
        const int seeds = 40;
        var table = new System.Text.StringBuilder();
        (double wins, double survivor, double firstKill, double seconds) Run(float dt)
        {
            var wins = 0;
            double survivor = 0, firstKill = 0, seconds = 0;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var fight = TwoAiFight(seed, dt);
                if (fight.eastHull > fight.westHull) wins++;
                survivor += Math.Max(fight.eastHull, fight.westHull);
                firstKill += fight.firstKill;
                seconds += fight.stepSeconds;
                table.AppendLine($"dt {dt:R} seed {seed}: east {fight.eastHull:R} west {fight.westHull:R} first kill {fight.firstKill:R} s, {fight.stepSeconds:F2} s wall");
            }
            return (wins / (double) seeds, survivor / seeds, firstKill / seeds, seconds);
        }

        var old = Run(1f / 60f);
        var now = Run(SimClock.GameStep);
        var se = Math.Sqrt(old.wins * (1 - old.wins) / seeds + now.wins * (1 - now.wins) / seeds);
        table.AppendLine($"60 Hz: east wins {old.wins:P0}, mean survivor hull {old.survivor:F1}, mean first kill {old.firstKill:F2} s, {old.seconds:F1} s wall");
        table.AppendLine($"{SimClock.TickRate} Hz: east wins {now.wins:P0}, mean survivor hull {now.survivor:F1}, mean first kill {now.firstKill:F2} s, {now.seconds:F1} s wall");
        table.AppendLine($"win-rate difference {Math.Abs(old.wins - now.wins):F3} (95% interval +-{1.96 * se:F3}); wall-time speedup {old.seconds / now.seconds:F2}x");
        System.IO.File.WriteAllText(EquivalenceTable, table.ToString());

        Assert.True(Math.Abs(old.wins - now.wins) <= 1.96 * se, table.ToString());
        Assert.True(Math.Abs(now.survivor - old.survivor) <= .1 * old.survivor, table.ToString());
        Assert.True(Math.Abs(now.firstKill - old.firstKill) <= .1 * old.firstKill, table.ToString());
    }

    // Every shipped cooldown, ping cooldown and ammo interval spans at least two steps. A burst interval under one step
    // fires several rounds in one step (carry makes that exact on average); those are listed, not failed.
    [Fact]
    public void ShortestTimerIsSeveralSteps()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var timers = new List<(string item, string timer, float seconds)>();
        var bursts = new List<string>();
        void Read(string item, IEnumerable<BehaviorData> behaviors)
        {
            foreach (var behavior in behaviors.Where(b => b != null))
                switch (behavior)
                {
                    case InstantWeaponData gun:
                        timers.Add((item, "Cooldown", Math.Min(gun.Cooldown.Min, gun.Cooldown.Max)));
                        var count = Math.Max(1f, Math.Min(gun.Count.Min, gun.Count.Max));
                        var interval = Math.Min(gun.BurstTime.Min, gun.BurstTime.Max) / count;
                        if (count > 1 && interval < SimClock.GameStep) bursts.Add($"{item}: burst interval {interval:R} s");
                        break;
                    case SensorData sensor:
                        timers.Add((item, "PingCooldown", Math.Min(sensor.PingCooldown.Min, sensor.PingCooldown.Max)));
                        break;
                    case ConstantWeaponData beam when beam.AmmoType.IsSet():
                        timers.Add((item, "AmmoInterval", beam.AmmoInterval));
                        break;
                    case CooldownData cooldown:
                        timers.Add((item, "Cooldown", Math.Min(cooldown.Cooldown.Min, cooldown.Cooldown.Max)));
                        break;
                }
        }
        foreach (var item in cache.GetAll<EquippableItemData>()) Read(item.Name, item.Behaviors);
        foreach (var item in cache.GetAll<ConsumableItemData>()) Read(item.Name, item.Behaviors);

        var offenders = timers.Where(t => t.seconds > 0 && t.seconds < 2 * SimClock.GameStep)
            .Select(t => $"{t.item}.{t.timer} = {t.seconds:R} s").ToList();
        Assert.True(offenders.Count == 0, $"timers under two steps of {SimClock.GameStep:R} s: {string.Join("; ", offenders)}. Burst intervals under a step: {string.Join("; ", bursts)}");
    }
}
