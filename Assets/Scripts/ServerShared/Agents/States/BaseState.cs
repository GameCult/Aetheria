using System;
using System.Collections.Generic;

public class BaseState
{
    public List<StateTransition> Transitions { get; } = new List<StateTransition>();
    protected Agent _agent;
    

    public BaseState(Agent agent)
    {
        _agent = agent;
    }

    // The walk's state is the call's own: zones (and so their Minions) are built on more than one thread.
    public void AddTransition(BaseState targetState, Func<bool> condition, Action onTransition = null, bool includeChildren = false, params BaseState[] ignoreStates)
    {
        Transitions.Add(new StateTransition(targetState, condition, onTransition));

        // Traverse the state graph, adding every state that is reachable from this state without traversing over ignored nodes
        if (includeChildren)
        {
            var visitedStates = new List<BaseState>();
            var ignored = new HashSet<BaseState>();
            if(ignoreStates!=null)
                foreach (var ignoreState in ignoreStates)
                    ignored.Add(ignoreState);
            var leafStates = new List<BaseState> { this };
            ignored.Add(this);
            ignored.Add(targetState);
            while (leafStates.Count > 0)
            {
                var lastVisitedStates = leafStates;
                leafStates = new List<BaseState>();
                foreach (var state in lastVisitedStates)
                {
                    foreach (var transition in state.Transitions)
                    {
                        if(!ignored.Contains(transition.TargetState))
                        {
                            visitedStates.Add(transition.TargetState);
                            ignored.Add(transition.TargetState);
                            leafStates.Add(transition.TargetState);
                        }
                    }
                }
            }
            foreach (var state in visitedStates)
            {
                state.AddTransition(targetState, condition, onTransition);
            }
        }
    }
    
    public virtual void Update(float delta){}
    public virtual void OnEnterState(){}
    public virtual void OnExitState(){}
}