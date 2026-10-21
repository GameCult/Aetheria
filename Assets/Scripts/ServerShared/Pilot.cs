/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using CultMath;

// A turn demand as a pilot commands it: a rate, or a heading resolved against the hull's facing when it is applied, so a
// held heading keeps steering as the hull turns.
public readonly struct TurnCommand
{
    private readonly bool _toward;
    private readonly float _rate;
    private readonly float2 _heading;

    private TurnCommand(bool toward, float rate, float2 heading)
    {
        _toward = toward;
        _rate = rate;
        _heading = heading;
    }

    public static TurnCommand OfRate(float rate) => new TurnCommand(false, rate, default);
    public static TurnCommand Toward(float2 heading) => new TurnCommand(true, 0, heading);

    public float Resolve(Ship ship) => _toward ? Steering.Toward(ship, _heading) : _rate;
}

// The one input an entity's pilot gives it (its agent, the player, a scripted pilot): only own-ship vectors and handles the
// pilot was given (ruling observation-sensors-only). Commands are per entity, never per Flight or side: a side may be one
// ship (ruling brain-one-per-side-r2). PilotApplier.Apply is its only reader.
// Held fields persist until the pilot replaces them, so a pilot deciding below the sim rate keeps flying (ruling
// lower-game-sim-rate); null leaves the field to whoever wrote it last. One-shots apply once, in order, and are cleared.
public sealed class PilotCommand
{
    public TurnCommand? Turn;
    public float2? Movement;
    public float3? Aim;

    internal readonly List<TargetRef> Designations = new List<TargetRef>();
    internal readonly List<EquippedItem> Selections = new List<EquippedItem>();
    internal readonly List<string> Hails = new List<string>();
    internal readonly List<ConsumableItemData> Uses = new List<ConsumableItemData>();
    internal readonly List<(IActivatedBehavior Behavior, bool Pull)> Triggers = new List<(IActivatedBehavior, bool)>();

    public void Designate(TargetRef target) => Designations.Add(target);
    public void SelectItem(EquippedItem item) => Selections.Add(item);
    public void Hail(string message) => Hails.Add(message);
    public void Use(ConsumableItemData consumable) => Uses.Add(consumable);
    public void Pull(IActivatedBehavior behavior) => Triggers.Add((behavior, true));
    public void Release(IActivatedBehavior behavior) => Triggers.Add((behavior, false));

    // The pilot's own pending decisions, which it reads back instead of the ship (that is last step's applied state).
    public TargetRef? Designated => Designations.Count > 0 ? Designations[Designations.Count - 1] : (TargetRef?) null;

    // Whether the last trigger this pilot commanded for `behavior` this step is a pull.
    public bool Pulls(IActivatedBehavior behavior)
    {
        for (var i = Triggers.Count - 1; i >= 0; i--)
            if (ReferenceEquals(Triggers[i].Behavior, behavior)) return Triggers[i].Pull;
        return false;
    }

    internal void ClearOneShots()
    {
        Designations.Clear();
        Selections.Clear();
        Hails.Clear();
        Uses.Clear();
        Triggers.Clear();
    }
}

// The one writer of an entity's pilot-controlled fields, called only from Zone.Update. The order is a rule: the target
// lands before its item is selected, and the aim before the triggers, so a pilot fires along the aim it commands this
// step, as the player does.
public static class PilotApplier
{
    public static void Apply(Entity entity)
    {
        var command = entity.Command;
        foreach (var target in command.Designations)
            if (!entity.Target.Value.Equals(target)) entity.SetTarget(target);
        foreach (var item in command.Selections) entity.TrySelectTargetItem(item);
        foreach (var hail in command.Hails) entity.SetMessage(hail);
        foreach (var consumable in command.Uses) entity.TryActivateConsumable(consumable);
        if (command.Aim is float3 aim) entity.Aim = aim;
        foreach (var (behavior, pull) in command.Triggers)
            if (pull) behavior.Activate();
            else behavior.Deactivate();
        if (entity is Ship ship)
        {
            if (command.Turn is TurnCommand turn) ship.Turn = turn.Resolve(ship);
            if (command.Movement is float2 movement) ship.MovementDirection = movement;
        }
        command.ClearOneShots();
    }
}
