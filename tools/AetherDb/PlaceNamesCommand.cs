/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameCult.Caching;

// place-names [apply] (aetheria-release, cut faction-curated-place-names; ruling faction-curated-place-names):
// authors every curated faction's Places from the table below, which is the one source of those names. A faction is
// identified by its record key, and its ShortName must agree (ghostlight-verse ruling faction-identity-is-record-key).
// A faction without a row keeps the catalogue designations Galaxy.NamePlaces falls back to.
// Order is meaning: the first name is the faction's capital, and the home zone takes it. Dry run unless passed "apply".
public static class PlaceNamesCommand
{
    // List holds the names separated by " | ", in authored order.
    internal sealed record Row(string RecordKey, string ShortName, string List)
    {
        public string[] Names => List.Split(" | ");
    }

    internal static readonly Row[] Table =
    {
        new("bbd619ed-76c1-464c-829d-3f419cdc2525", "Adrasteia",
            "Shunya | Unlisted | Tamas | Silent Address | Maya | The Warm Hour | Nishabda | The Vouch | Akasha | No Such Node | Chhaya | Heat Death Reach | Santulan | Cold Margin | Antara | Without Light | Nirvata | Redacted Deep | Quiet Reach | Last Known"),
        new("8443f966-0d88-4ec7-a20e-8edf73fa62f6", "AU",
            "Ceres Ramp | Pallas Provisional | Ramp Two | Claimshare | Vesta Works | Hygiea | Bloom Seven | Half Measure | Further Out | Semi-Permanent | Under Construction | Drawing Board | Survey Pending | Ceres Control | Margin of Error | Proof of Concept | Ramp Nine | Good Enough | Ambition | Not Yet Named"),
        new("a1f76631-d6d1-443a-9be1-c9b1267d6ca2", "Alakrita",
            "Aurea | Celeritas | Elegantia | Via Alakrita | Fatum | Quiet Bay | Laurea | Gloria | Prima Linea | Destiny Yard | Aequor | Corona | Vela | Lumen | Nobilis | Portus Aureus | Inscription | Tessera | Proportio | Decus"),
        new("48bbb7537ff9ed0a73c3c353598e8f69", "Aya",
            "Hellas | Sanctuary | Common Kitchen | Open Courtyard | Enceladus Commons | Mutual | Assembly Hall | Repair Circle | Refuge Route | Not Inevitable | Free Clinic | Shared Table | Drawings Included | All Welcome | Congress | Keep It Running | Terracotta | Open Door | Freehold Garden | Green Shade"),
        // DME's voice is lowercase on purpose.
        new("8419f9bc-0167-46ec-86c3-3265d2905fbc", "DME",
            "the choir loft | encore | burn it down | turn it up | void communion | last call | margin poem | basement tape | loud room | second encore | feedback | no permit | the pit | spare amp | open mic | after hours | smoke machine | b-side | green room | yara's version"),
        new("a9672650-c335-484a-9555-71a35c841aa1", "Ewan Hart",
            "Homestead | Second Harvest | Convoy's End | Invented Spring | Grange Hall | Long Furrow | Silo Nine | Good Dog | Feed and Seed | False October | County Line | Still Growing | Tractor Pull | Prairie Bloom | Rain Check | Elevator | Hayloft | Late Frost | Sealed Summer | Old Blue"),
        new("1dfaa629-6c63-42a7-91e1-f1ae90f92f09", "Finch",
            "Tycho | Serenity Ward | Tranquility | Far Side | Nectaris Clinic | Grace | Crisium | Mireille | Copernicus | Imbrium | Full Coverage | Aitken | Fecunditas | Vaporum | Recovery Ring | Clavius | Earthrise | Second Opinion | Frigoris | Plato"),
        new("2f74e6b4-d268-48a9-b2a8-24591c680e84", "Lightsail",
            "Mile Zero | Ganymede Stop | Last Anchorage | Dispatch | The Drop Book | Truck Stop | Route Choir | On Time | Callisto Crossing | Haul Road | Weigh Station | Long Haul | Bill of Lading | Overnight | Deadhead | Jake Brake | Signed For | The Line | Breaker Nineteen | Final Mile"),
        new("a012e0f4-4b22-47b7-8b24-ff3e1439c949", "Lucent",
            "Ishtar Studios | Prime Time | Maxwell Heights | Cloud Nine Live | Channel Aphrodite | Clout | Buzz Market | Ratings Tower | Lagrange Feed | Spotlight | Exclusive | Trending | Broadcast Row | Red Carpet | Season Finale | Sponsored | Headliner | Viral | Golden Hour | Breaking News"),
        new("087df3f89e59beab14f9d41c849b202f", "Megiddo",
            "First Vigil | Earth Stars | Continuity | The Long Silence | Deferred Ground | Unverified | Boundary Watch | Custody | Not Our World | Pleiades Watch | Second Vault | Presumption | Under Orion | Night Watch | Witness | Flatland Edge | Old Sky | Record Keeper | Patience | Revelation Pending"),
        // Real Australian place names, Indigenous-derived and English, a draft Emily may replace (ruling miss-terris-improvised-places).
        new("d6466821-6776-4f40-9609-2342debf5c86", "Miss Terri's",
            "Woolloomooloo | Wagga Wagga | Humpty Doo | Come By Chance | Yackandandah | Nowhere Else | Kurri Kurri | Lightning Ridge | Mooloolaba | Woy Woy | Banana | Coober Pedy | Gundagai | Burpengary | Toowoomba | Kalgoorlie | Bald Hills | Wollongong | Innaloo | Ballarat"),
        new("73d368dd-58a8-4431-b9ba-2d44754d36a1", "NiteLife",
            "Caloris Array | Arete Grid | Malam | Cahaya | Graveyard Shift | Lights On | Bintang | Night Count | Pelita | Uptime Pro | Bulan | Grid Plus | Kilat | Afterglow | Load Balance | Peak Hours | Terang | Version Two | Midnight Blue | Keep On"),
        new("e34d0354f46873df7ec27273fa2abe3d", "Forge",
            "Hollow Pallas | Cinderlace | Stubborn | Open Drawing | Pattern Yard | Bored Through | Slagside | Tolerance | Free Standard | Lathe | Anvil Rock | Copied Twice | Mandrel | Inner Shell | Quench | Forge Light | Billet | Rivet | Blueprint | Worlds Enough"),
        // The two emoji are U+1F3F4 and U+1F99C (ruling pirates-record-content).
        new("1d8a168def994a76a1ce7cb18f59c038", "Pirates",
            "Trojan Nest | Genuine Rescue | Ring Shadow | Patched Cavity | \U0001F3F4 Nobody's | Repainted | Finders Keepers | Hidden Dock | Free Parking | Legit Waystation | New Management | No Questions | Second Hand | False Beacon | Salvage Rights | The Fence | Elegance. Ours. | \U0001F99C Roost | Maintenance Due | Not A Base"),
        new("adfb9b08-aabc-45c6-9586-915698a9c4b0", "R&D",
            "Prüfstand | Findings | Recertification | Configuration B | Asterisk | Freigabe | Friendly | Sicherheit | Compliance | Revision C | Normal Operation | Abnahme | Test Bench Four | Fine Print | Hold Music | Reassurance | Warranty | Grundlage | Standard Procedure | Kinderleicht"),
        new("16bad4f9-7fb5-41c5-8da0-dc76709eb7d3", "Zhestokost",
            "Cimmeria | Tharsis Bastion | Olimp | Molot | Valles Forward | Arsenal Commission | Eternal Council | Moneta | Kuznya | Overkill | Assignment Board | Stal | Krepost | Gromada | Campaign | Frontline | Beton | Plamya | Zhelezo | Pobeda"),
    };

