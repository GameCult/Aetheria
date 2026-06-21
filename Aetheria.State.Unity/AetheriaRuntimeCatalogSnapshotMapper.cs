using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

namespace Aetheria.State.Unity;

internal static class AetheriaRuntimeCatalogSnapshotMapper
{
    public static AetheriaRuntimeCatalogSnapshot FromCatalog(AetheriaCatalogSnapshot catalog)
    {
        return new AetheriaRuntimeCatalogSnapshot(
            catalog.Items.Select(FromState).ToArray(),
            catalog.Corporations.Select(FromState).ToArray(),
            catalog.NameFiles.Select(FromState).ToArray());
    }

    private static AetheriaRuntimeCatalogItem FromState(AetheriaItemDefinition item)
    {
        return new AetheriaRuntimeCatalogItem(
            item.ItemKey,
            item.Name,
            item.Category,
            item.Description,
            item.ManufacturerKey,
            item.Price,
            item.Mass,
            item.SpecificHeat,
            item.Conductivity,
            item.Volume,
            item.ShapeWidth,
            item.ShapeHeight,
            item.OccupiedCells,
            item.ShapeCells.Select(FromState).ToArray(),
            item.InteriorShapeWidth,
            item.InteriorShapeHeight,
            item.InteriorOccupiedCells,
            item.InteriorShapeCells.Select(FromState).ToArray(),
            item.Hardpoints.Select(FromState).ToArray(),
            item.BehaviorPayloads.Select(FromState).ToArray(),
            item.HardpointType,
            item.HullType,
            item.BehaviorKinds,
            item.MaxStack,
            item.Stackable,
            item.Duration,
            item.Durability,
            item.WeaponRange,
            item.WeaponCaliber,
            item.WeaponType,
            item.WeaponFireTypes,
            item.WeaponModifiers,
            item.MinimumTemperature,
            item.MaximumTemperature,
            item.ThermalPerformanceCurveKeys.Select(FromState).ToArray(),
            item.HullPrefab,
            item.ThermalResilience,
            item.HullGridOffset,
            item.HullArmor,
            item.HullDrag,
            item.HullCanTow,
            item.DockingMaxSizeX,
            item.DockingMaxSizeY,
            item.ActionBarIcon,
            item.AudioStats.Select(FromState).ToArray(),
            item.EffectivenessCurveKeys.Select(FromState).ToArray(),
            item.SimpleCommodityCategory,
            item.CompoundCommodityCategory);
    }

    private static AetheriaRuntimeAudioStat FromState(AetheriaItemAudioStat stat)
    {
        return new AetheriaRuntimeAudioStat(stat.Parameter, FromState(stat.Stat));
    }

    private static AetheriaRuntimePerformanceStat FromState(AetheriaItemPerformanceStat stat)
    {
        return new AetheriaRuntimePerformanceStat(
            stat.Min,
            stat.Max,
            stat.HeatExponentMultiplier,
            stat.DurabilityExponentMultiplier,
            stat.QualityExponent,
            null);
    }

    private static AetheriaRuntimeShapeCell FromState(AetheriaShapeCell cell)
    {
        return new AetheriaRuntimeShapeCell(cell.X, cell.Y);
    }

    private static AetheriaRuntimeCurveKey FromState(AetheriaCurveKey key)
    {
        return new AetheriaRuntimeCurveKey(key.Time, key.Value, key.InTangent, key.OutTangent);
    }

    private static AetheriaRuntimeBehaviorPayload FromState(AetheriaBehaviorPayload payload)
    {
        return new AetheriaRuntimeBehaviorPayload(
            payload.UnionKey,
            payload.Kind,
            payload.Group,
            payload.Fields.Select(FromState).ToArray());
    }

    private static AetheriaRuntimeBehaviorField FromState(AetheriaBehaviorField field)
    {
        return new AetheriaRuntimeBehaviorField(field.Key, FromState(field.Value));
    }

    private static AetheriaRuntimeBehaviorMapEntry FromState(AetheriaBehaviorMapEntry entry)
    {
        return new AetheriaRuntimeBehaviorMapEntry(entry.Key, FromState(entry.Value));
    }

    private static AetheriaRuntimeBehaviorValue FromState(AetheriaBehaviorValue value)
    {
        return new AetheriaRuntimeBehaviorValue(
            value.Kind,
            value.StringValue,
            value.NumberValue,
            value.BoolValue,
            value.LegacyIdValue,
            value.ItemKeyValue,
            value.Children.Select(FromState).ToArray(),
            value.MapEntries.Select(FromState).ToArray());
    }

    private static AetheriaRuntimeHardpoint FromState(AetheriaItemHardpoint hardpoint)
    {
        return new AetheriaRuntimeHardpoint(
            hardpoint.Type,
            hardpoint.PositionX,
            hardpoint.PositionY,
            hardpoint.ShapeWidth,
            hardpoint.ShapeHeight,
            hardpoint.OccupiedCells,
            hardpoint.ShapeCells.Select(FromState).ToArray(),
            hardpoint.Transform,
            hardpoint.Rotation,
            hardpoint.Armor);
    }

    private static AetheriaRuntimeCorporation FromState(AetheriaCorporation corporation)
    {
        return new AetheriaRuntimeCorporation(
            corporation.CorporationKey,
            corporation.Name,
            corporation.ShortName,
            corporation.Description,
            corporation.GeonameFileKey,
            corporation.BossHullItemKey,
            corporation.InfluenceDistance,
            corporation.AllegianceCount,
            corporation.Allegiances.Select(allegiance => new AetheriaRuntimeCorporationAllegiance(
                allegiance.CorporationKey,
                allegiance.Weight)).ToArray());
    }

    private static AetheriaRuntimeNameFile FromState(AetheriaNameFile nameFile)
    {
        return new AetheriaRuntimeNameFile(
            nameFile.NameFileKey,
            nameFile.Name,
            nameFile.NameCount,
            nameFile.SampleNames,
            nameFile.Names);
    }
}
