using GameCult.Caching;
using System;

public class PatrolOrbitsTask : AgentTask
{
    public override TaskType Type => TaskType.Defend;
    public CultRecordKey[] Circuit;
}

public class PatrolOrbitsState : BaseState
{
    public PatrolOrbitsTask Task;
    public CultRecordKey CurrentTarget
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
            () => patrolMoveState.Orbit = CurrentTarget));
        // Arrival advances the circuit only while the pilot is still on this task: a task change leaves the patrol first.
        patrolMoveState.Transitions.Add(new StateTransition(this, () => patrolMoveState.Distance < 10 && ReferenceEquals(agent.Task, Task), NextTarget));
    }
}