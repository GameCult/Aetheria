/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;

// The run's lot ledger: one run-scoped global document, LotId -> Lot. The live instance is ItemManager.Lots.
[CultDocument("aetheria.provenanceledger", "1"), CultGlobal, MessagePackObject]
public class ProvenanceLedger
{
    [Key(0)] public int NextLot = 1;
    [Key(1)] public Dictionary<int, Lot> Lots = new Dictionary<int, Lot>();

    // The only way a lot enters the ledger.
    public int Add(Lot lot)
    {
        var id = NextLot++;
        Lots[id] = lot;
        return id;
    }

    // Absent lots throw: a dangling LotId is corruption, and it must be loud. No try-variant.
    public Lot this[int id]
    {
        get
        {
            if (!Lots.TryGetValue(id, out var lot))
                throw new InvalidOperationException($"Lot {id} is not in the provenance ledger.");
            return lot;
        }
    }

    // A new ledger holding only lots reachable from roots, closed over Produced.Facility and Produced.Inputs. Never
    // mutates this: the live ledger keeps unreachable lots, because a floor pickup can still be collected after a save.
    public ProvenanceLedger Reachable(IEnumerable<int> roots)
    {
        var reached = new Dictionary<int, Lot>();
        var pending = new Queue<int>(roots);
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (reached.ContainsKey(id)) continue;
            var lot = this[id];
            reached[id] = lot;
            if (lot.Origin is Produced produced)
            {
                pending.Enqueue(produced.Facility);
                if (produced.Inputs != null)
                    foreach (var input in produced.Inputs)
                        pending.Enqueue(input);
            }
        }

        return new ProvenanceLedger { NextLot = NextLot, Lots = reached };
    }
}

// One lot: the design it built, who made it and from what, its rolled workmanship, and how well each design role
// was filled. Immutable once minted; instances carry a LotId, never a copy.
[MessagePackObject]
public class Lot
{
    [Key(0)] public CultRecordRef<ItemData> Design;
    [Key(1)] public Provenance Origin;
    [Key(2)] public float Quality;
    [Key(3)] public List<RoleFill> Roles;

    // The quality a stat reads for one of the design's roles. An unnamed role, a design without roles, and a lot
    // minted before roles existed all fall back to the lot's own workmanship.
    public float QualityForRole(string role)
    {
        if (string.IsNullOrEmpty(role) || Roles == null) return Quality;
        foreach (var fill in Roles)
            if (fill.Role == role) return fill.Quality;
        return Quality;
    }
}

// One filled slot: the design's role name and how good the part that went into it turned out to be.
[MessagePackObject]
public class RoleFill
{
    [Key(0)] public string Role;
    [Key(1)] public float Quality;
}

// Who built a lot, where, and from what. Terminus mints Attributed placeholders; Produced and Extracted are the
// forward shape for backward generation, next scope.
[Union(0, typeof(Attributed)),
 Union(1, typeof(Produced)),
 Union(2, typeof(Extracted))]
public abstract class Provenance { }

// The Terminus placeholder: only the faction is known. No other level of the crafting pyramid is generated yet.
[MessagePackObject]
public class Attributed : Provenance
{
    [Key(0)] public CultRecordRef<Faction> Faction;
}

// Built in place at a station, from a factory item's own lot and a set of input lots.
[MessagePackObject]
public class Produced : Provenance
{
    [Key(0)] public CultRecordRef<Faction> Faction;
    [Key(1)] public int Station;
    [Key(2)] public int Facility;
    [Key(3)] public int[] Inputs;
}

// A raw commodity pulled from a zone.
[MessagePackObject]
public class Extracted : Provenance
{
    [Key(0)] public int Zone;
    [Key(1)] public CultRecordRef<SimpleCommodityData> Commodity;
}
