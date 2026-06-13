/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using MessagePack.Formatters;
// TODO: USE THIS EVERYWHERE
using Unity.Mathematics;
using static Unity.Mathematics.math;

public interface INamedEntry
{
    string EntryName { get; set; }
}

[MessagePackObject,
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
    [Key(0)]
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

[MessagePackObject]
public class DatabaseLink<T> : DatabaseLinkBase where T : ItemData
{
    [IgnoreMember]
    public T Value => ResolveLegacyCatalog<T>(LinkID);
}

[MessagePackObject]
public class DatabaseLinkBase
{
    [Key(0)]
    public Guid LinkID;

    [IgnoreMember]
    private static ILegacyCatalogReader Catalog { get; set; }

    protected static T ResolveLegacyCatalog<T>(Guid linkId) where T : ItemData
    {
        if (Catalog == null)
            throw new InvalidOperationException("Legacy item link resolution is not bound. Open LegacyCatalogBoundary before reading legacy item object graphs.");

        return Catalog.Get<T>(linkId);
    }

    public static void BindLegacyCatalog(ILegacyCatalogReader catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        Catalog = catalog;
    }
}

public interface ITintInspector
{
    float3 TintColor { get; }
}
