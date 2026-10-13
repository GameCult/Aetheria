using System.Linq;
using CultMath;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;

// Passive listening gear (ruling sensor-stat-set): an observer with a listener hears an emission at the emitter's own
// range times the listener's Multiple, bearing only. The pinger is the fixture's "Eye" (PingRange 300, intercept
// factor absent so 1), so a sensor hears it at 300 and the "Listener" (Multiple 3) at 900. Every fixture drives the
// production path: Sensor.Execute posts through Entity.Emit and Sense hears, ticking inside Zone.Update.
public sealed partial class ContactRevealTests
{
    private static GearData Listener(string name, float multiple)
    {
        var cell = new Shape(1, 1);
        cell[new int2(0, 0)] = true;
        return new GearData
        {
            Name = name, Hardpoint = HardpointType.Tool, Shape = cell, Durability = 10,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new ListenerData { Multiple = Const(multiple) } }
        };
    }

    // ---- AListenerHearsAtItsMultiple ----

    [Theory]
    [InlineData(899f, true)]
    [InlineData(901f, false)]
    public void AListenerHearsAtItsMultiple(float distance, bool heard)
    {
        var p = Add(float3(0, 0, 0), new[] { "Eye" });
        var ear = Add(float3(distance, 0, 0), tools: new[] { "Listener" });

        var at = PingAt(p);

        if (!heard) { Assert.Empty(ear.RevealsOf(p)); return; }
        var reveal = Assert.Single(ear.RevealsOf(p));
        Assert.Equal(SignatureChannel.Emission, reveal.Channel);
        Assert.Equal(at, reveal.Time);
        AssertBearing(-1f, 0f, reveal.Bearing); // normalize((P - ear).xz)
        Assert.Equal(0f, Track(ear, p)); // bearing only: it builds no track
    }

    // ---- AListenerAndASensorTakeTheLarger ----

    [Theory]
    [InlineData(true, 899f, true)]
    [InlineData(true, 901f, false)]
    [InlineData(false, 299f, true)]
    [InlineData(false, 301f, false)]
    public void AListenerAndASensorTakeTheLarger(bool listenerActive, float distance, bool heard)
    {
        var p = Add(float3(0, 0, 0), new[] { "Eye" });
        var both = Add(float3(distance, 0, 0), new[] { "Eye" }, new[] { "Listener" }); // a sum would hear at 1200
        var listener = both.Equipment.Single(i => i.Behaviors.Any(b => b is Listener));
        listener.Enabled.Value = listenerActive;

        PingAt(p);

        Assert.Equal(heard ? 1 : 0, both.RevealsOf(p).Count());
    }

    // ---- AnInactiveListenerHearsNothing ----

    [Fact]
    public void AnInactiveListenerHearsNothing()
    {
        var p = Add(float3(0, 0, 0), new[] { "Eye" });
        var ear = Add(float3(100, 0, 0), tools: new[] { "Listener" });
        ear.Equipment.Single(i => i.Behaviors.Any(b => b is Listener)).Enabled.Value = false;

        PingAt(p);

        Assert.Empty(ear.RevealsOf(p));
    }

    // ---- AConsumableListenerHearsAtItsMultiple ----

    // The Earpiece is a consumable hosting a Listener (Multiple 3): an observer with it active hears the "Eye" at 900, the
    // same as with the gear; with it not yet activated it has no instrument at all.
    [Theory]
    [InlineData(true, 899f, true)]
    [InlineData(true, 901f, false)]
    [InlineData(false, 100f, false)]
    public void AConsumableListenerHearsAtItsMultiple(bool activated, float distance, bool heard)
    {
        var p = Add(float3(0, 0, 0), new[] { "Eye" });
        var ear = Add(float3(distance, 0, 0));
        if (activated)
        {
            var data = _openCache.GetByName<ConsumableItemData>("Earpiece");
            ear.ActivateConsumable(Assert.IsType<ConsumableItem>(_items.CreateInstance(_items.CreateLot(data, _maker, .5f))));
            Assert.Single(ear.FindActiveConsumable(data).Behaviors.OfType<Listener>()); // fixture: the consumable hosts it
        }

        PingAt(p);

        Assert.Equal(heard ? 1 : 0, ear.RevealsOf(p).Count());
    }

    // ---- ListenerDataRoundTripsAsUnionKey50 ----

    [Fact]
    public void ListenerDataRoundTripsAsUnionKey50()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ZonePack).Assembly);
        var data = new ListenerData { Multiple = Const(2.5f) };

        var bytes = MessagePackSerializer.Serialize<BehaviorData>(data, options);
        var reader = new MessagePackReader(bytes);
        Assert.Equal(2, reader.ReadArrayHeader());
        Assert.Equal(50, reader.ReadInt32());
        Assert.Equal(2, reader.ReadArrayHeader()); // Group is key 0, then Multiple

        var back = Assert.IsType<ListenerData>(MessagePackSerializer.Deserialize<BehaviorData>(bytes, options));
        Assert.Equal(2.5f, back.Multiple.Min);
    }
}
