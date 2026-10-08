/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;

// The demo scenario (aetheria-release, cut demo-galaxy): a prelude galaxy of a fixed cast whose antagonist's home is the
// boss zone and the exit. The shipped catalog holds no Pirates faction yet, so each test that needs one adds it to its
// scratch copy of the catalog.
public sealed partial class RunStartTests
{
    private static readonly uint[] DemoSeeds = { 1, 2, 3, 4, 5 };

    // The catalog record the demo's protagonist needs, until the shipped catalog has one. Zone names need a name file,
    // and the entrance's station needs a faction whose allegiance reaches some manufacturer, so it reaches every one.
    private void AddPirates()
    {
        var pirates = new Faction
        {
            Name = "Pirates", ShortName = "Pirates", GeonameFile = _cache.RefOf(_cache.GetAll<NameFile>().First())
        };
        foreach (var faction in _cache.GetAll<Faction>().ToArray()) pirates.Allegiance[_cache.RefOf(faction)] = 1;
        _cache.Upsert(pirates);
    }

    private Galaxy DemoGalaxy(uint seed) => RunStart.Generate(new DemoTerminus(), Inputs(() => seed));

    [Fact]
    public void DemoTerminus_fixed_cast()
    {
        AddPirates();
        foreach (var seed in DemoSeeds)
            Assert.Equal(new[] { "Pirates", "Zhestokost", "Lucent Media", "Aeronautics Unlimited" },
                DemoGalaxy(seed).Factions.Select(faction => faction.Name));
    }

    [Fact]
    public void DemoTerminus_gate_at_antagonist_home()
    {
        AddPirates();
        foreach (var seed in DemoSeeds)
        {
            var galaxy = DemoGalaxy(seed);
            // Zones are compared by index: xunit formats a failing zone into its message, and a zone drags the galaxy
            // graph in with it until the test host dies.
            int At(GalaxyZone zone) => Array.IndexOf(galaxy.Zones, zone);
            var zhestokost = galaxy.Factions.Single(faction => faction.Name == "Zhestokost");
            Assert.Equal(new[] { "Zhestokost" }, galaxy.BossZones.Keys.Select(faction => faction.Name));
            Assert.Equal(At(galaxy.HomeZones[zhestokost]), At(galaxy.BossZones[zhestokost]));
            Assert.Equal(At(galaxy.HomeZones[zhestokost]), At(galaxy.Exit));
            Assert.NotEqual(At(galaxy.Entrance), At(galaxy.Exit));
        }
    }

    // The player's standing is part of the run: Continue restores it, and the gate, through the saved game.
    [Fact]
    public void DemoTerminus_standing_survives_continue()
    {
        AddPirates();
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        galaxy.Entrance.Contents = arena;
        var (game, zones) = RunSave.Capture(_cache, galaxy, arena, staged.Player, new SavedActionBarBinding[0]);
        RunSave.Commit(_cache, game, zones, _items.Lots);
        var restored = new Galaxy(_cache, _cache.GetGlobal<SavedGame>(), _ => { });

        Dictionary<string, FactionRelationship> Standing(Galaxy g) => g.Factions.ToDictionary(faction => faction.Name, faction => g.FactionRelationships[faction]);
        Assert.Equal(new Dictionary<string, FactionRelationship>
        {
            ["Pirates"] = FactionRelationship.Friendly,
            ["Zhestokost"] = FactionRelationship.Hated,
            ["Lucent Media"] = FactionRelationship.Neutral,
            ["Aeronautics Unlimited"] = FactionRelationship.Neutral
        }, Standing(restored));
        Assert.Equal(Standing(galaxy), Standing(restored));
        Assert.Equal(Array.IndexOf(galaxy.Zones, galaxy.Exit), Array.IndexOf(restored.Zones, restored.Exit));
        Assert.Equal(
            galaxy.BossZones.Select(boss => (boss.Key.Name, Array.IndexOf(galaxy.Zones, boss.Value))),
            restored.BossZones.Select(boss => (boss.Key.Name, Array.IndexOf(restored.Zones, boss.Value))));
    }

    // On the shipped catalog, which has no Pirates, the run does not start: the galaxy refuses, naming the cast field,
    // and the scenario never reaches staging.
    [Fact]
    public void DemoTerminus_missing_cast_refused()
    {
        Assert.DoesNotContain(_cache.GetAll<Faction>(), faction => faction.Name.StartsWith("Pirates", StringComparison.Ordinal));
        var refusal = Assert.Throws<InvalidOperationException>(() => DemoGalaxy(1));
        Assert.Contains("Pirates", refusal.Message);
        Assert.Contains(nameof(TutorialGenerationSettings.ProtagonistFaction), refusal.Message);
    }

