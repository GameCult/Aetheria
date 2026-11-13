/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using UniRx;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Mining Cut 4b (docs/mining-cut-refresh.md, Q12 = A): a shot at a rock goes through FireControl's one fire path
// and wears it; the rock is a disc. The fixture is MiningCut3Tests' belt scene: a shooter at the origin, one rock
// straight ahead (+z, phase .25) at `Distance`, a gun and a targeting system fitted on the shooter.
public sealed class MiningCut4bTests : IDisposable
{
    private const float Distance = 100f;
    private readonly MiningCut3Tests _fixtures = new MiningCut3Tests();

    public void Dispose() => _fixtures.Dispose();

    internal sealed class Rig
    {
        public MiningCut3Tests.Scene Scene;
        public Zone Zone => Scene.Zone;
        public Ship Shooter;
        public EquippedItem GunItem;
        public Weapon Gun;
        public ChunkId Rock;
        public List<ShotOutcome> Resolved = new List<ShotOutcome>();
    }

    private static InstantWeaponData Behavior(bool launcher, float damage, float velocity, float spread, float effective = float.PositiveInfinity)
    {
        InstantWeaponData data = launcher ? new LockWeaponData
        {
            LockSpeed = MiningCut3Tests.Constant(1000), SensorImpact = MiningCut3Tests.Constant(1), LockAngle = MiningCut3Tests.Constant(180),
            DirectionImpact = MiningCut3Tests.Constant(1), Decay = MiningCut3Tests.Constant(1)
        } : new InstantWeaponData();
        data.Damage = MiningCut3Tests.Constant(damage);
        data.Range = MiningCut3Tests.Constant(500f);
        data.MinRange = MiningCut3Tests.Constant(0);
        data.EffectiveRange = MiningCut3Tests.Constant(effective);
        data.DamageAtMaxRange = 0f;
        data.Velocity = MiningCut3Tests.Constant(velocity);
        data.Spread = MiningCut3Tests.Constant(spread);
        data.DamageSpread = MiningCut3Tests.Constant(0);
        data.Penetration = MiningCut3Tests.Constant(0);
        data.Count = MiningCut3Tests.Constant(1);
        data.BurstTime = MiningCut3Tests.Constant(0);
        data.Cooldown = MiningCut3Tests.Constant(1000);
        return data;
    }

    // `precision` 1 gives sigma 1 cell; accuracy 1 and a resolution that asks little of the info make PSensor 1.
    internal Rig Build(bool sensor = true, bool launcher = false, float damage = 10f, float velocity = 0f, float spread = 0f,
        float precision = 1f, float effective = float.PositiveInfinity, WeaponFuse? fuse = null, float blastRadius = 0f,
        float rockSize = 0f, float crossSection = 20f, bool target = true, float tracking = 1000000f, FieldKindData kind = null, uint galaxySeed = 0)
    {
        _fixtures.GalaxySeed = galaxySeed;
        kind ??= MiningCut3Tests.Kind("Asteroid", crossSection);
        var scene = _fixtures.BuildScene(new[] { kind }, MiningCut3Tests.Belt(kind, MiningCut3Tests.Rock(Distance, .25f, rockSize)));
        var gear = MiningCut3Tests.Gear("Targeting", new TargetingSystemData
        {
            Accuracy = MiningCut3Tests.Constant(1f), Resolution = MiningCut3Tests.Constant(1000f),
            Precision = MiningCut3Tests.Constant(precision), Tracking = MiningCut3Tests.Constant(tracking)
        });
        scene.Cache.Upsert(gear);
        var gun = new WeaponItemData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Fuse = fuse, BlastRadius = blastRadius,
            Behaviors = { Behavior(launcher, damage, velocity, spread, effective) }
        };
        scene.Cache.Upsert(gun);

        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hull = new HullData
        {
            Name = "Gunship", HullType = HullType.Ship, Shape = shape, Durability = 10, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };
        scene.Cache.Upsert(hull);
        var shooter = new Ship(scene.Items, scene.Zone, MiningCut3Tests.Mint(scene, hull), new EntitySettings());
        if (sensor) Assert.True(shooter.TryEquip(MiningCut3Tests.Mint(scene, scene.Eye)), "eye");
        Assert.True(shooter.TryEquip(MiningCut3Tests.Mint(scene, gear)), "targeting");
        var minted = MiningCut3Tests.Mint(scene, gun);
        Assert.True(shooter.TryEquip(minted, new int2(0, 0)), "gun");
        scene.Zone.Admit(shooter, piloted: false);
        shooter.Position = float3.zero;
        shooter.Direction = float2(0, 1);
        shooter.Aim = float3(0, 0, 1);

