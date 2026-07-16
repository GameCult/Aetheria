using Aetheria.State;
using Aetheria.State.Daemon;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;
using GameCult.Eve.PluginFields;
using System.Globalization;

var checks = new AetheriaDaemonYmirSmokeChecks();
if (args.Contains("--gravity", StringComparer.Ordinal))
{
    checks.RunGravity();
    Console.WriteLine("Daemon Ymir gravity-sign smoke passed.");
}
else if (args.Contains("--loadout", StringComparer.Ordinal))
{
    checks.RunLoadout();
    Console.WriteLine("Daemon loadout hardpoint smoke passed.");
}
else if (args.Contains("--pickup", StringComparer.Ordinal))
{
    checks.RunPickup();
    Console.WriteLine("Daemon Ymir pickup-contact smoke passed.");
}
else if (args.Contains("--payload", StringComparer.Ordinal))
{
    checks.RunPayload();
    Console.WriteLine("Daemon retained Ymir payload smoke passed.");
}
else
{
    checks.Run();
    Console.WriteLine("Daemon Ymir physical payload smoke passed.");
}

internal sealed class AetheriaDaemonYmirSmokeChecks
{
    private static readonly List<AetheriaYmirWorldPhysics> OwnedPhysics = [];

    public void RunGravity() => RunCheck(PositiveGravityDepthAttractsAndProjectsAsAWell);

    public void RunLoadout() => DaemonLoadoutsRespectFactionAvailabilityAndHullRoles();

    public void RunPickup()
    {
        RunCheck(RetainedWorldAdvancesEveryFixedSubstep);
        RunCheck(StableEntityIdentitySurvivesCrossZoneReindex);
        RunCheck(TractorRampsAndPullsThroughYmirWithoutTeleportingCargo);
        RunCheck(PickupIsCapacityCheckedExactlyOnceAndExpires);
        RunCheck(StationBodiesCannotConsumePickups);
        RunCheck(PickupShieldContactCollectsOrBounces);
        RunCheck(YmirRestartDoesNotReplayConsumedPickupContact);
    }

    public void RunPayload()
    {
        RunCheck(YmirMovesProjectileAndReportsStableContact);
        RunCheck(PayloadQueryExcludesItsSource);
        RunCheck(PayloadBodiesDoNotCollideWithEachOther);
        RunCheck(OverlappingMineRemainsQueryableAfterArming);
    }

