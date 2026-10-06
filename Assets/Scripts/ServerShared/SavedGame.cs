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

    [Key(13)]
    public int Credits;
}

// The run's lifecycle over the run store. Only Commit creates SavedGame, SavedZone and ProvenanceLedger records;
// the ledger is the fifth run record.
public static class RunSave
{
    // The live run as plain documents. Writes nothing.
    public static (SavedGame Game, SavedZone[] Zones) Capture(CultCache cache, Galaxy galaxy, Zone currentZone,
        Entity currentEntity, SavedActionBarBinding[] actionBar, int credits)
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
            IsTutorial = galaxy.IsPrelude,
            ActionBarBindings = actionBar,
            Credits = credits
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

    // Continue's gate: every design a saved run names (each zone's items and each minted lot) must be one this catalog
    // holds. A run made with a mod that is no longer installed is refused, naming the mod ships, before any scene is
    // built; the save is never edited to fit.
    public static void RequireDesigns(CultCache cache, SavedGame game)
    {
        var designs = new List<CultRecordKey>();
        foreach (var zoneRef in game.Zones ?? Array.Empty<CultRecordRef<SavedZone>>())
        {
            var where = zoneRef.Key.Value;
            var zone = cache.Get(zoneRef) ?? throw MalformedSave($"zone {where} has no record");
            // A zone never visited has no contents: legitimately null.
            foreach (var pack in zone.Contents?.Entities ?? new List<EntityPack>())
                foreach (var item in EntitySerializer.Items(pack))
                    designs.Add((item ?? throw MalformedSave($"an item in {where} is null")).Data.Key);
        }
        foreach (var (number, lot) in Lots(cache).Lots ?? throw MalformedSave("the lot ledger has no lots"))
            designs.Add((lot ?? throw MalformedSave($"lot {number} is null")).Design.Key);
        var named = designs.Where(key => key.IsSet()).Distinct()
            .Where(key => cache.Get<ItemData>(key) == null)
            .Select(key => key.Value).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        if (named.Length == 0) return;
        const string modHull = "mod-hull:";
        var mods = named.Where(key => key.StartsWith(modHull, StringComparison.Ordinal)).Select(key => key.Substring(modHull.Length)).ToArray();
        var others = named.Where(key => !key.StartsWith(modHull, StringComparison.Ordinal)).ToArray();
        throw new InvalidOperationException("This run names designs the catalog no longer holds" +
            (mods.Length > 0 ? $"; missing mod ships: {string.Join(", ", mods)}" : "") +
            (others.Length > 0 ? $"; missing other designs: {string.Join(", ", others)}" : "") +
            ". Reinstall them, or start a new game.");
    }

    private static InvalidOperationException MalformedSave(string where) =>
        new InvalidOperationException($"This run's save is malformed: {where}. Start a new game.");

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
    public static void Clear(CultCache cache) => Remove(cache, Records(cache));

    // A new run replaces the saved one only once it has started: start runs with the saved run still in the store.
    // When it returns a value, the saved run's records go. When it returns null or throws, the records it wrote go and
    // the saved run stays as it was.
    public static T Replace<T>(CultCache cache, Func<T> start) where T : class
    {
        var saved = Records(cache);
        T started = null;
        try
        {
            started = start();
        }
        finally
        {
            Remove(cache, started != null ? saved : Records(cache).Except(saved).ToArray());
        }
        return started;
    }

    private static CultRecordKey[] Records(CultCache cache) =>
        cache.AllStoredDocuments
            .Where(stored => IsRunRecord(stored.Descriptor.DocumentType))
            .Select(stored => stored.Key)
            .ToArray();

    private static void Remove(CultCache cache, CultRecordKey[] records) =>
        cache.Commit(batch =>
        {
            foreach (var key in records) batch.Remove(key);
        });

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
