using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using MessagePack;

// Makes the named plain guns fully automatic (ruling fire-mode-is-the-design): each one's InstantWeaponData becomes an
// AutoWeaponData carrying every authored stat unchanged, so cadence and damage do not move. A gun that is already an
// AutoWeapon (or any other kind) is left alone, so a rerun writes nothing. Dry run unless apply.
public static class AutoGunsCatalog
{
    public static readonly string[] Guns = { "Slug Rifle", "Long Slug Rifle" };

    public static int Run(CultCache cache, bool apply, TextWriter output)
    {
        var changed = new System.Collections.Generic.List<GearData>();
        foreach (var name in Guns)
        {
            var gear = cache.GetAll<GearData>().SingleOrDefault(g => g.Name == name);
            if (gear == null) { output.WriteLine($"{name}: no such gear, skipped"); continue; }
            var index = gear.Behaviors.FindIndex(b => b.GetType() == typeof(InstantWeaponData));
            if (index < 0) { output.WriteLine($"{name}: no plain gun, kept"); continue; }
            var plain = (InstantWeaponData) gear.Behaviors[index];
            var bytes = MessagePackSerializer.Serialize(typeof(InstantWeaponData), plain);
            gear.Behaviors[index] = MessagePackSerializer.Deserialize<AutoWeaponData>(bytes);
            output.WriteLine($"{name}: InstantWeaponData -> AutoWeaponData (cooldown {plain.Cooldown.Max:0.###} s)");
            changed.Add(gear);
        }

        if (!apply) { output.WriteLine("\nDry run. Pass \"apply\" to author them."); return 0; }
        if (changed.Count == 0) { output.WriteLine("Nothing to author."); return 0; }
        foreach (var gear in changed) CultRecordRefs.Validate(gear);
        cache.Commit(batch => { foreach (var gear in changed) batch.Upsert(typeof(GearData), gear, cache.RefOf(gear).Key); });
        output.WriteLine($"\nAuthored {changed.Count} guns in Aetheria.cc");
        return 0;
    }
}
