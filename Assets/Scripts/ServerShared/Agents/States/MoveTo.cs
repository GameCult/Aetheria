
using System;
using static CultMath.math;
using cfloat2 = CultMath.float2;

public abstract class MoveToState : BaseState
{
    private VelocityLimit _velocityLimit;
    protected abstract cfloat2 TargetPosition { get; }
    public float Distance { get; private set; }

    protected MoveToState(Agent agent) : base(agent)
    {
    }

    public override void Update(float delta)
    {
        var diff = TargetPosition - _agent.Ship.CultPositionXZ;
        var dir = normalize(diff);
        Distance = length(diff);
        
        // We want to go top speed in the direction of our target
        var desiredVelocity = dir * _agent.TopSpeed;
        _agent.Ship.CultLookDirectionXZ = dir;
        _agent.Accelerate(desiredVelocity);
    }
}

public class MoveToEntityState : MoveToState
{
    public Entity TargetEntity { get; set; }
    protected override cfloat2 TargetPosition => TargetEntity != null ? TargetEntity.CultPositionXZ : cfloat2.zero;

    public MoveToEntityState(Agent agent) : base(agent) { }
}

public class MoveToOrbitState : MoveToState
{
    public string OrbitKey { get; set; } = "";
    public MoveToOrbitState(Agent agent) : base(agent) { }

    protected override cfloat2 TargetPosition => AetheriaMath.ToCult(_agent.Ship.Zone.GetOrbitPosition(OrbitKey));
}