    public static int Run(string[] args, string root = null) => Run(args, root, Table);

    // The table is a parameter so a test can hand it a malformed one; production always passes Table.
    internal static int Run(string[] args, string root, Row[] table)
    {
        var apply = args.Contains("apply");
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var factions = db.Cache.GetAll<Faction>().ToArray();
        var keyOf = factions.ToDictionary(f => db.Cache.RefOf(f).Key.Value, StringComparer.Ordinal);

        // Refuse before any write. A refusal never echoes the offending value.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table)
        {
            if (!keyOf.TryGetValue(row.RecordKey, out var faction))
                throw new InvalidOperationException("Place names row: its key is not a faction in the catalog (missing).");
            if (faction.ShortName != row.ShortName)
                throw new InvalidOperationException("Place names row: its ShortName differs from the faction under its key (conflict).");
            foreach (var name in row.Names)
            {
                if (!FactionPlace.IsValidName(name))
                    throw new InvalidOperationException("Place names row: a name is empty, untrimmed or a catalogue designation (invalid).");
                if (!seen.Add(name))
                    throw new InvalidOperationException("Place names table: a name is repeated (conflict).");
            }
        }

        var rows = table.ToDictionary(r => r.RecordKey, StringComparer.Ordinal);
        var changed = table.Where(r => !keyOf[r.RecordKey].Places.Select(p => p.Name).SequenceEqual(r.Names)).ToArray();

        foreach (var faction in factions.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (!rows.TryGetValue(db.Cache.RefOf(faction).Key.Value, out var row))
            {
                Console.WriteLine($"{faction.Name}: no list: catalogue designations");
                continue;
            }
            Console.WriteLine($"{faction.Name}: {row.Names.Length} places, capital {row.Names[0]}, {(changed.Contains(row) ? "changed" : "unchanged")}");
        }
        if (changed.Length == 0)
        {
            Console.WriteLine("nothing to do");
            return 0;
        }
        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author {changed.Length} place lists.");
            return 0;
        }

        if (!db.Cache.Commit(batch =>
        {
            foreach (var row in changed)
            {
                var faction = keyOf[row.RecordKey];
                faction.Places = row.Names.Select(n => new FactionPlace { Name = n }).ToArray();
                batch.Upsert(typeof(Faction), faction, db.Cache.RefOf(faction).Key);
            }
        }))
            throw new InvalidOperationException("The catalog changed while place names were being authored; nothing was written. Run it again.");
        Console.WriteLine($"\nAuthored {changed.Length} place lists in Aetheria.cc");
        return 0;
    }
}