    private static void RetainedWorldAdvancesEveryFixedSubstep()
    {
        var ship = Entity(0, 0, "player");
        ship.VelocityX = 10;
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "retained-substep-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [ship] }]
        };
        using var physics = NewPhysics();
        AetheriaRuntimeDaemonTickRunner.Tick(
            Path.Combine(Path.GetTempPath(), "aetheria-retained-substeps.cc"),
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                WorldPhysics = physics,
                FrameId = 1,
                FixedDeltaSeconds = 0.1,
                SimulationStepCount = 3,
                SimulationTimeSeconds = 0.3,
                BuildPublications = false
            });
        RequireNear(3, ship.PositionX, 0.05,
            "one retained Box3D world must advance once for every fixed substep, not once per publication frame");
    }

    private static void StableEntityIdentitySurvivesCrossZoneReindex()
    {
        var departing = Entity(0, 0, "player");
        departing.EntityId = "cross-zone.departing";
        var survivor = Entity(1, 100, "player");
        survivor.EntityId = "cross-zone.survivor";
        var source = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities = [departing, survivor]
        };
        var destination = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 1, Entities = [] };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "cross-zone-retained-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [source, destination]
        };
        using var physics = NewPhysics();
        AetheriaRuntimeDaemonTickRunner.Tick(
            Path.Combine(Path.GetTempPath(), "aetheria-cross-zone-before.cc"),
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                WorldPhysics = physics,
                FrameId = 1,
                FixedDeltaSeconds = 0.1,
                SimulationTimeSeconds = 0.1,
                BuildPublications = false
            });

        survivor.EntityIndex = 0;
        departing.EntityIndex = 0;
        departing.PositionX = 250;
        source.Entities = [survivor];
        destination.Entities = [departing];
        run.CurrentZoneIndex = 1;
        run.CurrentEntityKey = "zone.1.entity.0";
        AetheriaRuntimeDaemonTickRunner.Tick(
            Path.Combine(Path.GetTempPath(), "aetheria-cross-zone-after.cc"),
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                WorldPhysics = physics,
                FrameId = 2,
                FixedDeltaSeconds = 0.1,
                SimulationTimeSeconds = 0.2,
                BuildPublications = false
            });

        RequireNear(100, survivor.PositionX, 0.05,
            "source-zone reindex must not give a survivor the departed entity's retained Box3D body");
        RequireNear(250, departing.PositionX, 0.05,
            "a cross-zone arrival must retain its stable identity while spawning in the destination session");
    }

    public void Run()
    {
        Action[] checks =
        [
            PositiveGravityDepthAttractsAndProjectsAsAWell,
            VolumeSurfaceKeepsNativeShaderAbiInAssetVariant,
            YmirMovesProjectileAndReportsStableContact,
            InstantWeaponRequestSurvivesLockAcquisition,
            ConstantWeaponRunsOnDaemonThroughYmirBeamContact,
            ChargedWeaponCannotBypassChargeLifecycle,
            ChargedWeaponHoldRiskMalfunctionsDeterministically,
            DeployableWeaponRunsThroughYmirAndDetonatesOnDaemon,
            DeployableRangeExpiryDetonatesAfterYmirMovement,
            CanonicalCatalogPublishesRecoveredMineLauncher,
            EnergyFundedShieldInterceptsDamageBeforeHull,
            ArmorCellsResolveBeforeEquipmentAndHull,
            DestructionDropsLootExactlyOnce,
            DaemonSimulationTreatsYmirHitAsPresentationOnly,
            ProjectileContactCannotKill,
            MissingWorldPhysicsOwnerCannotAdvanceShips,
            TractorRampsAndPullsThroughYmirWithoutTeleportingCargo,
            PickupIsCapacityCheckedExactlyOnceAndExpires,
            TradePurchaseDerivesAcceptanceFromDaemonState,
            StationBodiesCannotConsumePickups,
            PickupShieldContactCollectsOrBounces,
            YmirRestartDoesNotReplayConsumedPickupContact,
            ThermalCellsUseFossilConductionAndRadiation,
            EnergyNetworkSettlesReactorAfterConsumers,
            RadiatorPumpsHeatBeforeReactorSettlement,
            EquipmentThermalPerformanceOwnsShutdownAndWear,
            ThermalTopologyComesFromHullAndEquipment,
            ThermalMedicalExposureUsesCockpitTemperature,
            ThermalMedicalDeathUsesOrdinaryDestructionPath,
            MultipleActorsUseTheSameMovementLever,
            DirectionalThrustersOwnOrdinaryFlightFeedback,
            UnpoweredThrustersCannotMoveOrAdvertisePlume,
            RareAetherDriveSpoolsOnlyOnModifiedHullFixture,
            LookDirectionRejectsInvalidVectorsWithoutMutatingTheShip,
            AgentClaimsAndCompletesExploreTaskThroughCommands,
            SchedulerAssignsHighestPriorityCompatibleTask,
            SchedulerRequeuesTaskFromDeadAgent,
            SchedulerCollapsesDuplicateAssignmentMarkers,
            SchedulerAssignsShortestGalaxyRoute,
            AgentTraversesGalaxyRouteBeforeExecutingTask,
            IdleAgentReturnsToCanonicalHomeAndDocks,
            ControlledShipDoesNotReceiveAutonomousHelmCommands,
            AttackAgentControlsOptimumRangeThroughMovementLever,
            AttackTaskAdmissionRejectsImpossibleWork,
            AgentCompletesAttackTaskThroughTargetFireAndYmir,
            AgentCompletesHaulTaskThroughMovementAndCargoCommands,
            RejectedHaulTransferDoesNotAdvanceTask,
            AgentPatrolsHistoricalOrbitCircuitThroughMovementCommands,
            TickReconcilesAndEvaluatesCatalogBehaviors,
            DaemonLoadoutsRespectFactionAvailabilityAndHullRoles,
            AgentMinesAsteroidThroughEquippedBehavior,
            CargoCapacityComesFromHullAndCatalogVolumes,
            AgentSurveysBodyIntoCorporationKnowledge,
            AgentTowsStationIntoPersistentOrbit
        ];
        foreach (var check in checks)
            RunCheck(check);
    }

    private static void VolumeSurfaceKeepsNativeShaderAbiInAssetVariant()
    {
        var player = Entity(0, 0, "player");
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "volume-program-ownership-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [player] }]
        };
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        var volume = Flatten(surface.Surface.Root).Single(node => node.Kind == "field.volume3d");
        var stardust = Flatten(surface.Surface.Root).Single(node => node.Kind == "field.particles3d");
        var gravityField = Flatten(surface.Surface.Root).Single(node => node.Kind == "field.surface2d");
        Require(volume.Props.Values.All(value => value == null || !value.Contains("_Nebula", StringComparison.Ordinal)) &&
                volume.Props["layerBindings"].Contains("fog.surface_height=surfaceHeight", StringComparison.Ordinal) &&
                volume.Props["layerTargetDescriptors"].Contains("fog.surface_height=2,2,false,bilinear", StringComparison.Ordinal) &&
                volume.Props["layerTargetDescriptors"].Contains("fog.tint=0.5,0.5,true,trilinear", StringComparison.Ordinal) &&
                volume.Props["viewportTextureScaleBindings"] == "ditherCoordinates=dither" &&
                volume.Props["viewportAnchor"] == stardust.Props["viewportAnchor"] &&
                volume.Props["span"] == stardust.Props["span"] &&
                volume.Props["cellWorldSize"] == stardust.Props["cellWorldSize"] &&
                volume.Props["gravityTexelsPerCell"] == stardust.Props["gravityTexelsPerCell"] &&
                volume.Props["viewportSnapLayer"] == stardust.Props["viewportSnapLayer"] &&
                volume.Props["viewportSnapTexels"] == stardust.Props["viewportSnapTexels"] &&
                volume.Props["documentFloatBindings"] == "simulationTimeSeconds=flowScroll,0.025,0" &&
                !volume.Props.ContainsKey("vectorParameters"),
            "portable Eve volume surfaces must name logical ports rather than Unity shader properties");
        Require(gravityField.Props["minX"] == "-768" &&
                gravityField.Props["minY"] == "-768" &&
                gravityField.Props["maxX"] == "768" &&
                gravityField.Props["maxY"] == "768" &&
                volume.Props["documentRef"] == gravityField.Props["renderSplatsDocumentId"],
            "flight fog rasterization must share the exact 256-by-6 Stardust lattice viewport");
        Require(stardust.Props["documentRef"] == gravityField.Props["renderSplatsDocumentId"] &&
                stardust.Props["computeProgramAssetRef"] == "compute.environment.stardust" &&
                stardust.Props["materialAssetRef"] == "material.environment.stardust" &&
                stardust.Props["span"] == "256" &&
                stardust.Props["threadGroupSize"] == "128" &&
                stardust.Props["particleStrideBytes"] == "28" &&
                stardust.Props["viewportAnchor"] == "active-camera.xz" &&
                stardust.Props["layerBindings"] == "fog.surface_height=surfaceHeight;fog.tint=tint" &&
                stardust.Props["cellWorldSize"] == "6" &&
                stardust.Props["gravityTexelsPerCell"] == "8" &&
                stardust.Props["viewportSnapLayer"] == "fog.surface_height" &&
                stardust.Props["viewportSnapTexels"] == "8" &&
                stardust.Props["documentFloatBindings"] ==
                    "simulationTimeSeconds=time,1,0;simulationTimeSeconds=flowScroll,0.025,0" &&
                stardust.Props["documentTimeVectorPort"] == "timeVector" &&
                stardust.Props["floatParameters"].Contains(
                    "period=2;minimumSize=0.25;maximumSize=0.75;spacing=6;ceilingHeight=0;" +
                    "floorHeight=-10;minHeadroom=25;maxHeadroom=100;heightExponent=3",
                    StringComparison.Ordinal) &&
                !stardust.Props["floatParameters"].Contains("patch", StringComparison.OrdinalIgnoreCase),
            "portable Stardust semantics must preserve the grid/gravity lattice, dispatch, camera snap, time, and particle values exactly");

        var shader = AetheriaRuntimeAssets.ProjectManifest(null).Assets.Single(asset =>
            asset.Ref.AssetKey == "shader.environment.gravity-fog");
        var postProcess = AetheriaRuntimeAssets.ProjectManifest(null).Assets.Single(asset =>
            asset.Ref.AssetKey == "profile.environment.flight");
        var stardustCompute = AetheriaRuntimeAssets.ProjectManifest(null).Assets.Single(asset =>
            asset.Ref.AssetKey == "compute.environment.stardust");
        var stardustMaterial = AetheriaRuntimeAssets.ProjectManifest(null).Assets.Single(asset =>
            asset.Ref.AssetKey == "material.environment.stardust");
        var stardustColors = AetheriaRuntimeAssets.ProjectManifest(null).Assets.Single(asset =>
            asset.Ref.AssetKey == "texture.environment.stardust-colors");
        Require(postProcess.Ref.Kind == AetheriaRuntimeAssetKinds.VolumeProfile &&
                postProcess.Ref.Metadata["presentationRole"] == "environment.post-process.flight",
            "provider asset catalog must carry the advertised flight post-process profile variant");
        Require(shader.Ref.Metadata["unity.volume.texturePort.surfaceHeight"] == "_NebulaSurfaceHeight" &&
                shader.Ref.Metadata["unity.volume.matrixPort.cameraToWorld"] == "_CamToWorld" &&
                shader.Ref.Metadata["unity.volume.pass.raymarch"] == "0" &&
                shader.Ref.Metadata["unity.volume.pass.temporal"] == "1" &&
                shader.Ref.Metadata["unity.volume.pass.composite"] == "2" &&
                shader.Ref.Metadata["unity.volume.texturePort.currentSample"] == "_UndersampleCloudTex" &&
                shader.Ref.Metadata["unity.volume.texturePort.history"] == "_MainTex" &&
                shader.Ref.Metadata["unity.volume.matrixPort.previousViewProjection"] == "_PrevVP" &&
            shader.Ref.Metadata["unity.volume.floatPort.resetHistory"] == "_ResetHistory",
            "provider asset metadata must own the concrete Unity volume-program ABI projected into runtime variants");
        Require(stardustCompute.Ref.Kind == AetheriaRuntimeAssetKinds.ComputeShader &&
                stardustCompute.Ref.Metadata["unity.particles.kernel.update"] == "UpdateParticles" &&
                stardustCompute.Ref.Metadata["unity.particles.bufferPort.particles"] == "particles" &&
                !stardustCompute.Ref.Metadata.ContainsKey("unity.particles.texturePort.patch") &&
                !stardustCompute.Ref.Metadata.ContainsKey("unity.particles.texturePort.patchHeight") &&
                !stardustCompute.Ref.Metadata.ContainsKey("unity.particles.floatPort.patchDensity") &&
                !stardustCompute.Ref.Metadata.ContainsKey("unity.particles.floatPort.patchBlend") &&
                stardustCompute.Ref.Metadata["unity.particles.vectorPort.viewportTransform"] == "_GridTransform" &&
                stardustCompute.Ref.Metadata["unity.particles.vectorPort.timeVector"] == "_Time" &&
                stardustCompute.Ref.Metadata["unity.particles.intPort.span"] == "span" &&
                stardustMaterial.Ref.Metadata["unity.particles.bufferPort.particles"] == "particles" &&
                stardustMaterial.Ref.Metadata["unity.particles.bufferPort.quadPoints"] == "quadPoints" &&
                stardustColors.Ref.Metadata["unityAssetPath"] == "Assets/Resources/Gradients/blackbody.png",
            "provider asset metadata must own the exact Stardust compute/render ABI and authored color ramp");
        var fogVolume = Flatten(surface.Surface.Root)
            .Single(component => string.Equals(component.Kind, "field.volume3d", StringComparison.Ordinal));
        Require(fogVolume.Props.TryGetValue("floatParameters", out var fogParameters) &&
                fogParameters.Contains("compositeOpacity=1", StringComparison.Ordinal),
            "the portable fog surface must retain the fossil cloud shader's full-strength composite default");
    }

    private static AetheriaYmirWorldPhysics NewPhysics()
    {
        AetheriaYmirWorldPhysics physics = new();
        OwnedPhysics.Add(physics);
        return physics;
    }

    private static void RunCheck(Action check)
    {
        try
        {
            check();
        }
        finally
        {
            foreach (var physics in OwnedPhysics)
                physics.Dispose();
            OwnedPhysics.Clear();
        }
    }

    private static void PositiveGravityDepthAttractsAndProjectsAsAWell()
    {
        var body = new AetheriaRuntimeBodySnapshotCommit
        {
            BodyKey = "gravity-owner",
            Kind = "gas_giant",
            GravityInfluenceCenterX = 0,
            GravityInfluenceCenterZ = 0,
            GravityInfluenceRadius = 100,
            GravityWellDepth = 20,
            GravityWaveRadius = 80,
            GravityWaveDepth = 3,
            GravityWaveSpeed = 2,
            GravityWaveFrequency = 6
        };
        var actor = Entity(0, 10, "player");
        actor.PositionZ = 0;
        actor.VelocityX = 0;
        actor.VelocityY = 0;
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Bodies = [body],
            Entities = [actor],
            GravityTerrainRadius = 100,
            GravityTerrainDepth = 5
        };

        using var physics = NewPhysics();
        physics.RetainWorlds("gravity-sign-smoke", [zone.ZoneIndex]);
        var step = physics.Step("gravity-sign-smoke", 1, 0, zone, zone.Entities, 0.1);
        var steppedActor = step.Bodies.Single(value => value.EntityIndex == actor.EntityIndex);
        Require(steppedActor.VelocityX < 0,
            "positive authored gravity depth must accelerate a body toward the well center through Ymir");
        Require(AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(zone, 10, 0, 0) < 0,
            "the same positive gravity-depth magnitude must project as a negative terrain well");
        var splats = AetheriaRuntimeGameDocuments.RenderSplatsViewport(
            new AetheriaRuntimeDaemonFrameDocument
            {
                Run = new AetheriaRuntimeRunCheckpointCommit
                {
                    RunId = "gravity-sign-smoke",
                    CurrentZoneIndex = 0,
                    Zones = [zone]
                }
            },
            new AetheriaRuntimeViewportBounds { MinX = -20, MinY = -20, MaxX = 20, MaxY = 20 });
        var bodySplatIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == body.BodyKey).index;
        var terrainSplatIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == "environment.gravity_terrain").index;
        var fogSurfaceSplatIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == "environment.gravity_terrain:fog.surface_height").index;
        var gravityWaveIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == body.BodyKey + ":gravity.wave").index;
        var fogSurfaceWaveIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == body.BodyKey + ":fog.surface_height.wave").index;
        var fogPatchWaveIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == body.BodyKey + ":fog.patch_height.wave").index;
        Require(splats.Splats.ValueR[bodySplatIndex] < 0,
            "render-splat projection must convert positive canonical body depth into a negative gravity-height value");
        Require(splats.Splats.Falloff[terrainSplatIndex] == EveFieldsSplatFalloffs.PowerPulse &&
                splats.Splats.CenterX[terrainSplatIndex] == 0 &&
                splats.Splats.CenterY[terrainSplatIndex] == 0 &&
                splats.Splats.HalfExtentX[terrainSplatIndex] == zone.GravityTerrainRadius &&
                splats.Splats.FalloffScale[terrainSplatIndex] == 1,
            "zone gravity terrain must remain the fossil radial PowerPulse rather than a viewport-sized solid slab");
        Require(splats.Splats.Falloff[bodySplatIndex] == EveFieldsSplatFalloffs.PowerPulse &&
                splats.Splats.FalloffScale[bodySplatIndex] == 2 &&
                splats.Splats.FalloffExponent[bodySplatIndex] == body.GravityDepthExponent,
            "body gravity wells must publish the fossil radial PowerPulse scale and exponent");
        Require(splats.Splats.LayerIndex[fogSurfaceSplatIndex] != splats.Splats.LayerIndex[terrainSplatIndex] &&
                splats.Splats.ValueR[fogSurfaceSplatIndex] == -splats.Splats.ValueR[terrainSplatIndex] &&
                splats.Splats.ValueR[fogSurfaceSplatIndex] > 0 &&
                splats.Splats.Falloff[fogSurfaceSplatIndex] == EveFieldsSplatFalloffs.PowerPulse,
            "the fog surface must project positive fossil PowerBrush depth without changing negative gameplay terrain height");
        foreach (var waveIndex in new[] { gravityWaveIndex, fogSurfaceWaveIndex, fogPatchWaveIndex })
        {
            Require(splats.Splats.SourceKind[waveIndex] == EveFieldsSplatSourceKinds.AnimatedRadialCosine &&
                    splats.Splats.HalfExtentX[waveIndex] == body.GravityWaveRadius * 0.5 &&
                    splats.Splats.FrequencyX[waveIndex] == body.GravityWaveFrequency &&
                    splats.Splats.FrequencyY[waveIndex] == 1.25 &&
                    splats.Splats.AnimationSpeed[waveIndex] == body.GravityWaveSpeed &&
                    splats.Splats.Falloff[waveIndex] == EveFieldsSplatFalloffs.PowerPulse &&
                    splats.Splats.FalloffExponent[waveIndex] == 8,
                "gravity, fog surface, and fog patch height must share the exact fossil radial-wave source");
        }
        var patchLayerIndex = splats.Layers
            .Select((layer, index) => (layer, index))
            .Single(value => value.layer.LayerKey == EveFieldsSplatLayerKeys.FogPatch).index;
        var patchLayer = splats.Layers[patchLayerIndex];
        var patchMicroIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == "environment.fog_patch.depth_micro").index;
        var patchMacroIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == "environment.fog_patch.depth_macro").index;
        var ambientTintIndex = splats.Splats.SourceKey
            .Select((sourceKey, index) => (sourceKey, index))
            .Single(value => value.sourceKey == "environment.fog_tint.ambient").index;
        Require(patchLayer.BlendMode == EveFieldsSplatBlendModes.Add,
            "fossil patch depth brushes must add instead of taking the maximum");
        Require(splats.Splats.SourceKind[patchMicroIndex] == EveFieldsSplatSourceKinds.AnimatedSimplexNoise &&
                splats.Splats.SourceFlags[patchMicroIndex] == EveFieldsSplatSourceFlags.AbsoluteValue &&
                splats.Splats.FrequencyX[patchMicroIndex] == 0.005 &&
                splats.Splats.AnimationSpeed[patchMicroIndex] == 0.025 &&
                splats.Splats.HalfExtentX[patchMicroIndex] == 1500,
            "fog micro-patch must retain the fossil world-space animated absolute-simplex brush");
        Require(splats.Splats.SourceKind[patchMacroIndex] == EveFieldsSplatSourceKinds.AnimatedCellNoiseB &&
                splats.Splats.FrequencyX[patchMacroIndex] == 0.001 &&
                splats.Splats.AnimationSpeed[patchMacroIndex] == 0.025 &&
                splats.Splats.ValueR[patchMacroIndex] == 20,
            "fog macro-patch must retain the fossil moving cellular-B brush");
        Require(splats.Splats.ValueR[ambientTintIndex] > 0 &&
                splats.Splats.ValueB[ambientTintIndex] > splats.Splats.ValueG[ambientTintIndex] &&
                splats.Splats.ValueG[ambientTintIndex] > splats.Splats.ValueR[ambientTintIndex],
            "fog tint must include the fossil ambient blue brush before local body lights");
        Require(!splats.Splats.SourceKey.Contains("environment.fog_patch") &&
                !splats.Splats.SourceKey.Contains("environment.fog_patch_height"),
            "the viewport-local placeholder fog producers must remain deleted");
        var ordinaryPlanetSplats = AetheriaRuntimeGameDocuments.RenderSplatsViewport(
            new AetheriaRuntimeDaemonFrameDocument
            {
                Run = new AetheriaRuntimeRunCheckpointCommit
                {
                    CurrentZoneIndex = 0,
                    Zones =
                    [
                        new AetheriaRuntimeZoneSnapshotCommit
                        {
                            ZoneIndex = 0,
                            Bodies =
                            [
                                new AetheriaRuntimeBodySnapshotCommit
                                {
                                    BodyKey = "ordinary-planet",
                                    Kind = "planet",
                                    GravityInfluenceRadius = 20,
                                    GravityWaveRadius = 20,
                                    GravityWaveDepth = 4
                                }
                            ]
                        }
                    ]
                }
            },
            new AetheriaRuntimeViewportBounds { MinX = -30, MinY = -30, MaxX = 30, MaxY = 30 });
        Require(!ordinaryPlanetSplats.Splats.SourceKey.Any(key =>
                key.StartsWith("ordinary-planet:", StringComparison.Ordinal) &&
                key.EndsWith(".wave", StringComparison.Ordinal)),
            "ordinary planets must not acquire gas-giant gravity-wave visuals merely because compatibility rows contain wave values");

        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "gravity-height-smoke",
            CurrentZoneIndex = zone.ZoneIndex,
            Zones = [zone]
        };
        AetheriaRuntimeDaemonSimulation.Step(
            run,
            new AetheriaRuntimeDaemonIntentState(),
            0.1,
            new AetheriaRuntimeDaemonSimulationSettings(),
            physics,
            simulationTimeSeconds: 0.1);
        RequireNear(
            AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(
                zone, actor.PositionX, actor.PositionZ, 0.1),
            actor.PositionY,
            0.0001,
            "daemon entities must retain the fossil terrain-height projection while Ymir owns XZ physics");
    }

    private static void ConstantWeaponRunsOnDaemonThroughYmirBeamContact()
    {
        var source = Entity(0, 0, "player");
        source.DirectionX = 1;
        source.DirectionY = 0;
        source.TargetEntityIndex = 2;
        source.WeaponGroups = [new[] { 0 }];
        source.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-beam", Quality = 1, Durability = 1, Enabled = true } },
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-beam-capacitor", Quality = 1, Durability = 1, Enabled = true } }
        ];
        source.BehaviorStates = [new AetheriaRuntimeBehaviorStateCommit
        {
            OwnerKind = AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, OwnerIndex = 1, BehaviorIndex = 0,
            BehaviorKind = "Capacitor", CapacitorCharge = 10, CapacitorCapacity = 10, CapacitorEfficiency = 1
        }];
        source.CargoContents = [Cargo(("beam-ammo", 1, 0, 0))];
        var blocker = Entity(1, 40, "neutral");
        var selectedTarget = Entity(2, 100, "raider");
        var zone = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [source, blocker, selectedTarget] };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "constant-weapon-smoke", CurrentZoneIndex = 0,
            CurrentEntityKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey("constant-weapon-smoke", 0, 0),
            Zones = [zone]
        };
        var payload = new AetheriaRuntimeBehaviorPayload(0, AetheriaRuntimeBehaviorKinds.ConstantWeapon, 0,
        [
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(10)),
            new AetheriaRuntimeBehaviorField(6, PerformanceStat(150)),
            new AetheriaRuntimeBehaviorField(9, PerformanceStat(2)),
            new AetheriaRuntimeBehaviorField(10, PerformanceStat(3)),
            new AetheriaRuntimeBehaviorField(12, new AetheriaRuntimeBehaviorValue(
                "item-key", "", 0, false, "", "beam-ammo", [], [])),
            new AetheriaRuntimeBehaviorField(13, Number(3)),
            new AetheriaRuntimeBehaviorField(14, Number(0.3)),
            new AetheriaRuntimeBehaviorField(17, Number(0.15))
        ]);
        var catalog = new AetheriaRuntimeCatalogSnapshot(
            [CatalogItem("test-beam", payload), CatalogItem("test-beam-capacitor", CapacitorPayload(10, 1))], [], []);
        var intents = new AetheriaRuntimeDaemonIntentState();
        intents.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
        {
            ActorEntityKey = "zone.0.entity.0", WeaponGroup = 0, Fire = true, Active = true
        });
        for (var frame = 0; frame < 7; frame++)
            AetheriaRuntimeDaemonSimulation.Step(run, intents, 0.1,
                new AetheriaRuntimeDaemonSimulationSettings(),
                NewPhysics(), catalog, frame, frame * 0.1);

        var state = source.WeaponStates.Single(value => value.BehaviorKind == AetheriaRuntimeBehaviorKinds.ConstantWeapon);
        Require(run.GameEvents.Count(value => value.Kind == "weapon.firing.started") == 1,
            "held constant fire must publish one start transition");
        Require(run.GameEvents.Count(value => value.Kind == "weapon.reload.started") == 1 &&
                run.GameEvents.Count(value => value.Kind == "weapon.firing.stopped") == 1,
            "empty constant weapon magazine must start reload and stop the held effect once");
        Require(state.Reloading && !state.Firing,
            "constant weapon reload must remain authoritative persisted state");
        Require(Math.Abs(Stat(blocker, "hull") - 100) < 0.000001 && Stat(selectedTarget, "hull") < 100,
            "constant weapon must resolve against its firing solution rather than a presentation collider");
        Require(run.ShotReceipts.Any(value => value.TargetEntityIndex == selectedTarget.EntityIndex &&
                value.WeaponItemKey == "test-beam" && value.Hit),
            "daemon must publish accepted continuous shot receipts with authored beam identity");
        Require(CargoQuantity(source, "beam-ammo") == 0,
            "constant weapon reload must consume its reserve cargo through the shared transaction");
        Require(source.BehaviorStates.Single(value => value.BehaviorKind == "Capacitor").CapacitorCharge < 10,
            "constant weapon must pay elapsed-time energy through canonical capacitor state");
        for (var frame = 7; frame < 11; frame++)
            AetheriaRuntimeDaemonSimulation.Step(run, intents, 0.1,
                new AetheriaRuntimeDaemonSimulationSettings(),
                NewPhysics(), catalog, frame, frame * 0.1);
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            new AetheriaRuntimeDaemonSimulationSettings(),
            NewPhysics(), catalog, 11, 1.1);
        Require(!state.Firing && run.GameEvents.Count(value => value.Kind == "weapon.firing.stopped") == 2,
            "releasing held fire must stop the persisted constant weapon and publish its transition");
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 11, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        var weaponNodes = Flatten(surface.Surface.Root).Where(node => node.Kind == "weapon.state").ToArray();
        Require(weaponNodes.Any(node =>
                node.Props["behaviorKind"] == AetheriaRuntimeBehaviorKinds.ConstantWeapon &&
                node.Props["itemKey"] == "test-beam" && node.Props["firing"] == "false"),
            $"Eve world entity must project recoverable generic ConstantWeapon state; nodes={string.Join(";", weaponNodes.Select(node => string.Join(",", node.Props.Select(pair => $"{pair.Key}={pair.Value}"))))}");
        Require(Flatten(surface.Surface.Root).Any(node => node.Kind == "feedback.event" &&
                node.Props["eventKind"] == "weapon.firing.started" && node.Props["itemKey"] == "test-beam"),
            "Eve feedback stream must project continuous weapon transition chronology");
        var cockpit = Flatten(surface.Surface.Root).Single(node => node.Id == "aetheria.daemon.game.cockpit");
        Require(cockpit.Kind == "pane" && cockpit.Props["role"] == "pilot.cockpit" &&
                cockpit.Layout["position"] == "absolute" && cockpit.Style["background"] == "transparent",
            "pilot surface must publish a transparent provider-authored cockpit overlay");
        Require(Flatten(cockpit).Count(node => node.Kind == "progress") >= 7 &&
                Flatten(cockpit).Any(node => node.Id == "aetheria.daemon.game.cockpit.capacitor") &&
                Flatten(cockpit).Any(node => node.Id == "aetheria.daemon.game.cockpit.targetLock" &&
                    node.Props["label"] == "TARGET LOCK") &&
                Flatten(cockpit).Any(node => node.Id == "aetheria.daemon.game.cockpit.targetHull"),
            "pilot cockpit must expose native ship and target instrumentation through generic Eve progress components");
        Require(Flatten(surface.Surface.Root).Where(node =>
                    node.Id is "aetheria.daemon.game.frame" or "aetheria.daemon.game.player" or "aetheria.daemon.game.commands")
                .All(node => node.Layout.TryGetValue("display", out var display) && display == "none"),
            "operator diagnostics must remain published state without becoming pilot-camera UI");
    }

    private static void ChargedWeaponCannotBypassChargeLifecycle()
    {
        var source = Entity(0, 0, "player");
        source.DirectionX = 1;
        source.TargetEntityIndex = -1;
        source.WeaponGroups = [new[] { 0 }];
        source.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-charged", Quality = 1, Durability = 1, Enabled = true }
        }];
        var target = Entity(1, 80, "raider");
        var zone = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [source, target] };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "charged-weapon-admission-smoke", CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0", Zones = [zone]
        };
        var payload = new AetheriaRuntimeBehaviorPayload(0, AetheriaRuntimeBehaviorKinds.ChargedWeapon, 0,
        [
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(20)),
            new AetheriaRuntimeBehaviorField(6, PerformanceStat(150)),
            new AetheriaRuntimeBehaviorField(15, PerformanceStat(1000000000)),
            new AetheriaRuntimeBehaviorField(16, PerformanceStat(200)),
            new AetheriaRuntimeBehaviorField(17, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(19, PerformanceStat(0.5)),
            new AetheriaRuntimeBehaviorField(21, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(24, new AetheriaRuntimeBehaviorValue("bool", "", 0, true, "", "", [], [])),
            new AetheriaRuntimeBehaviorField(27, Number(2))
        ]);
        var intents = new AetheriaRuntimeDaemonIntentState();
        intents.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
        {
            ActorEntityKey = "zone.0.entity.0", WeaponGroup = 0, Fire = true, Active = true
        });
        AetheriaRuntimeDaemonSimulation.Step(run, intents, 0.1,
            new AetheriaRuntimeDaemonSimulationSettings(),
            NewPhysics(),
            new AetheriaRuntimeCatalogSnapshot([CatalogItem("test-charged", payload)], [], []), 0, 0);

        Require(zone.PhysicalPayloads.Count == 0 && !run.GameEvents.Any(value => value.Kind == "shot.committed"),
            "pressing an authored ChargedWeapon must not bypass charging through the instant weapon resolver");
        Require(Math.Abs(Stat(target, "hull") - 100) < 0.000001,
            "charged weapon admission must not apply damage before a daemon-owned release commits the shot");
        var state = source.WeaponStates.Single(value => value.BehaviorKind == AetheriaRuntimeBehaviorKinds.ChargedWeapon);
        for (var frame = 1; frame < 11; frame++)
            AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
                new AetheriaRuntimeDaemonSimulationSettings(),
                NewPhysics(),
                new AetheriaRuntimeCatalogSnapshot([CatalogItem("test-charged", payload)], [], []), frame, frame * 0.1);
        Require(state.Charging && state.Charged && state.Charge >= 1 && state.ChargeHoldSeconds > 0,
            "one semantic request must precharge without a firing solution and enter persisted hold state");
        Require(!run.GameEvents.Any(value => value.Kind == "shot.committed"),
            "ready charged weapon must hold rather than invent a firing solution");
        source.TargetEntityIndex = target.EntityIndex;
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            new AetheriaRuntimeDaemonSimulationSettings(),
            NewPhysics(),
            new AetheriaRuntimeCatalogSnapshot([CatalogItem("test-charged", payload)], [], []), 11, 1.1);
        Require(run.GameEvents.Count(value => value.Kind == "weapon.charge.committed") == 1 &&
                run.GameEvents.Any(value => value.Kind == "shot.committed" &&
                    value.ItemKey == "test-charged" && Math.Abs(value.ScalarValue - 40) < 0.000001),
            "stored full charge must commit automatically when a firing solution becomes available");
        var miss = run.ShotReceipts.Single(value => value.WeaponItemKey == "test-charged");
        var missDx = miss.EndpointX - target.PositionX;
        var missDz = miss.EndpointZ - target.PositionZ;
        Require(!miss.Hit && miss.AppliedDamage == 0 && miss.HitRoll > miss.HitProbability,
            "extreme authored dispersion must deterministically resolve a miss without damage");
        RequireEqual("none", miss.ImpactKind, "a miss must not advertise a fabricated impact response");
        RequireEqual("bolt", miss.PresentationKind, "instant weapon receipts must advertise a runtime-neutral travelling effect");
        Require(Math.Sqrt(missDx * missDx + missDz * missDz) > 20 &&
                miss.ImpactAngleRoll >= 0 && miss.ImpactAngleRoll < 1 &&
                miss.ImpactRadiusRoll >= 0 && miss.ImpactRadiusRoll < 1,
            "miss receipt must carry independent named impact rolls and an endpoint outside the target silhouette");
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 11, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        Require(Flatten(surface.Surface.Root).Any(node => node.Kind == "weapon.state" &&
                node.Props.ContainsKey("chargeMalfunctionRisk")),
            "Eve must expose charged hold duration and malfunction risk generically");
        Require(Flatten(surface.Surface.Root).Any(node => node.Kind == "feedback.event" &&
                node.Props["eventKind"] == "weapon.charge.committed"),
            "Eve feedback must expose charged solution commit chronology");
    }

    private static void InstantWeaponRequestSurvivesLockAcquisition()
    {
        var source = Entity(0, 0, "player");
        source.DirectionX = 1;
        source.VelocityX = 1;
        source.TargetEntityIndex = 1;
        source.Contacts = [new AetheriaRuntimeEntityContactCommit
        {
            TargetEntityIndex = 1, InfoGathered = 1, Hostile = true, Visible = true
        }];
        source.WeaponGroups = [new[] { 0 }];
        source.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-instant", Quality = 1, Durability = 1, Enabled = true }
        }];
        var target = Entity(1, 80, "raider");
        var zone = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [source, target] };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "instant-trigger-smoke", CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0", Zones = [zone]
        };
        var payload = new AetheriaRuntimeBehaviorPayload(0, AetheriaRuntimeBehaviorKinds.InstantWeapon, 0,
        [
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(20)),
            new AetheriaRuntimeBehaviorField(6, PerformanceStat(150)),
            new AetheriaRuntimeBehaviorField(15, PerformanceStat(0)),
            new AetheriaRuntimeBehaviorField(16, PerformanceStat(200)),
            new AetheriaRuntimeBehaviorField(17, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(19, PerformanceStat(0.5)),
            new AetheriaRuntimeBehaviorField(23, PerformanceStat(45)),
            new AetheriaRuntimeBehaviorField(24, PerformanceStat(0)),
            new AetheriaRuntimeBehaviorField(25, PerformanceStat(1))
        ]);
        var catalog = new AetheriaRuntimeCatalogSnapshot([CatalogItem("test-instant", payload)], [], []);
        RequireNear(45,
            AetheriaRuntimeEquippedBehaviorQueries.Find(source, catalog, AetheriaRuntimeBehaviorKinds.InstantWeapon)
                .Single().EvaluateStat(23),
            0.000001,
            "lock-transition fixture must retain its authored 45-degree acquisition cone");
        var fire = new AetheriaRuntimeDaemonIntentState();
        fire.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
        {
            ActorEntityKey = "zone.0.entity.0", WeaponGroup = 0, Fire = true, Active = true
        });

        AetheriaRuntimeDaemonSimulation.Step(run, fire, 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 0, 0);
        for (var frame = 1; frame < 12 && run.ShotReceipts.Count == 0; frame++)
            AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
                AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
                NewPhysics(), catalog, frame, frame * 0.1);

        Require(run.ShotReceipts.Count == 1 && run.ShotReceipts[0].SourceEntityIndex == source.EntityIndex,
            $"one instant weapon request must remain pending until daemon lock acquisition commits its shot; " +
            $"receipts={run.ShotReceipts.Count}, direction={source.DirectionX},{source.DirectionY}, contacts={string.Join(";", (source.Contacts ?? []).Select(value => $"{value.TargetEntityIndex}:{value.InfoGathered}"))}, states={string.Join(";", (source.WeaponStates ?? []).Select(value => $"{value.BehaviorKind}:pending={value.TriggerPending}:lock={value.LockProgress}:target={value.LockTargetEntityIndex}:burst={value.BurstRemaining}"))}");
        Require(run.GameEvents.Count(value => value.Kind == "weapon.lock.started" &&
                    value.SourceEntityIndex == 0 && value.TargetEntityIndex == 1) == 1 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.acquired" &&
                    value.SourceEntityIndex == 0 && value.TargetEntityIndex == 1) == 1,
            "daemon lock acquisition must publish one start and one completed transition instead of client-timed feedback");
        var state = source.WeaponStates.Single(value => value.BehaviorKind == AetheriaRuntimeBehaviorKinds.InstantWeapon);
        Require(!state.TriggerPending,
            "instant weapon request must clear after the committed burst begins");
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 12, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        var combat = Flatten(surface.Surface.Root).Single(node => node.Kind == "combat.presentation");
        var world = Flatten(surface.Surface.Root).Single(node => node.Kind == "world.scene3d");
        var aim = Flatten(surface.Surface.Root).Single(node => node.Kind == "aim.presentation");
        Require(!string.IsNullOrWhiteSpace(world.Props["lookCommand"]) &&
                world.Props["lookModel"] == "planar-yaw.v1" &&
                world.Props["lookSensitivityRadians"] == "-0.001",
            "playable world must advertise the daemon-owned continuous look command and fossil pointer response");
        Require(world.Props["cameraRig"] == "perspective.entity-forward-follow.v1" &&
                world.Props["cameraLookAt"] == "aim.convergence-point.v1" &&
                world.Props["cameraDistance"] == "30" &&
                world.Props["cameraVerticalFieldOfViewDegrees"] == "60" &&
                world.Props["cameraTargetScreenX"] == "0.64" &&
                world.Props["cameraTargetScreenY"] == "0.19" &&
                world.Props["cameraPositionDamping"] == "0" &&
                world.Props["cameraNearClipPlane"] == "1" &&
                world.Props["cameraFarClipPlane"] == "4096",
            "undocked world must translate the authored ARPG Third Person Rig into bottom-origin Eve viewport coordinates");
        Require(world.Props["skyboxAssetRef"] == "material.environment.skybox" &&
                world.Props["reflectionAssetRef"] == "texture.environment.reflection" &&
                world.Props["postProcessProfileAssetRef"] == "profile.environment.flight" &&
                world.Props["cameraReconstruction"] == "temporal-reprojection.v1" &&
                world.Props["temporalQuality"] == "high" &&
                world.Props["temporalHistoryBlend"] == "0.99" &&
                world.Props["temporalJitterScale"] == "0.1" &&
                world.Props["temporalSharpening"] == "0" &&
                world.Props["reflectionIntensity"] == "1" &&
                world.Props["ambientLightIntensity"] == "1.46" &&
                !world.Props.ContainsKey("keyLightDirection") &&
                !world.Props.ContainsKey("keyLightColor") &&
                !world.Props.ContainsKey("keyLightIntensity"),
            "playable world must advertise the provider-owned fossil skybox and reflection without inventing a scene light");
        Require(aim.Props["controlledEntityIndex"] == "0" &&
                aim.Props["convergenceTargetEntityId"] == run.EntityRecordKey(0, 1) &&
                aim.Props["minimumConvergenceDistance"] == "50",
            "Eve aim presentation must bind the controlled body direction to the fossil convergence semantic");
        Require(combat.Props["controlledEntityIndex"] == "0" &&
                combat.Props["selectedTargetEntityIndex"] == "1" &&
                combat.Props["targetVisible"] == "true" &&
                combat.Props["targetHostile"] == "true" &&
                double.Parse(combat.Props["lockProgress"], CultureInfo.InvariantCulture) > 0.99,
            "Eve combat presentation must expose daemon-owned selection, contact, and completed lock without duplicating body transforms");
        var cockpitLock = Flatten(surface.Surface.Root).Single(node =>
            node.Id == "aetheria.daemon.game.cockpit.targetLock");
        Require(cockpitLock.Kind == "progress" && cockpitLock.Props["label"] == "TARGET LOCK" &&
                double.Parse(cockpitLock.Props["value"], CultureInfo.InvariantCulture) > 0.99,
            "provider cockpit must project the same completed daemon lock through a generic Eve progress component");
        Require(combat.Props["hitMarkerDurationSeconds"] == "0.25" &&
                combat.Props["radialFillMinimum"] == "0.25" &&
                combat.Props["radialFillMaximum"] == "0.75",
            "Eve combat presentation must preserve the fossil's presentation timing and radial meter semantics");

        var secondTarget = Entity(2, 100, "raider");
        zone.Entities = [source, target, secondTarget];
        source.TargetEntityIndex = secondTarget.EntityIndex;
        source.Contacts =
        [
            new AetheriaRuntimeEntityContactCommit
                { TargetEntityIndex = 1, InfoGathered = 1, Hostile = true, Visible = true },
            new AetheriaRuntimeEntityContactCommit
                { TargetEntityIndex = 2, InfoGathered = 1, Hostile = true, Visible = true }
        ];
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 13, 1.3);
        Require(state.LockTargetEntityIndex == 2 && state.LockProgress > 0 && state.LockProgress < 0.99 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.lost" &&
                    value.TargetEntityIndex == 1 && value.Reason == "target-changed") == 1 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.started" &&
                    value.TargetEntityIndex == 2) == 1 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.acquired" &&
                    value.TargetEntityIndex == 2) == 0,
            $"idle authored lock behavior must tick every daemon step and transfer chronology exactly once on target change; " +
            $"target={state.LockTargetEntityIndex} progress={state.LockProgress:0.###} events={string.Join('|', run.GameEvents.Where(value => value.Kind.StartsWith("weapon.lock.", StringComparison.Ordinal)).Select(value => $"{value.Kind}:{value.TargetEntityIndex}:{value.Reason}:{value.AuxiliaryValue:0.###}->{value.ScalarValue:0.###}"))}");

        for (var frame = 14; frame <= 17; frame++)
            AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
                AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
                NewPhysics(), catalog, frame, frame * 0.1);
        Require(state.LockProgress > 0.99 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.started" &&
                    value.TargetEntityIndex == 2) == 1 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.acquired" &&
                    value.TargetEntityIndex == 2) == 1,
            "idle authored lock behavior must continue progressive acquisition without duplicating transition events");

        source.DirectionX = -1;
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 18, 1.8);
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 19, 1.9);
        Require(state.LockProgress < 0.99 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.lost" &&
                    value.TargetEntityIndex == 2 && value.Reason == "angle") == 1,
            $"crossing the authored lock angle must publish one loss transition while continued decay remains silent; " +
            $"direction={source.DirectionX},{source.DirectionY} source={source.PositionX:0.###},{source.PositionZ:0.###} target={secondTarget.PositionX:0.###},{secondTarget.PositionZ:0.###} progress={state.LockProgress:0.###} events={string.Join('|', run.GameEvents.Where(value => value.Kind.StartsWith("weapon.lock.", StringComparison.Ordinal)).Select(value => $"{value.Kind}:{value.TargetEntityIndex}:{value.Reason}:{value.AuxiliaryValue:0.###}->{value.ScalarValue:0.###}"))}");

        source.DirectionX = 1;
        for (var frame = 20; frame <= 21; frame++)
            AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
                AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
                NewPhysics(), catalog, frame, frame * 0.1);
        Require(state.LockProgress > 0.99 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.acquired" &&
                    value.TargetEntityIndex == 2) == 2,
            "restored facing must reacquire through a second completed transition without replaying lock start");

        zone.Entities = [source, target];
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 22, 2.2);
        Require(state.LockTargetEntityIndex == -1 && state.LockProgress == 0 &&
                run.ShotReceipts.Count == 1 &&
                run.GameEvents.Count(value => value.Kind == "weapon.lock.lost" &&
                    value.TargetEntityIndex == 2 && value.Reason == "target-invalid") == 1,
            $"an invalid target must clear an idle weapon's authoritative lock and publish one loss transition; " +
            $"target={state.LockTargetEntityIndex} progress={state.LockProgress:0.###} shots={run.ShotReceipts.Count} events={string.Join('|', run.GameEvents.Where(value => value.Kind.StartsWith("weapon.lock.", StringComparison.Ordinal)).Select(value => $"{value.Kind}:{value.TargetEntityIndex}:{value.Reason}:{value.AuxiliaryValue:0.###}->{value.ScalarValue:0.###}"))}");
        var resetSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 22, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        Require(Flatten(resetSurface.Surface.Root).Any(node => node.Kind == "feedback.event" &&
                node.Props["eventKind"] == "weapon.lock.lost" &&
                node.Props["targetEntityIndex"] == "2" &&
                node.Props["reason"] == "target-invalid" &&
                double.Parse(node.Props["auxiliaryValue"], CultureInfo.InvariantCulture) > 0.99),
            "Eve feedback must project the daemon lock-loss transition and its prior completed progress");
    }

    private static void DeployableWeaponRunsThroughYmirAndDetonatesOnDaemon()
    {
        var source = Entity(0, 0, "player");
        source.DirectionX = 1;
        source.WeaponGroups = [new[] { 0 }];
        source.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-mine-layer", Quality = 1, Durability = 1, Enabled = true }
        }];
        var target = Entity(1, 50, "neutral");
        var zone = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [source, target] };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "deployable-smoke", CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0", Zones = [zone]
        };
        var payload = new AetheriaRuntimeBehaviorPayload(0, AetheriaRuntimeBehaviorKinds.DeployableWeapon, 0,
        [
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(35)),
            new AetheriaRuntimeBehaviorField(6, PerformanceStat(100)),
            new AetheriaRuntimeBehaviorField(16, PerformanceStat(10)),
            new AetheriaRuntimeBehaviorField(17, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(19, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(26, Number(0.2)),
            new AetheriaRuntimeBehaviorField(27, Number(5)),
            new AetheriaRuntimeBehaviorField(28, PerformanceStat(30)),
            new AetheriaRuntimeBehaviorField(29, Number(0.2)),
            new AetheriaRuntimeBehaviorField(30, PerformanceStat(50))
        ]);
        var catalog = new AetheriaRuntimeCatalogSnapshot(
            [CatalogItem("test-mine-layer", payload)], [], []);
        var fire = new AetheriaRuntimeDaemonIntentState();
        fire.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
            { ActorEntityKey = "zone.0.entity.0", WeaponGroup = 0, Fire = true, Active = true });

        AetheriaRuntimeDaemonSimulation.Step(run, fire, 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 1, 0.1);
        RequireEqual(1, zone.PhysicalPayloads.Count, "deployable fire must create one persistent physical payload");
        Require(!run.ShotReceipts.Any(), "deployable fire must not masquerade as an ordinary shot receipt");
        var mine = zone.PhysicalPayloads.Single();
        Require(mine.PositionX > 2 && mine.TriggeredAtSeconds < 0,
            "Ymir must advance the unarmed mine without inventing a trigger");

        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 2, 0.2);
        mine = zone.PhysicalPayloads.Single();
        Require(mine.Stationary && mine.TriggeredAtSeconds >= 0 &&
                run.GameEvents.Count(value => value.Kind == "deployable.triggered") == 1,
            "an armed Ymir contact must become one daemon-owned trigger transition");
        RequireNear(100, Stat(target, "hull"), 0.000001,
            "trigger contact must not bypass the authored detonation delay");

        using var soaCache = new CultCache();
        using var soaPublisher = new AetheriaRuntimeDaemonSoaFramePublisher(soaCache, producerEpoch: 1);
        var soaFrame = soaPublisher.BuildCurrentZoneEntities(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 2, Run = run });
        var mineIdentity = soaFrame.View.Identities.Single(identity =>
            identity.EntityId == $"{run.RunId}:zone:0:physical-payload:{mine.PayloadId}");
        Require(mineIdentity.Kind == "physical-payload" && mineIdentity.AssetRef == "prefab.entity.mine" &&
                !mineIdentity.Selectable && mineIdentity.EntityIndex < -1,
            "the playable SoA must carry the retained Ymir mine through its provider-owned asset identity");

        var objects = AetheriaRuntimeGameDocuments.ObjectsViewport(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 2, Run = run },
            new AetheriaRuntimeViewportBounds { MinX = -1000, MinY = -1000, MaxX = 1000, MaxY = 1000 });
        var mineObject = objects.Objects.Single(value => value.EntityKey == mine.PayloadId);
        RequireEqual("projectile", mineObject.Kind,
            "the viewport projection must expose the retained physical payload without reviving world.scene3d as a transform authority");
        Require(mineObject.IsActive && mineObject.IconAsset != null,
            "the viewport projection must expose an active provider-authored presentation for the retained mine body");
        Require(mine.Stationary && mine.TriggeredAtSeconds >= 0,
            "daemon state must retain Ymir-derived motion and trigger state independently of presentation");
        RequireNear(35, mine.PayloadMagnitude, 0.000001, "deployable must retain authored payload magnitude");
        RequireNear(50, mine.BlastRadius, 0.000001, "deployable must retain authored blast radius");
        Require(Math.Abs(target.PositionX - mine.PositionX) <= mine.BlastRadius,
            $"triggered target must remain inside authored blast radius (mine {mine.PositionX}, target {target.PositionX})");

        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.21,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 3, 0.41);
        RequireEqual(0, zone.PhysicalPayloads.Count, "detonated mine must leave canonical payload state");
        RequireNear(65, Stat(target, "hull"), 0.000001,
            "Aetheria must apply authored blast damage after the delay");
        Require(run.GameEvents.Count(value => value.Kind == "deployable.detonated") == 1,
            "daemon must publish one authoritative detonation event");
    }

    private static void DeployableRangeExpiryDetonatesAfterYmirMovement()
    {
        var source = Entity(0, -50, "player");
        var target = Entity(1, 35, "neutral");
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities = [source, target],
            PhysicalPayloads = [new AetheriaRuntimePhysicalPayloadCommit
            {
                PayloadId = "range-expiry-mine",
                PayloadKind = "mine",
                WeaponItemKey = "canonical-mine",
                SourceEntityIndex = source.EntityIndex,
                PositionX = 5,
                VelocityX = 20,
                Radius = 1,
                ActivationDelaySeconds = 10,
                LifetimeSeconds = 30,
                MaximumSourceDistance = 6,
                BlastRadius = 50,
                PayloadMagnitude = 15,
                TriggeredAtSeconds = -1,
                Active = true
            }]
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "range-expiry-smoke", CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0", Zones = [zone]
        };

        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), frameId: 4,
            simulationTimeSeconds: 0.1);

        RequireEqual(0, zone.PhysicalPayloads.Count,
            "mine crossing source-relative range after Ymir movement must detonate in the same tick");
        RequireNear(85, Stat(target, "hull"), 0.000001,
            "range expiry must preserve the fossil splash consequence");
        Require(run.GameEvents.Any(value => value.Kind == "deployable.expired" &&
                value.Reason == "range" && value.ItemKey == "canonical-mine"),
            "daemon must explain range-triggered expiry with stable item identity");
        Require(run.GameEvents.Any(value => value.Kind == "deployable.detonated" &&
                value.Reason == "range" && value.ItemKey == "canonical-mine"),
            "range expiry must flow through the shared detonation transaction");
    }

    private static void CanonicalCatalogPublishesRecoveredMineLauncher()
    {
        var catalog = AetheriaRuntimeCatalogStore.OpenReadOnly(
            Path.Combine(Directory.GetCurrentDirectory(), "GameData", "aetheria-world.cc"));
        var drives = catalog.Items
            .Where(item => item.BehaviorKinds.Contains(AetheriaRuntimeBehaviorKinds.AetherDrive))
            .ToArray();
        var driveHulls = catalog.Items
            .Where(item => item.Hardpoints.Any(point => point.Type == "AetherDrive"))
            .ToArray();
        Require(drives.Length == 1 && drives[0].Name == "Traction" &&
                drives[0].HardpointType == "AetherDrive" && drives[0].Price == 250000 &&
                drives[0].Tags.Contains("rarity:rare", StringComparer.Ordinal),
            "canonical AetherDrive must remain the singular expensive Traction retrofit");
        Require(driveHulls.Length == 1 && driveHulls[0].Name == "LonginusX" &&
                driveHulls[0].Price == 7500000 &&
                driveHulls[0].Tags.Contains("rarity:rare", StringComparer.Ordinal),
            "canonical AetherDrive compatibility must remain confined to the rare modified LonginusX hull");
        var ordinaryShipHulls = catalog.Items.Where(item => item.HullType == "Ship" &&
                item.Hardpoints.All(point => point.Type != "AetherDrive") &&
                !item.Tags.Contains("rarity:rare", StringComparer.Ordinal))
            .ToArray();
        Require(ordinaryShipHulls.Any(item => item.Name == "Longinus") &&
                ordinaryShipHulls.Any(item => item.Name == "Djinni"),
            "the recovered common Longinus and Djinni must own ordinary ship generation");
        var mine = catalog.Items.Single(item =>
            item.ItemKey == "aetheria.item_definition:legacy:e78d3670-3ac6-4d9b-834c-1da4228ac311");
        RequireEqual("Mine Launcher", mine.Name, "recovered item must retain fossil identity");
        RequireEqual("Mine", mine.WeaponType, "recovered item must publish normalized semantic weapon type");
        var behavior = mine.BehaviorPayloads.Single(value =>
            value.Kind == AetheriaRuntimeBehaviorKinds.DeployableWeapon);
        RequireNear(2, behavior.Fields.Single(field => field.Key == 26).Value.NumberValue, 0.000001,
            "catalog mine must retain two-second activation delay");
        RequireNear(30, behavior.Fields.Single(field => field.Key == 27).Value.NumberValue, 0.000001,
            "catalog mine must retain thirty-second lifetime");
        var manifest = AetheriaRuntimeAssets.ProjectManifest(catalog);
        var skybox = manifest.Assets.Single(value => value.Ref.AssetKey == "material.environment.skybox");
        RequireEqual(AetheriaRuntimeAssetKinds.Material, skybox.Ref.Kind,
            "provider must type the fossil skybox as a native material asset");
        RequireEqual("environment.skybox", skybox.Ref.Metadata["presentationRole"],
            "provider must advertise the generic skybox presentation role");
        RequireEqual("Assets/Materials/Skybox.mat", skybox.Ref.Metadata["unityAssetPath"],
            "provider bundle must own the fossil skybox material");
        var reflection = manifest.Assets.Single(value => value.Ref.AssetKey == "texture.environment.reflection");
        RequireEqual(AetheriaRuntimeAssetKinds.Texture, reflection.Ref.Kind,
            "provider must type the pre-generated environment map as a texture asset");
        RequireEqual("environment.reflection", reflection.Ref.Metadata["presentationRole"],
            "provider must advertise the generic reflection presentation role");
        RequireEqual("Assets/Textures/studio2.hdr", reflection.Ref.Metadata["unityAssetPath"],
            "provider bundle must own the pre-generated reflection map without an importer plugin");
        var asset = manifest.Assets.Single(value => value.Ref.AssetKey == "prefab.entity.mine");
        RequireEqual("physical-payload.mine", asset.Ref.Metadata["presentationRole"],
            "provider must advertise the script-free mine presentation role");
        RequireEqual("0.25", asset.Ref.Metadata["triggeredPulseSeconds"],
            "mine presentation must retain the fossil triggered pulse cadence");

        var rareHull = driveHulls.Single();
        var rareHullAssetKey = AetheriaRuntimeAssets.HullPrefabAssetKey(rareHull.ItemKey);
        var rareHullAsset = manifest.Assets.Single(value => value.Ref.AssetKey == rareHullAssetKey);
        var expectedHullResourcePath = rareHull.HullPrefab.Replace('\\', '/');
        const string resourcesPrefix = "Assets/Resources/";
        if (expectedHullResourcePath.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
            expectedHullResourcePath = expectedHullResourcePath[resourcesPrefix.Length..];
        if (expectedHullResourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            expectedHullResourcePath = expectedHullResourcePath[..^".prefab".Length];
        RequireEqual("entity.hull", rareHullAsset.Ref.Metadata["presentationRole"],
            "provider manifest must type catalog hull prefabs as generic hull presentation assets");
        RequireEqual(expectedHullResourcePath, rareHullAsset.Ref.Metadata["resourcesPath"],
            "provider hull asset must retain the catalog-authored prefab instead of a faction fallback");

        var rareShip = Entity(0, 0, "player");
        rareShip.HullItemKey = rareHull.ItemKey;
        var rareRun = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "rare-hull-presentation-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [rareShip] }]
        };
        var rareFrame = new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = rareRun };
        var rareSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            rareFrame,
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"),
            catalog: catalog);
        var rareSurfaceEntity = Flatten(rareSurface.Surface.Root).Single(node =>
            node.Id == "aetheria.daemon.game.world.entity.0");
        RequireEqual(rareHullAssetKey, rareSurfaceEntity.Props["assetRef"],
            "Eve scene projection must select the typed hull asset rather than the player Djinni fallback");
        using var rareSoaCache = new CultCache();
        using var rareSoaPublisher = new AetheriaRuntimeDaemonSoaFramePublisher(rareSoaCache, producerEpoch: 1);
        var rareSoa = rareSoaPublisher.BuildCurrentZoneEntities(rareFrame, catalog);
        RequireEqual(rareHullAssetKey, rareSoa.View.Identities.Single().AssetRef,
            "SoA identity and Eve scene projection must share the catalog-owned hull asset key");
    }

    private static void EnergyFundedShieldInterceptsDamageBeforeHull()
    {
        var source = Entity(0, -50, "player");
        var target = Entity(1, 0, "neutral");
        target.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-energy-shield", Quality = 1, Durability = 1, Enabled = true } },
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-shield-capacitor", Quality = 1, Durability = 1, Enabled = true } }
        ];
        target.BehaviorStates = [new AetheriaRuntimeBehaviorStateCommit
        {
            OwnerKind = AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, OwnerIndex = 1, BehaviorIndex = 0,
            BehaviorKind = "Capacitor", CapacitorCharge = 100, CapacitorCapacity = 100,
            CapacitorEfficiency = 1
        }];
        var shield = new AetheriaRuntimeBehaviorPayload(0, "Shield", 0,
        [
            new AetheriaRuntimeBehaviorField(1, PerformanceStat(2)),
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(3))
        ]);
        var catalog = new AetheriaRuntimeCatalogSnapshot(
            [CatalogItem("test-energy-shield", shield), CatalogItem("test-shield-capacitor", CapacitorPayload(100, 1))], [], []);
        var zone = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [source, target] };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "energy-shield-smoke", CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0", Zones = [zone]
        };

        void AddDetonatingPayload(string id, double damage)
        {
            zone.PhysicalPayloads = [new AetheriaRuntimePhysicalPayloadCommit
            {
                PayloadId = id, PayloadKind = "mine", WeaponItemKey = "test-blast",
                SourceEntityIndex = source.EntityIndex, PositionX = target.PositionX,
                LifetimeSeconds = 30, BlastRadius = 10, PayloadMagnitude = damage,
                TriggeredAtSeconds = 0, DetonationDelaySeconds = 0, Stationary = true, Active = true
            }];
        }

        AddDetonatingPayload("shielded-blast", 20);
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 1, 0.1);
        var capacitor = target.BehaviorStates.Single(value => value.BehaviorKind == "Capacitor");
        RequireNear(100, Stat(target, "hull"), 0.000001,
            "funded shield must intercept damage before hull");
        RequireNear(40, capacitor.CapacitorCharge, 0.000001,
            "shield must consume damage multiplied by authored energy usage");
        RequireNear(40.0 / 3.0, Stat(target, "shield"), 0.000001,
            "legacy shield meter must be derived from remaining funded absorption, not own hit points");
        Require(run.GameEvents.Any(value => value.Kind == "shield.absorbed" &&
                Math.Abs(value.ScalarValue - 20) < 0.000001 &&
                Math.Abs(value.AuxiliaryValue - 10) < 0.000001),
            "shield outcome must publish absorbed damage and authored heat contribution");
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        Require(Flatten(surface.Surface.Root).Any(node => node.Kind == "feedback.event" &&
                node.Props["eventKind"] == "shield.absorbed" &&
                node.Props["scalarValue"] == "20" && node.Props["auxiliaryValue"] == "10"),
            "Eve feedback must expose exact shield absorption and heat facts");
        var shieldAsset = AetheriaRuntimeAssets.ProjectManifest(catalog).Assets.Single(value =>
            value.Ref.Metadata.TryGetValue("presentationRole", out var role) && role == "effect.impact.shield");
        RequireEqual("prefab.effect.impact.shield", shieldAsset.Ref.AssetKey,
            "provider must advertise the authored shield visual by semantic impact role");

        capacitor.CapacitorCharge = 0;
        AddDetonatingPayload("unfunded-blast", 20);
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 2, 0.2);
        RequireNear(80, Stat(target, "hull"), 0.000001,
            "unfunded shield must not manufacture absorption");
    }

    private static void ArmorCellsResolveBeforeEquipmentAndHull()
    {
        var source = Entity(0, -100, "player");
        var target = Entity(1, 0, "neutral");
        target.HullItemKey = "test-cell-hull";
        target.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            X = 0, Y = 1,
            Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-cell-gear", Quality = 1, Durability = 2, Enabled = true }
        }];
        var hull = HullCatalogItem("test-cell-hull", 3, 3, 5);
        var gear = CatalogItem("test-cell-gear");
        gear.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        var catalog = new AetheriaRuntimeCatalogSnapshot([hull, gear], [], []);
        var zone = new AetheriaRuntimeZoneSnapshotCommit
            { ZoneIndex = 0, Entities = [source, target], PhysicalPayloads = [new AetheriaRuntimePhysicalPayloadCommit
            {
                PayloadId = "armor-blast", PayloadKind = "mine", WeaponItemKey = "test-cannon",
                SourceEntityIndex = source.EntityIndex, PositionX = target.PositionX, PositionZ = target.PositionZ,
                LifetimeSeconds = 30, BlastRadius = 200, PayloadMagnitude = 20,
                DamageType = "Corrosive", Penetration = 0, DamageSpread = 0,
                TriggeredAtSeconds = 0, DetonationDelaySeconds = 0, Stationary = true, Active = true
            }] };
        var run = new AetheriaRuntimeRunCheckpointCommit
            { RunId = "armor-cell-smoke", CurrentZoneIndex = 0,
                CurrentEntityKey = "zone.0.entity.0", Zones = [zone] };

        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 1, 0.1);

        var armor = target.StatGrids.Single(value => value.Name == "armor");
        RequireNear(0, armor.Values[3], 0.000001, "source-facing splash cells must absorb damage first");
        RequireNear(1.0 / 3.0, target.Equipment.Single().Item.Durability, 0.000001,
            "installed equipment at the impact cell must receive residual damage second");
        RequireNear(100 - (10.0 / 3.0), Stat(target, "hull"), 0.000001,
            "only residual damage after armor and equipment may reach hull durability");
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        var targetNode = Flatten(surface.Surface.Root).Single(node =>
            node.Id == "aetheria.daemon.game.world.entity.1");
        RequireEqual("entity.presentation", targetNode.Kind,
            "Eve semantic entity facts must not become a second body-transform authority");
        RequireEqual("30", targetNode.Props["armor"],
            "Eve world projection must expose aggregate armor as derived presentation state");
        RequireEqual("0,5,5,0,5,5,0,5,5", targetNode.Props["armorGrid"],
            "Eve world projection must expose the exact daemon-owned schematic grid");
    }

    private static void DestructionDropsLootExactlyOnce()
    {
        var source = Entity(0, -100, "player");
        var target = Entity(1, 0, "raider");
        target.HullItemKey = "drop-hull";
        target.StatGrids = [Grid("hull", 1), Grid("shield", 0), Grid("heat", 0)];
        target.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "drop-gear", Quality = 0.8, Durability = 0.6, Quantity = 1, Enabled = true }
        }];
        target.CargoContents = [Cargo(("drop-cargo", 4, 0, 0))];
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0, Entities = [source, target],
            PhysicalPayloads = [new AetheriaRuntimePhysicalPayloadCommit
            {
                PayloadId = "destruction-mine", PayloadKind = "mine", WeaponItemKey = "destruction-charge",
                SourceEntityIndex = 0, PositionX = 0, PositionZ = 0, BlastRadius = 200,
                PayloadMagnitude = 20, TriggeredAtSeconds = 0, DetonationDelaySeconds = 0,
                LifetimeSeconds = 30, Active = true, Stationary = true
            }]
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "destruction-smoke", GenerationSeed = 29, CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0", Zones = [zone]
        };
        var settings = LootSettings(1);

        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1, settings,
            NewPhysics(), null, 1, 0.1);

        Require(!target.IsActive && target.DestroyedFrameId == 1 &&
                !string.IsNullOrWhiteSpace(target.DestructionId),
            "lethal daemon damage must commit one durable destruction identity and deactivate the entity");
        RequireEqual(2, zone.DroppedPickups.Count,
            "guaranteed fossil loot roll must drop non-hull equipment and every cargo stack");
        Require(!zone.DroppedPickups.Any(value => value.Item.ItemKey == "drop-hull"),
            "the equipped hull must remain excluded from fossil destruction loot");
        RequireEqual(4, zone.DroppedPickups.Single(value => value.Item.ItemKey == "drop-cargo").Item.Quantity,
            "cargo drops must preserve exact stack quantity");
        Require(zone.DroppedPickups.All(value => Math.Abs(Math.Sqrt(
                    value.VelocityX * value.VelocityX + value.VelocityY * value.VelocityY +
                    value.VelocityZ * value.VelocityZ) - 25) < 0.000001 &&
                Math.Abs(value.LifetimeSeconds - 30) < 0.000001),
            "destruction pickups must retain fossil launch speed and lifetime");
        RequireEqual(2, run.GameEvents.Count(value => value.Kind == "pickup.dropped"),
            "each durable pickup must publish one drop event");
        RequireEqual(1, run.GameEvents.Count(value => value.Kind == "entity.destroyed"),
            "destruction chronology must publish exactly once");

        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        Require(!Flatten(surface.Surface.Root).Any(node => node.Id == "aetheria.daemon.game.world.entity.1"),
            "Eve must not project a destroyed tombstone as an active world entity");
        var destructionAsset = AetheriaRuntimeAssets.ProjectManifest(null).Assets.Single(value =>
            value.Ref.Metadata.TryGetValue("presentationRole", out var role) &&
            role == "effect.feedback.entity.destroyed");
        RequireEqual("prefab.effect.entity.destroyed", destructionAsset.Ref.AssetKey,
            "provider must advertise the original destruction effect by semantic feedback role");

        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1, settings,
            NewPhysics(), null, 2, 0.2);
        RequireEqual(2, zone.DroppedPickups.Count,
            "later ticks must not duplicate an already committed destruction transaction");
        RequireEqual(1, run.GameEvents.Count(value => value.Kind == "entity.destroyed"),
            "later ticks must not duplicate destruction chronology");
    }

    private static void ChargedWeaponHoldRiskMalfunctionsDeterministically()
    {
        var source = Entity(0, 0, "player");
        source.WeaponGroups = [new[] { 0 }];
        var item = new AetheriaRuntimeLoadoutItemCommit
            { ItemKey = "risk-charger", Quality = 1, Durability = 1, Enabled = true };
        source.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit { Item = item }];
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "charged-risk-smoke", GenerationSeed = 17,
            CurrentZoneIndex = 0, CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [source] }]
        };
        var payload = new AetheriaRuntimeBehaviorPayload(0, AetheriaRuntimeBehaviorKinds.ChargedWeapon, 0,
        [
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(20)),
            new AetheriaRuntimeBehaviorField(6, PerformanceStat(150)),
            new AetheriaRuntimeBehaviorField(17, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(19, PerformanceStat(0.5)),
            new AetheriaRuntimeBehaviorField(21, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(25, Number(1)),
            new AetheriaRuntimeBehaviorField(26, Number(0.25))
        ]);
        var catalog = new AetheriaRuntimeCatalogSnapshot([CatalogItem(item.ItemKey, payload)], [], []);
        for (var frame = 0; frame < 21; frame++)
        {
            var intents = new AetheriaRuntimeDaemonIntentState();
            if (frame == 0) intents.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
                { ActorEntityKey = "zone.0.entity.0", WeaponGroup = 0, Fire = true, Active = true });
            AetheriaRuntimeDaemonSimulation.Step(run, intents, 0.1,
                new AetheriaRuntimeDaemonSimulationSettings(),
                NewPhysics(), catalog, frame, frame * 0.1);
        }
        var state = source.WeaponStates.Single(value => value.BehaviorKind == AetheriaRuntimeBehaviorKinds.ChargedWeapon);
        Require(!state.Charging && !state.Charged && state.CoolingDown && state.ChargeRiskChecks == 1 &&
                Math.Abs(state.ChargeMalfunctionRisk - 1) < 0.000001,
            "held full charge must persist its risk check and fail when authored risk reaches certainty");
        RequireNear(0.75, item.Durability, 0.000001,
            "charged malfunction must damage canonical equipped-item durability");
        Require(run.GameEvents.Count(value => value.Kind == "weapon.charge.malfunctioned" &&
                value.SubjectKey == "hold-risk" && value.ItemKey == item.ItemKey) == 1,
            "charged hold risk must emit one authoritative malfunction event");
        Require(!run.GameEvents.Any(value => value.Kind == "shot.committed"),
            "malfunction without a firing solution must never leak a projectile");
    }

    private static void AgentTowsStationIntoPersistentOrbit()
    {
        var tug = Entity(0, 0, "workers");
        var station = Entity(1, 0, "workers");
        station.Kind = "station";
        tug.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Tow];
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0, Entities = [tug, station],
            Orbits = [new AetheriaRuntimeOrbitSnapshotCommit { OrbitKey = "parent-orbit", FixedPositionX = 20 }],
            Bodies = [new AetheriaRuntimeBodySnapshotCommit { BodyKey = "parent-body", Kind = "planet", OrbitKey = "parent-orbit" }]
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            CurrentZoneIndex = 0, CurrentEntityKey = "", Zones = [zone],
            AgentTasks = [new AetheriaRuntimeAgentTaskCommit { TaskId = "tow-1", CorporationKey = "workers", TaskType = AetheriaRuntimeAgentTaskTypes.Tow, ZoneIndex = 0, Status = AetheriaRuntimeAgentTaskStatuses.Queued, TargetEntityIndex = 1, OrbitParentKey = "parent-body", OrbitDistance = 20, CompletionRadius = 5 }]
        };
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-tow-attach-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = NewPhysics(), FrameId = 1, FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 1, BuildPublications = false });
        Require(run.Zones[0].Entities[0].ChildEntityIndices.Contains(1), "tow pickup must attach station to tug parentage");
        RequireEqual("", run.Zones[0].Entities[1].OrbitKey, "attached station must no longer own an orbit");

        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-tow-detach-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = NewPhysics(), FrameId = 2, FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 2, BuildPublications = false });
        Require(!run.Zones[0].Entities[0].ChildEntityIndices.Contains(1), "tow delivery must detach station parentage");
        Require(!string.IsNullOrWhiteSpace(run.Zones[0].Entities[1].OrbitKey), "delivered station must own a persistent orbit");
        Require(run.Zones[0].Orbits.Any(orbit => orbit.OrbitKey == run.Zones[0].Entities[1].OrbitKey && orbit.ParentOrbitKey == "parent-orbit" && Math.Abs(orbit.Distance - 20) < 0.001),
            "delivered orbit must preserve requested parent and radius");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Completed, run.AgentTasks[0].Status, "tow task must complete only after detach applies");
    }

    private static void AgentSurveysBodyIntoCorporationKnowledge()
    {
        var surveyor = Entity(0, 0, "workers");
        surveyor.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        surveyor.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = "scanner", Enabled = true } }];
        var scanner = CatalogItem("scanner", new AetheriaRuntimeBehaviorPayload(0, "ResourceScanner", 0,
            [new AetheriaRuntimeBehaviorField(1, PerformanceStat(100)), new AetheriaRuntimeBehaviorField(2, PerformanceStat(4)), new AetheriaRuntimeBehaviorField(3, PerformanceStat(0.5))]));
        var catalog = new AetheriaRuntimeCatalogSnapshot([scanner], [], []);
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            CurrentZoneIndex = 0, CurrentEntityKey = "",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit
            {
                ZoneIndex = 0, Entities = [surveyor],
                Orbits = [new AetheriaRuntimeOrbitSnapshotCommit { OrbitKey = "survey-orbit", FixedPositionX = 10 }],
                Bodies = [new AetheriaRuntimeBodySnapshotCommit { BodyKey = "survey-world", Kind = "planet", OrbitKey = "survey-orbit" }]
            }],
            AgentTasks = [new AetheriaRuntimeAgentTaskCommit { TaskId = "survey-1", CorporationKey = "workers", TaskType = AetheriaRuntimeAgentTaskTypes.Explore, ZoneIndex = 0, Status = AetheriaRuntimeAgentTaskStatuses.Queued, TargetBodyKeys = ["survey-world"] }]
        };
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-survey-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = NewPhysics(), FrameId = 1, FixedDeltaSeconds = 1, SimulationTimeSeconds = 1, Catalog = catalog, BuildPublications = false });
        var knowledge = run.CorporationSurveys.Single();
        RequireEqual("workers", knowledge.CorporationKey, "survey knowledge must belong to the agent corporation");
        RequireEqual("survey-world", knowledge.BodyKey, "survey knowledge must identify the scanned body");
        RequireNear(4, knowledge.DensityFloor, 0.000001, "survey must publish the scanner minimum density");

        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-survey-complete-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = NewPhysics(), FrameId = 2, FixedDeltaSeconds = 1, SimulationTimeSeconds = 2, Catalog = catalog, BuildPublications = false });
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Completed, run.AgentTasks[0].Status,
            "survey order must complete when corporation knowledge satisfies the scanner threshold");
    }

    private static void CargoCapacityComesFromHullAndCatalogVolumes()
    {
        var hull = CatalogItem("hauler-hull");
        hull.HullCapacity = PerformanceStat(20);
        var gear = CatalogItem("scanner");
        gear.Volume = 3;
        var cargoBay = CatalogItem("hauler-cargo");
        cargoBay.Volume = 0;
        cargoBay.InteriorOccupiedCells = 16;
        var ore = CatalogItem("ore");
        ore.Volume = 2;
        var catalog = new AetheriaRuntimeCatalogSnapshot([hull, gear, cargoBay, ore], [], []);
        var entity = Entity(0, 0, "workers");
        entity.HullItemKey = hull.ItemKey;
        entity.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = gear.ItemKey } },
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = cargoBay.ItemKey } }
        ];
        entity.CargoContents = [new AetheriaRuntimeCargoBayLoadoutCommit { Items = [new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = ore.ItemKey, Quantity = 4 } }] }];

        RequireNear(20, AetheriaRuntimeCargoCapacityQueries.Capacity(entity, catalog), 0.000001,
            "cargo capacity must evaluate the authored hull performance stat");
        RequireNear(11, AetheriaRuntimeCargoCapacityQueries.Occupied(entity, catalog), 0.000001,
            "equipment and stacked cargo must consume catalog volume");
        RequireEqual(4, AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(entity, catalog, ore.ItemKey),
            "the installed cargo bay's remaining volume must determine whole units that fit");
    }

    private static void AgentMinesAsteroidThroughEquippedBehavior()
    {
        var miner = Entity(0, 0, "workers");
        var home = Entity(1, 0, "workers");
        miner.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Mine];
        miner.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = "mining-tool", Enabled = true, Quality = 1, Durability = 1 } },
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = "miner-cargo", Enabled = true, Quality = 1, Durability = 1 } }
        ];
        home.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = "home-cargo", Enabled = true, Quality = 1, Durability = 1 } }
        ];
        var minerHull = CatalogItem("miner-hull");
        minerHull.HullCapacity = PerformanceStat(3);
        var homeHull = CatalogItem("home-hull");
        homeHull.HullCapacity = PerformanceStat(100);
        var iron = CatalogItem("iron");
        iron.SimpleCommodityCategory = "ore";
        iron.Volume = 1;
        var minerCargo = CatalogItem("miner-cargo");
        minerCargo.InteriorOccupiedCells = 2;
        var homeCargo = CatalogItem("home-cargo");
        homeCargo.InteriorOccupiedCells = 100;
        miner.HullItemKey = minerHull.ItemKey;
        home.HullItemKey = homeHull.ItemKey;
        miner.CargoContents = [new AetheriaRuntimeCargoBayLoadoutCommit()];
        home.CargoContents = [new AetheriaRuntimeCargoBayLoadoutCommit()];
        var catalog = new AetheriaRuntimeCatalogSnapshot(
            [minerHull, homeHull, iron, minerCargo, homeCargo, CatalogItem("mining-tool", new AetheriaRuntimeBehaviorPayload(0, "MiningTool", 0,
                [new AetheriaRuntimeBehaviorField(1, PerformanceStat(1000)), new AetheriaRuntimeBehaviorField(2, PerformanceStat(1000000000)), new AetheriaRuntimeBehaviorField(3, PerformanceStat(2)), new AetheriaRuntimeBehaviorField(4, PerformanceStat(1000))]))],
            [], []);
        var asteroid = new AetheriaRuntimeAsteroidCommit { Distance = 0, Size = 6 };
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities = [miner, home],
            Bodies = [new AetheriaRuntimeBodySnapshotCommit { BodyKey = "belt", Kind = "asteroid_belt", Asteroids = [asteroid], Resources = [new AetheriaRuntimeBodyResourceCommit { ItemKey = "iron", Amount = 1 }] }]
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            GenerationSeed = 7,
            CurrentZoneIndex = 0,
            CurrentEntityKey = "",
            Zones = [zone],
            AgentTasks = [new AetheriaRuntimeAgentTaskCommit { TaskId = "mine-1", CorporationKey = "workers", TaskType = AetheriaRuntimeAgentTaskTypes.Mine, ZoneIndex = 0, OriginEntityIndex = 1, CompletionRadius = 10, Status = AetheriaRuntimeAgentTaskStatuses.Queued, TargetBodyKeys = ["belt"] }]
        };

        var result = AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-mining-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = NewPhysics(), FrameId = 1, FixedDeltaSeconds = 1, SimulationTimeSeconds = 1, Catalog = catalog, BuildPublications = false });

        Require(run.AgentTasks[0].Phase == "mining", "mining task must activate its equipped tool");
        Require(result.OperationResult.Intents.Behaviors.Count == 1,
            $"mining behavior command must become daemon intent (count={result.OperationResult.Intents.Behaviors.Count}, applied={string.Join(',', result.Frame.AppliedCommandIds)}, rejected={string.Join(',', result.Frame.RejectedCommandIds)})");
        var committedMiner = run.Zones[0].Entities.Single(entity => entity.EntityIndex == 0);
        var committedAsteroid = run.Zones[0].Bodies[0].Asteroids[0];
        Require(committedAsteroid.RespawnTimer > 0, $"mining damage must deplete the asteroid and start historical respawn (damage={committedAsteroid.Damage}, respawn={committedAsteroid.RespawnTimer})");
        Require(committedMiner.CargoContents.SelectMany(bay => bay.Items).Any(slot => slot.Item.ItemKey == "iron" && slot.Item.Quantity > 0),
            "historical mining yield must enter daemon-owned cargo");

        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-mining-offload-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = NewPhysics(), FrameId = 2, FixedDeltaSeconds = 1, SimulationTimeSeconds = 2, Catalog = catalog, BuildPublications = false });
        Require(!run.Zones[0].Entities[0].CargoContents.SelectMany(bay => bay.Items).Any(),
            "full miner must offload through the shared cargo transfer command");
        Require(run.Zones[0].Entities[1].CargoContents.SelectMany(bay => bay.Items).Any(slot => slot.Item.ItemKey == "iron" && slot.Item.Quantity == 1),
            "home storage must receive the mined commodity");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Assigned, run.AgentTasks[0].Status,
            "miner must remain assigned after a successful offload");
    }

    private static void TickReconcilesAndEvaluatesCatalogBehaviors()
    {
        var entity = Entity(0, 0, "workers");
        entity.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit
            {
                Item = new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemKey = "mining-tool",
                    Quality = 1,
                    Durability = 1,
                    Enabled = true
                }
            }
        ];
        entity.BehaviorStates = Array.Empty<AetheriaRuntimeBehaviorStateCommit>();
        var payload = new AetheriaRuntimeBehaviorPayload(
            0,
            "MiningTool",
            0,
            [
                new AetheriaRuntimeBehaviorField(1, PerformanceStat(12)),
                new AetheriaRuntimeBehaviorField(2, PerformanceStat(0.8)),
                new AetheriaRuntimeBehaviorField(3, PerformanceStat(2)),
                new AetheriaRuntimeBehaviorField(4, PerformanceStat(50))
            ]);
        var catalog = new AetheriaRuntimeCatalogSnapshot(
            [CatalogItem("mining-tool", payload)],
            Array.Empty<AetheriaRuntimeCorporation>(),
            Array.Empty<AetheriaRuntimeNameFile>());
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "behavior-query-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [entity] }]
        };

        AetheriaRuntimeDaemonTickRunner.Tick(
            Path.Combine(Path.GetTempPath(), "aetheria-behavior-query-smoke.cc"),
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                WorldPhysics = NewPhysics(),
                FrameId = 1,
                FixedDeltaSeconds = 0,
                Catalog = catalog,
                BuildPublications = false
            });

        var behavior = AetheriaRuntimeEquippedBehaviorQueries.Find(entity, catalog, "MiningTool").Single();
        RequireEqual("MiningTool", behavior.State.BehaviorKind,
            "tick must reconcile equipped catalog payloads into persistent behavior state");
        RequireNear(12, behavior.EvaluateStat(1), 0.000001, "behavior query must evaluate mining damage from catalog");
        RequireNear(50, behavior.EvaluateStat(4), 0.000001, "behavior query must evaluate mining range from catalog");
    }

    private static void DaemonLoadoutsRespectFactionAvailabilityAndHullRoles()
    {
        var cells4 = Enumerable.Range(0, 4).SelectMany(y => Enumerable.Range(0, 4)
            .Select(x => new AetheriaRuntimeShapeCell(x, y))).ToArray();
        var one = new[] { new AetheriaRuntimeShapeCell(0, 0) };
        var cells2 = Enumerable.Range(0, 2).SelectMany(y => Enumerable.Range(0, 2)
            .Select(x => new AetheriaRuntimeShapeCell(x, y))).ToArray();

        AetheriaRuntimeCatalogItem Item(string key, string category, string manufacturer, int price,
            string hardpointType = "", params string[] behaviors)
        {
            var item = CatalogItem(key, behaviors.Select((kind, index) =>
                new AetheriaRuntimeBehaviorPayload(index, kind, 0, Array.Empty<AetheriaRuntimeBehaviorField>())).ToArray());
            item.Category = category; item.ManufacturerKey = manufacturer; item.Price = price;
            item.HardpointType = hardpointType; item.ShapeWidth = 1; item.ShapeHeight = 1;
            item.OccupiedCells = 1; item.ShapeCells = one; item.BehaviorKinds = behaviors;
            return item;
        }

        var availableHull = Item("available-hull", AetheriaRuntimeItemCategories.Hull, "forge", 100);
        availableHull.HardpointType = "Hull";
        availableHull.HullType = "Ship"; availableHull.ShapeWidth = 4; availableHull.ShapeHeight = 4;
        availableHull.OccupiedCells = 16; availableHull.ShapeCells = cells4;
        availableHull.Hardpoints =
        [
            new AetheriaRuntimeHardpoint("ControlModule", 0, 0, 1, 1, 1, one, "", "None", 0),
            new AetheriaRuntimeHardpoint("Weapon", 1, 0, 1, 1, 1, one, "", "Clockwise", 0),
            new AetheriaRuntimeHardpoint("Sensors", 2, 0, 1, 1, 1, one, "", "None", 0)
        ];
        var unavailableHull = Item("cheap-foreign-hull", AetheriaRuntimeItemCategories.Hull, "foreign", 1);
        unavailableHull.HardpointType = "Hull";
        unavailableHull.HullType = "Ship"; unavailableHull.ShapeWidth = 4; unavailableHull.ShapeHeight = 4;
        unavailableHull.OccupiedCells = 16; unavailableHull.ShapeCells = cells4;
        var stationHull = Item("station-hull", AetheriaRuntimeItemCategories.Hull, "forge", 120);
        stationHull.HardpointType = "Hull"; stationHull.HullType = "Station";
        stationHull.ShapeWidth = 4; stationHull.ShapeHeight = 4; stationHull.OccupiedCells = 16;
        stationHull.ShapeCells = cells4;
        stationHull.Hardpoints =
        [
            new AetheriaRuntimeHardpoint("ControlModule", 0, 0, 1, 1, 1, one, "", "None", 0),
            new AetheriaRuntimeHardpoint("Sensors", 1, 1, 2, 2, 4, cells2, "", "None", 0)
        ];
        var cockpit = Item("cockpit", AetheriaRuntimeItemCategories.Gear, "forge", 20, "ControlModule", "Cockpit");
        var wrongController = Item("cheap-turret-controller", AetheriaRuntimeItemCategories.Gear, "forge", 1,
            "ControlModule", "TurretController");
        var weapon = Item("cannon", AetheriaRuntimeItemCategories.Weapon, "forge", 40, "Weapon", "LockWeapon");
        var shipSensor = Item("ship-sensor", AetheriaRuntimeItemCategories.Gear, "forge", 15, "Sensors", "Sensor");
        var stationSensor = Item("station-sensor", AetheriaRuntimeItemCategories.Gear, "forge", 80, "Sensors", "Sensor");
        stationSensor.ShapeWidth = 2; stationSensor.ShapeHeight = 2;
        stationSensor.OccupiedCells = 4; stationSensor.ShapeCells = cells2;
        var cargo = Item("cargo-bay", AetheriaRuntimeItemCategories.CargoBay, "forge", 30);
        cargo.HardpointType = "Internal";
        cargo.InteriorShapeWidth = 3; cargo.InteriorShapeHeight = 3; cargo.InteriorOccupiedCells = 9;
        cargo.InteriorShapeCells = Enumerable.Range(0, 3).SelectMany(y => Enumerable.Range(0, 3)
            .Select(x => new AetheriaRuntimeShapeCell(x, y))).ToArray();
        var docking = Item("docking-bay", AetheriaRuntimeItemCategories.DockingBay, "forge", 35);
        docking.HardpointType = "Internal";
        var capacitor = Item("capacitor", AetheriaRuntimeItemCategories.Gear, "forge", 25, "", "Capacitor");
        capacitor.HardpointType = "Internal";
        var faction = new AetheriaRuntimeCorporation("forge", "Forge", "F", "", "", "", 1, 1,
            [new AetheriaRuntimeCorporationAllegiance("forge", 1)]);
        var foreign = new AetheriaRuntimeCorporation("foreign", "Foreign", "X", "", "", "", 1, 2,
            [new AetheriaRuntimeCorporationAllegiance("foreign", 1), new AetheriaRuntimeCorporationAllegiance("forge", 0.5)]);
        var reactor = Item("reactor", AetheriaRuntimeItemCategories.Gear, "forge", 25, "", "Reactor");
        reactor.HardpointType = "Internal";
        var catalog = new AetheriaRuntimeCatalogSnapshot(
            [availableHull, unavailableHull, stationHull, cockpit, wrongController, weapon, shipSensor, stationSensor, cargo, docking, capacitor, reactor],
            [faction, foreign], Array.Empty<AetheriaRuntimeNameFile>());
        var fallbackCatalog = new AetheriaRuntimeCatalogSnapshot(
            [availableHull, unavailableHull, stationHull, cockpit, wrongController, weapon, shipSensor, cargo, docking, capacitor, reactor],
            [faction, foreign], Array.Empty<AetheriaRuntimeNameFile>());
        var homes = new Dictionary<string, int> { ["forge"] = 0, ["foreign"] = 1 };
        var adjacency = new Dictionary<int, IReadOnlyList<int>> { [0] = [1], [1] = [0] };

        var rareHull = Item("rare-hull", AetheriaRuntimeItemCategories.Hull, "forge", 100);
        rareHull.HardpointType = availableHull.HardpointType;
        rareHull.HullType = availableHull.HullType;
        rareHull.ShapeWidth = availableHull.ShapeWidth;
        rareHull.ShapeHeight = availableHull.ShapeHeight;
        rareHull.OccupiedCells = availableHull.OccupiedCells;
        rareHull.ShapeCells = availableHull.ShapeCells;
        rareHull.Hardpoints = availableHull.Hardpoints;
        rareHull.Tags = ["rarity:rare"];
        var rarityCatalog = new AetheriaRuntimeCatalogSnapshot(
            [availableHull, rareHull, cockpit, wrongController, weapon, shipSensor, cargo, capacitor, reactor],
            [faction], Array.Empty<AetheriaRuntimeNameFile>());

        var first = new AetheriaDaemonLoadoutGenerator(catalog, 42, 0, homes, adjacency).Build("ship", "forge");
        var second = new AetheriaDaemonLoadoutGenerator(catalog, 42, 0, homes, adjacency).Build("ship", "forge");
        var station = new AetheriaDaemonLoadoutGenerator(catalog, 84, 0, homes, adjacency).Build("station", "forge");
        var fallbackStation = new AetheriaDaemonLoadoutGenerator(fallbackCatalog, 84, 0, homes, adjacency)
            .Build("station", "forge");
        var uninterruptedForge = new AetheriaDaemonLoadoutGenerator(catalog, 101, 0, homes, adjacency);
        var interleavedForge = new AetheriaDaemonLoadoutGenerator(catalog, 101, 0, homes, adjacency);
        var foreignStream = new AetheriaDaemonLoadoutGenerator(catalog, 202, 1, homes, adjacency);
        var uninterruptedSequence = new[]
        {
            uninterruptedForge.Build("ship", "forge"),
            uninterruptedForge.Build("ship", "forge")
        };
        var interleavedFirst = interleavedForge.Build("ship", "forge");
        foreignStream.Build("ship", "foreign");
        var interleavedSecond = interleavedForge.Build("ship", "forge");
        var rarityGenerator = new AetheriaDaemonLoadoutGenerator(
            rarityCatalog, 9001, 0, homes, adjacency);
        var rareSelections = Enumerable.Range(0, 2000)
            .Select(_ => rarityGenerator.Build("ship", "forge").HullItemKey)
            .Count(key => key == "rare-hull");

        RequireEqual("available-hull", first.HullItemKey,
            "loadout generation must exclude manufacturers outside the faction allegiance graph");
        Require(first.Equipment.Any(value => value.ItemKey == "cockpit") &&
                first.Equipment.All(value => value.ItemKey != "cheap-turret-controller"),
            "ship control hardpoints must require the cockpit role even when the wrong controller is cheaper");
        Require(first.Equipment.Any(value => value.ItemKey == "cannon") &&
                first.Equipment.Any(value => value.ItemKey == "cargo-bay") &&
                first.Equipment.Any(value => value.ItemKey == "capacitor") &&
                first.Equipment.Any(value => value.ItemKey == "ship-sensor") &&
                first.Equipment.All(value => value.ItemKey != "station-sensor"),
            "generated ships must fit hardpoint equipment and reject station-sized sensor gear");
        RequireEqual("Clockwise", first.Equipment.Single(value => value.ItemKey == "cannon").Rotation,
            "generated equipment must preserve the placement rotation used by hardpoint fitting");
        Require(first.HullItemKey == second.HullItemKey &&
                first.Equipment.Select(value => value.ItemKey).SequenceEqual(second.Equipment.Select(value => value.ItemKey)),
            "same seed, map, faction and catalog must produce the same loadout");
        Require(first.Cargo.Length == 0 && first.Receipt.Selections.All(value => value.Role != "cargo"),
            "ordinary generated ships must not acquire cargo from non-canonical scenario aliases");
        Require(first.Receipt.Seed == 42 && first.Receipt.SourceZoneIndex == 0 &&
                first.Receipt.AvailabilityFactionKey == "forge" &&
                first.Receipt.Selections.Any(value => value.Role == "hull" && value.ItemKey == "available-hull" &&
                    value.ManufacturerKey == "forge" && value.ManufacturerDistance == 1 && value.Allegiance == 1),
            "generated loadouts must carry daemon-authored source and selection provenance");
        Require(rareSelections > 0 && rareSelections < 200,
            $"rare catalog candidates must remain possible without becoming a default loadout path (selected {rareSelections}/2000)");
        Require(station.Equipment.Any(value => value.ItemKey == "docking-bay") &&
                station.Equipment.Any(value => value.ItemKey == "cargo-bay") &&
                station.Equipment.Any(value => value.ItemKey == "capacitor") &&
                station.Equipment.Any(value => value.ItemKey == "cheap-turret-controller") &&
                station.Equipment.Any(value => value.ItemKey == "station-sensor") &&
                station.Equipment.All(value => value.ItemKey != "ship-sensor"),
            "station generation must fill a large sensor hardpoint with the largest available compatible array");
        Require(fallbackStation.Equipment.Any(value => value.ItemKey == "ship-sensor"),
            "a smaller sensor must remain a valid fallback when it fits inside a larger same-type hardpoint");
        Require(station.Cargo.Length > 0 && station.Cargo.Length <= cargo.InteriorOccupiedCells &&
                station.Cargo.All(value => value.Item.ItemKey != "cheap-foreign-hull" &&
                    catalog.FindItem(value.Item.ItemKey) != null) &&
                station.Receipt.Selections.Where(value => value.Role == "cargo")
                    .Select(value => value.ItemKey)
                    .SequenceEqual(station.Cargo.Select(value => value.Item.ItemKey)),
            "station inventory draws must use canonical catalog keys, respect cargo capacity, exclude unavailable manufacturers, and preserve exact provenance");
        Require(LoadoutKeys(uninterruptedSequence[0]).SequenceEqual(LoadoutKeys(interleavedFirst)) &&
                LoadoutKeys(uninterruptedSequence[1]).SequenceEqual(LoadoutKeys(interleavedSecond)),
            "generation in another faction stream must not perturb this faction's continuing loadout sequence");
    }

    private static IEnumerable<string> LoadoutKeys(AetheriaDaemonLoadout loadout) =>
        new[] { loadout.HullItemKey }
            .Concat(loadout.Equipment.Select(value => value.ItemKey))
            .Concat(loadout.Cargo.Select(value => value.Item.ItemKey));

    private static AetheriaRuntimeBehaviorValue PerformanceStat(double value) => new(
        "performance-stat",
        "",
        0,
        false,
        "",
        "",
        [Number(value), Number(value), Number(0), Number(0), Number(0)],
        Array.Empty<AetheriaRuntimeBehaviorMapEntry>());

    private static AetheriaRuntimeBehaviorValue Number(double value) => new(
        "number",
        "",
        value,
        false,
        "",
        "",
        Array.Empty<AetheriaRuntimeBehaviorValue>(),
        Array.Empty<AetheriaRuntimeBehaviorMapEntry>());

    private static AetheriaRuntimeBehaviorValue Vector(params double[] values) => new(
        "float3",
        "",
        0,
        false,
        "",
        "",
        values.Select(Number).ToArray(),
        Array.Empty<AetheriaRuntimeBehaviorMapEntry>());

    private static AetheriaRuntimeBehaviorValue Curve(params AetheriaRuntimeCurveKey[] keys) => new(
        "bezier-curve",
        "",
        0,
        false,
        "",
        "",
        [new AetheriaRuntimeBehaviorValue(
            "array",
            "",
            0,
            false,
            "",
            "",
            keys.Select(key => Vector(key.Time, key.Value, key.InTangent, key.OutTangent)).ToArray(),
            Array.Empty<AetheriaRuntimeBehaviorMapEntry>())],
        Array.Empty<AetheriaRuntimeBehaviorMapEntry>());

    private static AetheriaRuntimeBehaviorPayload AetherDrivePayload(
        double couplingEfficiency = 1,
        double energyDraw = 0) => new(
        0,
        AetheriaRuntimeBehaviorKinds.AetherDrive,
        0,
        [
            new AetheriaRuntimeBehaviorField(1, Vector(100, 100, 100)),
            new AetheriaRuntimeBehaviorField(2, Vector(10, 10, 0.1)),
            new AetheriaRuntimeBehaviorField(3, PerformanceStat(100)),
            new AetheriaRuntimeBehaviorField(4, Vector(10, 10, 10)),
            new AetheriaRuntimeBehaviorField(5, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(6, PerformanceStat(couplingEfficiency)),
            new AetheriaRuntimeBehaviorField(7, PerformanceStat(10000)),
            new AetheriaRuntimeBehaviorField(8, Curve(
                new AetheriaRuntimeCurveKey(0, 1, 0, 0),
                new AetheriaRuntimeCurveKey(1, 1, 0, 0))),
            new AetheriaRuntimeBehaviorField(9, PerformanceStat(energyDraw)),
            new AetheriaRuntimeBehaviorField(10, PerformanceStat(0))
        ]);

    private static AetheriaRuntimeBehaviorPayload ThrusterPayload() => new(
        0,
        AetheriaRuntimeBehaviorKinds.Thruster,
        0,
        [
            new AetheriaRuntimeBehaviorField(1, PerformanceStat(90)),
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(2)),
            new AetheriaRuntimeBehaviorField(3, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(4, PerformanceStat(0))
        ]);

    private static AetheriaRuntimeCatalogSnapshot EquipThrusterBank(
        IEnumerable<AetheriaRuntimeEntitySnapshotCommit> entities,
        params AetheriaRuntimeCatalogItem[] additionalItems)
    {
        var hull = HullCatalogItem("smoke-standard-thruster-hull", 5, 5, 0);
        hull.HullDrag = 1;
        var thruster = CatalogItem("smoke-directional-thruster", ThrusterPayload());
        var placements = new[]
        {
            (0, 2, "None"), (4, 2, "None"),
            (0, 1, "Half"), (4, 1, "Half"),
            (2, 0, "CounterClockwise"), (2, 4, "CounterClockwise"),
            (1, 0, "Clockwise"), (1, 4, "Clockwise")
        };
        foreach (var entity in entities.Where(entity => entity.Kind == "ship"))
        {
            if (string.IsNullOrWhiteSpace(entity.HullItemKey))
                entity.HullItemKey = hull.ItemKey;
            entity.Equipment = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Concat(placements.Select(value => new AetheriaRuntimeLoadoutItemSlotCommit
                {
                    X = value.Item1,
                    Y = value.Item2,
                    Rotation = value.Item3,
                    Item = new AetheriaRuntimeLoadoutItemCommit
                    {
                        ItemKey = thruster.ItemKey,
                        Quantity = 1,
                        Durability = 1,
                        Enabled = true
                    }
                }))
                .ToArray();
        }
        return new AetheriaRuntimeCatalogSnapshot(
            additionalItems.Concat([hull, thruster]).ToArray(), [], []);
    }

    private static AetheriaRuntimeCatalogSnapshot EquipAetherDrive(
        IEnumerable<AetheriaRuntimeEntitySnapshotCommit> entities,
        params AetheriaRuntimeCatalogItem[] additionalItems)
    {
        var hull = HullCatalogItem("smoke-rare-aether-drive-hull", 3, 3, 0);
        hull.Hardpoints =
        [
            new AetheriaRuntimeHardpoint(
                "AetherDrive", 1, 1, 1, 1, 1,
                [new AetheriaRuntimeShapeCell(0, 0)], "", "None", 0)
        ];
        var drive = CatalogItem("smoke-aether-drive", AetherDrivePayload());
        drive.HardpointType = "AetherDrive";
        foreach (var entity in entities.Where(entity => entity.Kind == "ship"))
        {
            if (string.IsNullOrWhiteSpace(entity.HullItemKey))
                entity.HullItemKey = hull.ItemKey;
            entity.Equipment = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Concat([new AetheriaRuntimeLoadoutItemSlotCommit
                {
                    X = 1,
                    Y = 1,
                    Rotation = "None",
                    Item = new AetheriaRuntimeLoadoutItemCommit
                    {
                        ItemKey = drive.ItemKey,
                        Quantity = 1,
                        Durability = 1,
                        Enabled = true
                    }
                }])
                .ToArray();
        }
        return new AetheriaRuntimeCatalogSnapshot(
            additionalItems.Concat([hull, drive]).ToArray(), [], []);
    }

    private static AetheriaRuntimeBehaviorPayload CapacitorPayload(double capacity, double efficiency) => new(
        0, "Capacitor", 0,
        [
            new AetheriaRuntimeBehaviorField(1, PerformanceStat(capacity)),
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(efficiency))
        ]);

    private static AetheriaRuntimeCatalogItem CatalogItem(string itemKey, params AetheriaRuntimeBehaviorPayload[] payloads) => new(
        itemKey, itemKey, "equipment", "", "", 0, 1, 1, 1, 1,
        1, 1, 1, Array.Empty<AetheriaRuntimeShapeCell>(),
        0, 0, 0, Array.Empty<AetheriaRuntimeShapeCell>(),
        Array.Empty<AetheriaRuntimeHardpoint>(), payloads,
        "utility", "", payloads.Select(payload => payload.Kind).ToArray(),
        1, false, 0, 1, "", "", "", "", "",
        0, 1000, Array.Empty<AetheriaRuntimeCurveKey>(), "", 1, 0, 0, 0, false,
        0, 0, "", Array.Empty<AetheriaRuntimeAudioStat>(), Array.Empty<AetheriaRuntimeCurveKey>(), "", "");

    private static AetheriaRuntimeCatalogItem HullCatalogItem(string itemKey, int width, int height, double armor)
    {
        var cells = Enumerable.Range(0, height)
            .SelectMany(y => Enumerable.Range(0, width).Select(x => new AetheriaRuntimeShapeCell(x, y)))
            .ToArray();
        return new AetheriaRuntimeCatalogItem(
            itemKey, itemKey, "hull", "", "", 0, 1, 1, 1, 1,
            width, height, cells.Length, cells,
            width, height, cells.Length, cells,
            Array.Empty<AetheriaRuntimeHardpoint>(), Array.Empty<AetheriaRuntimeBehaviorPayload>(),
            "", "ship", Array.Empty<string>(),
            1, false, 0, 1, "", "", "", "", "",
            0, 1000, Array.Empty<AetheriaRuntimeCurveKey>(), "", 1, 0, armor, 0, false,
            0, 0, "", Array.Empty<AetheriaRuntimeAudioStat>(), Array.Empty<AetheriaRuntimeCurveKey>(), "", "");
    }

    private static AetheriaRuntimeDaemonSimulationSettings LootSettings(double probability)
    {
        var value = AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault;
        return new AetheriaRuntimeDaemonSimulationSettings(
            value.PawnSpeed, value.RaiderSpeed, value.AttackRange, value.AttackHoldRatio,
            value.PawnProjectileDamage, value.RaiderProjectileDamage, value.WeaponCooldownSeconds,
            value.ProjectileSpeed, value.ProjectileRadius, value.ProjectileLifetimeSeconds,
            value.ProjectileSpawnOffset, value.ProjectileHeatScale, value.HeatDissipationPerSecond,
            value.StationSensorRange, value.EntitySensorRange, value.PlayerStationHull,
            value.HostileStationHull, value.PlayerEntityHull, value.RaiderEntityHull,
            value.StationShield, value.EntityShield, value.WeaponLockSpeed,
            value.WeaponLockSensorImpact, value.WeaponLockAngleDegrees,
            value.WeaponLockDirectionImpact, value.WeaponLockDecayPerSecond,
            probability, value.LootDropVelocity, value.PickupLifetimeSeconds);
    }

    private static void AgentPatrolsHistoricalOrbitCircuitThroughMovementCommands()
    {
        var agent = Entity(0, 0, "workers");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Defend];
        var catalog = EquipThrusterBank([agent]);
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-patrol-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "",
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    Entities = [agent],
                    Orbits =
                    [
                        new AetheriaRuntimeOrbitSnapshotCommit { OrbitKey = "orbit:east", FixedPositionX = 20 },
                        new AetheriaRuntimeOrbitSnapshotCommit { OrbitKey = "orbit:west", FixedPositionX = -20 }
                    ],
                    Bodies =
                    [
                        new AetheriaRuntimeBodySnapshotCommit { BodyKey = "body:east", OrbitKey = "orbit:east", Kind = "planet" },
                        new AetheriaRuntimeBodySnapshotCommit { BodyKey = "body:west", OrbitKey = "orbit:west", Kind = "planet" }
                    ]
                }
            ]
        };
        var issue = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.IssueAgentTask,
            "commander-smoke",
            "starbridge-smoke",
            0,
            "");
        issue.CommandId = "issue-patrol";
        issue.AgentTask = new AetheriaRuntimeAgentTaskCommand
        {
            TaskId = "patrol-orbits",
            CorporationKey = "workers",
            TaskType = AetheriaRuntimeAgentTaskTypes.Defend,
            Priority = 10,
            ZoneIndex = 0,
            CompletionRadius = 5,
            TargetBodyKeys = ["body:east", "body:west"]
        };
        var sawWestLeg = false;
        var sawReturnToEast = false;
        var physics = NewPhysics();
        for (var frame = 0; frame < 100; frame++)
        {
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), "aetheria-agent-patrol-smoke.cc"),
                run,
                new AetheriaRuntimeDaemonTickOptions
                {
                    WorldPhysics = physics,
                    Catalog = catalog,
                    FrameId = frame,
                    FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frame * 0.1,
                    ObservedCommands = frame == 0 ? [issue] : Array.Empty<AetheriaRuntimeDaemonCommandDocument>(),
                    BuildPublications = false
                });
            Require(tick.OperationResult.AppliedCommandIds.Any(id => id.EndsWith(":move", StringComparison.Ordinal)),
                "patrol controller must drive the shared movement command every tick");
            var cursor = run.AgentTasks.Single().CircuitIndex;
            sawWestLeg |= cursor == 1;
            sawReturnToEast |= sawWestLeg && cursor == 0;
            if (sawReturnToEast)
                break;
        }

        Require(sawWestLeg && sawReturnToEast, "patrol must advance through and wrap its authored orbit circuit");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Assigned, run.AgentTasks.Single().Status,
            "patrol is persistent work and must remain assigned after one circuit");
    }

    private static void RejectedHaulTransferDoesNotAdvanceTask()
    {
        var origin = Entity(0, 0, "workers");
        origin.CargoContents = [Cargo(("ore", 2, 0, 0))];
        var agent = Entity(1, 0, "workers");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Haul];
        agent.CargoContents = Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
        var destination = Entity(2, 20, "workers");
        destination.CargoContents = [Cargo()];
        var task = new AetheriaRuntimeAgentTaskCommit
        {
            TaskId = "rejected-haul",
            CorporationKey = "workers",
            TaskType = AetheriaRuntimeAgentTaskTypes.Haul,
            Priority = 1,
            ZoneIndex = 0,
            OriginEntityIndex = 0,
            TargetEntityIndex = 2,
            ItemKey = "ore",
            RequestedQuantity = 1,
            CompletionRadius = 5,
            Phase = "pickup"
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "rejected-haul-smoke",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [origin, agent, destination] }],
            AgentTasks = [task]
        };

        var planned = AetheriaRuntimeAgentScheduler.AssignAndPlan(run, 1);
        var reduced = AetheriaRuntimeDaemonOperations.Execute(run, planned);
        AetheriaRuntimeAgentScheduler.Reconcile(run, 1, reduced.AppliedCommandIds, reduced.RejectedCommandIds);

        Require(reduced.RejectedCommandIds.Any(id => id.EndsWith(":pickup", StringComparison.Ordinal)),
            "invalid pickup must be rejected by the normal cargo reducer");
        RequireEqual("pickup", task.Phase, "rejected pickup must not advance the haul task");
        RequireEqual(0, task.PendingQuantity, "rejected pickup must clear pending transfer state");
        RequireEqual(2, CargoQuantity(origin, "ore"), "rejected pickup must leave origin cargo untouched");
    }

    private static void AgentCompletesHaulTaskThroughMovementAndCargoCommands()
    {
        var origin = Entity(0, 0, "workers");
        origin.Kind = "station";
        origin.CargoContents = [Cargo(("ore", 5, 2, 3))];
        var agent = Entity(1, 0, "workers");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Haul];
        agent.CargoContents = [Cargo()];
        var destination = Entity(2, 50, "workers");
        destination.Kind = "station";
        destination.CargoContents = [Cargo()];
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-haul-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [origin, agent, destination] }]
        };
        var issue = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.IssueAgentTask,
            "commander-smoke",
            "starbridge-smoke",
            0,
            "");
        issue.CommandId = "issue-haul";
        issue.AgentTask = new AetheriaRuntimeAgentTaskCommand
        {
            TaskId = "haul-ore",
            CorporationKey = "workers",
            TaskType = AetheriaRuntimeAgentTaskTypes.Haul,
            Priority = 40,
            ZoneIndex = 0,
            OriginEntityIndex = 0,
            TargetEntityIndex = 2,
            ItemKey = "ore",
            Quantity = 3,
            CompletionRadius = 5
        };
        var sawPickup = false;
        var sawDelivery = false;
        for (var frame = 0; frame < 30; frame++)
        {
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), "aetheria-agent-haul-smoke.cc"),
                run,
                new AetheriaRuntimeDaemonTickOptions
                {
                    WorldPhysics = NewPhysics(),
                    FrameId = frame,
                    FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frame * 0.1,
                    ObservedCommands = frame == 0 ? [issue] : Array.Empty<AetheriaRuntimeDaemonCommandDocument>(),
                    BuildPublications = false
                });
            sawPickup |= tick.OperationResult.AppliedCommandIds.Any(id => id.EndsWith(":pickup", StringComparison.Ordinal));
            sawDelivery |= tick.OperationResult.AppliedCommandIds.Any(id => id.EndsWith(":delivery", StringComparison.Ordinal));
            if (string.Equals(run.AgentTasks.Single().Status, AetheriaRuntimeAgentTaskStatuses.Completed, StringComparison.Ordinal))
                break;
        }

        Require(sawPickup && sawDelivery, "haul task must use accepted pickup and delivery cargo commands");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Completed, run.AgentTasks.Single().Status,
            "haul task must complete only after accepted delivery");
        RequireEqual(2, CargoQuantity(origin, "ore"), "origin must retain the unrequested stack quantity");
        RequireEqual(3, CargoQuantity(destination, "ore"), "destination must receive exactly the requested quantity");
        RequireEqual(0, CargoQuantity(agent, "ore"), "hauler must finish with no in-transit cargo");
    }

    private static AetheriaRuntimeCargoBayLoadoutCommit Cargo(params (string ItemKey, int Quantity, int X, int Y)[] items) => new()
    {
        Items = items.Select(item => new AetheriaRuntimeLoadoutItemSlotCommit
        {
            X = item.X,
            Y = item.Y,
            Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = item.ItemKey, Quantity = item.Quantity }
        }).ToArray()
    };

    private static int CargoQuantity(AetheriaRuntimeEntitySnapshotCommit entity, string itemKey) =>
        (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
            .SelectMany(bay => bay.Items)
            .Where(slot => string.Equals(slot.Item.ItemKey, itemKey, StringComparison.Ordinal))
            .Sum(slot => slot.Item.Quantity);

    private static void AgentCompletesAttackTaskThroughTargetFireAndYmir()
    {
        var agent = Entity(0, 0, "player");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Attack];
        agent.WeaponGroups = [new[] { 0 }];
        agent.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit
            {
                Item = new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemKey = "test-lock-cannon", Quality = 1, Durability = 1, Enabled = true
                }
            },
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "test-attack-capacitor", Quality = 1, Durability = 1, Enabled = true } }
        ];
        agent.BehaviorStates =
        [
            new AetheriaRuntimeBehaviorStateCommit
            {
            OwnerKind = AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, OwnerIndex = 1, BehaviorIndex = 0,
                BehaviorKind = "Capacitor", CapacitorCharge = 50, CapacitorCapacity = 50, CapacitorEfficiency = 1
            }
        ];
        agent.CargoContents = [Cargo(("test-ammo", 2, 0, 0))];
        var weaponPayload = new AetheriaRuntimeBehaviorPayload(
            0,
            "LockWeapon",
            0,
            [
                new AetheriaRuntimeBehaviorField(1, Number(1)),
                new AetheriaRuntimeBehaviorField(2, PerformanceStat(7)),
                new AetheriaRuntimeBehaviorField(3, PerformanceStat(1.5)),
                new AetheriaRuntimeBehaviorField(4, PerformanceStat(2)),
                new AetheriaRuntimeBehaviorField(6, PerformanceStat(145)),
                new AetheriaRuntimeBehaviorField(9, PerformanceStat(5)),
                new AetheriaRuntimeBehaviorField(10, PerformanceStat(3)),
                new AetheriaRuntimeBehaviorField(12, new AetheriaRuntimeBehaviorValue(
                    "item-key", "", 0, false, "", "test-ammo",
                    Array.Empty<AetheriaRuntimeBehaviorValue>(), Array.Empty<AetheriaRuntimeBehaviorMapEntry>())),
                new AetheriaRuntimeBehaviorField(13, Number(3)),
                new AetheriaRuntimeBehaviorField(14, Number(0.3)),
                new AetheriaRuntimeBehaviorField(16, PerformanceStat(330)),
                new AetheriaRuntimeBehaviorField(17, PerformanceStat(3)),
                new AetheriaRuntimeBehaviorField(18, PerformanceStat(0.3)),
                new AetheriaRuntimeBehaviorField(19, PerformanceStat(0.2)),
                new AetheriaRuntimeBehaviorField(21, PerformanceStat(2)),
                new AetheriaRuntimeBehaviorField(22, PerformanceStat(1)),
                new AetheriaRuntimeBehaviorField(23, PerformanceStat(45)),
                new AetheriaRuntimeBehaviorField(24, PerformanceStat(1)),
                new AetheriaRuntimeBehaviorField(25, PerformanceStat(1))
            ]);
        var catalog = EquipThrusterBank(
            [agent],
            CatalogItem("test-lock-cannon", weaponPayload),
            CatalogItem("test-attack-capacitor", CapacitorPayload(50, 1)));
        var target = Entity(1, 105, "raider");
        target.Kind = "station";
        target.StatGrids = [Grid("hull", 14), Grid("shield", 0), Grid("heat", 0)];
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-attack-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [agent, target] }]
        };
        var issue = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.IssueAgentTask,
            "commander-smoke",
            "starbridge-smoke",
            0,
            "");
        issue.CommandId = "issue-attack";
        issue.AgentTask = new AetheriaRuntimeAgentTaskCommand
        {
            TaskId = "attack-raider",
            CorporationKey = "player",
            TaskType = AetheriaRuntimeAgentTaskTypes.Attack,
            Priority = 100,
            ZoneIndex = 0,
            TargetEntityIndex = 1,
            CompletionRadius = 25,
            WeaponGroup = 0
        };
        var sawTargetCommand = false;
        var sawFireCommand = false;
        var sawPartialLock = false;
        var lockTrace = new List<double>();
        var attackTrace = new List<string>();
        var firstLaunchFrame = -1;
        var physics = NewPhysics();
        for (var frame = 0; frame < 60; frame++)
        {
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), "aetheria-agent-attack-smoke.cc"),
                run,
                new AetheriaRuntimeDaemonTickOptions
                {
                    WorldPhysics = physics,
                    FrameId = frame,
                    FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frame * 0.1,
                    ObservedCommands = frame == 0 ? [issue] : Array.Empty<AetheriaRuntimeDaemonCommandDocument>(),
                    Catalog = catalog,
                    BuildPublications = false
                });
            sawTargetCommand |= tick.OperationResult.AppliedCommandIds.Any(id => id.EndsWith(":target", StringComparison.Ordinal));
            sawFireCommand |= tick.OperationResult.AppliedCommandIds.Any(id => id.EndsWith(":fire", StringComparison.Ordinal));
            var weapon = (agent.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
                .Single(value => value.OwnerKind == AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind);
            lockTrace.Add(weapon.LockProgress);
            attackTrace.Add($"{frame}:{agent.TargetEntityIndex}:{agent.PositionX:0.##},{agent.PositionZ:0.##}:{agent.DirectionX:0.##},{agent.DirectionY:0.##}:{weapon.LockTargetEntityIndex}");
            sawPartialLock |= weapon.LockProgress > 0 && weapon.LockProgress < 0.99;
            if (firstLaunchFrame < 0 && run.GameEvents.Any(value => value.Kind == "shot.committed"))
                firstLaunchFrame = frame;
            if (string.Equals(run.AgentTasks.Single().Status, AetheriaRuntimeAgentTaskStatuses.Completed, StringComparison.Ordinal))
                break;
        }

        Require(sawTargetCommand, "attack agent must target through the shared target command");
        Require(sawFireCommand, "attack agent must fire through the shared weapon-group command");
        Require(sawPartialLock,
            $"attack agent must acquire a persisted partial weapon lock before firing; locks={string.Join(',', lockTrace.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture)))} states={string.Join('|', attackTrace)}");
        Require(firstLaunchFrame > 1, "accepted fire intent must not bypass progressive weapon lock acquisition");
        Require(run.GameEvents.Any(value => value.Kind == "shot.committed" && value.SourceEntityIndex == agent.EntityIndex),
            "accepted fire control must emit authoritative shot commitment chronology");
        Require(run.GameEvents.Any(value => value.Kind == "shot.committed" &&
                value.ItemKey == "test-lock-cannon" && Math.Abs(value.ScalarValue - (7.0 / 3.0)) < 0.000001),
            "daemon combat must divide authored damage across the configured burst rounds");
        Require((agent.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>()).Any(value => value.OwnerKind == AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind &&
                value.OwnerIndex == 0 && value.BehaviorKind == "LockWeapon"),
            "weapon progress must belong to the equipped authored behavior instead of a synthetic entity weapon");
        var authoredWeapon = agent.WeaponStates.Single(value =>
            value.OwnerKind == AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind);
        RequireEqual(0, authoredWeapon.Ammo,
            "two three-round authored bursts must consume both complete magazines");
        RequireNear(40, agent.BehaviorStates.Single(value => value.BehaviorKind == "Capacitor").CapacitorCharge,
            0.000001, "two authored bursts must drain exactly their combined energy from canonical capacitor state");
        RequireEqual(1, CargoQuantity(agent, "test-ammo"),
            "one empty magazine must consume exactly one reserve cargo commodity before reload");
        Require(run.GameEvents.Count(value => value.Kind == "shot.committed" && value.ItemKey == "test-lock-cannon") >= 6,
            "two authored bursts must commit all six due rounds through the shot resolver");
        Require(run.ShotReceipts.Count(value => value.WeaponItemKey == "test-lock-cannon") >= 6 &&
                run.ShotReceipts.All(value => value.Hit && value.HullAppliedDamage >= 0 &&
                    value.HullAppliedDamage <= value.NominalDamage && value.ShieldAbsorbedDamage == 0 &&
                    value.DamageType == "Corrosive" && Math.Abs(value.Penetration - 1.5) < 0.000001 &&
                    Math.Abs(value.DamageSpread - 2) < 0.000001),
            "authored rounds must retain damage type, penetration, spread, and exact damage receipts before presentation travel");
        Require(run.ShotReceipts.Select(value => value.ShotId).Distinct(StringComparer.Ordinal).Count() ==
                run.ShotReceipts.Count,
            "every burst round must own a stable independent shot identity");
        Require(run.GameEvents.Count(value => value.Kind == "weapon.reload.started") == 1 &&
                run.GameEvents.Count(value => value.Kind == "weapon.reload.completed") == 1,
            "empty magazine transition must publish one authoritative reload start and completion pair");
        var reloadStarted = run.GameEvents.Single(value => value.Kind == "weapon.reload.started").FrameId;
        var reloadCompleted = run.GameEvents.Single(value => value.Kind == "weapon.reload.completed").FrameId;
        Require(reloadCompleted > reloadStarted && !run.GameEvents.Any(value =>
                value.Kind == "shot.committed" && value.FrameId > reloadStarted && value.FrameId < reloadCompleted),
            "reload interval must be a shot-free authoritative timeline, not a cosmetic client delay");
        Require(!target.IsActive,
            $"attack task must end through daemon shot resolution before presentation projectile contacts; hull={Stat(target, "hull"):0.###} " +
            $"agent={agent.PositionX:0.###},{agent.PositionZ:0.###} target={target.PositionX:0.###},{target.PositionZ:0.###} " +
            $"projectiles={run.Zones[0].PhysicalPayloads.Count} task={run.AgentTasks.Single().Status}");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Completed, run.AgentTasks.Single().Status,
            "attack task must complete when its target dies");
        var shotSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 60, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        Require(Flatten(shotSurface.Surface.Root).Any(node => node.Kind == "shot.receipt" &&
                node.Props["itemKey"] == "test-lock-cannon" && node.Props["outcome"] == "hit" &&
                node.Props["presentationKind"] == "bolt" &&
                node.Props.ContainsKey("presentationIntensity") &&
                node.Props.ContainsKey("impactKind")),
            "Eve must project inspectable deterministic shot receipts without reconstructing combat");
    }

    private static void AttackTaskAdmissionRejectsImpossibleWork()
    {
        var agent = Entity(0, 0, "player");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Attack];
        agent.WeaponGroups = [new[] { 0 }];
        var hostile = Entity(1, 100, "raider");
        var friendly = Entity(2, 100, "player");
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "attack-admission-smoke",
            CurrentEntityKey = "",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [agent, hostile, friendly] }]
        };

        AetheriaRuntimeDaemonCommandDocument Command(string id, int targetIndex, int weaponGroup)
        {
            var command = AetheriaRuntimeDaemonCommandDocument.Create(
                AetheriaRuntimeDaemonCommandKinds.IssueAgentTask, "commander-smoke", "starbridge-smoke", 0, "");
            command.CommandId = id;
            command.AgentTask = new AetheriaRuntimeAgentTaskCommand
            {
                TaskId = id,
                CorporationKey = "player",
                TaskType = AetheriaRuntimeAgentTaskTypes.Attack,
                ZoneIndex = 0,
                TargetEntityIndex = targetIndex,
                WeaponGroup = weaponGroup
            };
            return command;
        }

        var result = AetheriaRuntimeDaemonOperations.Execute(run,
        [
            Command("missing-target", 99, 0),
            Command("friendly-target", 2, 0),
            Command("missing-group", 1, 1),
            Command("valid-attack", 1, 0)
        ]);

        Require(result.RejectedCommandIds.SequenceEqual(new[] { "missing-target", "friendly-target", "missing-group" }),
            "attack admission must reject missing/friendly targets and unavailable weapon groups");
        Require(result.AppliedCommandIds.SequenceEqual(new[] { "valid-attack" }),
            "attack admission must accept executable hostile work");
        Require(run.AgentTasks.Count == 1 && run.AgentTasks[0].TaskId == "valid-attack",
            "rejected attack orders must not enter the durable corporation queue");
    }

    private static void AttackAgentControlsOptimumRangeThroughMovementLever()
    {
        var settings = AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault;
        var optimum = settings.AttackRange * settings.AttackHoldRatio;

        var closing = AttackMovementAtDistance(optimum + 30);
        Require(closing.DirectionX > 0.99 && closing.ScalarValue > 0,
            "attack agent outside optimum range must close through the shared movement lever");

        var retreating = AttackMovementAtDistance(optimum - 30);
        Require(retreating.DirectionX < -0.99 && retreating.ScalarValue > 0,
            "attack agent inside optimum range must retreat through the shared movement lever");

        var holding = AttackMovementAtDistance(optimum);
        Require(Math.Abs(holding.DirectionX) < 0.001 && Math.Abs(holding.ScalarValue) < 0.001,
            "attack agent in the optimum band must hold range instead of charging the target");
    }

    private static AetheriaRuntimeDaemonCommandDocument AttackMovementAtDistance(double distance)
    {
        var agent = Entity(0, 0, "workers");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Attack];
        agent.WeaponGroups = [new[] { 0 }];
        var target = Entity(1, distance, "raiders");
        var task = new AetheriaRuntimeAgentTaskCommit
        {
            TaskId = "range-control",
            CorporationKey = "workers",
            TaskType = AetheriaRuntimeAgentTaskTypes.Attack,
            ZoneIndex = 0,
            TargetEntityIndex = 1,
            WeaponGroup = 0
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-range-control-smoke",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [agent, target] }],
            AgentTasks = [task]
        };

        return AetheriaRuntimeAgentScheduler.AssignAndPlan(run, 1)
            .Single(command => command.Kind == AetheriaRuntimeDaemonCommandKinds.SetMoveVector);
    }

    private static void SchedulerAssignsHighestPriorityCompatibleTask()
    {
        var agent = Entity(0, 0, "workers");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var low = AgentTask("low", 1);
        var high = AgentTask("high", 99);
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-priority-smoke",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [agent] }],
            AgentTasks = [low, high]
        };

        AetheriaRuntimeAgentScheduler.AssignAndPlan(run, 4);

        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Assigned, high.Status,
            "highest priority compatible task must claim the available agent");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Queued, low.Status,
            "lower priority task must remain queued when no controller remains");
        RequireEqual("high", agent.AssignedAgentTaskId, "agent assignment must point at the selected task");
    }

    private static void SchedulerRequeuesTaskFromDeadAgent()
    {
        var dead = Entity(0, 0, "workers");
        dead.IsActive = false;
        dead.AssignedAgentTaskId = "recover-work";
        var replacement = Entity(1, 0, "workers");
        replacement.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var task = new AetheriaRuntimeAgentTaskCommit
        {
            TaskId = "recover-work",
            CorporationKey = "workers",
            TaskType = AetheriaRuntimeAgentTaskTypes.Explore,
            ZoneIndex = 0,
            Status = AetheriaRuntimeAgentTaskStatuses.Assigned,
            AssignedEntityIndex = 0,
            TargetPositionX = 100,
            DeliveredQuantity = 3,
            RequestedQuantity = 5
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-recovery-smoke",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [dead, replacement] }],
            AgentTasks = [task]
        };

        AetheriaRuntimeAgentScheduler.AssignAndPlan(run, 8);

        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Assigned, task.Status,
            "work abandoned by an inactive agent must return to the corporation queue and be reassigned");
        RequireEqual(replacement.EntityIndex, task.AssignedEntityIndex,
            "compatible active replacement must receive abandoned work");
        RequireEqual(3, task.DeliveredQuantity,
            "reassignment must preserve authoritative task progress");
        Require(string.IsNullOrWhiteSpace(dead.AssignedAgentTaskId),
            "inactive agent must not retain reservation authority");
    }

    private static void SchedulerCollapsesDuplicateAssignmentMarkers()
    {
        var first = Entity(0, 0, "workers");
        first.AssignedAgentTaskId = "single-owner";
        var duplicate = Entity(1, 0, "workers");
        duplicate.AssignedAgentTaskId = "single-owner";
        var task = AgentTask("single-owner", 1);
        task.Status = AetheriaRuntimeAgentTaskStatuses.Assigned;
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-single-owner-smoke",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [first, duplicate] }],
            AgentTasks = [task]
        };

        AetheriaRuntimeAgentScheduler.AssignAndPlan(run, 9);

        RequireEqual(1, run.Zones.SelectMany(zone => zone.Entities)
                .Count(entity => string.Equals(entity.AssignedAgentTaskId, task.TaskId, StringComparison.Ordinal)),
            "one task must have exactly one active carrier after reconciliation");
        RequireEqual(first.EntityIndex, task.AssignedEntityIndex,
            "duplicate assignment reconciliation must be deterministic");
    }

    private static AetheriaRuntimeAgentTaskCommit AgentTask(string id, int priority) => new()
    {
        TaskId = id,
        CorporationKey = "workers",
        TaskType = AetheriaRuntimeAgentTaskTypes.Explore,
        Priority = priority,
        ZoneIndex = 0,
        TargetPositionX = 100,
        CompletionRadius = 5
    };

    private static void SchedulerAssignsShortestGalaxyRoute()
    {
        var distant = Entity(0, 0, "workers");
        distant.EntityId = "worker.route.distant";
        distant.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var nearby = Entity(0, 0, "workers");
        nearby.EntityId = "worker.route.nearby";
        nearby.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var task = AgentTask("route-choice", 10);
        task.ZoneIndex = 2;
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-route-choice-smoke",
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, PositionX = 0, GravityTerrainRadius = 100, AdjacentZoneIndices = [1], Entities = [distant] },
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 1, PositionX = 100, GravityTerrainRadius = 100, AdjacentZoneIndices = [0, 2], Entities = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>() },
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 2, PositionX = 200, GravityTerrainRadius = 100, AdjacentZoneIndices = [1, 3], Entities = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>() },
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 3, PositionX = 300, GravityTerrainRadius = 100, AdjacentZoneIndices = [2], Entities = [nearby] }
            ],
            AgentTasks = [task]
        };

        AetheriaRuntimeAgentScheduler.AssignAndPlan(run, 1);

        RequireEqual("route-choice", nearby.AssignedAgentTaskId,
            "scheduler must reserve the controller with the shortest galaxy route to the task zone");
        Require(string.IsNullOrWhiteSpace(distant.AssignedAgentTaskId),
            "longer-route controller must remain available");
    }

    private static void AgentTraversesGalaxyRouteBeforeExecutingTask()
    {
        var agent = Entity(0, 0, "workers");
        agent.EntityId = "worker.cross-zone.stable";
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var catalog = EquipThrusterBank([agent]);
        var task = AgentTask("cross-zone", 10);
        task.ZoneIndex = 2;
        task.TargetPositionX = 0;
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-route-travel-smoke",
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, PositionX = 0, GravityTerrainRadius = 100, AdjacentZoneIndices = [1], Entities = [agent] },
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 1, PositionX = 100, GravityTerrainRadius = 100, AdjacentZoneIndices = [0, 2], Entities = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>() },
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 2, PositionX = 200, GravityTerrainRadius = 100, AdjacentZoneIndices = [1], Entities = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>() }
            ],
            AgentTasks = [task]
        };
        var appliedTravelCommands = 0;
        var appliedApproachCommands = 0;
        var physics = NewPhysics();
        for (var frame = 0; frame < 100; frame++)
        {
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), "aetheria-agent-route-travel-smoke.cc"),
                run,
                new AetheriaRuntimeDaemonTickOptions
                {
                    WorldPhysics = physics,
                    Catalog = catalog,
                    FrameId = frame,
                    FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frame * 0.1,
                    BuildPublications = false
                });
            appliedTravelCommands += tick.OperationResult.AppliedCommandIds.Count(id => id.EndsWith(":travel", StringComparison.Ordinal));
            appliedApproachCommands += tick.OperationResult.AppliedCommandIds.Count(id => id.EndsWith(":travel-approach", StringComparison.Ordinal));
            if (string.Equals(task.Status, AetheriaRuntimeAgentTaskStatuses.Completed, StringComparison.Ordinal))
                break;
        }

        Require(appliedApproachCommands > 0,
            "agent must approach wormholes through shared movement commands and Ymir before transition");
        RequireEqual(2, appliedTravelCommands,
            "agent must traverse each galaxy edge through the shared wormhole command boundary");
        Require(run.Zones.Single(zone => zone.ZoneIndex == 2).Entities.Count == 1,
            "assigned agent must arrive in the task zone without a parallel teleport owner");
        RequireEqual("worker.cross-zone.stable", run.Zones.Single(zone => zone.ZoneIndex == 2).Entities.Single().EntityId,
            "zone transfer must preserve stable entity identity while projection indices change");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Completed, task.Status,
            "agent must execute the task only after arriving in its destination zone");
    }

    private static void IdleAgentReturnsToCanonicalHomeAndDocks()
    {
        var worker = Entity(0, 0, "workers");
        worker.EntityId = "worker.return-home";
        worker.HomeEntityId = "station.home";
        worker.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Haul];
        var home = Entity(0, 60, "workers");
        home.EntityId = "station.home";
        home.Kind = "station";
        var catalog = EquipThrusterBank([worker]);
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-return-home-smoke",
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, PositionX = 0, GravityTerrainRadius = 100, AdjacentZoneIndices = [1], Entities = [worker] },
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 1, PositionX = 100, GravityTerrainRadius = 100, AdjacentZoneIndices = [0, 2], Entities = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>() },
                new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 2, PositionX = 200, GravityTerrainRadius = 100, AdjacentZoneIndices = [1], Entities = [home] }
            ]
        };
        var sawApproach = false;
        var travelCount = 0;
        var sawDock = false;
        var rejectedHomeCommands = new List<string>();
        var physics = NewPhysics();
        for (var frame = 0; frame < 120; frame++)
        {
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), "aetheria-agent-return-home-smoke.cc"),
                run,
                new AetheriaRuntimeDaemonTickOptions
                {
                    WorldPhysics = physics,
                    Catalog = catalog,
                    FrameId = frame,
                    FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frame * 0.1,
                    BuildPublications = false
                });
            sawApproach |= tick.OperationResult.AppliedCommandIds.Any(id => id.Contains(":home-approach", StringComparison.Ordinal));
            travelCount += tick.OperationResult.AppliedCommandIds.Count(id => id.EndsWith(":home-travel", StringComparison.Ordinal));
            sawDock |= tick.OperationResult.AppliedCommandIds.Any(id => id.EndsWith(":home-dock", StringComparison.Ordinal));
            rejectedHomeCommands.AddRange(tick.OperationResult.RejectedCommandIds.Where(id => id.Contains(":home-", StringComparison.Ordinal)));
            if (sawDock)
                break;
        }

        var homeZone = run.Zones.Single(zone => zone.ZoneIndex == 2);
        var arrivedWorker = homeZone.Entities.Single(entity => entity.EntityId == "worker.return-home");
        var arrivedHome = homeZone.Entities.Single(entity => entity.EntityId == "station.home");
        Require(sawApproach, "idle worker must approach its route and home through shared movement commands");
        RequireEqual(2, travelCount, "idle worker must traverse the shortest route to its canonical home");
        Require(sawDock,
            $"idle worker must dock through the daemon docking command; worker={arrivedWorker.PositionX:0.###},{arrivedWorker.PositionZ:0.###} home={arrivedHome.PositionX:0.###},{arrivedHome.PositionZ:0.###} rejected={string.Join(",", rejectedHomeCommands)}");
        Require(arrivedHome.DockingBayAssignments.Contains(arrivedWorker.EntityIndex),
            "home docking parentage must be the authoritative completion fact");

        var settled = AetheriaRuntimeDaemonTickRunner.Tick(
            Path.Combine(Path.GetTempPath(), "aetheria-agent-return-home-settled-smoke.cc"),
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                WorldPhysics = physics, Catalog = catalog, FrameId = 121,
                FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 12.1, BuildPublications = false
            });
        Require(!settled.OperationResult.AppliedCommandIds.Any(id => id.Contains(":home-", StringComparison.Ordinal)),
            "docked worker must not emit a perpetual homecoming repair loop");
    }

    private static void ControlledShipDoesNotReceiveAutonomousHelmCommands()
    {
        var controlled = Entity(0, 100, "workers");
        controlled.EntityId = "controlled-worker";
        controlled.HomeEntityId = "controlled-home";
        controlled.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var home = Entity(1, 0, "workers");
        home.EntityId = "controlled-home";
        home.Kind = "station";
        var task = AgentTask("controlled-task", 10);
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "controlled-agent-authority-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [controlled, home] }],
            AgentTasks = [task]
        };

        var commands = AetheriaRuntimeAgentScheduler.AssignAndPlan(run, 1);

        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Queued, task.Status,
            "manual helm owner must not be conscripted into autonomous corporation work");
        Require(commands.Count == 0,
            "controlled ship must emit no task or return-home commands without delegated helm authority");
    }

    private static void AgentClaimsAndCompletesExploreTaskThroughCommands()
    {
        var agent = Entity(0, 0, "workers");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var catalog = EquipThrusterBank([agent]);
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-task-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [agent] }]
        };
        var issue = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.IssueAgentTask,
            "commander-smoke",
            "starbridge-smoke",
            0,
            "");
        issue.CommandId = "issue-explore";
        issue.AgentTask = new AetheriaRuntimeAgentTaskCommand
        {
            TaskId = "explore-east",
            CorporationKey = "workers",
            TaskType = AetheriaRuntimeAgentTaskTypes.Explore,
            Priority = 50,
            ZoneIndex = 0,
            TargetPositionX = 50,
            TargetPositionZ = 0,
            CompletionRadius = 5
        };
        var completed = false;
        var physics = NewPhysics();
        for (var frame = 0; frame <= 60; frame++)
        {
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), "aetheria-agent-task-smoke.cc"),
                run,
                new AetheriaRuntimeDaemonTickOptions
                {
                    WorldPhysics = physics,
                    FrameId = frame,
                    FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frame * 0.1,
                    ObservedCommands = frame == 0 ? [issue] : Array.Empty<AetheriaRuntimeDaemonCommandDocument>(),
                    Catalog = catalog,
                    BuildPublications = false
                });
            if (frame == 0)
            {
                Require(tick.OperationResult.AppliedCommandIds.Contains("issue-explore"),
                    "commander task command must enter the normal tick reducer");
                Require(tick.OperationResult.AppliedCommandIds.Any(id => id.Contains(":explore-east:0:move", StringComparison.Ordinal)),
                    "agent movement must return through the same command receipts");
            }
            completed = string.Equals(run.AgentTasks.Single().Status, AetheriaRuntimeAgentTaskStatuses.Completed, StringComparison.Ordinal);
            if (completed)
                break;
        }

        Require(completed,
            $"agent must complete an explore task without direct position mutation; position={agent.PositionX:0.###},{agent.PositionZ:0.###} velocity={agent.VelocityX:0.###},{agent.VelocityY:0.###}");
        Require(string.IsNullOrWhiteSpace(agent.AssignedAgentTaskId), "completed task must release the agent");
        Require(agent.PositionX >= 45, "agent must reach the task through repeated movement commands");

        var frameDocument = new AetheriaRuntimeDaemonFrameDocument { FrameId = 21, Run = run };
        var commander = AetheriaRuntimeDaemonGameSurfaceBuilder.BuildCommander(
            frameDocument,
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("agent-task-smoke"));
        var taskNode = Flatten(commander.Surface.Root)
            .Single(component => string.Equals(component.Id, "aetheria.starbridge.commander.tasks.explore_east", StringComparison.Ordinal));
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Completed, taskNode.Props["status"],
            "commander Eve surface must publish authoritative task status");
        Require(commander.Commands.Any(command => string.Equals(command.Command, "aetheria.daemon.issue_agent_task", StringComparison.Ordinal)),
            "commander Eve surface must advertise task issue command");
        var roster = Flatten(commander.Surface.Root)
            .Single(component => string.Equals(component.Kind, "agent.roster", StringComparison.Ordinal));
        var worker = Flatten(roster)
            .Single(component => string.Equals(component.Kind, "agent.item", StringComparison.Ordinal));
        Require(!string.IsNullOrWhiteSpace(worker.Props["entityId"]),
            "commander Eve roster must expose stable provider-owned worker identity");
        Require(worker.Props["capabilities"].Split(',').Contains(AetheriaRuntimeAgentTaskTypes.Explore),
            "commander Eve roster must expose provider-owned capability claims");
        RequireEqual("idle", worker.Props["status"],
            "commander Eve roster must publish generic status for runtime-independent lowering");
        Require(worker.Props["badges"].Split(',').Contains(AetheriaRuntimeAgentTaskTypes.Explore),
            "commander Eve roster must publish capability badges through the generic item contract");
        RequireEqual("", worker.Props["assignedTaskId"],
            "completed worker assignment must be visible as released on the Eve roster");
        RequireEqual("false", worker.Props["controlled"],
            "commander Eve roster must distinguish autonomous workers from manual helm authority");
    }

    private static void MultipleActorsUseTheSameMovementLever()
    {
        var player = Entity(0, 0, "player");
        var agent = Entity(1, 200, "worker");
        var catalog = EquipThrusterBank([player, agent]);
        player.TargetEntityIndex = agent.EntityIndex;
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "shared-lever-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [player, agent] }]
        };
        var commands = new[]
        {
            MovementCommand("player-move", "zone.0.entity.0", 1, 0),
            MovementCommand("agent-move", "zone.0.entity.1", 0, 1)
        };
        var operation = AetheriaRuntimeDaemonOperations.Execute(run, commands);

        RequireEqual(2, operation.Intents.Movements.Count, "movement intent must retain one lever position per actor");
        var physics = NewPhysics();
        for (var step = 0; step < 2; step++)
            AetheriaRuntimeDaemonSimulation.Step(
                run,
                operation.Intents,
                0.1,
                AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
                physics,
                catalog,
                step + 1);
        Require(player.VelocityX > 0 && Math.Abs(player.VelocityY) < Math.Abs(player.VelocityX) * 0.1,
            $"player command must drive its actor through the shared movement lever; velocity={player.VelocityX:0.###},{player.VelocityY:0.###}");
        Require(agent.VelocityY > 0 && Math.Abs(agent.VelocityX) < Math.Abs(agent.VelocityY) * 0.1,
            $"agent command must drive its actor through the shared movement lever; velocity={agent.VelocityX:0.###},{agent.VelocityY:0.###}");

        var release = MovementCommand("player-release", "zone.0.entity.0", 0, 0);
        release.ScalarValue = 0;
        var releaseOperation = AetheriaRuntimeDaemonOperations.Execute(run, [release]);
        AetheriaRuntimeDaemonSimulation.Step(
            run,
            releaseOperation.Intents,
            0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            physics,
            catalog,
            3);
        RequireEqual(agent.EntityIndex, player.TargetEntityIndex,
            "releasing the movement lever must not steal targeting authority");
    }

    private static void DirectionalThrustersOwnOrdinaryFlightFeedback()
    {
        var ship = Entity(0, 0, "player");
        var catalog = EquipThrusterBank([ship]);
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "ordinary-thruster-flight-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [ship] }]
        };
        var operation = AetheriaRuntimeDaemonOperations.Execute(
            run, [MovementCommand("forward-thrust", "zone.0.entity.0", 0, 1)]);
        var physics = NewPhysics();
        AetheriaRuntimeDaemonSimulation.Step(
            run, operation.Intents, 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            physics, catalog, 1);

        var thrusterStates = ship.BehaviorStates
            .Where(value => value.BehaviorKind == AetheriaRuntimeBehaviorKinds.Thruster)
            .ToArray();
        RequireEqual(8, thrusterStates.Length,
            "ordinary hull fixture must fly through its directional thruster bank");
        Require(thrusterStates.Count(value => value.ThrusterAxis > 0.99) == 2 &&
                thrusterStates.Where(value => value.ThrusterAxis > 0.99)
                    .All(value => ship.Equipment[value.OwnerIndex].Rotation == "Half"),
            "forward helm must fire only the reversed forward-thrust pair when no turn is requested");
        Require(thrusterStates.Where(value => value.ThrusterAxis > 0.99)
                .All(value => value.ThrusterThrust > 0 && Math.Abs(value.ThrusterTorque) > 0.5),
            "thruster state must expose evaluated thrust and placement-derived torque");
        Require(ship.VelocityY > 0 && Math.Abs(ship.VelocityX) < Math.Abs(ship.VelocityY) * 0.1,
            "ordinary thrust must become Ymir-integrated forward velocity");
        Require(ship.Visibility > 1.9,
            "successful thruster execution must publish its maximum plume visibility");
        Require(thrusterStates.Where(value => value.ThrusterAxis > 0.99)
                .Any(value => AetheriaRuntimeThermalSimulation.EquipmentTemperature(
                    ship, catalog, value.OwnerIndex) > ship.EquipmentStates
                    .Single(state => state.EquipmentIndex == value.OwnerIndex).Temperature),
            "successful thruster execution must deposit authored heat into daemon equipment cells");
        Require(!ship.BehaviorStates.Any(value => value.BehaviorKind == AetheriaRuntimeBehaviorKinds.AetherDrive),
            "ordinary flight must not manufacture an AetherDrive behavior");
    }

    private static void UnpoweredThrustersCannotMoveOrAdvertisePlume()
    {
        var ship = Entity(0, 0, "player");
        var catalog = EquipThrusterBank([ship]);
        var thruster = catalog.Items.Single(value => value.ItemKey == "smoke-directional-thruster");
        thruster.BehaviorPayloads.Single().Fields = thruster.BehaviorPayloads.Single().Fields
            .Select(field => field.Key == 4
                ? new AetheriaRuntimeBehaviorField(4, PerformanceStat(10))
                : field)
            .ToArray();
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "unpowered-thruster-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [ship] }]
        };
        var operation = AetheriaRuntimeDaemonOperations.Execute(
            run, [MovementCommand("unpowered-forward", "zone.0.entity.0", 0, 1)]);
        AetheriaRuntimeDaemonSimulation.Step(
            run, operation.Intents, 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, 1);

        RequireNear(0, ship.VelocityX, 0.000001,
            "an unfunded thruster bank must not create lateral velocity");
        RequireNear(0, ship.VelocityY, 0.000001,
            "an unfunded thruster bank must not create forward velocity");
        RequireNear(0, ship.Visibility, 0.000001,
            "an unfunded thruster bank must not advertise a plume that did not execute");
    }

    private static void RareAetherDriveSpoolsOnlyOnModifiedHullFixture()
    {
        var ship = Entity(0, 0, "player");
        var catalog = EquipAetherDrive([ship]);
        var hull = catalog.FindItem(ship.HullItemKey)!;
        var drive = catalog.Items.Single(value => value.ItemKey == "smoke-aether-drive");
        Require(drive.HardpointType == "AetherDrive" &&
                hull.Hardpoints.Count(value => value.Type == drive.HardpointType) == 1,
            "rare AetherDrive fixture must use an explicit modified-hull hardpoint instead of the ordinary hull path");
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "rare-aether-drive-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [ship] }]
        };
        var operation = AetheriaRuntimeDaemonOperations.Execute(
            run, [MovementCommand("rare-drive-forward", "zone.0.entity.0", 0, 1)]);
        var physics = NewPhysics();
        for (var frame = 1; frame <= 2; frame++)
            AetheriaRuntimeDaemonSimulation.Step(
                run, operation.Intents, 0.1,
                AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
                physics, catalog, frame);

        var state = ship.BehaviorStates.Single(value =>
            value.BehaviorKind == AetheriaRuntimeBehaviorKinds.AetherDrive);
        Require(state.AetherDriveRpmX > 0 && state.AetherDriveMaximumRpm > 0,
            "rare installed drive must persist rotor spool state");
        Require(ship.VelocityY > 0,
            "rare installed drive must contribute force only after its rotor has spooled");
        Require(!ship.BehaviorStates.Any(value => value.BehaviorKind == AetheriaRuntimeBehaviorKinds.Thruster),
            "isolated AetherDrive proof must not conceal its force behind ordinary thrusters");
    }

    private static void LookDirectionRejectsInvalidVectorsWithoutMutatingTheShip()
    {
        var player = Entity(0, 0, "player");
        player.DirectionX = 0.6;
        player.DirectionY = 0.8;
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "invalid-look-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [player] }]
        };

        AetheriaRuntimeDaemonCommandDocument Look(string id, double x, double z)
        {
            var command = AetheriaRuntimeDaemonCommandDocument.Create(
                AetheriaRuntimeDaemonCommandKinds.SetLookDirection,
                "look-smoke",
                "look-session",
                0,
                "zone.0.entity.0");
            command.CommandId = id;
            command.DirectionX = x;
            command.PositionZ = z;
            return command;
        }

        var operation = AetheriaRuntimeDaemonOperations.Execute(run,
        [
            Look("zero-look", 0, 0),
            Look("nan-look", double.NaN, 1),
            Look("infinite-look", 1, double.PositiveInfinity)
        ]);

        Require(operation.RejectedCommandIds.SequenceEqual(new[] { "zero-look", "nan-look", "infinite-look" }),
            "zero and non-finite look intents must be rejected at the daemon authority boundary");
        RequireNear(0.6, player.DirectionX, 0.000001,
            "rejected look intents must not mutate the authoritative ship direction X");
        RequireNear(0.8, player.DirectionY, 0.000001,
            "rejected look intents must not mutate the authoritative ship direction Z");
    }

    private static AetheriaRuntimeDaemonCommandDocument MovementCommand(
        string commandId,
        string actor,
        double x,
        double y)
    {
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetMoveVector,
            "shared-control-smoke",
            "shared-control-session",
            0,
            actor);
        command.CommandId = commandId;
        command.DirectionX = x;
        command.DirectionY = y;
        command.ScalarValue = 1;
        return command;
    }

    private static void EnergyNetworkSettlesReactorAfterConsumers()
    {
        var entity = Entity(0, 0, "player");
        entity.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "ledger-capacitor", Quality = 1, Durability = 1, Enabled = true } },
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "ledger-reactor", Quality = 1, Durability = 1, Enabled = true } }
        ];
        entity.BehaviorStates =
        [
            new AetheriaRuntimeBehaviorStateCommit
            {
                OwnerKind = AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind,
                OwnerIndex = 0, BehaviorIndex = 0, BehaviorKind = "Capacitor",
                CapacitorCharge = 2, CapacitorCapacity = 10, CapacitorEfficiency = 0.5
            },
            new AetheriaRuntimeBehaviorStateCommit
            {
                OwnerKind = AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind,
                OwnerIndex = 1, BehaviorIndex = 0, BehaviorKind = "Reactor"
            }
        ];
        entity.StatGrids =
        [
            Grid("hull", 100), Grid("shield", 0), Grid("heat", 280),
            Grid("temperature", 280), Grid("thermal-mass", 1), Grid("conductivity", 1)
        ];
        var capacitorItem = CatalogItem("ledger-capacitor", CapacitorPayload(10, 0.5));
        capacitorItem.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        var reactorPayload = new AetheriaRuntimeBehaviorPayload(0, "Reactor", 100,
        [
            new AetheriaRuntimeBehaviorField(1, PerformanceStat(4)),
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(2)),
            new AetheriaRuntimeBehaviorField(3, PerformanceStat(0.5)),
            new AetheriaRuntimeBehaviorField(4, PerformanceStat(2))
        ]);
        var reactorItem = CatalogItem("ledger-reactor", reactorPayload);
        reactorItem.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        var catalog = new AetheriaRuntimeCatalogSnapshot([capacitorItem, reactorItem], [], []);

        AetheriaRuntimeEnergySimulation.BeginTick(entity, catalog);
        Require(AetheriaRuntimeEnergySimulation.TryConsume(entity, catalog, 5),
            "online reactor must accept residual demand after capacitors drain");
        var capacitor = entity.BehaviorStates.Single(value => value.BehaviorKind == "Capacitor");
        var reactor = entity.BehaviorStates.Single(value => value.BehaviorKind == "Reactor");
        RequireNear(0, capacitor.CapacitorCharge, 0.000001,
            "consumers must drain capacitors before adding reactor draw");
        RequireNear(3, reactor.ReactorDraw, 0.000001,
            "residual demand must accumulate on the last-running reactor");

        AetheriaRuntimeEnergySimulation.SettleReactors(entity, catalog, 1);
        RequireNear(1, capacitor.CapacitorCharge, 0.000001,
            "baseline reactor surplus must refill non-full capacitors after demand settles");
        RequireNear(0, reactor.ReactorDraw, 0.000001,
            "reactor settlement must consume and reset the per-tick demand ledger");
        RequireNear(283.5, GridValue(entity, "temperature", 0), 0.000001,
            "capacitor discharge/refill losses and reactor baseline heat must reach canonical item cells");
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "energy-ledger-smoke", CurrentZoneIndex = 0, CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [entity] }]
        };
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        var worldEntity = Flatten(surface.Surface.Root).Single(node =>
            node.Id == "aetheria.daemon.game.world.entity.0");
        RequireEqual("1", worldEntity.Props["capacitorCharge"],
            "Eve must expose the settled canonical capacitor charge");
        RequireEqual("0", worldEntity.Props["reactorDraw"],
            "Eve must expose the reset post-settlement reactor ledger");
    }

    private static void RadiatorPumpsHeatBeforeReactorSettlement()
    {
        var entity = Entity(0, 0, "player");
        entity.HeatsinksEnabled = true;
        entity.Equipment =
        [
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "radiator-capacitor", Quality = 1, Durability = 1, Enabled = true } },
            new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "radiator", Quality = 1, Durability = 1, Enabled = true } }
        ];
        entity.BehaviorStates =
        [
            new AetheriaRuntimeBehaviorStateCommit
            {
                OwnerKind = AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind,
                OwnerIndex = 0, BehaviorIndex = 0, BehaviorKind = "Capacitor", CapacitorCharge = 10
            },
            new AetheriaRuntimeBehaviorStateCommit
            {
                OwnerKind = AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind,
                OwnerIndex = 1, BehaviorIndex = 0, BehaviorKind = "Radiator", RadiatorTemperature = 280
            }
        ];
        entity.StatGrids =
        [
            Grid("hull", 100), Grid("shield", 0), Grid("heat", 400),
            Grid("temperature", 400), Grid("thermal-mass", 1), Grid("conductivity", 1)
        ];
        var capacitor = CatalogItem("radiator-capacitor", CapacitorPayload(10, 1));
        capacitor.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        var radiatorPayload = new AetheriaRuntimeBehaviorPayload(0, "Radiator", 0,
        [
            new AetheriaRuntimeBehaviorField(1, PerformanceStat(0)),
            new AetheriaRuntimeBehaviorField(2, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(3, Number(300)),
            new AetheriaRuntimeBehaviorField(4, PerformanceStat(0.5)),
            new AetheriaRuntimeBehaviorField(5, PerformanceStat(1)),
            new AetheriaRuntimeBehaviorField(6, PerformanceStat(10))
        ]);
        var radiator = CatalogItem("radiator", radiatorPayload);
        radiator.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        var catalog = new AetheriaRuntimeCatalogSnapshot([capacitor, radiator], [], []);

        AetheriaRuntimeEnergySimulation.BeginTick(entity, catalog);
        AetheriaRuntimeEnergySimulation.StepRadiators(entity, catalog, 1);

        RequireNear(9, entity.BehaviorStates.Single(value => value.BehaviorKind == "Capacitor").CapacitorCharge,
            0.000001, "radiator pump must consume authored energy before reactor settlement");
        RequireNear(300.5, GridValue(entity, "temperature", 0), 0.000001,
            "radiator must remove pumped heat and return authored waste heat to its item cells");
        RequireNear(290, entity.BehaviorStates.Single(value => value.BehaviorKind == "Radiator").RadiatorTemperature,
            0.000001, "pumped heat must accumulate in radiator thermal mass before radiation");
    }

    private static void EquipmentThermalPerformanceOwnsShutdownAndWear()
    {
        var entity = Entity(0, 0, "player");
        entity.ShutdownPerformance = 0.25;
        var item = new AetheriaRuntimeLoadoutItemCommit
            { ItemKey = "thermal-wear-item", Quality = 0, Durability = 10, Enabled = true };
        entity.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit { Item = item }];
        entity.StatGrids =
        [
            Grid("hull", 100), Grid("shield", 0), Grid("heat", 300),
            Grid("temperature", 300), Grid("thermal-mass", 1), Grid("conductivity", 1)
        ];
        var wearPayload = new AetheriaRuntimeBehaviorPayload(0, "Wear", 0,
            [new AetheriaRuntimeBehaviorField(1, new AetheriaRuntimeBehaviorValue(
                "bool", "", 0, true, "", "", [], []))]);
        var typed = CatalogItem("thermal-wear-item", wearPayload);
        typed.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        typed.Durability = 10;
        typed.ThermalResilience = 2;
        typed.MinimumTemperature = 200;
        typed.MaximumTemperature = 400;
        typed.ThermalPerformanceCurveKeys =
        [
            new AetheriaRuntimeCurveKey(0, 1, 0, 0),
            new AetheriaRuntimeCurveKey(1, 0, 0, 0)
        ];
        var catalog = new AetheriaRuntimeCatalogSnapshot([typed], [], []);

        AetheriaRuntimeThermalSimulation.UpdateEquipmentStates(entity, catalog, 1);
        var state = entity.EquipmentStates.Single();
        RequireNear(0.5, state.ThermalPerformance, 0.000001,
            "item thermal performance must come from mean occupied-cell temperature and authored curve");
        Require(state.ThermalOnline && state.Online,
            "item above shutdown performance with positive durability must remain online");
        RequireNear(10 - state.Wear, item.Durability, 0.000001,
            "generic Wear behavior must apply the computed thermal wear potential at its authored cadence");

        var temperature = entity.StatGrids.Single(value => value.Name == "temperature");
        temperature.Values = [380];
        item.OverrideShutdown = false;
        entity.OverrideShutdown = true;
        AetheriaRuntimeThermalSimulation.UpdateEquipmentStates(entity, catalog, 1);
        state = entity.EquipmentStates.Single();
        Require(!state.ThermalOnline && !state.Online,
            "entity override alone must not bypass item thermal shutdown");
        item.OverrideShutdown = true;
        AetheriaRuntimeThermalSimulation.UpdateEquipmentStates(entity, catalog, 1);
        Require(entity.EquipmentStates.Single().ThermalOnline,
            "thermal shutdown bypass must require both entity-wide and item-local override");
    }

    private static void ThermalTopologyComesFromHullAndEquipment()
    {
        var entity = Entity(0, 0, "player");
        entity.HullItemKey = "thermal-hull";
        entity.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            X = 0, Y = 0, Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "thermal-gear", Quality = 1, Durability = 1, Enabled = true }
        }];
        var hull = HullCatalogItem("thermal-hull", 2, 1, 0);
        hull.Mass = 10;
        hull.SpecificHeat = 2;
        hull.Conductivity = 4;
        hull.InteriorShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        var gear = CatalogItem("thermal-gear");
        gear.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        gear.Mass = 4;
        gear.SpecificHeat = 5;
        gear.Conductivity = 3;
        var catalog = new AetheriaRuntimeCatalogSnapshot([hull, gear], [], []);

        AetheriaRuntimeThermalSimulation.EnsureTopology(entity, catalog);

        var mass = entity.StatGrids.Single(value => value.Name == "thermal-mass");
        var conductivity = entity.StatGrids.Single(value => value.Name == "conductivity");
        RequireNear(30, mass.Values[0], 0.000001,
            "occupied cell thermal mass must include its proportional equipment mass");
        RequireNear(10, mass.Values[1], 0.000001,
            "hull thermal mass must be divided across authored hull cells");
        RequireNear(3, conductivity.Values[0], 0.000001,
            "occupied cell conductivity must come from installed equipment");
        RequireNear(280, entity.StatGrids.Single(value => value.Name == "temperature").Values[1], 0.000001,
            "authored hull topology must initialize each occupied cell at the fossil temperature");
    }

    private static void ThermalMedicalExposureUsesCockpitTemperature()
    {
        var entity = Entity(0, 0, "player");
        entity.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "medical-cockpit", Quality = 1, Durability = 1, Enabled = true }
        }];
        entity.StatGrids =
        [
            Grid("hull", 100), Grid("temperature", 340),
            Grid("thermal-mass", 1), Grid("conductivity", 1)
        ];
        var cockpit = CatalogItem("medical-cockpit",
            new AetheriaRuntimeBehaviorPayload(0, "Cockpit", 0, []));
        cockpit.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        var catalog = new AetheriaRuntimeCatalogSnapshot([cockpit], [], []);
        var settings = AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault;

        var heat = AetheriaRuntimeThermalMedicalSimulation.Step(entity, catalog, 300, settings);
        RequireNear(0.3, entity.Heatstroke, 0.000001,
            "cockpit heat must accumulate nonlinear heatstroke at the fossil rate");
        Require(heat.HeatstrokeRiskCrossed,
            "heatstroke must emit one upward severe-risk crossing");
        RequireNear(340, Stat(entity, "cockpit-temperature"), 0.000001,
            "cockpit cell temperature must become an authoritative Eve-readable fact");

        entity.StatGrids.Single(value => value.Name == "temperature").Values = [280];
        AetheriaRuntimeThermalMedicalSimulation.Step(entity, catalog, 1, settings);
        RequireNear(0.1, entity.Heatstroke, 0.000001,
            "heatstroke must recover linearly below the authored threshold");

        entity.StatGrids.Single(value => value.Name == "temperature").Values = [263];
        var cold = AetheriaRuntimeThermalMedicalSimulation.Step(entity, catalog, 300, settings);
        RequireNear(0.3, entity.Hypothermia, 0.000001,
            "cockpit cold must accumulate nonlinear hypothermia at the fossil rate");
        Require(cold.HypothermiaRiskCrossed,
            "hypothermia must use its own exposure for the intended severe-risk crossing");

        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "thermal-medical-surface", CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [entity] }]
        };
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        var worldEntity = Flatten(surface.Surface.Root).Single(node =>
            node.Id == "aetheria.daemon.game.world.entity.0");
        RequireEqual("263", worldEntity.Props["cockpitTemperature"],
            "Eve must publish canonical cockpit temperature rather than deriving it from effects");
        RequireEqual("true", worldEntity.Props["hypothermiaRisk"],
            "Eve must publish the current severe cold-risk state");
        Require(worldEntity.Props.ContainsKey("heatstrokePostWeight") &&
                worldEntity.Props.ContainsKey("severeHeatstrokeWeight"),
            "Eve must publish the exact source weights used by the original heatstroke presentation");
        RequireEqual("5", worldEntity.Props["heatstrokePhasingFrequency"],
            "Eve must publish the provider-authored severe heatstroke pulse frequency");
        RequireEqual("1", worldEntity.Props["deathTransitionSeconds"],
            "Eve must publish the original thermal death crossfade duration");
    }

    private static void ThermalMedicalDeathUsesOrdinaryDestructionPath()
    {
        var entity = Entity(0, 0, "player");
        entity.EntityId = "thermal-pilot";
        entity.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            Item = new AetheriaRuntimeLoadoutItemCommit
                { ItemKey = "lethal-cockpit", Quality = 1, Durability = 1, Enabled = true }
        }];
        entity.StatGrids =
        [
            Grid("hull", 100), Grid("temperature", 700),
            Grid("thermal-mass", 1), Grid("conductivity", 1)
        ];
        var cockpit = CatalogItem("lethal-cockpit",
            new AetheriaRuntimeBehaviorPayload(0, "Cockpit", 0, []));
        cockpit.ShapeCells = [new AetheriaRuntimeShapeCell(0, 0)];
        var catalog = new AetheriaRuntimeCatalogSnapshot([cockpit], [], []);
        var zone = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [entity] };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "thermal-death", CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0", Zones = [zone]
        };

        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics(), catalog, frameId: 17);

        RequireEqual("heatstroke", entity.CauseOfDeath,
            "lethal cockpit heat must persist the typed cause of death");
        Require(!entity.IsActive && !string.IsNullOrWhiteSpace(entity.DestructionId),
            "thermal death must enter the ordinary exactly-once destruction path");
        var death = run.GameEvents.Single(value => value.Kind == "entity.destroyed");
        RequireEqual("heatstroke", death.Reason,
            "Eve feedback must carry the thermal cause needed for the original death transition");
        Require(run.GameEvents.Any(value => value.Kind == "pilot.heatstroke.risk"),
            "a lethal step crossing the severe threshold must publish risk before death");
    }

    private static void ThermalCellsUseFossilConductionAndRadiation()
    {
        var entity = Entity(7, 0, "player");
        entity.StatGrids =
        [
            Grid(AetheriaRuntimeThermalSimulation.TemperatureGrid, 2, 1, 300, 280),
            Grid(AetheriaRuntimeThermalSimulation.ThermalMassGrid, 2, 1, 1, 1),
            Grid(AetheriaRuntimeThermalSimulation.ConductivityGrid, 2, 1, 1, 1)
        ];

        AetheriaRuntimeThermalSimulation.AddHeat(entity, 20);
        RequireNear(310, GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 0), 0.000001,
            "heat energy must be divided across cells and thermal mass");
        RequireNear(290, GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 1), 0.000001,
            "heat energy must be divided across cells and thermal mass");

        AetheriaRuntimeThermalSimulation.Step(entity, 0.1);
        var expectedHot = (310 / 0.01 + 290) / 101;
        expectedHot -= Math.Pow(expectedHot, 3) * 0.00000001 * 0.1;
        var expectedCool = (290 / 0.01 + 310) / 101;
        expectedCool -= Math.Pow(expectedCool, 3) * 0.00000001 * 0.1;
        RequireNear(expectedHot, GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 0), 0.000001,
            "hot cell must follow fossil conduction and radiation");
        RequireNear(expectedCool, GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 1), 0.000001,
            "cool cell must follow fossil conduction and radiation");
        RequireNear((expectedHot + expectedCool) / 2, Stat(entity, "heat"), 0.000001,
            "legacy heat scalar must be derived from cell temperature");

        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "thermal-projection-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.7",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [entity] }]
        };
        var document = AetheriaRuntimeGameDocuments.CurrentEntity(new AetheriaRuntimeDaemonFrameDocument { Run = run });
        RequireNear(Stat(entity, "heat"), document.Hud.MeanTemperature, 0.000001,
            "Eve current-entity state must publish mean temperature");
        RequireNear(GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 0), document.Hud.MaximumTemperature, 0.000001,
            "Eve current-entity state must publish maximum temperature");
        Require(document.Hud.ThermalVisibility > 0, "Eve current-entity state must publish thermal visibility");
    }

    private static void YmirMovesProjectileAndReportsStableContact()
    {
        var (run, zone, target) = Scenario();
        using var physics = NewPhysics();
        physics.RetainWorlds(run.RunId, [zone.ZoneIndex]);
        physics.Step(run.RunId, 0, 0, zone, zone.Entities, 0.1);
        var step = physics.StepPhysicalPayloads(run.RunId, 0, 0, zone, zone.Entities, 0.1);

        RequireEqual("ymir.box3d.retained-session.v1", physics.ImplementationId, "zone physics must identify its retained Ymir implementation");
        RequireEqual(0, step.PhysicalPayloads.Count, "contacted projectile must not survive");
        RequireEqual(1, step.Hits.Count, "Ymir must report one projectile contact");
        RequireEqual(target.EntityIndex, step.Hits[0].TargetEntityIndex, "contact must resolve the daemon entity");
        RequireEqual("aetheria.physical-payload.smoke-projectile", step.Hits[0].PhysicalPayloadBodyId, "physical payload body id must be stable");
        RequireEqual("aetheria.daemon.entity.smoke.entity.2", step.Hits[0].TargetBodyId, "entity body id must be stable");
    }

    private static void PayloadQueryExcludesItsSource()
    {
        var source = Entity(1, 0, "player");
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities = [source],
            PhysicalPayloads =
            [
                new AetheriaRuntimePhysicalPayloadCommit
                {
                    PayloadId = "source-exclusion",
                    SourceEntityIndex = source.EntityIndex,
                    PositionX = 0,
                    PositionZ = 0,
                    VelocityX = 10,
                    Radius = 1,
                    LifetimeSeconds = 5,
                    Active = true
                }
            ]
        };
        using var physics = NewPhysics();
        physics.RetainWorlds("source-exclusion-smoke", [zone.ZoneIndex]);
        physics.Step("source-exclusion-smoke", 1, 0, zone, zone.Entities, 0.1);
        var step = physics.StepPhysicalPayloads("source-exclusion-smoke", 1, 0, zone, zone.Entities, 0.1);

        RequireEqual(0, step.Hits.Count, "a payload query must not hit its source body");
        RequireEqual(1, step.PhysicalPayloads.Count, "source exclusion must leave the payload alive");
    }

    private static void PayloadBodiesDoNotCollideWithEachOther()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities = [],
            PhysicalPayloads =
            [
                new AetheriaRuntimePhysicalPayloadCommit
                {
                    PayloadId = "payload-left",
                    SourceEntityIndex = -1,
                    PositionX = -5,
                    VelocityX = 100,
                    Radius = 1,
                    LifetimeSeconds = 5,
                    Active = true
                },
                new AetheriaRuntimePhysicalPayloadCommit
                {
                    PayloadId = "payload-right",
                    SourceEntityIndex = -1,
                    PositionX = 5,
                    VelocityX = -100,
                    Radius = 1,
                    LifetimeSeconds = 5,
                    Active = true
                }
            ]
        };
        using var physics = NewPhysics();
        physics.RetainWorlds("payload-isolation-smoke", [zone.ZoneIndex]);
        physics.Step("payload-isolation-smoke", 2, 0, zone, zone.Entities, 0.1);
        var step = physics.StepPhysicalPayloads("payload-isolation-smoke", 2, 0, zone, zone.Entities, 0.1);

        RequireEqual(0, step.Hits.Count, "payload bodies must not manufacture world-query hits against each other");
        RequireEqual(2, step.PhysicalPayloads.Count, "payload bodies must survive crossing each other");
        Require(step.PhysicalPayloads.Single(payload => payload.PayloadId == "payload-left").VelocityX > 0,
            "payload-to-payload response must not reverse the left payload");
        Require(step.PhysicalPayloads.Single(payload => payload.PayloadId == "payload-right").VelocityX < 0,
            "payload-to-payload response must not reverse the right payload");
    }

    private static void OverlappingMineRemainsQueryableAfterArming()
    {
        var target = Entity(2, 0, "enemy");
        var mine = new AetheriaRuntimePhysicalPayloadCommit
        {
            PayloadId = "arming-overlap",
            PayloadKind = "mine",
            SourceEntityIndex = 1,
            PositionX = 0,
            PositionZ = 0,
            Radius = 1,
            TriggerRadius = 25,
            ActivationDelaySeconds = 1,
            LifetimeSeconds = 10,
            Stationary = false,
            Active = true
        };
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities = [target],
            PhysicalPayloads = [mine]
        };
        using var physics = NewPhysics();
        physics.RetainWorlds("mine-arming-overlap-smoke", [zone.ZoneIndex]);
        physics.Step("mine-arming-overlap-smoke", 1, 0, zone, zone.Entities, 0.5);
        var unarmed = physics.StepPhysicalPayloads("mine-arming-overlap-smoke", 1, 0, zone, zone.Entities, 0.5);
        RequireEqual(1, unarmed.Hits.Count, "an unarmed overlap may be observed without granting Ymir trigger authority");

        mine.AgeSeconds = mine.ActivationDelaySeconds;
        mine.Radius = mine.TriggerRadius;
        zone.PhysicalPayloads = unarmed.PhysicalPayloads;
        physics.Step("mine-arming-overlap-smoke", 2, 0, zone, zone.Entities, 0.5);
        var armed = physics.StepPhysicalPayloads("mine-arming-overlap-smoke", 2, 0, zone, zone.Entities, 0.5);
        RequireEqual(1, armed.Hits.Count,
            "a retained mine already overlapping at activation must still yield an eligible Box3D overlap fact");
        RequireEqual(target.EntityIndex, armed.Hits[0].TargetEntityIndex,
            "the armed overlap fact must preserve the target identity");
    }

    private static void DaemonSimulationTreatsYmirHitAsPresentationOnly()
    {
        var (run, _, target) = Scenario();

        AetheriaRuntimeDaemonSimulation.Step(
            run,
            new AetheriaRuntimeDaemonIntentState(),
            0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            NewPhysics());

        RequireEqual(100.0, Stat(target, "hull"), "presentation projectile contact must not write combat damage");
        RequireEqual(0, run.Zones[0].PhysicalPayloads.Count, "spent projectile must leave daemon state");
        RequireEqual(1, run.GameEvents.Count(value => value.Kind == "physical-payload.contact" && value.SubjectKey == "smoke-projectile"),
            "Ymir contact must emit one projectile-identity impact event");
        RequireEqual(0, run.GameEvents.Count(value => value.Kind == "entity.damaged" && value.TargetEntityIndex == target.EntityIndex),
            "presentation projectile contact must not manufacture a damage event");
        var feedback = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 0, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        Require(Flatten(feedback.Surface.Root).Any(node => node.Kind == "feedback.event" && node.Props["eventKind"] == "physical-payload.contact" && node.Props["subjectKey"] == "smoke-projectile"),
            "Eve feedback must project authoritative projectile impact identity");
    }

    private static void ProjectileContactCannotKill()
    {
        var (run, _, target) = Scenario();
        target.StatGrids.Single(grid => grid.Name == "hull").Values = [5];
        AetheriaRuntimeDaemonSimulation.Step(run, new AetheriaRuntimeDaemonIntentState(), 0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault, NewPhysics(), frameId: 9);
        Require(target.IsActive && Math.Abs(Stat(target, "hull") - 5) < 0.000001,
            "even nominally lethal presentation contact must leave canonical target state untouched");
        RequireEqual(0, run.GameEvents.Count(value => value.Kind == "entity.destroyed" && value.TargetEntityIndex == target.EntityIndex),
            "presentation contact must not manufacture a destruction event");
    }

    private static void MissingWorldPhysicsOwnerCannotAdvanceShips()
    {
        var entity = Entity(0, 0, "player");
        entity.VelocityX = 10;
        var run = new AetheriaRuntimeRunCheckpointCommit { CurrentZoneIndex = 0, CurrentEntityKey = "zone.0.entity.0", Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [entity] }] };
        try
        {
            AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-missing-world-physics.cc"), run,
                new AetheriaRuntimeDaemonTickOptions { FrameId = 1, FixedDeltaSeconds = 0.1, BuildPublications = false });
        }
        catch (InvalidOperationException)
        {
            RequireNear(0, entity.PositionX, 0.000001, "ship must not advance without Ymir world authority");
            return;
        }
        throw new InvalidOperationException("daemon advanced a ship without an authoritative world physics owner");
    }

    private static void TractorRampsAndPullsThroughYmirWithoutTeleportingCargo()
    {
        var ship = Entity(0, 0, "player");
        ship.DirectionX = 1; ship.DirectionY = 0;
        var pickup = new AetheriaRuntimeDroppedPickupCommit { PickupIndex = 3, PositionX = 60, Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = "salvage", Quantity = 1 }, LifetimeSeconds = 30 };
        var run = new AetheriaRuntimeRunCheckpointCommit { CurrentZoneIndex = 0, CurrentEntityKey = "zone.0.entity.0", Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [ship], DroppedPickups = [pickup] }] };
        var command = AetheriaRuntimeDaemonCommandDocument.Create(AetheriaRuntimeDaemonCommandKinds.SetTractorPower, "pilot", "tractor-smoke", 0, "zone.0.entity.0");
        command.CommandId = "tractor-on"; command.ScalarValue = 1;
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-tractor-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = NewPhysics(), FrameId = 1, FixedDeltaSeconds = 0.25, SimulationTimeSeconds = 0.25, ObservedCommands = [command], BuildPublications = false });
        RequireNear(0.5, ship.TractorPower, 0.000001, "tractor power must use the fossil two-per-second ramp");
        Require(pickup.VelocityX < 0 && pickup.PositionX < 60, "Ymir must pull a pickup inside the forward tractor volume toward the ship");
        RequireEqual(1, pickup.Item.Quantity, "tractor force must not consume the pickup item");
        RequireEqual(0, CargoQuantity(ship, "salvage"), "scooping must remain a separate capacity-checked transaction");
        Require(pickup.AgeSeconds > 0 && pickup.AgeSeconds < pickup.LifetimeSeconds,
            "daemon-owned pickup lifetime must advance independently of its SoA presentation");
        var release = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetTractorPower,
            "pilot",
            "tractor-smoke",
            1,
            "zone.0.entity.0");
        release.CommandId = "tractor-off";
        release.ScalarValue = 0;
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-tractor-release-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = NewPhysics(), FrameId = 2, FixedDeltaSeconds = 0.25, SimulationTimeSeconds = 0.5, ObservedCommands = [release], BuildPublications = false });
        RequireNear(0, ship.TractorTargetPower, 0.000001, "released tractor input must set daemon target power to zero");
        RequireNear(0, ship.TractorPower, 0.000001, "daemon must ramp released tractor power back to zero at the fossil rate");
        var inputCapability = AetheriaRuntimeInputCapabilityDocument
            .FromFrame(new AetheriaRuntimeDaemonFrameDocument { FrameId = 2, Run = run })
            .ToEveDocument();
        var scoopAction = inputCapability.Actions.Single(action => action.ActionId == "pilot.scoop");
        Require(scoopAction.InputValue?.Model == "button-hold.v1" &&
                scoopAction.InputValue.PayloadKey == "scalarValue" &&
                scoopAction.Payload["scalarValue"] == "0",
            "tractor input must advertise press/release scalar ownership instead of a one-shot on command");
        ship.TractorPower = 0.5;
        ship.TractorTargetPower = 1;
        var surface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        var beam = Flatten(surface.Surface.Root).Single(node => node.Kind == "beam.presentation");
        Require(beam.Props["sourceEntityId"] == run.EntityRecordKey(0, ship.EntityIndex) &&
                beam.Props["assetRole"] == "effect.beam.tractor" &&
                beam.Props["directionMode"] == "source-forward.v1" &&
                beam.Props["activationActionId"] == "pilot.scoop" &&
                beam.Props["power"] == "0.5" && beam.Props["radius"] == "25" &&
                beam.Props["maximumDistance"] == "75",
            "Eve beam presentation must project the same daemon tractor power and fossil volume without owning force or contact");

        double PulledVelocity(double power, int frameId)
        {
            var actor = Entity(0, 0, "player");
            actor.DirectionX = 1;
            actor.DirectionY = 0;
            actor.TractorPower = power;
            actor.TractorTargetPower = power;
            var cargo = new AetheriaRuntimeDroppedPickupCommit
            {
                PickupIndex = 30 + frameId,
                PositionX = 60,
                Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = "salvage", Quantity = 1 },
                LifetimeSeconds = 30
            };
            var scenario = new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = $"tractor-power-{frameId}",
                CurrentZoneIndex = 0,
                CurrentEntityKey = "zone.0.entity.0",
                Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [actor], DroppedPickups = [cargo] }]
            };
            AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), $"aetheria-tractor-power-{frameId}.cc"),
                scenario,
                new AetheriaRuntimeDaemonTickOptions
                {
                    WorldPhysics = NewPhysics(), FrameId = frameId, FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frameId * 0.1, BuildPublications = false
                });
            return cargo.VelocityX;
        }

        RequireNear(PulledVelocity(0.02, 20), PulledVelocity(1, 21), 0.000001,
            "tractor power above the fossil threshold must gate full traction rather than scale force");
        var pickupAsset = AetheriaRuntimeAssets.ProjectManifest(null).Assets.Single(asset => asset.Ref.AssetKey == "prefab.entity.pickup");
        Require(pickupAsset.Ref.Metadata.TryGetValue("unityAssetPath", out var pickupPath) && pickupPath == "Assets/Prefabs/RPG/Pickups/Tetrahedron.prefab",
            "provider asset manifest must advertise the pickup visual used by Eve");
        var assets = AetheriaRuntimeAssets.ProjectManifest(null).Assets;
        var playerAsset = assets.Single(asset => asset.Ref.AssetKey == "prefab.entity.player");
        RequireEqual("player", playerAsset.Ref.Metadata["presentationRole"],
            "entity prefab fallback must use a specific presentation role instead of the generic world tag");
        var boltEffect = assets.Single(asset => asset.Ref.AssetKey == "prefab.effect.shot.bolt");
        RequireEqual("effect.shot.bolt", boltEffect.Ref.Metadata["presentationRole"],
            "provider manifest must bind semantic shot feedback to its graduated EveUnity effect");
        var tractorEffect = assets.Single(asset => asset.Ref.AssetKey == "prefab.effect.beam.tractor");
        RequireEqual("effect.beam.tractor", tractorEffect.Ref.Metadata["presentationRole"],
            "provider manifest must bind the stripped fossil tractor prefab to the generic beam role");
        var thermalProfiles = assets.Where(asset =>
            string.Equals(asset.Ref.Kind, AetheriaRuntimeAssetKinds.VolumeProfile, StringComparison.Ordinal) &&
            asset.Ref.Metadata.TryGetValue("presentationRole", out var role) &&
            (role.StartsWith("post.thermal.", StringComparison.Ordinal) ||
             string.Equals(role, "post.death", StringComparison.Ordinal))).ToArray();
        RequireEqual(5, thermalProfiles.Length,
            "provider manifest must advertise every original thermal and death volume profile");
        Require(thermalProfiles.All(asset =>
                asset.Ref.Metadata.ContainsKey("unityAssetPath") &&
                asset.Ref.Metadata.ContainsKey("presentationRole")),
            "thermal profiles must cross the CDN boundary with exact Unity paths and semantic roles");
    }

    private static void PickupIsCapacityCheckedExactlyOnceAndExpires()
    {
        var hull = CatalogItem("pickup-hull"); hull.HullCapacity = PerformanceStat(1);
        var salvage = CatalogItem("salvage"); salvage.Volume = 1;
        var cargoBay = CatalogItem("pickup-cargo-bay"); cargoBay.InteriorOccupiedCells = 1;
        var catalog = new AetheriaRuntimeCatalogSnapshot([hull, salvage, cargoBay], [], []);
        var ship = Entity(0, 0, "player"); ship.HullItemKey = hull.ItemKey; ship.CargoContents = [Cargo()];
        ship.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = cargoBay.ItemKey, Quantity = 1 } }];
        var zone = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [ship], DroppedPickups = [new AetheriaRuntimeDroppedPickupCommit { PickupIndex = 7, PositionX = 10, Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = salvage.ItemKey, Quantity = 1 }, LifetimeSeconds = 30 }] };
        var run = new AetheriaRuntimeRunCheckpointCommit { CurrentZoneIndex = 0, CurrentEntityKey = "zone.0.entity.0", Zones = [zone] };
        var forbiddenCommand = AetheriaRuntimeDaemonCommandDocument.Create(AetheriaRuntimeDaemonCommandKinds.PickUpLoot, "pilot", "pickup-smoke", 0, "zone.0.entity.0");
        forbiddenCommand.CommandId = "pickup-command-forbidden";
        forbiddenCommand.TargetEntityKey = "zone.0.entity.0";
        var forbidden = AetheriaRuntimeDaemonOperations.Execute(run, [forbiddenCommand], new AetheriaRuntimeDaemonOperationContext { Catalog = catalog });
        Require(forbidden.RejectedCommandIds.Contains(forbiddenCommand.CommandId),
            "client pickup commands must not own cargo collection");

        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-pickup-no-contact.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = new ScriptedWorldPhysics(), FrameId = 1, FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 0.1, Catalog = catalog, BuildPublications = false });
        RequireEqual(1, zone.DroppedPickups.Count, "nearby pickup without a Ymir contact fact must remain in the world");
        RequireEqual(0, CargoQuantity(ship, salvage.ItemKey), "proximity must not mutate cargo");

        var forgedContact = new AetheriaRuntimeWorldBeginContact
        {
            FactId = "ymir-fact-forged-identity",
            EntityAIndex = 0,
            EntityAId = "another-entity",
            PickupIndex = 7,
            NormalX = 1
        };
        var rejectedForgedIdentity = false;
        try
        {
            AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-pickup-forged-identity.cc"), run,
                new AetheriaRuntimeDaemonTickOptions { WorldPhysics = new ScriptedWorldPhysics(forgedContact), FrameId = 2, FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 0.2, Catalog = catalog, BuildPublications = false });
        }
        catch (InvalidOperationException)
        {
            rejectedForgedIdentity = true;
        }
        Require(rejectedForgedIdentity, "a Ymir contact whose stable identity disagrees with the live entity must fail closed");
        RequireEqual(1, zone.DroppedPickups.Count, "forged contact identity must not remove the pickup");
        RequireEqual(0, CargoQuantity(ship, salvage.ItemKey), "forged contact identity must not mutate cargo");
        RequireEqual(0, run.PickupContactReceipts.Count, "forged contact identity must not mint a consumption receipt");

        var contact = new AetheriaRuntimeWorldBeginContact
        {
            FactId = "ymir-fact-pickup-7",
            EntityAIndex = 0,
            EntityAId = ship.EntityId,
            PickupIndex = 7,
            NormalX = 1
        };
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-pickup-contact-dedup.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = new ScriptedWorldPhysics(contact, contact), FrameId = 3, FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 0.3, Catalog = catalog, BuildPublications = false });
        RequireEqual(0, zone.DroppedPickups.Count, "one Ymir contact must consume the pickup");
        RequireEqual(1, CargoQuantity(ship, salvage.ItemKey), "duplicate contact facts must commit cargo exactly once");
        RequireEqual(1, run.GameEvents.Count(value => value.Kind == "pickup.collected" && value.PickupIndex == 7),
            "duplicate contact facts must emit one collection event");
        var collectionEvent = run.GameEvents.Single(value => value.Kind == "pickup.collected" && value.PickupIndex == 7);
        RequireNear(1, collectionEvent.ScalarValue, 0.000001,
            "collection feedback must publish the committed cargo delta");
        RequireNear(0, collectionEvent.AuxiliaryValue, 0.000001,
            "collection feedback must publish cargo quantity before the transaction");
        RequireEqual(1, run.PickupContactReceipts.Count(value => value.FactId == contact.FactId),
            "duplicate delivery of one Ymir fact must persist one Aetheria consumption receipt");
        var collectionFeedback = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 3, Run = run },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        Require(Flatten(collectionFeedback.Surface.Root).Any(node => node.Kind == "feedback.event" &&
                node.Props["eventKind"] == "pickup.collected" && node.Props["pickupIndex"] == "7" &&
                node.Props["itemKey"] == salvage.ItemKey && node.Props["scalarValue"] == "1" &&
                node.Props["cargoQuantityBefore"] == "0" && node.Props["cargoQuantityAfter"] == "1"),
            "Eve feedback must publish the exact cargo delta committed by one Ymir contact fact");

        zone.DroppedPickups = [new AetheriaRuntimeDroppedPickupCommit { PickupIndex = 8, PositionX = 10, Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = salvage.ItemKey, Quantity = 1 }, LifetimeSeconds = 30 }];
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-pickup-expiry-smoke.cc"), run,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = new ScriptedWorldPhysics(), FrameId = 4, FixedDeltaSeconds = 30, SimulationTimeSeconds = 30, BuildPublications = false });
        RequireEqual(0, zone.DroppedPickups.Count, "pickup must expire after the fossil thirty-second lifetime");
        Require(run.GameEvents.Any(value => value.Kind == "pickup.expired" && value.PickupIndex == 8),
            "daemon lifetime owner must emit authoritative pickup expiry event");
    }

    private static void TradePurchaseDerivesAcceptanceFromDaemonState()
    {
        var ore = CatalogItem("trade-ore"); ore.Price = 40; ore.Volume = 1;
        var cargoBay = CatalogItem("trade-cargo-bay"); cargoBay.InteriorOccupiedCells = 10;
        var catalog = new AetheriaRuntimeCatalogSnapshot([ore, cargoBay], [], []);
        var station = Entity(0, 0, "station");
        station.Kind = "station";
        station.CargoContents = [Cargo((ore.ItemKey, 10, 2, 3))];
        station.DockingBayAssignments = [1];
        station.ChildEntityIndices = [1];
        var ship = Entity(1, 0, "player");
        ship.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = cargoBay.ItemKey, Quantity = 1 }
        }];
        ship.CargoContents = [Cargo()];
        var zone = new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [station, ship] };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "trade-authority-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.1",
            Credits = 5000,
            Zones = [zone]
        };
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TradePurchase,
            "pilot",
            "trade-authority-smoke",
            1,
            run.CurrentEntityKey);
        command.TradePurchase.ItemKey = ore.ItemKey;
        command.TradePurchase.Quantity = 5;
        command.TradePurchase.StationCargoIndex = 0;
        command.TradePurchase.TargetCargoIndex = 0;
        command.TradePurchase.SourceX = 2;
        command.TradePurchase.SourceY = 3;
        command.TradePurchase.PurchaseKind = "docked_ship";
        command.TradePurchase.UnitPrice = -999;
        command.TradePurchase.TotalPrice = -999;
        command.TradePurchase.StationEntityKey = "forged-station";
        command.TradePurchase.TargetEntityKey = "forged-target";
        command.TradePurchase.CreatesDockedShip = true;

        Require(AetheriaRuntimeRunCheckpointCommit.TryParseEntityKey(run.CurrentEntityKey, out _, out _),
            "trade smoke current entity key must be daemon-resolvable");
        RequireEqual(10, AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(ship, catalog, ore.ItemKey, 0),
            "trade smoke cargo fixture must have room for the requested commodity");
        var daemonUnitPrice = AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
            ore,
            station.CargoContents[0].Items[0].Item,
            catalog.TradeValueSettings).Price;
        Require(daemonUnitPrice > 0, "trade smoke daemon catalog price must be positive");

        var result = AetheriaRuntimeDaemonOperations.Execute(
            run,
            [command],
            new AetheriaRuntimeDaemonOperationContext { Catalog = catalog });

        Require(result.AppliedCommandIds.Contains(command.CommandId),
            "daemon must accept a valid commodity purchase regardless of forged compatibility opinions");
        RequireEqual(5000 - daemonUnitPrice * 5, run.Credits,
            "daemon catalog price must own trade credit mutation");
        RequireEqual(5, CargoQuantity(ship, ore.ItemKey), "daemon cargo capacity and placement must own delivery");
        RequireEqual(2, zone.Entities.Count, "forged ship-creation opinion must not materialize an entity");
    }

    private static void StationBodiesCannotConsumePickups()
    {
        var hull = CatalogItem("station-filter-hull"); hull.HullCapacity = PerformanceStat(1);
        var salvage = CatalogItem("station-filter-salvage"); salvage.Volume = 1;
        var cargoBay = CatalogItem("station-filter-cargo-bay"); cargoBay.InteriorOccupiedCells = 1;
        var catalog = new AetheriaRuntimeCatalogSnapshot([hull, salvage, cargoBay], [], []);
        var station = Entity(0, 0, "player");
        station.Kind = "station";
        station.HullItemKey = hull.ItemKey;
        station.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
        {
            Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = cargoBay.ItemKey, Quantity = 1 }
        }];
        station.CargoContents = [Cargo()];
        var ship = Entity(1, 200, "player");
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.1",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit
            {
                ZoneIndex = 0,
                Entities = [station, ship],
                DroppedPickups = [new AetheriaRuntimeDroppedPickupCommit
                {
                    PickupIndex = 9,
                    PositionX = 0,
                    Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = salvage.ItemKey, Quantity = 1 },
                    LifetimeSeconds = 30
                }]
            }]
        };

        using var physics = NewPhysics();
        AetheriaRuntimeDaemonTickRunner.Tick(
            Path.Combine(Path.GetTempPath(), "aetheria-pickup-station-filter.cc"),
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                WorldPhysics = physics,
                FrameId = 1,
                FixedDeltaSeconds = 0.1,
                SimulationTimeSeconds = 0.1,
                Catalog = catalog,
                BuildPublications = false
            });

        RequireEqual(1, run.Zones[0].DroppedPickups.Count,
            "station world bodies must not consume ship-scoop pickups");
        RequireEqual(0, run.PickupContactReceipts.Count,
            "Ymir collision filtering must not publish station/pickup Begin facts");
        RequireEqual(0, CargoQuantity(station, salvage.ItemKey),
            "station cargo must remain unchanged without a ship shield contact");
    }

    private static void PickupShieldContactCollectsOrBounces()
    {
        var hull = CatalogItem("contact-hull"); hull.HullCapacity = PerformanceStat(1);
        var salvage = CatalogItem("contact-salvage"); salvage.Volume = 1;
        var cargoBay = CatalogItem("contact-cargo-bay"); cargoBay.InteriorOccupiedCells = 1;
        var catalog = new AetheriaRuntimeCatalogSnapshot([hull, salvage, cargoBay], [], []);
        AetheriaRuntimeRunCheckpointCommit Scenario(bool full)
        {
            var ship = Entity(0, 0, "player"); ship.HullItemKey = hull.ItemKey;
            ship.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit { Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = cargoBay.ItemKey, Quantity = 1 } }];
            ship.CargoContents = [full ? Cargo((salvage.ItemKey, 1, 0, 0)) : Cargo()];
            return new AetheriaRuntimeRunCheckpointCommit { CurrentZoneIndex = 0, CurrentEntityKey = "zone.0.entity.0", Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [ship], DroppedPickups = [new AetheriaRuntimeDroppedPickupCommit { PickupIndex = 10, PositionX = 20, Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = salvage.ItemKey, Quantity = 1 }, LifetimeSeconds = 30 }] }] };
        }
        var open = Scenario(false);
        using var openPhysics = NewPhysics();
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-pickup-contact-open.cc"), open,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = openPhysics, FrameId = 1, FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 0.1, Catalog = catalog, BuildPublications = false });
        RequireEqual(0, open.Zones[0].DroppedPickups.Count, "shield contact with capacity must collect pickup automatically");
        RequireEqual(1, CargoQuantity(open.Zones[0].Entities[0], salvage.ItemKey), "contact collection must commit cargo once");
        RequireEqual(1, open.GameEvents.Count(value => value.Kind == "pickup.collected" && value.PickupIndex == 10),
            "contact collection must emit one stable event");
        RequireEqual(1, open.PickupContactReceipts.Count,
            "a real Box3D Begin fact must persist one cargo-consumption receipt");

        var full = Scenario(true);
        using var fullPhysics = NewPhysics();
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-pickup-contact-full.cc"), full,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = fullPhysics, FrameId = 1, FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 0.1, Catalog = catalog, BuildPublications = false });
        RequireEqual(1, full.Zones[0].DroppedPickups.Count, "full hold must leave contacted pickup alive");
        Require(full.Zones[0].DroppedPickups[0].VelocityX > 20, "failed pickup must receive the fossil outward kick");
        RequireEqual(1, full.GameEvents.Count(value => value.Kind == "pickup.rejected" && value.PickupIndex == 10),
            "capacity rejection must emit one stable event");
        var rejectionEvent = full.GameEvents.Single(value => value.Kind == "pickup.rejected" && value.PickupIndex == 10);
        RequireEqual("cargo-capacity", rejectionEvent.Reason,
            "capacity rejection feedback must name the provider-owned reason");
        RequireNear(1, rejectionEvent.AuxiliaryValue, 0.000001,
            "capacity rejection feedback must preserve the pre-contact cargo quantity");
        RequireEqual(1, full.PickupContactReceipts.Count,
            "capacity rejection must consume its Box3D Begin fact exactly once");
        AetheriaRuntimeDaemonTickRunner.Tick(Path.Combine(Path.GetTempPath(), "aetheria-pickup-contact-full-second.cc"), full,
            new AetheriaRuntimeDaemonTickOptions { WorldPhysics = fullPhysics, FrameId = 2, FixedDeltaSeconds = 0.1, SimulationTimeSeconds = 0.2, Catalog = catalog, BuildPublications = false });
        RequireEqual(1, full.PickupContactReceipts.Count,
            "persistent or separating contact must not replay an already consumed Begin fact");
        RequireEqual(1, full.GameEvents.Count(value => value.Kind == "pickup.rejected" && value.PickupIndex == 10),
            "a consumed rejection fact must not emit a second gameplay event");
        var feedback = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
            new AetheriaRuntimeDaemonFrameDocument { FrameId = 1, Run = full },
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("daemon"));
        Require(Flatten(feedback.Surface.Root).Any(node => node.Kind == "feedback.event" &&
                node.Props["eventKind"] == "pickup.rejected" && node.Props["pickupIndex"] == "10" &&
                node.Props["reason"] == "cargo-capacity" &&
                node.Props["cargoQuantityBefore"] == "1" && node.Props["cargoQuantityAfter"] == "1"),
            "Eve surface must project authoritative pickup feedback chronology");
    }

    private static (AetheriaRuntimeRunCheckpointCommit Run, AetheriaRuntimeZoneSnapshotCommit Zone, AetheriaRuntimeEntitySnapshotCommit Target) Scenario()
    {
        var source = Entity(1, -100, "player");
        var target = Entity(2, 30, "enemy");
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities = [source, target],
            PhysicalPayloads =
            [
                new AetheriaRuntimePhysicalPayloadCommit
                {
                    PayloadId = "smoke-projectile",
                    SourceEntityIndex = 1,
                    TargetEntityIndex = 2,
                    PositionX = 0,
                    PositionZ = 0,
                    VelocityX = 100,
                    VelocityY = 0,
                    Radius = 1,
                    ContactMagnitude = 12,
                    LifetimeSeconds = 5,
                    Active = true
                }
            ]
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "ymir-projectile-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.1",
            Zones = [zone]
        };
        return (run, zone, target);
    }

    private static AetheriaRuntimeEntitySnapshotCommit Entity(int index, double x, string faction) => new()
    {
        EntityIndex = index,
        EntityId = $"smoke.entity.{index}",
        Kind = "ship",
        FactionKey = faction,
        PositionX = x,
        PositionZ = 0,
        TargetEntityIndex = -1,
        IsActive = true,
        StatGrids = [Grid("hull", 100), Grid("shield", 0), Grid("heat", 0)]
    };

    private static AetheriaRuntimeEntityStatGridCommit Grid(string name, double value) => new()
    {
        Name = name,
        Width = 1,
        Height = 1,
        Values = [value]
    };

    private static AetheriaRuntimeEntityStatGridCommit Grid(string name, int width, int height, params double[] values) => new()
    {
        Name = name,
        Width = width,
        Height = height,
        Values = values
    };

    private static double GridValue(AetheriaRuntimeEntitySnapshotCommit entity, string name, int index) =>
        entity.StatGrids.Single(grid => string.Equals(grid.Name, name, StringComparison.OrdinalIgnoreCase)).Values[index];

    private static IEnumerable<AetheriaRuntimeSurfaceComponent> Flatten(AetheriaRuntimeSurfaceComponent root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var descendant in Flatten(child))
            yield return descendant;
    }

    private static double Stat(AetheriaRuntimeEntitySnapshotCommit entity, string name) =>
        entity.StatGrids.Single(grid => string.Equals(grid.Name, name, StringComparison.OrdinalIgnoreCase)).Values[0];

    private static void RequireEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}. Expected {expected}; actual {actual}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireNear(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message}. Expected {expected}; actual {actual}.");
    }

    private static void YmirRestartDoesNotReplayConsumedPickupContact()
    {
        var root = Path.Combine(Path.GetTempPath(), "aetheria-ymir-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var statePath = Path.Combine(root, "world.cc");
        try
        {
            var hull = CatalogItem("restart-contact-hull"); hull.HullCapacity = PerformanceStat(1);
            var salvage = CatalogItem("restart-contact-salvage"); salvage.Volume = 1;
            var cargoBay = CatalogItem("restart-contact-cargo-bay"); cargoBay.InteriorOccupiedCells = 1;
            var catalog = new AetheriaRuntimeCatalogSnapshot([hull, salvage, cargoBay], [], []);
            var ship = Entity(0, 0, "player"); ship.HullItemKey = hull.ItemKey;
            ship.Equipment = [new AetheriaRuntimeLoadoutItemSlotCommit
            {
                Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = cargoBay.ItemKey, Quantity = 1 }
            }];
            ship.CargoContents = [Cargo((salvage.ItemKey, 1, 0, 0))];
            var run = new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = "ymir-restart-contact-smoke",
                CurrentZoneIndex = 0,
                CurrentEntityKey = "zone.0.entity.0",
                Zones = [new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    Entities = [ship],
                    DroppedPickups = [new AetheriaRuntimeDroppedPickupCommit
                    {
                        PickupIndex = 10,
                        PositionX = 20,
                        Item = new AetheriaRuntimeLoadoutItemCommit { ItemKey = salvage.ItemKey, Quantity = 1 },
                        LifetimeSeconds = 30
                    }]
                }]
            };
            var frame = new AetheriaRuntimeDaemonFrameDocument
            {
                FrameId = 1,
                FixedDeltaSeconds = 0.1,
                SimulationTimeSeconds = 0.1,
                Run = run
            };

            using (var physics = new AetheriaYmirWorldPhysics())
            {
                AetheriaRuntimeDaemonTickRunner.Tick(statePath, run,
                    new AetheriaRuntimeDaemonTickOptions
                    {
                        WorldPhysics = physics,
                        FrameId = 1,
                        FixedDeltaSeconds = 0.1,
                        SimulationTimeSeconds = 0.1,
                        Catalog = catalog,
                        BuildPublications = false
                    });
                RequireEqual(1, run.PickupContactReceipts.Count,
                    "pre-restart rejection must consume exactly one Box3D Begin fact");
                var rejectedVelocity = run.Zones[0].DroppedPickups.Single().VelocityX;
                Require(rejectedVelocity > 20, "pre-restart rejection must persist the outward kick in world truth");

                frame.Run = run;
                using var node = AetheriaStateNode.OpenAsync(statePath).GetAwaiter().GetResult();
                using var persistence = AetheriaYmirPersistenceCoordinator.OpenAsync(node, physics, null)
                    .GetAwaiter().GetResult();
                var capture = persistence.Capture(frame);
                persistence.PersistPrivateAsync(capture).GetAwaiter().GetResult();
                node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
                    .ReplaceAsync(frame).GetAwaiter().GetResult();
                node.FlushAsync(soft: false).GetAwaiter().GetResult();
                persistence.ActivateAsync().GetAwaiter().GetResult();
                Require(!AetheriaDocumentRegistry.DocumentTypes.Contains(typeof(AetheriaYmirResumeDocument)),
                    "daemon-private Ymir resume types must not enter the public Aetheria registry");
                Require(!node.Cache.AllEntries.Any(value => value is AetheriaYmirResumeDocument or AetheriaYmirJournalChunkDocument),
                    "public CultMesh cache must not enumerate daemon-private Ymir restart material");
            }

            using (var node = AetheriaStateNode.OpenAsync(statePath).GetAwaiter().GetResult())
            {
                var durable = node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
                    .ReadAsync().GetAwaiter().GetResult()
                    ?? throw new InvalidOperationException("Durable restart smoke frame is missing.");
                using var restoredPhysics = new AetheriaYmirWorldPhysics();
                using var persistence = AetheriaYmirPersistenceCoordinator.OpenAsync(node, restoredPhysics, durable)
                    .GetAwaiter().GetResult();
                AetheriaRuntimeDaemonTickRunner.Tick(statePath, durable.Run,
                    new AetheriaRuntimeDaemonTickOptions
                    {
                        WorldPhysics = restoredPhysics,
                        FrameId = 2,
                        FixedDeltaSeconds = 0.1,
                        SimulationTimeSeconds = 0.2,
                        Catalog = catalog,
                        BuildPublications = false
                    });
                RequireEqual(1, durable.Run.PickupContactReceipts.Count,
                    "restart must not replay an already consumed Box3D Begin fact");
                RequireEqual(1, durable.Run.GameEvents.Count(value =>
                        value.Kind == "pickup.rejected" && value.PickupIndex == 10),
                    "restart must not duplicate authoritative pickup rejection feedback");
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ScriptedWorldPhysics(params AetheriaRuntimeWorldBeginContact[] contacts) : IAetheriaRuntimeWorldPhysics
    {
        public string ImplementationId => "smoke.scripted-world";

        public void RetainWorlds(string runId, IReadOnlyList<int> zoneIndices) { }

        public AetheriaRuntimeWorldPickupStep ApplyPickupRejection(
            string runId,
            int zoneIndex,
            AetheriaRuntimeWorldBeginContact contact) =>
            new() { PickupIndex = contact.PickupIndex, VelocityX = contact.NormalX * 25, VelocityZ = contact.NormalZ * 25 };

        public AetheriaRuntimeWorldStep Step(
            string runId,
            long frameId,
            int simulationStepIndex,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds) => new([], [], contacts);

        public AetheriaRuntimePhysicalPayloadStep StepPhysicalPayloads(
            string runId,
            long frameId,
            int simulationStepIndex,
            AetheriaRuntimeZoneSnapshotCommit zone,
            IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
            double deltaSeconds) => new(zone.PhysicalPayloads, []);
    }
}
