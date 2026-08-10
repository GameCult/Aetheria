
using System;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

public abstract class MoveToState : BaseState
{
    private VelocityLimit _velocityLimit;
    protected abstract float2 TargetPosition { get; }
    public float Distance { get; private set; }

    protected MoveToState(Agent agent) : base(agent)
    {
    }

    public override void Update(float delta)
    {
        var diff = TargetPosition - _agent.Ship.Position.xz;
        var dir = normalize(diff);
        Distance = length(diff);
        
        // We want to go top speed in the direction of our target
        var desiredVelocity = dir * _agent.TopSpeed;
        _agent.Ship.LookDirection = float3(dir.x, 0, dir.y);
        _agent.Accelerate(desiredVelocity);
    }
}

public class MoveToEntityState : MoveToState
{
    public Entity TargetEntity { get; set; }
    protected override float2 TargetPosition => TargetEntity?.Position.xz ?? float2.zero;

    public MoveToEntityState(Agent agent) : base(agent) { }
}

public class MoveToOrbitState : MoveToState
{
    public Guid Orbit { get; set; }
    public MoveToOrbitState(Agent agent) : base(agent) { }

    protected override float2 TargetPosition => _agent.Ship.Zone.GetOrbitPosition(Orbit);
}
