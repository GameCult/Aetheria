using MessagePack;
using Newtonsoft.Json;

// A coolant dump that becomes a vapour cloud: the first time it runs it vents one cloud around its venter,
// drifting with the venter's velocity. Radius, Opacity and Lifetime are evaluated through the host, so role
// quality applies as for any other stat. Later runs vent nothing.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class VapourDumpData : BehaviorData
{
    [Inspectable, JsonProperty("radius"), Key(1), RuntimeInspectable]
    public PerformanceStat Radius = new PerformanceStat();

    // How much of a sight line's signal a fresh cloud removes, in [0, 1].
    [Inspectable, JsonProperty("opacity"), Key(2), RuntimeInspectable]
    public PerformanceStat Opacity = new PerformanceStat();

    // Seconds from venting until the cloud has faded away and is removed.
    [Inspectable, JsonProperty("lifetime"), Key(3), RuntimeInspectable]
    public PerformanceStat Lifetime = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new VapourDump(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new VapourDump(this, item);
    }
}

public class VapourDump : Behavior
{
    private VapourDumpData _data;
    private bool _vented;

    public VapourDump(VapourDumpData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public VapourDump(VapourDumpData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        if (_vented) return true;
        _vented = true;
        Entity.Zone.Vent(new VapourCloud
        {
            Body = new KinematicBody { Position = Entity.Position.xz, Velocity = Entity.Velocity },
            Radius = Evaluate(_data.Radius),
            Opacity = Evaluate(_data.Opacity),
            Lifetime = Evaluate(_data.Lifetime),
            Venter = Entity
        });
        return true;
    }
}
