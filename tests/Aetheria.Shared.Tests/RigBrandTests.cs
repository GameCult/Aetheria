using System.Collections.Generic;
using System.Linq;
using Xunit;

// Ruling loadout-product-optional-context-chooses: a rig whose test reads stats names the brand it was measured on, and
// staging builds exactly that brand whatever the stage's seed draws for open designs. The fixture is RunStartTests'.
public sealed partial class RunStartTests
{
    private string ProductOf(EquippableItem item) => _items.Brand(item).Product.Name;

    private IEnumerable<string> FittedBrands(Entity entity, string design) =>
        entity.Equipment.Where(item => _items.GetData(item.EquippableItem).Name == design).Select(item => ProductOf(item.EquippableItem));

    [Fact]
    public void TheRigsKeepTheirBrands()
    {
        var pursuit = Launch(new Pursuit(), Inputs(() => GalaxySeed));
        Assert.Empty(pursuit.failures);
        Assert.Equal(new[] { "parting gift" }, FittedBrands(pursuit.staged.Player, "Mine Launcher"));
        Assert.Equal(new[] { "Leonid" }, FittedBrands(pursuit.staged.Player, "Swarm Rocket Pod"));

        var duel = Launch(new Duel(), Inputs(() => GalaxySeed));
        Assert.Empty(duel.failures);
        var rival = Assert.Single(duel.staged.Entities);
        Assert.Equal(new[] { "Spectra" }, Held(rival).Select(item => _items.Brand(item).Product.Name));

        var arcs = Launch(new Arcs(), Inputs(() => GalaxySeed));
        Assert.Empty(arcs.failures);
        Assert.Equal(new[] { "🫳🔫" }, FittedBrands(arcs.staged.Player, "Autocannon"));
    }
}
