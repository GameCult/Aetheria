/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The command seam, player half (aetheria-arena, cut command-seam-player): the player's input writes only its entity's
// PilotCommand, as the agents do, and PilotApplier lands it at the next zone step. Its target presses are verbs the entity's
// chooser answers, its stance and aim-cycle presses are one-shots, its helm and tractor are held, and its triggers are
// edges: a press is one pull and a let-go one release.
public sealed partial class RunStartTests
{
    // East, west 200 away and a third 250 away, all seen by east once the zone has stepped.
    private (Zone arena, Ship east, Ship west, Ship third) PlayerSeamTrio()
    {
        Ship third = null;
        var (arena, east, west) = SeamPair(false, (stage, e, w) =>
            third = stage.Place(EvLonginus(stage), float2(350, 0), facing: float2(-1, 0)) as Ship);
        for (var step = 0; step < 600 && !(east.VisibleEntities.Contains(west) && east.VisibleEntities.Contains(third)); step++) arena.Update(SeamDt);
        Assert.Contains(west, east.VisibleEntities);
        Assert.Contains(third, east.VisibleEntities);
        return (arena, east, west, third);
    }

    [Fact]
    public void HelmDemandIsAnIntent()
    {
        var (arena, east, _) = SeamPair(false);
        var held = new HelmInput { Held = true };
        var heading = float2(0, 1); // a quarter turn from its facing
        Assert.Equal(Steering.Toward(east, heading), held.Demand(heading, .5f).Resolve(east));
        Assert.Equal(.5f, new HelmInput().Demand(heading, .5f).Resolve(east));
        Assert.Equal(-1f, new HelmInput().Demand(heading, -4f).Resolve(east));

        // One face-aim demand, held: it re-resolves against the hull at every step as the hull comes round.
        east.Command.Turn = held.Demand(heading, 0);
        var turns = new List<float>();
        for (var step = 0; step < 600; step++)
        {
            var expected = Steering.Toward(east, heading);
            arena.Update(SeamDt);
            Assert.Equal(expected, east.Turn);
            turns.Add(east.Turn);
        }
        Assert.True(turns.Distinct().Count() > 2, "the resolved turn changes as the hull turns");
    }

    [Fact]
    public void TargetVerbsApplyOnceAtTheStep()
    {
        var (arena, east, west, third) = PlayerSeamTrio();
        Assert.True(east.Target.Value.IsNone);

        // The last verb in a step wins: Previous would pick the farthest (third), Next picks the nearest.
        east.Command.Verb(TargetVerb.Previous);
        east.Command.Verb(TargetVerb.Next);
        Assert.True(east.Target.Value.IsNone, "nothing lands before the step");
        arena.Update(SeamDt);
        Assert.Same(west, east.Target.Value.Entity);
        arena.Update(SeamDt);
        Assert.Same(west, east.Target.Value.Entity);

        east.Command.Verb(TargetVerb.Next);
        arena.Update(SeamDt);
        Assert.Same(third, east.Target.Value.Entity);

        east.Command.Verb(TargetVerb.Previous);
        arena.Update(SeamDt);
        Assert.Same(west, east.Target.Value.Entity);

        // Enemies only: the farther hostile, not the nearer neutral.
        east.SetIff(third, true);
        east.Command.Verb(TargetVerb.NearestEnemy);
        arena.Update(SeamDt);
        Assert.Same(third, east.Target.Value.Entity);

        // The reticle picks along the aim, and picking the held target again clears it (no other verb answers None here).
        east.Command.Verb(TargetVerb.Previous);
        arena.Update(SeamDt);
        Assert.Same(west, east.Target.Value.Entity);
        east.Command.Aim = float3(-1, 0, 0);
        east.Command.Verb(TargetVerb.UnderReticle);
        arena.Update(SeamDt);
        Assert.True(east.Target.Value.IsNone);
        arena.Update(SeamDt);
        Assert.True(east.Target.Value.IsNone, "the reticle press is not replayed");
    }

    [Fact]
    public void IffAndCycleAimAreOneShots()
    {
        var (arena, east, west, _) = PlayerSeamTrio();
        Assert.False(east.IsHostileTo(west));
        east.Command.Iff(west, true);
        Assert.False(east.IsHostileTo(west), "nothing lands before the step");
        arena.Update(SeamDt);
        Assert.True(east.IsHostileTo(west));
        // Not replayed: a stance set after the step stays.
        east.SetIff(west, false);
        arena.Update(SeamDt);
        Assert.False(east.IsHostileTo(west));
        east.Command.Iff(west, true);
        east.Command.Iff(west, null);
        arena.Update(SeamDt);
        Assert.False(east.IsHostileTo(west), "the later stance in a step lands last");

        var first = SeamGun(west).Item;
        var second = west.Weapons.Select(w => w.Item).First(i => i != first && i.Data.Name == first.Data.Name);
        east.RecordReveal(west, first, SignatureChannel.Emission, west.ClaimOf(first));
        east.RecordReveal(west, second, SignatureChannel.Emission, west.ClaimOf(second));
        east.Command.Verb(TargetVerb.Next);
        arena.Update(SeamDt);
        Assert.Same(west, east.Target.Value.Entity);
        var before = east.ResolvedAim;

        east.Command.CycleAim();
        Assert.Equal(before.Item, east.ResolvedAim.Item);
        arena.Update(SeamDt);
        var once = east.ResolvedAim;
        Assert.True(once.Item != before.Item || once.Mount != before.Mount, "the cycle advanced the aim");
        arena.Update(SeamDt);
        Assert.Equal(once.Item, east.ResolvedAim.Item);
        Assert.Equal(once.Mount, east.ResolvedAim.Mount);
    }

    // A trigger that counts its edges: a semi-auto fires once per Activate.
    private sealed class SeamTrigger : IActivatedBehavior
    {
        public int Pulled, Released;
        public void Activate() => Pulled++;
        public void Deactivate() => Released++;
    }

    // A press is one pull at the next step and the button held after it pulls nothing more, so a semi-auto fires once;
    // the let-go is one release.
    [Fact]
    public void PlayerTriggerIsAnEdge()
    {
        var (arena, east, _) = SeamPair(false);
        var trigger = new SeamTrigger();
        east.Command.Pull(trigger);
        Assert.Equal(0, trigger.Pulled);
        for (var step = 0; step < 10; step++) arena.Update(SeamDt);
        Assert.Equal((1, 0), (trigger.Pulled, trigger.Released));

        east.Command.Release(trigger);
        for (var step = 0; step < 10; step++) arena.Update(SeamDt);
        Assert.Equal((1, 1), (trigger.Pulled, trigger.Released));
    }

    [Fact]
    public void TractorIsHeld()
    {
        var (arena, east, _) = SeamPair(false);
        east.Command.Tractor = 1;
        for (var step = 0; step < 5; step++)
        {
            east.TractorCommand = 0; // a disturbance between steps: the held command lands again at every step
            arena.Update(SeamDt);
            Assert.Equal(1f, east.TractorCommand);
        }
        east.Command.Tractor = .25f;
        arena.Update(SeamDt);
        Assert.Equal(.25f, east.TractorCommand);
    }
}
