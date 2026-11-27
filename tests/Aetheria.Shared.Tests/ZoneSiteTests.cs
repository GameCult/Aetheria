/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using Xunit;

// Zone body identity (cut zone-body-sites, ruling zone-body-site-identity): a generated body's key is minted by the cache and
// is identity only; its Site is the generation coordinate (zone seed, kind, place in the pack) that ore, tie-breaks, rock meshes
// and names read. Every input is the production one: the fixture galaxy, GenerateZone, the run store.
public sealed partial class RunStartTests
{
    private BodyData BodyOf(CultRecordRef<BodyData> body) => _cache.Get(body);

    // Replace and Beside generate a scenario of a saved run's seed beside the saved run in one store.
    [Fact]
    public void ASameSeedBesideItselfTouchesNoneOfTheFirstBodies()
    {
        var zone = _galaxy.Entrance;
        var first = BossZonePack(_galaxy, _items, zone, ambient: false);
        var held = first.Planets.Select(body => (body, Doc: BodyOf(body))).ToArray();
        var second = BossZonePack(_galaxy, _items, zone, ambient: false);

        Assert.Equal(first.Planets.Count, second.Planets.Count);
        Assert.Empty(first.Planets.Select(body => body.Key).Intersect(second.Planets.Select(body => body.Key)));
        Assert.Empty(first.Orbits.Select(orbit => orbit.Key).Intersect(second.Orbits.Select(orbit => orbit.Key)));
        Assert.Equal(first.Planets.Select(body => BodyOf(body).Site), second.Planets.Select(body => BodyOf(body).Site));
        Assert.Equal(first.Planets.Select(body => BodyOf(body).Name), second.Planets.Select(body => BodyOf(body).Name));
        foreach (var (body, doc) in held) Assert.Same(doc, BodyOf(body));
    }

    [Fact]
    public void EveryGeneratedBodyHasASiteUniqueInItsPack()
    {
        var swarms = 0;
        var lanes = 0;
        foreach (var zone in _galaxy.Zones)
        {
            var bodies = BossZonePack(_galaxy, _items, zone, ambient: false).Planets.Select(BodyOf).ToArray();
            Assert.All(bodies, body => Assert.False(string.IsNullOrEmpty(body.Site), $"{zone.Name} has a body with no Site"));
            Assert.Equal(bodies.Length, bodies.Select(body => body.Site).Distinct().Count());
            Assert.All(bodies, body => Assert.Equal(BodyData.NameOf(body.Site), body.Name));
            swarms += bodies.Count(body => body.Site.Contains(":swarm:"));
            lanes += bodies.Count(body => body.Site.Contains(":lane:"));
        }
        Assert.True(swarms > 0, "the fixture galaxy generated no trojan swarm");
        Assert.True(lanes > 0, "the fixture galaxy generated no laned belt");
    }
}
