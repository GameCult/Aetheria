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
// mount it has located without knowing what is fitted there. The fixture hull "Bay" has a reactor mount (the 2x2 block at the
// origin, the reactor one cell inside it at (1,1)), a shield mount and a weapon mount, and nothing ever acts unless a test
// makes it. Identified is TargetGearInfoThreshold (.8) of the observer's info on the ship. Every fixture drives the
// production path (Fire, CommitProbability, SchematicRows, TrySelectTargetItem, the AI's Combat state inside Zone.Update).
// Entities and items are compared with ReferenceEquals: a failing xunit assertion formats its arguments, and an Entity's
// graph overflows the test host.
public sealed partial class ContactRevealTests
{
    private const float Identify = .8f;

    // A Bay at `position`: its reactor on the reactor mount, a gun on the weapon mount when asked (with the targeting system
    // and reactor tool it needs to fire), and `tools` on free cells.
    private Ship AddBay(float3 position, bool gun = false, string[] tools = null)
    {
        var hullRef = _items.ItemData.RefOf<ItemData>(_items.ItemData.GetByName<HullData>("Bay"));
        var ship = new Ship(_items, _zone, new EquippableItem { Data = hullRef, Durability = 1000000, Lot = _nextLot++ }, new EntitySettings())
            { Faction = new Faction { Name = "F" + Guid.NewGuid().ToString("N") } };
        Assert.True(ship.TryEquip(Make("BayReactor"), new int2(1, 1)));
        if (gun) Assert.True(ship.TryEquip(Make("Gun"), new int2(0, 3)));
        foreach (var name in (tools ?? new string[0]).Concat(gun ? new[] { "Targeting", "Reactor" } : new string[0]))
        {
            var item = Make(name);
            Assert.True(ship.TryFindSpace(item, out var at));
            Assert.True(ship.TryEquip(item, at));
        }
        _zone.Admit(ship, piloted: false);
        ship.Position = position;
        ship.Aim = float3(0, 0, 1);
        _zone.Update(0f);
        return ship;
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
        var shooter = Add(float3(0, 0, 0), new[] { "Eye", "Gun" });
        shooter.SetTarget(bay);
        for (var i = 0; i < 3; i++) Hold(shooter, 1, 1f, bay);
        Assert.True(shooter.VisibleEntities.Contains(bay), "fixture: the shooter tracks the ship");
        var gun = Guns(shooter).Single();
        var reactor = Fitted(bay, "BayReactor");
        Assert.True(shooter.TrySelectTargetItem(reactor), "fixture: the located reactor is aimed at");
        return (shooter, bay, reactor, (Weapon) gun.Behaviors.Single(b => b is Weapon), gun);
    }

    private PendingShot FireAt(Weapon weapon, EquippedItem gun, Entity shooter)
    {
        var before = _zone.PendingShots.Count;
        FireControl.Fire(weapon, gun, shooter);
        Assert.Equal(before + 1, _zone.PendingShots.Count);
        return _zone.PendingShots[before];
    }

    // The reactor is one cell inside its two by two mount. Aimed at while located, a shot centres on the mount's cells (.5, .5);
    // once the reactor is revealed the next shot centres on the reactor's own cell (1, 1). The HUD's forecast reads the same
    // aim point as the shot it forecasts.
    [Fact]
    public void AMountAimIsTheMountsCells()
    {
        var (shooter, bay, reactor, weapon, gun) = Gunner();

        var located = FireControl.Inspect(weapon, shooter, bay);
        var shot = FireAt(weapon, gun, shooter);
        Assert.True(shot.AimAtMount && ReferenceEquals(reactor, shot.Aimed), "the shot is aimed at the located reactor's mount");
        Assert.True(FireControl.CommitProbability(shot, _zone.Time, out var mountSil) > 0f, "fixture: the shot has a price");
        Assert.Equal(.5f, mountSil.AimPoint.x, 4);
        Assert.Equal(.5f, mountSil.AimPoint.y, 4);
        Assert.Equal(mountSil.POnHull, located.POnHull, 4);

        shooter.RecordReveal(bay, reactor, SignatureChannel.LaunchFlash, reactor.Data);
        Assert.True(FireControl.IsRevealed(shooter, reactor), "fixture: the reactor is revealed");
        var revealed = FireControl.Inspect(weapon, shooter, bay);
        var next = FireAt(weapon, gun, shooter);
        Assert.False(next.AimAtMount);
        Assert.True(FireControl.CommitProbability(next, _zone.Time, out var itemSil) > 0f, "fixture: the shot has a price");
        Assert.Equal(1f, itemSil.AimPoint.x, 4);
        Assert.Equal(1f, itemSil.AimPoint.y, 4);
        Assert.Equal(itemSil.POnHull, revealed.POnHull, 4);
        Assert.True(Math.Abs(mountSil.POnHull - itemSil.POnHull) > 1e-3f, "fixture: the two aim points are priced apart");
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
        Assert.Equal(.5f, sil.AimPoint.x, 4);
        Assert.Equal(.5f, sil.AimPoint.y, 4);
    }

    // ---- TheSchematicListsUnfilledMounts ----

    // Identified, nothing revealed: a mount row for each hardpoint, naming no item and no class and giving no range. After a
    // scan the reactor's mount row gives way to its gear row. The aimed reactor is marked on whichever row stands for it, and
    // no other row is.
    [Fact]
    public void TheSchematicListsUnfilledMounts()
    {
        var (bay, observer, reactor) = Identified();
        var hardpoints = bay.HullData.Hardpoints;

        var rows = observer.SchematicRows(bay).ToArray();
        Assert.Equal(3, rows.Length);
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
        Assert.True(rows[0].Mount == null && ReferenceEquals(rows[0].Item, reactor), "the reactor's gear row leads");
        Assert.True(hardpoints.Skip(1).SequenceEqual(rows.Skip(1).Select(r => r.Mount)), "the other two mounts stay mount rows");
        Assert.Equal(new[] { true, false, false }, rows.Select(r => r.Marks(observer.ResolvedTargetItem)).ToArray());
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
