using System.Collections.Generic;
using System.Linq;
using Xunit;

// Ruling loadout-product-optional-context-chooses: a rig whose test reads stats names the brand it was measured on, and
// staging builds exactly that brand whatever the stage's seed draws for open designs. The fixture is RunStartTests'.
public sealed partial class RunStartTests
{
    private string ProductOf(EquippableItem item) => _items.Brand(item).Product.Name;

    private IEnumerable<string> FittedBrands(Entity entity, string design) =>
        entity.Equipment.Concat(entity.CargoBays).Where(item => _items.GetData(item.EquippableItem).Name == design).Select(item => ProductOf(item.EquippableItem));

    // Every product a scenario's fits name. A rig whose fits name a design where a brand stood would draw its brand from
    // the stage's seed; so each scenario is staged under eight seeds, and the design of every pinned product must come out
    // as exactly that product on every ship the scenario fits. A hull that is not a fit's (a generated or bare ship)
    // is left out: its gear is not pinned.
    public static IEnumerable<object[]> PinnedRigs() => new[]
    {
        Rig(new AiFusedDiscipline(), "not if i see you first", "Panopticon Prime"),
        Rig(new Arcs(), "GT 3K", "FastBlast+-", "🫳🔫", "ChargeBlast SG", "ClearPath", "not if i see you first", "Panopticon Prime"),
        Rig(new Belt(), "GT 3K", "DragOnBreath", "FastBlast+-", "not if i see you first", "Panopticon Prime"),
        Rig(new Crossing(), "GT 3K", "DragOnBreath", "FastBlast+-", "not if i see you first", "Panopticon Prime"),
        Rig(new Duel(), "GT 3K", "FastBlast+-", "Store-All Plus", "not if i see you first", "Panopticon Prime"),
        Rig(new FusedRounds(), "DeathCluster", "not if i see you first", "Panopticon Prime"),
        Rig(new LauncherAngles(), "GT 3K", "Leonid", "FastBlast+-", "not if i see you first", "Panopticon Prime"),
        Rig(new LockOn(), "GT 3K", "FastBlast+-", "Store-All Plus", "not if i see you first", "Panopticon Prime"),
        Rig(new Pursuit(), "parting gift", "Leonid", "GT 3K", "FastBlast+-", "Store-All Plus", "not if i see you first", "Panopticon Prime"),
        Rig(new RefusedRounds(), "🫳🔫", "not if i see you first", "Panopticon Prime"),
        Rig(new StarvedReactor(), "Spectra", "not if i see you first", "Panopticon Prime"),
        Rig(new WeaponFeel(), "ChargeBlast SG", "🫳🔫", "FastBlast+-", "not if i see you first", "Panopticon Prime"),
    };

    private static object[] Rig(Scenario scenario, params string[] brands) => new object[] { scenario, brands };

    [Theory]
    [MemberData(nameof(PinnedRigs))]
    public void TheRigsKeepTheirBrands(Scenario scenario, string[] brands)
    {
        for (uint seed = 1; seed <= 8; seed++)
        {
            var (_, _, staged, failures) = Launch(new Scripted(false, scenario.Stage, seed * 7919, scenario.Generate));
            Assert.Empty(failures);
            var ships = staged.Entities.Append(staged.Player).Where(ship => _items.GetData(ship.Hull).Name is "Longinus" or "Smoke Test Hull");
            foreach (var brand in brands)
            {
                var product = _items.ItemData.GetAll<FactionProductData>().Single(candidate => candidate.Name == brand);
                var design = _items.ItemData.Get(product.Design).Name;
                var built = ships.SelectMany(ship => FittedBrands(ship, design)).Distinct().ToArray();
                Assert.True(built.SequenceEqual(new[] { brand }), $"{scenario.Name} seed {seed}: {design} is built as [{string.Join(", ", built)}], not {brand}");
            }
        }
    }

    // Duel's rival holds Spectra in its cargo.
    [Fact]
    public void TheDuelRivalHoldsItsBrand()
    {
        var duel = Launch(new Scripted(false, new Duel().Stage, 7919));
        Assert.Empty(duel.failures);
        var rival = Assert.Single(duel.staged.Entities);
        Assert.Equal(new[] { "Spectra" }, Held(rival).Select(item => _items.Brand(item).Product.Name));
    }
}
