/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;

// loadout-commit <name> <hull product> [<x>,<y>,<rotation>,<design>[@<product>]]... [--group <i>,<j>...]... [--replace] [--boss-of <faction>]
// (aetheria-release, cut loadout-commit; rulings boss-loadouts-agent-authored, catalog-is-a-package-stack): writes one Loadout
// record into GameData/Aetheria.cc through Loadouts.Commit, the only writer, and with --boss-of makes it that faction's boss in
// the same commit. Names resolve exactly against the catalog the game boots: ShipModCatalog.ResolveCatalog over the shipped
// file and GameData/Mods, composed into a scratch file that is never committed. So a loadout may name a package's hull,
// design or product (one hull product, one design per slot, one product where a slot names one, a rotation by its
// ItemRotation name, a faction by Name or ShortName). The loadout is then built once on a throwaway ItemManager with
// Loadouts.Materialize, so a slot that does not fit or a weapon group that names a non-weapon is refused here. Any refusal
// happens before Commit: the catalog stays byte-identical. A refusal names the field and the slot, never the value it was given.
public static class LoadoutCommitCommand
{
    private const string Usage = "usage: loadout-commit <name> <hull product> [<x>,<y>,<rotation>,<design>[@<product>]]... [--group <i>,<j>...]... [--replace] [--boss-of <faction>]";

    public static int Run(string[] args, string root = null, TextWriter output = null)
    {
        output ??= Console.Out;
        try
        {
            return Commit(args, root, output);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or FormatException)
        {
            output.WriteLine($"refused: {e.Message}");
            return 1;
        }
    }

    private static int Commit(string[] args, string root, TextWriter output)
    {
        var replace = false;
        string bossOfName = null;
        var groups = new List<int[]>();
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--replace") replace = true;
            else if (args[i] == "--boss-of")
            {
                if (++i >= args.Length) throw new ArgumentException("--boss-of needs a faction");
                bossOfName = args[i];
            }
            else if (args[i] == "--group")
            {
                if (++i >= args.Length) throw new ArgumentException("--group needs a list of slot indices");
                groups.Add(args[i].Split(',').Select(part => int.TryParse(part, out var index) ? index : throw new FormatException($"group {groups.Count}: an index is not a number")).ToArray());
            }
            else positional.Add(args[i]);
        }

        if (positional.Count < 2)
        {
            output.WriteLine(Usage);
            return 1;
        }

        root ??= AetherDb.FindRoot();
        var catalogPath = Path.Combine(root, "GameData", "Aetheria.cc");
        var scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aetherdb-loadout-commit-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var (composed, excluded) = ShipModCatalog.ResolveCatalog(catalogPath, Path.Combine(scratch, "derived.cc"), Path.Combine(root, "GameData", "Mods"));
            foreach (var exclusion in excluded) output.WriteLine($"package left out, as at boot: {exclusion}");

            Loadout loadout;
            CultRecordRef<Faction> bossOf = default;
            string summary;
            // Disposed before the commit: with no package composed, this is the shipped file itself.
            using (var cache = AetheriaStores.Open(composed))
            {
                loadout = Build(cache, positional[0], positional[1], positional.Skip(2).ToArray(), groups);
                // Compose writes no faction, so the composed catalog's factions are the shipped ones under the same keys.
                if (bossOfName != null)
                    bossOf = cache.RefOf(ExactlyOne(cache.GetAll<Faction>().Where(f => f.Name == bossOfName || f.ShortName == bossOfName).ToArray(), "boss faction"));

                var failures = new List<string>();
                var settings = AuthoredSettings.Load(root).Read<GameplaySettings>("GameplaySettings");
                // Validation never draws for play, so the item stream gets a fixed seed.
                var itemManager = new ItemManager(1u, cache, new ProvenanceLedger(), settings, _ => { });
                var entity = Loadouts.Materialize(itemManager, null, loadout, design => ProductsOf(cache, design).FirstOrDefault(), failures);
                if (entity == null) throw new InvalidOperationException($"the loadout cannot be built: {string.Join("; ", failures)}");

                summary = $"{loadout.Name}: committed, hull {cache.Get(loadout.Hull).Name} ({cache.Get(loadout.HullProduct).Name}), {loadout.Slots.Count} slots" +
                          (bossOf.IsSet() ? $", boss of {cache.Get(bossOf).Name}" : "");
            }

            if (!Loadouts.Commit(catalogPath, loadout, replace, bossOf))
                throw new InvalidOperationException("the catalog changed since it was read; nothing was written");
            output.WriteLine(summary);
            return 0;
        }
        finally
        {
            Directory.Delete(scratch, true);
        }
    }

    // Validation only: a design-only slot is built at its first product by Name, so the throwaway build proves the fit.
    private static IEnumerable<FactionProductData> ProductsOf(CultCache cache, CraftedItemData design)
    {
        var key = cache.RefOf(design).Key;
        return cache.GetAll<FactionProductData>().Where(product => product.Design.Key.Equals(key)).OrderBy(product => product.Name, StringComparer.Ordinal);
    }

    private static Loadout Build(CultCache cache, string name, string hullProductName, string[] slotArgs, List<int[]> groups)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("the loadout name is empty");
        var hullProduct = ExactlyOne(cache.GetAll<FactionProductData>().Where(product => product.Name == hullProductName).ToArray(), "hull product");
        var hull = cache.Get(hullProduct.Design) as HullData ?? throw new InvalidOperationException("hull product: its design is not a hull");
        var slots = new List<LoadoutSlot>();
        for (var i = 0; i < slotArgs.Length; i++)
        {
            var where = $"slot {i}";
            var parts = slotArgs[i].Split(',', 4);
            if (parts.Length != 4 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
                throw new FormatException($"{where}: expected <x>,<y>,<rotation>,<design>[@<product>]");
            if (!Enum.TryParse<ItemRotation>(parts[2], out var rotation) || !Enum.IsDefined(typeof(ItemRotation), rotation) || int.TryParse(parts[2], out _))
                throw new FormatException($"{where}: rotation is not an ItemRotation name");
            var at = parts[3].IndexOf('@');
            var designName = at < 0 ? parts[3] : parts[3][..at];
            var design = ExactlyOne(cache.GetAll<EquippableItemData>().Where(d => d.Name == designName).ToArray(), $"{where} design");
            var slot = new LoadoutSlot { Position = new int2(x, y), Rotation = rotation, Design = cache.RefOf(design) };
            if (at >= 0)
            {
                var productName = parts[3][(at + 1)..];
                slot.Product = cache.RefOf(ExactlyOne(cache.GetAll<FactionProductData>().Where(p => p.Name == productName).ToArray(), $"{where} product"));
            }
            slots.Add(slot);
        }

        return new Loadout
        {
            Name = name,
            Hull = cache.RefOf(hull),
            HullProduct = cache.RefOf(hullProduct),
            Slots = slots,
            WeaponGroups = groups.Count == 0 ? null : groups.ToArray()
        };
    }

    private static T ExactlyOne<T>(T[] found, string field) =>
        found.Length == 1 ? found[0] : throw new InvalidOperationException(found.Length == 0 ? $"{field}: no record has that name" : $"{field}: more than one record has that name");
}
