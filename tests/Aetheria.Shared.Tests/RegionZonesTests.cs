/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

// The region's zones (aetheria-living-world, cut region-zones-live; ruling demo-chain-spans-the-region): every zone of
// a run exists as a live Zone from the start of the run and on Continue, and RunStart.Materialize owns that.
public sealed partial class RunStartTests
{
    private static readonly uint[] RegionSeeds = { 1, 2, 3, 69, 97 };

    // What MainMenu.Launch does after the arena stages: the demo's galaxy and arena, then every other zone.
    private (Galaxy galaxy, Zone arena, RunStart.Staged staged) RegionRun(uint seed, bool materialize = true)
    {
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
        Assert.True(failures.Count == 0, $"seed {seed}: " + string.Join("; ", failures));
        galaxy.Entrance.Contents = arena;
        if (materialize) RunStart.Materialize(_items, _zoneSettings, _planetSettings, galaxy);
        return (galaxy, arena, staged);
    }

    // What a zone's generation drew, as a person reads it: each entity's hull, faction, kind, equipment and cargo designs.
    // A lot's workmanship is a draw; its serial counts the run's mints, which no zone's draws decide, so it is left out.
    private string[] Drawn(GalaxyZone zone) => zone.PackedContents.Entities.Select(entity =>
    {
        string Design(ItemInstance item) => _items.GetData(item).Name
            + (item is ICommodityStack stack ? "x" + stack.Quantity : "")
            + (item is CraftedItemInstance crafted && crafted.Lot != 0 ? "@" + _items.Lots[crafted.Lot].EffectiveQuality().ToString("R") : "");
        var kind = entity is OrbitalEntityPack orbital ? orbital.Kind.ToString() : "";
        return string.Join("|", entity.Name, Design(entity.Hull), entity.Faction.ToString(), kind,
            string.Join(",", entity.Equipment.Select(e => Design(e.item))),
            string.Join(",", entity.CargoContents.SelectMany(bay => bay).Select(c => Design(c.item))));
    }).ToArray();

    [Fact]
    public void EveryZoneIsLive()
    {
        foreach (var seed in RegionSeeds)
        {
            var (galaxy, arena, _) = RegionRun(seed);
            Assert.True(galaxy.Zones.Length > 1, $"seed {seed}: one zone");
            for (var i = 0; i < galaxy.Zones.Length; i++)
            {
                Assert.True(galaxy.Zones[i].Contents != null, $"seed {seed}: zone {i} has no Contents");
                Assert.True(galaxy.Zones[i].PackedContents != null, $"seed {seed}: zone {i} has no pack");
            }
            Assert.Same(arena, galaxy.Entrance.Contents);
            Assert.Contains(galaxy.Zones, zone => zone != galaxy.Entrance && zone.PackedContents.Entities.Count > 0);
        }
    }

    // A zone draws its items from the run seed and its index, not from what was generated before it.
    [Fact]
    public void GenerationOrderMovesNoZone()
    {
        var (all, _, _) = RegionRun(1);
        var picks = new[] { 1, all.Zones.Length / 2, all.Zones.Length - 1 };
        foreach (var pick in picks)
        {
            var (alone, _, _) = RegionRun(1, materialize: false);
            Assert.NotSame(all.Zones[pick], alone.Zones[pick]);
            // every other zone is already generated (empty), so only `pick` draws; it draws after nothing
            for (var i = 0; i < alone.Zones.Length; i++)
                if (i != pick && alone.Zones[i].PackedContents == null) alone.Zones[i].PackedContents = new ZonePack();
            RunStart.Materialize(_items, _zoneSettings, _planetSettings, alone);
            var expected = Drawn(all.Zones[pick]);
            Assert.NotEmpty(expected);
            Assert.Equal(expected, Drawn(alone.Zones[pick]));
        }
    }

    [Fact]
    public void MaterializeIsIdempotent()
    {
        var (galaxy, _, _) = RegionRun(1);
        var contents = galaxy.Zones.Select(zone => zone.Contents).ToArray();
        var packs = galaxy.Zones.Select(zone => zone.PackedContents).ToArray();
        _items.Random = new CultMath.Random(12345u); // a canary: any reseed in Materialize replaces it
        RunStart.Materialize(_items, _zoneSettings, _planetSettings, galaxy);
        for (var i = 0; i < galaxy.Zones.Length; i++)
        {
            Assert.Same(contents[i], galaxy.Zones[i].Contents);
            Assert.Same(packs[i], galaxy.Zones[i].PackedContents);
        }
        Assert.Equal(new CultMath.Random(12345u).NextUInt(), _items.Random.NextUInt()); // nothing reseeded, so nothing generated
    }

    // A run restored from its save builds every zone from what the save holds; a zone the save never generated draws what
    // a fresh run gives it.
    [Fact]
    public void ContinueBuildsEveryZone()
    {
        var (galaxy, arena, staged) = RegionRun(1);
        var (game, zones) = RunSave.Capture(_cache, galaxy, arena, staged.Player, new SavedActionBarBinding[0], 0);
        RunSave.Commit(_cache, game, zones, _items.Lots);
        var restored = new Galaxy(_cache, _cache.GetGlobal<SavedGame>(), _ => { });
        Assert.All(restored.Zones, zone => Assert.Null(zone.Contents));
        RunStart.Materialize(_items, _zoneSettings, _planetSettings, restored);
        Assert.Equal(galaxy.Zones.Length, restored.Zones.Length);
        for (var i = 0; i < galaxy.Zones.Length; i++)
        {
            Assert.NotNull(restored.Zones[i].Contents);
            Assert.Equal(galaxy.Zones[i].Contents.Entities.Count(entity => entity is OrbitalEntity),
                restored.Zones[i].Contents.Entities.Count(entity => entity is OrbitalEntity));
        }
    }

    [Fact]
    public void ASavedRunWithoutAZonePackGeneratesWhatAFreshRunGives()
    {
        var (fresh, _, _) = RegionRun(1);
        var (saved, arena, staged) = RegionRun(1, materialize: false); // a save from before the region was live
        var (game, zones) = RunSave.Capture(_cache, saved, arena, staged.Player, new SavedActionBarBinding[0], 0);
        RunSave.Commit(_cache, game, zones, _items.Lots);
        var restored = new Galaxy(_cache, _cache.GetGlobal<SavedGame>(), _ => { });
        var absent = restored.Zones.Select((zone, i) => (zone, i)).Where(x => x.zone.PackedContents == null).Select(x => x.i).ToArray();
        Assert.NotEmpty(absent);
        RunStart.Materialize(_items, _zoneSettings, _planetSettings, restored);
        foreach (var i in absent)
        {
            Assert.NotNull(restored.Zones[i].Contents);
            Assert.Equal(Drawn(fresh.Zones[i]), Drawn(restored.Zones[i]));
        }
    }

    // Materialize follows a staged scenario (MainMenu.Launch); staging that fails leaves the rest of the region ungenerated.
    [Fact]
    public void AFailedStartMaterializesNothing()
    {
        var scenario = new Scripted(true, _ => { }, generate: stage => new DemoTerminus().Generate(stage));
        var (galaxy, _, staged, failures) = Launch(scenario, Inputs(() => 1));
        Assert.Null(staged);
        Assert.NotEmpty(failures);
        Assert.All(galaxy.Zones.Where(zone => zone != galaxy.Entrance), zone =>
        {
            Assert.Null(zone.Contents);
            Assert.Null(zone.PackedContents);
        });
    }
}
