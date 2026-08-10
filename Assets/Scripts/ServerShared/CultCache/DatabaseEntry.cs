/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using JsonKnownTypes;
using MessagePack;
using MessagePack.Formatters;
using Newtonsoft.Json;
// TODO: USE THIS EVERYWHERE
using Unity.Mathematics;
using static Unity.Mathematics.math;

public interface INamedEntry
{
    string EntryName { get; set; }
}

[MessagePackObject,
 JsonObject(MemberSerialization.OptIn), 
 JsonConverter(typeof(JsonKnownTypesConverter<DatabaseEntry>)),
 Union(0, typeof(SimpleCommodityData)), 
 Union(1, typeof(CompoundCommodityData)),
 Union(2, typeof(GearData)), 
 Union(3, typeof(HullData)), 
 Union(4, typeof(SimpleCommodity)), 
 Union(5, typeof(CompoundCommodity)), 
 Union(6, typeof(EquippableItem)),
 Union(8, typeof(GalaxyMapLayerData)),
 Union(9, typeof(NameFile)), 
 //Union(10, typeof(ZoneData)), 
 Union(11, typeof(PlayerData)), 
 // Union(12, typeof(Corporation)),
 Union(13, typeof(Faction)),
 Union(14, typeof(OrbitalEntity)), 
 Union(15, typeof(OrbitData)), 
 Union(16, typeof(BodyData)),
 Union(17, typeof(PersonalityAttribute)),
 Union(18, typeof(AgentTask)),
 // Union(19, typeof(LoadoutData)),
 Union(20, typeof(Ship)),
 Union(21, typeof(Mining)),
 Union(22, typeof(StationTowing)), 
 Union(23, typeof(Survey)), 
 Union(24, typeof(HaulingTask)), 
 Union(25, typeof(AsteroidBeltData)), 
 Union(26, typeof(GasGiantData)), 
 Union(27, typeof(SunData)), 
 Union(28, typeof(PlanetData)), 
 Union(29, typeof(CargoBayData)), 
 Union(30, typeof(DockingBayData)), 
 Union(31, typeof(WeaponItemData))]
public abstract class DatabaseEntry
{
    [JsonProperty("id"), Key(0)]
    public Guid ID = Guid.NewGuid();

    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is DatabaseEntry entry) return entry.ID == ID;
        return false;
    }
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class DatabaseLink<T> : DatabaseLinkBase where T : DatabaseEntry
{
    [IgnoreMember]
    public T Value => Cache.Get<T>(LinkID);
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class DatabaseLinkBase
{
    [Key(0), JsonProperty("linkID")]
    public Guid LinkID;
    
    [IgnoreMember]
    public static CultCache Cache;
}

public interface ITintInspector
{
    float3 TintColor { get; }
}