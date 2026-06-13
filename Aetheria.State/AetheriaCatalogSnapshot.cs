using System;
using System.Collections.Generic;
using System.Linq;
using Aetheria.State.Documents;

namespace Aetheria.State;

public sealed class AetheriaCatalogSnapshot
{
    private readonly Dictionary<string, AetheriaItemDefinition> _itemsByLegacyId;
    private readonly Dictionary<string, AetheriaCorporation> _corporationsByLegacyId;
    private readonly Dictionary<string, AetheriaNameFile> _nameFilesByLegacyId;

    public AetheriaCatalogSnapshot(
        IEnumerable<AetheriaItemDefinition> items,
        IEnumerable<AetheriaCorporation> corporations,
        IEnumerable<AetheriaNameFile> nameFiles)
    {
        Items = items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        Corporations = corporations.OrderBy(corporation => corporation.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        NameFiles = nameFiles.OrderBy(nameFile => nameFile.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        _itemsByLegacyId = Items
            .Where(item => !string.IsNullOrWhiteSpace(item.LegacyId))
            .ToDictionary(item => item.LegacyId, StringComparer.OrdinalIgnoreCase);
        _corporationsByLegacyId = Corporations
            .Where(corporation => !string.IsNullOrWhiteSpace(corporation.LegacyId))
            .ToDictionary(corporation => corporation.LegacyId, StringComparer.OrdinalIgnoreCase);
        _nameFilesByLegacyId = NameFiles
            .Where(nameFile => !string.IsNullOrWhiteSpace(nameFile.LegacyId))
            .ToDictionary(nameFile => nameFile.LegacyId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AetheriaItemDefinition> Items { get; }

    public IReadOnlyList<AetheriaCorporation> Corporations { get; }

    public IReadOnlyList<AetheriaNameFile> NameFiles { get; }

    public IEnumerable<AetheriaItemDefinition> TradeItems =>
        Items.Where(item => item.Price > 0).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

    public AetheriaItemDefinition? FindItemByLegacyId(string legacyId)
    {
        return TryGet(_itemsByLegacyId, legacyId);
    }

    public AetheriaCorporation? FindCorporationByLegacyId(string legacyId)
    {
        return TryGet(_corporationsByLegacyId, legacyId);
    }

    public AetheriaNameFile? FindNameFileByLegacyId(string legacyId)
    {
        return TryGet(_nameFilesByLegacyId, legacyId);
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
        return FindCorporationByLegacyId(item.ManufacturerLegacyId);
    }

    public AetheriaNameFile? GetNameFile(AetheriaCorporation corporation)
    {
        return FindNameFileByLegacyId(corporation.GeonameFileLegacyId);
    }

    private static T? TryGet<T>(IReadOnlyDictionary<string, T> dictionary, string key) where T : class
    {
        return string.IsNullOrWhiteSpace(key) ? null : dictionary.TryGetValue(key, out var value) ? value : null;
    }
}
