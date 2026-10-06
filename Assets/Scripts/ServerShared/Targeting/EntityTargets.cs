/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using static CultMath.math;

// Mining index: entities as targets. Perception stays Entity's: the only entities a search can be offered are those
// already in its observer's VisibleEntities, so the one region carries no visibility bound (they are already seen).
public sealed class EntityTargets : ITargetProvider
{
    public void Regions(in TargetSearch search, List<TargetRegion> into)
    {
        var observer = search.Observer;
        if (observer == null || observer.VisibleEntities.Count == 0) return;
        var nearest = float.PositiveInfinity;
        var farthest = 0f;
        foreach (var entity in observer.VisibleEntities)
        {
            var distance = length(entity.Position.xz - search.Position);
            nearest = min(nearest, distance);
            farthest = max(farthest, distance);
        }
        into.Add(new TargetRegion
        {
            Provider = this,
            Nearest = nearest,
            Farthest = farthest,
            BearingHalfWidth = PI,
            MaxVisibility = float.PositiveInfinity
        });
    }

    public void Candidates(in TargetSearch search, in TargetRegion region, List<TargetCandidate> into)
    {
        foreach (var entity in search.Observer.VisibleEntities)
            into.Add(new TargetCandidate(entity, entity.Position.xz));
    }
}
