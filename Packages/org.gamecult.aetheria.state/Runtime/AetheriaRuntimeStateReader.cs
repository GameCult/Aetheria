using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    // Unity runtime shells acquire typed state through this port so the transport can move to the daemon without spelunking every consumer.
    public static class AetheriaRuntimeStateReader
    {
        public static AetheriaRuntimeCatalogSnapshot OpenRuntimeCatalog(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.OpenReadOnly(stateFilePath);
        }

        public static AetheriaRuntimePlayerSettingsSnapshot? ReadPlayerSettings(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadPlayerSettings(stateFilePath);
        }

        public static AetheriaRuntimeVerseHostSettingsSnapshot? ReadVerseHostSettings(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadVerseHostSettings(stateFilePath);
        }

        public static IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot> ReadLoadoutTemplates(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(stateFilePath);
        }

        public static IReadOnlyList<AetheriaRuntimeRunStateSnapshot> ReadRunStates(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadRunStates(stateFilePath);
        }

        public static IReadOnlyList<AetheriaRuntimeZoneStateSnapshot> ReadZoneStates(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadZoneStates(stateFilePath);
        }

        public static IReadOnlyList<AetheriaRuntimeEntitySnapshot> ReadEntitySnapshots(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadEntitySnapshots(stateFilePath);
        }

        public static bool TryReadDaemonFrame(string stateFilePath, out AetheriaRuntimeDaemonFrameDocument frame)
        {
            return AetheriaRuntimeDaemonFrameStore.TryReadFrame(stateFilePath, out frame);
        }

        public static bool TryReadDaemonSoaView(string stateFilePath, out AetheriaRuntimeDaemonSoaViewDocument view)
        {
            return AetheriaRuntimeDaemonSoaViewStore.TryReadView(stateFilePath, out view);
        }

        public static bool TryReadObservedDaemonState(
            string stateFilePath,
            out AetheriaRuntimeObservedDaemonState observed)
        {
            if (!AetheriaRuntimeDaemonFrameStore.TryReadFrame(stateFilePath, out var frame))
            {
                observed = new AetheriaRuntimeObservedDaemonState(
                    new AetheriaRuntimeDaemonFrameDocument { IsAuthoritative = false, StateSource = "missing" },
                    null,
                    AetheriaRuntimeDaemonFrameStore.GetFramePath(stateFilePath),
                    AetheriaRuntimeDaemonSoaViewStore.GetViewPath(stateFilePath));
                return false;
            }

            AetheriaRuntimeDaemonSoaViewStore.TryReadView(stateFilePath, out var soaView);
            observed = new AetheriaRuntimeObservedDaemonState(
                frame,
                string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal) ? soaView : null,
                AetheriaRuntimeDaemonFrameStore.GetFramePath(stateFilePath),
                AetheriaRuntimeDaemonSoaViewStore.GetViewPath(stateFilePath));
            return true;
        }

        public static EveSurfaceDocument? ReadEveSurface(string stateFilePath, string surfaceId)
        {
            if (string.Equals(surfaceId, AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, StringComparison.Ordinal) &&
                TryReadDaemonGameSurface(stateFilePath, out var gameSurface))
            {
                return gameSurface;
            }

            if (string.Equals(surfaceId, AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, StringComparison.Ordinal) &&
                TryReadDaemonGameTuiSurface(stateFilePath, out var gameTuiSurface))
            {
                return gameTuiSurface;
            }

            if (string.Equals(surfaceId, AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, StringComparison.Ordinal) &&
                TryReadDaemonEditorSurface(stateFilePath, out var editorSurface))
            {
                return editorSurface;
            }

            if (string.Equals(surfaceId, AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, StringComparison.Ordinal) &&
                TryReadDaemonEditorTuiSurface(stateFilePath, out var editorTuiSurface))
            {
                return editorTuiSurface;
            }

            return AetheriaRuntimeCatalogStore.ReadEveSurfaces(stateFilePath)
                .FirstOrDefault(candidate => string.Equals(candidate.Surface.Id, surfaceId, StringComparison.Ordinal));
        }

        public static bool TryReadDaemonGameSurface(
            string stateFilePath,
            out EveSurfaceDocument document)
        {
            if (!AetheriaRuntimeDaemonPublicationStore.TryReadGameSurface(stateFilePath, out var surface))
            {
                document = EmptySurface(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId);
                return false;
            }

            document = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(surface);
            return true;
        }

        public static bool TryReadDaemonGameTuiSurface(
            string stateFilePath,
            out EveSurfaceDocument document)
        {
            if (!AetheriaRuntimeDaemonPublicationStore.TryReadGameTuiSurface(stateFilePath, out var surface))
            {
                document = EmptySurface(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId);
                return false;
            }

            document = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(surface);
            return true;
        }

        public static bool TryReadDaemonEditorSurface(
            string stateFilePath,
            out EveSurfaceDocument document)
        {
            if (!AetheriaRuntimeDaemonPublicationStore.TryReadEditorSurface(stateFilePath, out var surface))
            {
                document = EmptySurface(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId);
                return false;
            }

            document = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(surface);
            return true;
        }

        public static bool TryReadDaemonEditorTuiSurface(
            string stateFilePath,
            out EveSurfaceDocument document)
        {
            if (!AetheriaRuntimeDaemonPublicationStore.TryReadEditorTuiSurface(stateFilePath, out var surface))
            {
                document = EmptySurface(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId);
                return false;
            }

            document = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(surface);
            return true;
        }

        private static EveSurfaceDocument EmptySurface(string surfaceId)
        {
            return AetheriaRuntimeEveSurfaceAdapter.EmptySurface(surfaceId);
        }
    }
}
