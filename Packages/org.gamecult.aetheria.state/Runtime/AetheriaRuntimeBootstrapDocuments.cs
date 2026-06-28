namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeBootstrapDocuments
    {
        public static AetheriaRuntimeCatalogSnapshot RuntimeCatalog(string statePath)
        {
            return AetheriaRuntimeCatalogStore.OpenReadOnly(statePath);
        }

        public static AetheriaRuntimeLoadoutTemplatesDocument LoadoutTemplates(string statePath)
        {
            return new AetheriaRuntimeLoadoutTemplatesDocument(
                AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(statePath));
        }

        public static AetheriaRuntimePlayerSettingsDocument PlayerSettings(string statePath)
        {
            return AetheriaRuntimePlayerSettingsDocument.FromSnapshot(
                AetheriaRuntimeCatalogStore.ReadPlayerSettings(statePath));
        }

        public static AetheriaRuntimeVerseHostSettingsDocument VerseHostSettings(string statePath)
        {
            return AetheriaRuntimeVerseHostSettingsDocument.FromSnapshot(
                AetheriaRuntimeCatalogStore.ReadVerseHostSettings(statePath));
        }
    }
}
