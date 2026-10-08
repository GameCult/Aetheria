/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using System.Linq;
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
        var db = AetherDb.Open(catalogWritable: true, root: _root);
        var pirates = db.Cache.GetAll<Faction>().Single(faction => faction.Name == "Pirates");
        var ours = db.Cache.RefOf(pirates);
        foreach (var product in db.Cache.GetAll<FactionProductData>().Where(product => product.Manufacturer.Equals(ours)).ToArray())
            Assert.True(db.Cache.Remove(db.Cache.RefOf(product).Key));
        Assert.True(db.Cache.Remove(ours.Key));
        db.Cache.Dispose();
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
        Assert.Single(pirates);
        Assert.Equal(8, products.Length);

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
}
