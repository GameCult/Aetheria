using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeRtsDocuments
    {
        private const double ZoneRenderWormholeDistanceRatio = 1.0;

        public static AetheriaRuntimeRtsViewportDocument Viewport(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            var objects = ObjectsViewport(frame, viewport);
            var gravity = GravityViewport(frame, viewport);

            return new AetheriaRuntimeRtsViewportDocument
            {
                FrameId = objects.FrameId,
                PublishedAtUtc = objects.PublishedAtUtc,
                SimulationTimeSeconds = objects.SimulationTimeSeconds,
                RunId = objects.RunId,
                ZoneIndex = objects.ZoneIndex,
                ZoneName = objects.ZoneName,
                CurrentEntityKey = objects.CurrentEntityKey,
                Viewport = objects.Viewport,
                ControlledEntityIndices = objects.ControlledEntityIndices,
                Objects = objects.Objects,
                GravityInfluences = gravity.GravityInfluences,
                Bodies = gravity.Bodies
            };
        }

        public static AetheriaRuntimeObjectsViewportDocument ObjectsViewport(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            viewport ??= new AetheriaRuntimeRtsViewportBounds();

            var normalizedViewport = Normalize(viewport);
            var context = Context(frame);
            var run = context.Run;
            var zone = context.Zone;
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var controlledEntityIndices = entities
                .Where(IsPlayerControlled)
                .Select(entity => entity.EntityIndex)
                .ToArray();
            var controlled = entities
                .Where(entity => controlledEntityIndices.Contains(entity.EntityIndex))
                .ToArray();

            return new AetheriaRuntimeObjectsViewportDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = zone.ZoneIndex,
                ZoneName = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneIndex}" : zone.Name,
                CurrentEntityKey = run.CurrentEntityKey ?? "",
                Viewport = normalizedViewport,
                ControlledEntityIndices = controlledEntityIndices,
                Objects = entities
                    .Where(entity => IntersectsViewport(entity, normalizedViewport))
                    .Where(entity => IsPlayerControlled(entity) ||
                        controlled.Length == 0 ||
                        controlled.Any(observer => CanSee(observer, entity)))
                    .Select(entity => ToViewportObject(entity, context.RunId, zone.ZoneIndex))
                    .ToArray()
            };
        }

        public static AetheriaRuntimeGravityViewportDocument GravityViewport(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            viewport ??= new AetheriaRuntimeRtsViewportBounds();

            var normalizedViewport = Normalize(viewport);
            var context = Context(frame);
            var zone = context.Zone;
            var visibleBodies = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                .Where(body => GravityInfluenceIntersectsViewport(body, normalizedViewport))
                .ToArray();

            return new AetheriaRuntimeGravityViewportDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = zone.ZoneIndex,
                ZoneName = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneIndex}" : zone.Name,
                Viewport = normalizedViewport,
                GravityInfluences = visibleBodies.Select(ToGravityInfluence).ToArray(),
                Bodies = visibleBodies.Select(ToBodyView).ToArray(),
                TerrainRadius = zone.GravityTerrainRadius,
                TerrainDepth = zone.GravityTerrainDepth,
                TerrainDepthExponent = zone.GravityTerrainDepthExponent,
                TerrainWaveFrequency = zone.GravityTerrainWaveFrequency
            };
        }

        public static AetheriaRuntimeRenderSplatsViewportDocument RenderSplatsViewport(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            viewport ??= new AetheriaRuntimeRtsViewportBounds();

            var normalizedViewport = Normalize(viewport);
            var context = Context(frame);
            var zone = context.Zone;
            var layers = BuildDefaultRenderSplatLayers();
            var layerIndices = layers
                .Select((layer, index) => (layer.LayerKey, index))
                .ToDictionary(pair => pair.LayerKey, pair => pair.index, StringComparer.Ordinal);
            var splats = new RenderSplatBuilder();
            var viewportCenterX = (normalizedViewport.MinX + normalizedViewport.MaxX) * 0.5;
            var viewportCenterY = (normalizedViewport.MinY + normalizedViewport.MaxY) * 0.5;
            var viewportHalfX = Math.Max(0.0001, (normalizedViewport.MaxX - normalizedViewport.MinX) * 0.5);
            var viewportHalfY = Math.Max(0.0001, (normalizedViewport.MaxY - normalizedViewport.MinY) * 0.5);
            if (zone.GravityTerrainDepth != 0)
            {
                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.GravityHeight],
                    viewportCenterX,
                    viewportCenterY,
                    viewportHalfX,
                    viewportHalfY,
                    AetheriaRuntimeRenderSplatChannels.Gravity,
                    AetheriaRuntimeRenderSplatFalloffs.Solid,
                    -zone.GravityTerrainDepth,
                    0,
                    0,
                    1,
                    "environment.gravity_terrain",
                    sourceKind: AetheriaRuntimeRenderSplatSourceKinds.AnimatedSimplexNoise,
                    frequencyX: 3.0,
                    frequencyY: 3.0,
                    animationSpeed: zone.GravityTerrainWaveFrequency * 0.025,
                    sourceFlags: 1);
            }

            splats.Add(
                layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight],
                viewportCenterX,
                viewportCenterY,
                viewportHalfX,
                viewportHalfY,
                AetheriaRuntimeRenderSplatChannels.Tint,
                AetheriaRuntimeRenderSplatFalloffs.Solid,
                1,
                0,
                0,
                1,
                "environment.fog_surface_height",
                sourceKind: AetheriaRuntimeRenderSplatSourceKinds.AnimatedSimplexNoise,
                frequencyX: 4.0,
                frequencyY: 4.0,
                animationSpeed: 0.015,
                sourceFlags: 1);
            splats.Add(
                layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight],
                viewportCenterX,
                viewportCenterY,
                viewportHalfX,
                viewportHalfY,
                AetheriaRuntimeRenderSplatChannels.Tint,
                AetheriaRuntimeRenderSplatFalloffs.Solid,
                1,
                0,
                0,
                1,
                "environment.fog_patch_height",
                sourceKind: AetheriaRuntimeRenderSplatSourceKinds.AnimatedSimplexNoise,
                frequencyX: 9.0,
                frequencyY: 9.0,
                animationSpeed: 0.02,
                sourceFlags: 1);
            splats.Add(
                layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogPatch],
                viewportCenterX,
                viewportCenterY,
                viewportHalfX,
                viewportHalfY,
                AetheriaRuntimeRenderSplatChannels.Tint,
                AetheriaRuntimeRenderSplatFalloffs.Solid,
                1,
                0,
                0,
                1,
                "environment.fog_patch",
                sourceKind: AetheriaRuntimeRenderSplatSourceKinds.AnimatedSimplexNoise,
                frequencyX: 6.0,
                frequencyY: 6.0,
                animationSpeed: 0.01,
                sourceFlags: 1);

            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (!GravityInfluenceIntersectsViewport(body, normalizedViewport))
                    continue;

                var radius = ResolveGravityRadius(body);
                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.GravityHeight],
                    body.GravityInfluenceCenterX,
                    body.GravityInfluenceCenterZ,
                    radius,
                    radius,
                    AetheriaRuntimeRenderSplatChannels.Gravity,
                    AetheriaRuntimeRenderSplatFalloffs.InverseSmooth,
                    body.GravityWellDepth,
                    0,
                    0,
                    1,
                    body.BodyKey ?? "");

                if (body.GravityWaveRadius > 0 && body.GravityWaveDepth != 0)
                {
                    splats.Add(
                        layerIndices[AetheriaRuntimeRenderSplatLayerKeys.GravityWave],
                        body.GravityInfluenceCenterX,
                        body.GravityInfluenceCenterZ,
                        body.GravityWaveRadius,
                        body.GravityWaveRadius,
                        AetheriaRuntimeRenderSplatChannels.GravityWave,
                        AetheriaRuntimeRenderSplatFalloffs.Smooth,
                        body.GravityWaveDepth,
                        body.GravityWaveSpeed,
                        0,
                        1,
                        body.BodyKey ?? "");
                }

                if (IsBodyKind(body, "sun"))
                {
                    var tintRadius = Math.Max(radius, Math.Max(32, body.BodyRadiusMultiplier * 70) *
                        Math.Max(0.01, body.SunVisual?.LightRadiusMultiplier ?? 1.0));
                    splats.Add(
                        layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogTint],
                        body.GravityInfluenceCenterX,
                        body.GravityInfluenceCenterZ,
                        tintRadius,
                        tintRadius,
                        AetheriaRuntimeRenderSplatChannels.Tint,
                        AetheriaRuntimeRenderSplatFalloffs.Smooth,
                        body.SunVisual?.FogTintColorX ?? 0,
                        body.SunVisual?.FogTintColorY ?? 0,
                        body.SunVisual?.FogTintColorZ ?? 0,
                        1,
                        body.BodyKey ?? "");
                }
            }

            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (!IsPlayerControlled(entity))
                    continue;

                var visibility = Math.Max(180, entity.Visibility);
                if (entity.PositionX + visibility < normalizedViewport.MinX ||
                    entity.PositionX - visibility > normalizedViewport.MaxX ||
                    entity.PositionZ + visibility < normalizedViewport.MinY ||
                    entity.PositionZ - visibility > normalizedViewport.MaxY)
                {
                    continue;
                }

                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.Visibility],
                    entity.PositionX,
                    entity.PositionZ,
                    visibility,
                    visibility,
                    AetheriaRuntimeRenderSplatChannels.Visibility,
                    AetheriaRuntimeRenderSplatFalloffs.Smooth,
                    1,
                    1,
                    1,
                    1,
                    AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, zone.ZoneIndex, entity.EntityIndex));
            }

            return new AetheriaRuntimeRenderSplatsViewportDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = zone.ZoneIndex,
                ZoneName = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneIndex}" : zone.Name,
                Viewport = normalizedViewport,
                Layers = layers,
                Splats = splats.Build()
            };
        }

        private static IReadOnlyList<AetheriaRuntimeRenderSplatLayerDefinition> BuildDefaultRenderSplatLayers()
        {
            return new[]
            {
                new AetheriaRuntimeRenderSplatLayerDefinition
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.GravityHeight,
                    DisplayName = "Gravity Height",
                    Channel = AetheriaRuntimeRenderSplatChannels.Gravity,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new AetheriaRuntimeRenderSplatLayerDefinition
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.GravityWave,
                    DisplayName = "Gravity Wave",
                    Channel = AetheriaRuntimeRenderSplatChannels.GravityWave,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new AetheriaRuntimeRenderSplatLayerDefinition
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.Visibility,
                    DisplayName = "Visibility Mask",
                    Channel = AetheriaRuntimeRenderSplatChannels.Visibility,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Max,
                    GraphicsFormat = "R16_SFloat"
                },
                new AetheriaRuntimeRenderSplatLayerDefinition
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight,
                    DisplayName = "Fog Surface Height",
                    Channel = AetheriaRuntimeRenderSplatChannels.Tint,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new AetheriaRuntimeRenderSplatLayerDefinition
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight,
                    DisplayName = "Fog Patch Height",
                    Channel = AetheriaRuntimeRenderSplatChannels.Tint,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new AetheriaRuntimeRenderSplatLayerDefinition
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogPatch,
                    DisplayName = "Fog Patch",
                    Channel = AetheriaRuntimeRenderSplatChannels.Tint,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Max,
                    GraphicsFormat = "R16_SFloat"
                },
                new AetheriaRuntimeRenderSplatLayerDefinition
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogTint,
                    DisplayName = "Fog Tint",
                    Channel = AetheriaRuntimeRenderSplatChannels.Tint,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "B10G11R11_UFloatPack32"
                },
                new AetheriaRuntimeRenderSplatLayerDefinition
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.Influence,
                    DisplayName = "Influence",
                    Channel = AetheriaRuntimeRenderSplatChannels.Influence,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                }
            };
        }

        public static AetheriaRuntimeCurrentZoneDocument CurrentZone(
            AetheriaRuntimeDaemonFrameDocument frame)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var run = context.Run;
            var zone = context.Zone;

            return new AetheriaRuntimeCurrentZoneDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = zone.ZoneIndex,
                ZoneName = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneIndex}" : zone.Name,
                PositionX = zone.PositionX,
                PositionY = zone.PositionY,
                CurrentEntityKey = run.CurrentEntityKey ?? "",
                AdjacentZoneIndices = zone.AdjacentZoneIndices ?? Array.Empty<int>()
            };
        }

        public static AetheriaRuntimeCurrentEntityDocument CurrentEntity(
            AetheriaRuntimeDaemonFrameDocument frame)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var currentEntityIndex = TryParseEntityIndex(context.Run.CurrentEntityKey);
            var entity = (context.Zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(candidate => candidate.EntityIndex == currentEntityIndex);
            var entityKey = entity == null
                ? context.Run.CurrentEntityKey ?? ""
                : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, context.Zone.ZoneIndex, entity.EntityIndex);
            var inventory = entity == null
                ? Array.Empty<AetheriaRuntimeRtsInventoryItem>()
                : Inventory(entity).ToArray();

            return new AetheriaRuntimeCurrentEntityDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = context.Zone.ZoneIndex,
                EntityKey = entityKey,
                EntityIndex = entity?.EntityIndex ?? currentEntityIndex,
                Entity = entity == null ? null : ToViewportObject(entity, context.RunId, context.Zone.ZoneIndex),
                Status = entity == null
                    ? new AetheriaRuntimeRtsEntityStatus()
                    : new AetheriaRuntimeRtsEntityStatus
                    {
                        Hull = Stat(entity, "hull"),
                        Shield = Stat(entity, "shield"),
                        Heat = Stat(entity, "heat")
                    },
                Inventory = inventory,
                Equipment = inventory.Where(item => string.Equals(item.Source, "equipment", StringComparison.Ordinal)).ToArray(),
                Cargo = inventory.Where(item => string.Equals(item.Source, "cargo", StringComparison.Ordinal)).ToArray(),
                ShutdownPerformance = entity?.ShutdownPerformance ?? 0,
                Hud = CurrentEntityHudStatus(entity)
            };
        }

        public static AetheriaRuntimeCurrentDockingDocument CurrentDocking(
            AetheriaRuntimeDaemonFrameDocument frame)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var currentEntityIndex = TryParseEntityIndex(context.Run.CurrentEntityKey);
            var parent = FindDockParent(context.Zone, currentEntityIndex, out var dockingBayIndex);
            var currentEntityKey = currentEntityIndex < 0
                ? context.Run.CurrentEntityKey ?? ""
                : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, context.Zone.ZoneIndex, currentEntityIndex);
            var parentKey = parent == null
                ? ""
                : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, context.Zone.ZoneIndex, parent.EntityIndex);

            return new AetheriaRuntimeCurrentDockingDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = context.Zone.ZoneIndex,
                CurrentEntityKey = currentEntityKey,
                CurrentEntityIndex = currentEntityIndex,
                IsDocked = parent != null && dockingBayIndex >= 0,
                DockParentEntityKey = parentKey,
                DockParentEntityIndex = parent?.EntityIndex ?? -1,
                DockingBayIndex = dockingBayIndex,
                DockParent = parent == null ? null : ToViewportObject(parent, context.RunId, context.Zone.ZoneIndex)
            };
        }

        public static AetheriaRuntimeZoneContactsDocument ZoneContacts(
            AetheriaRuntimeDaemonFrameDocument frame)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var entities = context.Zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var entityMap = entities
                .Where(entity => entity != null && entity.EntityIndex >= 0)
                .ToDictionary(entity => entity.EntityIndex, entity => entity);

            return new AetheriaRuntimeZoneContactsDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = context.Zone.ZoneIndex,
                CurrentEntityKey = context.Run.CurrentEntityKey ?? "",
                Targets = entities
                    .Where(entity => entity != null &&
                                     entity.TargetEntityIndex >= 0 &&
                                     entityMap.ContainsKey(entity.TargetEntityIndex))
                    .Select(entity => ProjectZoneTargetRow(entity, entityMap[entity.TargetEntityIndex]))
                    .ToArray(),
                Contacts = entities
                    .Where(entity => entity != null)
                    .SelectMany(entity => (entity.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                        .Where(contact => contact != null &&
                                          contact.TargetEntityIndex >= 0 &&
                                          entityMap.ContainsKey(contact.TargetEntityIndex))
                        .Select(contact => ProjectZoneContactRow(entity, entityMap[contact.TargetEntityIndex], contact)))
                    .ToArray()
            };
        }

        public static AetheriaRuntimeStationRefitDocument StationRefit(
            AetheriaRuntimeDaemonFrameDocument frame,
            IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot>? loadoutTemplates = null,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var currentEntityIndex = TryParseEntityIndex(context.Run.CurrentEntityKey);
            var parent = FindDockParent(context.Zone, currentEntityIndex, out var dockingBayIndex);
            var currentEntityKey = currentEntityIndex < 0
                ? context.Run.CurrentEntityKey ?? ""
                : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, context.Zone.ZoneIndex, currentEntityIndex);
            var parentKey = parent == null
                ? ""
                : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, context.Zone.ZoneIndex, parent.EntityIndex);
            var availableEntities = parent == null
                ? Array.Empty<AetheriaRuntimeStationRefitEntityOption>()
                : StationRefitEntities(context, parent, currentEntityIndex);
            var stationStock = parent == null
                ? Array.Empty<AetheriaRuntimeStationStockItem>()
                : ProjectStationStock(parent);
            var dockingBays = parent == null
                ? Array.Empty<AetheriaRuntimeStationDockingBayRow>()
                : ProjectStationDockingBays(context, parent, currentEntityIndex);
            var cargoTargets = parent == null
                ? Array.Empty<AetheriaRuntimeStationCargoTargetRow>()
                : ProjectStationCargoTargets(parentKey, dockingBayIndex, dockingBays, availableEntities);
            stationStock = ProjectStationStockTradeFacts(
                stationStock,
                availableEntities,
                cargoTargets,
                context.Run.Credits,
                catalog);

            return new AetheriaRuntimeStationRefitDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = context.Zone.ZoneIndex,
                CurrentEntityKey = currentEntityKey,
                CurrentEntityIndex = currentEntityIndex,
                IsDocked = parent != null && dockingBayIndex >= 0,
                DockParentEntityKey = parentKey,
                DockParentEntityIndex = parent?.EntityIndex ?? -1,
                DockingBayIndex = dockingBayIndex,
                DockParent = parent == null ? null : ToViewportObject(parent, context.RunId, context.Zone.ZoneIndex),
                AvailableEntities = availableEntities,
                Credits = context.Run.Credits,
                StationStock = stationStock,
                DockingBays = dockingBays,
                LoadoutRestoreOptions = parent == null
                    ? Array.Empty<AetheriaRuntimeStationLoadoutRestoreOption>()
                    : ProjectLoadoutRestoreOptions(parentKey, context.Run.Credits, loadoutTemplates, catalog),
                CargoTargets = cargoTargets
            };
        }

        private static AetheriaRuntimeZoneTargetRow ProjectZoneTargetRow(
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEntitySnapshotCommit target)
        {
            var deltaX = target.PositionX - observer.PositionX;
            var deltaY = target.PositionY - observer.PositionY;
            var deltaZ = target.PositionZ - observer.PositionZ;
            return new AetheriaRuntimeZoneTargetRow
            {
                EntityIndex = observer.EntityIndex,
                TargetEntityIndex = target.EntityIndex,
                TargetPositionX = target.PositionX,
                TargetPositionY = target.PositionY,
                TargetPositionZ = target.PositionZ,
                DeltaX = deltaX,
                DeltaY = deltaY,
                DeltaZ = deltaZ,
                Distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ)
            };
        }

        private static AetheriaRuntimeZoneContactRow ProjectZoneContactRow(
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEntitySnapshotCommit target,
            AetheriaRuntimeEntityContactCommit contact)
        {
            var deltaX = target.PositionX - observer.PositionX;
            var deltaY = target.PositionY - observer.PositionY;
            var deltaZ = target.PositionZ - observer.PositionZ;
            return new AetheriaRuntimeZoneContactRow
            {
                ObserverEntityIndex = observer.EntityIndex,
                TargetEntityIndex = target.EntityIndex,
                InfoGathered = contact.InfoGathered,
                Hostile = contact.Hostile,
                Visible = contact.Visible,
                TargetPositionX = target.PositionX,
                TargetPositionY = target.PositionY,
                TargetPositionZ = target.PositionZ,
                DeltaX = deltaX,
                DeltaY = deltaY,
                DeltaZ = deltaZ,
                Distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ)
            };
        }

        public static AetheriaRuntimeSectorMapDocument SectorMap(
            AetheriaRuntimeDaemonFrameDocument frame)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var run = context.Run;
            var zones = run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();
            var discovered = new HashSet<int>(run.DiscoveredZoneIndices ?? Array.Empty<int>());
            if (run.CurrentZoneIndex >= 0)
                discovered.Add(run.CurrentZoneIndex);

            return new AetheriaRuntimeSectorMapDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                CurrentZoneIndex = run.CurrentZoneIndex,
                EntranceZoneIndex = run.EntranceZoneIndex,
                ExitZoneIndex = run.ExitZoneIndex,
                IsTutorial = run.IsTutorial,
                GenerationSeed = run.GenerationSeed,
                FactionRelationships = (run.FactionRelationships ?? Array.Empty<AetheriaRuntimeFactionRelationshipCommit>())
                    .Where(relationship => relationship != null)
                    .ToArray(),
                DiscoveredZoneIndices = discovered.OrderBy(index => index).ToArray(),
                Zones = zones
                    .OrderBy(zone => zone.ZoneIndex)
                    .Select(zone => ToSectorMapZone(zone, run, discovered))
                    .ToArray(),
                Links = SectorMapLinks(zones, discovered)
            };
        }

        public static AetheriaRuntimeZoneDetailsDocument ZoneDetails(
            AetheriaRuntimeDaemonFrameDocument frame,
            int zoneIndex)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var zone = (context.Run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);

            if (zone == null)
            {
                return new AetheriaRuntimeZoneDetailsDocument
                {
                    FrameId = frame.FrameId,
                    PublishedAtUtc = frame.PublishedAtUtc ?? "",
                    SimulationTimeSeconds = frame.SimulationTimeSeconds,
                    RunId = context.RunId,
                    ZoneIndex = zoneIndex,
                    ZoneName = zoneIndex < 0 ? "" : $"Zone {zoneIndex}",
                    HasContents = false
                };
            }

            return new AetheriaRuntimeZoneDetailsDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = zone.ZoneIndex,
                ZoneName = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneIndex}" : zone.Name,
                Mass = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                    .Where(body => body != null)
                    .Sum(body => body.Mass),
                Radius = Math.Max(0, zone.GravityTerrainRadius),
                BodyKinds = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                    .Where(body => body != null && !string.IsNullOrWhiteSpace(body.Kind))
                    .Select(body => body.Kind)
                    .ToArray(),
                EntityHullItemKeys = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity != null && !string.IsNullOrWhiteSpace(entity.HullItemKey))
                    .Select(entity => entity.HullItemKey)
                    .ToArray(),
                HasContents = true
            };
        }

        public static AetheriaRuntimeZoneRenderDocument ZoneRender(
            AetheriaRuntimeDaemonFrameDocument frame)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var run = context.Run;
            var zone = context.Zone;
            var zoneRenderRadius = AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(zone, 2000);

            return new AetheriaRuntimeZoneRenderDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = zone.ZoneIndex,
                ZoneName = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneIndex}" : zone.Name,
                CurrentEntityKey = run.CurrentEntityKey ?? "",
                ZoneRenderRadius = zoneRenderRadius,
                Credits = run.Credits,
                AdjacentZones = ZoneRenderAdjacentZones(run, zone),
                WormholeExits = ZoneRenderWormholeExits(run, zone, zoneRenderRadius),
                BodyPoses = ZoneRenderBodyPoses(zone),
                AsteroidBeltPoses = ZoneRenderAsteroidBeltPoses(zone, frame.SimulationTimeSeconds),
                DroppedPickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                    .Where(pickup => pickup != null)
                    .OrderBy(pickup => pickup.PickupIndex)
                    .ToArray(),
                EntitySnapshots = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                    .Where(entity => entity != null)
                    .OrderBy(entity => entity.EntityIndex)
                    .ToArray(),
                Orbits = (zone.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>())
                    .Where(orbit => orbit != null)
                    .ToArray(),
                Bodies = (zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
                    .Where(body => body != null)
                    .ToArray()
            };
        }

        public static AetheriaRuntimeSelectedObjectDocument SelectedObject(
            AetheriaRuntimeDaemonFrameDocument frame,
            int entityIndex)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var entity = (context.Zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(candidate => candidate.EntityIndex == entityIndex);

            return new AetheriaRuntimeSelectedObjectDocument
            {
                FrameId = frame.FrameId,
                RunId = context.RunId,
                ZoneIndex = context.Zone.ZoneIndex,
                EntityIndex = entityIndex,
                Selected = entity == null ? null : ToViewportObject(entity, context.RunId, context.Zone.ZoneIndex)
            };
        }

        public static AetheriaRuntimeInventoryDocument Inventory(
            AetheriaRuntimeDaemonFrameDocument frame,
            int entityIndex)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            var context = Context(frame);
            var entity = (context.Zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(candidate => candidate.EntityIndex == entityIndex);
            var items = entity == null
                ? Array.Empty<AetheriaRuntimeRtsInventoryItem>()
                : Inventory(entity).ToArray();

            return new AetheriaRuntimeInventoryDocument
            {
                FrameId = frame.FrameId,
                RunId = context.RunId,
                ZoneIndex = context.Zone.ZoneIndex,
                EntityIndex = entityIndex,
                EntityKey = entity == null ? "" : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, context.Zone.ZoneIndex, entityIndex),
                Items = items,
                Equipment = items.Where(item => string.Equals(item.Source, "equipment", StringComparison.Ordinal)).ToArray(),
                Cargo = items.Where(item => string.Equals(item.Source, "cargo", StringComparison.Ordinal)).ToArray()
            };
        }

        public static AetheriaRuntimeRtsViewportBounds Normalize(AetheriaRuntimeRtsViewportBounds viewport)
        {
            viewport ??= new AetheriaRuntimeRtsViewportBounds();
            return new AetheriaRuntimeRtsViewportBounds
            {
                MinX = Math.Min(viewport.MinX, viewport.MaxX),
                MinY = Math.Min(viewport.MinY, viewport.MaxY),
                MaxX = Math.Max(viewport.MinX, viewport.MaxX),
                MaxY = Math.Max(viewport.MinY, viewport.MaxY)
            };
        }

        public static bool IntersectsViewport(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            return entity.PositionX >= viewport.MinX &&
                entity.PositionX <= viewport.MaxX &&
                entity.PositionZ >= viewport.MinY &&
                entity.PositionZ <= viewport.MaxY;
        }

        public static bool GravityInfluenceIntersectsViewport(
            AetheriaRuntimeBodySnapshotCommit body,
            AetheriaRuntimeRtsViewportBounds viewport)
        {
            var radius = ResolveGravityRadius(body);
            return body.GravityInfluenceCenterX + radius >= viewport.MinX &&
                body.GravityInfluenceCenterX - radius <= viewport.MaxX &&
                body.GravityInfluenceCenterZ + radius >= viewport.MinY &&
                body.GravityInfluenceCenterZ - radius <= viewport.MaxY;
        }

        public static double ResolveGravityRadius(AetheriaRuntimeBodySnapshotCommit body)
        {
            if (double.IsFinite(body.GravityInfluenceRadius) && body.GravityInfluenceRadius > 0)
                return body.GravityInfluenceRadius;
            return Math.Max(32, body.BodyRadiusMultiplier * 70);
        }

        private static AetheriaRuntimeRtsViewportObject ToViewportObject(
            AetheriaRuntimeEntitySnapshotCommit entity,
            string runId,
            int zoneIndex)
        {
            var obj = new AetheriaRuntimeRtsViewportObject
            {
                EntityIndex = entity.EntityIndex,
                EntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(runId, zoneIndex, entity.EntityIndex),
                DisplayName = entity.Name ?? "",
                Kind = entity.Kind ?? "",
                FactionKey = entity.FactionKey ?? "",
                X = entity.PositionX,
                Y = entity.PositionZ,
                Z = entity.PositionY,
                DirectionX = entity.DirectionX,
                DirectionY = entity.DirectionY,
                VelocityX = entity.VelocityX,
                VelocityY = entity.VelocityY,
                Controlled = IsPlayerControlled(entity),
                TargetEntityIndex = entity.TargetEntityIndex,
                IsActive = entity.IsActive,
                Visibility = entity.Visibility,
                Status = new AetheriaRuntimeRtsEntityStatus
                {
                    Hull = Stat(entity, "hull"),
                    Shield = Stat(entity, "shield"),
                    Heat = Stat(entity, "heat")
                },
                Inventory = Inventory(entity)
            };
            obj.IconAsset = AetheriaRuntimeAssets.ResolveEntityIcon(obj);
            return obj;
        }

        private static AetheriaRuntimeRtsBodyView ToBodyView(AetheriaRuntimeBodySnapshotCommit body)
        {
            return new AetheriaRuntimeRtsBodyView
            {
                BodyKey = body.BodyKey ?? "",
                OrbitKey = body.OrbitKey ?? "",
                Name = body.Name ?? "",
                Kind = body.Kind ?? "",
                X = body.GravityInfluenceCenterX,
                Y = body.GravityInfluenceCenterZ,
                Radius = Math.Max(32, body.BodyRadiusMultiplier * 70),
                IsAsteroidBelt = (body.Kind ?? "").IndexOf("asteroid", StringComparison.OrdinalIgnoreCase) >= 0,
                Body = body
            };
        }

        private static AetheriaRuntimeRtsGravityInfluence ToGravityInfluence(AetheriaRuntimeBodySnapshotCommit body)
        {
            return new AetheriaRuntimeRtsGravityInfluence
            {
                BodyKey = body.BodyKey ?? "",
                OrbitKey = body.OrbitKey ?? "",
                Kind = body.Kind ?? "",
                X = body.GravityInfluenceCenterX,
                Y = body.GravityInfluenceCenterZ,
                Radius = ResolveGravityRadius(body),
                GravityDepth = body.GravityWellDepth,
                GravityDepthExponent = body.GravityDepthExponent,
                WaveRadius = body.GravityWaveRadius,
                WaveDepth = body.GravityWaveDepth,
                WaveSpeed = body.GravityWaveSpeed
            };
        }

        private static AetheriaRuntimeSectorMapZone ToSectorMapZone(
            AetheriaRuntimeZoneSnapshotCommit zone,
            AetheriaRuntimeRunCheckpointCommit run,
            HashSet<int> discovered)
        {
            return new AetheriaRuntimeSectorMapZone
            {
                ZoneIndex = zone.ZoneIndex,
                Name = zone.Name ?? "",
                X = zone.PositionX,
                Y = zone.PositionY,
                OwnerFactionIndex = zone.OwnerFactionIndex,
                FactionIndices = zone.FactionIndices ?? Array.Empty<int>(),
                AdjacentZoneIndices = zone.AdjacentZoneIndices ?? Array.Empty<int>(),
                Discovered = discovered.Contains(zone.ZoneIndex),
                Current = zone.ZoneIndex == run.CurrentZoneIndex,
                Entrance = zone.ZoneIndex == run.EntranceZoneIndex,
                Exit = zone.ZoneIndex == run.ExitZoneIndex
            };
        }

        private static IReadOnlyList<AetheriaRuntimeSectorMapLink> SectorMapLinks(
            IReadOnlyList<AetheriaRuntimeZoneSnapshotCommit> zones,
            HashSet<int> discovered)
        {
            var links = new List<AetheriaRuntimeSectorMapLink>();
            foreach (var zone in zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            {
                foreach (var adjacentIndex in zone.AdjacentZoneIndices ?? Array.Empty<int>())
                {
                    if (zone.ZoneIndex < 0 || adjacentIndex < 0 || zone.ZoneIndex >= adjacentIndex)
                        continue;

                    links.Add(new AetheriaRuntimeSectorMapLink
                    {
                        FromZoneIndex = zone.ZoneIndex,
                        ToZoneIndex = adjacentIndex,
                        Discovered = discovered.Contains(zone.ZoneIndex) &&
                                     discovered.Contains(adjacentIndex)
                    });
                }
            }

            return links.ToArray();
        }

        private static AetheriaRuntimeZoneRenderAdjacentZone[] ZoneRenderAdjacentZones(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            var adjacent = new HashSet<int>(zone.AdjacentZoneIndices ?? Array.Empty<int>());
            if (adjacent.Count == 0)
                return Array.Empty<AetheriaRuntimeZoneRenderAdjacentZone>();

            return (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(candidate => candidate != null && adjacent.Contains(candidate.ZoneIndex))
                .OrderBy(candidate => candidate.ZoneIndex)
                .Select(candidate => new AetheriaRuntimeZoneRenderAdjacentZone
                {
                    ZoneIndex = candidate.ZoneIndex,
                    X = candidate.PositionX,
                    Y = candidate.PositionY
                })
                .ToArray();
        }

        private static AetheriaRuntimeZoneRenderWormholeExit[] ZoneRenderWormholeExits(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeZoneSnapshotCommit zone,
            double zoneRenderRadius)
        {
            return AetheriaRuntimeDaemonRenderQueries
                .QueryWormholeExits(run, zone, zoneRenderRadius, ZoneRenderWormholeDistanceRatio)
                .Select(exit => new AetheriaRuntimeZoneRenderWormholeExit
                {
                    TargetZoneIndex = exit.TargetZoneIndex,
                    DirectionX = exit.DirectionX,
                    DirectionZ = exit.DirectionZ,
                    PositionX = exit.PositionX,
                    PositionZ = exit.PositionZ
                })
                .ToArray();
        }

        private static AetheriaRuntimeZoneRenderBodyPose[] ZoneRenderBodyPoses(
            AetheriaRuntimeZoneSnapshotCommit zone)
        {
            return AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
                .Select(pose => new AetheriaRuntimeZoneRenderBodyPose
                {
                    BodyKey = pose.BodyKey,
                    OrbitKey = pose.OrbitKey,
                    ParentOrbitKey = pose.ParentOrbitKey,
                    Kind = pose.Kind,
                    CenterX = pose.CenterX,
                    CenterZ = pose.CenterZ,
                    ParentCenterX = pose.ParentCenterX,
                    ParentCenterZ = pose.ParentCenterZ,
                    GravityWaveSpeed = pose.GravityWaveSpeed
                })
                .ToArray();
        }

        private static AetheriaRuntimeZoneRenderAsteroidBeltPose[] ZoneRenderAsteroidBeltPoses(
            AetheriaRuntimeZoneSnapshotCommit zone,
            double simulationTimeSeconds)
        {
            return AetheriaRuntimeDaemonRenderQueries.QueryAsteroidBeltPoses(zone)
                .Select(pose => new AetheriaRuntimeZoneRenderAsteroidBeltPose
                {
                    BodyKey = pose.BodyKey,
                    OrbitKey = pose.OrbitKey,
                    CenterX = pose.CenterX,
                    CenterZ = pose.CenterZ,
                    Radius = pose.Radius,
                    AsteroidCount = pose.AsteroidCount,
                    InstancePoses = ZoneRenderAsteroidInstancePoses(
                        zone,
                        pose.BodyKey,
                        simulationTimeSeconds)
                })
                .ToArray();
        }

        private static AetheriaRuntimeZoneRenderAsteroidInstancePose[] ZoneRenderAsteroidInstancePoses(
            AetheriaRuntimeZoneSnapshotCommit zone,
            string bodyKey,
            double simulationTimeSeconds)
        {
            return AetheriaRuntimeDaemonRenderQueries
                .QueryAsteroidInstancePoses(zone, bodyKey, simulationTimeSeconds)
                .Select(pose => new AetheriaRuntimeZoneRenderAsteroidInstancePose
                {
                    BodyKey = pose.BodyKey,
                    AsteroidIndex = pose.AsteroidIndex,
                    PositionX = pose.PositionX,
                    PositionZ = pose.PositionZ,
                    Rotation = pose.Rotation,
                    Size = pose.Size
                })
                .ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeRtsInventoryItem> Inventory(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            var items = new List<AetheriaRuntimeRtsInventoryItem>();
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var equipmentIndex = 0; equipmentIndex < equipment.Count; equipmentIndex++)
                AddSlot(items, "equipment", equipmentIndex, equipment[equipmentIndex]);

            var cargo = entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            for (var cargoBayIndex = 0; cargoBayIndex < cargo.Count; cargoBayIndex++)
            {
                var bay = cargo[cargoBayIndex];
                foreach (var slot in bay.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                    AddSlot(items, "cargo", cargoBayIndex, slot);
            }

            return items.Where(item => !string.IsNullOrWhiteSpace(item.ItemKey)).ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeStationRefitEntityOption> StationRefitEntities(
            DocumentContext context,
            AetheriaRuntimeEntitySnapshotCommit dockParent,
            int currentEntityIndex)
        {
            var entities = context.Zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var assignments = dockParent.DockingBayAssignments ?? Array.Empty<int>();
            var options = new List<AetheriaRuntimeStationRefitEntityOption>();
            for (var dockingBayIndex = 0; dockingBayIndex < assignments.Count; dockingBayIndex++)
            {
                var entityIndex = assignments[dockingBayIndex];
                if (entityIndex < 0)
                    continue;

                var entity = entities.FirstOrDefault(candidate => candidate.EntityIndex == entityIndex);
                if (entity == null)
                    continue;

                options.Add(new AetheriaRuntimeStationRefitEntityOption
                {
                    EntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, context.Zone.ZoneIndex, entity.EntityIndex),
                    EntityIndex = entity.EntityIndex,
                    DisplayName = entity.Name ?? "",
                    Kind = entity.Kind ?? "",
                    IsCurrentEntity = entity.EntityIndex == currentEntityIndex,
                    IsPlayerShip =
                        IsPlayerControlled(entity) &&
                        string.Equals(entity.Kind, "Ship", StringComparison.OrdinalIgnoreCase),
                    CargoBayCount = Math.Max(
                        entity.CargoBays?.Count ?? 0,
                        entity.CargoContents?.Count ?? 0),
                    DockingBayIndex = dockingBayIndex,
                    HullItemKey = entity.HullItemKey ?? "",
                    CargoItems = ProjectStationStock(entity)
                });
            }

            return options.ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeStationStockItem> ProjectStationStock(
            AetheriaRuntimeEntitySnapshotCommit dockParent)
        {
            var stock = new List<AetheriaRuntimeStationStockItem>();
            var cargo = dockParent.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            for (var cargoBayIndex = 0; cargoBayIndex < cargo.Count; cargoBayIndex++)
            {
                var bay = cargo[cargoBayIndex];
                if (bay == null)
                    continue;

                foreach (var slot in bay.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                {
                    if (slot == null)
                        continue;

                    var item = slot.Item;
                    if (item == null || string.IsNullOrWhiteSpace(item.ItemKey))
                        continue;

                    stock.Add(new AetheriaRuntimeStationStockItem
                    {
                        ItemKey = item.ItemKey ?? "",
                        Quantity = item.Quantity,
                        Quality = item.Quality,
                        Durability = item.Durability,
                        CargoBayIndex = cargoBayIndex,
                        X = slot.X,
                        Y = slot.Y
                    });
                }
            }

            return stock.ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeStationDockingBayRow> ProjectStationDockingBays(
            DocumentContext context,
            AetheriaRuntimeEntitySnapshotCommit dockParent,
            int currentEntityIndex)
        {
            var entities = context.Zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var bays = dockParent.DockingBays ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            var assignments = dockParent.DockingBayAssignments ?? Array.Empty<int>();
            var contents = dockParent.DockingBayContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            var rows = new List<AetheriaRuntimeStationDockingBayRow>();
            for (var dockingBayIndex = 0; dockingBayIndex < bays.Count; dockingBayIndex++)
            {
                var bay = bays[dockingBayIndex];
                var assignedEntityIndex = dockingBayIndex < assignments.Count
                    ? assignments[dockingBayIndex]
                    : -1;
                var assignedEntity = assignedEntityIndex < 0
                    ? null
                    : entities.FirstOrDefault(candidate => candidate.EntityIndex == assignedEntityIndex);

                rows.Add(new AetheriaRuntimeStationDockingBayRow
                {
                    DockingBayIndex = dockingBayIndex,
                    ItemKey = bay?.Item?.ItemKey ?? "",
                    X = bay?.X ?? -1,
                    Y = bay?.Y ?? -1,
                    OccupiedEntityIndex = assignedEntity?.EntityIndex ?? assignedEntityIndex,
                    OccupiedEntityKey = assignedEntity == null
                        ? ""
                        : AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(context.RunId, context.Zone.ZoneIndex, assignedEntity.EntityIndex),
                    OccupiedEntityName = assignedEntity?.Name ?? "",
                    OccupiedHullItemKey = assignedEntity?.HullItemKey ?? "",
                    OccupiedByCurrentEntity = assignedEntityIndex >= 0 && assignedEntityIndex == currentEntityIndex,
                    CargoItems = dockingBayIndex < contents.Count
                        ? ProjectStationStock(contents[dockingBayIndex], dockingBayIndex)
                        : Array.Empty<AetheriaRuntimeStationStockItem>()
                });
            }

            return rows.ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeStationStockItem> ProjectStationStock(
            AetheriaRuntimeCargoBayLoadoutCommit cargoBay,
            int cargoBayIndex)
        {
            if (cargoBay == null)
                return Array.Empty<AetheriaRuntimeStationStockItem>();

            return (cargoBay.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Where(slot => slot?.Item != null && !string.IsNullOrWhiteSpace(slot.Item.ItemKey))
                .Select(slot => new AetheriaRuntimeStationStockItem
                {
                    ItemKey = slot.Item.ItemKey ?? "",
                    Quantity = slot.Item.Quantity,
                    Quality = slot.Item.Quality,
                    Durability = slot.Item.Durability,
                    CargoBayIndex = cargoBayIndex,
                    X = slot.X,
                    Y = slot.Y
                })
                .ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeStationLoadoutRestoreOption> ProjectLoadoutRestoreOptions(
            string targetEntityKey,
            int credits,
            IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot>? loadoutTemplates,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var templates = loadoutTemplates ?? Array.Empty<AetheriaRuntimeLoadoutTemplateSnapshot>();
            if (string.IsNullOrWhiteSpace(targetEntityKey) || templates.Count == 0)
                return Array.Empty<AetheriaRuntimeStationLoadoutRestoreOption>();

            var options = new List<AetheriaRuntimeStationLoadoutRestoreOption>();
            for (var templateIndex = 0; templateIndex < templates.Count; templateIndex++)
            {
                var template = templates[templateIndex];
                var canPrice = AetheriaRuntimeDaemonTradeItemQueries.TryPriceLoadoutTemplate(
                    template,
                    catalog,
                    catalog?.TradeValueSettings,
                    out var price);
                var canRestore = canPrice &&
                                 price >= 0 &&
                                 credits >= price &&
                                 !string.IsNullOrWhiteSpace(template?.Name);
                options.Add(new AetheriaRuntimeStationLoadoutRestoreOption
                {
                    TemplateIndex = templateIndex,
                    TemplateName = template?.Name ?? "",
                    TargetEntityKey = targetEntityKey,
                    Price = canPrice ? price : 0,
                    CanRestore = canRestore
                });
            }

            return options.ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeStationStockItem> ProjectStationStockTradeFacts(
            IReadOnlyList<AetheriaRuntimeStationStockItem> stationStock,
            IReadOnlyList<AetheriaRuntimeStationRefitEntityOption> availableEntities,
            IReadOnlyList<AetheriaRuntimeStationCargoTargetRow> cargoTargets,
            int credits,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            return (stationStock ?? Array.Empty<AetheriaRuntimeStationStockItem>())
                .Select(stock =>
                {
                    var itemKey = stock?.ItemKey ?? "";
                    var price = ProjectStationStockPrice(stock, catalog);
                    return new AetheriaRuntimeStationStockItem
                    {
                        ItemKey = itemKey,
                        Quantity = stock?.Quantity ?? 0,
                        Quality = stock?.Quality ?? 1,
                        Durability = stock?.Durability ?? 1,
                        CargoBayIndex = stock?.CargoBayIndex ?? -1,
                        X = stock?.X ?? -1,
                        Y = stock?.Y ?? -1,
                        Price = price,
                        CanAfford = price >= 0 && credits >= price,
                        OwnedQuantity = CountStationOwnedQuantity(itemKey, availableEntities, cargoTargets, catalog)
                    };
                })
                .ToArray();
        }

        private static int ProjectStationStockPrice(
            AetheriaRuntimeStationStockItem? stock,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            var itemKey = stock?.ItemKey ?? "";
            var typedItem = catalog?.FindItem(itemKey);
            if (typedItem == null)
                return 0;

            return AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
                    typedItem,
                    AetheriaRuntimeDaemonTradeItemQueries.CraftedItemCommit(
                        itemKey,
                        stock?.Quality ?? 1,
                        stock?.Durability ?? 1),
                    catalog?.TradeValueSettings)
                .Price;
        }

        private static int CountStationOwnedQuantity(
            string itemKey,
            IReadOnlyList<AetheriaRuntimeStationRefitEntityOption> availableEntities,
            IReadOnlyList<AetheriaRuntimeStationCargoTargetRow> cargoTargets,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (string.IsNullOrWhiteSpace(itemKey))
                return 0;

            var typedItem = catalog?.FindItem(itemKey);
            if (!string.IsNullOrWhiteSpace(typedItem?.HullType))
            {
                return (availableEntities ?? Array.Empty<AetheriaRuntimeStationRefitEntityOption>())
                    .Count(entity =>
                        entity?.IsPlayerShip == true &&
                        string.Equals(entity.HullItemKey, itemKey, StringComparison.Ordinal));
            }

            var stackable = typedItem?.Stackable == true;
            var matchingCargo = (cargoTargets ?? Array.Empty<AetheriaRuntimeStationCargoTargetRow>())
                .SelectMany(target => target?.CargoItems ?? Array.Empty<AetheriaRuntimeStationStockItem>())
                .Where(item => string.Equals(item.ItemKey, itemKey, StringComparison.Ordinal));
            return stackable
                ? matchingCargo.Sum(item => Math.Max(item.Quantity, 0))
                : matchingCargo.Count();
        }

        private static IReadOnlyList<AetheriaRuntimeStationCargoTargetRow> ProjectStationCargoTargets(
            string dockParentEntityKey,
            int currentDockingBayIndex,
            IReadOnlyList<AetheriaRuntimeStationDockingBayRow> dockingBays,
            IReadOnlyList<AetheriaRuntimeStationRefitEntityOption> availableEntities)
        {
            var targets = new List<AetheriaRuntimeStationCargoTargetRow>();
            var targetIndex = 0;
            var currentDockingBay = (dockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>())
                .FirstOrDefault(row => row != null && row.DockingBayIndex == currentDockingBayIndex);
            if (!string.IsNullOrWhiteSpace(dockParentEntityKey) && currentDockingBayIndex >= 0)
            {
                targets.Add(new AetheriaRuntimeStationCargoTargetRow
                {
                    TargetIndex = targetIndex++,
                    Kind = AetheriaRuntimeTradeCargoTargetKind.DockingBay,
                    Label = "Docking Bay",
                    EntityKey = dockParentEntityKey,
                    BayIndex = currentDockingBayIndex,
                    IsCurrent = true,
                    CargoItems = currentDockingBay?.CargoItems ?? Array.Empty<AetheriaRuntimeStationStockItem>()
                });
            }

            foreach (var entity in availableEntities ?? Array.Empty<AetheriaRuntimeStationRefitEntityOption>())
            {
                if (entity?.IsPlayerShip != true ||
                    string.IsNullOrWhiteSpace(entity.EntityKey) ||
                    entity.CargoBayCount <= 0)
                {
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(entity.DisplayName)
                    ? $"Ship {entity.EntityIndex}"
                    : entity.DisplayName;
                for (var bayIndex = 0; bayIndex < entity.CargoBayCount; bayIndex++)
                {
                    targets.Add(new AetheriaRuntimeStationCargoTargetRow
                    {
                        TargetIndex = targetIndex++,
                        Kind = AetheriaRuntimeTradeCargoTargetKind.ShipBay,
                        Label = $"{displayName} Bay {bayIndex + 1}",
                        EntityKey = entity.EntityKey,
                        BayIndex = bayIndex,
                        IsPlayerShip = true,
                        HullItemKey = entity.HullItemKey ?? "",
                        CargoItems = (entity.CargoItems ?? Array.Empty<AetheriaRuntimeStationStockItem>())
                            .Where(item => item.CargoBayIndex == bayIndex)
                            .ToArray()
                    });
                }
            }

            return targets.ToArray();
        }

        private static void AddSlot(
            List<AetheriaRuntimeRtsInventoryItem> items,
            string source,
            int sourceIndex,
            AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            var item = slot.Item ?? new AetheriaRuntimeLoadoutItemCommit();
            items.Add(new AetheriaRuntimeRtsInventoryItem
            {
                Source = source,
                ItemKey = item.ItemKey ?? "",
                Quantity = item.Quantity,
                Quality = item.Quality,
                Durability = item.Durability,
                Enabled = item.Enabled,
                SourceIndex = sourceIndex,
                X = slot.X,
                Y = slot.Y,
                IconAsset = AetheriaRuntimeAssetRef.FromKey(
                    $"item.{item.ItemKey ?? ""}.icon",
                    AetheriaRuntimeAssetKinds.Texture,
                    $"item.{item.ItemKey ?? ""}.icon")
            });
        }

        private static bool CanSee(
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEntitySnapshotCommit target)
        {
            if (observer.EntityIndex == target.EntityIndex)
                return true;

            var dx = observer.PositionX - target.PositionX;
            var dy = observer.PositionZ - target.PositionZ;
            var range = Math.Max(180, observer.Visibility);
            return dx * dx + dy * dy <= range * range;
        }

        private static bool IsPlayerControlled(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBodyKind(AetheriaRuntimeBodySnapshotCommit body, string kind)
        {
            return body != null &&
                   string.Equals(body.Kind ?? "", kind ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class RenderSplatBuilder
        {
            private readonly List<double> _centerX = new List<double>();
            private readonly List<double> _centerY = new List<double>();
            private readonly List<double> _halfExtentX = new List<double>();
            private readonly List<double> _halfExtentY = new List<double>();
            private readonly List<double> _rotationCos = new List<double>();
            private readonly List<double> _rotationSin = new List<double>();
            private readonly List<int> _channel = new List<int>();
            private readonly List<int> _falloff = new List<int>();
            private readonly List<double> _valueR = new List<double>();
            private readonly List<double> _valueG = new List<double>();
            private readonly List<double> _valueB = new List<double>();
            private readonly List<double> _valueA = new List<double>();
            private readonly List<string> _sourceKey = new List<string>();
            private readonly List<int> _layerIndex = new List<int>();
            private readonly List<int> _sourceKind = new List<int>();
            private readonly List<double> _frequencyX = new List<double>();
            private readonly List<double> _frequencyY = new List<double>();
            private readonly List<double> _phaseX = new List<double>();
            private readonly List<double> _phaseY = new List<double>();
            private readonly List<double> _animationSpeed = new List<double>();
            private readonly List<double> _sourceFlags = new List<double>();

            public void Add(
                int layerIndex,
                double centerX,
                double centerY,
                double halfExtentX,
                double halfExtentY,
                int channel,
                int falloff,
                double valueR,
                double valueG,
                double valueB,
                double valueA,
                string sourceKey,
                double rotationRadians = 0,
                int sourceKind = AetheriaRuntimeRenderSplatSourceKinds.Constant,
                double frequencyX = 1,
                double frequencyY = 1,
                double phaseX = 0,
                double phaseY = 0,
                double animationSpeed = 0,
                double sourceFlags = 0)
            {
                _layerIndex.Add(Math.Max(0, layerIndex));
                _centerX.Add(centerX);
                _centerY.Add(centerY);
                _halfExtentX.Add(Math.Max(0, halfExtentX));
                _halfExtentY.Add(Math.Max(0, halfExtentY));
                _rotationCos.Add(Math.Cos(rotationRadians));
                _rotationSin.Add(Math.Sin(rotationRadians));
                _channel.Add(channel);
                _falloff.Add(falloff);
                _valueR.Add(valueR);
                _valueG.Add(valueG);
                _valueB.Add(valueB);
                _valueA.Add(valueA);
                _sourceKey.Add(sourceKey ?? "");
                _sourceKind.Add(sourceKind);
                _frequencyX.Add(frequencyX);
                _frequencyY.Add(frequencyY);
                _phaseX.Add(phaseX);
                _phaseY.Add(phaseY);
                _animationSpeed.Add(animationSpeed);
                _sourceFlags.Add(sourceFlags);
            }

            public AetheriaRuntimeRenderSplatSoa Build()
            {
                return new AetheriaRuntimeRenderSplatSoa
                {
                    Count = _centerX.Count,
                    CenterX = _centerX.ToArray(),
                    CenterY = _centerY.ToArray(),
                    HalfExtentX = _halfExtentX.ToArray(),
                    HalfExtentY = _halfExtentY.ToArray(),
                    RotationCos = _rotationCos.ToArray(),
                    RotationSin = _rotationSin.ToArray(),
                    Channel = _channel.ToArray(),
                    Falloff = _falloff.ToArray(),
                    ValueR = _valueR.ToArray(),
                    ValueG = _valueG.ToArray(),
                    ValueB = _valueB.ToArray(),
                    ValueA = _valueA.ToArray(),
                    SourceKey = _sourceKey.ToArray(),
                    LayerIndex = _layerIndex.ToArray(),
                    SourceKind = _sourceKind.ToArray(),
                    FrequencyX = _frequencyX.ToArray(),
                    FrequencyY = _frequencyY.ToArray(),
                    PhaseX = _phaseX.ToArray(),
                    PhaseY = _phaseY.ToArray(),
                    AnimationSpeed = _animationSpeed.ToArray(),
                    SourceFlags = _sourceFlags.ToArray()
                };
            }
        }

        private static DocumentContext Context(AetheriaRuntimeDaemonFrameDocument frame)
        {
            var run = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var zones = run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();
            var zone = zones.FirstOrDefault(candidate => candidate.ZoneIndex == run.CurrentZoneIndex) ??
                zones.FirstOrDefault() ??
                new AetheriaRuntimeZoneSnapshotCommit();
            var runId = string.IsNullOrWhiteSpace(run.RunId) ? "local-rts" : run.RunId;
            return new DocumentContext(run, zone, runId);
        }

        private static double Stat(AetheriaRuntimeEntitySnapshotCommit entity, string name)
        {
            var grid = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            return grid?.Values?.FirstOrDefault() ?? 0;
        }

        private static AetheriaRuntimeCurrentEntityHudStatus CurrentEntityHudStatus(
            AetheriaRuntimeEntitySnapshotCommit? entity)
        {
            if (entity == null)
                return new AetheriaRuntimeCurrentEntityHudStatus();

            var states = entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>();
            var radiatorTemperatures = states
                .Where(state => string.Equals(state?.BehaviorKind, "Radiator", StringComparison.OrdinalIgnoreCase))
                .Select(state => state.RadiatorTemperature)
                .Where(temperature => temperature > 0)
                .ToArray();
            var capacitorStates = states
                .Where(state => state != null && state.CapacitorCapacity > 0)
                .ToArray();
            var driveState = states.FirstOrDefault(state => state != null && state.AetherDriveMaximumRpm > 0);

            return new AetheriaRuntimeCurrentEntityHudStatus
            {
                OverrideShutdown = entity.OverrideShutdown,
                ShieldActive = Stat(entity, "shield") > 0,
                HeatsinksEnabled = entity.HeatsinksEnabled,
                Heatstroke = entity.Heatstroke,
                Hypothermia = entity.Hypothermia,
                Visibility = entity.Visibility,
                HullDurabilityRatio = Math.Clamp(Stat(entity, "hull"), 0, 1),
                RadiatorTemperatureMinimum = radiatorTemperatures.Length == 0 ? 0 : radiatorTemperatures.Min(),
                RadiatorTemperatureMaximum = radiatorTemperatures.Length == 0 ? 0 : radiatorTemperatures.Max(),
                RadiatorCount = radiatorTemperatures.Length,
                SensorCooldown = states
                    .Where(state => string.Equals(state?.BehaviorKind, "Sensor", StringComparison.OrdinalIgnoreCase))
                    .Select(state => state.PingCooldown)
                    .DefaultIfEmpty(0)
                    .Max(),
                ReactorDraw = states.Sum(state => state?.ReactorDraw ?? 0),
                CapacitorCharge = capacitorStates.Sum(state => state.CapacitorCharge),
                CapacitorCapacity = capacitorStates.Sum(state => state.CapacitorCapacity),
                AetherDriveRpmX = driveState?.AetherDriveRpmX ?? 0,
                AetherDriveRpmY = driveState?.AetherDriveRpmY ?? 0,
                AetherDriveRpmZ = driveState?.AetherDriveRpmZ ?? 0,
                AetherDriveMaximumRpm = driveState?.AetherDriveMaximumRpm ?? 0
            };
        }

        private static AetheriaRuntimeEntitySnapshotCommit? FindDockParent(
            AetheriaRuntimeZoneSnapshotCommit zone,
            int currentEntityIndex,
            out int dockingBayIndex)
        {
            dockingBayIndex = -1;
            if (currentEntityIndex < 0)
                return null;

            foreach (var candidate in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                var assignments = candidate?.DockingBayAssignments ?? Array.Empty<int>();
                for (var index = 0; index < assignments.Count; index++)
                {
                    if (assignments[index] != currentEntityIndex)
                        continue;

                    dockingBayIndex = index;
                    return candidate;
                }
            }

            return null;
        }

        private static int TryParseEntityIndex(string? entityKey)
        {
            if (string.IsNullOrWhiteSpace(entityKey))
                return -1;

            const string marker = ".entity.";
            var markerIndex = entityKey.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return -1;

            var start = markerIndex + marker.Length;
            var end = entityKey.IndexOf('.', start);
            var text = end < 0 ? entityKey.Substring(start) : entityKey.Substring(start, end - start);
            return int.TryParse(text, out var value) ? value : -1;
        }

        private readonly struct DocumentContext
        {
            public DocumentContext(
                AetheriaRuntimeRunCheckpointCommit run,
                AetheriaRuntimeZoneSnapshotCommit zone,
                string runId)
            {
                Run = run;
                Zone = zone;
                RunId = runId ?? "";
            }

            public AetheriaRuntimeRunCheckpointCommit Run { get; }
            public AetheriaRuntimeZoneSnapshotCommit Zone { get; }
            public string RunId { get; }
        }
    }
}
