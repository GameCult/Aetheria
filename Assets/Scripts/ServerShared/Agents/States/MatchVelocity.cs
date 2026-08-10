using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

public abstract class MatchVelocityState : BaseState
{
    protected abstract float2 TargetVelocity { get; }

    protected MatchVelocityState(Agent agent) : base(agent) { }

    public override void Update(float delta)
    {
        _agent.Accelerate(TargetVelocity);
    }
    
    public float2 MatchDistanceTime
    {
        get
        {
            var velocity = length(_agent.Ship.Velocity);
            var deltaV = TargetVelocity - _agent.Ship.Velocity;
            
            var stoppingTime = length(deltaV) / (_agent.Ship.ForwardThrust / _agent.Ship.Mass);
            var stoppingDistance = stoppingTime * (velocity / 2);

            var turnaroundTime = _agent.Ship.TurnTime(deltaV);
            var turnaroundDistance = turnaroundTime * velocity;
            
            return float2(stoppingDistance + turnaroundDistance, stoppingTime + turnaroundTime);
        }
    }
}