using System;
using System.IO;
using System.Linq;
using GameCult.Caching;

// Authors the starting FactionDoctrine of the four factions the demo cast uses (aetheria-release, faction-play-1).
// Tuning is a starting point; the operator adjusts it after play. The Pirates record is on master (cut pirates-record);
// a faction with no Faction record is named and skipped, and apply is re-run after a cast faction's record lands.
// A faction that already has a doctrine is kept as it is (the operator tunes after play), so a rerun that finds
// nothing to author writes nothing.
public static class DoctrineCatalog
{
    private static readonly (string faction, FactionDoctrine doctrine)[] Table =
    {
        ("Zhestokost", new FactionDoctrine
        {
            EngageOn = EngageOn.Trespass, Grace = 6f, Hail = "Cut thrust and hold for inspection.", ComplySpeed = 10f,
            Combatant = new RoleDoctrine { RangeExponent = .05f, MinHitProbability = .05f }
        }),
        ("Lucent Media", new FactionDoctrine
        {
            EngageOn = EngageOn.Detection, Grace = 2f, Hail = "You're live.",
            Combatant = new RoleDoctrine { RangeExponent = .25f, MinHitProbability = .3f }
        }),
        ("Aeronautics Unlimited", new FactionDoctrine
        {
            EngageOn = EngageOn.Detection,
            Combatant = new RoleDoctrine { RangeExponent = .25f, MinHitProbability = .05f }
        }),
        ("Pirates", new FactionDoctrine
        {
            EngageOn = EngageOn.Detection, Grace = 8f, Hail = "Drop your cargo and you can go.",
            Combatant = new RoleDoctrine { RangeExponent = .5f, MinHitProbability = .15f }
        }),
    };

    // Dry run unless apply, as the other catalog commands. The cache must be writable for apply.
    public static int Run(CultCache cache, bool apply, TextWriter output)
    {
        var factions = cache.GetAll<Faction>().ToArray();
        var changed = new System.Collections.Generic.List<Faction>();
        foreach (var (name, doctrine) in Table)
        {
            var faction = factions.FirstOrDefault(f => f.Name == name);
            if (faction == null)
            {
                output.WriteLine($"{name}: no Faction record, doctrine not written");
                continue;
            }

            if (faction.Doctrine != null)
            {
                output.WriteLine($"{name}: already has a doctrine, kept");
                continue;
            }

            output.WriteLine($"{name}: engage on {doctrine.EngageOn}, grace {doctrine.Grace:0.##}s, hail \"{doctrine.Hail}\", " +
                             $"comply {doctrine.ComplySpeed:0.##}, range exponent {doctrine.Combatant.RangeExponent:0.##}, " +
                             $"min hit {doctrine.Combatant.MinHitProbability:0.##}");
            faction.Doctrine = doctrine;
            changed.Add(faction);
        }

        if (!apply)
        {
            output.WriteLine("\nDry run. Pass \"apply\" to author them.");
            return 0;
        }

        if (changed.Count == 0)
        {
            output.WriteLine("
Nothing to author.");
            return 0;
        }

        cache.Commit(batch =>
        {
            foreach (var faction in changed) batch.Upsert(typeof(Faction), faction, cache.RefOf(faction).Key);
        });
        output.WriteLine($"\nAuthored {changed.Count} doctrines in Aetheria.cc");
        return 0;
    }
}
