#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeDaemonSoaViewStore
    {
        public static string GetViewPath(string stateFilePath)
        {
            return AetheriaRuntimeStateBoundary.GetDaemonSoaViewPath(stateFilePath);
        }

        public static string PublishView(string stateFilePath, AetheriaRuntimeDaemonSoaViewDocument view)
        {
            var path = GetViewPath(stateFilePath);
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonSoaView(path, view);
            return path;
        }
    }
}
