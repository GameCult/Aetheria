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
        // assembly (TestCatalog.Registry); the shipped catalog already holds the grammar global.
        var registry = TestCatalog.Registry();
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
    }

    public void Dispose() => Directory.Delete(_root, true);

    private int AllFactionsButThePirates()
    {
        var db = AetherDb.Open(root: _root);
        try { return db.Cache.GetAll<Faction>().Count(f => f.Name != "Pirates"); }
        finally { db.Cache.Dispose(); }
    }

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
        Assert.Equal(AllFactionsButThePirates(), faction.Allegiance.Count);
        Assert.All(faction.Allegiance.Values, value => Assert.Equal(1f, value));

        var db = AetherDb.Open(root: _root);
        try
        {
            var expected = new (string Design, string Name, string Description)[]
            {
                ("Autocannon", "🫳🔫", "Serial number filed off. Still shoots."),
                ("Long Slug Rifle", "🪦🤠", "The previous owner had no further use for it."),
                ("Cell Blaster", "🧾🚫", "Warranty void where prohibited, which is everywhere."),
                ("Slug Rifle", "🎨🙈", "The logo underneath is somebody else's problem."),
                ("Incendiary Rocket Pod", "🔥", "Turns out we did start the fire after all."),
                ("Targeting Computer", "👀🎯", "Somebody else's eyes. They still work."),
                ("Small Drive", "🔑🏃💨", "Hotwired. Do not ask about the ignition."),
                ("Cargo Bay", "📦🤫", "Contents not as declared."),
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

    // An existing hand-authored Pirates record is topped up, never rewritten (follow-up pirates-allegiance-and-identity): it keeps
    // its description, short name and record key and gains no products; after a faction is added, apply gives allegiance 1.0 to
    // that faction alone; a second apply leaves the file bytes unchanged and a dry run writes nothing.
    [Fact]
    public void An_existing_Pirates_faction_is_topped_up_with_allegiance_and_nothing_else()
    {
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        db.Cache.Commit(batch => batch.Upsert(typeof(Faction), new Faction { Name = "Pirates", ShortName = "Mine", Description = "authored by hand" }));
        var key = db.Cache.RefOf(db.Cache.GetAll<Faction>().Single(f => f.Name == "Pirates")).Key;
        db.Cache.Dispose();

        var beforeDry = File.ReadAllBytes(CatalogPath);
        Assert.Equal(0, Program.PiratesFaction(apply: false, root: _root));
        Assert.Equal(beforeDry, File.ReadAllBytes(CatalogPath));

        Assert.Equal(0, Program.PiratesFaction(apply: true, root: _root));
        var (pirates, products) = Read();
        var record = Assert.Single(pirates);
        Assert.Equal("authored by hand", record.Description);
        Assert.Equal("Mine", record.ShortName);
        Assert.Empty(products);
        var topped = File.ReadAllBytes(CatalogPath);

        var late = AetherDb.Open(catalogWritable: true, root: _root);
        late.Cache.Commit(batch => batch.Upsert(typeof(Faction), new Faction { Name = "Latecomer", ShortName = "Late" }));
        var lateRef = late.Cache.RefOf(late.Cache.GetAll<Faction>().Single(f => f.Name == "Latecomer"));
        var others = late.Cache.GetAll<Faction>().Count(f => f.Name != "Pirates");
        late.Cache.Dispose();

        Assert.Equal(0, Program.PiratesFaction(apply: true, root: _root));
        var check = AetherDb.Open(root: _root);
        try
        {
            var after = check.Cache.GetAll<Faction>().Single(f => f.Name == "Pirates");
            Assert.Equal(key, check.Cache.RefOf(after).Key);
            Assert.Equal("authored by hand", after.Description);
            Assert.Equal(others, after.Allegiance.Count);
            Assert.Equal(1f, after.Allegiance[lateRef]);
            Assert.All(after.Allegiance.Values, value => Assert.Equal(1f, value));
        }
        finally { check.Cache.Dispose(); }

        var settled = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(topped, settled);
        Assert.Equal(0, Program.PiratesFaction(apply: true, root: _root));
        Assert.Equal(settled, File.ReadAllBytes(CatalogPath));
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
}
