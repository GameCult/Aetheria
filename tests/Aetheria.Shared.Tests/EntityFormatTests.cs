using System.Linq;
using Xunit;
using Xunit.Sdk;
using static CultMath.math;

// follow-up entity-formats-safely: a failing assertion over an Entity reports it by name. xunit formats an object by walking
// its graph unless the object has a ToString, and an Entity's graph is the whole zone, which overflowed the test host on
// every failing Assert over an Entity.
public sealed partial class ContactRevealTests
{
    [Fact]
    public void EntityFormatsByName()
    {
        var bay = AddBay(float3(0, 0, 100), gun: true);
        var other = Add(float3(0, 0, 0));

        var entities = Assert.ThrowsAny<XunitException>(() => Assert.Same(bay, other));
        Assert.Contains("Ship 'Bay'", entities.Message);
        Assert.Contains("Ship 'Rack'", entities.Message);
        Assert.True(entities.Message.Length < 2000, $"the message is {entities.Message.Length} characters");

        var gun = Guns(bay).Single();
        var reactor = Fitted(bay, "BayReactor");
        var items = Assert.ThrowsAny<XunitException>(() => Assert.Same(gun, reactor));
        Assert.True(items.Message.Length < 2000, $"the message is {items.Message.Length} characters");
    }
}
