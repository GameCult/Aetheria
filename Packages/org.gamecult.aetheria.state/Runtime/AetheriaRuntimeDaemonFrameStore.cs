using System;
using System.IO;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeDaemonFrameStore
    {
        public static string GetFramePath(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            return AetheriaRuntimeStateBoundary.GetDaemonFramePath(stateFilePath);
        }

        public static string PublishFrame(string stateFilePath, AetheriaRuntimeDaemonFrameDocument frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            frame.Schema = AetheriaRuntimeDaemonSchemas.Frame;
            if (string.IsNullOrWhiteSpace(frame.PublishedAtUtc))
                frame.PublishedAtUtc = DateTime.UtcNow.ToString("O");

            var framePath = GetFramePath(stateFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(framePath) ?? ".");
            AetheriaRuntimeCultCacheDocumentStore.WriteDaemonFrame(framePath, frame);
            return framePath;
        }

        public static bool TryReadFrame(string stateFilePath, out AetheriaRuntimeDaemonFrameDocument frame)
        {
            var framePath = GetFramePath(stateFilePath);
            if (!File.Exists(framePath))
            {
                frame = new AetheriaRuntimeDaemonFrameDocument();
                return false;
            }

            frame = AetheriaRuntimeCultCacheDocumentStore.ReadDaemonFrame(framePath);
            return true;
        }

        public static AetheriaRuntimeDaemonFrameDocument ReadFrame(string stateFilePath)
        {
            return AetheriaRuntimeCultCacheDocumentStore.ReadDaemonFrame(GetFramePath(stateFilePath));
        }
    }
}
