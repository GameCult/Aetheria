/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// The verse grammar (aetheria-release, cut verse-grammar; ghostlight-verse rulings verse-grammar-home-ruled,
// verse-verbs-two-render-paths; question verse-social-verbs, default speak-only): the document set, its refusals through
// AetheriaStores.Open, the AetherDb command that authors the starter set, and the fixture writer Ghostlight's
// grammar-reader copies its fixture from.
public sealed class VerseGrammarTests : IDisposable
{
    // Set to a path to have FixtureWriterEmitsTheCatalogGhostlightReads leave the fixture there.
    public const string FixturePathVariable = "AETHERIA_VERSE_FIXTURE_PATH";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "verse-grammar-" + Guid.NewGuid().ToString("N"));

    public VerseGrammarTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private string PathOf(string name) => Path.Combine(_root, name + ".cc");

    private static VerseVerb Good(string name) => new VerseVerb
    {
        Name = name,
        RenderPath = VerseRenderPath.ShipAction,
        Description = "A good verb.",
        Roles = new[] { new VerseRole { Name = "site", Binds = VerseReferentKind.Place }, new VerseRole { Name = "load", Binds = VerseReferentKind.Cargo } },
    };

    // A catalog authored through AetheriaStores.Open; a null grammar leaves the catalog without its global.
    private string Author(string name, VerseGrammar grammar, params VerseVerb[] verbs)
    {
        var path = PathOf(name);
        using var cache = AetheriaStores.Open(path, catalogWritable: true);
        if (grammar != null) cache.Upsert(grammar);
        foreach (var verb in verbs) cache.Upsert(verb);
        cache.FlushAsync().Wait();
        return path;
    }

    private static CultCache OpenScoped(string path, bool readOnly)
    {
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));
        var cache = new CultCache(registry);
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(path, readOnly), AetheriaStores.CatalogTypes);
        return cache;
    }

    [Fact]
    public void A_catalog_with_the_grammar_and_verbs_opens_read_only_and_returns_every_field()
    {
        var verbs = new[]
        {
            Good("haul"),
            new VerseVerb { Name = "speak", RenderPath = VerseRenderPath.Conversation, Description = "Say a thing.", Roles = new VerseRole[0] },
            new VerseVerb { Name = "levy", RenderPath = VerseRenderPath.ShipAction, Description = "Raise a levy.",
                Roles = new[] { new VerseRole { Name = "who", Binds = VerseReferentKind.Population }, new VerseRole { Name = "boss", Binds = VerseReferentKind.Person }, new VerseRole { Name = "flag", Binds = VerseReferentKind.Faction } } },
        };
        var path = Author("roundtrip", new VerseGrammar { Revision = 7, Description = "Seven." }, verbs);

        using var cache = AetheriaStores.Open(path);
        var grammar = cache.GetGlobal<VerseGrammar>();
        Assert.Equal(7, grammar.Revision);
        Assert.Equal("Seven.", grammar.Description);
        Assert.Equal(verbs.Length, cache.GetAll<VerseVerb>().Count());
        foreach (var expected in verbs)
        {
            var actual = cache.GetByName<VerseVerb>(expected.Name);
            Assert.Equal(expected.RenderPath, actual.RenderPath);
            Assert.Equal(expected.Description, actual.Description);
            Assert.Equal(expected.Roles.Select(r => (r.Name, r.Binds)), actual.Roles.Select(r => (r.Name, r.Binds)));
        }
    }

    [Fact]
    public void A_catalog_without_the_grammar_global_is_refused()
    {
        var path = Author("nogrammar", null, Good("haul"));
        var error = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(path));
        Assert.Contains("aetheria.verse_grammar", error.Message);
    }

    public static IEnumerable<object[]> Refusals() => new[]
    {
        new object[] { "name with a space", (Action<VerseVerb>)(v => v.Name = "bad name"), "VerseVerb.Name", "bad name" },
        new object[] { "name starting with a digit", (Action<VerseVerb>)(v => v.Name = "9haul"), "VerseVerb.Name", "9haul" },
        new object[] { "upper-case name", (Action<VerseVerb>)(v => v.Name = "Haul"), "VerseVerb.Name", "Haul" },
        new object[] { "name one past 48 characters", (Action<VerseVerb>)(v => v.Name = "a" + new string('b', 48)), "VerseVerb.Name", new string('b', 48) },
        new object[] { "render path zero", (Action<VerseVerb>)(v => v.RenderPath = 0), "RenderPath", null },
        new object[] { "undefined render path", (Action<VerseVerb>)(v => v.RenderPath = (VerseRenderPath)3), "RenderPath", null },
        new object[] { "missing roles", (Action<VerseVerb>)(v => v.Roles = null), "Roles", null },
        new object[] { "duplicate role name", (Action<VerseVerb>)(v => v.Roles = new[] { Good("x").Roles[0], Good("x").Roles[0] }), "duplicate role name", null },
        new object[] { "role name with a capital", (Action<VerseVerb>)(v => v.Roles[0].Name = "Site"), "Roles.Name", "Site" },
        new object[] { "role name one past 32 characters", (Action<VerseVerb>)(v => v.Roles[0].Name = "r" + new string('s', 32)), "Roles.Name", new string('s', 32) },
        new object[] { "referent kind zero", (Action<VerseVerb>)(v => v.Roles[0].Binds = 0), "Roles.Binds", null },
        new object[] { "undefined referent kind", (Action<VerseVerb>)(v => v.Roles[0].Binds = (VerseReferentKind)99), "Roles.Binds", null },
    };

    // Every verb is held to the rule, wherever it sits among the others; a refusal names the verb (once its name is canonical)
    // and the field, and never repeats the value the author wrote.
    [Theory]
    [MemberData(nameof(Refusals))]
    public void Open_refuses_a_malformed_verb_in_any_position(string label, Action<VerseVerb> corrupt, string fragment, string echoedValue)
    {
        for (var position = 0; position < 3; position++)
        {
            var verbs = new[] { Good("alpha"), Good("beta"), Good("gamma") };
            corrupt(verbs[position]);
            var canonicalName = verbs[position].Name is "alpha" or "beta" or "gamma" ? verbs[position].Name : null;
            var path = Author($"refused-{label.Replace(' ', '-')}-{position}", new VerseGrammar { Revision = 1 }, verbs);

            var error = Assert.Throws<InvalidOperationException>(() => AetheriaStores.Open(path));
            Assert.Contains(fragment, error.Message);
            if (canonicalName != null) Assert.Contains($"\"{canonicalName}\"", error.Message);
            if (echoedValue != null) Assert.DoesNotContain(echoedValue, error.Message);
        }
    }

    [Fact]
    public void The_longest_canonical_names_are_accepted()
    {
        var verb = Good("v" + new string('x', 47));
        verb.Roles[0].Name = "r" + new string('y', 31);
        var path = Author("longest", new VerseGrammar { Revision = 1 }, verb);
        using var cache = AetheriaStores.Open(path);
        Assert.Single(cache.GetAll<VerseVerb>());
    }

    // ---- the AetherDb command ----

    // A scratch root holding a copy of the shipped catalog with its verse grammar removed: the catalog as it was before the
    // command ran. The catalog is stripped and read through a registry scoped to the shipped assembly, as
    // PiratesFactionCommandTests does.
    private string RootWithoutGrammar(params VerseVerb[] preexisting)
    {
        var gameData = Path.Combine(_root, "cmd", "GameData");
        Directory.CreateDirectory(gameData);
        var catalog = Path.Combine(gameData, "Aetheria.cc");
        File.Copy(Path.Combine(AetherDb.FindRoot(), "GameData", "Aetheria.cc"), catalog, true);
        using (var cache = OpenScoped(catalog, false))
        {
            foreach (var grammar in cache.GetAll<VerseGrammar>().ToArray()) Assert.True(cache.Remove(cache.RefOf(grammar).Key));
            foreach (var verb in cache.GetAll<VerseVerb>().ToArray()) Assert.True(cache.Remove(cache.RefOf(verb).Key));
            foreach (var verb in preexisting) cache.Upsert(verb);
            cache.FlushAsync().Wait();
        }
        return Path.Combine(_root, "cmd");
    }

    private static string CatalogOf(string root) => Path.Combine(root, "GameData", "Aetheria.cc");

    [Fact]
    public void A_dry_run_writes_nothing()
    {
        var root = RootWithoutGrammar();
        var before = File.ReadAllBytes(CatalogOf(root));
        Assert.Equal(0, VerseGrammarCommand.Run(apply: false, root: root));
        Assert.Equal(before, File.ReadAllBytes(CatalogOf(root)));
    }

    [Fact]
    public void Apply_lands_revision_one_and_one_verb_per_task_type_plus_speak_and_a_second_apply_changes_nothing()
    {
        var root = RootWithoutGrammar();
        Assert.Equal(0, VerseGrammarCommand.Run(apply: true, root: root));

        using (var cache = OpenScoped(CatalogOf(root), true))
        {
            Assert.Equal(1, cache.GetGlobal<VerseGrammar>().Revision);
            var verbs = cache.GetAll<VerseVerb>().ToDictionary(verb => verb.Name);
            var ships = Enum.GetValues(typeof(TaskType)).Cast<TaskType>().Where(task => task != TaskType.None)
                .Select(task => task.ToString().ToLowerInvariant()).OrderBy(name => name, StringComparer.Ordinal);
            Assert.Equal(ships.Append("speak").OrderBy(name => name, StringComparer.Ordinal), verbs.Keys.OrderBy(name => name, StringComparer.Ordinal));
            Assert.All(ships, name => Assert.Equal(VerseRenderPath.ShipAction, verbs[name].RenderPath));
            Assert.Equal(VerseRenderPath.Conversation, verbs["speak"].RenderPath);
            Assert.Empty(verbs["speak"].Roles);
            Assert.All(verbs.Values, VerseGrammarValidation.Validate);

            // The role signatures the spec names.
            string Sig(string name) => string.Join(", ", verbs[name].Roles.Select(r => $"{r.Name}: {r.Binds}"));
            Assert.Equal("site: Place, ore: Cargo", Sig("mine"));
            Assert.Equal("cargo: Cargo, destination: Place", Sig("haul"));
            Assert.Equal("target: Faction", Sig("tow"));
            Assert.Equal("protected: Faction, at: Place", Sig("defend"));
            Assert.Equal("target: Faction", Sig("attack"));
            Assert.Equal("region: Place", Sig("explore"));
        }

        var landed = File.ReadAllBytes(CatalogOf(root));
        Assert.Equal(0, VerseGrammarCommand.Run(apply: true, root: root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogOf(root)));
    }

    // A verb already in the catalog under a starter name but with other roles is replaced; one the starter set does not name
    // is left alone.
    [Fact]
    public void Apply_replaces_a_starter_verb_that_differs_only_in_its_roles_and_leaves_other_verbs()
    {
        var stale = new VerseVerb { Name = "mine", RenderPath = VerseRenderPath.ShipAction, Description = VerseGrammarCommand.StarterVerbs().Single(v => v.Name == "mine").Description,
            Roles = new[] { new VerseRole { Name = "site", Binds = VerseReferentKind.Place } } };
        var root = RootWithoutGrammar(stale, Good("levy"));
        Assert.Equal(0, VerseGrammarCommand.Run(apply: true, root: root));

        using var cache = OpenScoped(CatalogOf(root), true);
        Assert.Equal(2, cache.GetByName<VerseVerb>("mine").Roles.Length);
        Assert.NotNull(cache.GetByName<VerseVerb>("levy"));
        Assert.Equal(1, cache.GetAll<VerseVerb>().Count(verb => verb.Name == "mine"));
    }

    // The shipped catalog carries the command's output.
    [Fact]
    public void The_shipped_catalog_holds_revision_one_and_the_seven_starter_verbs()
    {
        using var cache = OpenScoped(Path.Combine(AetherDb.FindRoot(), "GameData", "Aetheria.cc"), true);
        Assert.Equal(1, cache.GetGlobal<VerseGrammar>().Revision);
        var starter = VerseGrammarCommand.StarterVerbs();
        Assert.Equal(7, starter.Length);
        var shipped = cache.GetAll<VerseVerb>().ToArray();
        Assert.Equal(starter.Length, shipped.Length);
        foreach (var expected in starter)
            Assert.True(VerseGrammarCommand.Same(expected, cache.GetByName<VerseVerb>(expected.Name)), expected.Name);
    }

    // ---- the fixture writer ----

    // The minimal catalog Ghostlight's grammar-reader reads: the grammar global, three verbs, two factions with an allegiance.
    // Written through a registry scoped to the shipped assembly, so it holds nothing from this test assembly. Record keys are
    // fixed (CultCache would mint a random one per record; the shipped catalog's are random, so a reader must key on the
    // document's Name, never on the record key).
    public static void WriteFixture(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        using var cache = OpenScoped(path, false);
        var firstKey = new CultRecordKey("faction-adrasteia");
        var secondKey = new CultRecordKey("faction-brannoch");
        var first = new Faction { Name = "Adrasteia", ShortName = "ADR", Description = "First.", PrimaryColor = new float3(1, .5f, .25f) };
        var second = new Faction { Name = "Brannoch", ShortName = "BRN", Description = "Second.", PrimaryColor = new float3(.25f, .5f, 1) };
        first.Allegiance[new CultRecordRef<Faction>(secondKey)] = .75f;
        second.Allegiance[new CultRecordRef<Faction>(firstKey)] = .25f;
        cache.Commit(batch =>
        {
            batch.Upsert(typeof(VerseGrammar), new VerseGrammar { Revision = 1, Description = "Fixture grammar." });
            batch.Upsert(typeof(VerseVerb), new VerseVerb { Name = "haul", RenderPath = VerseRenderPath.ShipAction, Description = "Carry cargo to a place.",
                Roles = new[] { new VerseRole { Name = "cargo", Binds = VerseReferentKind.Cargo }, new VerseRole { Name = "destination", Binds = VerseReferentKind.Place } } }, new CultRecordKey("verb-haul"));
            batch.Upsert(typeof(VerseVerb), new VerseVerb { Name = "attack", RenderPath = VerseRenderPath.ShipAction, Description = "Attack a faction.",
                Roles = new[] { new VerseRole { Name = "target", Binds = VerseReferentKind.Faction } } }, new CultRecordKey("verb-attack"));
            batch.Upsert(typeof(VerseVerb), new VerseVerb { Name = "speak", RenderPath = VerseRenderPath.Conversation, Description = "Say a thing.", Roles = new VerseRole[0] },
                new CultRecordKey("verb-speak"));
            batch.Upsert(typeof(Faction), first, firstKey);
            batch.Upsert(typeof(Faction), second, secondKey);
        });
        cache.FlushAsync().Wait();
    }

    // Everything the file says except CultCache's own wall-clock storedAt stamp: schema, record key and the grammar and
    // faction content a reader consumes.
    private static string[] Content(string path)
    {
        using var cache = OpenScoped(path, true);
        return cache.AllStoredDocuments.Select(stored => $"{stored.Descriptor.SchemaName}|{stored.Key.Value}|" + stored.Document switch
        {
            VerseGrammar grammar => $"{grammar.Revision}|{grammar.Description}",
            VerseVerb verb => $"{verb.Name}|{verb.RenderPath}|{verb.Description}|{string.Join(",", verb.Roles.Select(r => $"{r.Name}:{r.Binds}"))}",
            Faction faction => $"{faction.Name}|{faction.ShortName}|{faction.Description}|{string.Join(",", faction.Allegiance.Select(a => $"{a.Key.Key.Value}={a.Value}"))}",
            var other => throw new InvalidOperationException($"The fixture holds a {other.GetType().Name}."),
        }).OrderBy(line => line, StringComparer.Ordinal).ToArray();
    }

    // Two writes of the fixture say the same thing: the same records under the same keys with the same content. (The file's
    // bytes differ by CultCache's storedAt stamps, which it mints from the wall clock and no caller can set.) So Ghostlight's
    // copy of it is reproducible and a diff of its content means the grammar changed. With AETHERIA_VERSE_FIXTURE_PATH set,
    // the fixture is left at that path for Ghostlight to copy.
    [Fact]
    public void Fixture_writer_is_deterministic_and_emits_what_the_grammar_reader_needs()
    {
        var left = PathOf("fixture-left");
        var right = PathOf("fixture-right");
        WriteFixture(left);
        WriteFixture(right);
        var content = Content(left);
        Assert.Equal(content, Content(right));
        Assert.Equal(6, content.Length);

        var emitted = Environment.GetEnvironmentVariable(FixturePathVariable);
        if (!string.IsNullOrEmpty(emitted))
        {
            WriteFixture(emitted);
            Assert.Equal(content, Content(emitted));
        }

        using var cache = OpenScoped(left, true);
        Assert.Equal(1, cache.GetGlobal<VerseGrammar>().Revision);
        Assert.Equal(new[] { "attack", "haul", "speak" }, cache.GetAll<VerseVerb>().Select(verb => verb.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(cache.GetAll<VerseVerb>(), VerseGrammarValidation.Validate);
        var factions = cache.GetAll<Faction>().ToArray();
        Assert.Equal(2, factions.Length);
        Assert.All(factions, faction => Assert.Single(faction.Allegiance));
        var adrasteia = cache.GetByName<Faction>("Adrasteia");
        Assert.Equal(.75f, adrasteia.Allegiance[cache.RefOf(cache.GetByName<Faction>("Brannoch"))]);
    }
}
