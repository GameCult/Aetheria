/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CultMath;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The command seam (aetheria-arena, cut command-seam-agents): every pilot writes only its entity's PilotCommand, and
// PilotApplier, in Zone.Update, is the one writer of the control fields. Held fields hold across steps (ruling
// lower-game-sim-rate), one-shots apply once, a command is per entity (ruling brain-one-per-side-r2), and the apply order
// is pinned here because the two-AI digest cannot see it.
public sealed partial class RunStartTests
{
    private const float SeamDt = 1f / 60f;

    // Two Longinus 2 * half apart (200 by default), facing each other, the bare player far away. Piloted ones get the
    // zone's pilot.
    private (Zone arena, Ship east, Ship west) SeamPair(bool piloted, Action<ScenarioStage, Ship, Ship> more = null, float half = 100)
    {
        Ship east = null, west = null;
        var (_, arena, _, failures) = Launch(new Scripted(false, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(-50000, -50000));
            east = stage.Place(EvLonginus(stage), float2(half, 0), facing: float2(-1, 0), piloted: piloted) as Ship;
            west = stage.Place(EvLonginus(stage), float2(-half, 0), facing: float2(1, 0), piloted: piloted) as Ship;
            more?.Invoke(stage, east, west);
        }));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        return (arena, east, west);
    }

    private static object SeamState(Agent agent) =>
        typeof(Agent).GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(agent);

    private static InstantWeapon SeamGun(Ship ship) =>
        Assert.IsAssignableFrom<InstantWeapon>(ship.Weapons.First(w => w.Item.Data.Name == "Cell Blaster"));

    [Fact]
    public void AnAgentWritesOnlyItsCommand()
    {
        var (arena, east, west) = SeamPair(true);
        east.SetIff(west, true);
        west.SetIff(east, true);
        var agent = arena.Agents.Single(a => a.Ship == east);
        for (var step = 0; step < 900 && !(SeamState(agent) is CombatState); step++) arena.Update(SeamDt);
        Assert.IsType<CombatState>(SeamState(agent));

        // Sentinels the decision would overwrite if it wrote the ship.
        east.Turn = 7f;
        east.MovementDirection = float2(9, 9);
        east.Aim = float3(0, 1, 0);
        var target = east.Target.Value;
        var aimed = east.AimedAt;
        var firing = east.Weapons.Select(w => w.Firing).ToArray();
        var consumables = east.CargoBays.Sum(bay => bay.Cargo.Count);

        agent.Update(SeamDt);

        Assert.Equal(7f, east.Turn);
        Assert.Equal(float2(9, 9), east.MovementDirection);
        Assert.Equal(float3(0, 1, 0), east.Aim);
        Assert.Equal(target, east.Target.Value);
        Assert.Equal(aimed.Item, east.AimedAt.Item);
        Assert.Equal(aimed.Mount, east.AimedAt.Mount);
        Assert.Equal(firing, east.Weapons.Select(w => w.Firing).ToArray());
        Assert.Equal(consumables, east.CargoBays.Sum(bay => bay.Cargo.Count));
        Assert.NotNull(east.Command.Turn);
        Assert.NotNull(east.Command.Movement);
        Assert.NotNull(east.Command.Aim);

        PilotApplier.Apply(east);

        Assert.NotEqual(7f, east.Turn);
        Assert.NotEqual(float2(9, 9), east.MovementDirection);
        Assert.Equal(east.Command.Aim.Value, east.Aim);
    }

    [Fact]
    public void AHeldCommandHoldsAcrossSteps()
    {
        var (arena, east, _) = SeamPair(false);
        east.Command.Turn = TurnCommand.OfRate(.5f);
        east.Command.Movement = float2(0, 1);
        for (var step = 0; step < 10; step++)
        {
            // A disturbance between steps: the held command is applied again at every step, not once.
            east.Turn = 0;
            east.MovementDirection = float2.zero;
            arena.Update(SeamDt);
            Assert.Equal(.5f, east.Turn);
            Assert.Equal(float2(0, 1), east.MovementDirection);
        }

        east.Command.Turn = TurnCommand.OfRate(-.25f);
        arena.Update(SeamDt);
        Assert.Equal(-.25f, east.Turn);
        Assert.Equal(float2(0, 1), east.MovementDirection);
    }

    [Fact]
    public void TowardIsResolvedEachStep()
    {
        var (arena, east, _) = SeamPair(false);
        var heading = float2(0, 1); // a quarter turn from its facing
        east.Command.Turn = TurnCommand.Toward(heading);
        var turns = new List<float>();
        for (var step = 0; step < 1200; step++)
        {
            // The applier runs before the hull moves this step, so it resolves on the facing the step starts with.
            var expected = Steering.Toward(east, heading);
            arena.Update(SeamDt);
            Assert.Equal(expected, east.Turn);
            turns.Add(east.Turn);
        }
        Assert.True(turns.Distinct().Count() > 2, "the resolved turn changes as the hull turns");
        Assert.NotEqual(0f, turns[0]);
        Assert.Contains(0f, turns.Skip(1));
    }

    [Fact]
    public void OneShotsApplyOnce()
    {
        // A stackable design: the second step's activation would succeed if the applier kept the Use.
        var design = _cache.GetAll<ConsumableItemData>().OrderBy(c => c.Name).First();
        design.Stackable = true;
        var (arena, east, west) = SeamPair(false, (stage, e, _) => stage.Cargo(e, design.Name, design.Name));
        int Carried() => east.CargoBays.Sum(bay => bay.Cargo.Keys.Count(i => i is ConsumableItem));
        Assert.Equal(2, Carried());

        east.Command.Use(design);
        arena.Update(SeamDt);
        Assert.Equal(1, Carried());
        arena.Update(SeamDt);
        Assert.Equal(1, Carried());

        east.Command.Designate(west);
        arena.Update(SeamDt);
        Assert.Same(west, east.Target.Value.Entity);
        // The entity's own rule clears it (a fixture stands in for the target-loss clear); the designation is not replayed.
        east.SetTarget(TargetRef.None);
        arena.Update(SeamDt);
        Assert.True(east.Target.Value.IsNone);
    }

    // A probe pilot state: it commands a turn rate and records what another ship's applied turn was when it decided.
    private sealed class SeamProbeState : BaseState
    {
        private readonly float _rate;
        private readonly Ship _watched;
        public readonly List<float> Seen = new List<float>();
        public SeamProbeState(Agent agent, float rate, Ship watched) : base(agent)
        {
            _rate = rate;
            _watched = watched;
        }

        public override void Update(float delta)
        {
            if (_watched != null) Seen.Add(_watched.Turn);
            _agent.Ship.Command.Turn = TurnCommand.OfRate(_rate);
        }
    }

    // Each agent's command is applied right after it decides, so the next agent sees it applied, as it saw the direct
    // writes this seam replaced. Breaks if the agents' commands are applied only after every agent has decided.
    [Fact]
    public void AnAgentsCommandLandsBeforeTheNextAgentDecides()
    {
        var (arena, east, west) = SeamPair(false);
        var first = new Agent(east, FactionDoctrine.Default(east.ItemManager.GameplaySettings).Combatant);
        var second = new Agent(west, FactionDoctrine.Default(west.ItemManager.GameplaySettings).Combatant);
        first.Transition(new SeamProbeState(first, .7f, null));
        var probe = new SeamProbeState(second, .1f, east);
        second.Transition(probe);
        arena.Agents.Add(first);
        arena.Agents.Add(second);

        arena.Update(SeamDt);

        Assert.Equal(new[] { .7f }, probe.Seen);
        Assert.Equal(.1f, west.Turn);
    }

    [Fact]
    public void AnEmptyCommandWritesNothing()
    {
        var (arena, east, _) = SeamPair(false);
        east.Turn = .3f;
        east.Aim = float3(0, 0, 1);
        arena.Update(SeamDt);
        Assert.Equal(.3f, east.Turn);
        Assert.Equal(float3(0, 0, 1), east.Aim);
    }

    [Fact]
    public void ALoneShipSideIsCommandedWithoutAFlight()
    {
        var (arena, east, west) = SeamPair(false);
        east.SetIff(west, true);
        west.SetIff(east, true);
        Assert.Empty(arena.Flights);
        Assert.Empty(arena.Agents);
        east.Command.Turn = TurnCommand.OfRate(.2f);
        west.Command.Turn = TurnCommand.OfRate(-.4f);
        arena.Update(SeamDt);
        Assert.Equal(.2f, east.Turn);
        Assert.Equal(-.4f, west.Turn);
    }

    [Fact]
    public void DesignationLandsBeforeItemSelection()
    {
        var (_, east, west) = SeamPair(false);
        var item = SeamGun(west).Item;
        east.RecordReveal(west, item, SignatureChannel.Emission, west.ClaimOf(item));
        Assert.True(FireControl.IsRevealed(east, item));

        east.Command.Designate(west);
        east.Command.SelectItem(item);
        PilotApplier.Apply(east);

        Assert.Same(west, east.Target.Value.Entity);
        Assert.Same(item, east.ResolvedAim.Item);
    }

    [Fact]
    public void TriggersFireAlongTheCommandedAim()
    {
        var (arena, east, _) = SeamPair(false);
        for (var step = 0; step < 60; step++) arena.Update(SeamDt); // the gun comes online and off cooldown
        var gun = SeamGun(east);
        var behind = float3(1, 0, 0);
        var ahead = float3(-1, 0, 0);
        east.Aim = behind;
        Assert.False(FireControl.ArcPermitsFire(gun, east), "the previous aim is out of the gun's arc");
        Assert.True(gun.CanFire);

        var shots = new List<PendingShot>();
        gun.OnFire += id =>
        {
            Assert.True(arena.TryGetShot(id, out var shot));
            shots.Add(shot);
        };
        east.Command.Aim = ahead;
        east.Command.Pull(gun);
        arena.Update(SeamDt);

        Assert.NotEmpty(shots);
        var along = FireControl.AimDirection(gun.Item, east);
        Assert.True(length(shots[0].TravelDirection - along) < 1e-4f, $"the shot flies {shots[0].TravelDirection}, the commanded aim {along}");
        Assert.True(dot(shots[0].TravelDirection, behind.xz) < 0, "not along the previous aim");
    }

    // A pull lands once and a release lands once: the trigger is let go at the step it is released, not held on.
    [Fact]
    public void AReleaseDeactivatesAtTheStep()
    {
        var (arena, east, _) = SeamPair(false);
        for (var step = 0; step < 60; step++) arena.Update(SeamDt); // the gun comes online and off cooldown
        var gun = SeamGun(east);

        east.Command.Pull(gun);
        PilotApplier.Apply(east);
        Assert.True(gun.Firing);
        Assert.Empty(east.Command.Triggers);

        east.Command.Release(gun);
        PilotApplier.Apply(east);
        Assert.False(gun.Firing);
        PilotApplier.Apply(east);
        Assert.False(gun.Firing);
    }

    // Combat lets go of a weapon it will not fire only if that weapon is firing: of a group's two guns, the one firing is
    // released and the idle one gets no trigger at all. Breaks if Combat's release guard or its Release is lost.
    [Fact]
    public void CombatReleasesOnlyAFiringWeaponItWillNotFire()
    {
        var (arena, east, west) = SeamPair(true);
        east.SetIff(west, true);
        west.SetIff(east, true);
        var agent = arena.Agents.Single(a => a.Ship == east);
        agent.Doctrine.MinHitProbability = 2; // no shot clears it, so Combat fires no unfused gun
        for (var step = 0; step < 900 && !(SeamState(agent) is CombatState); step++) arena.Update(SeamDt);
        Assert.IsType<CombatState>(SeamState(agent));
        var guns = east.WeaponGroups.Single(g => g.weapons.Contains(SeamGun(east))).weapons.ToArray();
        Assert.Equal(2, guns.Length);
        Assert.All(guns, g => Assert.False(g.Firing));

        guns[0].Activate(); // the pilot's earlier pull, still held
        Assert.True(guns[0].Firing);
        agent.Update(SeamDt);

        var triggers = east.Command.Triggers.Where(t => guns.Any(g => ReferenceEquals(g, t.Behavior))).ToArray();
        var (released, pull) = Assert.Single(triggers);
        Assert.Same(guns[0], released);
        Assert.False(pull);
        PilotApplier.Apply(east);
        Assert.False(guns[0].Firing);
        Assert.False(guns[1].Firing);
    }

    // Combat pulls a locking launcher once it can fire, beside the groups it chooses (a lock adds no group damage, so
    // this pull is its only one). Breaks if Combat's lock pull is lost.
    [Fact]
    public void CombatPullsALockThatCanFire()
    {
        var (arena, east, west) = SeamPair(true);
        east.SetIff(west, true);
        west.SetIff(east, true);
        var agent = arena.Agents.Single(a => a.Ship == east);
        var locks = east.Weapons.OfType<LockWeapon>().ToArray();
        Assert.NotEmpty(locks);
        agent.Doctrine.MinHitProbability = 2; // its guns hold fire, so its aim settles and the lock completes
        var step = 0;
        for (; step < 3000 && !(SeamState(agent) is CombatState && locks.Any(l => l.IsLocked)); step++) arena.Update(SeamDt);
        Assert.True(locks.Any(l => l.IsLocked), $"locked by step {step}: {string.Join(",", locks.Select(l => l.Lock))}");
        // The pair fights inside the launchers' minimum range: carry west out along east's aim, and let east's own update
        // (no pilot) see the new range.
        west.Position = east.Position + 600 * normalize(float3(east.Aim.x, 0, east.Aim.z));
        east.Update(SeamDt);
        Assert.IsType<CombatState>(SeamState(agent));
        var ready = locks.Where(l => l.CanFire).ToArray();
        Assert.True(ready.Length > 0, $"range {east.TargetRange}, min {locks[0].MinRange}, locked {string.Join(",", locks.Select(l => l.Lock))}");

        agent.Update(SeamDt);

        foreach (var launcher in ready) Assert.True(east.Command.Pulls(launcher), $"{launcher.Item.Data.Name} is pulled");
    }

    // Combat lets go of a lock it pulled this step (beside the groups) when the chosen group's own decision refuses it. An
    // unfused lock is priced on its hit probability, which no shot clears at MinHitProbability 2, and it was never firing,
    // so only the pending pull (Command.Pulls) tells Combat to release it. Breaks if Combat's release guard reads Firing
    // alone (Combat.cs).
    [Fact]
    public void CombatReleasesALockItPulledButWillNotFire()
    {
        var (arena, east, west) = SeamPair(true);
        east.SetIff(west, true);
        west.SetIff(east, true);
        var agent = arena.Agents.Single(a => a.Ship == east);
        var locks = east.Weapons.OfType<LockWeapon>().ToArray();
        Assert.NotEmpty(locks);
        agent.Doctrine.MinHitProbability = 2; // no unfused shot clears it
        var step = 0;
        for (; step < 3000 && !(SeamState(agent) is CombatState && locks.Any(l => l.IsLocked)); step++) arena.Update(SeamDt);
        var launcher = locks.FirstOrDefault(l => l.IsLocked);
        Assert.True(launcher != null, $"locked by step {step}");
        var gunRange = east.Weapons.Where(w => !(w is LockWeapon)).Max(w => w.Range);
        var distance = launcher.MinRange + 20;
        Assert.True(distance < gunRange, $"fixture: the guns reach past the launcher's minimum range ({launcher.MinRange} vs {gunRange})");
        west.Position = east.Position + distance * normalize(float3(east.Aim.x, 0, east.Aim.z));
        east.Update(SeamDt);
        Assert.IsType<CombatState>(SeamState(agent));
        Assert.True(launcher.CanFire && !launcher.Firing, $"fixture: ready and idle (range {east.TargetRange}, lock {launcher.Lock})");

        // Unfused, and in every group, so the chosen group decides it too.
        var data = Assert.IsAssignableFrom<WeaponItemData>(launcher.Item.Data);
        var radius = data.BlastRadius;
        try
        {
            data.BlastRadius = 0;
            foreach (var group in east.WeaponGroups)
                if (!group.weapons.Contains(launcher)) group.weapons.Add(launcher);

            agent.Update(SeamDt);

            var mine = east.Command.Triggers.Where(t => ReferenceEquals(t.Behavior, launcher)).Select(t => t.Pull).ToArray();
            Assert.True(mine.Contains(true), $"Combat pulls the ready lock: [{string.Join(",", mine)}]");
            Assert.False(east.Command.Pulls(launcher), $"and lets go of it when the group's decision refuses it: [{string.Join(",", mine)}]");
        }
        finally
        {
            data.BlastRadius = radius;
        }
    }

    // Combat prices each shot on the aim it commands this step, not the applied aim (last step's). With the applied aim
    // cleared and MinHitProbability between the two prices, the gun's one trigger follows the commanded price. Breaks if
    // Combat drops its aim from FireControl.AgentFires (Combat.cs).
    [Fact]
    public void CombatPricesFireOnTheCommandedAim()
    {
        var (arena, east, west) = SeamPair(true);
        east.SetIff(west, true);
        west.SetIff(east, true);
        var agent = arena.Agents.Single(a => a.Ship == east);
        for (var step = 0; step < 900 && !(SeamState(agent) is CombatState); step++) arena.Update(SeamDt);
        Assert.IsType<CombatState>(SeamState(agent));
        var revealed = SeamGun(west).Item;
        east.EntityInfoGathered[west] = 1;
        east.RecordReveal(west, revealed, SignatureChannel.Emission, west.ClaimOf(revealed));

        agent.Update(SeamDt); // learn the item Combat aims at
        var commanded = east.Command.Selections.LastOrDefault();
        Assert.NotNull(commanded);
        east.Command.ClearOneShots();
        Assert.True(east.TrySelectTargetItem(null));
        var gun = SeamGun(east);
        var pCommanded = FireControl.HitProbability(gun, east, west, AimTarget.OfItem(commanded));
        var pApplied = FireControl.HitProbability(gun, east, west);
        Assert.True(abs(pCommanded - pApplied) > 1e-4f, $"fixture: the two aims price apart ({pCommanded} vs {pApplied})");
        agent.Doctrine.MinHitProbability = (pCommanded + pApplied) / 2;
        var fires = pCommanded > pApplied;
        if (!fires) gun.Activate(); // held, so Combat must let go of it

        agent.Update(SeamDt);

        var (_, pull) = Assert.Single(east.Command.Triggers.Where(t => ReferenceEquals(t.Behavior, gun)));
        Assert.Equal(fires, pull);
    }

    // Pulls answers the pilot's last trigger for that behaviour this step, and nothing commanded is no pull.
    [Fact]
    public void PullsReadsTheLastTriggerForThatBehaviour()
    {
        var (_, east, west) = SeamPair(false);
        var gun = SeamGun(east);
        var other = SeamGun(west);
        Assert.False(east.Command.Pulls(gun));

        east.Command.Pull(gun);
        Assert.True(east.Command.Pulls(gun));
        Assert.False(east.Command.Pulls(other));
        east.Command.Pull(other);
        Assert.True(east.Command.Pulls(gun));

        east.Command.Release(gun);
        Assert.False(east.Command.Pulls(gun));
        Assert.True(east.Command.Pulls(other));
    }

    // The step a Minion's target is lost and its Flight designates another (the target-loss clear, then the Flight's
    // designation), the Minion stays in combat and aims at the other that step, as it did when SetTarget landed before it
    // decided. Breaks if the Minion's transition or Combat read only the applied target (Minion.cs, Combat.cs).
    [Fact]
    public void TheRetargetStepEngagesTheDesignatedOther()
    {
        Ship third = null;
        var (arena, east, west) = SeamPair(true, (stage, e, w) =>
            third = stage.Place(EvLonginus(stage), float2(350, 0), facing: float2(-1, 0)) as Ship);
        east.SetIff(west, true);
        west.SetIff(east, true);
        east.SetIff(third, true);
        var agent = arena.Agents.Single(a => a.Ship == east);
        for (var step = 0; step < 900 && !(SeamState(agent) is CombatState); step++) arena.Update(SeamDt);
        Assert.IsType<CombatState>(SeamState(agent));
        Assert.Same(west, east.Target.Value.Entity);

        // East is staged knowing the third ship, as an Approach follower knows its anchor: a sensor track by the sensors' own path.
        east.EntityInfoGathered[third] = (east.ItemManager.GameplaySettings.TargetDetectionInfoThreshold + 1f) / 2f;
        east.SetTarget(TargetRef.None);
        east.Command.Designate(new TargetRef(third));
        agent.Update(SeamDt);

        Assert.IsType<CombatState>(SeamState(agent));
        var aim = normalize(east.Command.Aim.Value.xz);
        var toThird = dot(aim, normalize(third.Position.xz - east.Position.xz));
        var toWest = dot(aim, normalize(west.Position.xz - east.Position.xz));
        Assert.True(toThird > toWest + .2f, $"aims at the designated target: toThird {toThird} toWest {toWest}");
    }

    [Fact]
    public void AgentFiresWithTheCommandedAimItem()
    {
        var (arena, east, west) = SeamPair(false);
        for (var step = 0; step < 60; step++) arena.Update(SeamDt); // the guns come online and evaluate their stats
        var gun = SeamGun(east);
        var item = SeamGun(west).Item;
        east.EntityInfoGathered[west] = 1;
        east.RecordReveal(west, item, SignatureChannel.Emission, west.ClaimOf(item));
        east.SetTarget(west);

        var refusesFor = FireControl.Refuses(gun, east, west);
        var commanded = FireControl.HitProbability(gun, east, west, AimTarget.OfItem(item));
        var firesCommanded = FireControl.AgentFires(gun, east, west, commanded, AimTarget.OfItem(item));
        Assert.Equal(AimTarget.None.Item, east.ResolvedAim.Item);
        Assert.True(east.TrySelectTargetItem(item));

        Assert.True(commanded > 0f, "the shot is priced");
        Assert.Equal(commanded, FireControl.HitProbability(gun, east, west));
        Assert.Equal(firesCommanded, FireControl.AgentFires(gun, east, west, commanded));
        Assert.True(firesCommanded);
        Assert.Equal(refusesFor, FireControl.Refuses(gun, east));
    }
}
