using System;

public class PatrolOrbitsTask : AgentTask
{
    public override TaskType Type => TaskType.Defend;
    public string[] Circuit = Array.Empty<string>();
}

public class PatrolOrbitsState : BaseState
{
    public PatrolOrbitsTask Task;
    public string CurrentTarget
    {
        get => Task.Circuit[_currentTargetIndex];
    }
    private int _currentTargetIndex;
    public void NextTarget()
    {
        _currentTargetIndex++;
        _currentTargetIndex %= Task.Circuit.Length;
    }

    public PatrolOrbitsState(Agent agent) : base(agent)
    {
        var patrolMoveState = new MoveToOrbitState(agent);
        Transitions.Add(new StateTransition(patrolMoveState, 
            () => true, 
            () => patrolMoveState.OrbitKey = CurrentTarget));
        patrolMoveState.Transitions.Add(new StateTransition(this, () => patrolMoveState.Distance < 10, NextTarget));
    }
}
