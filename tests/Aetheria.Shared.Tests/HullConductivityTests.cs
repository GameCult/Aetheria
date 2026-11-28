using System.Linq;
using Xunit;

// A hull conducts heat on every edge from construction (Entity.MapEntity), so a fit a scenario authors, a hand-built ship and a
// generated one all start where the generator's own ships always did; only the player's insulation toggle or a saved ship's edges change it.
public sealed partial class RunStartTests
{
    // The Duel fit goes through Loadouts.Materialize, which never wrote conductivity. Kills: MapEntity leaving the grid all-false
    // (an authored hull insulated on every edge, as at the base: 0-2 rocks a sortie against 1-33 conducting).
    [Fact]
    public void AMaterializedHullConductsOnEveryEdge()
    {
        var (_, _, staged, failures) = Launch(new Duel(), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var ship = staged.Player;
        var cells = ship.HullData.Shape.Coordinates;
        Assert.NotEmpty(cells);
        Assert.All(cells, v => Assert.True(ship.HullConductivity[v.x, v.y].x && ship.HullConductivity[v.x, v.y].y, $"cell {v}"));
    }
}
