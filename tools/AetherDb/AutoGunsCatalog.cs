using System;
using System.IO;
using System.Linq;
using GameCult.Caching;
using System.Reflection;

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
            var auto = new AutoWeaponData();
            // AutoWeaponData adds no member of its own: every field of the plain gun, from every base, carries over.
            for (var type = typeof(InstantWeaponData); type != null; type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    field.SetValue(auto, field.GetValue(plain));
            gear.Behaviors[index] = auto;
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
