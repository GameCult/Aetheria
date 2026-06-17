using Aetheria.State.Documents;

namespace Aetheria.State;

public static class AetheriaVerseHostSettingsNormalizer
{
    public static AetheriaVerseHostSettings Normalize(AetheriaVerseHostSettings? settings)
    {
        return new AetheriaVerseHostSettings
        {
            Schema = NormalizeOrDefault(settings?.Schema, "aetheria.verse_host_settings.v1"),
            ServiceId = NormalizeOrDefault(settings?.ServiceId, "aetheria.runtime"),
            VerseId = NormalizeOrDefault(settings?.VerseId, "aetheria.local"),
            RootVerse = NormalizeOrDefault(settings?.RootVerse, "asgard"),
            CanonicalService = NormalizeOrDefault(settings?.CanonicalService, "asgard.aetheria"),
            LocatedService = NormalizeOrDefault(settings?.LocatedService, "asgard.local.aetheria"),
            CultMeshAddress = NormalizeOrDefault(settings?.CultMeshAddress, "asgard.local.aetheria/eve"),
            Title = NormalizeOrDefault(settings?.Title, "Aetheria"),
            Visibility = NormalizeOrDefault(settings?.Visibility, "private"),
            LastUpdatedAtUtc = settings?.LastUpdatedAtUtc?.Trim() ?? ""
        };
    }

    public static bool Equivalent(AetheriaVerseHostSettings? left, AetheriaVerseHostSettings? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return string.Equals(normalizedLeft.Schema, normalizedRight.Schema, StringComparison.Ordinal) &&
               string.Equals(normalizedLeft.ServiceId, normalizedRight.ServiceId, StringComparison.Ordinal) &&
               string.Equals(normalizedLeft.VerseId, normalizedRight.VerseId, StringComparison.Ordinal) &&
               string.Equals(normalizedLeft.RootVerse, normalizedRight.RootVerse, StringComparison.Ordinal) &&
               string.Equals(normalizedLeft.CanonicalService, normalizedRight.CanonicalService, StringComparison.Ordinal) &&
               string.Equals(normalizedLeft.LocatedService, normalizedRight.LocatedService, StringComparison.Ordinal) &&
               string.Equals(normalizedLeft.CultMeshAddress, normalizedRight.CultMeshAddress, StringComparison.Ordinal) &&
               string.Equals(normalizedLeft.Title, normalizedRight.Title, StringComparison.Ordinal) &&
               string.Equals(normalizedLeft.Visibility, normalizedRight.Visibility, StringComparison.Ordinal);
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }
}
