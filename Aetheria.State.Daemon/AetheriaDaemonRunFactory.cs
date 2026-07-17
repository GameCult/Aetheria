using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

public static class AetheriaDaemonRunFactory
{
    public static async Task<AetheriaDaemonWrittenRun> WriteAsync(
        AetheriaStateNode node,
        AetheriaRuntimeCatalogSnapshot catalog,
        string now,
        string generationIdentity,
        AetheriaDaemonRegularTopologySettings? regularTopologySettings = null)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
            .ReadAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
        if (!settings.TutorialPassed)
            return await AetheriaDaemonTutorialRunWriter.WriteAsync(node, catalog, now).ConfigureAwait(false);

        return await AetheriaDaemonRegularRunWriter.WriteAsync(
            node,
            catalog,
            now,
            StableSeed(generationIdentity),
            regularTopologySettings).ConfigureAwait(false);
    }

    public static uint StableSeed(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value ?? "")
        {
            hash ^= character;
            hash *= 16777619u;
        }
        return hash == 0 ? 1u : hash;
    }
}
