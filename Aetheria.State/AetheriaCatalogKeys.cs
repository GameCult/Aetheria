using GameCult.Caching;

namespace Aetheria.State;

public static class AetheriaCatalogKeys
{
    public static CultRecordKey ItemDefinitionFromLegacyId(string legacyId)
    {
        return CreateLegacyKey("item", legacyId);
    }

    public static CultRecordKey CorporationFromLegacyId(string legacyId)
    {
        return CreateLegacyKey("faction", legacyId);
    }

    public static CultRecordKey NameFileFromLegacyId(string legacyId)
    {
        return CreateLegacyKey("name-file", legacyId);
    }

    private static CultRecordKey CreateLegacyKey(string catalogKind, string legacyId)
    {
        if (string.IsNullOrWhiteSpace(legacyId))
        {
            throw new ArgumentException("Legacy catalog id must be non-empty.", nameof(legacyId));
        }

        return new CultRecordKey($"legacy:{catalogKind}:{legacyId.Trim()}");
    }
}
