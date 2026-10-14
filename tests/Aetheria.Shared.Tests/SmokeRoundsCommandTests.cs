using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// AetherDb's smoke-rounds command (aetheria-release, cut scenarios-smoke-rounds), run on a scratch copy of the shipped catalog
// with the five smoke weapons removed from it.
public sealed class SmokeRoundsCommandTests : IDisposable
{
    private static readonly string[] Names = { "Smoke Proximity", "Smoke Delayed", "Smoke Refused", "Smoke Refused Beam", "Smoke Refused Charge" };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "smoke-rounds-command-" + Guid.NewGuid().ToString("N"));
    private string CatalogPath => Path.Combine(_root, "GameData", "Aetheria.cc");

    // The five designs as the shipped catalog holds them, taken before they are removed from the scratch copy: apply must write
    // exactly these, so the command stays the replay of what ships.
    private readonly Dictionary<string, byte[]> _shipped = new Dictionary<string, byte[]>();

    private static byte[] Pack(WeaponItemData design) =>
        MessagePackSerializer.Serialize(design, CultDocumentMessagePackSerialization.OptionsFor(typeof(WeaponItemData).Assembly));

    public SmokeRoundsCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "GameData"));
        Directory.CreateDirectory(Path.Combine(_root, "Assets", "Resources"));
        var shipped = AetherDb.FindRoot();
        File.Copy(Path.Combine(shipped, "GameData", "Aetheria.cc"), CatalogPath);
        File.Copy(Path.Combine(shipped, "Assets", "Resources", "Settings.asset"), Path.Combine(_root, "Assets", "Resources", "Settings.asset"));
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(CatalogPath), AetheriaStores.CatalogTypes);
        foreach (var weapon in cache.GetAll<WeaponItemData>().Where(w => Names.Contains(w.Name)).ToArray())
        {
            _shipped[weapon.Name] = Pack(weapon);
            Assert.True(cache.Remove(cache.RefOf(weapon).Key));
        }
        Assert.Equal(Names.OrderBy(n => n), _shipped.Keys.OrderBy(n => n));
        cache.FlushAsync().Wait();
    }

    public void Dispose() => Directory.Delete(_root, true);

    private (WeaponItemData[] Smoke, WeaponItemData LightLaser, WeaponItemData ChargedScatterBlaster, int Products) Read()
    {
        var db = AetherDb.Open(root: _root);
        try
        {
            var smoke = db.Cache.GetAll<WeaponItemData>().Where(w => Names.Contains(w.Name)).ToArray();
            var keys = smoke.Select(w => db.Cache.RefOf(w).Key).ToArray();
            var products = db.Cache.GetAll<FactionProductData>().Count(p => keys.Any(k => p.Design.Key.Equals(k)));
            return (smoke, db.Cache.GetAll<WeaponItemData>().Single(w => w.Name == "Light Laser"), db.Cache.GetAll<WeaponItemData>().Single(w => w.Name == "Charged Scatter Blaster"), products);
        }
        finally { db.Cache.Dispose(); }
    }

    [Fact]
    public void A_dry_run_writes_nothing_and_apply_lands_five_productless_designs_once()
    {
        var before = File.ReadAllBytes(CatalogPath);
        Assert.Empty(Read().Smoke);

        Assert.Equal(0, Program.SmokeRoundsCatalog(apply: false, root: _root));
        Assert.Equal(before, File.ReadAllBytes(CatalogPath));
        Assert.Empty(Read().Smoke);

        Assert.Equal(0, Program.SmokeRoundsCatalog(apply: true, root: _root));
        var landed = File.ReadAllBytes(CatalogPath);
        Assert.NotEqual(before, landed);
        var (smoke, spectra, chargeBlast, products) = Read();
        Assert.Equal(Names.OrderBy(n => n), smoke.Select(w => w.Name).OrderBy(n => n));
        Assert.Equal(0, products);

        // Replaying the command on the catalog without them writes the records the shipped catalog holds (the record keys are
        // CultCache's to mint and differ run to run; the designs do not).
        foreach (var design in smoke) Assert.True(_shipped[design.Name].AsSpan().SequenceEqual(Pack(design)), $"{design.Name} differs from the shipped catalog's record");

        WeaponItemData Design(string name) => smoke.Single(w => w.Name == name);
        WeaponData Data(string name) => Design(name).Behaviors.OfType<WeaponData>().Single();

        // The operator's own values: the fuse and blast of each, Smoke Delayed's two cells of penetration, Smoke Refused's range of 20.
        Assert.Equal((WeaponFuse.Proximity, 8f), (Design("Smoke Proximity").Fuse.Value, Design("Smoke Proximity").BlastRadius.Value));
        Assert.Equal((WeaponFuse.Delayed, 4f, 2f, 2f), (Design("Smoke Delayed").Fuse.Value, Design("Smoke Delayed").BlastRadius.Value, Data("Smoke Delayed").Penetration.Min, Data("Smoke Delayed").Penetration.Max));
        Assert.Equal((WeaponFuse.Proximity, 30f, 20f, 20f), (Design("Smoke Refused").Fuse.Value, Design("Smoke Refused").BlastRadius.Value, Data("Smoke Refused").Range.Min, Data("Smoke Refused").Range.Max));

        // Light Laser clones are energy, 1x2, with the Light Laser's own gear; the charged one is a Charged Scatter Blaster clone on its 2x2 energy mount.
        foreach (var name in new[] { "Smoke Proximity", "Smoke Delayed", "Smoke Refused", "Smoke Refused Beam" })
            Assert.Equal((spectra.Hardpoint, spectra.Shape.Width, spectra.Shape.Height, spectra.Mass), (Design(name).Hardpoint, Design(name).Shape.Width, Design(name).Shape.Height, Design(name).Mass));
        Assert.Equal((chargeBlast.Hardpoint, chargeBlast.Shape.Width, chargeBlast.Shape.Height), (Design("Smoke Refused Charge").Hardpoint, 2, 2));

        // The beam is a constant weapon and the charge a charged one, each fused with a Range below its BlastRadius.
        Assert.IsType<ConstantWeaponData>(Data("Smoke Refused Beam"));
        Assert.IsType<ChargedWeaponData>(Data("Smoke Refused Charge"));
        foreach (var name in new[] { "Smoke Refused Beam", "Smoke Refused Charge" })
        {
            Assert.NotNull(Design(name).Fuse);
            Assert.True(Data(name).Range.Max < Design(name).BlastRadius, $"{name}'s range is not below its blast radius");
        }

        // A second apply finds them authored and writes nothing; so does a dry run.
        Assert.Equal(0, Program.SmokeRoundsCatalog(apply: true, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Equal(0, Program.SmokeRoundsCatalog(apply: false, root: _root));
        Assert.Equal(landed, File.ReadAllBytes(CatalogPath));
        Assert.Equal(5, Read().Smoke.Length);
    }
}
