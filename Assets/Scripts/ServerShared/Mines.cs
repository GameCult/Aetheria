// A laid mine: a free-floating body with a frozen payload. Not an Entity, never priced or rolled, never saved.
// Zone.Lay is the only adder and Zone's mine step the only remover; every removal is its blast.
public sealed class Mine
{
    public FloatingBodyId Id;
    public KinematicBody Body;
    public double LaidAt;
    public float ArmingDelay;
    public float FuseDelay;
    public float Lifetime;
    public float BlastRadius;
    public float Damage;
    public DamageType DamageType;
    // The layer and its faction, frozen at lay (ruling mine-trigger-iff).
    public Entity Layer;
    public Faction LayerFaction;
    // Set once, when an eligible hull first touches the armed disc; the fuse counts from it.
    public double? TriggeredAt;

    public bool Armed(double now) => now >= LaidAt + ArmingDelay;

    // Ruling mine-trigger-iff: only an entity that is neither the layer nor of the layer's faction triggers it.
    public bool Triggers(Entity entity) =>
        entity != Layer && (LayerFaction == null || entity.Faction != LayerFaction);
}
