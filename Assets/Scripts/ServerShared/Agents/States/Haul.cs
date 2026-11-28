// A hauler's pilot: it flies to the orbit of HaulRun.Leg and, within docking distance of a station where the call would move a unit,
// commands the call (HaulRun.Call docks, moves the goods and undocks it, which gives it a fresh pilot on the same task). No field
// but its child: the leg is derived from the hold on every update, so a hand-emptied hold changes it at once.
public class HaulState : BaseState
{
    public HaulState(Agent agent) : base(agent)
    {
        var move = new MoveToOrbitState(agent);
        HaulOrderData Order() => _agent.Task is HaulingTask task ? HaulRun.OrderOf(_agent.ItemManager, task.Order) : null;
        OrbitalEntity Station() => HaulRun.Station(_agent.Ship.Zone, move.Orbit);
        bool Callable()
        {
            var order = Order();
            var station = order == null ? null : Station();
            return station != null && move.Distance < _agent.Settings.DockingDistance && HaulRun.CanCall(_agent.Ship, station, order);
        }

        Transitions.Add(new StateTransition(move, () => Order() != null, () => move.Orbit = HaulRun.Leg(_agent.Ship, Order())));
        // A changed leg re-enters this state, which points the child at it; a callable station commands the call.
        move.Transitions.Add(new StateTransition(this, () => Order() is HaulOrderData order && !HaulRun.Leg(_agent.Ship, order).Equals(move.Orbit), null));
        move.Transitions.Add(new StateTransition(this, Callable, () => _agent.Ship.Command.Call(Station())));
    }
}
