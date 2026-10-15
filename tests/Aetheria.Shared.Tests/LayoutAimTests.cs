using System;
using System.Linq;
using CultMath;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Visibility 5b (rulings quiet-gear-identifiable-hull-layout-known, ew-aim-under-hull-lie-ruled, reveal-ends-on-perception,
// schematic-and-model-are-separate-spaces): an observer that identifies a hull knows where its mounts are, and can aim at a
// mount it has located without knowing what is fitted there. The fixture hull "Bay" has a reactor mount (the 2x2 block at
// (3,3), the reactor the one cell at (3,3) inside it), a shield mount and a weapon mount, and nothing ever acts unless a test
// makes it. Identified is TargetGearInfoThreshold (.8) of the observer's info on the ship. Every fixture drives the
// production path (Fire, CommitProbability, SchematicRows, TrySelectTargetItem, the AI's Combat state inside Zone.Update).
// Entities and items are compared with ReferenceEquals: a failing xunit assertion formats its arguments, and an Entity's
// graph overflows the test host.
public sealed partial class ContactRevealTests
{
    private const float Identify = .8f;

    // A Bay at `position`: its reactor on the reactor mount, a gun on the weapon mount when asked (with the targeting system
    // and reactor tool it needs to fire), and `tools` on free cells.
    private Ship NewShip(string hull)
    {
        var hullRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<HullData>(hull));
        return new Ship(_items, _zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = _nextLot++ }, new EntitySettings())
            { Faction = new Faction { Name = "F" + Guid.NewGuid().ToString("N") } };
    }

    private Ship Place(Ship ship, float3 position)
    {
        _zone.Admit(ship, piloted: false);
        ship.Position = position;
        ship.Aim = float3(0, 0, 1);
        _zone.Update(0f);
        return ship;
    }

    private Ship AddBay(float3 position, bool gun = false, string[] tools = null)
    {
        var ship = NewShip("Bay");
        Assert.True(ship.TryEquip(Make("BayReactor"), new int2(3, 3)));
        if (gun) Assert.True(ship.TryEquip(Make("Gun"), new int2(0, 3)));
        foreach (var name in (tools ?? new string[0]).Concat(gun ? new[] { "Targeting", "Reactor" } : new string[0]))
        {
            var item = Make(name);
            Assert.True(ship.TryFindSpace(item, out var at));
            Assert.True(ship.TryEquip(item, at));
        }
        return Place(ship, position);
    }

    // A Wing at `position` with a gun on each of its two Sensors mounts: gunA on the mount at (0,0), gunB at (2,3).
    private (Ship wing, EquippedItem gunA, EquippedItem gunB) AddWing(float3 position)
    {
        var ship = NewShip("Wing");
        Assert.True(ship.TryEquip(Make("Gun"), new int2(0, 0)));
        Assert.True(ship.TryEquip(Make("Gun"), new int2(2, 3)));
        Place(ship, position);
        var gunA = ship.Equipment.Single(i => i.Data.Name == "Gun" && i.Position.x == 0);
        var gunB = ship.Equipment.Single(i => i.Data.Name == "Gun" && i.Position.x == 2);
        Assert.True(gunA.Hardpoint != null && gunB.Hardpoint != null && !ReferenceEquals(gunA.Hardpoint, gunB.Hardpoint), "fixture: each gun sits on its own mount");
        return (ship, gunA, gunB);
    }

    private static EquippedItem Fitted(Entity e, string design) => e.Equipment.Single(i => i.Data.Name == design);

    // An observer at the origin that identifies a Bay 100 ahead (nothing revealed, nothing targeted).
    private (Ship bay, Ship observer, EquippedItem reactor) Identified(float info = 1f)
    {
        var bay = AddBay(float3(0, 0, 100));
        var observer = Add(float3(0, 0, 0));
        observer.EntityInfoGathered[bay] = info;
        return (bay, observer, Fitted(bay, "BayReactor"));
    }

    private static void Target(Entity observer, Entity subject)
    {
        observer.SetTarget(subject);
        Assert.True(ReferenceEquals(subject, observer.Target.Value.Entity), "fixture: the observer targets the ship");
    }

    // ---- AnIdentifiedHullShowsItsMounts ----

    // At the identify threshold the observer's layout of the ship is its hull's hardpoints; just below it is empty.
    [Fact]
    public void AnIdentifiedHullShowsItsMounts()
    {
        var (bay, observer, _) = Identified(Identify);
        Assert.Equal(3, bay.HullData.Hardpoints.Count);
        Assert.True(bay.HullData.Hardpoints.SequenceEqual(observer.LayoutOf(bay)), "the layout is the hull's three hardpoints, in order");

        observer.EntityInfoGathered[bay] = Identify - .01f;
        Assert.Empty(observer.LayoutOf(bay));
    }

    // ---- TheReactorMountIsAimedNotRevealed ----

    // Identified, the reactor never acted: the observer has located its mount (can aim at it) and learned nothing of the
    // item. Below the identify threshold it can aim at no mount, and a mount aim already taken is dropped when the
    // identification is lost. An unrevealed item is never an item aim.
    [Fact]
    public void TheReactorMountIsAimedNotRevealed()
    {
        var (bay, observer, reactor) = Identified(Identify - .01f);
        Target(observer, bay);
        Assert.False(observer.TrySelectMount(reactor.Hardpoint));

        observer.EntityInfoGathered[bay] = Identify;
        Assert.False(FireControl.IsRevealed(observer, reactor));
        Assert.False(observer.TrySelectTargetItem(reactor), "an unrevealed item is not an item aim");
        Assert.True(observer.TrySelectMount(reactor.Hardpoint));
        Assert.True(ReferenceEquals(reactor.Hardpoint, observer.ResolvedAim.Mount), "the located reactor mount is the aim");
        Assert.True(observer.ResolvedAim.Item == null && observer.ResolvedTargetItem == null, "a mount aim names no item");

        observer.EntityInfoGathered[bay] = Identify - .01f;
        Assert.True(observer.ResolvedAim.Mount == null, "the aim is dropped with the identification");
    }

    // ---- AnUnperceivedDestructionLeavesTheMountAimed ----

    // The observer perceives a destruction only through its own Sense: the reactor destroyed unseen leaves its mount aimed
    // (a live read of the item's Durability would tell the observer what it has not sensed); once Sensed it is gone.
    [Fact]
    public void AnUnperceivedDestructionLeavesTheMountAimed()
    {
        var (bay, observer, reactor) = Identified();
        Target(observer, bay);
        Assert.True(observer.TrySelectMount(reactor.Hardpoint), "fixture: the reactor mount is aimed at");

        reactor.EquippableItem.Durability = 0f;
        Assert.True(reactor.EquippableItem.IsDestroyed, "fixture");
        Assert.True(ReferenceEquals(reactor.Hardpoint, observer.ResolvedAim.Mount), "nothing sensed yet: the aim stands");
        Assert.True(observer.TrySelectMount(reactor.Hardpoint));

        SenseOf(observer)();
        Assert.True(observer.ResolvedAim.Mount == null, "perceived destroyed: the aim goes");
        Assert.False(observer.TrySelectMount(reactor.Hardpoint));
    }

    // ---- UnderAHullLieNoGearIsAimed ----

    // Ruling ew-aim-under-hull-lie-ruled: while the lie is believed the observer's layout is the claimed hull's and the true
    // reactor sits on none of its mounts, so the true item is never an aim: the claimed mount is, and the true reactor's own
    // mount is not; the ship stays targetable. The true mount is aimable again when the lie is seen through.
    [Fact]
    public void UnderAHullLieNoGearIsAimed()
    {
        var (bay, observer, reactor) = Identified();
        var decoy = new HullData { Name = "Decoy", Hardpoints = { new HardpointData { Type = HardpointType.Reactor, Position = new int2(0, 0), Shape = new Shape() } } };
        bay.Claim(decoy);

        Assert.True(decoy.Hardpoints.SequenceEqual(observer.LayoutOf(bay)) && observer.LayoutOf(bay).Count == 1, "the layout is the claimed hull's");
        Target(observer, bay);
        Assert.False(observer.TrySelectTargetItem(reactor), "the true reactor is not an item aim");
        Assert.False(observer.TrySelectMount(reactor.Hardpoint), "nor is its true mount, which the observer does not believe in");
        Assert.True(observer.TrySelectMount(decoy.Hardpoints[0]), "the claimed mount is");
        Assert.True(ReferenceEquals(decoy.Hardpoints[0], observer.ResolvedAim.Mount));
        Assert.True(observer.ResolvedTargetItem == null);
        Assert.True(ReferenceEquals(bay, observer.Target.Value.Entity), "the ship stays targetable");

        bay.Claim(null);
        Assert.True(observer.ResolvedAim.Mount == null, "the claimed mount is not the believed hull's once the lie is seen through");
        Assert.True(observer.TrySelectMount(reactor.Hardpoint));
    }

    // The lie is a lookalike: the claimed hull is of the true hull's type and size, with a reactor mount of the same type,
    // position and shape as the true one, and a shield mount elsewhere. What the observer knows is the claimed layout, mount
    // object by mount object; nothing of the true hull's layout leaks in because the two are alike.
    [Fact]
    public void UnderALookalikeLieNoGearIsAimed()
    {
        var (bay, observer, reactor) = Identified();
        var truth = bay.HullData;
        var lookalike = new HullData
        {
            Name = "Lookalike", HullType = truth.HullType, Shape = new Shape(truth.Shape.Width, truth.Shape.Height),
            Hardpoints =
            {
                new HardpointData { Type = HardpointType.Reactor, Position = truth.Hardpoints[0].Position, Shape = truth.Hardpoints[0].Shape },
                new HardpointData { Type = HardpointType.Shield, Position = new int2(3, 1), Shape = new Shape() }
            }
        };
        bay.Claim(lookalike);
        Assert.True(truth.Shape.Width == lookalike.Shape.Width && truth.Shape.Height == lookalike.Shape.Height && truth.HullType == lookalike.HullType, "fixture: the two hulls are one size and type");
        Assert.True(lookalike.Hardpoints[0].Type == reactor.Hardpoint.Type && lookalike.Hardpoints[0].Position.Equals(reactor.Hardpoint.Position), "fixture: the claimed reactor mount looks like the true one");
        Assert.False(ReferenceEquals(lookalike.Hardpoints[0], reactor.Hardpoint), "fixture: and is not it");

        Assert.True(lookalike.Hardpoints.SequenceEqual(observer.LayoutOf(bay)), "the layout is the claimed hull's, not the true hull's");
        Target(observer, bay);
        Assert.False(observer.TrySelectTargetItem(reactor));
        Assert.False(observer.TrySelectMount(reactor.Hardpoint), "the true reactor mount is not the claimed one, though they look alike");
        Assert.True(observer.TrySelectMount(lookalike.Hardpoints[0]));
        Assert.True(ReferenceEquals(bay, observer.Target.Value.Entity), "the ship stays targetable");
        Assert.Equal(2, observer.SchematicRows(bay).Count(r => r.Mount != null));
    }

    // ---- ToolGearIsNotAimedByLayout ----

    // Tool gear sits on no mount, so the layout offers nothing of it to aim at; an act reveals it, and then it is an item aim.
    [Fact]
    public void ToolGearIsNotAimedByLayout()
    {
        var bay = AddBay(float3(0, 0, 100), tools: new[] { "Radiator" });
        var observer = Add(float3(0, 0, 0));
        observer.EntityInfoGathered[bay] = 1f;
        var radiator = Fitted(bay, "Radiator");
        Assert.True(radiator.Hardpoint == null, "fixture: a tool item sits on no mount");
        Target(observer, bay);

        Assert.False(observer.TrySelectTargetItem(radiator));
        Assert.DoesNotContain(observer.AimCycle(bay), a => a.Item != null);

        observer.RecordReveal(bay, radiator, SignatureChannel.LaunchFlash, radiator.Data);
        Assert.True(observer.TrySelectTargetItem(radiator));
        Assert.Contains(observer.AimCycle(bay), a => ReferenceEquals(a.Item, radiator));
    }

    // ---- AMountAimIsTheMountsCells ----

    // A shooter that holds a valid solution on a Bay 30 ahead, with the reactor's mount selected as the aim.
    private (Ship shooter, Ship bay, EquippedItem reactor, Weapon weapon, EquippedItem gun) Gunner()
    {
        var bay = AddBay(float3(0, 0, 30));
        return GunnerOn(bay, Fitted(bay, "BayReactor"));
    }

    // A shooter that holds a valid solution on `bay`, with the mount of `aimed` (fitted to it) selected as the aim.
    private (Ship shooter, Ship bay, EquippedItem reactor, Weapon weapon, EquippedItem gun) GunnerOn(Ship bay, EquippedItem aimed)
    {
        var shooter = Add(float3(0, 0, 0), new[] { "Eye", "Gun" });
        shooter.SetTarget(bay);
        for (var i = 0; i < 3; i++) Hold(shooter, 1, 1f, bay);
        Assert.True(shooter.VisibleEntities.Contains(bay), "fixture: the shooter tracks the ship");
        var gun = Guns(shooter).Single();
        Assert.True(shooter.TrySelectMount(aimed.Hardpoint), "fixture: the mount is aimed at");
        return (shooter, bay, aimed, (Weapon) gun.Behaviors.Single(b => b is Weapon), gun);
    }

    private PendingShot FireAt(Weapon weapon, EquippedItem gun, Entity shooter)
    {
        var before = _zone.PendingShots.Count;
        FireControl.Fire(weapon, gun, shooter);
        Assert.Equal(before + 1, _zone.PendingShots.Count);
        return _zone.PendingShots[before];
    }

    // The reactor is one cell inside its two by two mount. Aimed at while located, a shot centres on the mount's cells (3.5, 3.5);
    // once the reactor is revealed the next shot centres on the reactor's own cell (3, 3). The HUD's forecast reads the same
    // aim point as the shot it forecasts.
    [Fact]
    public void AMountAimIsTheMountsCells()
    {
        var (shooter, bay, reactor, weapon, gun) = Gunner();

        var located = FireControl.Inspect(weapon, shooter, bay);
        var shot = FireAt(weapon, gun, shooter);
        Assert.True(ReferenceEquals(reactor.Hardpoint, shot.AimedMount) && shot.Aimed == null, "the shot is aimed at the located reactor's mount, not the item");
        Assert.True(FireControl.CommitProbability(shot, _zone.Time, out var mountSil) > 0f, "fixture: the shot has a price");
        Assert.Equal(3.5f, mountSil.AimPoint.x, 4);
        Assert.Equal(3.5f, mountSil.AimPoint.y, 4);
        Assert.Equal(mountSil.POnHull, located.POnHull, 4);

        shooter.RecordReveal(bay, reactor, SignatureChannel.LaunchFlash, reactor.Data);
        Assert.True(FireControl.IsRevealed(shooter, reactor), "fixture: the reactor is revealed");
        Assert.True(shooter.TrySelectTargetItem(reactor), "fixture: the revealed reactor is aimed at as an item");
        var revealed = FireControl.Inspect(weapon, shooter, bay);
        var next = FireAt(weapon, gun, shooter);
        Assert.True(next.AimedMount == null && ReferenceEquals(reactor, next.Aimed), "the next shot is aimed at the item");
        Assert.True(FireControl.CommitProbability(next, _zone.Time, out var itemSil) > 0f, "fixture: the shot has a price");
        Assert.Equal(3f, itemSil.AimPoint.x, 4);
        Assert.Equal(3f, itemSil.AimPoint.y, 4);
        Assert.Equal(itemSil.POnHull, revealed.POnHull, 4);
        Assert.True(Math.Abs(mountSil.POnHull - itemSil.POnHull) > 1e-3f, "fixture: the two aim points are priced apart");
    }

    // The mount is a rectangle off the diagonal: the second Sensors mount covers (2,3) and (3,3), centre (2.5, 3). A position
    // read with its axes swapped would centre the shot on (3.5, 2).
    [Fact]
    public void AMountAimReadsThePositionAxesInOrder()
    {
        var (wing, _, gunB) = AddWing(float3(0, 0, 30));
        var (shooter, _, _, weapon, gun) = GunnerOn(wing, gunB);
        var shot = FireAt(weapon, gun, shooter);
        Assert.True(ReferenceEquals(gunB.Hardpoint, shot.AimedMount), "fixture: the shot is aimed at the located gun's mount");
        Assert.True(FireControl.CommitProbability(shot, _zone.Time, out var sil) > 0f, "fixture: the shot has a price");
        Assert.Equal(2.5f, sil.AimPoint.x, 4);
        Assert.Equal(3f, sil.AimPoint.y, 4);
    }

    // ---- AMountAimThatLostItsGateDropsOutOfForecastAndFire ----

    // The aim slot holds the mount, but what prices and freezes a shot is the aim as it resolves now: a mount the observer has
    // since seen destroyed, or whose hull it no longer identifies, is no aim. The forecast prices the unaimed hull and the
    // shot is fired aimed at nothing, though nothing wrote the slot at the moment the gate closed.
    [Theory]
    [InlineData("destroyed")]
    [InlineData("unidentified")]
    public void AMountAimThatLostItsGateDropsOutOfForecastAndFire(string how)
    {
        var (shooter, bay, reactor, weapon, gun) = Gunner();
        var aimedPrice = FireControl.Inspect(weapon, shooter, bay).POnHull;
        shooter.TrySelectTargetItem(null);
        var unaimedPrice = FireControl.Inspect(weapon, shooter, bay).POnHull;
        Assert.True(Math.Abs(aimedPrice - unaimedPrice) > 1e-3f, "fixture: the mount aim and no aim are priced apart");
        Assert.True(shooter.TrySelectMount(reactor.Hardpoint), "fixture: the mount is aimed at again");

        if (how == "destroyed")
        {
            reactor.EquippableItem.Durability = 0f;
            SenseOf(shooter)();
        }
        else
        {
            shooter.EntityInfoGathered[bay] = Identify - .01f;
        }
        Assert.True(shooter.ResolvedAim.Mount == null, "fixture: the aim no longer resolves");

        if (how == "destroyed")
            Assert.Equal(unaimedPrice, FireControl.Inspect(weapon, shooter, bay).POnHull, 4);
        var shot = FireAt(weapon, gun, shooter);
        Assert.True(shot.AimedMount == null && shot.Aimed == null, "the shot is aimed at nothing");
    }

    // ---- TheShotKeepsTheAimItWasFiredWith ----

    [Fact]
    public void TheShotKeepsTheAimItWasFiredWith()
    {
        var (shooter, bay, reactor, weapon, gun) = Gunner();
        var shot = FireAt(weapon, gun, shooter);
        Assert.True(ReferenceEquals(reactor.Hardpoint, shot.AimedMount), "fixture: fired at the mount");

        shooter.RecordReveal(bay, reactor, SignatureChannel.LaunchFlash, reactor.Data);
        Assert.True(FireControl.IsRevealed(shooter, reactor), "fixture: revealed before Commit");
        Assert.True(shooter.TrySelectTargetItem(reactor), "fixture: and aimed at as an item, which does not move a shot already fired");

        Assert.True(FireControl.CommitProbability(shot, _zone.Time, out var sil) > 0f, "fixture: the shot has a price");
        Assert.Equal(3.5f, sil.AimPoint.x, 4);
        Assert.Equal(3.5f, sil.AimPoint.y, 4);
    }

    // ---- TheSchematicListsUnfilledMounts ----

    // Identified, nothing revealed: a mount row for each hardpoint, naming no item and no class and giving no range. After a
    // scan the reactor's gear row leads and every mount keeps its row, the reactor's included (rows never compare the true
    // items to the believed layout). The aimed mount is marked on its row alone.
    [Fact]
    public void TheSchematicListsUnfilledMounts()
    {
        var (bay, observer, reactor) = Identified(Identify - .01f);
        var hardpoints = bay.HullData.Hardpoints;
        Assert.Empty(observer.SchematicRows(bay));

        observer.EntityInfoGathered[bay] = Identify;
        var rows = observer.SchematicRows(bay).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal(3, rows.Select(r => r.Key).Distinct().Count());
        Assert.True(hardpoints.SequenceEqual(rows.Select(r => r.Mount)), "one mount row per hardpoint, in order");
        Assert.All(rows, r =>
        {
            Assert.True(r.Item == null && r.Claimed == null, "a mount row names no item and no class");
            Assert.False(r.Range.HasValue);
        });

        Target(observer, bay);
        Assert.True(observer.TrySelectMount(reactor.Hardpoint));
        Assert.Equal(new[] { true, false, false }, rows.Select(r => r.Marks(observer.ResolvedAim)).ToArray());

        observer.RecordInspection(bay);
        rows = observer.SchematicRows(bay).ToArray();
        Assert.Equal(4, rows.Length);
        Assert.Equal(4, rows.Select(r => r.Key).Distinct().Count());
        Assert.True(rows[0].Mount == null && ReferenceEquals(rows[0].Item, reactor), "the reactor's gear row leads");
        Assert.True(hardpoints.SequenceEqual(rows.Skip(1).Select(r => r.Mount)), "every mount keeps its mount row, the reactor's too");
        Assert.Equal(new[] { false, true, false, false }, rows.Select(r => r.Marks(observer.ResolvedAim)).ToArray());

        Assert.True(observer.TrySelectTargetItem(reactor));
        Assert.Equal(new[] { true, false, false, false }, rows.Select(r => r.Marks(observer.ResolvedAim)).ToArray());
    }

    // Two mounts of one type: the rows tell them apart by mount, not by type. Aiming at one marks its row alone, and revealing
    // the other gives way only that mount's row to its gear row, leaving this one a mount row.
    [Fact]
    public void TwoMountsOfOneTypeAreTwoRows()
    {
        var (wing, gunA, gunB) = AddWing(float3(0, 0, 100));
        var observer = Add(float3(0, 0, 0));
        observer.EntityInfoGathered[wing] = 1f;
        var mountA = gunA.Hardpoint;
        var mountB = gunB.Hardpoint;
        Target(observer, wing);
        Assert.True(observer.TrySelectMount(mountB));

        var rows = observer.SchematicRows(wing).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.True(ReferenceEquals(mountA, rows[0].Mount) && ReferenceEquals(mountB, rows[1].Mount), "one mount row per mount, in order");
        Assert.Equal(new[] { false, true }, rows.Select(r => r.Marks(observer.ResolvedAim)).ToArray());

        observer.RecordReveal(wing, gunA, SignatureChannel.LaunchFlash, gunA.Data);
        rows = observer.SchematicRows(wing).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.True(ReferenceEquals(rows[0].Item, gunA) && rows[0].Mount == null, "the revealed gun's row leads");
        Assert.True(ReferenceEquals(rows[1].Mount, mountA) && ReferenceEquals(rows[2].Mount, mountB), "both mounts keep their mount rows");
        Assert.Equal(new[] { false, false, true }, rows.Select(r => r.Marks(observer.ResolvedAim)).ToArray());
    }

    // ---- TheAIStillChoosesOnlyRevealedWeapons ----

    // An AI ship that identifies a Bay with a gun on its weapon mount aims at no item: Combat chooses among the weapons it has
    // seen, and the located gun is not one. Once the gun fires in sight, Combat selects it.
    [Fact]
    public void TheAIStillChoosesOnlyRevealedWeapons()
    {
        var bay = AddBay(float3(0, 0, 40), gun: true);
        var gun = Guns(bay).Single();
        var ai = Add(float3(0, 0, 0), new[] { "Eye" });
        _zone.Agents.Add(new Minion(ai));
        ai.SetTarget(bay);
        Hold(ai, 12, 1f, bay);
        Assert.True(ReferenceEquals(bay, ai.Target.Value.Entity), "fixture: the AI keeps the ship as its target");
        Assert.False(FireControl.IsRevealed(ai, gun), "fixture: the gun is not revealed");
        Assert.True(ai.ResolvedTargetItem == null, "Combat aims at nothing it has not seen act");

        Fire(bay, gun);
        for (var i = 0; i < 40 && ai.ResolvedTargetItem == null; i++) Hold(ai, 1, 1f, bay);
        Assert.True(ReferenceEquals(gun, ai.ResolvedTargetItem), "after the gun fires Combat aims at it");
    }
}
