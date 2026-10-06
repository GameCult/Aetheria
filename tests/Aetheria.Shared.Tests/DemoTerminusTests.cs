/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

// The demo scenario (aetheria-release, cut demo-galaxy): a prelude galaxy of a fixed cast whose antagonist's home is the
// boss zone and the exit. The shipped catalog holds no Pirates faction yet, so each test that needs one adds it to its
// scratch copy of the catalog.
public sealed partial class RunStartTests
{
    private static readonly uint[] DemoSeeds = { 1, 2, 3, 4, 5 };

    // The catalog record the demo's protagonist needs, until the shipped catalog has one.
    private void AddPirates() => _cache.Upsert(new Faction { Name = "Pirates", ShortName = "Pirates" });

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
            var zhestokost = galaxy.Factions.Single(faction => faction.Name == "Zhestokost");
            Assert.Equal(new[] { zhestokost }, galaxy.BossZones.Keys);
            Assert.Same(galaxy.HomeZones[zhestokost], galaxy.BossZones[zhestokost]);
            Assert.Same(galaxy.HomeZones[zhestokost], galaxy.Exit);
            Assert.NotSame(galaxy.Entrance, galaxy.Exit);
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
        RunSave.Commit(_cache, game, zones, RunSave.Lots(_cache));
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

    // The tutorial is the galaxy it was before the demo: its cast, homes, entrance and ownership at a fixed seed, with
    // no exit and no boss zone.
    [Fact]
    public void TutorialGalaxy_unchanged()
    {
        var galaxy = RunStart.Generate(new TutorialGalaxy(), Inputs(() => GalaxySeed));
        Assert.Equal(TutorialSeed1, TutorialShape(galaxy));
        Assert.Null(galaxy.Exit);
        Assert.Empty(galaxy.BossZones);
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
