/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CultMath;
using GameCult.Caching;

// minor-power-factions [apply] (aetheria-release, cut minor-power-factions; rulings no-territory-ban, miss-terris-belongs-to-emily-r3):
// authors the Aya Collective, Megiddo and Orbital Forge as ordinary factions, and the allegiance every existing faction but the
// Pirates and Miss Terri's holds to them. The Pirates' allegiance is pirates-faction's alone (it tops itself up on a re-run).
// A faction already in the catalog under its name-derived key is skipped, as is a gain already held, so the same command is the
// first apply, the second apply and the replay onto a merged catalog. Dry run unless passed "apply".
public static class MinorPowerFactionsCommand
{
    // Own: the weights this faction names for the other minor powers; every other faction but the Pirates gets Default.
    // Gain: the weight every existing faction (bar the Pirates and Miss Terri's) gains toward this faction.
    internal sealed record Def(string Name, string ShortName, string Description, float3 Primary, float3 Secondary,
        int Influence, float Gain, float Default, (string ShortName, float Weight)[] Own);

    internal static readonly Def[] Factions =
    {
        new("Aya Collective", "Aya",
            "A federation of communes, clinics and repair crews. Its equipment comes with the drawings, so the people it protects can keep it running.",
            new float3(.72f, .36f, .22f), new float3(.12f, .36f, .25f), 3, .3f, .3f,
            new[] { ("Forge", 1f), ("Megiddo", .5f) }),
        new("Megiddo", "Megiddo",
            "A defended fleet that holds its boundary until the question is answered. Others buy its protection without sharing its faith.",
            new float3(.07f, .09f, .15f), new float3(.90f, .85f, .74f), 4, .3f, .2f,
            new[] { ("Aya", .5f), ("Forge", .5f) }),
        new("Orbital Forge", "Forge",
            "Belt engineers who hollow asteroids into homes and yards. Their standards leave room to adapt them, and their drawings travel further than their ships.",
            new float3(.42f, .30f, .22f), new float3(.30f, .38f, .52f), 3, .5f, .5f,
            Array.Empty<(string, float)>()),
    };

    private const string Pirates = "Pirates";
    private const string MissTerris = "Miss Terri's";

    // A faction's record key is derived from its name, so a replay reproduces the same key and the same bytes.
    internal static CultRecordKey KeyOf(Def def) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("faction:" + def.Name)).AsSpan(0, 16)).ToString("N"));

    public static int Run(string[] args, string root = null)
    {
        var apply = args.Contains("apply");
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var existing = db.Cache.GetAll<Faction>().ToArray();

        // Refuse before any write: a name or short name held by a faction under another key.
        foreach (var def in Factions)
        {
            var key = KeyOf(def);
            foreach (var other in existing.Where(f => f.Name == def.Name || f.ShortName == def.ShortName))
                if (!db.Cache.RefOf(other).Key.Equals(key))
                    throw new InvalidOperationException("Faction Name or ShortName: held by a faction under another key (conflict).");
        }

        var minorKeys = Factions.ToDictionary(d => d.ShortName, KeyOf);
        var created = Factions.Where(d => !existing.Any(f => db.Cache.RefOf(f).Key.Equals(KeyOf(d)))).ToArray();

        // Every existing faction that is not a minor power, the Pirates or Miss Terri's holds each minor power it lacks.
        var holders = existing.Where(f => f.ShortName != Pirates && f.ShortName != MissTerris && !minorKeys.ContainsKey(f.ShortName)).ToArray();
        var gains = holders
            .Select(h => (Holder: h, Missing: Factions.Where(d => !h.Allegiance.ContainsKey(new CultRecordRef<Faction>(minorKeys[d.ShortName]))).ToArray()))
            .Where(g => g.Missing.Length != 0).ToArray();

        if (created.Length == 0 && gains.Length == 0)
        {
            Console.WriteLine("The minor powers are already authored; nothing to do.");
            return 0;
        }

        foreach (var def in created)
            Console.WriteLine($"New faction \"{def.Name}\" ({def.ShortName}): influence {def.Influence}, colours ({def.Primary.x:0.00}, {def.Primary.y:0.00}, {def.Primary.z:0.00}) / ({def.Secondary.x:0.00}, {def.Secondary.y:0.00}, {def.Secondary.z:0.00})\n  {def.Description}\n  allegiance: {string.Join(", ", def.Own.Select(o => $"{o.ShortName} {o.Weight:0.##}"))} others {def.Default:0.##}");
        foreach (var (holder, missing) in gains)
            Console.WriteLine($"  {holder.ShortName} gains {string.Join(", ", missing.Select(d => $"{d.ShortName} {d.Gain:0.##}"))}");

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author {created.Length} factions and {gains.Sum(g => g.Missing.Length)} allegiance gains.");
            return 0;
        }

        // A new faction names every faction but the Pirates, the other minor powers included.
        var everyone = existing.Where(f => f.ShortName != Pirates && !minorKeys.ContainsKey(f.ShortName)).Select(f => db.Cache.RefOf(f)).ToArray();
        if (!db.Cache.Commit(batch =>
        {
            foreach (var def in created) batch.Expect(KeyOf(def), null);
            foreach (var def in created)
            {
                var faction = new Faction
                {
                    Name = def.Name, ShortName = def.ShortName, Description = def.Description,
                    PrimaryColor = def.Primary, SecondaryColor = def.Secondary,
                    InfluenceDistance = def.Influence,
                };
                foreach (var reference in everyone) faction.Allegiance[reference] = def.Default;
                foreach (var other in Factions.Where(o => o != def))
                    faction.Allegiance[new CultRecordRef<Faction>(minorKeys[other.ShortName])] =
                        def.Own.Where(o => o.ShortName == other.ShortName).Select(o => o.Weight).DefaultIfEmpty(def.Default).First();
                batch.Upsert(typeof(Faction), faction, KeyOf(def));
            }
            foreach (var (holder, missing) in gains)
            {
                foreach (var def in missing) holder.Allegiance[new CultRecordRef<Faction>(minorKeys[def.ShortName])] = def.Gain;
                batch.Upsert(typeof(Faction), holder, db.Cache.RefOf(holder).Key);
            }
        }))
            throw new InvalidOperationException("The catalog changed while the minor powers were being authored; nothing was written. Run it again.");
        Console.WriteLine($"\nAuthored {created.Length} factions and {gains.Sum(g => g.Missing.Length)} allegiance gains in Aetheria.cc");
        return 0;
    }
}
