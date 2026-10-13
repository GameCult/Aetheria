using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching.MessagePack;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Visibility cut 4 (rulings sensor-stat-set, visibility-per-capability-reveal, vapour-cloud-obscuration,
// reveal-ends-on-perception): a ping or a lock paint is an emission, heard by any ship with an active sensor within the
// emitter's range over its intercept factor (thinned by the fog on the line), bearing only, on the tick it is made. The
// numbers are the catalog's own: a ping reaches 300 and a launcher 1000; "Shout" has intercept factor 2 (heard at 150),
// "Roar" .5 (heard at 600), and both beam 10 degrees facing +z, so a hearer off the beam is never painted and its reveal
// is the hearing alone. Every fixture drives the production path (Sensor.Execute and LockWeapon.Execute post through
// Entity.Emit; Sense hears), ticking inside Zone.Update.
public sealed partial class ContactRevealTests
{
    private static Sensor SensorOf(Entity e) => e.Equipment.SelectMany(i => i.Behaviors).OfType<Sensor>().Single();

    private static EquippedItem SensorItemOf(Entity e) => e.Equipment.Single(i => i.Behaviors.Any(b => b is Sensor));

    // The pinger's first tick opens its ping cooldown; Ping() is refused before it. Returns the sweeping tick's clock.
    private float PingAt(Entity pinger)
    {
        Tick(.1f);
        SensorOf(pinger).Ping();
        Tick(.1f);
        return _zone.Time;
    }

    // A launcher at the origin locking a ship 200 ahead of it. Two ticks: the first paint is published on the second.
    private (Ship locker, Ship target) LockOn(string launcher)
    {
        var locker = Add(float3(0, 0, 0), new[] { launcher });
        var target = Add(float3(0, 0, 200));
        locker.SetIff(target, true);
        locker.EntityInfoGathered[target] = 1f;
        locker.SetTarget(target);
        locker.Aim = float3(0, 0, 1);
        return (locker, target);
    }

    // ---- APingIsHeardWithinRangeOverAlpha ----

    [Fact]
    public void APingIsHeardWithinRangeOverAlpha()
    {
        var p = Add(float3(0, 0, 0), new[] { "Shout" });
        var within = Add(float3(149, 0, 0), new[] { "Eye" });   // PingRange 300 / alpha 2 = 150
        var beyond = Add(float3(0, 0, -151), new[] { "Eye" });

        var at = PingAt(p);

        var reveal = Assert.Single(within.RevealsOf(p));
        Assert.Same(SensorItemOf(p), reveal.Item);
        Assert.Equal(SignatureChannel.Emission, reveal.Channel);
        AssertBearing(-1f, 0f, reveal.Bearing); // normalize((P - hearer).xz)
        Assert.Equal(at, reveal.Time);
        Assert.Empty(within.IncomingPaints); // fixture: off the beam, never painted
        Assert.Empty(beyond.RevealsOf(p));
    }

    // ---- ALoudSensorIsHeardBeyondItsReach ----

    [Fact]
    public void ALoudSensorIsHeardBeyondItsReach()
    {
        var p = Add(float3(0, 0, 0), new[] { "Roar" });          // PingRange 300 / alpha .5 = 600
        var within = Add(float3(599, 0, 0), new[] { "Eye" });
        var beyond = Add(float3(0, 0, -601), new[] { "Eye" });

        PingAt(p);

        Assert.Single(within.RevealsOf(p));
        Assert.Empty(beyond.RevealsOf(p));
        Assert.Empty(within.IncomingPaints); // fixture: beyond the ping's radius, so never painted
    }

    // ---- ALockPaintIsHeardByThirdParties ----

    // The launcher's Range is 1000; "Hushed" has alpha 4, heard at 250.
    [Theory]
    [InlineData("Locker", 999f, true)]
    [InlineData("Locker", 1001f, false)]
    [InlineData("Hushed", 249f, true)]
    [InlineData("Hushed", 251f, false)]
    public void ALockPaintIsHeardByThirdParties(string launcher, float distance, bool heard)
    {
        var (locker, target) = LockOn(launcher);
        var third = Add(float3(distance, 0, 0), new[] { "Eye" });

        Tick(.1f);
        Tick(.1f);

        Assert.Single(target.IncomingPaints); // fixture: the lock painted B
        var warned = Assert.Single(target.RevealsOf(locker)); // B records the launcher through its paint either way
        Assert.Equal(SignatureChannel.Emission, warned.Channel);
        if (!heard)
        {
            Assert.Empty(third.RevealsOf(locker));
            return;
        }
        var reveal = Assert.Single(third.RevealsOf(locker));
        Assert.Same(locker.Equipment.Single(i => i.Behaviors.Any(b => b is LockWeapon)), reveal.Item);
        Assert.Equal(SignatureChannel.Emission, reveal.Channel);
        AssertBearing(-1f, 0f, reveal.Bearing);
        Assert.Equal(_zone.Time, reveal.Time);
        Assert.Empty(third.IncomingPaints);
    }

    // ---- AFogBankShortensHearing ----

    // Alpha 2 reaches 150; a bank of opacity .5 across the line passes half of it: 75.
    [Theory]
    [InlineData(74f, true)]
    [InlineData(76f, false)]
    public void AFogBankShortensHearing(float distance, bool heard)
    {
        var p = Add(float3(0, 0, 0), new[] { "Shout" });
        var hearer = Add(float3(distance, 0, 0), new[] { "Eye" });
        Hand(_zone, float2(30, 0), 5f, .5f);
        Assert.Equal(.5f, _zone.Obscuration(p.Position.xz, hearer.Position.xz), 4); // fixture: the bank is on the line

        PingAt(p);

        Assert.Equal(heard ? 1 : 0, hearer.RevealsOf(p).Count());
    }

    // ---- ADeafShipHearsNothing ----

    [Fact]
    public void ADeafShipHearsNothing()
    {
        var p = Add(float3(0, 0, 0), new[] { "Shout" });
        var deaf = Add(float3(100, 0, 0));
        var hearing = Add(float3(-100, 0, 0), new[] { "Eye" });

        PingAt(p);

        Assert.Single(hearing.RevealsOf(p));
        Assert.Empty(deaf.RevealsOf(p));
    }

    // ---- AnEmissionIsHeardOnItsOwnTickWhateverTheListOrder ----

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(30f, -20f)]
    public void AnEmissionIsHeardOnItsOwnTickWhateverTheListOrder(float vx, float vz)
    {
        var before = Add(float3(-30, 0, 40), new[] { "Eye" });
        var p = Add(float3(30, 0, 40), new[] { "Shout" });
        var after = Add(float3(0, 0, 0), new[] { "Eye" });
        var start = p.Position;
        p.Velocity = float2(vx, vz);

        var at = PingAt(p);

        if (vx != 0f || vz != 0f) Assert.True(length(p.Position - start) > 1f, "fixture: the pinger moved");
        foreach (var observer in new Entity[] { before, after })
        {
            var reveal = Assert.Single(observer.RevealsOf(p));
            Assert.Equal(at, reveal.Time);
            var toward = normalize((p.Position - observer.Position).xz);
            AssertBearing(toward.x, toward.y, reveal.Bearing);
        }
    }

    // ---- AnEmissionEndsWithItsAct ----

    [Fact]
    public void AnEmissionEndsWithItsAct()
    {
        var p = Add(float3(0, 0, 0), new[] { "Shout" });
        var heard = Add(float3(100, 0, 0), new[] { "Eye" });
        var late = Add(float3(0, 0, -1000), new[] { "Eye" });
        var ended = false;
        SensorOf(p).OnPingEnd += () => ended = true;
        PingAt(p);
        for (var tick = 0; tick < 40 && !ended; tick++) Tick(.1f);
        Assert.True(ended, "fixture: the ping ended");
        var last = Freshest(heard, p);
        Assert.Equal(_zone.Time, last); // the ending tick still emitted

        late.Position = float3(-100, 0, 0); // moved into range after the act is over
        for (var tick = 0; tick < 3; tick++) Tick(.1f);

        Assert.Equal(last, Freshest(heard, p));
        Assert.Empty(late.RevealsOf(p));
    }

    [Fact]
    public void ALockEmissionEndsWhenTheLockLeavesItsAngle()
    {
        var (locker, target) = LockOn("Locker");
        var third = Add(float3(500, 0, 0), new[] { "Eye" });
        Tick(.1f);
        Tick(.1f);
        var last = Freshest(third, locker);
        Assert.Equal(_zone.Time, last); // fixture: heard on the lock's tick

        locker.Aim = float3(0, 0, -1); // 180 degrees off the target, past LockAngle
        for (var tick = 0; tick < 3; tick++) Tick(.1f);

        Assert.Equal(last, Freshest(third, locker));
    }

    // ---- HearingFeedsNoTrack ----

    [Fact]
    public void HearingFeedsNoTrack()
    {
        var (heardTrack, heardReveals) = RunHeard(ping: true);
        var (controlTrack, controlReveals) = RunHeard(ping: false);

        Assert.Equal(1, heardReveals); // fixture: the emission was heard
        Assert.Equal(0, controlReveals);
        Assert.Equal(controlTrack, heardTrack, 6);
    }

    private static (float track, int reveals) RunHeard(bool ping)
    {
        using var t = new ContactRevealTests();
        var p = t.Add(float3(0, 0, 0), new[] { "Shout" });
        var h = t.Add(float3(100, 0, 0), new[] { "Eye" });
        if (ping) t.PingAt(p);
        else
        {
            t.Tick(.1f);
            t.Tick(.1f);
        }
        return (Track(h, p), h.RevealsOf(p).Count());
    }

    // ---- AnAllyCreditingAReturnEmitsNothing ----

    // PaintModeTests.AnAllyHearsTheReturnSilently's geometry: A pings T, ally C hears the return. X stands near C and far
    // from A, so X hears C if and only if C emitted.
    [Fact]
    public void AnAllyCreditingAReturnEmitsNothing()
    {
        var a = Add(float3(0, 0, 0), new[] { "Shout" });
        var t = Add(float3(0, 0, 100));
        var c = Add(float3(30, 0, 140), new[] { "Eye" });
        c.Faction = a.Faction;
        var x = Add(float3(100, 0, 200), new[] { "Eye" });
        var returns = typeof(Entity).GetProperty("PingReturns", BindingFlags.Instance | BindingFlags.NonPublic);

        Tick(.1f);
        SensorOf(a).Ping();
        var credited = false;
        for (var tick = 0; tick < 24; tick++)
        {
            Tick(.1f);
            credited |= ((System.Collections.ICollection) returns.GetValue(c)).Count > 0;
        }

        Assert.True(credited, "fixture: C was handed A's return");
        Assert.NotEmpty(t.RevealsOf(a)); // fixture: T was painted
        Assert.Empty(x.RevealsOf(c));
        Assert.Empty(x.RevealsOf(a)); // fixture: X is beyond A's 150
    }

    // ---- TheInterceptFactorKeys ----

    private static MessagePackSerializerOptions EmissionWire => CultDocumentMessagePackSerialization.OptionsFor(typeof(ZonePack).Assembly);

    // InterceptFactor is slot 13 of the sensor record and slot 34 of the lock weapon (a launcher is its stored arm; its
    // own slot 26 is the guidance curve), and the JSON property interceptFactor. Read as bytes, so a renumbered key or a
    // renamed property cannot pass because writer and reader move together.
    [Fact]
    public void TheInterceptFactorKeys()
    {
        var sensor = MessagePackSerializer.Serialize<BehaviorData>(new SensorData { InterceptFactor = Const(2) }, EmissionWire);
        var reader = new MessagePackReader(sensor);
        Assert.Equal(2, reader.ReadArrayHeader());
        Assert.Equal(5, reader.ReadInt32());
        Assert.Equal(14, reader.ReadArrayHeader());
        for (var slot = 0; slot < 13; slot++) reader.Skip();
        Assert.Equal(2f, MessagePackSerializer.Deserialize<PerformanceStat>(ref reader, EmissionWire).Min);
        Assert.Equal(2f, JObject.Parse(JsonConvert.SerializeObject(new SensorData { InterceptFactor = Const(2) }))["interceptFactor"].ToObject<PerformanceStat>().Min);

        var launcher = new LauncherData { InterceptFactor = Const(3), GuidanceCurve = new[] { float4(0, 1, 0, 0) } };
        var bytes = MessagePackSerializer.Serialize<BehaviorData>(launcher, EmissionWire);
        reader = new MessagePackReader(bytes);
        Assert.Equal(2, reader.ReadArrayHeader());
        reader.Skip(); // the arm's union index
        Assert.Equal(35, reader.ReadArrayHeader());
        for (var slot = 0; slot < 34; slot++) reader.Skip();
        Assert.Equal(3f, MessagePackSerializer.Deserialize<PerformanceStat>(ref reader, EmissionWire).Min);
        Assert.Equal(3f, JObject.Parse(JsonConvert.SerializeObject(new LauncherData { InterceptFactor = Const(3) }))["interceptFactor"].ToObject<PerformanceStat>().Min);

        var back = Assert.IsType<LauncherData>(MessagePackSerializer.Deserialize<BehaviorData>(bytes, EmissionWire));
        Assert.Equal(3f, back.InterceptFactor.Min);
        Assert.Single(back.GuidanceCurve);
    }

    [Fact]
    public void AnAbsentInterceptFactorReadsAsOne()
    {
        var plain = Add(float3(0, 0, 0), new[] { "Eye" });
        var shout = Add(float3(10, 0, 0), new[] { "Shout" });
        var locker = Add(float3(20, 0, 0), new[] { "Locker" });
        var hushed = Add(float3(30, 0, 0), new[] { "Hushed" });

        Assert.Equal(1f, SensorOf(plain).InterceptFactor);
        Assert.Equal(2f, SensorOf(shout).InterceptFactor);
        Assert.Equal(1f, locker.Equipment.SelectMany(i => i.Behaviors).OfType<LockWeapon>().Single().InterceptFactor);
        Assert.Equal(4f, hushed.Equipment.SelectMany(i => i.Behaviors).OfType<LockWeapon>().Single().InterceptFactor);
    }
}
