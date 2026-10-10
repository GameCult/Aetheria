/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// AetherDb's pirates-faction command (aetheria-release, cut pirates-record), run on a scratch copy of the shipped catalog
// with the Pirates removed from it.
public sealed class PiratesFactionCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pirates-command-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    public PiratesFactionCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        File.Copy(Path.Combine(AetherDb.FindRoot(), "GameData", "Aetheria.cc"), CatalogPath);
        // AetherDb.Open composes the process-wide registry. So the copy is stripped through a registry scoped to the shipped
        // assembly (see FireControlCut7Tests) and then given the grammar global every populated catalog must hold.
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));
        using (var cache = new CultCache(registry))
        {
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
            var pirates = cache.GetAll<Faction>().Single(faction => faction.Name == "Pirates");
            var ours = cache.RefOf(pirates);
            foreach (var product in cache.GetAll<FactionProductData>().Where(product => product.Manufacturer.Equals(ours)).ToArray())
                Assert.True(cache.Remove(cache.RefOf(product).Key));
            Assert.True(cache.Remove(ours.Key));
            cache.FlushAsync().Wait();
        }
        using (var cache = new CultCache())
        {
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
            cache.Upsert(new VerseGrammar { Revision = 1 });
            cache.FlushAsync().Wait();
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    private (Faction[] Pirates, FactionProductData[] Products) Read()
    {
        var db = AetherDb.Open(root: _root);
        try
        {
            var pirates = db.Cache.GetAll<Faction>().Where(faction => faction.Name == "Pirates").ToArray();
            var products = pirates.Length == 0 ? new FactionProductData[0]
                : db.Cache.GetAll<FactionProductData>().Where(product => product.Manufacturer.Equals(db.Cache.RefOf(pirates[0]))).ToArray();
            return (pirates, products);
        }
        finally { db.Cache.Dispose(); }
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_lands_the_record_once()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Equal(0, Program.PiratesFaction(apply: false, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
        Assert.Empty(Read().Pirates);

        Assert.Equal(0, Program.PiratesFaction(apply: true, root: _root));
        var landed = File.ReadAllBytes(CatalogPath);
        var (pirates, products) = Read();
        var faction = Assert.Single(pirates);
        Assert.Equal("Pirates", faction.ShortName);
        Assert.StartsWith("A loose coalition of crews and hidden docks", faction.Description);
        Assert.Equal(3, faction.InfluenceDistance);
        Assert.Equal(new CultMath.float3(.42f, .40f, .37f), faction.PrimaryColor);
        Assert.Equal(new CultMath.float3(1f, .45f, .05f), faction.SecondaryColor);
        Assert.Equal(12, faction.Allegiance.Count);
        Assert.All(faction.Allegiance.Values, value => Assert.Equal(1f, value));

        var db = AetherDb.Open(root: _root);
        try
        {
            Assert.Equal("pleiades", db.Cache.Get(faction.GeonameFile).Name);
            var expected = new (string Design, string Name, string Description)[]
            {
                ("Autocannon", "🫳🔫", "Serial number filed off. Still shoots."),
                ("Earp", "🪦🤠", "The previous owner had no further use for it."),
                ("FastBlast+-", "🧾🚫", "Warranty void where prohibited, which is everywhere."),
                ("6k Shooter", "🎨🙈", "The logo underneath is somebody else's problem."),
                ("scorched void policy", "🔥", "Turns out we did start the fire after all."),
                ("Targeting Computer", "👀🎯", "Somebody else's eyes. They still work."),
                ("Small Drive", "🔑🏃💨", "Hotwired. Do not ask about the ignition."),
                ("Store-All Plus", "📦🤫", "Contents not as declared."),
            };
            Assert.Equal(expected.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal), products.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
            foreach (var (designName, name, description) in expected)
            {
                var product = products.Single(p => p.Name == name);
                var design = (CraftedItemData) db.Cache.Get(product.Design);
                Assert.Equal(designName, design.Name);
                Assert.Equal(description, product.Description);
                Assert.Equal((design.Roles ?? new List<ItemRole>()).Select(role => role.Name), (product.Roles ?? new List<ProductRole>()).Select(role => role.Role));
                Assert.All(product.Roles, role => { Assert.Equal(.45f, role.Mean); Assert.Equal(.22f, role.StandardDeviation); });
            }
        }
        finally { db.Cache.Dispose(); }

        // A second apply finds the faction and changes nothing.
        Assert.Equal(0, Program.PiratesFaction(apply: true, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
    }

    [Fact]
    public void An_existing_Pirates_faction_is_left_as_it_is()
    {
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        db.Cache.Commit(batch => batch.Upsert(typeof(Faction), new Faction { Name = "Pirates", ShortName = "Mine", Description = "authored by hand" }));
        db.Cache.Dispose();

        Assert.Equal(0, Program.PiratesFaction(apply: true, root: _root));
        var (pirates, products) = Read();
        Assert.Equal("authored by hand", Assert.Single(pirates).Description);
        Assert.Empty(products);
    }

    [Fact]
    public void A_missing_design_refuses_and_lands_nothing()
    {
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        var autocannon = db.Cache.GetAll<WeaponItemData>().Single(weapon => weapon.Name == "Autocannon");
        autocannon.Name = "Autocannon (renamed)";
        db.Cache.Commit(batch => batch.Upsert(typeof(WeaponItemData), autocannon, db.Cache.RefOf(autocannon).Key));
        db.Cache.Dispose();

        Assert.Throws<InvalidOperationException>(() => Program.PiratesFaction(apply: true, root: _root));
        Assert.Empty(Read().Pirates);
    }

    [Fact]
    public void A_missing_name_file_refuses_and_lands_nothing()
    {
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        var pleiades = db.Cache.GetAll<NameFile>().Single(file => file.Name == "pleiades");
        Assert.True(db.Cache.Remove(db.Cache.RefOf(pleiades).Key));
        db.Cache.FlushAsync().Wait();
        db.Cache.Dispose();

        Assert.Throws<InvalidOperationException>(() => Program.PiratesFaction(apply: true, root: _root));
        Assert.Empty(Read().Pirates);
    }
}
