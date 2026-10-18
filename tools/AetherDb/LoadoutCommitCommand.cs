/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;

// loadout-commit <name> <hull product> [<x>,<y>,<rotation>,<design>[@<product>]]... [--group <i>,<j>...]... [--replace]
// (aetheria-release, cut boss-hull-derived; ruling boss-loadouts-agent-authored): writes one Loadout record into
// GameData/Aetheria.cc through Loadouts.Commit, the only writer. Every name is resolved exactly (one hull product, one design per
// slot, one product where a slot names one, a rotation by its ItemRotation name), then the loadout is built once on a throwaway
// ItemManager with Loadouts.Materialize, so a slot that does not fit or a weapon group that names a non-weapon is refused here.
// Any refusal happens before Commit: the catalog stays byte-identical. A refusal names the field and the slot, never the value
// it was given.
public static class LoadoutCommitCommand
{
    private const string Usage = "usage: loadout-commit <name> <hull product> [<x>,<y>,<rotation>,<design>[@<product>]]... [--group <i>,<j>...]... [--replace]";

    public static int Run(string[] args, string root = null)
    {
        try
        {
            return Commit(args, root);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or FormatException)
        {
            Console.WriteLine($"refused: {e.Message}");
            return 1;
        }
    }

    private static int Commit(string[] args, string root)
    {
        var replace = false;
        var groups = new List<int[]>();
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--replace") replace = true;
            else if (args[i] == "--group")
            {
                if (++i >= args.Length) throw new ArgumentException("--group needs a list of slot indices");
                groups.Add(args[i].Split(',').Select(part => int.TryParse(part, out var index) ? index : throw new FormatException($"group {groups.Count}: an index is not a number")).ToArray());
            }
            else positional.Add(args[i]);
        }

        if (positional.Count < 2)
        {
            Console.WriteLine(Usage);
            return 1;
        }

        var db = AetherDb.Open(root: root);
        var catalogPath = Path.Combine(db.Root, "GameData", "Aetheria.cc");
        var loadout = Build(db.Cache, positional[0], positional[1], positional.Skip(2).ToArray(), groups);

        var failures = new List<string>();
        var settings = AuthoredSettings.Load(db.Root).Read<GameplaySettings>("GameplaySettings");
        var itemManager = new ItemManager(db.Cache, new ProvenanceLedger(), settings, _ => { });
        var entity = Loadouts.Materialize(itemManager, null, loadout, design => ProductsOf(db.Cache, design).FirstOrDefault(), failures);
        if (entity == null) throw new InvalidOperationException($"the loadout cannot be built: {string.Join("; ", failures)}");

        var hull = db.Cache.Get(loadout.Hull);
        var product = db.Cache.Get(loadout.HullProduct);
        var slots = loadout.Slots.Count;
        (db.Cache as IDisposable)?.Dispose();

        if (!Loadouts.Commit(catalogPath, loadout, replace))
            throw new InvalidOperationException("the catalog changed since it was read; nothing was written");
        Console.WriteLine($"{loadout.Name}: committed, hull {hull.Name} ({product.Name}), {slots} slots");
        return 0;
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
