using System.Linq;
using UniRx;

// The agent graph for a piloted ship. It executes the Task and Target its flight writes (Flight.cs) and decides
// neither: patrol and follow follow the Task kind, combat follows the Target.
public class Minion : Agent
{
    public Minion(Ship ship, RoleDoctrine doctrine) : base(ship, doctrine)
    {
        var patrolState = new PatrolOrbitsState(this);
        var followState = new FollowState(this);
        void Patrol() => patrolState.Task = Task as PatrolOrbitsTask;
        _rootState.AddTransition(patrolState, () => Task is PatrolOrbitsTask, Patrol);
        _rootState.AddTransition(followState, () => Task is FollowTask);

        // A Task change switches the substate in one update, from wherever the pilot is in the old task. includeChildren
        // reaches the patrol sub-states (MoveToOrbit) that Update actually sits in; the walk never enters the other task's state.
        patrolState.AddTransition(followState, () => Task is FollowTask, null, true, _rootState);
        patrolState.AddTransition(_rootState, () => !(Task is PatrolOrbitsTask), null, true, _rootState, followState);
        followState.AddTransition(patrolState, () => Task is PatrolOrbitsTask, Patrol);
        followState.AddTransition(_rootState, () => !(Task is FollowTask));

        var combatState = new CombatState(this);
        _rootState.AddTransition(combatState,
            () => Ship.Target.Value.Entity != null, null, true, _rootState);

        combatState.AddTransition(_rootState,
            () => Ship.Target.Value.Entity == null, null, true, _rootState);
    }
}
