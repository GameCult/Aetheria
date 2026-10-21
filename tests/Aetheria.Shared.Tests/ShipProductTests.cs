using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;

// A package carries its products (ruling package-carries-product): ship.cc homes FactionProductData at
// mod-product:<id>:<maker key>, compose checks makers and names, and ship-authoring product/price write them.
// The commands read the process working directory to find GameData, so these run alone.
[Collection("process working directory")]
public sealed class ShipProductTests : IDisposable
{
    private readonly TempDirectory _root = new TempDirectory();
    private readonly string _previous = Environment.CurrentDirectory;
    private readonly CultRecordKey _acme;
    private readonly CultRecordKey _rival;
    private string Shipped => Path.Combine(_root.Path, "GameData", "Aetheria.cc");
    private string Mods => Path.Combine(_root.Path, "GameData", "Mods");
    private string Derived => Path.Combine(_root.Path, "Derived", "Aetheria.modded.cc");

    public ShipProductTests()
    {
        Directory.CreateDirectory(Mods);
        Directory.CreateDirectory(Path.GetDirectoryName(Shipped));
        using var cache = AetheriaStores.Open(Shipped, catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        _acme = cache.Upsert(new Faction { Name = "Acme", ShortName = "ACM" }).Key;
        _rival = cache.Upsert(new Faction { Name = "Rival", ShortName = "RVL" }).Key;
        cache.Upsert(new Faction { Name = "Twin A", ShortName = "TWN" });
        cache.Upsert(new Faction { Name = "Twin B", ShortName = "TWN" });
        var wasp = cache.Upsert(new HullData { Name = "Wasp", Shape = ShipAuthoringTests.Fixture().Hull.Shape, Price = 10 });
        cache.Upsert(new FactionProductData
        {
            Name = "Taken",
            Design = new CultRecordRef<CraftedItemData>(wasp.Key),
            Manufacturer = new CultRecordRef<Faction>(_acme)
        });
        cache.FlushAsync().Wait();
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _previous;
        _root.Dispose();
    }

    private static FactionProductData Product(string name, CultRecordKey maker, string shipId) => new FactionProductData
    {
        Name = name,
        Design = new CultRecordRef<CraftedItemData>(ShipModCatalog.HullKey(shipId)),
        Manufacturer = new CultRecordRef<Faction>(maker)
    };

    // A valid package at Mods/<id>, priced, selling the given products through the one product writer.
    private string Package(string id, string hullName, int price, params FactionProductData[] products)
    {
        var directory = ShipFixture.WritePackage(Mods, id, hullName, tweak: ship =>
        {
            ship.Hull.Price = price;
            ship.Hull.Roles = new List<ItemRole> { new ItemRole { Name = "frame" }, new ItemRole { Name = "drive" } };
        });
        var path = Path.Combine(directory, "ship.cc");
        using var cache = ShipAuthoringStore.Open(path, writable: true);
        foreach (var product in products) ShipAuthoringStore.WriteProduct(cache, id, product);
        cache.FlushAsync().Wait();
        return path;
    }

    // A record stored where the caller says, bypassing the writer, to build the files the loader must refuse.
    private static void Raw(string path, FactionProductData product, CultRecordKey key)
    {
        using var cache = ShipAuthoringStore.Open(path, writable: true);
        cache.UpsertAsync(typeof(FactionProductData), product, key).GetAwaiter().GetResult();
        cache.FlushAsync().Wait();
    }

    [Fact]
    public void ComposeCopiesEveryProductAndEachMakerIsOfferedTheHull()
    {
        var path = Package("mod.skiff", "Skiffhull", 100, Product("Acme Skiff", _acme, "mod.skiff"), Product("Rival Skiff", _rival, "mod.skiff"));
        var (_, _, written) = ShipAuthoringStore.Read(path);
        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Equal(new[] { "mod.skiff" }, composition.Included);
        using var cache = AetheriaStores.Open(Derived);
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(FactionProductData).Assembly);
        foreach (var (maker, name) in new[] { (_acme, "Acme Skiff"), (_rival, "Rival Skiff") })
        {
            var product = cache.Get<FactionProductData>(ShipModCatalog.ProductKey("mod.skiff", maker));
            Assert.Equal(name, product.Name);
            Assert.Equal(MessagePack.MessagePackSerializer.Serialize(written.Single(w => w.Name == name), options),
                MessagePack.MessagePackSerializer.Serialize(product, options));
            // What LoadoutGenerator.AvailableProducts asks of a candidate: the design resolves to the hull, and it has a price.
            var design = Assert.IsType<HullData>(cache.Get(product.Design));
            Assert.Equal("Skiffhull", design.Name);
            Assert.True(design.Price > 0);
        }
    }

