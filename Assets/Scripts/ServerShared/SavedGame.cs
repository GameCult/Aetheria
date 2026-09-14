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
