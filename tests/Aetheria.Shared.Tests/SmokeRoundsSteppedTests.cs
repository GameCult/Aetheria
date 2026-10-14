using System.Collections.Generic;
using System.Linq;
using UniRx;
using Xunit;
using static CultMath.math;

// aetheria-release, cut scenarios-smoke-rounds: the Fused Rounds and AI Fused Discipline arenas flown, stepped through
// Zone.Update with the production fire path (Activate, the weapons' Execute, FireControl.Step), each against a control that
// shows the harness fires when the rule allows it. SmokeRoundsScenarioTests reads what the arenas stage; this reads what they do.
public sealed partial class RunStartTests
{
    private const float SteppedDt = .05f;

    private (Zone Arena, RunStart.Staged Staged) LaunchStepped(Scenario scenario)
    {
        var (_, arena, staged, failures) = Launch(scenario, Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        staged.Player.GenerateWeaponGroups();
        EvSettle(staged.Player);
        return (arena, staged);
    }

    // Fused Rounds, nothing selected, the player aiming down the bare hull ahead. The DeathCluster is the control: it fires
    // in this arena, so a fused weapon that does not is not a harness that cannot fire. A fused round flies slowly enough
    // to be in the air when the direct weapon kills the fragile hull (check A.3) and bursts on the first hull on the aim line.
    [Theory]
    [InlineData("DeathCluster")]
    [InlineData("Smoke Proximity")]
    [InlineData("Smoke Delayed")]
    public void EveryWeaponAboardFiresInTheSteppedFusedRoundsArena(string design)
    {
        var (arena, staged) = LaunchStepped(new FusedRounds());
        var player = staged.Player;
        var weapon = (InstantWeapon) WeaponOf(Carried(player, design));
        player.Aim = float3(0, 0, 1);
        var fired = new List<float>();
        var committed = new List<(float At, ShotOutcome Outcome)>();
        weapon.OnFire += _ => fired.Add(arena.Time);
        using var subscription = arena.ShotCommitted.Subscribe(outcome => committed.Add((arena.Time, outcome)));

        weapon.Activate();
        for (var i = 0; i < 60; i++) arena.Update(SteppedDt);

        Assert.True(fired.Count > 0, $"{design} fired nothing in 3 s; the ship's reactor is {(player.PowerBus.TotalGeneration > 0f ? "running" : "down")}");
        if (design == "DeathCluster") return;
        var (at, burst) = committed.First();
        Assert.Equal(ShotResult.Burst, burst.Result);
        Assert.True(burst.HasBurstPoint);
        var reach = length(burst.BurstPoint - player.Position.xz);
        Assert.InRange(reach, FusedRounds.Ahead.y - 30f, FusedRounds.Ahead.y); // on the first hull in the way, not at max range
        Assert.InRange(at + burst.ArrivalIn - fired[0], 1f, 5f); // in the air for seconds: not resolved in its firing tick, not minutes of flight
    }

    // Check A.3 flown: the fragile hull is selected and the DeathCluster, at full damage inside its effective range, starts
    // killing it; once half its hull is gone a Smoke Proximity round is fired at it, and the hull dies before the round
    // lands, so the round never bursts.
    [Fact]
    public void TheDirectWeaponKillsTheFragileHullWhileASmokeRoundIsStillInTheAir()
    {
        var (arena, staged) = LaunchStepped(new FusedRounds());
        var player = staged.Player;
        var fragile = staged.Entities[2];
        Assert.Equal(FusedRounds.Fragile, fragile.Position.xz);
        player.SetTarget(fragile);
        player.SetIff(fragile, true);
        player.EntityInfoGathered[fragile] = 1f;
        var proximity = Carried(player, "Smoke Proximity");
        (float At, ShotResult Result)? landed = null;
        using var subscription = arena.ShotResolved.Subscribe(outcome =>
        {
            if (landed == null && outcome.Weapon == proximity) landed = (arena.Time, outcome.Result);
        });

        var hullDurability = ((HullData) _items.GetData(fragile.Hull)).Durability;
        WeaponOf(Carried(player, "DeathCluster")).Activate();
        float? deadAt = null;
        var fired = false;
        for (var i = 0; i < 120 && landed == null; i++)
        {
            if (!fired && fragile.Hull.Durability < hullDurability / 2f) { WeaponOf(proximity).Activate(); fired = true; }
            arena.Update(SteppedDt);
            if (deadAt == null && !arena.Entities.Contains(fragile)) deadAt = arena.Time;
        }

        Assert.True(fired, "the DeathCluster never hurt the fragile hull");
        Assert.NotNull(deadAt);
        Assert.NotNull(landed);
        Assert.True(deadAt <= landed.Value.At, $"the round landed at {landed.Value.At:F2} s, before the fragile hull died at {deadAt:F2} s");
        Assert.NotEqual(ShotResult.Burst, landed.Value.Result);
    }

    // AI Fused Discipline, run for half a minute (the hostile closes from its 700 m in about 15 s, and either control fires within
    // a second), against its two controls. The hostile closes on the player and never fires its refused weapon; the neutral never fires its fused one. Each silence is discipline, not inability: the same hostile
    // fires once its weapon's Range reaches its blast radius, and the same neutral fires once it is hostile to the player.
    [Fact]
    public void TheAiHoldsARefusedWeaponAndANeutralHoldsItsFusedOne()
    {
        (int Hostile, int Neutral, float Closest) Fly(System.Action<Entity, Entity, Ship> alter = null)
        {
            var (arena, staged) = LaunchStepped(new AiFusedDiscipline());
            var (hostile, neutral) = (staged.Entities[0], staged.Entities[1]);
            alter?.Invoke(hostile, neutral, staged.Player);
            int hostileShots = 0, neutralShots = 0;
            using var subscription = arena.ShotCommitted.Subscribe(outcome =>
            {
                if (outcome.Source == hostile) hostileShots++;
                else if (outcome.Source == neutral) neutralShots++;
            });
            var closest = float.MaxValue;
            for (var i = 0; i < 300; i++)
            {
                arena.Update(.1f);
                closest = min(closest, length((hostile.Position - staged.Player.Position).xz));
            }
            return (hostileShots, neutralShots, closest);
        }

        var shipped = Fly();
        Assert.Equal((0, 0), (shipped.Hostile, shipped.Neutral));
        Assert.True(shipped.Closest < 100f, $"the hostile never closed on the player (nearest {shipped.Closest:F0} m)");

        // Control: the hostile's weapon given the reach to arm. The design's data is shared, so it is put back.
        var refused = _cache.GetAll<WeaponItemData>().Single(design => design.Name == "Smoke Refused").Behaviors.OfType<WeaponData>().Single();
        var range = refused.Range;
        refused.Range = new PerformanceStat { Min = 2000f, Max = 2000f };
        try { Assert.True(Fly().Hostile > 0, "the hostile fires nothing even when its weapon can arm: the arena cannot show it holding fire"); }
        finally { refused.Range = range; }

        // Control: the neutral made hostile to the player, both ways.
        Assert.True(Fly((hostile, neutral, player) => { neutral.SetIff(player, true); player.SetIff(neutral, true); }).Neutral > 0,
            "the neutral fires nothing even when hostile: the arena cannot show it holding fire");
    }
}
