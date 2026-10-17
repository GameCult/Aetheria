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

    private const string Sensor = "Compact Sensor", Fca = "Fire Control Array", Missile = "Guided Missile Launcher", Cell = "Cell Blaster", Cargo = "Cargo Bay";

    // Every product a scenario's fits name, as (design, brand). A rig whose fits name a design where a brand stood would
    // draw its brand from the stage's seed; so each scenario is staged under eight seeds, and every pinned design must
    // come out as exactly its brand on every ship the scenario fits. A hull that is not a fit's (a generated or bare ship)
    // is left out: its gear is not pinned.
    public static IEnumerable<object[]> PinnedRigs() => new[]
    {
        Rig(new AiFusedDiscipline(), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new Arcs(), (Missile, "GT 3K"), (Cell, "FastBlast+-"), ("Autocannon", "🫳🔫"), ("Charged Scatter Blaster", "ChargeBlast SG"), ("Light Machine Gun", "ClearPath"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new Belt(), (Missile, "GT 3K"), ("Lightning Projector", "DragOnBreath"), (Cell, "FastBlast+-"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new Crossing(), (Missile, "GT 3K"), ("Lightning Projector", "DragOnBreath"), (Cell, "FastBlast+-"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new Duel(), (Missile, "GT 3K"), (Cell, "FastBlast+-"), (Cargo, "Store-All Plus"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new FusedRounds(), ("Cluster Cannon", "DeathCluster"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new LauncherAngles(), (Missile, "GT 3K"), ("Swarm Rocket Pod", "Leonid"), (Cell, "FastBlast+-"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new LockOn(), (Missile, "GT 3K"), (Cell, "FastBlast+-"), (Cargo, "Store-All Plus"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new Pursuit(), ("Mine Launcher", "parting gift"), ("Swarm Rocket Pod", "Leonid"), (Missile, "GT 3K"), (Cell, "FastBlast+-"), (Cargo, "Store-All Plus"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new RefusedRounds(), ("Autocannon", "🫳🔫"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new StarvedReactor(), ("Light Laser", "Spectra"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
        Rig(new WeaponFeel(), ("Charged Scatter Blaster", "ChargeBlast SG"), ("Autocannon", "🫳🔫"), (Cell, "FastBlast+-"), (Sensor, "not if i see you first"), (Fca, "Panopticon Prime")),
    };

    private static object[] Rig(Scenario scenario, params (string design, string brand)[] pins) => new object[] { scenario, pins };

    [Theory]
    [MemberData(nameof(PinnedRigs))]
    public void TheRigsKeepTheirBrands(Scenario scenario, (string design, string brand)[] pins)
    {
        for (uint seed = 1; seed <= 8; seed++)
        {
            var (_, _, staged, failures) = Launch(new Scripted(false, scenario.Stage, seed * 7919, scenario.Generate));
            Assert.Empty(failures);
            var ships = staged.Entities.Append(staged.Player).Where(ship => _items.GetData(ship.Hull).Name is "Longinus" or "Smoke Test Hull");
            foreach (var (design, brand) in pins)
            {
                var brands = ships.SelectMany(ship => FittedBrands(ship, design)).Distinct().ToArray();
                Assert.True(brands.SequenceEqual(new[] { brand }), $"{scenario.Name} seed {seed}: {design} is built as [{string.Join(", ", brands)}], not {brand}");
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
