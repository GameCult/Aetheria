using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.PluginFields;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeGameDocuments
    {
        // Unity built-in mesh 10210 is a one-unit Quad; canonical ARPG.unity
        // scales the gameplay Zone Brushes parent to 3000.
        private const double FossilZoneBrushHalfExtent = 1500.0;
        private const double FossilGlobalBrushExponent = 0.2;

        public static AetheriaRuntimeGameViewportDocument Viewport(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeViewportBounds viewport)
        {
            var objects = ObjectsViewport(frame, viewport);
            var gravity = GravityViewport(frame, viewport);

            return new AetheriaRuntimeGameViewportDocument
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
            AetheriaRuntimeViewportBounds viewport)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            viewport ??= new AetheriaRuntimeViewportBounds();

            var normalizedViewport = Normalize(viewport);
            var context = Context(frame);
            var run = context.Run;
            var zone = context.Zone;
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            var projectiles = zone.PhysicalPayloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>();
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
                    .Concat(projectiles
                        .Where(projectile => projectile != null && projectile.Active)
                        .Where(projectile => IntersectsViewport(projectile, normalizedViewport))
                        .Select(ToViewportObject))
                    .ToArray()
            };
        }

        public static AetheriaRuntimeGravityViewportDocument GravityViewport(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeViewportBounds viewport)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            viewport ??= new AetheriaRuntimeViewportBounds();

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
                Bodies = visibleBodies.Select(body => ToBodyView(body, frame.RenderSettings)).ToArray(),
                TerrainRadius = zone.GravityTerrainRadius,
                TerrainDepth = zone.GravityTerrainDepth,
                TerrainDepthExponent = zone.GravityTerrainDepthExponent,
                TerrainWaveFrequency = zone.GravityTerrainWaveFrequency
            };
        }

        public static EveFieldsSplatsDocument RenderSplatsViewport(
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeViewportBounds viewport)
        {
            frame ??= new AetheriaRuntimeDaemonFrameDocument();
            viewport ??= new AetheriaRuntimeViewportBounds();

            var normalizedViewport = Normalize(viewport);
            var context = Context(frame);
            var zone = context.Zone;
            var layers = BuildDefaultRenderSplatLayers();
            var layerIndices = layers
                .Select((layer, index) => (layer.LayerKey, index))
                .ToDictionary(pair => pair.LayerKey, pair => pair.index, StringComparer.Ordinal);
            var splats = new RenderSplatBuilder();
            if (zone.GravityTerrainRadius > 0 && zone.GravityTerrainDepth != 0)
            {
                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.GravityHeight],
                    0,
                    0,
                    zone.GravityTerrainRadius,
                    zone.GravityTerrainRadius,
                    AetheriaRuntimeRenderSplatChannels.Gravity,
                    EveFieldsSplatFalloffs.PowerPulse,
                    -zone.GravityTerrainDepth,
                    0,
                    0,
                    1,
                    "environment.gravity_terrain",
                    falloffScale: 1,
                    falloffExponent: Math.Max(0.0001, zone.GravityTerrainDepthExponent));
                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight],
                    0,
                    0,
                    zone.GravityTerrainRadius,
                    zone.GravityTerrainRadius,
                    AetheriaRuntimeRenderSplatChannels.Gravity,
                    EveFieldsSplatFalloffs.PowerPulse,
                    zone.GravityTerrainDepth,
                    0,
                    0,
                    1,
                    "environment.gravity_terrain:fog.surface_height",
                    falloffScale: 1,
                    falloffExponent: Math.Max(0.0001, zone.GravityTerrainDepthExponent));
                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight],
                    0,
                    0,
                    zone.GravityTerrainRadius,
                    zone.GravityTerrainRadius,
                    AetheriaRuntimeRenderSplatChannels.Tint,
                    EveFieldsSplatFalloffs.PowerPulse,
                    zone.GravityTerrainDepth,
                    0,
                    0,
                    1,
                    "environment.gravity_terrain:fog.patch_height",
                    falloffScale: 1,
                    falloffExponent: Math.Max(0.0001, zone.GravityTerrainDepthExponent));
            }

            AddFossilGlobalFogBrushes(splats, layerIndices);

            var bodyPoses = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone)
                .ToDictionary(pose => pose.BodyKey, StringComparer.Ordinal);
            foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            {
                if (body == null ||
                    string.IsNullOrWhiteSpace(body.BodyKey) ||
                    !bodyPoses.TryGetValue(body.BodyKey, out var pose))
                    continue;

                var radius = ResolveGravityRadius(body);
                if (pose.CenterX + radius < normalizedViewport.MinX ||
                    pose.CenterX - radius > normalizedViewport.MaxX ||
                    pose.CenterZ + radius < normalizedViewport.MinY ||
                    pose.CenterZ - radius > normalizedViewport.MaxY)
                {
                    continue;
                }

                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.GravityHeight],
                    pose.CenterX,
                    pose.CenterZ,
                    radius,
                    radius,
                    AetheriaRuntimeRenderSplatChannels.Gravity,
                    EveFieldsSplatFalloffs.PowerPulse,
                    -body.GravityWellDepth,
                    0,
                    0,
                    1,
                    body.BodyKey ?? "",
                    falloffScale: 2,
                    falloffExponent: Math.Max(0.0001, body.GravityDepthExponent));
                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight],
                    pose.CenterX,
                    pose.CenterZ,
                    radius,
                    radius,
                    AetheriaRuntimeRenderSplatChannels.Gravity,
                    EveFieldsSplatFalloffs.PowerPulse,
                    body.GravityWellDepth,
                    0,
                    0,
                    1,
                    $"{body.BodyKey ?? ""}:fog.surface_height",
                    falloffScale: 2,
                    falloffExponent: Math.Max(0.0001, body.GravityDepthExponent));
                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight],
                    pose.CenterX,
                    pose.CenterZ,
                    radius,
                    radius,
                    AetheriaRuntimeRenderSplatChannels.Tint,
                    EveFieldsSplatFalloffs.PowerPulse,
                    body.GravityWellDepth,
                    0,
                    0,
                    1,
                    $"{body.BodyKey ?? ""}:fog.patch_height",
                    falloffScale: 2,
                    falloffExponent: Math.Max(0.0001, body.GravityDepthExponent));

                if ((IsBodyKind(body, "gas_giant") || IsBodyKind(body, "sun")) &&
                    body.GravityWaveRadius > 0 && body.GravityWaveDepth != 0)
                {
                    AddFossilRadialWaveBrush(
                        splats,
                        layerIndices[AetheriaRuntimeRenderSplatLayerKeys.GravityWave],
                        body,
                        pose.CenterX,
                        pose.CenterZ,
                        AetheriaRuntimeRenderSplatChannels.GravityWave,
                        $"{body.BodyKey ?? ""}:gravity.wave");
                    AddFossilRadialWaveBrush(
                        splats,
                        layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight],
                        body,
                        pose.CenterX,
                        pose.CenterZ,
                        AetheriaRuntimeRenderSplatChannels.Tint,
                        $"{body.BodyKey ?? ""}:fog.surface_height.wave");
                    AddFossilRadialWaveBrush(
                        splats,
                        layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight],
                        body,
                        pose.CenterX,
                        pose.CenterZ,
                        AetheriaRuntimeRenderSplatChannels.Tint,
                        $"{body.BodyKey ?? ""}:fog.patch_height.wave");
                }

                if (IsBodyKind(body, "sun"))
                {
                    // The tint prefab also uses the one-unit built-in Quad.
                    var tintHalfExtent = Math.Pow(Math.Max(0, body.Mass), 0.25) * 300 *
                        Math.Max(0.01, body.SunVisual?.LightRadiusMultiplier ?? 1.0) * 0.5;
                    splats.Add(
                        layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogTint],
                        pose.CenterX,
                        pose.CenterZ,
                        tintHalfExtent,
                        tintHalfExtent,
                        AetheriaRuntimeRenderSplatChannels.Tint,
                        EveFieldsSplatFalloffs.PowerPulse,
                        (body.SunVisual?.FogTintColorX ?? 0) * 0.25,
                        (body.SunVisual?.FogTintColorY ?? 0) * 0.25,
                        (body.SunVisual?.FogTintColorZ ?? 0) * 0.25,
                        1,
                        $"{body.BodyKey ?? ""}:fog.tint",
                        falloffScale: 1,
                        falloffExponent: 16);
                }
            }

            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                var entityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(
                    context.RunId,
                    zone.ZoneIndex,
                    entity.EntityIndex);
                AddEntityFogFieldEmitters(splats, layerIndices, entity, entityKey, normalizedViewport);

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
                    entityKey);
            }

            return new EveFieldsSplatsDocument
            {
                FrameId = frame.FrameId,
                PublishedAtUtc = frame.PublishedAtUtc ?? "",
                SimulationTimeSeconds = frame.SimulationTimeSeconds,
                RunId = context.RunId,
                ZoneIndex = zone.ZoneIndex,
                ZoneName = string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneIndex}" : zone.Name,
                Viewport = new EveFieldsViewport
                {
                    MinX = normalizedViewport.MinX,
                    MinY = normalizedViewport.MinY,
                    MaxX = normalizedViewport.MaxX,
                    MaxY = normalizedViewport.MaxY
                },
                Layers = layers,
                Splats = splats.Build()
            };
        }

        private static IReadOnlyList<EveFieldsSplatLayer> BuildDefaultRenderSplatLayers()
        {
            return new[]
            {
                new EveFieldsSplatLayer
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.GravityHeight,
                    DisplayName = "Gravity Height",
                    Channel = AetheriaRuntimeRenderSplatChannels.Gravity,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new EveFieldsSplatLayer
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.GravityWave,
                    DisplayName = "Gravity Wave",
                    Channel = AetheriaRuntimeRenderSplatChannels.GravityWave,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new EveFieldsSplatLayer
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.Visibility,
                    DisplayName = "Visibility Mask",
                    Channel = AetheriaRuntimeRenderSplatChannels.Visibility,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Max,
                    GraphicsFormat = "R16_SFloat"
                },
                new EveFieldsSplatLayer
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight,
                    DisplayName = "Fog Surface Height",
                    Channel = AetheriaRuntimeRenderSplatChannels.Tint,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new EveFieldsSplatLayer
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight,
                    DisplayName = "Fog Patch Height",
                    Channel = AetheriaRuntimeRenderSplatChannels.Tint,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new EveFieldsSplatLayer
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogPatch,
                    DisplayName = "Fog Patch",
                    Channel = AetheriaRuntimeRenderSplatChannels.Tint,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                },
                new EveFieldsSplatLayer
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.FogTint,
                    DisplayName = "Fog Tint",
                    Channel = AetheriaRuntimeRenderSplatChannels.Tint,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "B10G11R11_UFloatPack32"
                },
                new EveFieldsSplatLayer
                {
                    LayerKey = AetheriaRuntimeRenderSplatLayerKeys.Influence,
                    DisplayName = "Influence",
                    Channel = AetheriaRuntimeRenderSplatChannels.Influence,
                    BlendMode = AetheriaRuntimeRenderSplatBlendModes.Add,
                    GraphicsFormat = "R16_SFloat"
                }
            };
        }

        private static void AddFossilGlobalFogBrushes(
            RenderSplatBuilder splats,
            IReadOnlyDictionary<string, int> layerIndices)
        {
            var surfaceHeight = layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogSurfaceHeight];
            var patchHeight = layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogPatchHeight];
            var patch = layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogPatch];
            var tint = layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogTint];

            AddFossilSimplexBrush(splats, surfaceHeight, "environment.fog_surface.wave_large", 2, -2.46, 0.02, 0.1);
            AddFossilSimplexBrush(splats, surfaceHeight, "environment.fog_surface.wave_medium", 7.58, 1, 0.01, 0.0375);
            AddFossilSimplexBrush(splats, surfaceHeight, "environment.fog_surface.wave_small", -16.75, 0, 0.005, 0.075);
            AddFossilSimplexBrush(splats, patchHeight, "environment.fog_patch_height.wave_large", 2, -2.46, 0.02, 0.1);
            AddFossilSimplexBrush(splats, patchHeight, "environment.fog_patch_height.wave_medium", 7.58, 1, 0.01, 0.0375);
            AddFossilSimplexBrush(splats, patchHeight, "environment.fog_patch_height.wave_small", -16.75, 0, 0.005, 0.075);
            AddFossilSimplexBrush(splats, patchHeight, "environment.fog_patch_height.displacement", 10, -1, 0.0002, 0.01);
            AddFossilSimplexBrush(splats, patch, "environment.fog_patch.depth_micro", 10, 0, 0.005, 0.025);
            AddFossilCellBrush(splats, patch, "environment.fog_patch.depth_macro", 20, 0.001, 0.025);

            splats.Add(
                tint,
                0,
                0,
                FossilZoneBrushHalfExtent,
                FossilZoneBrushHalfExtent,
                AetheriaRuntimeRenderSplatChannels.Tint,
                EveFieldsSplatFalloffs.PowerPulse,
                0.025 * 0.496,
                0.025 * 0.5813884,
                0.025,
                1,
                "environment.fog_tint.ambient",
                falloffScale: 1,
                falloffExponent: 2);
        }

        private static void AddEntityFogFieldEmitters(
            RenderSplatBuilder splats,
            IReadOnlyDictionary<string, int> layerIndices,
            AetheriaRuntimeEntitySnapshotCommit entity,
            string entityKey,
            AetheriaRuntimeViewportBounds viewport)
        {
            if (!HasActiveFogFieldEmitter(entity))
                return;

            var emitters = entity.FogFieldEmitters ?? Array.Empty<AetheriaRuntimeFogFieldEmitterCommit>();
            for (var emitterIndex = 0; emitterIndex < emitters.Count; emitterIndex++)
            {
                var emitter = emitters[emitterIndex];
                if (emitter == null || !emitter.Enabled || !double.IsFinite(emitter.Radius) || emitter.Radius <= 0)
                    continue;

                var centerX = entity.PositionX + emitter.OffsetX;
                var centerY = entity.PositionZ + emitter.OffsetZ;
                if (centerX + emitter.Radius < viewport.MinX ||
                    centerX - emitter.Radius > viewport.MaxX ||
                    centerY + emitter.Radius < viewport.MinY ||
                    centerY - emitter.Radius > viewport.MaxY)
                {
                    continue;
                }

                splats.Add(
                    layerIndices[AetheriaRuntimeRenderSplatLayerKeys.FogPatch],
                    centerX,
                    centerY,
                    emitter.Radius,
                    emitter.Radius,
                    AetheriaRuntimeRenderSplatChannels.Tint,
                    EveFieldsSplatFalloffs.PowerPulse,
                    double.IsFinite(emitter.Density) ? Math.Max(0, emitter.Density) : 0,
                    0,
                    0,
                    1,
                    $"{entityKey}:fog-field:{emitterIndex}",
                    falloffScale: 1,
                    falloffExponent: double.IsFinite(emitter.FalloffExponent)
                        ? Math.Max(0.0001, emitter.FalloffExponent)
                        : 2);
            }
        }

        public static bool HasActiveFogFieldEmitter(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            return entity != null &&
                entity.IsActive &&
                (entity.FogFieldEmitters ?? Array.Empty<AetheriaRuntimeFogFieldEmitterCommit>())
                .Any(emitter => emitter != null &&
                    emitter.Enabled &&
                    double.IsFinite(emitter.Radius) &&
                    emitter.Radius > 0);
        }

        private static void AddFossilSimplexBrush(
            RenderSplatBuilder splats,
            int layerIndex,
            string sourceKey,
            double depth,
            double constant,
            double frequency,
            double speed)
        {
            if (constant != 0)
            {
                splats.Add(
                    layerIndex,
                    0,
                    0,
                    FossilZoneBrushHalfExtent,
                    FossilZoneBrushHalfExtent,
                    AetheriaRuntimeRenderSplatChannels.Tint,
                    EveFieldsSplatFalloffs.PowerPulse,
                    constant,
                    0,
                    0,
                    1,
                    sourceKey + ":constant",
                    falloffScale: 1,
                    falloffExponent: FossilGlobalBrushExponent);
            }

            splats.Add(
                layerIndex,
                0,
                0,
                FossilZoneBrushHalfExtent,
                FossilZoneBrushHalfExtent,
                AetheriaRuntimeRenderSplatChannels.Tint,
                EveFieldsSplatFalloffs.PowerPulse,
                depth,
                0,
                0,
                1,
                sourceKey,
                sourceKind: EveFieldsSplatSourceKinds.AnimatedSimplexNoise,
                frequencyX: frequency,
                frequencyY: frequency,
                animationSpeed: speed,
                sourceFlags: EveFieldsSplatSourceFlags.AbsoluteValue,
                falloffScale: 1,
                falloffExponent: FossilGlobalBrushExponent);
        }

        private static void AddFossilCellBrush(
            RenderSplatBuilder splats,
            int layerIndex,
            string sourceKey,
            double depth,
            double frequency,
            double speed)
        {
            splats.Add(
                layerIndex,
                0,
                0,
                FossilZoneBrushHalfExtent,
                FossilZoneBrushHalfExtent,
                AetheriaRuntimeRenderSplatChannels.Tint,
                EveFieldsSplatFalloffs.PowerPulse,
                depth,
                0,
                0,
                1,
                sourceKey,
                sourceKind: EveFieldsSplatSourceKinds.AnimatedCellNoiseB,
                frequencyX: frequency,
                frequencyY: frequency,
                animationSpeed: speed,
                falloffScale: 1,
                falloffExponent: FossilGlobalBrushExponent);
        }

        private static void AddFossilRadialWaveBrush(
            RenderSplatBuilder splats,
            int layerIndex,
            AetheriaRuntimeBodySnapshotCommit body,
            double centerX,
            double centerY,
            int channel,
            string sourceKey)
        {
            // The fossil scales a one-unit Quad by GravityWaveRadius. Local
            // distance therefore reaches one at physical radius / 2.
            var halfExtent = body.GravityWaveRadius * 0.5;
            splats.Add(
                layerIndex,
                centerX,
                centerY,
                halfExtent,
                halfExtent,
                channel,
                EveFieldsSplatFalloffs.PowerPulse,
                body.GravityWaveDepth,
                0,
                0,
                1,
                sourceKey,
                sourceKind: EveFieldsSplatSourceKinds.AnimatedRadialCosine,
                frequencyX: Math.Max(0.0001, body.GravityWaveFrequency),
                frequencyY: 1.25,
                animationSpeed: body.GravityWaveSpeed,
                falloffScale: 1,
                falloffExponent: 8);
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
                ? Array.Empty<AetheriaRuntimeInventoryItem>()
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
                    ? new AetheriaRuntimeEntityStatus()
                    : new AetheriaRuntimeEntityStatus
                    {
                        Hull = Stat(entity, "hull"),
                        Shield = Stat(entity, "shield"),
                        Heat = Stat(entity, "heat")
                    },
                Inventory = inventory,
                Equipment = inventory.Where(item => string.Equals(item.Source, "equipment", StringComparison.Ordinal)).ToArray(),
                Cargo = inventory.Where(item => string.Equals(item.Source, "cargo", StringComparison.Ordinal)).ToArray(),
                ShutdownPerformance = entity?.ShutdownPerformance ?? 0,
                Hud = CurrentEntityHudStatus(entity),
                WormholeTransition = entity?.WormholeTransition == null
                    ? null
                    : new AetheriaRuntimeWormholeTransitionView
                    {
                        TransitionId = entity.WormholeTransition.TransitionId,
                        Phase = entity.WormholeTransition.Phase,
                        Progress = entity.WormholeTransition.Progress,
                        SourceZoneIndex = entity.WormholeTransition.SourceZoneIndex,
                        TargetZoneIndex = entity.WormholeTransition.TargetZoneIndex,
                        VisualDepthOffset = entity.WormholeTransition.VisualDepthOffset
                    }
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
            var currentEntityIndex = TryParseEntityIndex(context.Run.CurrentEntityKey);

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
                    .Select(entity => ToZoneTargetRow(entity, entityMap[entity.TargetEntityIndex]))
                    .ToArray(),
                Contacts = entities
                    .Where(entity => entity != null && entity.EntityIndex != currentEntityIndex)
                    .SelectMany(entity => (entity.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                        .Where(contact => contact != null &&
                                          contact.TargetEntityIndex >= 0 &&
                                          entityMap.ContainsKey(contact.TargetEntityIndex))
                        .Select(contact => ProjectZoneContactRow(
                            entity.EntityIndex,
                            entity.EntityIndex,
                            entity,
                            entityMap[contact.TargetEntityIndex],
                            contact)))
                    .Concat(AetheriaRuntimeDaemonRenderQueries.QueryEffectiveContacts(context.Zone, currentEntityIndex)
                        .Where(effective => entityMap.ContainsKey(effective.Contact.TargetEntityIndex))
                        .Select(effective => ProjectZoneContactRow(
                            effective.ObserverEntityIndex,
                            effective.PrimarySensorSourceEntityIndex,
                            entityMap[currentEntityIndex],
                            entityMap[effective.Contact.TargetEntityIndex],
                            effective.Contact)))
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
                : StationStock(parent);
            var dockingBays = parent == null
                ? Array.Empty<AetheriaRuntimeStationDockingBayRow>()
                : ProjectStationDockingBays(context, parent, currentEntityIndex);
            var cargoTargets = parent == null
                ? Array.Empty<AetheriaRuntimeStationCargoTargetRow>()
                : ProjectStationCargoTargets(parentKey, dockingBayIndex, dockingBays, availableEntities);
            stationStock = StationStockTradeFacts(
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

        private static AetheriaRuntimeZoneTargetRow ToZoneTargetRow(
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
            int observerEntityIndex,
            int primarySensorSourceEntityIndex,
            AetheriaRuntimeEntitySnapshotCommit observer,
            AetheriaRuntimeEntitySnapshotCommit target,
            AetheriaRuntimeEntityContactCommit contact)
        {
            var deltaX = target.PositionX - observer.PositionX;
            var deltaY = target.PositionY - observer.PositionY;
            var deltaZ = target.PositionZ - observer.PositionZ;
            return new AetheriaRuntimeZoneContactRow
            {
                ObserverEntityIndex = observerEntityIndex,
                PrimarySensorSourceEntityIndex = primarySensorSourceEntityIndex,
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
                HomeZones = (run.HomeZones ?? Array.Empty<AetheriaRuntimeFactionZoneCommit>())
                    .Where(entry => entry != null)
                    .ToArray(),
                BossZones = (run.BossZones ?? Array.Empty<AetheriaRuntimeFactionZoneCommit>())
                    .Where(entry => entry != null)
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
            var zoneRenderRadius = AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(
                zone, AetheriaRuntimeDaemonRenderQueries.DefaultZoneRenderRadius);

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
                WormholeExits = ZoneRenderWormholeExits(run, zone, zoneRenderRadius, frame.RenderSettings),
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
                    .ToArray(),
                PhysicalPayloads = (zone.PhysicalPayloads ?? Array.Empty<AetheriaRuntimePhysicalPayloadCommit>())
                    .Where(projectile => projectile != null && projectile.Active)
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
                ? Array.Empty<AetheriaRuntimeInventoryItem>()
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

        public static AetheriaRuntimeViewportBounds Normalize(AetheriaRuntimeViewportBounds viewport)
        {
            viewport ??= new AetheriaRuntimeViewportBounds();
            return new AetheriaRuntimeViewportBounds
            {
                MinX = Math.Min(viewport.MinX, viewport.MaxX),
                MinY = Math.Min(viewport.MinY, viewport.MaxY),
                MaxX = Math.Max(viewport.MinX, viewport.MaxX),
                MaxY = Math.Max(viewport.MinY, viewport.MaxY)
            };
        }

        public static bool IntersectsViewport(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeViewportBounds viewport)
        {
            return entity.PositionX >= viewport.MinX &&
                entity.PositionX <= viewport.MaxX &&
                entity.PositionZ >= viewport.MinY &&
                entity.PositionZ <= viewport.MaxY;
        }

        public static bool IntersectsViewport(
            AetheriaRuntimePhysicalPayloadCommit projectile,
            AetheriaRuntimeViewportBounds viewport)
        {
            return projectile.PositionX >= viewport.MinX &&
                projectile.PositionX <= viewport.MaxX &&
                projectile.PositionZ >= viewport.MinY &&
                projectile.PositionZ <= viewport.MaxY;
        }

        public static bool GravityInfluenceIntersectsViewport(
            AetheriaRuntimeBodySnapshotCommit body,
            AetheriaRuntimeViewportBounds viewport)
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

        private static AetheriaRuntimeViewportObject ToViewportObject(
            AetheriaRuntimeEntitySnapshotCommit entity,
            string runId,
            int zoneIndex)
        {
            var obj = new AetheriaRuntimeViewportObject
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
                Status = new AetheriaRuntimeEntityStatus
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

        private static AetheriaRuntimeViewportObject ToViewportObject(
            AetheriaRuntimePhysicalPayloadCommit projectile)
        {
            return new AetheriaRuntimeViewportObject
            {
                EntityIndex = -1,
                EntityKey = projectile.PayloadId ?? "",
                DisplayName = projectile.PayloadKind ?? "projectile",
                Kind = "projectile",
                FactionKey = projectile.FactionKey ?? "",
                X = projectile.PositionX,
                Y = projectile.PositionZ,
                Z = projectile.PositionY,
                DirectionX = projectile.DirectionX,
                DirectionY = projectile.DirectionY,
                VelocityX = projectile.VelocityX,
                VelocityY = projectile.VelocityY,
                Controlled = false,
                TargetEntityIndex = projectile.TargetEntityIndex,
                IsActive = projectile.Active,
                Visibility = projectile.Radius,
                Status = new AetheriaRuntimeEntityStatus
                {
                    Hull = projectile.ContactMagnitude,
                    Shield = 0,
                    Heat = projectile.AgeSeconds
                },
                Inventory = Array.Empty<AetheriaRuntimeInventoryItem>(),
                IconAsset = AetheriaRuntimeAssetRef.FromKey(
                    "map.entity.projectile",
                    AetheriaRuntimeAssetKinds.Sprite,
                    "cultmesh://aetheria/assets/map/entity/projectile",
                    AetheriaRuntimeAssetTransports.CultMesh,
                    "image/*")
            };
        }

        private static AetheriaRuntimeBodyView ToBodyView(
            AetheriaRuntimeBodySnapshotCommit body,
            AetheriaRuntimeDaemonRenderSettings renderSettings)
        {
            var view = new AetheriaRuntimeBodyView
            {
                BodyKey = body.BodyKey ?? "",
                OrbitKey = body.OrbitKey ?? "",
                Name = body.Name ?? "",
                Kind = body.Kind ?? "",
                X = body.GravityInfluenceCenterX,
                Y = body.GravityInfluenceCenterZ,
                Radius = Math.Max(32, body.BodyRadiusMultiplier * 70),
                IsAsteroidBelt = (body.Kind ?? "").IndexOf("asteroid", StringComparison.OrdinalIgnoreCase) >= 0,
                Body = body,
                IconSize = body.IconSize > 0 ? body.IconSize : renderSettings.ResolveBodyIconSize(body.Mass)
            };
            view.IconAsset = AetheriaRuntimeAssets.ResolveBodyIcon(view);
            return view;
        }

        private static AetheriaRuntimeGravityInfluence ToGravityInfluence(AetheriaRuntimeBodySnapshotCommit body)
        {
            return new AetheriaRuntimeGravityInfluence
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
            double zoneRenderRadius,
            AetheriaRuntimeDaemonRenderSettings renderSettings)
        {
            return AetheriaRuntimeDaemonRenderQueries
                .QueryWormholeExits(run, zone, zoneRenderRadius, renderSettings.WormholeDistanceRatio)
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

        private static IReadOnlyList<AetheriaRuntimeInventoryItem> Inventory(AetheriaRuntimeEntitySnapshotCommit entity)
        {
            var items = new List<AetheriaRuntimeInventoryItem>();
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
                    CargoItems = StationStock(entity)
                });
            }

            return options.ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeStationStockItem> StationStock(
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
                        ? StationStock(contents[dockingBayIndex], dockingBayIndex)
                        : Array.Empty<AetheriaRuntimeStationStockItem>()
                });
            }

            return rows.ToArray();
        }

        private static IReadOnlyList<AetheriaRuntimeStationStockItem> StationStock(
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

        private static IReadOnlyList<AetheriaRuntimeStationStockItem> StationStockTradeFacts(
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
                    var price = StationStockPrice(stock, catalog);
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

        private static int StationStockPrice(
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
            List<AetheriaRuntimeInventoryItem> items,
            string source,
            int sourceIndex,
            AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            var item = slot.Item ?? new AetheriaRuntimeLoadoutItemCommit();
            items.Add(new AetheriaRuntimeInventoryItem
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
            private readonly List<double> _falloffScale = new List<double>();
            private readonly List<double> _falloffExponent = new List<double>();

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
                double sourceFlags = 0,
                double falloffScale = 1,
                double falloffExponent = 1)
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
                _falloffScale.Add(Math.Max(0, falloffScale));
                _falloffExponent.Add(Math.Max(0.0001, falloffExponent));
            }

            public EveFieldsSplatSoa Build()
            {
                return new EveFieldsSplatSoa
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
                    SourceFlags = _sourceFlags.ToArray(),
                    FalloffScale = _falloffScale.ToArray(),
                    FalloffExponent = _falloffExponent.ToArray()
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
            var runId = string.IsNullOrWhiteSpace(run.RunId) ? "local-terminus" : run.RunId;
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
                MeanTemperature = Stat(entity, AetheriaRuntimeThermalSimulation.MeanTemperatureGrid),
                MinimumTemperature = Stat(entity, AetheriaRuntimeThermalSimulation.MinimumTemperatureGrid),
                MaximumTemperature = Stat(entity, AetheriaRuntimeThermalSimulation.MaximumTemperatureGrid),
                ThermalVisibility = Stat(entity, "thermal-visibility"),
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
