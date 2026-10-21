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

    // Two Longinus 200 apart, facing each other, the bare player far away. Piloted ones get the zone's pilot.
    private (Zone arena, Ship east, Ship west) SeamPair(bool piloted, Action<ScenarioStage, Ship, Ship> more = null)
    {
        Ship east = null, west = null;
        var (_, arena, _, failures) = Launch(new Scripted(false, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(-50000, -50000));
            east = stage.Place(EvLonginus(stage), float2(100, 0), facing: float2(-1, 0), piloted: piloted) as Ship;
            west = stage.Place(EvLonginus(stage), float2(-100, 0), facing: float2(1, 0), piloted: piloted) as Ship;
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
