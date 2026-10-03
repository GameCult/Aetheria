/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

// Mining index (docs/aetheria-release-map.md, L3-L5; operator 2026-10-01: "don't specialize the indexing too much
// towards asteroid belts"): one targeting index per zone (Zone.Targets), fed by providers. It owns which targetable
// things can lie in a region and an upper bound on how visible they can be. Each provider owns its own things'
// motion and bounds. Whether one target is actually visible stays with Entity (ChunkVisible, VisibleEntities): every
// answer here is a superset the caller filters exactly. Nothing in the index names a belt or a rock.

// One thing that can be targeted, with where it stands on the plane now.
public readonly struct TargetCandidate
{
    public readonly TargetRef Target;
    public readonly float2 Position;

    public TargetCandidate(TargetRef target, float2 position)
    {
        Target = target;
        Position = position;
    }
}

// Who is searching, from where, how far, and how well they can see. A null Observer perceives no entities.
// ReachPerVisibility is Entity.DetectionReachPerVisibility: no target of visibility v farther than v times it can be
// detected. Infinity applies no visibility bound.
public readonly struct TargetSearch
{
    public readonly float2 Position;
    public readonly float Reach;
    public readonly Entity Observer;
    public readonly float ReachPerVisibility;

    public TargetSearch(float2 position, float reach, Entity observer = null, float reachPerVisibility = float.PositiveInfinity)
    {
        Position = position;
        Reach = reach;
        Observer = observer;
        ReachPerVisibility = reachPerVisibility;
    }
}

// A group of a provider's things, with bounds that hold for every one of them at the time it was made: planar
// distance from the searcher in [Nearest, Farthest], bearing from the searcher (radians, as atan2 measures it)
// within BearingHalfWidth of BearingCentre (pi means any bearing), and visibility at most MaxVisibility.
// Group, Start and End are the provider's own: the index only hands the region back to it.
public struct TargetRegion
{
    public ITargetProvider Provider;
    public int Group, Start, End;
    public float Nearest, Farthest;
    public float BearingCentre, BearingHalfWidth;
    public float MaxVisibility;

    // Whether anything in this region can both lie within reach and be detected by the searcher.
    public bool CanHold(in TargetSearch search) =>
        !(Nearest > search.Reach) && !(Nearest > MaxVisibility * search.ReachPerVisibility);
}

public interface ITargetProvider
{
    // Regions that together hold every one of this provider's things within the search's reach, added to `into`.
    void Regions(in TargetSearch search, List<TargetRegion> into);

    // The things of one region this provider made for this search, with their positions now, added to `into`.
    void Candidates(in TargetSearch search, in TargetRegion region, List<TargetCandidate> into);
}

// A best-first question: the candidate with the least key wins. Bound must never exceed the key of any candidate the
// region holds (positive infinity skips the region). Key returns false for a candidate that is not eligible.
// Precedes breaks a tie between two equal keys.
public interface ITargetKey
{
    float Bound(in TargetRegion region);
    bool Key(in TargetCandidate candidate, out float key);
    bool Precedes(in TargetCandidate a, in TargetCandidate b);
}

public sealed class TargetingIndex
{
    private readonly List<ITargetProvider> _providers = new List<ITargetProvider>();
    private readonly List<TargetRegion> _regions = new List<TargetRegion>();
    private readonly List<TargetCandidate> _candidates = new List<TargetCandidate>();
    private readonly List<(float bound, int region)> _order = new List<(float, int)>();

    // Diagnostic only: how many candidates the index has posed and tested, ever. Tests pin a query's cost with it
    // rather than with timing.
    public long Examined { get; private set; }

    public void Add(ITargetProvider provider) => _providers.Add(provider);

    // Every candidate within the search's reach whose region can be detected, written into `into` (cleared first),
    // in no particular order. Reach is exact; detection is only bounded, so the caller still tests it.
    public void Within(in TargetSearch search, List<TargetCandidate> into)
    {
        into.Clear();
        _regions.Clear();
        foreach (var provider in _providers) provider.Regions(search, _regions);
        foreach (var region in _regions)
        {
            if (!region.CanHold(search)) continue;
            _candidates.Clear();
            region.Provider.Candidates(search, region, _candidates);
            foreach (var candidate in _candidates)
            {
                Examined++;
                if (length(candidate.Position - search.Position) <= search.Reach) into.Add(candidate);
            }
        }
    }

    // The eligible candidate within reach with the least key, or false if there is none. Regions are opened in order
    // of their bound, and the search stops at the first region whose bound is greater than the best key found:
    // Hjaltason and Samet's best-first browsing over the providers' flat region sets.
    public bool Best<TKey>(in TargetSearch search, TKey query, out TargetCandidate best) where TKey : ITargetKey
    {
        best = default;
        var found = false;
        var bestKey = float.PositiveInfinity;
        _regions.Clear();
        _order.Clear();
        foreach (var provider in _providers) provider.Regions(search, _regions);
        for (var i = 0; i < _regions.Count; i++)
        {
            if (!_regions[i].CanHold(search)) continue;
            var bound = query.Bound(_regions[i]);
            if (!float.IsPositiveInfinity(bound)) _order.Add((bound, i));
        }
        _order.Sort((a, b) => a.bound.CompareTo(b.bound));
        foreach (var (bound, index) in _order)
        {
            if (found && bound > bestKey) break;
            _candidates.Clear();
            var region = _regions[index];
            region.Provider.Candidates(search, region, _candidates);
            foreach (var candidate in _candidates)
            {
                Examined++;
                if (!(length(candidate.Position - search.Position) <= search.Reach) || !query.Key(candidate, out var key)) continue;
                if (!found || key < bestKey || key == bestKey && query.Precedes(candidate, best))
                {
                    best = candidate;
                    bestKey = key;
                    found = true;
                }
            }
        }
        return found;
    }
}
