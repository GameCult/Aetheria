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
    // East, west 200 away and a third about 277 away, both ahead of east and seen by it once the zone has stepped.
    private (Zone arena, Ship east, Ship west, Ship third) PlayerSeamTrio()
    {
        Ship third = null;
        var (arena, east, west) = SeamPair(false, (stage, e, w) =>
            third = stage.Place(EvLonginus(stage), float2(-150, 120), facing: float2(1, 0)) as Ship);
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

        // With nothing held, Previous picks the farthest and Next the nearest.
        east.SetTarget(TargetRef.None);
        east.Command.Verb(TargetVerb.Previous);
        arena.Update(SeamDt);
        Assert.Same(third, east.Target.Value.Entity);

        // Enemies only: the farther hostile, not the nearer neutral (Next, from nothing, would pick the neutral).
        east.SetTarget(TargetRef.None);
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

    private void SeamReveal(Ship east, Ship west, out EquippedItem first, out EquippedItem second)
    {
        first = SeamGun(west).Item;
        var one = first;
        second = west.Weapons.Select(w => w.Item).First(i => i != one && i.Data.Name == one.Data.Name);
        east.RecordReveal(west, first, SignatureChannel.Emission, west.ClaimOf(first));
        east.RecordReveal(west, second, SignatureChannel.Emission, west.ClaimOf(second));
    }

    private static void SeamAimEqual(AimTarget expected, AimTarget actual)
    {
        Assert.Equal(expected.Item, actual.Item);
        Assert.Equal(expected.Mount, actual.Mount);
    }

    private static bool SeamAimDiffers(AimTarget a, AimTarget b) => a.Item != b.Item || a.Mount != b.Mount;

    // Every aim-cycle press in a step cycles: two presses step the aim twice, as two direct cycles did.
    [Fact]
    public void CycleAimCountsEveryPress()
    {
        var (arena, east, west, _) = PlayerSeamTrio();
        SeamReveal(east, west, out var first, out _);
        east.SetTarget(new TargetRef(west));
        Assert.True(east.TrySelectTargetItem(first));
        east.CycleAim();
        var once = east.ResolvedAim;
        east.CycleAim();
        var twice = east.ResolvedAim;
        Assert.True(SeamAimDiffers(once, twice), "fixture: a second cycle moves the aim on");

        Assert.True(east.TrySelectTargetItem(first));
        east.Command.CycleAim();
        east.Command.CycleAim();
        arena.Update(SeamDt);
        SeamAimEqual(twice, east.ResolvedAim);
    }

    // Presses in one step apply in a fixed order, whatever order they came in: the designation, then the verb (answered
    // from the designated target), then the item selection, then the aim cycles (stepping from the verb's target and the
    // selected item). Each case is checked against both orders by direct calls, and fails if the two orders agree.
    [Fact]
    public void SameStepPressesApplyDesignationVerbSelectionCycle()
    {
        var (arena, east, west, third) = PlayerSeamTrio();
        SeamReveal(east, west, out var first, out _);
        void Reset(Entity target)
        {
            east.SetTarget(target == null ? TargetRef.None : new TargetRef(target));
            Assert.True(east.TrySelectTargetItem(null));
        }

        // Next steps on from the designated west (to third), not from nothing (to west).
        Reset(null);
        east.Command.Verb(TargetVerb.Next);
        east.Command.Designate(new TargetRef(west));
        arena.Update(SeamDt);
        Assert.Same(third, east.Target.Value.Entity);

        // The cycle steps the aim on the target the verb chose.
        Reset(null);
        east.TargetNext();
        east.CycleAim();
        var verbThenCycle = east.ResolvedAim;
        Reset(null);
        east.CycleAim();
        east.TargetNext();
        Assert.True(SeamAimDiffers(verbThenCycle, east.ResolvedAim), "fixture: the verb and the cycle do not commute");
        Reset(null);
        east.Command.CycleAim();
        east.Command.Verb(TargetVerb.Next);
        arena.Update(SeamDt);
        SeamAimEqual(verbThenCycle, east.ResolvedAim);

        // The cycle steps on from the selected item.
        Reset(west);
        Assert.True(east.TrySelectTargetItem(first));
        east.CycleAim();
        var selectThenCycle = east.ResolvedAim;
        Reset(west);
        east.CycleAim();
        Assert.True(east.TrySelectTargetItem(first));
        Assert.True(SeamAimDiffers(selectThenCycle, east.ResolvedAim), "fixture: the selection and the cycle do not commute");
        Reset(west);
        east.Command.CycleAim();
        east.Command.SelectItem(first);
        arena.Update(SeamDt);
        SeamAimEqual(selectThenCycle, east.ResolvedAim);
    }

    // A stance press flips the stance on the target as it stands when the press is applied, after the step's target verbs,
    // so two in a step cancel, as two flips in place did before the seam; none is replayed.
    [Fact]
    public void StanceTogglesInAStepCompose()
    {
        var (arena, east, west, third) = PlayerSeamTrio();
        Assert.True(east.Target.Value.IsNone);
        Assert.False(east.IsHostileTo(west));
        // No target is applied yet: the verb in the same step picks the nearest (west), and the press flips it.
        east.Command.Verb(TargetVerb.Next);
        east.Command.ToggleStance();
        Assert.False(east.IsHostileTo(west), "nothing lands before the step");
        arena.Update(SeamDt);
        Assert.Same(west, east.Target.Value.Entity);
        Assert.True(east.IsHostileTo(west), "the press lands on the target the step's verb picked");
        Assert.False(east.IsHostileTo(third));
        east.Command.Iff(west, true);
        arena.Update(SeamDt);
        Assert.True(east.IsHostileTo(west), "a stance set is a set, not a flip");

        east.Command.ToggleStance();
        east.Command.ToggleStance();
        arena.Update(SeamDt);
        Assert.True(east.IsHostileTo(west), "two flips in a step cancel");

        // Next then the press in one step: the press flips the new target, not the one applied when it was pressed.
        east.Command.Verb(TargetVerb.Next);
        east.Command.ToggleStance();
        arena.Update(SeamDt);
        Assert.Same(third, east.Target.Value.Entity);
        Assert.True(east.IsHostileTo(third), "the press flips the new target");
        Assert.True(east.IsHostileTo(west), "the old target is untouched");
        east.Command.Designate(new TargetRef(west));
        arena.Update(SeamDt);

        east.Command.ToggleStance();
        arena.Update(SeamDt);
        Assert.False(east.IsHostileTo(west));
        arena.Update(SeamDt);
        Assert.False(east.IsHostileTo(west), "the press is not replayed");
    }

    // The player's switches are presses on its command too: each flips at the next step, two in a step cancel, and none is
    // replayed. (The Longinus has no shield: the shield switch is pinned on a shielded
    // target in FireControlCut124Tests below, and here its press is a no-op.)
    [Fact]
    public void PlayerSwitchesArePressesAtTheStep()
    {
        var (arena, east, _) = SeamPair(false);
        Assert.Null(east.Shield);
        Assert.NotNull(east.Sensor);
        (bool, bool) Switches() => (east.OverrideShutdown, east.HeatsinksEnabled);
        Assert.Equal((false, true), Switches());

        east.Command.ToggleOverrideShutdown();
        east.Command.ToggleHeatsinks();
        east.Command.ToggleShield();
        Assert.Equal((false, true), Switches());
        arena.Update(SeamDt);
        Assert.Equal((true, false), Switches());
        arena.Update(SeamDt);
        Assert.Equal((true, false), Switches());

        east.Command.ToggleOverrideShutdown();
        east.Command.ToggleOverrideShutdown();
        east.Command.ToggleHeatsinks();
        east.Command.ToggleHeatsinks();
        arena.Update(SeamDt);
        Assert.Equal((true, false), Switches());
    }

    // A trigger that counts its edges: a semi-auto fires once per Activate.
    private sealed class SeamTrigger : ActivatedBehavior
    {
        public int Pulled, Released;
        public SeamTrigger(EquippedItem item) : base(null, item) { }
        internal override void Activate() => Pulled++;
        internal override void Deactivate() => Released++;
    }

    // A press is one pull at the next step and the button held after it pulls nothing more, so a semi-auto fires once;
    // the let-go is one release.
    [Fact]
    public void PlayerTriggerIsAnEdge()
    {
        var (arena, east, _) = SeamPair(false);
        var trigger = new SeamTrigger(SeamGun(east).Item);
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

// The shield switch on the seam (aetheria-arena, seam fixes): the player's shield press is a PilotCommand press, applied at
// the step like the other switches. Here because this class builds a shielded target.
public sealed partial class FireControlCut124Tests
{
    [Fact]
    public void TheShieldSwitchIsAPressAtTheStep()
    {
        var e = Build(TestSettings(), SolidShape(21, 3), equipShield: true);
        var shield = e.Target.Shield;
        Assert.NotNull(shield);
        Assert.True(shield.Item.Enabled.Value);

        e.Target.Command.ToggleShield();
        Assert.True(shield.Item.Enabled.Value, "nothing lands before the step");
        e.Zone.Update(.1f);
        Assert.False(shield.Item.Enabled.Value);
        e.Zone.Update(.1f);
        Assert.False(shield.Item.Enabled.Value, "the press is not replayed");

        e.Target.Command.ToggleShield();
        e.Target.Command.ToggleShield();
        e.Zone.Update(.1f);
        Assert.False(shield.Item.Enabled.Value, "two presses in a step cancel");
        e.Target.Command.ToggleShield();
        e.Zone.Update(.1f);
        Assert.True(shield.Item.Enabled.Value);
    }
}
