/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using Xunit;

// Assertions over collections of entities and shots. xunit formats a collection's contents into its failure
// message, and an Entity, a PendingShot or a ShotOutcome drags the whole zone graph in with it: the formatter
// recurses until the test host dies of a stack overflow, which a mutation tool scores as a kill of every test
// that was running. A failure here must be an ordinary red naming the test, so these compare counts and
// identities and never hand xunit an entity.
internal static class SafeAssert
{
    public static void NoShots(Zone zone) =>
        Assert.True(zone.PendingShots.Count == 0, $"{zone.PendingShots.Count} shot(s) still pending");

    public static PendingShot OnlyShot(Zone zone)
    {
        Assert.True(zone.PendingShots.Count == 1, $"expected exactly one pending shot, found {zone.PendingShots.Count}");
        return zone.PendingShots[0];
    }

    public static T Only<T>(List<T> list)
    {
        Assert.True(list.Count == 1, $"expected exactly one {typeof(T).Name}, found {list.Count}");
        return list[0];
    }

    public static void Empty<T>(List<T> list) => Assert.True(list.Count == 0, $"expected no {typeof(T).Name}, found {list.Count}");

    public static void In(Zone zone, Entity entity, string why = "entity should be in the zone") =>
        Assert.True(zone.Entities.Contains(entity), why);

    public static void NotIn(Zone zone, Entity entity, string why = "entity should have left the zone") =>
        Assert.False(zone.Entities.Contains(entity), why);
}
