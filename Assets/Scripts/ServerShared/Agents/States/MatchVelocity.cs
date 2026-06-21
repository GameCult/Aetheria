using static CultMath.math;
using cfloat2 = CultMath.float2;

public abstract class MatchVelocityState : BaseState
{
    protected abstract cfloat2 TargetVelocity { get; }

    protected MatchVelocityState(Agent agent) : base(agent) { }

    public override void Update(float delta)
    {
        _agent.Accelerate(TargetVelocity);
    }
    
    public cfloat2 MatchDistanceTime
    {
        get
        {
            var velocity = length(_agent.Ship.CultVelocity);
            var deltaV = TargetVelocity - _agent.Ship.CultVelocity;
            
            var stoppingTime = length(deltaV) / (_agent.Ship.ForwardThrust / _agent.Ship.Mass);
            var stoppingDistance = stoppingTime * (velocity / 2);

            var turnaroundTime = _agent.Ship.TurnTime(deltaV);
            var turnaroundDistance = turnaroundTime * velocity;
            
            return new cfloat2(stoppingDistance + turnaroundDistance, stoppingTime + turnaroundTime);
        }
    }
}
