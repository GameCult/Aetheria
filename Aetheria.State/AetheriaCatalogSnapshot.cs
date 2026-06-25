using System;
using System.Collections.Generic;
using System.Linq;
using Aetheria.State.Documents;

namespace Aetheria.State;

public sealed class AetheriaCatalogSnapshot
{
    private readonly Dictionary<string, AetheriaItemDefinition> _itemsByLegacyId;
    private readonly Dictionary<string, AetheriaCorporation> _corporationsByLegacyId;
    private readonly Dictionary<string, AetheriaCorporation> _corporationsByKey;
    private readonly Dictionary<string, AetheriaNameFile> _nameFilesByLegacyId;
    private readonly Dictionary<string, AetheriaNameFile> _nameFilesByKey;

    public AetheriaCatalogSnapshot(
        IEnumerable<AetheriaItemDefinition> items,
        IEnumerable<AetheriaCorporation> corporations,
        IEnumerable<AetheriaNameFile> nameFiles,
        AetheriaTradeValuePolicy? tradeValuePolicy = null)
    {
        Items = items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        Corporations = corporations.OrderBy(corporation => corporation.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        NameFiles = nameFiles.OrderBy(nameFile => nameFile.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        TradeValuePolicy = tradeValuePolicy;

        _itemsByLegacyId = Items
            .Where(item => !string.IsNullOrWhiteSpace(item.LegacyId))
            .ToDictionary(item => item.LegacyId, StringComparer.OrdinalIgnoreCase);
        _corporationsByLegacyId = Corporations
            .Where(corporation => !string.IsNullOrWhiteSpace(corporation.LegacyId))
            .ToDictionary(corporation => corporation.LegacyId, StringComparer.OrdinalIgnoreCase);
        _corporationsByKey = Corporations
            .Where(corporation => !string.IsNullOrWhiteSpace(corporation.CorporationKey))
            .ToDictionary(corporation => corporation.CorporationKey, StringComparer.OrdinalIgnoreCase);
        _nameFilesByLegacyId = NameFiles
            .Where(nameFile => !string.IsNullOrWhiteSpace(nameFile.LegacyId))
            .ToDictionary(nameFile => nameFile.LegacyId, StringComparer.OrdinalIgnoreCase);
        _nameFilesByKey = NameFiles
            .Where(nameFile => !string.IsNullOrWhiteSpace(nameFile.NameFileKey))
            .ToDictionary(nameFile => nameFile.NameFileKey, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AetheriaItemDefinition> Items { get; }

    public IReadOnlyList<AetheriaCorporation> Corporations { get; }

    public IReadOnlyList<AetheriaNameFile> NameFiles { get; }

    public AetheriaTradeValuePolicy? TradeValuePolicy { get; }

    public IEnumerable<AetheriaItemDefinition> TradeItems =>
        Items.Where(item => item.Price > 0).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

    public IEnumerable<AetheriaItemDefinition> EquipmentItems =>
        Items.Where(item => !string.IsNullOrWhiteSpace(item.HardpointType))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

    public IEnumerable<AetheriaItemDefinition> FindItemsByBehavior(string behaviorKind)
    {
        return string.IsNullOrWhiteSpace(behaviorKind)
            ? []
            : Items
                .Where(item => item.BehaviorKinds.Contains(behaviorKind, StringComparer.OrdinalIgnoreCase))
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<AetheriaItemDefinition> FindItemsByHardpoint(string hardpointType)
    {
        return string.IsNullOrWhiteSpace(hardpointType)
            ? []
            : Items
                .Where(item => string.Equals(item.HardpointType, hardpointType, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    public AetheriaItemDefinition? FindItemByLegacyId(string legacyId)
    {
        return TryGet(_itemsByLegacyId, legacyId);
    }

    public AetheriaCorporation? FindCorporationByLegacyId(string legacyId)
    {
        return TryGet(_corporationsByLegacyId, legacyId);
    }

    public AetheriaCorporation? FindCorporation(string corporationKey)
    {
        return TryGet(_corporationsByKey, corporationKey);
    }

    public AetheriaNameFile? FindNameFileByLegacyId(string legacyId)
    {
        return TryGet(_nameFilesByLegacyId, legacyId);
    }

    public AetheriaNameFile? FindNameFile(string nameFileKey)
    {
        return TryGet(_nameFilesByKey, nameFileKey);
    }

    public AetheriaCorporation? FindCorporationByNamePrefix(string namePrefix)
    {
        return string.IsNullOrWhiteSpace(namePrefix)
            ? null
            : Corporations.FirstOrDefault(corporation =>
                corporation.Name.StartsWith(namePrefix, StringComparison.InvariantCultureIgnoreCase));
    }

    public AetheriaCorporation? GetManufacturer(AetheriaItemDefinition item)
    {
        return FindCorporation(item.ManufacturerKey);
    }

    public AetheriaNameFile? GetNameFile(AetheriaCorporation corporation)
    {
        return FindNameFile(corporation.GeonameFileKey);
    }

    private static T? TryGet<T>(IReadOnlyDictionary<string, T> dictionary, string key) where T : class
    {
        return string.IsNullOrWhiteSpace(key) ? null : dictionary.TryGetValue(key, out var value) ? value : null;
    }
}
