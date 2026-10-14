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

    // ---- TheReactorIsLocatedNotRevealed ----

    // Identified, the reactor never acted: the observer has located it (can aim at it) and learned nothing of it. Below the
    // identify threshold it can aim at nothing, and a mount aim already taken is dropped when the identification is lost.
    [Fact]
    public void TheReactorIsLocatedNotRevealed()
    {
        var (bay, observer, reactor) = Identified(Identify - .01f);
        Target(observer, bay);
        Assert.False(FireControl.IsLocated(observer, reactor));
        Assert.False(observer.TrySelectTargetItem(reactor));

        observer.EntityInfoGathered[bay] = Identify;
        Assert.True(FireControl.IsLocated(observer, reactor));
        Assert.False(FireControl.IsRevealed(observer, reactor));
        Assert.True(observer.TrySelectTargetItem(reactor));
        Assert.True(ReferenceEquals(reactor, observer.ResolvedTargetItem), "the located reactor is the aim point");

        observer.EntityInfoGathered[bay] = Identify - .01f;
        Assert.True(observer.ResolvedTargetItem == null, "the aim is dropped with the identification");
    }

    // ---- ADestroyedMountItemIsNotLocated ----

    [Fact]
    public void ADestroyedMountItemIsNotLocated()
    {
        var (bay, observer, reactor) = Identified();
        Target(observer, bay);
        Assert.True(observer.TrySelectTargetItem(reactor), "fixture: the live reactor is located and aimed at");

        reactor.EquippableItem.Durability = 0f;
        Assert.True(reactor.EquippableItem.IsDestroyed, "fixture");
        Assert.False(FireControl.IsLocated(observer, reactor));
        Assert.True(observer.ResolvedTargetItem == null);
        Assert.False(observer.TrySelectTargetItem(reactor));
    }

    // ---- UnderAHullLieNoGearIsLocated ----

    // Ruling ew-aim-under-hull-lie-ruled: while the lie is believed the observer's layout is the claimed hull's and the true
    // reactor sits on none of its mounts, so no gear can be aimed at and the ship stays targetable; gear aim opens when the
    // lie is seen through.
    [Fact]
    public void UnderAHullLieNoGearIsLocated()
    {
        var (bay, observer, reactor) = Identified();
        var decoy = new HullData { Name = "Decoy", Hardpoints = { new HardpointData { Type = HardpointType.Reactor, Position = new int2(0, 0), Shape = new Shape() } } };
        bay.Claim(decoy);

        Assert.True(decoy.Hardpoints.SequenceEqual(observer.LayoutOf(bay)) && observer.LayoutOf(bay).Count == 1, "the layout is the claimed hull's");
        Target(observer, bay);
        Assert.False(FireControl.IsLocated(observer, reactor));
        Assert.False(observer.TrySelectTargetItem(reactor));
        Assert.True(observer.ResolvedTargetItem == null);
        Assert.True(ReferenceEquals(bay, observer.Target.Value.Entity), "the ship stays targetable");

        bay.Claim(null);
        Assert.True(FireControl.IsLocated(observer, reactor));
        Assert.True(observer.TrySelectTargetItem(reactor));
    }

    // The lie is a lookalike: the claimed hull is of the true hull's type and size, with a reactor mount of the same type,
    // position and shape as the true one, and a shield mount elsewhere. What the observer knows is the claimed layout, mount
    // object by mount object; nothing of the true hull's layout leaks in because the two are alike.
    [Fact]
    public void UnderALookalikeLieNoGearIsLocated()
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
        Assert.False(FireControl.IsLocated(observer, reactor));
        Assert.False(observer.TrySelectTargetItem(reactor));
        Assert.True(ReferenceEquals(bay, observer.Target.Value.Entity), "the ship stays targetable");
        Assert.Equal(2, observer.SchematicRows(bay).Count(r => r.Mount != null));
    }

    // ---- ToolGearIsNotLocatedByLayout ----

    // Tool gear sits on no mount, so the layout locates nothing of it; an act does.
    [Fact]
    public void ToolGearIsNotLocatedByLayout()
    {
        var bay = AddBay(float3(0, 0, 100), tools: new[] { "Radiator" });
        var observer = Add(float3(0, 0, 0));
        observer.EntityInfoGathered[bay] = 1f;
        var radiator = Fitted(bay, "Radiator");
        Assert.True(radiator.Hardpoint == null, "fixture: a tool item sits on no mount");
        Target(observer, bay);

        Assert.False(FireControl.IsLocated(observer, radiator));
        Assert.False(observer.TrySelectTargetItem(radiator));

        observer.RecordReveal(bay, radiator, SignatureChannel.LaunchFlash, radiator.Data);
        Assert.True(FireControl.IsLocated(observer, radiator));
    }

    // ---- AMountAimIsTheMountsCells ----

    // A shooter that holds a valid solution on a Bay 30 ahead, with the reactor selected as the aim point.
    private (Ship shooter, Ship bay, EquippedItem reactor, Weapon weapon, EquippedItem gun) Gunner()
    {
        var bay = AddBay(float3(0, 0, 30));
        return GunnerOn(bay, Fitted(bay, "BayReactor"));
    }

    // A shooter that holds a valid solution on `bay`, with `aimed` (fitted to it) selected as the aim point.
    private (Ship shooter, Ship bay, EquippedItem reactor, Weapon weapon, EquippedItem gun) GunnerOn(Ship bay, EquippedItem aimed)
    {
        var shooter = Add(float3(0, 0, 0), new[] { "Eye", "Gun" });
        shooter.SetTarget(bay);
        for (var i = 0; i < 3; i++) Hold(shooter, 1, 1f, bay);
        Assert.True(shooter.VisibleEntities.Contains(bay), "fixture: the shooter tracks the ship");
        var gun = Guns(shooter).Single();
        Assert.True(shooter.TrySelectTargetItem(aimed), "fixture: the located item is aimed at");
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
        Assert.True(shot.AimAtMount && ReferenceEquals(reactor, shot.Aimed), "the shot is aimed at the located reactor's mount");
        Assert.True(FireControl.CommitProbability(shot, _zone.Time, out var mountSil) > 0f, "fixture: the shot has a price");
        Assert.Equal(3.5f, mountSil.AimPoint.x, 4);
        Assert.Equal(3.5f, mountSil.AimPoint.y, 4);
        Assert.Equal(mountSil.POnHull, located.POnHull, 4);

        shooter.RecordReveal(bay, reactor, SignatureChannel.LaunchFlash, reactor.Data);
        Assert.True(FireControl.IsRevealed(shooter, reactor), "fixture: the reactor is revealed");
        var revealed = FireControl.Inspect(weapon, shooter, bay);
        var next = FireAt(weapon, gun, shooter);
        Assert.False(next.AimAtMount);
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
        Assert.True(shot.AimAtMount && ReferenceEquals(gunB, shot.Aimed), "fixture: the shot is aimed at the located gun's mount");
        Assert.True(FireControl.CommitProbability(shot, _zone.Time, out var sil) > 0f, "fixture: the shot has a price");
        Assert.Equal(2.5f, sil.AimPoint.x, 4);
        Assert.Equal(3f, sil.AimPoint.y, 4);
    }

    // ---- TheShotKeepsTheAimItWasFiredWith ----

    [Fact]
    public void TheShotKeepsTheAimItWasFiredWith()
    {
        var (shooter, bay, reactor, weapon, gun) = Gunner();
        var shot = FireAt(weapon, gun, shooter);
        Assert.True(shot.AimAtMount, "fixture: fired at the mount");

        shooter.RecordReveal(bay, reactor, SignatureChannel.LaunchFlash, reactor.Data);
        Assert.True(FireControl.IsRevealed(shooter, reactor), "fixture: revealed before Commit");

        Assert.True(FireControl.CommitProbability(shot, _zone.Time, out var sil) > 0f, "fixture: the shot has a price");
        Assert.Equal(3.5f, sil.AimPoint.x, 4);
        Assert.Equal(3.5f, sil.AimPoint.y, 4);
    }

    // ---- TheSchematicListsUnfilledMounts ----

    // Identified, nothing revealed: a mount row for each hardpoint, naming no item and no class and giving no range. After a
    // scan the reactor's mount row gives way to its gear row. The aimed reactor is marked on whichever row stands for it, and
    // no other row is.
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
        Assert.True(observer.TrySelectTargetItem(reactor));
        Assert.Equal(new[] { true, false, false }, rows.Select(r => r.Marks(observer.ResolvedTargetItem)).ToArray());

        observer.RecordInspection(bay);
        rows = observer.SchematicRows(bay).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal(3, rows.Select(r => r.Key).Distinct().Count());
        Assert.True(rows[0].Mount == null && ReferenceEquals(rows[0].Item, reactor), "the reactor's gear row leads");
        Assert.True(hardpoints.Skip(1).SequenceEqual(rows.Skip(1).Select(r => r.Mount)), "the other two mounts stay mount rows");
        Assert.Equal(new[] { true, false, false }, rows.Select(r => r.Marks(observer.ResolvedTargetItem)).ToArray());
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
        Assert.True(observer.TrySelectTargetItem(gunB));

        var rows = observer.SchematicRows(wing).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.True(ReferenceEquals(mountA, rows[0].Mount) && ReferenceEquals(mountB, rows[1].Mount), "one mount row per mount, in order");
        Assert.Equal(new[] { false, true }, rows.Select(r => r.Marks(observer.ResolvedTargetItem)).ToArray());

        observer.RecordReveal(wing, gunA, SignatureChannel.LaunchFlash, gunA.Data);
        rows = observer.SchematicRows(wing).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.True(ReferenceEquals(rows[0].Item, gunA) && rows[0].Mount == null, "the revealed gun's row leads");
        Assert.True(ReferenceEquals(rows[1].Mount, mountB) && rows[1].Item == null, "the other mount of the same type stays a mount row");
        Assert.Equal(new[] { false, true }, rows.Select(r => r.Marks(observer.ResolvedTargetItem)).ToArray());
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
        Assert.True(FireControl.IsLocated(ai, gun) && !FireControl.IsRevealed(ai, gun), "fixture: the gun is located and not revealed");
        Assert.True(ai.ResolvedTargetItem == null, "Combat aims at nothing it has not seen act");

        Fire(bay, gun);
        for (var i = 0; i < 40 && ai.ResolvedTargetItem == null; i++) Hold(ai, 1, 1f, bay);
        Assert.True(ReferenceEquals(gun, ai.ResolvedTargetItem), "after the gun fires Combat aims at it");
    }
}
