/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;

// verse-grammar [apply] (aetheria-release, cut verse-grammar; question verse-social-verbs, default speak-only): authors
// the starter verse grammar into GameData/Aetheria.cc. The ship-action half is one verb per TaskType, the shapes of what
// the game can film today; the conversation half is `speak`, matching Ghostlight's kernel-built speak. Dry run unless
// passed "apply", same contract as the other *-migrate commands.
//
// The catalog is opened here rather than through AetheriaStores.Open: that refuses a populated catalog that lacks a
// [CultGlobal], and this command is what authors the VerseGrammar global into a catalog that predates it.
public static class VerseGrammarCommand
{
    public const int StarterRevision = 1;
    public const string StarterDescription = "Verbs a Ghostlight-simulated Aetheria world may use: ship actions the game films in-engine, and speech rendered as Aetheria conversation.";

    private static VerseRole Role(string name, VerseReferentKind binds) => new VerseRole { Name = name, Binds = binds };

    // One ShipAction verb per TaskType (None excepted), then the conversation verbs.
    public static VerseVerb[] StarterVerbs() => new[]
    {
        new VerseVerb { Name = "mine", RenderPath = VerseRenderPath.ShipAction, Description = "The actor works an ore body at a place and takes the ore into its hold.",
            Roles = new[] { Role("site", VerseReferentKind.Place), Role("ore", VerseReferentKind.Cargo) } },
        new VerseVerb { Name = "haul", RenderPath = VerseRenderPath.ShipAction, Description = "The actor carries cargo to a destination place.",
            Roles = new[] { Role("cargo", VerseReferentKind.Cargo), Role("destination", VerseReferentKind.Place) } },
        new VerseVerb { Name = "tow", RenderPath = VerseRenderPath.ShipAction, Description = "The actor tows a station or hull belonging to the target faction.",
            Roles = new[] { Role("target", VerseReferentKind.Faction) } },
        new VerseVerb { Name = "defend", RenderPath = VerseRenderPath.ShipAction, Description = "The actor guards a protected faction's ships and holdings at a place.",
            Roles = new[] { Role("protected", VerseReferentKind.Faction), Role("at", VerseReferentKind.Place) } },
        new VerseVerb { Name = "attack", RenderPath = VerseRenderPath.ShipAction, Description = "The actor attacks the target faction's ships and holdings.",
            Roles = new[] { Role("target", VerseReferentKind.Faction) } },
        new VerseVerb { Name = "explore", RenderPath = VerseRenderPath.ShipAction, Description = "The actor surveys a region and charts what is there.",
            Roles = new[] { Role("region", VerseReferentKind.Place) } },
        new VerseVerb { Name = "speak", RenderPath = VerseRenderPath.Conversation, Description = "The actor says something to whoever is in earshot. Shown as Aetheria dialogue: face to face when docked, by radio with the speaker's portrait over their ship when undocked.",
            Roles = new VerseRole[0] },
    };

    public static bool Same(VerseVerb a, VerseVerb b) =>
        a.Name == b.Name && a.RenderPath == b.RenderPath && a.Description == b.Description &&
        a.Roles.Select(r => (r.Name, r.Binds)).SequenceEqual(b.Roles.Select(r => (r.Name, r.Binds)));

    private static string Shape(VerseVerb verb) =>
        $"{verb.Name}({string.Join(", ", verb.Roles.Select(r => $"{r.Name}: {r.Binds}"))})  [{verb.RenderPath}]";

    public static int Run(bool apply, string root = null)
    {
        var catalogPath = Path.Combine(root ?? AetherDb.FindRoot(), "GameData", "Aetheria.cc");
        using var cache = new CultCache();
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalogPath, !apply), AetheriaStores.CatalogTypes);

        var starter = StarterVerbs();
        foreach (var verb in starter) VerseGrammarValidation.Validate(verb);
        var grammar = cache.GetGlobal<VerseGrammar>();
        var grammarCurrent = grammar != null && grammar.Revision == StarterRevision && grammar.Description == StarterDescription;
        Console.WriteLine(grammar == null ? $"Grammar: new, revision {StarterRevision}."
            : grammarCurrent ? $"Grammar: revision {grammar.Revision}, already authored."
            : $"Grammar: revision {grammar.Revision} -> {StarterRevision}.");
        Console.WriteLine($"  {StarterDescription}");

        var existing = cache.GetAll<VerseVerb>().ToDictionary(verb => verb.Name, StringComparer.Ordinal);
        var changes = starter.Where(verb => !existing.TryGetValue(verb.Name, out var current) || !Same(current, verb)).ToArray();
        Console.WriteLine($"\nStarter verbs ({starter.Length}):");
        foreach (var verb in starter)
        {
            var state = !existing.TryGetValue(verb.Name, out var current) ? "new" : Same(current, verb) ? "unchanged" : "differs, will be replaced";
            Console.WriteLine($"  {Shape(verb)}  {state}");
            Console.WriteLine($"    {verb.Description}");
        }
        var extra = existing.Keys.Where(name => starter.All(verb => verb.Name != name)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (extra.Length > 0) Console.WriteLine($"\nAlready in the catalog and left alone: {string.Join(", ", extra)}");

        if (grammarCurrent && changes.Length == 0)
        {
            Console.WriteLine("\nNothing to do.");
            return 0;
        }
        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to land the grammar global and {changes.Length} verb(s).");
            return 0;
        }

        cache.Commit(batch =>
        {
            if (!grammarCurrent)
            {
                var record = grammar ?? new VerseGrammar();
                record.Revision = StarterRevision;
                record.Description = StarterDescription;
                if (grammar == null) batch.Upsert(typeof(VerseGrammar), record);
                else batch.Upsert(typeof(VerseGrammar), record, cache.RefOf(grammar).Key);
            }
            foreach (var verb in changes)
            {
                if (existing.TryGetValue(verb.Name, out var current)) batch.Upsert(typeof(VerseVerb), verb, cache.RefOf(current).Key);
                else batch.Upsert(typeof(VerseVerb), verb);
            }
        });
        Console.WriteLine($"\nLanded the grammar global and {changes.Length} verb(s) in Aetheria.cc");
        return 0;
    }
}