        shooter.Update(.05f); // the fitted sensor becomes active on its first tick; zone time does not advance
        shooter.Position = float3.zero;
        shooter.Velocity = float2.zero;
        var item = shooter.Equipment.Single(x => x.EquippableItem == minted);
        var rig = new Rig
        {
            Scene = scene, Shooter = shooter, GunItem = item, Gun = (Weapon) item.Behaviors.Single(b => b is Weapon),
            Rock = new ChunkId(scene.Belts[0], 0)
        };
        scene.Zone.ShotResolved.Subscribe(outcome => rig.Resolved.Add(outcome));
        if (target && sensor) Assert.True(shooter.SetTarget(rig.Rock), "target");
        return rig;
    }

    private static float Phi(double z) => (float) (.5 * (1 + erf((float) (z / Math.Sqrt(2)))));

    // The rock's radius in cells is the one the zone reports, sigma the targeting system's (floored at half a cell).
    private static float Disc(Rig rig)
    {
        var sigma = max(1f / FireControl.Precision(rig.Shooter), .5f);
        var radius = rig.Zone.ChunkCells(rig.Rock);
        return Phi(radius / sigma) - Phi(-radius / sigma);
    }

    // PSensor as the rule states it, for a gear that wants only the detection threshold plus a thousandth of the rest.
    private static float Sensor(Rig rig, float info)
    {
        var th = rig.Scene.Items.GameplaySettings.TargetDetectionInfoThreshold;
        return saturate(unlerp(th, th + (1f - th) / 1000f, info));
    }

    // ==== one fire path ====

    // Accuracy, PSensor, PSpread, Sigma and PMount price a rock exactly as a ship at equal range, precision and info:
    // a two-cell ship (span 2, centre-aimed) and a rock one cell in radius (span 2) are the same disc to the gun, and
    // an unauthored gun follows anything, so the mount is 1 for both.
    [Fact]
    public void ARockShotUsesTheSameFactors()
    {
        var rig = Build(spread: 4f, precision: 1f, crossSection: 1f);
        var s = rig.Scene;
        Assert.Equal(1f, rig.Zone.ChunkCells(rig.Rock), 4);
        var info = rig.Shooter.ChunkInfo(rig.Rock);
        Assert.True(info > .11f && info < 1f, $"the rock's info {info} should be visible and unsaturated");

        var hull = new HullData { Name = "Pair", HullType = HullType.Ship, Shape = new Shape(2, 2), Durability = 10, Mass = 1000 };
        foreach (var cell in hull.Shape.AllCoordinates) hull.Shape[cell] = true;
        s.Cache.Upsert(hull);
        var at = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
        var ship = new Ship(s.Items, rig.Zone, MiningCut3Tests.Mint(s, hull), new EntitySettings());
        rig.Zone.Admit(ship, piloted: false);
        ship.Position = float3(at.x, 0, at.y);
        ship.Direction = float2(0, 1);
        rig.Shooter.EntityInfoGathered[ship] = info;
        rig.Shooter.VisibleEntities.Add(ship);

        var rock = FireControl.HitProbability(rig.Gun, rig.Shooter, rig.Rock);
        var vessel = FireControl.HitProbability(rig.Gun, rig.Shooter, ship);
        Assert.InRange(rock, .05f, .99f);
        Assert.Equal(vessel, rock, 4);
    }

    [Fact]
    public void AnUnseenRockCannotBeFiredOn()
    {
        var blind = Build(sensor: false);
        Assert.False(FireControl.Designated(blind.Gun, blind.Shooter, blind.Rock, out var range));
        Assert.Equal(Distance, range, 2);
        Assert.Equal(0f, FireControl.HitProbability(blind.Gun, blind.Shooter, blind.Rock));
        Assert.False(FireControl.Solve(blind.Gun, blind.GunItem, blind.Shooter, blind.Rock).Designated);

        var sighted = Build();
        Assert.True(FireControl.Designated(sighted.Gun, sighted.Shooter, sighted.Rock, out _));
        Assert.True(FireControl.HitProbability(sighted.Gun, sighted.Shooter, sighted.Rock) > 0f);
    }

    // Q12 = A: a launcher (a LockWeapon) is never designated on a rock, locked or not.
    [Fact]
    public void LaunchersCannotFireOnARock()
    {
        var launcher = Build(launcher: true);
        Assert.False(FireControl.Designated(launcher.Gun, launcher.Shooter, launcher.Rock, out _));
        Assert.Equal(0f, FireControl.HitProbability(launcher.Gun, launcher.Shooter, launcher.Rock));
        var plain = Build();
        Assert.True(FireControl.Designated(plain.Gun, plain.Shooter, plain.Rock, out _));
    }

    // POnHull is the closed form Phi(R/sigma) - Phi(-R/sigma): a zero-spread gun with a full sensor reading is priced by the
    // disc alone.
    [Fact]
    public void DiscMassIsTheClosedForm()
    {
        var rig = Build(spread: 0f, precision: 1f, rockSize: .5f);
        var info = rig.Shooter.ChunkInfo(rig.Rock);
        var expected = Sensor(rig, info) * Disc(rig);
        Assert.InRange(Disc(rig), .5f, .99f);
        Assert.Equal(expected, FireControl.HitProbability(rig.Gun, rig.Shooter, rig.Rock), 4);
    }

    // Commit's price at fire time is the forecast: for a rock still at the instant it is fired at, one price.
    [Fact]
    public void TheForecastIsTheCommitPriceForARock()
    {
        var rig = Build(spread: 4f, precision: 1f, velocity: 50f);
        var forecast = FireControl.HitProbability(rig.Gun, rig.Shooter, rig.Rock);
        var id = FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
        Assert.True(rig.Zone.TryGetShot(id, out var shot));
        Assert.Equal(rig.Rock, shot.Chunk);
        Assert.Null(shot.Target);
        Assert.Equal(forecast, FireControl.CommitProbability(shot, shot.FireTime, out _), 4);
    }

    // ==== resolution ====

    internal static void Run(Rig rig, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += .01f) rig.Zone.Update(.01f);
    }

    // A rock hit has no lane and no shield. Hits wear the rock by the shot's Damage after the falloff, and each
    // publishes one absorb on the zone's ChunkCapabilities at the rock's surface facing the shot.
    [Fact]
    public void DirectHitsWearTheRock()
    {
        var rig = Build(damage: 3f, rockSize: .5f, precision: 1000f, effective: 50f);
        var absorbs = new List<AbsorbEvent>();
        var surface = new List<float>();
        rig.Zone.ChunkCapabilities.Absorb.Subscribe(e =>
        {
            absorbs.Add(e);
            var centre = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
            surface.Add(length(float2(e.Position.x, e.Position.z) - centre) - rig.Zone.ChunkRadius(rig.Rock));
        });
        var before = rig.Zone.ChunkRadius(rig.Rock);
        var factor = WeaponData.RangeFactor(0f, 50f, 500f, 0f, Distance);
        Assert.InRange(factor, .01f, .99f);

        for (var shot = 0; shot < 12; shot++)
        {
            FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
            Run(rig, .02f);
        }

        var hits = rig.Resolved.Count(o => o.Hit);
        Assert.InRange(hits, 1, 12);
        Assert.Equal(hits, absorbs.Count);
        Assert.All(rig.Resolved.Where(o => o.Hit), o => { Assert.Equal(0, o.Cell.x); Assert.Equal(0, o.Cell.y); Assert.False(o.Shielded); });
        Assert.All(absorbs, e => Assert.Equal(3f * factor, e.Magnitude, 2));
        Assert.All(surface, d => Assert.True(abs(d) < 1e-3f, $"absorb was {d} off the rock's surface"));
        Assert.All(absorbs, e => Assert.Equal(AbsorbLayer.Hull, e.Layer));
        var hp = MiningCut3Tests.PlanetSettings().AsteroidHitpoints.Evaluate(.5f);
        var expected = MiningCut3Tests.PlanetSettings().AsteroidSize.Evaluate((hp - hits * 3f * factor) / hp * .5f);
        Assert.Equal(expected, rig.Zone.ChunkRadius(rig.Rock), 3);
        Assert.True(rig.Zone.ChunkRadius(rig.Rock) < before);
    }

    // A contact round that hits a rock detonates at its centre as it arrives, through Detonate: a bystander beside it
    // takes the blast from the rock's centre, not from the muzzle or from a lane.
    [Fact]
    public void AContactRoundAtARockDetonatesAtItsCentre()
    {
        var rig = Build(damage: 10f, fuse: WeaponFuse.Contact, blastRadius: 6f, precision: 1000f, rockSize: .5f);
        var s = rig.Scene;
        var at = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
        var bystander = new Ship(s.Items, rig.Zone, MiningCut3Tests.Mint(s, s.Hull), new EntitySettings());
        rig.Zone.Admit(bystander, piloted: false);
        bystander.Position = float3(at.x + 3f, 0, at.y);
        var burstAt = new List<float>();
        bystander.Capabilities.Absorb.Subscribe(e =>
        {
            var centre = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
            burstAt.Add(length(float2(e.Position.x, e.Position.z) - centre));
        });

        for (var shot = 0; shot < 12; shot++)
        {
            FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
            Run(rig, .02f);
        }

        Assert.InRange(rig.Resolved.Count(o => o.Hit), 1, 12);
        Assert.NotEmpty(burstAt);
        Assert.All(burstAt, d => Assert.True(d < 1e-2f, $"blast was {d} from the rock's centre"));
    }

    // A rock that no longer exists when its round arrives, broken in flight, resolves as a miss, like a target that
    // left the zone: nothing is worn and nothing absorbs.
    [Fact]
    public void ARockThatBreaksMidFlightIsAMiss()
    {
        var rig = Build(velocity: 50f, precision: 1000f);
        var absorbs = 0;
        rig.Zone.ChunkCapabilities.Absorb.Subscribe(_ => absorbs++);
        FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
        Assert.Empty(rig.Resolved);

        Assert.True(rig.Zone.Breaks(rig.Rock, 1e6f));
        Run(rig, 2.5f);

        var outcome = Assert.Single(rig.Resolved);
        Assert.Equal(ShotResult.Miss, outcome.Result);
        Assert.Equal(0, absorbs);
        Assert.False(rig.Zone.ChunkExists(rig.Rock));
    }

    // The shot's own dice: two zones built alike roll the same sequence of outcomes for the same shot ids.
    [Fact]
    public void RockDiceAreTheShotsOwn()
    {
        List<ShotResult> Roll()
        {
            var rig = Build(precision: 1f);
            for (var shot = 0; shot < 20; shot++)
            {
                FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
                Run(rig, .02f);
            }
            return rig.Resolved.Select(o => o.Result).ToList();
        }

        var first = Roll();
        var second = Roll();
        Assert.Equal(20, first.Count);
        Assert.Contains(ShotResult.Hit, first);
        Assert.Contains(ShotResult.Miss, first);
        Assert.Equal(first, second);
    }

    // A rock's velocity is the pose function's derivative, derived on every read.
    [Fact]
    public void ARockMovesAtItsPoseVelocity()
    {
        var rig = Build();
        var velocity = rig.Zone.ChunkVelocity(rig.Rock);
        var ahead = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index, rig.Zone.Time + .5).xy;
        var now = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
        Assert.True(length(velocity) > 1f);
        Assert.True(length(velocity * .5f - (ahead - now)) < .05f * length(ahead - now));
    }

    // A rock is not a Ship: its evasion is 0, so Tracking (the metres of deviation a shooter forgives) never
    // discounts the price of a rock. The same rock priced by a tight and by a loose tracker reads one price.
    [Fact]
    public void ARockEvadesNothing()
    {
        var tight = Build(spread: 4f, precision: 1f, tracking: 10f);
        var loose = Build(spread: 4f, precision: 1f, tracking: 1000000f);
        var tightPrice = FireControl.HitProbability(tight.Gun, tight.Shooter, tight.Rock);
        var loosePrice = FireControl.HitProbability(loose.Gun, loose.Shooter, loose.Rock);
        Assert.InRange(loosePrice, .05f, .99f);
        Assert.Equal(loosePrice, tightPrice, 5);
    }

    // The lead is the intercept of the rock's pose velocity: the point is where the rock will be when a round of the
    // gun's velocity gets there, derived here from the time of flight, not from the code under test.
    [Fact]
    public void AMovingRockIsLeadAtItsPoseVelocity()
    {
        var rig = Build(velocity: 50f);
        var velocity = rig.Zone.ChunkVelocity(rig.Rock);
        var now = rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy;
        Assert.True(length(velocity) > 1f);

        var point = FireControl.PredictedIntercept(rig.Gun, rig.Shooter, rig.Rock);

        var flight = length(point.xz - rig.Shooter.Position.xz) / 50f;
        var there = now + velocity * flight;
        Assert.True(length(point.xz - now) > .5f, "the aim leads the rock");
        Assert.Equal(there.x, point.x, 2);
        Assert.Equal(there.y, point.z, 2);
    }

    // A rock that breaks after its round committed a hit resolves as a miss through Step's gone rule: the frozen
    // outcome says Hit, so only that rule stops the round from absorbing on, and wearing, a rock that is gone.
    [Fact]
    public void ARockThatBreaksAfterCommitIsAMissNotAFrozenHit()
    {
        var rig = Build(velocity: 50f, precision: 1000f);
        var committed = new List<ShotOutcome>();
        rig.Zone.ShotCommitted.Subscribe(committed.Add);
        var absorbs = 0;
        rig.Zone.ChunkCapabilities.Absorb.Subscribe(_ => absorbs++);
        FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
        for (var waited = 0f; committed.Count == 0 && waited < 3f; waited += .01f) rig.Zone.Update(.01f);
        var frozen = Assert.Single(committed);
        Assert.True(frozen.Hit, "fixture: the commit rolled a hit");
        Assert.Empty(rig.Resolved);

        Assert.True(rig.Zone.Breaks(rig.Rock, 1e6f));
        Run(rig, 2.5f);

        var outcome = Assert.Single(rig.Resolved);
        Assert.False(outcome.Hit);
        Assert.Equal(0, absorbs);
    }

    private static void Wear(Rig rig, WeaponFuse fuse, int shots, out int hits, out List<AbsorbEvent> absorbs, out float worn)
    {
        var seen = new List<AbsorbEvent>();
        rig.Zone.ChunkCapabilities.Absorb.Subscribe(seen.Add);
        for (var shot = 0; shot < shots; shot++)
        {
            FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
            Run(rig, .02f);
        }
        hits = rig.Resolved.Count(o => o.Hit);
        absorbs = seen;
        worn = seen.Sum(e => e.Magnitude);
    }

    // A contact round that hits a rock wears it exactly once, by the blast's share through Detonate: one absorb per hit,
    // and the rock's wear is the sum of those absorbs' magnitudes, with no Damage added on top.
    [Fact]
    public void AContactRoundWearsARockExactlyOnce()
    {
        var rig = Build(damage: 10f, fuse: WeaponFuse.Contact, blastRadius: 6f, precision: 1000f, rockSize: .5f);
        Wear(rig, WeaponFuse.Contact, 12, out var hits, out var absorbs, out var worn);

        Assert.InRange(hits, 1, 12);
        Assert.Equal(hits, absorbs.Count);
        var belt = rig.Zone.AsteroidBelts[rig.Rock.Field];
        Assert.True(rig.Zone.ChunkExists(rig.Rock));
        Assert.Equal(belt.DamagedSize(rig.Rock.Index, worn, rig.Zone.Settings), rig.Zone.ChunkRadius(rig.Rock), 3);
    }

    // The same for a proximity round, which detonates at its burst point hit or miss: one absorb per resolved round
    // that covers the rock, and no wear beyond them.
    [Fact]
    public void AProximityRoundWearsARockExactlyOnce()
    {
        var rig = Build(damage: 10f, fuse: WeaponFuse.Proximity, blastRadius: 6f, precision: 1000f, rockSize: .5f);
        Wear(rig, WeaponFuse.Proximity, 12, out _, out var absorbs, out var worn);

        Assert.Equal(12, rig.Resolved.Count);
        Assert.NotEmpty(absorbs);
        var belt = rig.Zone.AsteroidBelts[rig.Rock.Field];
        Assert.True(rig.Zone.ChunkExists(rig.Rock));
        Assert.Equal(belt.DamagedSize(rig.Rock.Index, worn, rig.Zone.Settings), rig.Zone.ChunkRadius(rig.Rock), 3);
    }

    // A rock's absorb has one height rule whoever reports it: a direct hit and a blast both publish at height 0, not at
    // the shooter's.
    [Fact]
    public void ARockAbsorbHasOneHeightRule()
    {
        var rig = Build(damage: 3f, rockSize: .5f, precision: 1000f);
        rig.Shooter.Position = float3(0, 7, 0);
        var heights = new List<float>();
        rig.Zone.ChunkCapabilities.Absorb.Subscribe(e => heights.Add(e.Position.y));
        for (var shot = 0; shot < 6; shot++)
        {
            FireControl.Fire(rig.Gun, rig.GunItem, rig.Shooter);
            Run(rig, .02f);
        }
        var direct = heights.Count;
        FireControl.Detonate(rig.Zone, rig.Zone.ChunkPose(rig.Rock.Field, rig.Rock.Index).xy, 5f, 3f, DamageType.Kinetic);

        Assert.True(direct > 0 && heights.Count > direct);
        Assert.All(heights, h => Assert.Equal(0f, h));
    }
}
