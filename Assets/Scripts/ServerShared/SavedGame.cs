using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;
using CultMath;
using static CultMath.math;

[CultDocument("aetheria.savedgame", "1"), CultGlobal, MessagePackObject]
public class SavedGame
{
    [Key(0)]
    public CultRecordRef<SavedZone>[] Zones;

    [Key(1)]
    public CultRecordRef<Faction>[] Factions;

    [Key(2)]
    public Dictionary<int, int> HomeZones;

    [Key(3)]
    public Dictionary<int, int> BossZones;

    [Key(4)]
    public int Entrance;

    [Key(5)]
    public int Exit;

    [Key(6)]
    public int CurrentZone;

    [Key(7)]
    public int CurrentZoneEntity;

    [Key(8)]
    public SectorBackgroundSettings Background;

    [Key(9)]
    public int[] DiscoveredZones;

    [Key(10)]
    public SavedActionBarBinding[] ActionBarBindings;

    [Key(11)]
    public bool IsTutorial;

    [Key(12)]
    public FactionRelationship[] Relationships;
}

// The run's lifecycle over the run store. Only Commit creates SavedGame, SavedZone and ProvenanceLedger records;
// the ledger is the fifth run record.
public static class RunSave
{
    // The live run as plain documents. Writes nothing.
    public static (SavedGame Game, SavedZone[] Zones) Capture(CultCache cache, Galaxy galaxy, Zone currentZone,
        Entity currentEntity, bool isTutorial, SavedActionBarBinding[] actionBar)
    {
        var factions = galaxy.HomeZones.Keys.ToArray();
        var game = new SavedGame
        {
            DiscoveredZones = galaxy.DiscoveredZones.Select(dz => Array.IndexOf(galaxy.Zones, dz)).ToArray(),
            Background = galaxy.Background,
            Factions = factions.Select(f => cache.RefOf(f)).ToArray(),
            Relationships = galaxy.Factions.Select(f => galaxy.FactionRelationships[f]).ToArray(),
            HomeZones = galaxy.HomeZones.ToDictionary(
                x => Array.IndexOf(factions, x.Key),
                x => Array.IndexOf(galaxy.Zones, x.Value)),
            BossZones = galaxy.BossZones.ToDictionary(
                x => Array.IndexOf(factions, x.Key),
                x => Array.IndexOf(galaxy.Zones, x.Value)),
            CurrentZone = Array.FindIndex(galaxy.Zones, zone => zone.Contents == currentZone),
            CurrentZoneEntity = currentZone.Entities.IndexOf(currentEntity),
            Entrance = Array.IndexOf(galaxy.Zones, galaxy.Entrance),
            Exit = Array.IndexOf(galaxy.Zones, galaxy.Exit),
            IsTutorial = isTutorial,
            ActionBarBindings = actionBar
        };

        // A zone generated earlier but not loaded this session has no live Contents; its packed contents carry over.
        var zones = galaxy.Zones.Select(zone => new SavedZone
        {
            Name = zone.Name,
            Position = zone.Position,
            AdjacentZones = zone.AdjacentZones.Select(az => Array.IndexOf(galaxy.Zones, az)).ToArray(),
            Factions = zone.Factions.Select(f => Array.IndexOf(factions, f)).ToArray(),
            Contents = zone.Contents?.PackZone() ?? zone.PackedContents,
            Owner = zone.Owner == null ? -1 : Array.IndexOf(factions, zone.Owner)
        }).ToArray();
        return (game, zones);
    }

    // The stored ledger, or a fresh empty one when the run has minted nothing yet.
    public static ProvenanceLedger Lots(CultCache cache) => cache.GetGlobal<ProvenanceLedger>() ?? new ProvenanceLedger();

    // The only writer of SavedGame and SavedZone: one Commit to the run store. Zone i lands at savedzone-{i}, stable
    // because a run's zone array is fixed at generation, and every stored SavedZone outside that set is removed. The
    // run store's single-file commit writes its whole view, so orbits and bodies staged by zone generation land too.
    // The ledger lands too, pruned to what the committed zones' entities still reach; the live ledger is untouched.
    public static void Commit(CultCache cache, SavedGame game, IReadOnlyList<SavedZone> zones, ProvenanceLedger lots)
    {
        var keys = zones.Select((_, i) => new CultRecordKey($"savedzone-{i}")).ToArray();
        var kept = new HashSet<CultRecordKey>(keys);
        var stale = cache.AllStoredDocuments
            .Where(stored => stored.Document is SavedZone && !kept.Contains(stored.Key))
            .Select(stored => stored.Key)
            .ToArray();
        game.Zones = keys.Select(key => new CultRecordRef<SavedZone>(key)).ToArray();
        var roots = zones
            .SelectMany(zone => zone.Contents?.Entities ?? new List<EntityPack>())
            .SelectMany(EntitySerializer.Items)
            .OfType<CraftedItemInstance>()
            .Select(item => item.Lot);
        cache.Commit(batch =>
        {
            for (var i = 0; i < keys.Length; i++) batch.Upsert(typeof(SavedZone), zones[i], keys[i]);
            batch.Upsert(game);
            batch.Upsert(lots.Reachable(roots));
            foreach (var key in stale) batch.Remove(key);
        });
    }

    // Removes every run record (SavedGame, SavedZone, OrbitData, BodyData, ProvenanceLedger) in one Commit.
    public static void Clear(CultCache cache)
    {
        var run = cache.AllStoredDocuments
            .Where(stored => IsRunRecord(stored.Descriptor.DocumentType))
            .Select(stored => stored.Key)
            .ToArray();
        cache.Commit(batch =>
        {
            foreach (var key in run) batch.Remove(key);
        });
    }

    public static bool IsRunRecord(Type documentType) =>
        AetheriaStores.RunTypes.Any(home => home.IsAssignableFrom(documentType));
}

[CultDocument("aetheria.savedzone", "1"), MessagePackObject]
public class SavedZone
{
    [Key(0)]
    public string Name;

    [Key(1)]
    public float2 Position;

    [Key(2)]
    public int[] AdjacentZones;

    [Key(3)]
    public int[] Factions;

    [Key(4)]
    public int Owner;

    [Key(5)]
    public ZonePack Contents;
}

[MessagePackObject,
 Union(0, typeof(SavedActionBarConsumableBinding)),
 Union(1, typeof(SavedActionBarGearBinding)),
 Union(2, typeof(SavedActionBarWeaponGroupBinding))
]
public abstract class SavedActionBarBinding
{
}

[MessagePackObject]
public class SavedActionBarConsumableBinding : SavedActionBarBinding
{
    [Key(0)] public CultRecordRef<ConsumableItemData> Target;
}

[MessagePackObject]
public class SavedActionBarGearBinding : SavedActionBarBinding
{
    [Key(0)] public int EquipmentIndex;
    [Key(1)] public int BehaviorIndex;
}

[MessagePackObject]
public class SavedActionBarWeaponGroupBinding : SavedActionBarBinding
{
    [Key(0)] public int Group;
}
