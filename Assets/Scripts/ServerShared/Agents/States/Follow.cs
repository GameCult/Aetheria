using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

// Hold a standoff from the anchor: match its velocity, and close or open the gap in proportion to the error.
public class FollowState : BaseState
{
    public FollowState(Agent agent) : base(agent) { }

    public override void Update(float delta)
    {
        var task = _agent.Task as FollowTask;
        if (task?.Anchor == null) return;

        var toAnchor = task.Anchor.Position.xz - _agent.Ship.Position.xz;
        var distance = length(toAnchor);
        var direction = distance > 0 ? toAnchor / distance : _agent.Ship.Direction;
        // Positive error: too far, close. Negative: too near, open.
        var error = distance - task.Standoff;
        var pull = direction * (float) sign(error) * saturate(abs(error) / max(task.Standoff, 1f));
        var desired = task.Anchor.Velocity + pull * _agent.TopSpeed;

        _agent.Ship.Aim = float3(direction.x, 0, direction.y);
        _agent.Accelerate(desired);
    }
}
