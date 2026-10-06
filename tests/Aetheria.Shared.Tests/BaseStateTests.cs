/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

// A transition added with includeChildren reaches every state its source can reach. Zones, and so their Minions'
// state graphs, are built on more than one thread at once, so each walk must see only its own graph.
public sealed class BaseStateTests
{
    private const int Depth = 200;

    // A chain root -> s1 -> ... -> sN, and the exit a transition from root will lead to.
    private static (BaseState root, List<BaseState> chain, BaseState exit) Chain()
    {
        var root = new BaseState(null);
        var chain = new List<BaseState>();
        var previous = root;
        for (var i = 0; i < Depth; i++)
        {
            var state = new BaseState(null);
            previous.AddTransition(state, () => false);
            chain.Add(state);
            previous = state;
        }
        return (root, chain, new BaseState(null));
    }

    // Every state on the chain gains exactly one transition to the exit, and the exit gains none.
    private static bool ExitReachesTheWholeChain((BaseState root, List<BaseState> chain, BaseState exit) graph) =>
        graph.chain.Prepend(graph.root).All(state => state.Transitions.Count(t => t.TargetState == graph.exit) == 1)
        && graph.exit.Transitions.Count == 0;

    private static void AddExit((BaseState root, List<BaseState> chain, BaseState exit) graph) =>
        graph.root.AddTransition(graph.exit, () => false, null, true, graph.root);

    [Fact]
    public void AnIncludeChildrenTransitionReachesTheWholeGraph()
    {
        var graph = Chain();
        AddExit(graph);
        Assert.True(ExitReachesTheWholeChain(graph));
    }

    // Graphs built beforehand, then their walks started together on parallel threads, round after round.
    [Fact]
    public void GraphWalksOnParallelThreadsSeeOnlyTheirOwnGraph()
    {
        const int rounds = 50, width = 16;
        var wrong = 0;
        var threw = 0;
        for (var round = 0; round < rounds; round++)
        {
            var graphs = Enumerable.Range(0, width).Select(_ => Chain()).ToArray();
            using var start = new Barrier(width);
            var threads = graphs.Select(graph => new Thread(() =>
            {
                start.SignalAndWait();
                try { AddExit(graph); }
                catch (Exception) { Interlocked.Increment(ref threw); }
            })).ToArray();
            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();
            wrong += graphs.Count(graph => !ExitReachesTheWholeChain(graph));
        }
        Assert.True(wrong == 0 && threw == 0, $"of {rounds * width} parallel walks, {wrong} left the wrong graph and {threw} threw");
    }
}
