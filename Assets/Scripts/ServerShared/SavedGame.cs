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

    public SavedGame() { }

    // Writes a SavedZone record for every galaxy zone through the cache.
    public SavedGame(CultCache cache, Galaxy galaxy, Zone currentZone, Entity currentEntity)
    {
        DiscoveredZones = galaxy.DiscoveredZones.Select(dz => Array.IndexOf(galaxy.Zones, dz)).ToArray();
        Background = galaxy.Background;
        var factions = galaxy.HomeZones.Keys.ToArray();
        Factions = factions.Select(f => cache.RefOf(f)).ToArray();
        Relationships = galaxy.Factions.Select(f => galaxy.FactionRelationships[f]).ToArray();

        HomeZones = galaxy.HomeZones.ToDictionary(
            x => Array.IndexOf(factions, x.Key),
            x => Array.IndexOf(galaxy.Zones, x.Value));
        BossZones = galaxy.BossZones.ToDictionary(
            x => Array.IndexOf(factions, x.Key),
            x => Array.IndexOf(galaxy.Zones, x.Value));

        Zones = galaxy.Zones.Select(zone => cache.Upsert(new SavedZone
        {
            Name = zone.Name,
            Position = zone.Position,
            AdjacentZones = zone.AdjacentZones.Select(az=> Array.IndexOf(galaxy.Zones, az)).ToArray(),
            Factions = zone.Factions.Select(f=> Array.IndexOf(factions, f)).ToArray(),
            Contents = zone.Contents?.PackZone(),
            Owner = zone.Owner == null ? -1 : Array.IndexOf(factions, zone.Owner)
        })).ToArray();

        CurrentZone = Array.FindIndex(galaxy.Zones, zone => zone.Contents == currentZone);
        CurrentZoneEntity = currentZone.Entities.IndexOf(currentEntity);

        Entrance = Array.IndexOf(galaxy.Zones, galaxy.Entrance);
        Exit = Array.IndexOf(galaxy.Zones, galaxy.Exit);
    }
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
