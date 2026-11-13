using System;
using System.Collections.Generic;
using Xunit;

// RunStart.Launch decides whether a start produced a run; the game scene halts on its answer (a failed start clears the
// galaxy, and a scene with no galaxy steps nothing). Kills: a start that throws or returns nothing reading as a run, a
// failure with no reason to show on the menu, a thrown exception that is neither logged nor listed, and a start that
// worked being reported failed.
public sealed class RunLaunchTests
{
    [Fact]
    public void AStartThatThrowsIsNoRunAndSaysWhy()
    {
        var failures = new List<string>();
        var logged = new List<Exception>();
        var staged = RunStart.Launch(() => throw new InvalidOperationException("the arena would not generate"), failures, logged.Add);
        Assert.Null(staged);
        Assert.Equal(new[] { "the arena would not generate" }, failures);
        Assert.Single(logged);
    }

    [Fact]
    public void AStartThatReturnsNothingIsNoRunAndKeepsItsOwnReasons()
    {
        var failures = new List<string>();
        Assert.Null(RunStart.Launch(() => { failures.Add("player: the scenario places no player"); return null; }, failures));
        Assert.Equal(new[] { "player: the scenario places no player" }, failures);
    }

    [Fact]
    public void AStartThatReturnsNothingAndSaysNothingStillGivesAReason()
    {
        var failures = new List<string>();
        Assert.Null(RunStart.Launch(() => null, failures));
        Assert.Single(failures);
    }

    [Fact]
    public void AStartThatStagedIsARunWithNoFailures()
    {
        var failures = new List<string>();
        var made = new RunStart.Staged();
        Assert.Same(made, RunStart.Launch(() => made, failures));
        Assert.Empty(failures);
    }
}