    [Fact]
    public void APackageWithAnUnknownMakerIsExcluded()
    {
        Package("mod.good", "Good", 100, Product("Good Skiff", _acme, "mod.good"));
        Package("mod.bad", "Bad", 100, Product("Bad Skiff", new CultRecordKey("faction:nobody"), "mod.bad"));

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Equal(new[] { "mod.good" }, composition.Included);
        var excluded = Assert.Single(composition.Excluded);
        Assert.Equal("mod.bad", excluded.Package);
        Assert.Contains("manufacturer", excluded.Reason);
    }

    [Fact]
    public void AProductKeyAlreadyInTheCatalogExcludesThePackage()
    {
        // A catalog record already at this ship's product key for this maker, under another name.
        using (var shipped = AetheriaStores.Open(Shipped, catalogWritable: true))
        {
            var held = Product("Held", _acme, "mod.keyed");
            shipped.UpsertAsync(typeof(FactionProductData), held, ShipModCatalog.ProductKey("mod.keyed", _acme)).GetAwaiter().GetResult();
            shipped.FlushAsync().Wait();
        }
        using (var shipped = AetheriaStores.Open(Shipped, catalogWritable: true))
        {
            var held = Product("Held Too", _rival, "mod.keyedr");
            shipped.UpsertAsync(typeof(FactionProductData), held, ShipModCatalog.ProductKey("mod.keyedr", _rival)).GetAwaiter().GetResult();
            shipped.FlushAsync().Wait();
        }
        // The colliding product is one of two, and each maker takes its turn at being the colliding one, so the check
        // cannot pass by looking at whichever product is listed first.
        Package("mod.keyed", "Keyedhull", 100, Product("Fresh Name", _acme, "mod.keyed"), Product("Fresh Rebrand", _rival, "mod.keyed"));
        Package("mod.keyedr", "Keyedrhull", 100, Product("Fresh Other", _acme, "mod.keyedr"), Product("Fresh Rival", _rival, "mod.keyedr"));
        Package("mod.fine", "Finehull", 100, Product("Fine", _acme, "mod.fine"));

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Equal(new[] { "mod.fine" }, composition.Included);
        Assert.Equal(new[] { "mod.keyed", "mod.keyedr" }, composition.Excluded.Select(e => e.Package).OrderBy(p => p, StringComparer.Ordinal));
        Assert.All(composition.Excluded, e => Assert.Contains("product key", e.Reason));
    }

    [Fact]
    public void AProductNameCollisionExcludesThePackage()
    {
        Package("mod.shipped", "Shippedhull", 100, Product("Taken", _rival, "mod.shipped"));
        Package("mod.a", "Ahull", 100, Product("Twin", _acme, "mod.a"));
        Package("mod.b", "Bhull", 100, Product("Twin", _rival, "mod.b"));
        Package("mod.fine", "Finehull", 100, Product("Fine", _acme, "mod.fine"));

        var composition = ShipModCatalog.Compose(Shipped, Derived, Mods);

        Assert.Equal(new[] { "mod.fine" }, composition.Included);
        Assert.Equal(new[] { "mod.a", "mod.b", "mod.shipped" }, composition.Excluded.Select(e => e.Package).OrderBy(p => p, StringComparer.Ordinal));
        Assert.All(composition.Excluded, e => Assert.Contains("product name", e.Reason));
    }

    [Fact]
    public void AProductNotAtItsMakersKeyIsRefused()
    {
        string Fresh(string id) => Package(id, id, 100);
        void Refused(string path, string message) =>
            Assert.Contains(message, Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Load(path)).Message);

        var misplaced = Fresh("mod.misplaced");
        Raw(misplaced, Product("P", _acme, "mod.misplaced"), new CultRecordKey("mod-product:elsewhere"));
        Refused(misplaced, "must be stored at");

        // A second product by one maker cannot be stored: the key is the maker's, so it is stored there or misplaced.
        var second = Package("mod.second", "second", 100, Product("First", _acme, "mod.second"));
        Raw(second, Product("Second", _acme, "mod.second"), new CultRecordKey("mod-product:mod.second:other"));
        Refused(second, "must be stored at");

        var wrongDesign = Fresh("mod.design");
        Raw(wrongDesign, Product("P", _acme, "mod.other"), ShipModCatalog.ProductKey("mod.design", _acme));
        Refused(wrongDesign, "as its design");

        var noMaker = Fresh("mod.nomaker");
        Raw(noMaker, Product("P", default, "mod.nomaker"), new CultRecordKey("mod-product:mod.nomaker:"));
        Refused(noMaker, "manufacturer");

        var modMaker = Fresh("mod.modmaker");
        Raw(modMaker, Product("P", ShipModCatalog.HullKey("mod.x"), "mod.modmaker"), ShipModCatalog.ProductKey("mod.modmaker", ShipModCatalog.HullKey("mod.x")));
        Refused(modMaker, "manufacturer");

