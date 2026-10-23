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

// The player's target presses, each answered by the entity's own chooser (Entity.TargetNext and the rest) when applied.
public enum TargetVerb
{
    None,
    UnderReticle,
    NearestEnemy,
    Next,
    Previous
}

// The one input an entity's pilot gives it (its agent, the player, a scripted pilot): only own-ship vectors and handles the
// pilot was given (ruling observation-sensors-only). Commands are per entity, never per Flight or side: a side may be one
// ship (ruling brain-one-per-side-r2). PilotApplier.Apply is the only writer of the entity's fields from it; the pilot
// also reads back its own pending decisions this step (Designated, Pulls), because the entity still holds last step's.
// Held fields persist until the pilot replaces them, so a pilot deciding below the sim rate keeps flying (ruling
// lower-game-sim-rate); null leaves the field to whoever wrote it last. One-shots apply once, in order, and are cleared.
public sealed class PilotCommand
{
    public TurnCommand? Turn;
    public float2? Movement;
    public float3? Aim;
    public float? Tractor;

    internal readonly List<TargetRef> Designations = new List<TargetRef>();
    internal readonly List<EquippedItem> Selections = new List<EquippedItem>();
    internal readonly List<string> Hails = new List<string>();
    internal readonly List<ConsumableItemData> Uses = new List<ConsumableItemData>();
    internal readonly List<(ActivatedBehavior Behavior, bool Pull)> Triggers = new List<(ActivatedBehavior, bool)>();
    internal readonly List<(Entity Other, bool? Hostile, bool Toggle)> Iffs = new List<(Entity, bool?, bool)>();
    internal TargetVerb TargetVerb;
    // The step's give verb: a Give, or a stop when its Receiver is null. The last one in a step wins.
    internal (Entity Receiver, ItemInstance Item, int Quantity)? GiveVerb;
    internal int AimCycles;
    internal int ShutdownOverrideToggles, HeatsinkToggles, ShieldToggles, Pings;

    public void Designate(TargetRef target) => Designations.Add(target);
    public void SelectItem(EquippedItem item) => Selections.Add(item);
    public void Hail(string message) => Hails.Add(message);
    public void Use(ConsumableItemData consumable) => Uses.Add(consumable);
    public void Pull(ActivatedBehavior behavior) => Triggers.Add((behavior, true));
    public void Release(ActivatedBehavior behavior) => Triggers.Add((behavior, false));
    // The last verb in a step wins.
    public void Verb(TargetVerb verb) => TargetVerb = verb;
    public void CycleAim() => AimCycles++;
    public void Iff(Entity other, bool? hostile) => Iffs.Add((other, hostile, false));
    // A press that flips the stance on the entity's target as both stand when applied, after the step's target verbs and in
    // order with Iff, so two in a step cancel. With no target then, it does nothing.
    public void ToggleStance() => Iffs.Add((null, null, true));
    // The entity's own switches, pressed: each press flips its switch when applied, so two in a step cancel.
    public void ToggleOverrideShutdown() => ShutdownOverrideToggles++;
    public void ToggleHeatsinks() => HeatsinkToggles++;
    public void ToggleShield() => ShieldToggles++;
    public void Ping() => Pings++;
    // Offers goods from this entity's hold to a ship alongside (ruling transfer-is-give-only): the quantity of a commodity,
    // or the one crafted instance. It replaces any give under way. A give of nothing, or of less than one, is ignored, and
    // so is one to oneself when applied. The last give or stop in a step wins.
    public void Give(Entity receiver, ItemInstance item, int quantity)
    {
        if (receiver == null || item == null) return;
        var count = item is CraftedItemInstance ? 1 : quantity;
        if (count < 1) return;
        GiveVerb = (receiver, item, count);
    }
    public void StopGiving() => GiveVerb = (null, null, 0);

    // The pilot's own pending decisions, which it reads back instead of the ship (that is last step's applied state).
    public TargetRef? Designated => Designations.Count > 0 ? Designations[Designations.Count - 1] : (TargetRef?) null;

    // Whether this pilot has commanded a trigger, pull or release, for `behavior` this step.
    public bool Commands(ActivatedBehavior behavior)
    {
        foreach (var trigger in Triggers)
            if (ReferenceEquals(trigger.Behavior, behavior)) return true;
        return false;
    }

    // Whether the last trigger this pilot commanded for `behavior` this step is a pull.
    public bool Pulls(ActivatedBehavior behavior)
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
        Iffs.Clear();
        TargetVerb = TargetVerb.None;
        GiveVerb = null;
        AimCycles = 0;
        ShutdownOverrideToggles = HeatsinkToggles = ShieldToggles = Pings = 0;
    }
}

// The one writer of an entity's pilot-controlled fields, called only from Zone.Update. The order is a rule: the target
// lands (a designation, then a chooser verb) before its item is selected or the aim cycled, and the aim before the
// triggers, so a pilot fires along the aim it commands this step.
public static class PilotApplier
{
    public static void Apply(Entity entity)
    {
        var command = entity.Command;
        foreach (var target in command.Designations)
            if (!entity.Target.Value.Equals(target)) entity.SetTarget(target);
        switch (command.TargetVerb)
        {
            case TargetVerb.UnderReticle: entity.TargetUnderReticle(); break;
            case TargetVerb.NearestEnemy: entity.TargetNearestEnemy(); break;
            case TargetVerb.Next: entity.TargetNext(); break;
            case TargetVerb.Previous: entity.TargetPrevious(); break;
        }
        foreach (var item in command.Selections) entity.TrySelectTargetItem(item);
        for (var i = 0; i < command.AimCycles; i++) entity.CycleAim();
        foreach (var (other, hostile, toggle) in command.Iffs)
        {
            if (!toggle) entity.SetIff(other, hostile);
            else if (entity.Target.Value.Entity is Entity target) entity.SetIff(target, !entity.IsHostileTo(target));
        }
        for (var i = 0; i < command.ShutdownOverrideToggles; i++) entity.OverrideShutdown = !entity.OverrideShutdown;
        for (var i = 0; i < command.HeatsinkToggles; i++) entity.HeatsinksEnabled = !entity.HeatsinksEnabled;
        if (entity.Shield != null)
            for (var i = 0; i < command.ShieldToggles; i++) entity.Shield.Item.Enabled.Value = !entity.Shield.Item.Enabled.Value;
        for (var i = 0; i < command.Pings; i++) entity.Sensor?.Ping();
        foreach (var hail in command.Hails) entity.SetMessage(hail);
        foreach (var consumable in command.Uses) entity.TryActivateConsumable(consumable);
        if (command.GiveVerb is { } give)
        {
            if (give.Receiver == null) CargoTransfer.End(entity, GiveEnd.Stopped);
            else if (!ReferenceEquals(give.Receiver, entity)) CargoTransfer.Begin(entity, new GiveOrder(give.Receiver, give.Item, give.Quantity));
        }
        if (command.Aim is float3 aim) entity.Aim = aim;
        foreach (var (behavior, pull) in command.Triggers)
            if (pull) behavior.Activate();
            else behavior.Deactivate();
        if (entity is Ship ship)
        {
            if (command.Turn is TurnCommand turn) ship.Turn = turn.Resolve(ship);
            if (command.Movement is float2 movement) ship.MovementDirection = movement;
        }
        if (command.Tractor is float tractor) entity.TractorCommand = tractor;
        command.ClearOneShots();
    }
}
