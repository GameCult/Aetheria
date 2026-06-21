using System.IO;

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

        public static bool TryReadView(string stateFilePath, out AetheriaRuntimeDaemonSoaViewDocument view)
        {
            var path = GetViewPath(stateFilePath);
            if (!File.Exists(path))
            {
                view = new AetheriaRuntimeDaemonSoaViewDocument();
                return false;
            }

            view = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonSoaView(path);
            return true;
        }

        public static AetheriaRuntimeDaemonSoaViewDocument ReadView(string stateFilePath)
        {
            return AetheriaRuntimeCultCacheDocumentStore.ReadDaemonSoaView(GetViewPath(stateFilePath));
        }
    }
}
