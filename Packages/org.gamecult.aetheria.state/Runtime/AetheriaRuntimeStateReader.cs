using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    // Compatibility reader for file-backed tools and pure state-ref helpers; Unity shells should prefer AetheriaClient.
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

        public static AetheriaRuntimeTradeValueSettings ReadTradeValuePolicy(string stateFilePath)
        {
            return AetheriaRuntimeCatalogStore.ReadTradeValuePolicy(stateFilePath);
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

            var surface = AetheriaRuntimeCatalogStore.ReadEveSurfaces(stateFilePath)
                .FirstOrDefault(candidate => string.Equals(candidate.Surface.Id, surfaceId, StringComparison.Ordinal));
            return surface == null ? null : ResolveSurfaceStateRefs(stateFilePath, surface);
        }

        public static string ResolveEveSurfaceStateRef(string stateFilePath, string stateRef)
        {
            return AetheriaRuntimeStateRefResolver.ResolveEveSurfaceStateRef(
                stateFilePath,
                stateRef,
                OpenRuntimeCatalog);
        }

        public static bool TryResolveEveSurfaceStateRef(string stateFilePath, string stateRef, out string value)
        {
            return AetheriaRuntimeStateRefResolver.TryResolveEveSurfaceStateRef(
                stateFilePath,
                stateRef,
                OpenRuntimeCatalog,
                out value);
        }

        public static bool TryResolveDaemonStateRef(
            AetheriaRuntimeDaemonFrameDocument? frame,
            AetheriaRuntimeDaemonHealthDocument? health,
            AetheriaRuntimeDaemonCommandBoundaryDocument? commandBoundary,
            string stateRef,
            out string value)
        {
            return AetheriaRuntimeStateRefResolver.TryResolveDaemonStateRef(
                frame,
                health,
                commandBoundary,
                stateRef,
                out value);
        }

        public static bool TryResolveDaemonItemStatRef(
            AetheriaRuntimeDaemonFrameDocument? frame,
            AetheriaRuntimeCatalogSnapshot? catalog,
            string stateRef,
            out string value)
        {
            return AetheriaRuntimeStateRefResolver.TryResolveDaemonItemStatRef(
                frame,
                catalog,
                stateRef,
                out value);
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

            document = ToResolvedEveSurfaceDocument(stateFilePath, surface);
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

            document = ToResolvedEveSurfaceDocument(stateFilePath, surface);
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

            document = ToResolvedEveSurfaceDocument(stateFilePath, surface);
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

            document = ToResolvedEveSurfaceDocument(stateFilePath, surface);
            return true;
        }

        private static EveSurfaceDocument EmptySurface(string surfaceId)
        {
            return AetheriaRuntimeEveSurfaceAdapter.EmptySurface(surfaceId);
        }

        public static Func<string, string> CreateEveSurfaceStateRefResolver(string stateFilePath)
        {
            return CreateEveSurfaceCultMeshStateRefResolver(stateFilePath).AsFunc();
        }

        public static CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver(
            string stateFilePath,
            string runtimeId = AetheriaRuntimeVerseClient.DefaultRuntimeId)
        {
            return AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(
                stateFilePath,
                runtimeId,
                OpenRuntimeCatalog);
        }

        public static CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver(
            AetheriaRuntimeDaemonFrameDocument? frame,
            AetheriaRuntimeDaemonHealthDocument? health,
            AetheriaRuntimeDaemonCommandBoundaryDocument? commandBoundary,
            Func<AetheriaRuntimeCatalogSnapshot?>? catalogProvider = null,
            CultMeshRouteHint? routeHint = null)
        {
            return AetheriaRuntimeStateRefResolver.CreateEveSurfaceCultMeshStateRefResolver(
                frame,
                health,
                commandBoundary,
                catalogProvider,
                routeHint);
        }

        private static EveSurfaceDocument ToResolvedEveSurfaceDocument(
            string stateFilePath,
            AetheriaRuntimeSurfaceDocument surface)
        {
            return AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
                surface,
                CreateEveSurfaceStateRefResolver(stateFilePath));
        }

        private static EveSurfaceDocument ResolveSurfaceStateRefs(
            string stateFilePath,
            EveSurfaceDocument surface)
        {
            return AetheriaRuntimeEveSurfaceAdapter.ResolveStateRefs(
                surface,
                CreateEveSurfaceStateRefResolver(stateFilePath));
        }

    }

    public static class AetheriaRuntimeStateRefResolver
    {
        public static string ResolveEveSurfaceStateRef(
            string stateFilePath,
            string stateRef,
            Func<string, AetheriaRuntimeCatalogSnapshot> catalogProvider)
        {
            return TryResolveEveSurfaceStateRef(stateFilePath, stateRef, catalogProvider, out var value) ? value : "";
        }

        public static bool TryResolveEveSurfaceStateRef(
            string stateFilePath,
            string stateRef,
            Func<string, AetheriaRuntimeCatalogSnapshot> catalogProvider,
            out string value)
        {
            value = "";
            if (string.IsNullOrWhiteSpace(stateRef))
                return false;

            AetheriaRuntimeDaemonFrameStore.TryReadFrame(stateFilePath, out var frame);
            if (stateRef.StartsWith(AetheriaRuntimeDaemonStateRefs.Prefix + "/", StringComparison.Ordinal))
            {
                AetheriaRuntimeDaemonPublicationStore.TryReadHealth(stateFilePath, out var health);
                AetheriaRuntimeDaemonPublicationStore.TryReadCommandBoundary(stateFilePath, out var commandBoundary);
                return TryResolveDaemonStateRef(frame, health, commandBoundary, stateRef, out value);
            }

            if (stateRef.StartsWith(AetheriaRuntimeDaemonItemStatQueries.StateRefPrefix + "/", StringComparison.Ordinal))
            {
                return TryResolveDaemonItemStatRef(
                    frame,
                    catalogProvider(stateFilePath),
                    stateRef,
                    out value);
            }

            return false;
        }

        public static bool TryResolveDaemonStateRef(
            AetheriaRuntimeDaemonFrameDocument? frame,
            AetheriaRuntimeDaemonHealthDocument? health,
            AetheriaRuntimeDaemonCommandBoundaryDocument? commandBoundary,
            string stateRef,
            out string value)
        {
            value = "";
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            health ??= new AetheriaRuntimeDaemonHealthDocument();
            commandBoundary ??= new AetheriaRuntimeDaemonCommandBoundaryDocument();

            var run = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var zone = FindCurrentZone(run);
            var entity = FindCurrentEntity(run, zone);
            var target = FindTargetEntity(zone, entity);

            switch (stateRef)
            {
                case AetheriaRuntimeDaemonStateRefs.FrameDaemonId:
                    value = frame.DaemonId ?? "";
                    return true;
                case AetheriaRuntimeDaemonStateRefs.FrameVerseId:
                    value = health.VerseId ?? "";
                    return true;
                case AetheriaRuntimeDaemonStateRefs.FrameId:
                    value = frame.FrameId.ToString(CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.FrameTime:
                    value = frame.SimulationTimeSeconds.ToString("0.###", CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.FrameStatus:
                    value = health.Status ?? "";
                    return true;
                case AetheriaRuntimeDaemonStateRefs.FrameObservedCommands:
                    value = health.ObservedCommandCount.ToString(CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.FrameAppliedCommands:
                    value = health.AppliedCommandCount.ToString(CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.FrameRejectedCommands:
                    value = health.RejectedCommandCount.ToString(CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentRunId:
                    value = run.RunId ?? "";
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentZoneIndex:
                    value = run.CurrentZoneIndex.ToString(CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentEntityKey:
                    value = run.CurrentEntityKey ?? "";
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentEntityName:
                    value = string.IsNullOrWhiteSpace(entity?.Name) ? "(no current entity)" : entity!.Name;
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentEntityPosition:
                    value = FormatPosition(entity);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentTargetName:
                    value = string.IsNullOrWhiteSpace(target?.Name) ? "(none)" : target!.Name;
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentEquipmentCount:
                    value = Count(entity?.Equipment).ToString(CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentCargoBayCount:
                    value = Count(entity?.CargoContents).ToString(CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CurrentWeaponGroupCount:
                    value = Count(entity?.WeaponGroups).ToString(CultureInfo.InvariantCulture);
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CommandBoundaryId:
                    value = commandBoundary.BoundaryId ?? "";
                    return true;
                case AetheriaRuntimeDaemonStateRefs.CommandCount:
                    value = Count(commandBoundary.Commands).ToString(CultureInfo.InvariantCulture);
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryResolveDaemonItemStatRef(
            AetheriaRuntimeDaemonFrameDocument? frame,
            AetheriaRuntimeCatalogSnapshot? catalog,
            string stateRef,
            out string value)
        {
            value = "";
            if (!AetheriaRuntimeDaemonItemStatQueries.TryReadItemStatRef(
                    stateRef,
                    out var itemKey,
                    out var behaviorKind,
                    out var behaviorGroup,
                    out var fieldKey))
            {
                return false;
            }

            var item = FindDaemonItem(frame?.Run, itemKey);
            var typedItem = catalog?.FindItem(itemKey);
            var field = typedItem?.BehaviorPayloads?
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Kind, behaviorKind, StringComparison.Ordinal) &&
                    candidate.Group == behaviorGroup)
                ?.Fields?
                .FirstOrDefault(candidate => candidate.Key == fieldKey);
            if (item == null || field == null)
                return false;

            value = AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                    field.Value,
                    item,
                    item.Temperature)
                .ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }

        public static CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver(
            string stateFilePath,
            string runtimeId,
            Func<string, AetheriaRuntimeCatalogSnapshot> catalogProvider)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                return CultMeshStateRefResolver.Empty;

            AetheriaRuntimeDaemonFrameStore.TryReadFrame(stateFilePath, out var frame);
            AetheriaRuntimeDaemonPublicationStore.TryReadHealth(stateFilePath, out var health);
            AetheriaRuntimeDaemonPublicationStore.TryReadCommandBoundary(stateFilePath, out var commandBoundary);

            return CreateEveSurfaceCultMeshStateRefResolver(
                frame,
                health,
                commandBoundary,
                () => catalogProvider(stateFilePath),
                new CultMeshRouteHint(
                    CultMeshLocalityKind.InProcess,
                    string.IsNullOrWhiteSpace(runtimeId)
                        ? AetheriaRuntimeVerseClient.DefaultRuntimeId
                        : runtimeId));
        }

        public static CultMeshStateRefResolver CreateEveSurfaceCultMeshStateRefResolver(
            AetheriaRuntimeDaemonFrameDocument? frame,
            AetheriaRuntimeDaemonHealthDocument? health,
            AetheriaRuntimeDaemonCommandBoundaryDocument? commandBoundary,
            Func<AetheriaRuntimeCatalogSnapshot?>? catalogProvider = null,
            CultMeshRouteHint? routeHint = null)
        {
            AetheriaRuntimeCatalogSnapshot? catalog = null;

            var daemonRefs = CultMesh.StateRefResolver(
                "aetheria.daemon.refs",
                (stateRef, _context) => TryResolveDaemonStateRef(frame, health, commandBoundary, stateRef, out var value)
                    ? value
                    : "",
                new[]
                {
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString()),
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString()),
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString())
                },
                routeHint);

            var itemStatRefs = CultMesh.StateRefResolver(
                "aetheria.daemon.item_stats.refs",
                (stateRef, _context) =>
                {
                    if (!stateRef.StartsWith(AetheriaRuntimeDaemonItemStatQueries.StateRefPrefix + "/", StringComparison.Ordinal))
                        return "";

                    catalog ??= catalogProvider?.Invoke();
                    return TryResolveDaemonItemStatRef(frame, catalog, stateRef, out var value)
                        ? value
                        : "";
                },
                new[]
                {
                    CultMesh.ProjectionSource(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString()),
                    CultMesh.ProjectionSource("catalog:aetheria.runtime")
                },
                routeHint);

            return daemonRefs.Or(itemStatRefs);
        }

        private static AetheriaRuntimeLoadoutItemCommit? FindDaemonItem(
            AetheriaRuntimeRunCheckpointCommit? run,
            string itemKey)
        {
            if (run == null || string.IsNullOrWhiteSpace(itemKey))
                return null;

            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                var item = FindDaemonItem(entity.Equipment, itemKey)
                    ?? FindDaemonItem(entity.CargoBays, itemKey)
                    ?? FindDaemonItem(entity.DockingBays, itemKey)
                    ?? FindDaemonItem(entity.CargoContents, itemKey)
                    ?? FindDaemonItem(entity.DockingBayContents, itemKey);
                if (item != null)
                    return item;
            }

            return null;
        }

        private static AetheriaRuntimeLoadoutItemCommit? FindDaemonItem(
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots,
            string itemKey)
        {
            foreach (var slot in slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            {
                if (IsItemMatch(slot?.Item, itemKey))
                    return slot?.Item;
            }

            return null;
        }

        private static AetheriaRuntimeLoadoutItemCommit? FindDaemonItem(
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit>? cargoBays,
            string itemKey)
        {
            foreach (var cargoBay in cargoBays ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
            {
                var item = FindDaemonItem(cargoBay?.Items, itemKey);
                if (item != null)
                    return item;
            }

            return null;
        }

        private static bool IsItemMatch(AetheriaRuntimeLoadoutItemCommit? item, string itemKey)
        {
            return item != null &&
                   string.Equals(item.ItemKey ?? "", itemKey ?? "", StringComparison.Ordinal);
        }

        private static AetheriaRuntimeZoneSnapshotCommit FindCurrentZone(AetheriaRuntimeRunCheckpointCommit run)
        {
            return run.Zones.FirstOrDefault(zone => zone.ZoneIndex == run.CurrentZoneIndex)
                ?? run.Zones.FirstOrDefault()
                ?? new AetheriaRuntimeZoneSnapshotCommit();
        }

        private static AetheriaRuntimeEntitySnapshotCommit? FindCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var entityIndex = TryParseEntityIndex(run.CurrentEntityKey);
            if (entityIndex >= 0)
                return zone.Entities.FirstOrDefault(entity => entity.EntityIndex == entityIndex);

            return zone.Entities.FirstOrDefault();
        }

        private static AetheriaRuntimeEntitySnapshotCommit? FindTargetEntity(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeEntitySnapshotCommit? entity)
        {
            if (entity == null || entity.TargetEntityIndex < 0)
                return null;

            return zone.Entities.FirstOrDefault(candidate => candidate.EntityIndex == entity.TargetEntityIndex);
        }

        private static int TryParseEntityIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return -1;

            var marker = ".entity.";
            var markerIndex = key.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return -1;

            var start = markerIndex + marker.Length;
            var end = start;
            while (end < key.Length && char.IsDigit(key[end]))
                end++;

            return int.TryParse(key.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : -1;
        }

        private static string FormatPosition(AetheriaRuntimeEntitySnapshotCommit? entity)
        {
            if (entity == null)
                return "(none)";

            return string.Join(", ", new[]
            {
                entity.PositionX.ToString("0.###", CultureInfo.InvariantCulture),
                entity.PositionY.ToString("0.###", CultureInfo.InvariantCulture),
                entity.PositionZ.ToString("0.###", CultureInfo.InvariantCulture)
            });
        }

        private static int Count<T>(IReadOnlyCollection<T>? values)
        {
            return values?.Count ?? 0;
        }
    }
}
