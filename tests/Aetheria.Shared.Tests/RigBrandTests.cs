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

    // Each rig is staged under eight seeds: an open design would draw its brand from the seed, so a pin that holds under
    // all of them is a pin, not a draw that happened to land on the same product.
    [Fact]
    public void TheRigsKeepTheirBrands()
    {
        for (uint seed = 1; seed <= 8; seed++)
        {
            var pursuit = Launch(new Scripted(false, new Pursuit().Stage, seed * 7919));
            Assert.Empty(pursuit.failures);
            Assert.Equal(new[] { "parting gift" }, FittedBrands(pursuit.staged.Player, "Mine Launcher"));
            Assert.Equal(new[] { "Leonid" }, FittedBrands(pursuit.staged.Player, "Swarm Rocket Pod"));

            var duel = Launch(new Scripted(false, new Duel().Stage, seed * 7919));
            Assert.Empty(duel.failures);
            var rival = Assert.Single(duel.staged.Entities);
            Assert.Equal(new[] { "Spectra" }, Held(rival).Select(item => _items.Brand(item).Product.Name));

            var arcs = Launch(new Scripted(false, new Arcs().Stage, seed * 7919));
            Assert.Empty(arcs.failures);
            Assert.Equal(new[] { "🫳🔫" }, FittedBrands(arcs.staged.Player, "Autocannon"));
        }
    }
}
