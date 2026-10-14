using System.Linq;
using Xunit;
using static CultMath.math;

// aetheria-release, cut scenarios-smoke-rounds: what the Fused Rounds, Refused Rounds and AI Fused Discipline arenas stage,
// read from the launched run, the shipped catalog and the production fire solution (FireControl.Solve) the HUD and the
// weapons both ask.
public sealed partial class RunStartTests
{
    private static readonly string[] SmokeWeapons = { "Smoke Proximity", "Smoke Delayed", "Smoke Refused", "Smoke Refused Beam", "Smoke Refused Charge" };

    private static EquippedItem Carried(Entity ship, string design) => Assert.Single(ship.Equipment, item => item.Data.Name == design);

    private static Weapon WeaponOf(EquippedItem mount) => mount.Behaviors.OfType<Weapon>().Single();

    private static FireOutcome OutcomeOn(Entity shooter, EquippedItem mount, Entity target) => FireControl.Solve(WeaponOf(mount), mount, shooter, target).Outcome;

    // Fused Rounds: the player carries Smoke Proximity, Smoke Delayed and a direct weapon with nothing selected; a bare hull
    // lies within the arc on the initial aim line; the close hull is nearer than Smoke Proximity's blast radius; the stern
    // target is outside every mount's arc; the direct weapon can kill the fragile hull (damage x shots in its magazine above
    // its hull). The smoke weapons are unbranded and the fused weapons read Burst where the direct one reads Direct.
    [Fact]
    public void FusedRoundsConditions()
    {
        var (_, _, staged, failures) = Launch(new FusedRounds(), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var player = staged.Player;
        player.GenerateWeaponGroups();
        EvSettle(player); // a weapon's Range is read once the ship's stats have settled; the geometry is as staged
        Assert.Equal(5, staged.Entities.Count);
        var (mid, ahead, fragile, close, stern) = (staged.Entities[0], staged.Entities[1], staged.Entities[2], staged.Entities[3], staged.Entities[4]);
        Assert.Equal((FusedRounds.Mid, FusedRounds.Ahead, FusedRounds.Fragile, FusedRounds.Close, FusedRounds.Stern),
            (mid.Position.xz, ahead.Position.xz, fragile.Position.xz, close.Position.xz, stern.Position.xz));

        var proximity = Carried(player, "Smoke Proximity");
        var delayed = Carried(player, "Smoke Delayed");
        var direct = Carried(player, "Cluster Cannon");
        Assert.Null(player.Target.Value.Entity);

        // The bare hull is on the initial aim line, ahead, and inside every weapon mount's arc.
        var aim = player.Direction;
        Assert.True(abs(aim.x * ahead.Position.z - aim.y * ahead.Position.x) < 1e-3f && dot(aim, ahead.Position.xz) > 0, "the bare hull is not on the player's initial aim line");
        foreach (var mount in WeaponMounts(player))
            Assert.True(Bears(mount, player, ahead), $"{mount.Data.Name} cannot bear on the bare hull ahead");

        // The stern target is outside every mount's arc.
        foreach (var mount in WeaponMounts(player))
            Assert.False(Bears(mount, player, stern), $"{mount.Data.Name} bears on the stern hull");

        // The close hull is nearer than Smoke Proximity's blast radius, measured from the ship's centre, and inside its arc.
        Assert.NotNull(FireControl.FuseOf(proximity, out var blast));
        Assert.True(length((close.Position - player.Position).xz) < blast, "the close hull is not inside Smoke Proximity's blast radius");
        Assert.True(Bears(proximity, player, close), "Smoke Proximity cannot bear on the close hull");

        // The fragile and hostile hulls are reachable by the fused weapons, the fragile one by the direct weapon too, and the
        // direct weapon's magazine holds enough damage to kill the fragile hull.
        foreach (var target in new[] { mid, fragile })
        {
            Assert.True(Bears(proximity, player, target) && Bears(delayed, player, target), "a fused weapon cannot bear on a target hull");
            Assert.True(length((target.Position - player.Position).xz) <= WeaponOf(proximity).Range, "a target hull lies beyond Smoke Proximity's range");
        }
        var directWeapon = WeaponOf(direct);
        Assert.True(Bears(direct, player, fragile), "the direct weapon cannot bear on the fragile hull");
        Assert.True(length((fragile.Position - player.Position).xz) <= directWeapon.EffectiveRange, "the fragile hull lies beyond the direct weapon's effective range, where its damage falls off");
        var magazine = direct.Data.Behaviors.OfType<WeaponData>().Single().MagazineSize;
        var burst = ((InstantWeapon) directWeapon).BurstCount;
        Assert.True(magazine > 0 && directWeapon.Damage * burst * magazine > ((HullData) _items.GetData(fragile.Hull)).Durability,
            $"the direct weapon cannot kill the fragile hull from its magazine ({directWeapon.Damage} x {burst} x {magazine})");

        // Only the Longinus at mid range is hostile.
        Assert.True(player.IsHostileTo(mid) && mid.IsHostileTo(player), "the mid-range hull is hostile");
        Assert.False(new[] { ahead, fragile, close, stern }.Any(entity => player.IsHostileTo(entity)), "a bare hull is hostile");

        // What the HUD forecasts: fused weapons burst, the direct weapon resolves against its target.
        Assert.Equal((FireOutcome.Burst, FireOutcome.Burst, FireOutcome.Direct),
            (OutcomeOn(player, proximity, ahead), OutcomeOn(player, delayed, ahead), OutcomeOn(player, direct, ahead)));

        // The smoke weapons carry no manufacturer and no product, so the properties panel shows no Manufacturer row.
        foreach (var mount in new[] { proximity, delayed })
        {
            var (maker, product) = _items.Brand((CraftedItemInstance) mount.EquippableItem);
            Assert.Null(maker);
            Assert.Null(product);
        }
    }

    // Refused Rounds: Smoke Refused, Smoke Refused Beam and Smoke Refused Charge, each with a Range below its BlastRadius, and one
    // working weapon. AI Fused Discipline: a piloted neutral carrying a fused weapon and a piloted hostile whose only weapon is
    // refused.
    [Fact]
    public void RefusedAndAiConditions()
    {
        var (_, _, rounds, roundsFailures) = Launch(new RefusedRounds(), Inputs(() => GalaxySeed));
        Assert.True(roundsFailures.Count == 0, string.Join("; ", roundsFailures));
        var player = rounds.Player;
        player.GenerateWeaponGroups();
        EvSettle(player);
        var target = Assert.Single(rounds.Entities);
        Assert.Equal(4, WeaponMounts(player).Count);

        foreach (var design in SmokeWeapons.Skip(2))
        {
            var mount = Carried(player, design);
            var weapon = WeaponOf(mount);
            Assert.NotNull(FireControl.FuseOf(mount, out var blast));
            Assert.True(weapon.Range > 0f, $"{design}'s range has not settled");
            Assert.True(weapon.Range < blast, $"{design}'s range is not below its blast radius");
            Assert.True(Bears(mount, player, target), $"{design} cannot bear on the bare hull");
            Assert.Equal(FireOutcome.Refused, OutcomeOn(player, mount, target));
        }
        Assert.IsType<ConstantWeapon>(WeaponOf(Carried(player, "Smoke Refused Beam")));
        Assert.IsType<ChargedWeapon>(WeaponOf(Carried(player, "Smoke Refused Charge")));
        var working = Carried(player, "Autocannon");
        Assert.True(Bears(working, player, target) && length((target.Position - player.Position).xz) <= WeaponOf(working).Range, "the working weapon cannot reach the bare hull");
        Assert.Equal(FireOutcome.Direct, OutcomeOn(player, working, target));

        var (_, arena, ai, aiFailures) = Launch(new AiFusedDiscipline(), Inputs(() => GalaxySeed));
        Assert.True(aiFailures.Count == 0, string.Join("; ", aiFailures));
        Assert.Equal(2, ai.Entities.Count);
        var (hostile, neutral) = (ai.Entities[0], ai.Entities[1]);
        Assert.True(ai.Player.IsHostileTo(hostile) && hostile.IsHostileTo(ai.Player), "the refused-weapon ship is hostile");
        Assert.False(ai.Player.IsHostileTo(neutral) || neutral.IsHostileTo(ai.Player), "the fused-weapon ship is neutral");
        Assert.Equal((1, 1), (AgentsOf(arena, hostile), AgentsOf(arena, neutral)));
        Assert.Equal("Djinni", _items.GetData(ai.Player.Hull).Name);

        EvSettle((Ship) hostile);
        EvSettle((Ship) neutral);
        var refused = Assert.Single(WeaponMounts(hostile));
        Assert.Equal("Smoke Refused", refused.Data.Name);
        Assert.True(WeaponOf(refused).Range > 0f, "the hostile's refused weapon range has not settled");
        Assert.Equal(FireOutcome.Refused, OutcomeOn(hostile, refused, ai.Player));
        var fused = Assert.Single(WeaponMounts(neutral));
        Assert.Equal("Smoke Proximity", fused.Data.Name);
        Assert.NotNull(FireControl.FuseOf(fused, out _));
        Assert.Equal(FireOutcome.Burst, OutcomeOn(neutral, fused, ai.Player));
    }

    // The five smoke weapons have no product, so nothing offers them: no product names one, the charged one has no home on any
    // hull a product sells, and no generated ship carries one.
    [Fact]
    public void SmokeWeaponsAreNeverOffered()
    {
        var designs = _cache.GetAll<EquippableItemData>().Where(design => SmokeWeapons.Contains(design.Name)).ToArray();
        Assert.Equal(SmokeWeapons.OrderBy(name => name), designs.Select(design => design.Name).OrderBy(name => name));
        var keys = designs.Select(design => _cache.RefOf(design).Key).ToArray();
        Assert.DoesNotContain(_cache.GetAll<FactionProductData>(), product => keys.Any(key => product.Design.Key.Equals(key)));
        Assert.False(LoadoutGenerator.HasHome(designs.Single(design => design.Name == "Smoke Refused Charge"), LoadoutGenerator.SoldHulls(_cache)));

        var arena = Arena(false);
        var generator = new LoadoutGenerator(ref _items.Random, _items, arena.Galaxy, arena.GalaxyZone, null, 2);
        var carried = 0;
        for (var i = 0; i < 100; i++)
        {
            var pack = generator.GenerateShipLoadout();
            Assert.NotNull(pack);
            var ship = EntitySerializer.Unpack(_items, arena, pack);
            carried += ship.Equipment.Count();
            Assert.DoesNotContain(ship.Equipment, item => SmokeWeapons.Contains(item.Data.Name));
        }
        Assert.True(carried > 100, "the generated ships carried no gear to check");
    }

    // New Game lists the three arenas. The census (EveryScenario) is built from the same listing, so only a read of the listing
    // itself notices an arena dropped from it.
    [Fact]
    public void NewGameListsTheThreeSmokeRoundsArenas()
    {
        var listed = Scenarios.Tests.Select(scenario => scenario.Name).ToArray();
        Assert.Contains(new FusedRounds().Name, listed);
        Assert.Contains(new RefusedRounds().Name, listed);
        Assert.Contains(new AiFusedDiscipline().Name, listed);
    }
}