    // The operator's ruling is that the first release is a demo: the one scenario a release build lists says so by name.
    [Fact]
    public void DemoTerminus_is_named_a_demo()
    {
        var listed = Assert.Single(Scenarios.Modes);
        Assert.IsType<DemoTerminus>(listed);
        Assert.Equal("Terminus (Demo)", listed.Name);
    }

    // The run start as New Game runs it: the player is not a Pirate, so the hull is the base Longinus, made by Alakrita,
    // not by the Pirates or any cast faction, with no faction on the ship, at the entrance zone's origin.
    [Fact]
    public void DemoTerminus_player_flies_a_non_pirates_brand_hull_the_longinus()
    {
        AddPirates();
        foreach (var seed in DemoSeeds)
        {
            var (galaxy, _, staged, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
            Assert.True(failures.Count == 0, string.Join("; ", failures));
            Assert.Equal("Longinus", staged.Player.HullData.Name);
            var design = _cache.RefOf(staged.Player.HullData).Key;
            var product = Assert.Single(_cache.GetAll<FactionProductData>(), p => p.Design.Key.Equals(design));
            var maker = _cache.Get(product.Manufacturer);
            Assert.Equal("Alakrita", maker.Name);
            Assert.DoesNotContain(galaxy.Factions, faction => faction.Name == maker.Name);
            Assert.Null(staged.Player.Faction);
            Assert.Equal(float3.zero, staged.Player.Position);
        }
    }

    // The ruling says the Longinus with its default fit: the fit the game generates for that hull from the run's item
    // draws, which Stage seeds from the galaxy's seed. The expected fit is that same generation replayed from the same
    // seed, so no item list is spelled here; a bare hull, a preset, or one item swapped for another differs from it.
    [Fact]
    public void DemoTerminus_player_carries_the_longinus_default_fit()
    {
        AddPirates();
        string Fit(Entity ship) => string.Join(";", ship.Equipment
            .Select(item => $"{item.Data.Name}@{item.Position}/{item.EquippableItem.Rotation}")
            .OrderBy(entry => entry, StringComparer.Ordinal));
        foreach (var seed in DemoSeeds)
        {
            var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
            Assert.True(failures.Count == 0, string.Join("; ", failures));
            _items.Random = new CultMath.Random(galaxy.Seed * 0x9E3779B1u + 2);
            var generator = new LoadoutGenerator(ref _items.Random, _items, galaxy, arena.GalaxyZone, null, 2);
            var expected = EntitySerializer.Unpack(_items, null, generator.GenerateShipLoadout(hull => hull.Name == "Longinus"));
            Assert.NotEmpty(expected.Equipment);
            Assert.Equal(Fit(expected), Fit(staged.Player));
        }
    }

    // The demo region's size and connectivity are authored: 64 zones, links thinned to half. The reference galaxy is the
    // same cast with the thinning written out here, generated at the same seed, so any other density is a different graph.
    [Fact]
    public void DemoTerminus_region_is_sixty_four_zones_linked_at_half_density()
    {
        AddPirates();
        string Links(Galaxy g) => string.Join(";", g.Zones.Select(zone =>
            string.Join(",", zone.AdjacentZones.Select(adjacent => Array.IndexOf(g.Zones, adjacent)).OrderBy(index => index))));
        Galaxy Reference(uint seed, float density)
        {
            var cast = new TutorialGenerationSettings
            {
                ProtagonistFaction = "Pirates", AntagonistFaction = "Zhe", BufferFaction = "Luc",
                NeutralFactions = new[] { "Aero" }, QuestFaction = null, ZoneCount = 64, LinkDensity = density
            };
            return RunStart.Generate(new Scripted(false, _ => { }, seed, galaxy => galaxy.Prelude(cast)), Inputs());
        }

        foreach (var seed in DemoSeeds)
        {
            var galaxy = DemoGalaxy(seed);
            Assert.Equal(64, galaxy.Zones.Length);
            Assert.Equal(Links(Reference(seed, .5f)), Links(galaxy));
            Assert.NotEqual(Links(Reference(seed, 1f)), Links(galaxy));
        }
    }

    // A cast that names no quest faction places no quest zone: one home for each of its four factions.
    [Fact]
    public void ACastNamingNoQuestFactionPlacesNoQuestZone()
    {
        AddPirates();
        var galaxy = DemoGalaxy(1);
        Assert.Equal(4, galaxy.HomeZones.Count);
        Assert.Equal(4, galaxy.Factions.Length);
    }

    // A name that resolves to nothing refuses the galaxy whichever cast field names it, and the refusal names the field.
    [Theory]
    [InlineData(nameof(TutorialGenerationSettings.ProtagonistFaction))]
    [InlineData(nameof(TutorialGenerationSettings.AntagonistFaction))]
    [InlineData(nameof(TutorialGenerationSettings.BufferFaction))]
    [InlineData(nameof(TutorialGenerationSettings.QuestFaction))]
    [InlineData(nameof(TutorialGenerationSettings.NeutralFactions))]
    public void AnUnresolvedCastNameRefusesTheGalaxyNamingItsField(string field)
    {
        var cast = new TutorialGenerationSettings
        {
            ProtagonistFaction = _tutorialSettings.ProtagonistFaction,
            AntagonistFaction = _tutorialSettings.AntagonistFaction,
            BufferFaction = _tutorialSettings.BufferFaction,
            QuestFaction = _tutorialSettings.QuestFaction,
            NeutralFactions = _tutorialSettings.NeutralFactions,
            ZoneCount = _tutorialSettings.ZoneCount,
            LinkDensity = _tutorialSettings.LinkDensity
        };
        typeof(TutorialGenerationSettings).GetField(field).SetValue(cast, field == nameof(cast.NeutralFactions) ? new[] { "Nonesuch" } : "Nonesuch");
        var scenario = new Scripted(false, _ => { }, generate: galaxy => galaxy.Prelude(cast));
        var refusal = Assert.Throws<InvalidOperationException>(() => RunStart.Generate(scenario, Inputs()));
        Assert.Contains(field, refusal.Message);
    }

    // What a release build lists under New Game is the demo alone; the tutorial and the main galaxy are listed only in
    // editor and development builds (MainMenu.ShowScenarios), beside the test arenas.
    [Fact]
    public void ReleaseBuildsListOnlyTheDemo()
    {
        Assert.Equal(new[] { typeof(DemoTerminus) }, Scenarios.Modes.Select(scenario => scenario.GetType()));
        Assert.Equal(new[] { typeof(TutorialGalaxy), typeof(MainGalaxy) }, Scenarios.Development.Select(scenario => scenario.GetType()));
    }

    // The tutorial is the galaxy it was before the demo: its cast, homes, entrance and ownership at a fixed seed, with
    // no exit and no boss zone.
    [Fact]
    public void TutorialGalaxy_unchanged()
    {
        var galaxy = RunStart.Generate(new TutorialGalaxy(), Inputs(() => GalaxySeed));
        Assert.Equal(TutorialSeed1, TutorialShape(galaxy));
        Assert.True(galaxy.Exit == null, "the tutorial has no exit");
        Assert.True(galaxy.BossZones.Count == 0, $"the tutorial has {galaxy.BossZones.Count} boss zones");
    }

    private static string TutorialShape(Galaxy g) =>
        string.Join(",", g.Factions.Select(f => f.Name)) +
        " | homes " + string.Join(",", g.Factions.Select(f => g.HomeZones.TryGetValue(f, out var z) ? Array.IndexOf(g.Zones, z) : -2)) +
        " | entrance " + Array.IndexOf(g.Zones, g.Entrance) +
        " | zones " + g.Zones.Length +
        " | owners " + string.Join(",", g.Zones.Select(z => z.Owner == null ? "-" : z.Owner.ShortName));

    // Measured on 228f241e, before the demo cut, at seed 1.
    private const string TutorialSeed1 =
        "Miss Terri’s Sugariffic Snack Company,Zhestokost,Lucent Media,Adrasteia,Aeronautics Unlimited,Finch Cybernetics | homes 47,10,17,49,35,1 | entrance 25 | zones 64 | owners Zhestokost,Finch,Lucent,Zhestokost,Finch,Adrasteia,Zhestokost,Adrasteia,Zhestokost,Zhestokost,Zhestokost,Zhestokost,Finch,Lucent,Adrasteia,Adrasteia,Zhestokost,Lucent,Finch,Finch,Lucent,Lucent,Finch,AU,Adrasteia,-,-,Adrasteia,Lucent,-,AU,Lucent,Zhestokost,Lucent,Lucent,AU,Adrasteia,AU,Lucent,AU,Finch,Finch,-,Lucent,Zhestokost,Zhestokost,Miss Terri's,Miss Terri's,Miss Terri's,Adrasteia,Finch,-,-,Miss Terri's,Lucent,AU,Miss Terri's,Zhestokost,Lucent,Lucent,Lucent,Finch,-,-";
}
