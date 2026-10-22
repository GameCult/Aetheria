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
        var anchor = _agent.View.SideTrack(task.Anchor.Value);
        if (anchor == null) return;

        var toAnchor = anchor.Value.Position.xz - _agent.Ship.Position.xz;
        var distance = length(toAnchor);
        var direction = distance > 0 ? toAnchor / distance : _agent.Ship.Direction;
        // Positive error: too far, close. Negative: too near, open.
        var error = distance - task.Standoff;
        var pull = direction * (float) sign(error) * saturate(abs(error) / max(task.Standoff, 1f));
        var desired = anchor.Value.Velocity + pull * _agent.TopSpeed;

        _agent.Ship.Command.Aim = float3(direction.x, 0, direction.y);
        _agent.Accelerate(desired);
    }
}