        // Writing a maker's product again replaces it.
        var replaced = Package("mod.replaced", "replaced", 100, Product("Old", _acme, "mod.replaced"));
        using (var cache = ShipAuthoringStore.Open(replaced, writable: true))
        {
            ShipAuthoringStore.WriteProduct(cache, "mod.replaced", Product("New", _acme, "mod.replaced"));
            cache.FlushAsync().Wait();
        }
        Assert.Equal("New", Assert.Single(ShipAuthoringStore.Load(replaced).Products).Name);
    }

    [Fact]
    public void ASoldPackageHullNeedsAPrice()
    {
        var sold = Package("mod.sold", "Sold", 0, Product("Sold Skiff", _acme, "mod.sold"));
        Assert.Contains("price above 0", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Read(sold)).Message);
        Assert.Single(ShipAuthoringStore.Load(sold).Products);

        // Below 0 is not sellable either.
        var negative = Package("mod.negative", "Negative", -5, Product("Negative Skiff", _acme, "mod.negative"));
        Assert.Contains("price above 0", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Read(negative)).Message);

        // The smallest sellable price: the rule is "above 0", so 1 passes.
        Assert.Single(ShipAuthoringStore.Read(Package("mod.cheap", "Cheap", 1, Product("Cheap Skiff", _acme, "mod.cheap"))).Products);

        Assert.Empty(ShipAuthoringStore.Read(Package("mod.console", "Console", 0)).Products);
        Assert.Single(ShipAuthoringStore.Read(Package("mod.priced", "Priced", 5, Product("Priced Skiff", _acme, "mod.priced"))).Products);
    }

    [Fact]
    public void AnUnnamedOrDuplicatedProductNameIsRefused()
    {
        var blank = Package("mod.blank", "Blank", 100, Product(" ", _acme, "mod.blank"));
        Assert.Contains("needs a name", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Read(blank)).Message);
        // The unnamed product is one of two, once under each maker, so a check of the first product alone passes neither.
        foreach (var (id, unnamed, named) in new[] { ("mod.blank-acme", _acme, _rival), ("mod.blank-rival", _rival, _acme) })
        {
            var second = Package(id, id, 100, Product("Named", named, id), Product(" ", unnamed, id));
            Assert.Contains("needs a name", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Read(second)).Message);
        }
        var twice = Package("mod.twice", "Twice", 100, Product("Same", _acme, "mod.twice"), Product("Same", _rival, "mod.twice"));
        Assert.Contains("unique", Assert.Throws<InvalidOperationException>(() => ShipAuthoringStore.Read(twice)).Message);
    }

    [Fact]
    public void AThirdKindOfRecordIsRefused()
    {
        var withFaction = Package("mod.faction", "Faction", 100);
        var cache = new CultCache();
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(withFaction, false),
            new[] { typeof(HullData), typeof(ShipAuthoring), typeof(FactionProductData), typeof(Faction) });
        using (cache)
        {
            cache.Upsert(new Faction { Name = "Stowaway" });
            cache.FlushAsync().Wait();
        }
        Assert.Contains("no store", Assert.ThrowsAny<Exception>(() => ShipAuthoringStore.Load(withFaction)).Message);

        Assert.Single(ShipAuthoringStore.Load(Package("mod.products", "Products", 100, Product("P", _acme, "mod.products"))).Products);
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var (previousOut, previousError) = (Console.Out, Console.Error);
        Console.SetOut(output);
        Console.SetError(error);
        try { return (ShipAuthoringCommands.Run(args), output.ToString(), error.ToString()); }
        finally { Console.SetOut(previousOut); Console.SetError(previousError); }
    }

    [Fact]
    public void ProductAndPriceCommands()
    {
        Environment.CurrentDirectory = _root.Path;
        var path = Package("mod.skiff", "Skiffhull", 0);
        void RefusedAndUntouched(params string[] args)
        {
            var before = File.ReadAllBytes(path);
            Assert.Equal(1, Run(args).Code);
            Assert.Equal(before, File.ReadAllBytes(path));
        }

        RefusedAndUntouched("product", path, "Nobody", "--name", "X");
        RefusedAndUntouched("product", path, "TWN", "--name", "X");
        RefusedAndUntouched("product", path, "ACM");
        RefusedAndUntouched("product", path, "ACM", "--name", " ");
        RefusedAndUntouched("product", path, "ACM", "--remove");
        RefusedAndUntouched("product", path, "ACM", "--not-offered");
        RefusedAndUntouched("product", path, "ACM", "--name", "--not-offered");

        var (code, _, error) = Run("product", path, "ACM", "--name", "Acme Skiff", "--description", "Reliable.");
        Assert.True(code == 0, error);
        var (hull, _, products) = ShipAuthoringStore.Load(path);
        var product = Assert.Single(products);
        using (var reopened = ShipAuthoringStore.Open(path))
            Assert.Equal(ShipModCatalog.ProductKey("mod.skiff", _acme), reopened.TryGetHandle(Assert.Single(reopened.GetAll<FactionProductData>())).Value.Key);
        Assert.Equal(_acme, product.Manufacturer.Key);
        Assert.Equal(ShipModCatalog.HullKey("mod.skiff"), product.Design.Key);
        Assert.Equal(hull.Roles.Select(role => role.Name), product.Roles.Select(role => role.Role));
        Assert.Equal("Reliable.", product.Description);
        Assert.False(product.NotOffered);

        // --not-offered writes the product off the market (ruling off-market-is-unoffered-product), with or without a description.
        var (offCode, offOutput, offError) = Run("product", path, "ACM", "--name", "Acme Boss Skiff", "--not-offered");
        Assert.True(offCode == 0, offError);
        Assert.Contains("not offered", offOutput);
        var off = Assert.Single(ShipAuthoringStore.Load(path).Products);
        Assert.True(off.NotOffered);
        Assert.Equal("Acme Boss Skiff", off.Name);
        Assert.Null(off.Description);
        Assert.Equal(0, Run("product", path, "ACM", "--name", "Acme Boss Skiff", "--description", "Unsold.", "--not-offered").Code);
        off = Assert.Single(ShipAuthoringStore.Load(path).Products);
        Assert.True(off.NotOffered);
        Assert.Equal("Unsold.", off.Description);

        // The maker by name replaces the product by short name: a maker has one product of the hull.
        // Written again without the flag, it is back on the market.
        var (renamedCode, renamedOutput, _) = Run("product", path, "Acme", "--name", "Renamed");
        Assert.Equal(0, renamedCode);
        Assert.DoesNotContain("not offered", renamedOutput);
        var renamed = Assert.Single(ShipAuthoringStore.Load(path).Products);
        Assert.Equal("Renamed", renamed.Name);
        Assert.False(renamed.NotOffered);

        RefusedAndUntouched("price", path, "-1");
        RefusedAndUntouched("price", path, "1.5");
        RefusedAndUntouched("price", path, "cheap");
        Assert.Equal(0, Run("price", path, "250").Code);
        Assert.Equal(250, ShipAuthoringStore.Read(path).Hull.Price);

        RefusedAndUntouched("product", path, "ACM", "--remove", "--not-offered");
        Assert.Equal(0, Run("product", path, "ACM", "--remove").Code);
        Assert.Empty(ShipAuthoringStore.Load(path).Products);
        RefusedAndUntouched("product", path, "ACM", "--remove");
    }

    [Fact]
    public void ValidateJudgesProductsOnADraftAndInspectListsThem()
    {
        Environment.CurrentDirectory = _root.Path;
        // A draft is a package with no model yet: validate then judges its records alone.
        string Draft(string path)
        {
            File.Delete(Path.Combine(Path.GetDirectoryName(path), "skiff.glb"));
            return path;
        }
        string Refusal(string path)
        {
            var (code, _, error) = Run("validate", path);
            Assert.Equal(1, code);
            return error;
        }

        Assert.Contains("price above 0", Refusal(Draft(Package("mod.free", "Free", 0, Product("Free Skiff", _acme, "mod.free")))));
        Assert.Contains("needs a name", Refusal(Draft(Package("mod.blank", "Blank", 100, Product(" ", _acme, "mod.blank")))));
        Assert.Contains("unique", Refusal(Draft(Package("mod.twice", "Twice", 100, Product("Same", _acme, "mod.twice"), Product("Same", _rival, "mod.twice")))));

        // The hull's own rules judge a draft too: validate runs the one composition Read runs.
        var unnamedHull = Draft(Path.Combine(ShipFixture.WritePackage(Mods, "mod.unnamed", hullName: ""), "ship.cc"));
        Assert.Contains("hull name is required", Refusal(unnamedHull));

        var sound = Draft(Package("mod.sound", "Sound", 100, Product("Sound Skiff", _acme, "mod.sound"), Product("Sound Rebrand", _rival, "mod.sound")));
        var (validated, output, _) = Run("validate", sound);
        Assert.Equal(0, validated);
        Assert.Contains("records only", output);

        var (inspected, listing, _) = Run("inspect", sound);
        Assert.Equal(0, inspected);
        Assert.Contains($"product {_acme.Value}: Sound Skiff", listing);
        Assert.Contains($"product {_rival.Value}: Sound Rebrand", listing);
    }
}
