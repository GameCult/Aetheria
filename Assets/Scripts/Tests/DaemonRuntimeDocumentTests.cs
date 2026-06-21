using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class DaemonRuntimeDocumentTests
{
    [Test]
    public void FrameFactoryMarksDaemonAsAuthoritativeStateSource()
    {
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "run-1",
            CurrentZoneIndex = 3,
            CurrentEntityKey = "entity:player"
        };

        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            run,
            "ymir-daemon",
            "session-1",
            42,
            12.5,
            0.02);

        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Frame, frame.Schema);
        Assert.AreEqual("ymir-daemon", frame.DaemonId);
        Assert.AreEqual("session-1", frame.SessionId);
        Assert.AreEqual(42, frame.FrameId);
        Assert.AreEqual(12.5, frame.SimulationTimeSeconds);
        Assert.AreEqual(0.02, frame.FixedDeltaSeconds);
        Assert.IsTrue(frame.IsAuthoritative);
        Assert.AreEqual("daemon", frame.StateSource);
        Assert.AreSame(run, frame.Run);
        Assert.AreEqual("entity:player", frame.Run.CurrentEntityKey);
        Assert.IsNotEmpty(frame.PublishedAtUtc);
    }

    [Test]
    public void CommandFactoryPreservesClientObservedFrame()
    {
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetTarget,
            "codex",
            "session-1",
            42,
            "entity:player");

        command.TargetEntityKey = "entity:pirate";

        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Command, command.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonCommandKinds.SetTarget, command.Kind);
        Assert.AreEqual("codex", command.ClientId);
        Assert.AreEqual("session-1", command.SessionId);
        Assert.AreEqual(42, command.ObservedFrameId);
        Assert.AreEqual("entity:player", command.ActorEntityKey);
        Assert.AreEqual("entity:pirate", command.TargetEntityKey);
        Assert.IsNotEmpty(command.CommandId);
        Assert.IsNotEmpty(command.IssuedAtUtc);
    }

    [Test]
    public void CommandDocumentRoundTripsThroughStateNode()
    {
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup,
            "bifrost",
            "session-2",
            99,
            "entity:player");
        command.WeaponGroup = 2;

        var result = AetheriaRuntimeDaemonOperations.Execute(
            new AetheriaRuntimeRunCheckpointCommit(),
            new[] { command });

        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Command, command.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, command.Kind);
        Assert.AreEqual("bifrost", command.ClientId);
        Assert.AreEqual("session-2", command.SessionId);
        Assert.AreEqual(99, command.ObservedFrameId);
        Assert.AreEqual("entity:player", command.ActorEntityKey);
        Assert.AreEqual(2, command.WeaponGroup);
        CollectionAssert.Contains(result.AppliedCommandIds, command.CommandId);
    }

    [Test]
    public void FrameStorePublishesLatestDaemonFrame()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-frame-store-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = "run-2",
                CurrentZoneIndex = 7,
                CurrentEntityKey = "entity:ship"
            },
            "aetheria-daemon",
            "session-3",
            123,
            45.5,
            0.016);

        var framePath = AetheriaRuntimeDaemonFrameStore.PublishFrame(statePath, frame);
        var read = AetheriaRuntimeDaemonFrameStore.TryReadFrame(statePath, out var stored);

        Assert.IsTrue(read);
        Assert.AreEqual(AetheriaRuntimeStateBoundary.GetDaemonFramePath(statePath), framePath);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Frame, stored.Schema);
        Assert.AreEqual("aetheria-daemon", stored.DaemonId);
        Assert.AreEqual("session-3", stored.SessionId);
        Assert.AreEqual(123, stored.FrameId);
        Assert.IsTrue(stored.IsAuthoritative);
        Assert.AreEqual("daemon", stored.StateSource);
        Assert.AreEqual("run-2", stored.Run.RunId);
        Assert.AreEqual(7, stored.Run.CurrentZoneIndex);
        Assert.AreEqual("entity:ship", stored.Run.CurrentEntityKey);
    }

    [Test]
    public void FrameStoreReportsMissingFrame()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-frame-store-tests",
            Path.GetRandomFileName(),
            "missing-state.cc");

        Assert.IsFalse(AetheriaRuntimeDaemonFrameStore.TryReadFrame(statePath, out _));
    }

    [Test]
    public void TickRunnerAppliesObservedCommandsAndPublishesFrame()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-tick-runner-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var run = RunWithTwoEntities();
        var targetCommand = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetTarget,
            "codex",
            "session-tick",
            40,
            "zone.0.entity.0");
        targetCommand.TargetEntityKey = "zone.0.entity.1";
        var movementCommand = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetMoveVector,
            "codex",
            "session-tick",
            41,
            "zone.0.entity.0");
        movementCommand.DirectionX = 1.0;
        movementCommand.DirectionY = 0.0;
        movementCommand.ScalarValue = 1.0;

        var result = AetheriaRuntimeDaemonTickRunner.Tick(
            statePath,
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                DaemonId = "test-daemon",
                SessionId = "session-tick",
                VerseId = "aetheria.test",
                CultMeshAddress = "cultmesh://aetheria.test/eve/providers/aetheria.daemon",
                FrameId = 42,
                SimulationTimeSeconds = 12.5,
                FixedDeltaSeconds = 0.02,
                ObservedCommands = new[] { targetCommand, movementCommand }
            });

        Assert.AreEqual(2, result.OperationResult.AppliedCommandIds.Count);
        Assert.AreEqual(0, result.OperationResult.RejectedCommandIds.Count);
        Assert.AreEqual(1, run.Zones[0].Entities[0].TargetEntityIndex);
        Assert.AreEqual("zone.0.entity.0", result.Intents.Movement.ActorEntityKey);
        Assert.AreEqual(1.0, result.Intents.Movement.DirectionX, 0.0001);
        Assert.IsTrue(AetheriaRuntimeDaemonFrameStore.TryReadFrame(statePath, out var frame));
        Assert.AreEqual(result.FramePath, AetheriaRuntimeDaemonFrameStore.GetFramePath(statePath));
        Assert.AreEqual("test-daemon", frame.DaemonId);
        Assert.AreEqual("session-tick", frame.SessionId);
        Assert.AreEqual(42, frame.FrameId);
        Assert.IsTrue(frame.IsAuthoritative);
        Assert.AreEqual("daemon", frame.StateSource);
        CollectionAssert.Contains(frame.Capabilities, "aetheria.daemon.intent_state.v1");
        CollectionAssert.Contains(frame.Capabilities, AetheriaRuntimeDaemonSchemas.ProviderAdvertisement);
        CollectionAssert.Contains(frame.Capabilities, AetheriaRuntimeDaemonSchemas.CommandBoundary);
        CollectionAssert.Contains(frame.Capabilities, AetheriaRuntimeDaemonSchemas.Health);
        CollectionAssert.Contains(frame.Capabilities, AetheriaRuntimeDaemonSchemas.GameSurface);
        CollectionAssert.Contains(frame.Capabilities, AetheriaRuntimeDaemonSchemas.EditorSurface);
        CollectionAssert.AreEquivalent(result.OperationResult.AppliedCommandIds, frame.AppliedCommandIds);
        CollectionAssert.Contains(frame.AccountedCommandIds, targetCommand.CommandId);
        CollectionAssert.Contains(frame.AccountedCommandIds, movementCommand.CommandId);
        Assert.AreEqual(1, frame.Run.Zones[0].Entities[0].TargetEntityIndex);
        Assert.AreEqual(
            AetheriaRuntimeDaemonPublicationStore.GetProviderAdvertisementPath(statePath),
            result.ProviderAdvertisementPath);
        Assert.AreEqual(AetheriaRuntimeDaemonPublicationStore.GetHealthPath(statePath), result.HealthPath);
        Assert.AreEqual(
            AetheriaRuntimeDaemonPublicationStore.GetCommandBoundaryPath(statePath),
            result.CommandBoundaryPath);
        Assert.AreEqual(AetheriaRuntimeDaemonPublicationStore.GetGameSurfacePath(statePath), result.GameSurfacePath);
        Assert.AreEqual(AetheriaRuntimeDaemonPublicationStore.GetGameTuiSurfacePath(statePath), result.GameTuiSurfacePath);
        Assert.AreEqual(AetheriaRuntimeDaemonPublicationStore.GetEditorSurfacePath(statePath), result.EditorSurfacePath);
        Assert.AreEqual(AetheriaRuntimeDaemonPublicationStore.GetEditorTuiSurfacePath(statePath), result.EditorTuiSurfacePath);
        Assert.IsTrue(AetheriaRuntimeDaemonPublicationStore.TryReadProviderAdvertisement(
            statePath,
            out var providerAdvertisement));
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ProviderAdvertisement, providerAdvertisement.Schema);
        Assert.AreEqual("aetheria.test", providerAdvertisement.VerseId);
        Assert.AreEqual("aetheria.daemon", providerAdvertisement.ProviderId);
        Assert.AreEqual("test-daemon", providerAdvertisement.DaemonId);
        Assert.AreEqual("cultmesh://aetheria.test/eve/providers/aetheria.daemon", providerAdvertisement.CultMeshAddress);
        Assert.AreEqual(AetheriaRuntimeDaemonFrameStore.GetFramePath(statePath), providerAdvertisement.FrameWitnessPath);
        Assert.AreEqual(AetheriaRuntimeDaemonPublicationStore.GetHealthPath(statePath), providerAdvertisement.HealthWitnessPath);
        Assert.AreEqual(
            AetheriaRuntimeDaemonPublicationStore.GetCommandBoundaryPath(statePath),
            providerAdvertisement.CommandBoundaryWitnessPath);
        Assert.AreEqual(
            AetheriaRuntimeDaemonPublicationStore.GetGameSurfacePath(statePath),
            providerAdvertisement.EveGuiSurfaceWitnessPath);
        Assert.AreEqual(
            AetheriaRuntimeDaemonPublicationStore.GetGameTuiSurfacePath(statePath),
            providerAdvertisement.EveTuiSurfaceWitnessPath);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, providerAdvertisement.EveGuiSurfaceId);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, providerAdvertisement.EveTuiSurfaceId);
        Assert.AreEqual(
            AetheriaRuntimeDaemonPublicationStore.GetEditorSurfacePath(statePath),
            providerAdvertisement.EditorGuiSurfaceWitnessPath);
        Assert.AreEqual(
            AetheriaRuntimeDaemonPublicationStore.GetEditorTuiSurfacePath(statePath),
            providerAdvertisement.EditorTuiSurfaceWitnessPath);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, providerAdvertisement.EditorGuiSurfaceId);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, providerAdvertisement.EditorTuiSurfaceId);
        CollectionAssert.Contains(providerAdvertisement.PublishedSchemas, AetheriaRuntimeDaemonSchemas.Command);
        CollectionAssert.Contains(providerAdvertisement.PublishedSchemas, AetheriaRuntimeDaemonSchemas.CommandBoundary);
        CollectionAssert.Contains(providerAdvertisement.PublishedSchemas, AetheriaRuntimeDaemonSchemas.GameSurface);
        CollectionAssert.Contains(providerAdvertisement.PublishedSchemas, AetheriaRuntimeDaemonSchemas.EditorSurface);
        Assert.IsTrue(AetheriaRuntimeDaemonPublicationStore.TryReadHealth(statePath, out var health));
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Health, health.Schema);
        Assert.AreEqual("test-daemon", health.DaemonId);
        Assert.AreEqual("aetheria.test", health.VerseId);
        Assert.AreEqual(42, health.FrameId);
        Assert.AreEqual(2, health.ObservedCommandCount);
        Assert.AreEqual(2, health.AppliedCommandCount);
        Assert.AreEqual(0, health.RejectedCommandCount);
        Assert.AreEqual("daemon-published", health.PublicationSource);
        Assert.AreEqual("cultcache-witness", health.Transport);
        Assert.IsTrue(AetheriaRuntimeDaemonPublicationStore.TryReadCommandBoundary(
            statePath,
            out var commandBoundary));
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.CommandBoundary, commandBoundary.Schema);
        Assert.AreEqual("aetheria.daemon.commands", commandBoundary.BoundaryId);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Command, commandBoundary.CommandSchema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Frame, commandBoundary.ReceiptSchema);
        Assert.IsTrue(commandBoundary.Commands.Any(entry => entry.Kind == AetheriaRuntimeDaemonCommandKinds.SetMoveVector));
        Assert.IsTrue(commandBoundary.Commands.Any(entry =>
            entry.Kind == AetheriaRuntimeDaemonCommandKinds.TransferCargoItem &&
            entry.CommandBody == nameof(AetheriaRuntimeCargoTransferCommand)));
        Assert.IsTrue(AetheriaRuntimeDaemonPublicationStore.TryReadGameSurface(statePath, out var gameSurface));
        Assert.AreEqual("aetheria.daemon", gameSurface.ProviderId);
        Assert.AreEqual("game.daemon", gameSurface.ProviderKind);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, gameSurface.Surface.Id);
        Assert.AreEqual(42, gameSurface.Version);
        Assert.IsTrue(gameSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.SetMoveVector" &&
            command.Transport == "cultmesh"));
        Assert.IsTrue(ContainsSurfaceMetric(gameSurface.Surface.Root, "Name", "Player"));
        Assert.IsTrue(ContainsSurfaceMetric(gameSurface.Surface.Root, "Target", "Target"));
        Assert.IsTrue(AetheriaRuntimeDaemonPublicationStore.TryReadGameTuiSurface(statePath, out var gameTuiSurface));
        Assert.AreEqual("aetheria.daemon", gameTuiSurface.ProviderId);
        Assert.AreEqual("game.daemon", gameTuiSurface.ProviderKind);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, gameTuiSurface.Surface.Id);
        Assert.AreEqual(42, gameTuiSurface.Version);
        Assert.IsTrue(AetheriaRuntimeDaemonPublicationStore.TryReadEditorSurface(statePath, out var editorSurface));
        Assert.AreEqual("aetheria.daemon", editorSurface.ProviderId);
        Assert.AreEqual("editor.daemon", editorSurface.ProviderKind);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, editorSurface.Surface.Id);
        Assert.AreEqual(42, editorSurface.Version);
        Assert.IsTrue(ContainsSurfaceMetric(editorSurface.Surface.Root, "Verse", "aetheria.test"));
        Assert.IsTrue(ContainsSurfaceMetric(editorSurface.Surface.Root, "Status", "healthy"));
        Assert.IsTrue(ContainsSurfaceProp(
            editorSurface.Surface.Root,
            "surfaceId",
            AetheriaRuntimeStatRecipeCommands.SurfaceId));
        Assert.IsTrue(editorSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.TransferCargoItem" &&
            command.Transport == "cultmesh"));
        Assert.IsTrue(AetheriaRuntimeDaemonPublicationStore.TryReadEditorTuiSurface(statePath, out var editorTuiSurface));
        Assert.AreEqual("aetheria.daemon", editorTuiSurface.ProviderId);
        Assert.AreEqual("editor.daemon", editorTuiSurface.ProviderKind);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, editorTuiSurface.Surface.Id);
        Assert.AreEqual(42, editorTuiSurface.Version);
        Assert.IsTrue(AetheriaRuntimeStateReader.TryReadDaemonGameSurface(
            statePath,
            out var unityGameSurface));
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, unityGameSurface.Surface.Id);
        Assert.AreEqual("aetheria.daemon", unityGameSurface.ProviderId);
        Assert.IsTrue(unityGameSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.SetMoveVector" &&
            command.Transport == "cultmesh"));
        Assert.IsTrue(AetheriaRuntimeStateReader.TryReadDaemonGameTuiSurface(
            statePath,
            out var unityGameTuiSurface));
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, unityGameTuiSurface.Surface.Id);
        Assert.AreEqual("game.daemon", unityGameTuiSurface.ProviderKind);
        Assert.IsTrue(AetheriaRuntimeStateReader.TryReadDaemonEditorSurface(
            statePath,
            out var unityEditorSurface));
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, unityEditorSurface.Surface.Id);
        Assert.AreEqual("editor.daemon", unityEditorSurface.ProviderKind);
        Assert.IsTrue(unityEditorSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.TransferCargoItem" &&
            command.Transport == "cultmesh"));
        Assert.IsTrue(AetheriaRuntimeStateReader.TryReadDaemonEditorTuiSurface(
            statePath,
            out var unityEditorTuiSurface));
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, unityEditorTuiSurface.Surface.Id);
        Assert.AreEqual("editor.daemon", unityEditorTuiSurface.ProviderKind);
        var genericGameSurface = AetheriaRuntimeStateReader.ReadEveSurface(
            statePath,
            AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId);
        Assert.IsNotNull(genericGameSurface);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, genericGameSurface.Surface.Id);
        Assert.AreEqual(unityGameSurface.ProviderId, genericGameSurface.ProviderId);
        var genericGameTuiSurface = AetheriaRuntimeStateReader.ReadEveSurface(
            statePath,
            AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId);
        Assert.IsNotNull(genericGameTuiSurface);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, genericGameTuiSurface.Surface.Id);
        var genericEditorTuiSurface = AetheriaRuntimeStateReader.ReadEveSurface(
            statePath,
            AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId);
        Assert.IsNotNull(genericEditorTuiSurface);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, genericEditorTuiSurface.Surface.Id);
    }

    [Test]
    public void TickRunnerSkipsPreviouslyAccountedObservedCommands()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-tick-runner-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var run = RunWithTwoEntities();
        var targetCommand = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetTarget,
            "codex",
            "session-tick",
            40,
            "zone.0.entity.0");
        targetCommand.TargetEntityKey = "zone.0.entity.1";

        var result = AetheriaRuntimeDaemonTickRunner.Tick(
            statePath,
            run,
            new AetheriaRuntimeDaemonTickOptions
            {
                DaemonId = "test-daemon",
                SessionId = "session-tick",
                FrameId = 43,
                SimulationTimeSeconds = 12.52,
                FixedDeltaSeconds = 0.02,
                ObservedCommands = new[] { targetCommand },
                AccountedCommandIds = new[] { targetCommand.CommandId }
            });

        Assert.AreEqual(0, result.OperationResult.AppliedCommandIds.Count);
        Assert.AreEqual(0, result.OperationResult.RejectedCommandIds.Count);
        Assert.AreEqual(-1, run.Zones[0].Entities[0].TargetEntityIndex);
        CollectionAssert.Contains(result.Frame.AccountedCommandIds, targetCommand.CommandId);
        Assert.IsTrue(AetheriaRuntimeDaemonPublicationStore.TryReadHealth(statePath, out var health));
        Assert.AreEqual(1, health.ObservedCommandCount);
        Assert.AreEqual(0, health.AppliedCommandCount);
        Assert.AreEqual(0, health.RejectedCommandCount);
    }

    [Test]
    public void SoaViewFactoryDescribesDaemonOwnedObserverReadOnlySlabs()
    {
        var buffer = new AetheriaRuntimeDaemonSoaBufferDocument
        {
            BufferId = "hot-transform-0",
            DisplayName = "Hot Transform Page 0",
            Backend = AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile,
            Location = "Local\\AetheriaHotTransform0",
            ByteLength = 4096,
            Generation = 77,
            DaemonWritable = true,
            ObserverWritable = false
        };
        var positionX = new AetheriaRuntimeDaemonSoaColumnDocument
        {
            ColumnId = "position-x",
            Kind = AetheriaRuntimeDaemonSoaColumnKinds.PositionX,
            BufferId = buffer.BufferId,
            ScalarType = "float32",
            ByteOffset = 0,
            ElementStride = 4,
            ElementCount = 128,
            Unit = "world_units",
            CoordinateSpace = "zone"
        };
        var dirtyRange = new AetheriaRuntimeDaemonSoaDirtyRangeDocument
        {
            ColumnId = positionX.ColumnId,
            StartIndex = 16,
            Count = 8,
            Generation = 77
        };

        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "ymir-daemon",
            "session-soa",
            42,
            77,
            new[] { buffer },
            new[] { positionX },
            new[] { dirtyRange },
            AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile,
            AetheriaRuntimeDaemonSoaSynchronizationModes.DoubleBuffered);

        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.SoaView, view.Schema);
        Assert.AreEqual("ymir-daemon", view.DaemonId);
        Assert.AreEqual("session-soa", view.SessionId);
        Assert.AreEqual(42, view.FrameId);
        Assert.AreEqual(77, view.Generation);
        Assert.IsTrue(view.IsAuthoritative);
        Assert.AreEqual(AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile, view.Backend);
        Assert.AreEqual(AetheriaRuntimeDaemonSoaSynchronizationModes.DoubleBuffered, view.SynchronizationMode);
        Assert.AreEqual(1, view.Buffers.Count);
        Assert.IsTrue(view.Buffers[0].DaemonWritable);
        Assert.IsFalse(view.Buffers[0].ObserverWritable);
        Assert.AreEqual(AetheriaRuntimeDaemonSoaColumnKinds.PositionX, view.Columns[0].Kind);
        Assert.AreEqual(16, view.DirtyRanges[0].StartIndex);
        Assert.AreEqual(8, view.DirtyRanges[0].Count);
        Assert.IsNotEmpty(view.PublishedAtUtc);
    }

    [Test]
    public void SoaViewIndexBindsReadOnlyDirectMemoryColumns()
    {
        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "ymir-daemon",
            "session-soa-index",
            90,
            91,
            new[]
            {
                new AetheriaRuntimeDaemonSoaBufferDocument
                {
                    BufferId = "transform-hot",
                    Backend = AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile,
                    Location = "Local\\AetheriaTransformHot",
                    ByteOffset = 128,
                    ByteLength = 512,
                    Generation = 91,
                    DaemonWritable = true,
                    ObserverWritable = false
                }
            },
            new[]
            {
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "position-x",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.PositionX,
                    BufferId = "transform-hot",
                    ScalarType = "float32",
                    ByteOffset = 16,
                    ElementStride = 4,
                    ElementCount = 64,
                    Unit = "world_units",
                    CoordinateSpace = "zone"
                }
            },
            new[]
            {
                new AetheriaRuntimeDaemonSoaDirtyRangeDocument
                {
                    ColumnId = "position-x",
                    StartIndex = 8,
                    Count = 4,
                    Generation = 91
                }
            },
            AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile);

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);

        Assert.IsTrue(index.IsValid);
        Assert.IsTrue(index.TryGetFirstColumnOfKind(AetheriaRuntimeDaemonSoaColumnKinds.PositionX, out var binding));
        Assert.AreEqual("position-x", binding.Column.ColumnId);
        Assert.AreEqual("transform-hot", binding.Buffer.BufferId);
        Assert.AreEqual(144, binding.AbsoluteByteOffset);
        Assert.AreEqual(256, binding.ByteLength);
        Assert.IsTrue(binding.DirectMemoryCompatible);
        Assert.AreEqual(1, index.GetDirtyRanges("position-x").Count);
        Assert.AreEqual(8, index.GetDirtyRanges("position-x")[0].StartIndex);
    }

    [Test]
    public void SoaViewIndexBindsDaemonAuthoredRenderGroups()
    {
        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "aetheria-daemon",
            "session-render-groups",
            120,
            121,
            new[]
            {
                new AetheriaRuntimeDaemonSoaBufferDocument
                {
                    BufferId = "render-hot",
                    Backend = AetheriaRuntimeDaemonSoaBackends.SharedNativeMemory,
                    ByteLength = 256,
                    Generation = 121,
                    DaemonWritable = true,
                    ObserverWritable = false
                }
            },
            new[]
            {
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "render-group-id",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.RenderGroupId,
                    BufferId = "render-hot",
                    ScalarType = "uint32",
                    ByteOffset = 0,
                    ElementStride = 4,
                    ElementCount = 64
                }
            },
            backend: AetheriaRuntimeDaemonSoaBackends.SharedNativeMemory,
            renderGroups: new[]
            {
                new AetheriaRuntimeDaemonRenderGroupDocument
                {
                    GroupId = 7,
                    MeshKey = "ships/djinni",
                    MaterialKey = "materials/ships/hull",
                    SubMeshIndex = 0,
                    Layer = 12,
                    ShaderKey = "aetheria/daemon-indirect",
                    DisplayName = "Djinni hull",
                    InstanceCount = 24,
                    BoundsCenterX = 10,
                    BoundsCenterY = 20,
                    BoundsCenterZ = 30,
                    BoundsSizeX = 100,
                    BoundsSizeY = 80,
                    BoundsSizeZ = 60,
                    ShadowMode = AetheriaRuntimeDaemonRenderShadowModes.TwoSided,
                    ReceiveShadows = false,
                    DefaultScale = 2.5f,
                    Lod = 2
                }
            });

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);

        Assert.IsTrue(index.IsValid);
        Assert.IsTrue(index.TryGetFirstColumnOfKind(AetheriaRuntimeDaemonSoaColumnKinds.RenderGroupId, out var groupColumn));
        Assert.AreEqual("render-group-id", groupColumn.Column.ColumnId);
        Assert.IsTrue(groupColumn.DirectMemoryCompatible);
        Assert.AreEqual(1, index.RenderGroups.Count);
        Assert.IsTrue(index.TryGetRenderGroup(7, out var renderGroup));
        Assert.AreEqual("ships/djinni", renderGroup.MeshKey);
        Assert.AreEqual("materials/ships/hull", renderGroup.MaterialKey);
        Assert.AreEqual(12, renderGroup.Layer);
        Assert.AreEqual(24, renderGroup.InstanceCount);
        Assert.AreEqual(10, renderGroup.BoundsCenterX);
        Assert.AreEqual(100, renderGroup.BoundsSizeX);
        Assert.AreEqual(AetheriaRuntimeDaemonRenderShadowModes.TwoSided, renderGroup.ShadowMode);
        Assert.IsFalse(renderGroup.ReceiveShadows);
        Assert.AreEqual(2.5f, renderGroup.DefaultScale);
        Assert.AreEqual(2, renderGroup.Lod);
    }

    [Test]
    public void SoaViewIndexRejectsInvalidRenderGroups()
    {
        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "aetheria-daemon",
            "session-render-groups-invalid",
            122,
            123,
            new[] { new AetheriaRuntimeDaemonSoaBufferDocument { BufferId = "render-hot", ByteLength = 16 } },
            new[] { new AetheriaRuntimeDaemonSoaColumnDocument { ColumnId = "render-group-id", Kind = AetheriaRuntimeDaemonSoaColumnKinds.RenderGroupId, BufferId = "render-hot", ScalarType = "uint32", ElementStride = 4, ElementCount = 4 } },
            renderGroups: new[]
            {
                new AetheriaRuntimeDaemonRenderGroupDocument { GroupId = 3, MeshKey = "ships/djinni" },
                new AetheriaRuntimeDaemonRenderGroupDocument { GroupId = 3, MeshKey = "ships/djinni", MaterialKey = "materials/ships/hull" },
                new AetheriaRuntimeDaemonRenderGroupDocument { GroupId = 4, MeshKey = "ships/pirate", MaterialKey = "materials/ships/hull", SubMeshIndex = -1 },
                new AetheriaRuntimeDaemonRenderGroupDocument { GroupId = 5, MeshKey = "ships/frigate", MaterialKey = "materials/ships/hull", InstanceCount = -2 },
                new AetheriaRuntimeDaemonRenderGroupDocument { GroupId = 6, MeshKey = "ships/cruiser", MaterialKey = "materials/ships/hull", BoundsSizeX = 10, BoundsSizeY = 0, BoundsSizeZ = 10 },
                new AetheriaRuntimeDaemonRenderGroupDocument { GroupId = 7, MeshKey = "ships/hauler", MaterialKey = "materials/ships/hull", ShadowMode = "unity-local-policy" },
                new AetheriaRuntimeDaemonRenderGroupDocument { GroupId = 8, MeshKey = "ships/shuttle", MaterialKey = "materials/ships/hull", DefaultScale = 0 },
                new AetheriaRuntimeDaemonRenderGroupDocument { GroupId = 9, MeshKey = "ships/scout", MaterialKey = "materials/ships/hull", Lod = -2 }
            });

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);
        var errors = string.Join("\n", index.ValidationErrors);

        Assert.IsFalse(index.IsValid);
        StringAssert.Contains("missing a material key", errors);
        StringAssert.Contains("Duplicate render group id '3'", errors);
        StringAssert.Contains("negative submesh index", errors);
        StringAssert.Contains("invalid instance count", errors);
        StringAssert.Contains("must publish positive render bounds", errors);
        StringAssert.Contains("invalid shadow mode", errors);
        StringAssert.Contains("non-positive default scale", errors);
        StringAssert.Contains("invalid render lod", errors);
    }

    [Test]
    public void SoaViewIndexRejectsInvalidRenderSemanticColumnTypes()
    {
        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "aetheria-daemon",
            "session-render-column-types-invalid",
            126,
            127,
            new[] { new AetheriaRuntimeDaemonSoaBufferDocument { BufferId = "render-hot", ByteLength = 64 } },
            new[]
            {
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "render-scale",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.RenderScale,
                    BufferId = "render-hot",
                    ScalarType = "uint32",
                    ElementStride = 4,
                    ElementCount = 4
                },
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "render-visibility",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.RenderVisibility,
                    BufferId = "render-hot",
                    ScalarType = "float32",
                    ByteOffset = 16,
                    ElementStride = 4,
                    ElementCount = 4
                },
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "render-lod",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.RenderLod,
                    BufferId = "render-hot",
                    ScalarType = "float32",
                    ByteOffset = 32,
                    ElementStride = 4,
                    ElementCount = 4
                },
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "physics-body-radius",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.PhysicsBodyRadius,
                    BufferId = "render-hot",
                    ScalarType = "uint32",
                    ByteOffset = 48,
                    ElementStride = 4,
                    ElementCount = 4
                }
            });

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);
        var errors = string.Join("\n", index.ValidationErrors);

        Assert.IsFalse(index.IsValid);
        StringAssert.Contains("render scale must use float32", errors);
        StringAssert.Contains("render visibility must use bool, byte, or uint8", errors);
        StringAssert.Contains("render lod must use int32", errors);
        StringAssert.Contains("physics.body.radius must use float32", errors);
    }

    [Test]
    public void SoaViewIndexRequiresRenderGroupIdColumnForMultipleRenderGroups()
    {
        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "aetheria-daemon",
            "session-render-groups-without-group-column",
            124,
            125,
            Array.Empty<AetheriaRuntimeDaemonSoaBufferDocument>(),
            Array.Empty<AetheriaRuntimeDaemonSoaColumnDocument>(),
            renderGroups: new[]
            {
                new AetheriaRuntimeDaemonRenderGroupDocument
                {
                    GroupId = 1,
                    MeshKey = "ships/djinni",
                    MaterialKey = "materials/ships/hull",
                    BoundsSizeX = 10,
                    BoundsSizeY = 10,
                    BoundsSizeZ = 10
                },
                new AetheriaRuntimeDaemonRenderGroupDocument
                {
                    GroupId = 2,
                    MeshKey = "ships/frigate",
                    MaterialKey = "materials/ships/hull",
                    BoundsSizeX = 10,
                    BoundsSizeY = 10,
                    BoundsSizeZ = 10
                }
            });

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);
        var errors = string.Join("\n", index.ValidationErrors);

        Assert.IsFalse(index.IsValid);
        StringAssert.Contains("Multiple render groups require a render group id column", errors);
    }

    [Test]
    public void SoaViewIndexRejectsObserverWritableAndOutOfBoundsColumns()
    {
        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "ymir-daemon",
            "session-soa-index-invalid",
            92,
            93,
            new[]
            {
                new AetheriaRuntimeDaemonSoaBufferDocument
                {
                    BufferId = "mutable-hot",
                    Backend = AetheriaRuntimeDaemonSoaBackends.SharedNativeMemory,
                    ByteLength = 16,
                    ObserverWritable = true
                }
            },
            new[]
            {
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "heat",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.Heat,
                    BufferId = "mutable-hot",
                    ScalarType = "float32",
                    ByteOffset = 8,
                    ElementStride = 4,
                    ElementCount = 8
                }
            });

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);

        Assert.IsFalse(index.IsValid);
        Assert.GreaterOrEqual(index.ValidationErrors.Count, 2);
        Assert.IsTrue(index.TryGetFirstColumnOfKind(AetheriaRuntimeDaemonSoaColumnKinds.Heat, out var binding));
        Assert.IsFalse(binding.DirectMemoryCompatible);
    }

    [Test]
    public void SoaViewStorePublishesLatestDaemonView()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-soa-view-store-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "aetheria-daemon",
            "session-soa-store",
            123,
            456,
            new[]
            {
                new AetheriaRuntimeDaemonSoaBufferDocument
                {
                    BufferId = "entity-hot-0",
                    Backend = AetheriaRuntimeDaemonSoaBackends.CultCache,
                    Location = "cultcache://aetheria/run/local/entity-hot-0",
                    ByteLength = 8192,
                    Generation = 456,
                    DaemonWritable = true,
                    ObserverWritable = false
                }
            },
            new[]
            {
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "heat",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.Heat,
                    BufferId = "entity-hot-0",
                    ScalarType = "float32",
                    ByteOffset = 1024,
                    ElementStride = 4,
                    ElementCount = 256,
                    Unit = "thermal_units"
                }
            },
            new[]
            {
                new AetheriaRuntimeDaemonSoaDirtyRangeDocument
                {
                    ColumnId = "heat",
                    StartIndex = 0,
                    Count = 256,
                    Generation = 456
                }
            });

        var viewPath = AetheriaRuntimeDaemonSoaViewStore.PublishView(statePath, view);
        var read = AetheriaRuntimeDaemonSoaViewStore.TryReadView(statePath, out var stored);

        Assert.IsTrue(read);
        Assert.AreEqual(AetheriaRuntimeStateBoundary.GetDaemonSoaViewPath(statePath), viewPath);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.SoaView, stored.Schema);
        Assert.AreEqual("aetheria-daemon", stored.DaemonId);
        Assert.AreEqual("session-soa-store", stored.SessionId);
        Assert.AreEqual(123, stored.FrameId);
        Assert.AreEqual(456, stored.Generation);
        Assert.IsTrue(stored.IsAuthoritative);
        Assert.AreEqual(1, stored.Buffers.Count);
        Assert.AreEqual("entity-hot-0", stored.Buffers[0].BufferId);
        Assert.IsFalse(stored.Buffers[0].ObserverWritable);
        Assert.AreEqual(1, stored.Columns.Count);
        Assert.AreEqual(AetheriaRuntimeDaemonSoaColumnKinds.Heat, stored.Columns[0].Kind);
        Assert.AreEqual(1, stored.DirtyRanges.Count);
        Assert.AreEqual(256, stored.DirtyRanges[0].Count);
    }

    [Test]
    public void SoaViewStoreReportsMissingView()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-soa-view-store-tests",
            Path.GetRandomFileName(),
            "missing-state.cc");

        Assert.IsFalse(AetheriaRuntimeDaemonSoaViewStore.TryReadView(statePath, out _));
    }

    [Test]
    public void StateReaderReadsObservedDaemonStateWithSoaView()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-observed-daemon-state-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = "daemon-run",
                CurrentZoneIndex = 2,
                CurrentEntityKey = "entity:observer-target"
            },
            "aetheria-daemon",
            "session-observed",
            300,
            12.0,
            0.02);
        var soaView = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "aetheria-daemon",
            "session-observed",
            300,
            301,
            new[]
            {
                new AetheriaRuntimeDaemonSoaBufferDocument
                {
                    BufferId = "entity-hot",
                    Backend = AetheriaRuntimeDaemonSoaBackends.CultCache,
                    Location = "cultcache://aetheria/run/daemon-run/entity-hot",
                    ByteLength = 1024,
                    Generation = 301,
                    DaemonWritable = true,
                    ObserverWritable = false
                }
            },
            new[]
            {
                new AetheriaRuntimeDaemonSoaColumnDocument
                {
                    ColumnId = "position-x",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.PositionX,
                    BufferId = "entity-hot",
                    ScalarType = "float32",
                    ElementStride = 4,
                    ElementCount = 64
                }
            });

        AetheriaRuntimeDaemonFrameStore.PublishFrame(statePath, frame);
        AetheriaRuntimeDaemonSoaViewStore.PublishView(statePath, soaView);

        var read = AetheriaRuntimeStateReader.TryReadObservedDaemonState(statePath, out var observed);

        Assert.IsTrue(read);
        Assert.IsTrue(observed.IsAuthoritative);
        Assert.IsTrue(observed.HasSoaView);
        Assert.AreEqual("daemon-run", observed.Run.RunId);
        Assert.AreEqual("entity:observer-target", observed.Run.CurrentEntityKey);
        Assert.AreEqual(300, observed.Frame.FrameId);
        Assert.AreEqual(301, observed.SoaView.Generation);
        Assert.AreEqual(AetheriaRuntimeStateBoundary.GetDaemonFramePath(statePath), observed.FramePath);
        Assert.AreEqual(AetheriaRuntimeStateBoundary.GetDaemonSoaViewPath(statePath), observed.SoaViewPath);
        Assert.IsFalse(observed.SoaView.Buffers[0].ObserverWritable);
    }

    [Test]
    public void StateReaderReportsMissingObservedDaemonState()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-observed-daemon-state-tests",
            Path.GetRandomFileName(),
            "missing-state.cc");

        var read = AetheriaRuntimeStateReader.TryReadObservedDaemonState(statePath, out var observed);

        Assert.IsFalse(read);
        Assert.IsFalse(observed.IsAuthoritative);
        Assert.IsFalse(observed.HasSoaView);
        Assert.AreEqual("missing", observed.Frame.StateSource);
        Assert.AreEqual(AetheriaRuntimeStateBoundary.GetDaemonFramePath(statePath), observed.FramePath);
    }

    [Test]
    public void ObservationCursorTracksFrameAndSoaGenerationChanges()
    {
        var cursor = new AetheriaRuntimeDaemonObservationCursor();
        var observed = ObservedState(frameId: 10, soaGeneration: 20);

        var first = cursor.Observe(observed);
        var second = cursor.Observe(observed);
        var third = cursor.Observe(ObservedState(frameId: 11, soaGeneration: 20));
        var fourth = cursor.Observe(ObservedState(frameId: 11, soaGeneration: 21));

        Assert.IsTrue(first.Observed);
        Assert.IsTrue(first.FrameChanged);
        Assert.IsTrue(first.SoaViewChanged);
        Assert.AreEqual(10, first.FrameId);
        Assert.AreEqual(20, first.SoaGeneration);
        Assert.IsFalse(second.Changed);
        Assert.IsTrue(third.FrameChanged);
        Assert.IsFalse(third.SoaViewChanged);
        Assert.IsFalse(fourth.FrameChanged);
        Assert.IsTrue(fourth.SoaViewChanged);
        Assert.AreEqual(11, cursor.LastFrameId);
        Assert.AreEqual(21, cursor.LastSoaGeneration);
    }

    [Test]
    public void ObservationCursorResetForgetsDaemonState()
    {
        var cursor = new AetheriaRuntimeDaemonObservationCursor();

        cursor.Observe(ObservedState(frameId: 12, soaGeneration: 22));
        cursor.Reset();
        var missing = cursor.Observe(null);

        Assert.AreEqual(-1, cursor.LastFrameId);
        Assert.AreEqual(-1, cursor.LastSoaGeneration);
        Assert.IsFalse(missing.Observed);
        Assert.IsFalse(missing.Changed);
    }

    [Test]
    public void CommandClientSendsCommandAgainstObservedDaemonFrame()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-command-client-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var observed = ObservedState(frameId: 33, soaGeneration: 44);
        observed.Frame.SessionId = "session-command-client";
        observed.Frame.Run.CurrentEntityKey = "entity:player";
        var client = new AetheriaRuntimeDaemonOperationClient(statePath, "unity-test", "unobserved-session");

        var envelope = client.FireWeaponGroup(observed, 2);

        Assert.AreEqual(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, envelope.Kind);
        Assert.AreEqual("unity-test", envelope.ClientId);
        Assert.AreEqual("session-command-client", envelope.SessionId);
        Assert.AreEqual(33, envelope.ObservedFrameId);
        Assert.AreEqual("entity:player", envelope.ActorEntityKey);
        Assert.IsEmpty(envelope.Path);
    }

    [Test]
    public void DaemonEveSurfaceCommandSubmitsTypedDaemonOperation()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-surface-command-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            new AetheriaRuntimeRunCheckpointCommit
            {
                CurrentEntityKey = "entity:surface-player"
            },
            "aetheria-daemon",
            "session-surface-command",
            77,
            1.5,
            0.02);
        AetheriaRuntimeDaemonFrameStore.PublishFrame(statePath, frame);
        var request = new EveSurfaceCommandRequest(
            "aetheria.daemon",
            AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
            "aetheria.daemon.commands.FireWeaponGroup",
            new Dictionary<string, string>(StringComparer.Ordinal),
            DateTimeOffset.UtcNow,
            "unity-uitoolkit");

        Assert.IsTrue(AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(statePath, request, out var envelope));

        Assert.AreEqual(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, envelope.Kind);
        Assert.AreEqual("unity-uitoolkit", envelope.ClientId);
        Assert.AreEqual("session-surface-command", envelope.SessionId);
        Assert.AreEqual(77, envelope.ObservedFrameId);
        Assert.AreEqual("entity:surface-player", envelope.ActorEntityKey);
        Assert.IsEmpty(envelope.Path);
    }

    [Test]
    public void DaemonOperationsMovesTargetSelectionIntoDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetTarget,
            "codex",
            "session-target",
            15,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.1";

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(0, result.RejectedCommandIds.Count);
        Assert.AreEqual(1, run.Zones[0].Entities[0].TargetEntityIndex);
    }

    [Test]
    public void DaemonOperationsRejectsTargetSelectionWithoutVisibleContact()
    {
        var run = RunWithTwoEntities();
        run.Zones[0].Entities[0].Contacts = Array.Empty<AetheriaRuntimeEntityContactCommit>();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetTarget,
            "codex",
            "session-target",
            15,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.1";

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(-1, run.Zones[0].Entities[0].TargetEntityIndex);
    }

    [Test]
    public void DaemonOperationsCyclesTargetsFromAuthoritativeContacts()
    {
        var run = RunWithTwoEntities();
        var zone = run.Zones[0];
        zone.Entities[0].PositionX = 0;
        zone.Entities[0].PositionY = 0;
        zone.Entities[1].PositionX = 5;
        zone.Entities[1].PositionY = 0;
        zone.Entities = zone.Entities
            .Concat(new[]
            {
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 2,
                    Name = "Near Hostile",
                    PositionX = 2,
                    PositionY = 0
                }
            })
            .ToArray();
        zone.Entities[0].Contacts = new[]
        {
            new AetheriaRuntimeEntityContactCommit
            {
                TargetEntityIndex = 1,
                Visible = true,
                Hostile = true
            },
            new AetheriaRuntimeEntityContactCommit
            {
                TargetEntityIndex = 2,
                Visible = true,
                Hostile = true
            }
        };

        var nearest = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TargetNearest,
            "codex",
            "session-target",
            15,
            "zone.0.entity.0");
        var next = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TargetNext,
            "codex",
            "session-target",
            16,
            "zone.0.entity.0");
        var previous = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TargetPrevious,
            "codex",
            "session-target",
            17,
            "zone.0.entity.0");

        var previousFromNoneResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { previous });
        Assert.AreEqual(1, previousFromNoneResult.AppliedCommandIds.Count);
        Assert.AreEqual(2, zone.Entities[0].TargetEntityIndex);

        var nearestResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { nearest });
        Assert.AreEqual(1, nearestResult.AppliedCommandIds.Count);
        Assert.AreEqual(2, zone.Entities[0].TargetEntityIndex);

        var nextResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { next });
        Assert.AreEqual(1, nextResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, zone.Entities[0].TargetEntityIndex);

        var previousResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { previous });
        Assert.AreEqual(1, previousResult.AppliedCommandIds.Count);
        Assert.AreEqual(2, zone.Entities[0].TargetEntityIndex);
    }

    [Test]
    public void DaemonOperationsTargetsReticleFromAuthoritativeContacts()
    {
        var run = RunWithTwoEntities();
        var zone = run.Zones[0];
        zone.Entities[0].PositionX = 0;
        zone.Entities[0].PositionY = 0;
        zone.Entities[0].PositionZ = 0;
        zone.Entities[1].PositionX = 0;
        zone.Entities[1].PositionY = 0;
        zone.Entities[1].PositionZ = 5;
        zone.Entities = zone.Entities
            .Concat(new[]
            {
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 2,
                    Name = "Off Reticle",
                    PositionX = 5,
                    PositionY = 0,
                    PositionZ = 0
                }
            })
            .ToArray();
        zone.Entities[0].Contacts = new[]
        {
            new AetheriaRuntimeEntityContactCommit
            {
                TargetEntityIndex = 1,
                Visible = true,
                Hostile = true
            },
            new AetheriaRuntimeEntityContactCommit
            {
                TargetEntityIndex = 2,
                Visible = true,
                Hostile = true
            }
        };

        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TargetReticle,
            "codex",
            "session-reticle",
            18,
            "zone.0.entity.0");
        command.PositionZ = 1;

        var targetResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });
        Assert.AreEqual(1, targetResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, zone.Entities[0].TargetEntityIndex);

        var toggleResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });
        Assert.AreEqual(1, toggleResult.AppliedCommandIds.Count);
        Assert.AreEqual(-1, zone.Entities[0].TargetEntityIndex);
    }

    [Test]
    public void DaemonOperationsMutatesHotEntityControlsInDaemonState()
    {
        var run = RunWithTwoEntities();
        var look = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetLookDirection,
            "codex",
            "session-controls",
            16,
            "zone.0.entity.0");
        look.DirectionX = 0.25;
        look.PositionZ = -0.75;
        var tractor = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetTractorPower,
            "codex",
            "session-controls",
            16,
            "zone.0.entity.0");
        tractor.ScalarValue = 0.8;
        var heatsinks = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled,
            "codex",
            "session-controls",
            16,
            "zone.0.entity.0");
        heatsinks.ScalarValue = 1.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { look, tractor, heatsinks });

        var player = run.Zones[0].Entities[0];
        Assert.AreEqual(3, result.AppliedCommandIds.Count);
        Assert.AreEqual(0, result.RejectedCommandIds.Count);
        Assert.AreEqual(0.25, player.DirectionX, 0.0001);
        Assert.AreEqual(-0.75, player.DirectionY, 0.0001);
        Assert.AreEqual(0.8, player.TractorPower, 0.0001);
        Assert.IsTrue(player.HeatsinksEnabled);
    }

    [Test]
    public void DaemonOperationsRejectsOutOfRangeTractorPower()
    {
        var run = RunWithTwoEntities();
        var tractor = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetTractorPower,
            "codex",
            "session-controls",
            16,
            "zone.0.entity.0");
        tractor.ScalarValue = 1.25;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { tractor });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(0.0, run.Zones[0].Entities[0].TractorPower, 0.0001);
    }

    [Test]
    public void DaemonOperationsRejectsOutOfRangeMoveMagnitude()
    {
        var run = RunWithTwoEntities();
        var move = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetMoveVector,
            "codex",
            "session-intents",
            32,
            "zone.0.entity.0");
        move.DirectionX = 0.25;
        move.DirectionY = -0.5;
        move.ScalarValue = 1.25;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { move });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.IsFalse(result.Intents.HasAny);
    }

    [Test]
    public void DaemonOperationsOwnsActionBarBindingsInDaemonState()
    {
        var run = RunWithTwoEntities();
        run.ActionBarBindings = new[]
        {
            new AetheriaRuntimeActionBarBindingCommit
            {
                ControlPath = "<Keyboard>/1",
                Kind = "equipment",
                EquipmentIndex = 0
            }
        };
        var set = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetActionBarBinding,
            "codex",
            "session-bindings",
            17,
            "zone.0.entity.0");
        set.TextValue = "<Keyboard>/1";
        set.ActionBarBinding.Kind = "weapon_group";
        set.ActionBarBinding.ItemKey = "laser";
        set.WeaponGroup = 2;
        var clear = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.ClearActionBarBinding,
            "codex",
            "session-bindings",
            18,
            "zone.0.entity.0");
        clear.TextValue = "<Keyboard>/1";

        var setResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { set });

        Assert.AreEqual(1, setResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, run.ActionBarBindings.Count);
        Assert.AreEqual("weapon_group", run.ActionBarBindings[0].Kind);
        Assert.AreEqual("laser", run.ActionBarBindings[0].ItemKey);
        Assert.AreEqual(2, run.ActionBarBindings[0].WeaponGroup);

        var clearResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { clear });

        Assert.AreEqual(1, clearResult.AppliedCommandIds.Count);
        Assert.AreEqual(0, run.ActionBarBindings.Count);
    }

    [Test]
    public void DaemonOperationsRejectsWeaponGroupActionBarBindingForMissingGroup()
    {
        var run = RunWithTwoEntities();
        var set = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetActionBarBinding,
            "codex",
            "session-bindings",
            17,
            "zone.0.entity.0");
        set.TextValue = "<Keyboard>/1";
        set.ActionBarBinding.Kind = "weapon_group";
        set.WeaponGroup = 99;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { set });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(0, run.ActionBarBindings.Count);
    }

    [Test]
    public void DaemonOperationsOwnsEquipmentSwitchesInDaemonState()
    {
        var run = RunWithTwoEntities();
        var disable = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetItemEnabled,
            "codex",
            "session-equipment",
            19,
            "zone.0.entity.0");
        disable.EquipmentIndex = 0;
        disable.ScalarValue = 0.0;
        var overrideShutdown = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetItemOverrideShutdown,
            "codex",
            "session-equipment",
            20,
            "zone.0.entity.0");
        overrideShutdown.TargetEntityKey = "zone.0.entity.0";
        overrideShutdown.EquipmentIndex = 0;
        overrideShutdown.ScalarValue = 1.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { disable, overrideShutdown });

        var item = run.Zones[0].Entities[0].Equipment[0].Item;
        Assert.AreEqual(2, result.AppliedCommandIds.Count);
        Assert.IsFalse(item.Enabled);
        Assert.IsTrue(item.OverrideShutdown);
    }

    [Test]
    public void DaemonOperationsOwnsShieldToggleSelectionInDaemonState()
    {
        var run = RunWithTwoEntities();
        var toggle = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled,
            "codex",
            "session-equipment",
            21,
            "zone.0.entity.0");

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { toggle });

        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.IsFalse(run.Zones[0].Entities[0].Equipment[1].Item.Enabled);
    }

    [Test]
    public void DaemonOperationsRejectsShieldToggleWhenNoShieldBehaviorExists()
    {
        var run = RunWithTwoEntities();
        run.Zones[0].Entities[0].BehaviorStates = Array.Empty<AetheriaRuntimeBehaviorStateCommit>();
        var toggle = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled,
            "codex",
            "session-equipment",
            21,
            "zone.0.entity.0");

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { toggle });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.IsTrue(run.Zones[0].Entities[0].Equipment[1].Item.Enabled);
    }

    [Test]
    public void DaemonOperationsOwnsWeaponGroupMembershipInDaemonState()
    {
        var run = RunWithTwoEntities();
        var add = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership,
            "codex",
            "session-weapon-groups",
            21,
            "zone.0.entity.0");
        add.TargetEntityKey = "zone.0.entity.0";
        add.EquipmentIndex = 1;
        add.WeaponGroup = 0;
        add.ScalarValue = 1.0;
        var remove = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership,
            "codex",
            "session-weapon-groups",
            22,
            "zone.0.entity.0");
        remove.TargetEntityKey = "zone.0.entity.0";
        remove.EquipmentIndex = 0;
        remove.WeaponGroup = 0;
        remove.ScalarValue = 0.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { add, remove });

        Assert.AreEqual(2, result.AppliedCommandIds.Count);
        CollectionAssert.AreEqual(new[] { 1 }, run.Zones[0].Entities[0].WeaponGroups[0]);
    }

    [Test]
    public void DaemonOperationsRejectsWeaponGroupMembershipForMissingEquipment()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership,
            "codex",
            "session-weapon-groups",
            21,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.0";
        command.EquipmentIndex = 99;
        command.WeaponGroup = 0;
        command.ScalarValue = 1.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        CollectionAssert.AreEqual(new[] { 0 }, run.Zones[0].Entities[0].WeaponGroups[0]);
    }

    [Test]
    public void DaemonOperationsOwnsThermotoggleTargetInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetThermotoggleTargetTemperature,
            "codex",
            "session-thermotoggle",
            23,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.0";
        command.EquipmentIndex = 0;
        command.BehaviorIndex = 0;
        command.ScalarValue = 450.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(450.0, run.Zones[0].Entities[0].BehaviorStates[0].ThermotoggleTargetTemperature, 0.0001);
    }

    [Test]
    public void DaemonOperationsTransfersCargoItemsInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TransferCargoItem,
            "codex",
            "session-inventory",
            24,
            "zone.0.entity.0");
        command.TextValue = "ore";
        command.TargetEntityKey = "zone.0.entity.1";
        command.PositionX = 5;
        command.PositionY = 6;
        command.ScalarValue = 12;
        command.CargoTransfer.OriginEntityKey = "zone.0.entity.0";
        command.CargoTransfer.OriginCargoIndex = 0;
        command.CargoTransfer.DestinationEntityKey = "zone.0.entity.1";
        command.CargoTransfer.DestinationCargoIndex = 0;
        command.CargoTransfer.SourceX = 2;
        command.CargoTransfer.SourceY = 3;
        command.CargoTransfer.DestinationX = 5;
        command.CargoTransfer.DestinationY = 6;
        command.CargoTransfer.HasDestinationPosition = true;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var originCargo = run.Zones[0].Entities[0].CargoContents[0].Items;
        var destinationCargo = run.Zones[0].Entities[1].CargoContents[0].Items;
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(0, originCargo.Count);
        Assert.AreEqual(1, destinationCargo.Count);
        Assert.AreEqual("ore", destinationCargo[0].Item.ItemKey);
        Assert.AreEqual(5, destinationCargo[0].X);
        Assert.AreEqual(6, destinationCargo[0].Y);
    }

    [Test]
    public void DaemonOperationsRejectsSameCargoTransferWithoutReposition()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TransferCargoItem,
            "codex",
            "session-inventory",
            24,
            "zone.0.entity.0");
        command.TextValue = "ore";
        command.CargoTransfer.OriginEntityKey = "zone.0.entity.0";
        command.CargoTransfer.OriginCargoIndex = 0;
        command.CargoTransfer.DestinationEntityKey = "zone.0.entity.0";
        command.CargoTransfer.DestinationCargoIndex = 0;
        command.CargoTransfer.SourceX = 2;
        command.CargoTransfer.SourceY = 3;
        command.CargoTransfer.HasDestinationPosition = false;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var cargo = run.Zones[0].Entities[0].CargoContents[0].Items;
        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(1, cargo.Count);
        Assert.AreEqual(2, cargo[0].X);
        Assert.AreEqual(3, cargo[0].Y);
    }

    [Test]
    public void DaemonOperationsRepositionsCargoItemWithinSameCargoBay()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TransferCargoItem,
            "codex",
            "session-inventory",
            24,
            "zone.0.entity.0");
        command.TextValue = "ore";
        command.CargoTransfer.OriginEntityKey = "zone.0.entity.0";
        command.CargoTransfer.OriginCargoIndex = 0;
        command.CargoTransfer.DestinationEntityKey = "zone.0.entity.0";
        command.CargoTransfer.DestinationCargoIndex = 0;
        command.CargoTransfer.SourceX = 2;
        command.CargoTransfer.SourceY = 3;
        command.CargoTransfer.DestinationX = 6;
        command.CargoTransfer.DestinationY = 7;
        command.CargoTransfer.HasDestinationPosition = true;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var cargo = run.Zones[0].Entities[0].CargoContents[0].Items;
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, cargo.Count);
        Assert.AreEqual(6, cargo[0].X);
        Assert.AreEqual(7, cargo[0].Y);
    }

    [Test]
    public void DaemonOperationsEquipsCargoItemInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.EquipItem,
            "codex",
            "session-inventory",
            25,
            "zone.0.entity.0");
        command.TextValue = "ore";
        command.TargetEntityKey = "zone.0.entity.0";
        command.EquipmentTransfer.SourceKind = "cargo";
        command.EquipmentTransfer.OriginEntityKey = "zone.0.entity.0";
        command.EquipmentTransfer.OriginIndex = 0;
        command.EquipmentTransfer.DestinationEntityKey = "zone.0.entity.0";
        command.EquipmentTransfer.SourceX = 2;
        command.EquipmentTransfer.SourceY = 3;
        command.EquipmentTransfer.DestinationX = 8;
        command.EquipmentTransfer.DestinationY = 9;
        command.EquipmentTransfer.HasDestinationPosition = true;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var entity = run.Zones[0].Entities[0];
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(0, entity.CargoContents[0].Items.Count);
        Assert.AreEqual(3, entity.Equipment.Count);
        Assert.AreEqual("ore", entity.Equipment[2].Item.ItemKey);
        Assert.AreEqual(8, entity.Equipment[2].X);
        Assert.AreEqual(9, entity.Equipment[2].Y);
    }

    [Test]
    public void DaemonOperationsStoresEquipmentItemInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.StoreItem,
            "codex",
            "session-inventory",
            26,
            "zone.0.entity.0");
        command.TextValue = "laser";
        command.StoreItem.OriginEntityKey = "zone.0.entity.0";
        command.StoreItem.SourceEquipmentIndex = 1;
        command.StoreItem.DestinationEntityKey = "zone.0.entity.1";
        command.StoreItem.DestinationCargoIndex = 0;
        command.StoreItem.DestinationX = 4;
        command.StoreItem.DestinationY = 7;
        command.StoreItem.HasDestinationPosition = true;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var origin = run.Zones[0].Entities[0];
        var destinationCargo = run.Zones[0].Entities[1].CargoContents[0].Items;
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, origin.Equipment.Count);
        Assert.AreEqual(1, destinationCargo.Count);
        Assert.AreEqual("laser", destinationCargo[0].Item.ItemKey);
        Assert.AreEqual(4, destinationCargo[0].X);
        Assert.AreEqual(7, destinationCargo[0].Y);
        CollectionAssert.IsEmpty(origin.WeaponGroups[0]);
    }

    [Test]
    public void DaemonOperationsRejectsStoreItemWhenEquipmentSourceDoesNotMatch()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.StoreItem,
            "codex",
            "session-inventory",
            26,
            "zone.0.entity.0");
        command.TextValue = "laser";
        command.StoreItem.OriginEntityKey = "zone.0.entity.0";
        command.StoreItem.SourceEquipmentIndex = 0;
        command.StoreItem.DestinationEntityKey = "zone.0.entity.1";
        command.StoreItem.DestinationCargoIndex = 0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(2, run.Zones[0].Entities[0].Equipment.Count);
        Assert.AreEqual(0, run.Zones[0].Entities[1].CargoContents[0].Items.Count);
    }

    [Test]
    public void DaemonOperationsRejectsEquipItemWhenEquipmentSourceDoesNotMatch()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.EquipItem,
            "codex",
            "session-inventory",
            26,
            "zone.0.entity.0");
        command.TextValue = "laser";
        command.EquipmentTransfer.SourceKind = "equipment";
        command.EquipmentTransfer.OriginEntityKey = "zone.0.entity.0";
        command.EquipmentTransfer.OriginIndex = 0;
        command.EquipmentTransfer.DestinationEntityKey = "zone.0.entity.1";

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(2, run.Zones[0].Entities[0].Equipment.Count);
        Assert.AreEqual(0, run.Zones[0].Entities[1].Equipment.Count);
    }

    [Test]
    public void DaemonOperationsPicksUpLootInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.PickUpLoot,
            "codex",
            "session-loot",
            27,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.0";
        command.TextValue = "loot-cell";
        command.PositionX = 10;
        command.PositionY = 11;
        command.PositionZ = 12;
        command.ScalarValue = 1;
        command.LootPickup.ItemKey = "loot-cell";
        command.LootPickup.Quantity = 1;
        command.LootPickup.PositionX = 10;
        command.LootPickup.PositionY = 11;
        command.LootPickup.PositionZ = 12;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var zone = run.Zones[0];
        var cargo = zone.Entities[0].CargoContents[0].Items;
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(0, zone.DroppedPickups.Count);
        Assert.AreEqual(2, cargo.Count);
        Assert.AreEqual("loot-cell", cargo[1].Item.ItemKey);
    }

    [Test]
    public void DaemonOperationsTogglesHullConductivityInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity,
            "codex",
            "session-hull",
            28,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.0";
        command.PositionX = 1;
        command.PositionY = 0;
        command.ScalarValue = 0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var grid = run.Zones[0].Entities[0].StatGrids[0];
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        CollectionAssert.AreEqual(new[] { 1.0, 1.0, 0.0, 1.0 }, grid.Values);
    }

    [Test]
    public void DaemonOperationsOwnsShutdownPerformanceInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance,
            "codex",
            "session-shutdown-performance",
            29,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.0";
        command.ScalarValue = 0.375;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(0.375, run.Zones[0].Entities[0].ShutdownPerformance, 0.0001);
    }

    [Test]
    public void DaemonOperationsRejectsOutOfRangeShutdownPerformance()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance,
            "codex",
            "session-shutdown-performance",
            29,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.0";
        command.ScalarValue = 1.5;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(0.25, run.Zones[0].Entities[0].ShutdownPerformance, 0.0001);
    }

    [Test]
    public void DaemonOperationsOwnsTradePurchaseCreditsAndCargoInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TradePurchase,
            "codex",
            "session-trade",
            30,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.1";
        command.TextValue = "ore";
        command.ScalarValue = 200;
        command.TradePurchase.PurchaseKind = "commodity";
        command.TradePurchase.ItemKey = "ore";
        command.TradePurchase.Quantity = 5;
        command.TradePurchase.UnitPrice = 40;
        command.TradePurchase.TotalPrice = 200;
        command.TradePurchase.StationEntityKey = "zone.0.entity.0";
        command.TradePurchase.StationCargoIndex = 0;
        command.TradePurchase.TargetEntityKey = "zone.0.entity.1";
        command.TradePurchase.TargetCargoIndex = 0;
        command.TradePurchase.SourceX = 2;
        command.TradePurchase.SourceY = 3;
        command.TradePurchase.CreatesDockedShip = false;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var stationCargo = run.Zones[0].Entities[0].CargoContents[0].Items;
        var targetCargo = run.Zones[0].Entities[1].CargoContents[0].Items;
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(800, run.Credits);
        Assert.AreEqual(1, stationCargo.Count);
        Assert.AreEqual(7, stationCargo[0].Item.Quantity);
        Assert.AreEqual(1, targetCargo.Count);
        Assert.AreEqual("ore", targetCargo[0].Item.ItemKey);
        Assert.AreEqual(5, targetCargo[0].Item.Quantity);
    }

    [Test]
    public void DaemonOperationsRestoresLoadoutIntoDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.RestoreLoadout,
            "codex",
            "session-loadout",
            31,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.0";
        command.TextValue = "Starter";
        command.ScalarValue = 250;
        command.LoadoutRestore.DockedEntityKey = "zone.0.entity.0";
        command.LoadoutRestore.TemplateName = "Starter";
        command.LoadoutRestore.Price = 250;

        var result = AetheriaRuntimeDaemonOperations.Execute(
            run,
            new[] { command },
            new AetheriaRuntimeDaemonOperationContext
            {
                LoadoutTemplates = new[]
                {
                    new AetheriaRuntimeLoadoutTemplateCommit
                    {
                        Name = "Starter",
                        RootEntity = new AetheriaRuntimeEntityLoadoutCommit
                        {
                            Name = "Starter Ship",
                            Kind = "ship",
                            Hull = new AetheriaRuntimeLoadoutItemCommit
                            {
                                ItemKey = "starter-hull"
                            },
                            Equipment = new[]
                            {
                                new AetheriaRuntimeLoadoutItemSlotCommit
                                {
                                    X = 1,
                                    Y = 2,
                                    Item = new AetheriaRuntimeLoadoutItemCommit
                                    {
                                        ItemKey = "starter-reactor"
                                    }
                                }
                            },
                            WeaponGroups = new[]
                            {
                                new[] { 0 }
                            }
                        }
                    }
                }
            });

        var zone = run.Zones[0];
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(750, run.Credits);
        Assert.AreEqual(3, zone.Entities.Count);
        Assert.AreEqual("Starter Ship", zone.Entities[2].Name);
        Assert.AreEqual("starter-hull", zone.Entities[2].HullItemKey);
        Assert.AreEqual("starter-reactor", zone.Entities[2].Equipment[0].Item.ItemKey);
        CollectionAssert.Contains(zone.Entities[0].ChildEntityIndices, 2);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.0.entity.2.v1", run.CurrentEntityKey);
    }

    [Test]
    public void DaemonOperationsDestroysEntityInDaemonState()
    {
        var run = RunWithTwoEntities();
        run.CurrentEntityKey = "zone.0.entity.1";
        run.Zones[0].Entities[0].TargetEntityIndex = 1;
        run.Zones[0].Entities[0].ChildEntityIndices = new[] { 1 };
        run.Zones[0].Entities[0].DockingBayAssignments = new[] { 1, -1 };
        run.Zones[0].Entities[0].Contacts = new[]
        {
            new AetheriaRuntimeEntityContactCommit
            {
                TargetEntityIndex = 1,
                Visible = true
            }
        };
        run.Zones[0].Entities = new[]
        {
            run.Zones[0].Entities[0],
            run.Zones[0].Entities[1],
            new AetheriaRuntimeEntitySnapshotCommit
            {
                EntityIndex = 2,
                Name = "Reindexed"
            }
        };
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.DestroyEntity,
            "codex",
            "session-destroy",
            44,
            "");
        command.TargetEntityKey = "zone.0.entity.1";

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(2, run.Zones[0].Entities.Count);
        Assert.AreEqual("Player", run.Zones[0].Entities[0].Name);
        Assert.AreEqual(0, run.Zones[0].Entities[0].EntityIndex);
        Assert.AreEqual("Reindexed", run.Zones[0].Entities[1].Name);
        Assert.AreEqual(1, run.Zones[0].Entities[1].EntityIndex);
        Assert.AreEqual(-1, run.Zones[0].Entities[0].TargetEntityIndex);
        Assert.AreEqual(0, run.Zones[0].Entities[0].ChildEntityIndices.Count);
        Assert.AreEqual(-1, run.Zones[0].Entities[0].DockingBayAssignments[0]);
        Assert.AreEqual(-1, run.Zones[0].Entities[0].DockingBayAssignments[1]);
        Assert.AreEqual(0, run.Zones[0].Entities[0].Contacts.Count);
        Assert.AreEqual("", run.CurrentEntityKey);
    }

    [Test]
    public void DaemonOperationsMovesEntityThroughWormholeInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.EnterWormhole,
            "codex",
            "session-wormhole",
            45,
            "zone.0.entity.0");
        command.TargetZoneIndex = 2;
        command.PositionX = 100;
        command.PositionY = 200;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var sourceZone = FindZone(run, 0);
        var targetZone = FindZone(run, 2);
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(2, run.CurrentZoneIndex);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.2.entity.0.v1", run.CurrentEntityKey);
        Assert.AreEqual(1, sourceZone.Entities.Count);
        Assert.AreEqual("Target", sourceZone.Entities[0].Name);
        Assert.AreEqual(0, sourceZone.Entities[0].EntityIndex);
        Assert.AreEqual(1, targetZone.Entities.Count);
        Assert.AreEqual("Player", targetZone.Entities[0].Name);
        Assert.AreEqual(0, targetZone.Entities[0].EntityIndex);
        Assert.AreEqual(100, targetZone.Entities[0].PositionX, 0.0001);
        Assert.AreEqual(200, targetZone.Entities[0].PositionZ, 0.0001);
        CollectionAssert.Contains(run.DiscoveredZoneIndices, 2);
        Assert.AreEqual(1, result.Intents.Wormholes.Count);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.2.entity.0.v1", result.Intents.Wormholes[0].ActorEntityKey);
        Assert.AreEqual(2, result.Intents.Wormholes[0].TargetZoneIndex);
    }

    [Test]
    public void DaemonOperationsRejectsWormholeMoveWhenEntityIsDocked()
    {
        var run = RunWithTwoEntities();
        run.Zones[0].Entities[1].ChildEntityIndices = new[] { 0 };
        run.Zones[0].Entities[1].DockingBayAssignments = new[] { 0, -1 };
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.EnterWormhole,
            "codex",
            "session-wormhole",
            45,
            "zone.0.entity.0");
        command.TargetZoneIndex = 2;
        command.PositionX = 100;
        command.PositionY = 200;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(0, result.Intents.Wormholes.Count);
        Assert.AreEqual(1, run.Zones[0].Entities[1].ChildEntityIndices.Count);
        CollectionAssert.Contains(run.Zones[0].Entities[1].DockingBayAssignments, 0);
    }

    [Test]
    public void DaemonOperationsTowsEntityToStationInDaemonState()
    {
        var run = RunWithTwoEntities();
        run.Zones = new[]
        {
            run.Zones[0],
            new AetheriaRuntimeZoneSnapshotCommit
            {
                ZoneIndex = 3,
                Entities = new[]
                {
                    new AetheriaRuntimeEntitySnapshotCommit
                    {
                        EntityIndex = 0,
                        Name = "Tow Station"
                    }
                }
            }
        };
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TowToStation,
            "codex",
            "session-towing",
            46,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.3.entity.0";
        command.TargetZoneIndex = 3;
        command.PositionX = 300;
        command.PositionY = 400;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var sourceZone = FindZone(run, 0);
        var targetZone = FindZone(run, 3);
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(3, run.CurrentZoneIndex);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.3.entity.1.v1", run.CurrentEntityKey);
        Assert.AreEqual(1, sourceZone.Entities.Count);
        Assert.AreEqual("Target", sourceZone.Entities[0].Name);
        Assert.AreEqual(2, targetZone.Entities.Count);
        Assert.AreEqual("Tow Station", targetZone.Entities[0].Name);
        Assert.AreEqual("Player", targetZone.Entities[1].Name);
        Assert.AreEqual(1, targetZone.Entities[1].EntityIndex);
        Assert.AreEqual(300, targetZone.Entities[1].PositionX, 0.0001);
        Assert.AreEqual(400, targetZone.Entities[1].PositionZ, 0.0001);
        Assert.AreEqual(1, result.Intents.Towing.Count);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.3.entity.1.v1", result.Intents.Towing[0].ActorEntityKey);
        Assert.AreEqual("zone.3.entity.0", result.Intents.Towing[0].StationEntityKey);
        Assert.AreEqual(3, result.Intents.Towing[0].TargetZoneIndex);
    }

    [Test]
    public void DaemonOperationsSetsDockedCurrentShipAsDaemonRunCursor()
    {
        var run = RunWithTwoEntities();
        run.Zones = new[]
        {
            run.Zones[0],
            new AetheriaRuntimeZoneSnapshotCommit
            {
                ZoneIndex = 2,
                Entities = new[]
                {
                    new AetheriaRuntimeEntitySnapshotCommit
                    {
                        EntityIndex = 0,
                        Name = "Docking Station",
                        DockingBayAssignments = new[] { 1 }
                    },
                    new AetheriaRuntimeEntitySnapshotCommit
                    {
                        EntityIndex = 1,
                        Name = "Docked Scout"
                    }
                }
            }
        };
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip,
            "codex",
            "session-current",
            46,
            "");
        command.TargetEntityKey = "zone.2.entity.1";

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(2, run.CurrentZoneIndex);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.2.entity.1.v1", run.CurrentEntityKey);
    }

    [Test]
    public void DaemonOperationsRejectsUndockedCurrentShipSelection()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip,
            "codex",
            "session-current",
            46,
            "");
        command.TargetEntityKey = "zone.0.entity.1";

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(0, run.CurrentZoneIndex);
        Assert.AreEqual("zone.0.entity.0", run.CurrentEntityKey);
    }

    [Test]
    public void DaemonOperationsRecordsSimulationIntentsForDaemonLoop()
    {
        var run = RunWithTwoEntities();
        var move = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetMoveVector,
            "codex",
            "session-intents",
            32,
            "");
        move.DirectionX = 0.25;
        move.DirectionY = -0.5;
        move.ScalarValue = 0.75;
        var fire = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup,
            "codex",
            "session-intents",
            33,
            "zone.0.entity.0");
        fire.WeaponGroup = 1;
        var behavior = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive,
            "codex",
            "session-intents",
            34,
            "zone.0.entity.0");
        behavior.EquipmentIndex = 0;
        behavior.BehaviorIndex = 0;
        behavior.ScalarValue = 1.0;
        var consumable = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.ActivateConsumable,
            "codex",
            "session-intents",
            35,
            "zone.0.entity.0");
        consumable.TextValue = "repair-gel";
        var ping = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SensorPing,
            "codex",
            "session-intents",
            36,
            "zone.0.entity.0");

        var result = AetheriaRuntimeDaemonOperations.Execute(
            run,
            new[] { move, fire, behavior, consumable, ping });

        Assert.AreEqual(5, result.AppliedCommandIds.Count);
        Assert.IsTrue(result.Intents.HasAny);
        Assert.AreEqual("zone.0.entity.0", result.Intents.Movement.ActorEntityKey);
        Assert.AreEqual(0.25, result.Intents.Movement.DirectionX, 0.0001);
        Assert.AreEqual(-0.5, result.Intents.Movement.DirectionY, 0.0001);
        Assert.AreEqual(0.75, result.Intents.Movement.Magnitude, 0.0001);
        Assert.AreEqual(1, result.Intents.WeaponGroups.Count);
        Assert.IsTrue(result.Intents.WeaponGroups[0].Fire);
        Assert.AreEqual(1, result.Intents.WeaponGroups[0].WeaponGroup);
        Assert.AreEqual(1, result.Intents.Behaviors.Count);
        Assert.IsTrue(result.Intents.Behaviors[0].Active);
        Assert.AreEqual(1, result.Intents.Consumables.Count);
        Assert.AreEqual("repair-gel", result.Intents.Consumables[0].ItemKey);
        Assert.IsTrue(result.Intents.SensorPingRequested);
    }

    [Test]
    public void DaemonOperationsRejectsWeaponGroupIntentForMissingGroup()
    {
        var run = RunWithTwoEntities();
        var fire = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup,
            "codex",
            "session-intents",
            33,
            "zone.0.entity.0");
        fire.WeaponGroup = 99;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { fire });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(0, result.Intents.WeaponGroups.Count);
    }

    [Test]
    public void DaemonOperationsRejectsBehaviorIntentForMissingBehavior()
    {
        var run = RunWithTwoEntities();
        var behavior = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive,
            "codex",
            "session-intents",
            34,
            "zone.0.entity.0");
        behavior.EquipmentIndex = 0;
        behavior.BehaviorIndex = 99;
        behavior.ScalarValue = 1.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { behavior });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(0, result.Intents.Behaviors.Count);
    }

    [Test]
    public void DaemonOperationsRecordsNavigationIntentsForDaemonLoop()
    {
        var run = RunWithTwoEntities();
        var dock = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.Dock,
            "codex",
            "session-navigation",
            37,
            "zone.0.entity.0");
        dock.TargetEntityKey = "zone.0.entity.1";

        var result = AetheriaRuntimeDaemonOperations.Execute(
            run,
            new[] { dock });

        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.Intents.Docking.Count);
        Assert.IsTrue(result.Intents.Docking[0].Dock);
        Assert.AreEqual("zone.0.entity.1", result.Intents.Docking[0].TargetEntityKey);
        CollectionAssert.Contains(run.Zones[0].Entities[1].ChildEntityIndices, 0);
        CollectionAssert.Contains(run.Zones[0].Entities[1].DockingBayAssignments, 0);

        var undock = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.Undock,
            "codex",
            "session-navigation",
            40,
            "zone.0.entity.0");
        var undockResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { undock });

        Assert.AreEqual(1, undockResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, undockResult.Intents.Docking.Count);
        Assert.IsTrue(undockResult.Intents.Docking[0].Undock);
        Assert.AreEqual(0, run.Zones[0].Entities[1].ChildEntityIndices.Count);
        CollectionAssert.DoesNotContain(run.Zones[0].Entities[1].DockingBayAssignments, 0);
    }

    [Test]
    public void DaemonOperationsSelectsNearestDockTargetFromAuthoritativeSnapshot()
    {
        var run = RunWithTwoEntities();
        var zone = run.Zones[0];
        zone.Entities[0].PositionX = 0;
        zone.Entities[0].PositionY = 0;
        zone.Entities[1].PositionX = 3;
        zone.Entities[1].PositionY = 4;
        zone.Entities = zone.Entities
            .Concat(new[]
            {
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 2,
                    Name = "Far Dock",
                    PositionX = 8,
                    PositionY = 0
                }
            })
            .ToArray();

        var dock = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.DockNearest,
            "codex",
            "session-navigation",
            37,
            "zone.0.entity.0");
        dock.ScalarValue = 6.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { dock });

        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.Intents.Docking.Count);
        Assert.IsTrue(result.Intents.Docking[0].Dock);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.0.entity.1.v1", result.Intents.Docking[0].TargetEntityKey);
        CollectionAssert.Contains(zone.Entities[1].ChildEntityIndices, 0);
        CollectionAssert.Contains(zone.Entities[1].DockingBayAssignments, 0);
        CollectionAssert.DoesNotContain(zone.Entities[2].ChildEntityIndices, 0);
    }

    [Test]
    public void DaemonOperationsRejectsUndockWhenEntityIsNotDocked()
    {
        var run = RunWithTwoEntities();
        var undock = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.Undock,
            "codex",
            "session-navigation",
            40,
            "zone.0.entity.0");

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { undock });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(0, result.Intents.Docking.Count);
    }

    [Test]
    public void DaemonOperationsRejectsDockWhenEntityIsAlreadyDocked()
    {
        var run = RunWithTwoEntities();
        var dock = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.Dock,
            "codex",
            "session-navigation",
            37,
            "zone.0.entity.0");
        dock.TargetEntityKey = "zone.0.entity.1";

        var firstResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { dock });
        var secondResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { dock });

        Assert.AreEqual(1, firstResult.AppliedCommandIds.Count);
        Assert.AreEqual(0, secondResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, secondResult.RejectedCommandIds.Count);
        Assert.AreEqual(0, secondResult.Intents.Docking.Count);
        CollectionAssert.Contains(run.Zones[0].Entities[1].ChildEntityIndices, 0);
    }

    [Test]
    public void CommandClientCanSendWithoutObservedState()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-command-client-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var client = new AetheriaRuntimeDaemonOperationClient(statePath, "headless-client", "session-headless");

        var envelope = client.SetMoveVector(null, 0.25, -0.75, 0.5);

        Assert.AreEqual(AetheriaRuntimeDaemonCommandKinds.SetMoveVector, envelope.Kind);
        Assert.AreEqual("headless-client", envelope.ClientId);
        Assert.AreEqual("session-headless", envelope.SessionId);
        Assert.AreEqual(-1, envelope.ObservedFrameId);
        Assert.AreEqual("", envelope.ActorEntityKey);
        Assert.IsEmpty(envelope.Path);
    }

    private static AetheriaRuntimeRunCheckpointCommit RunWithTwoEntities()
    {
        return new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "daemon-command-apply-run",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Credits = 1000,
            Zones = new[]
            {
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    Entities = new[]
                    {
                        new AetheriaRuntimeEntitySnapshotCommit
                        {
                            EntityIndex = 0,
                            Name = "Player",
                            TargetEntityIndex = -1,
                            ShutdownPerformance = 0.25,
                            Contacts = new[]
                            {
                                new AetheriaRuntimeEntityContactCommit
                                {
                                    TargetEntityIndex = 1,
                                    Visible = true,
                                    Hostile = true
                                }
                            },
                            Equipment = new[]
                            {
                                new AetheriaRuntimeLoadoutItemSlotCommit
                                {
                                    Item = new AetheriaRuntimeLoadoutItemCommit
                                    {
                                        ItemKey = "reactor",
                                        Enabled = true
                                    }
                                },
                                new AetheriaRuntimeLoadoutItemSlotCommit
                                {
                                    Item = new AetheriaRuntimeLoadoutItemCommit
                                    {
                                        ItemKey = "laser",
                                        Enabled = true
                                    }
                                }
                            },
                            WeaponGroups = new[]
                            {
                                new[] { 0 },
                                new[] { 1 },
                                Array.Empty<int>()
                            },
                            BehaviorStates = new[]
                            {
                                new AetheriaRuntimeBehaviorStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 0,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "Thermotoggle",
                                    ThermotoggleTargetTemperature = 300.0
                                },
                                new AetheriaRuntimeBehaviorStateCommit
                                {
                                    OwnerKind = "equipment",
                                    OwnerIndex = 1,
                                    BehaviorIndex = 0,
                                    BehaviorKind = "Shield"
                                }
                            },
                            CargoContents = new[]
                            {
                                new AetheriaRuntimeCargoBayLoadoutCommit
                                {
                                    Items = new[]
                                    {
                                        new AetheriaRuntimeLoadoutItemSlotCommit
                                        {
                                            X = 2,
                                            Y = 3,
                                            Item = new AetheriaRuntimeLoadoutItemCommit
                                            {
                                                ItemKey = "ore",
                                                Quantity = 12
                                            }
                                        }
                                    }
                                }
                            },
                            StatGrids = new[]
                            {
                                new AetheriaRuntimeEntityStatGridCommit
                                {
                                    Name = "hull_conductivity_x",
                                    Width = 2,
                                    Height = 2,
                                    Values = new[] { 1.0, 0.0, 0.0, 1.0 }
                                },
                                new AetheriaRuntimeEntityStatGridCommit
                                {
                                    Name = "hull_conductivity_y",
                                    Width = 2,
                                    Height = 2,
                                    Values = new[] { 0.0, 1.0, 1.0, 0.0 }
                                }
                            }
                        },
                        new AetheriaRuntimeEntitySnapshotCommit
                        {
                            EntityIndex = 1,
                            Name = "Target",
                            TargetEntityIndex = -1,
                            CargoContents = new[]
                            {
                                new AetheriaRuntimeCargoBayLoadoutCommit()
                            }
                        }
                    },
                    DroppedPickups = new[]
                {
                    new AetheriaRuntimeDroppedPickupCommit
                    {
                        PickupIndex = 0,
                        PositionX = 10,
                        PositionY = 11,
                        PositionZ = 12,
                        Item = new AetheriaRuntimeLoadoutItemCommit
                        {
                            ItemKey = "loot-cell",
                            Quantity = 1
                        }
                    }
                }
                }
            }
        };
    }

    private static AetheriaRuntimeZoneSnapshotCommit FindZone(AetheriaRuntimeRunCheckpointCommit run, int zoneIndex)
    {
        foreach (var zone in run.Zones)
        {
            if (zone.ZoneIndex == zoneIndex)
                return zone;
        }

        Assert.Fail("Expected zone " + zoneIndex);
        return null;
    }

    private static AetheriaRuntimeObservedDaemonState ObservedState(long frameId, long soaGeneration)
    {
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            new AetheriaRuntimeRunCheckpointCommit { RunId = "cursor-run" },
            "daemon",
            "session",
            frameId,
            0,
            0.02);
        var soaView = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "daemon",
            "session",
            frameId,
            soaGeneration,
            new[] { new AetheriaRuntimeDaemonSoaBufferDocument { BufferId = "hot" } },
            new[] { new AetheriaRuntimeDaemonSoaColumnDocument { ColumnId = "position-x", BufferId = "hot" } });

        return new AetheriaRuntimeObservedDaemonState(
            frame,
            soaView,
            "state.cc.daemon.frame.cc",
            "state.cc.daemon.soa.cc");
    }

    private static bool ContainsSurfaceMetric(
        AetheriaRuntimeSurfaceComponent component,
        string label,
        string value)
    {
        if (component.Kind == "metric" &&
            component.Props.TryGetValue("label", out var actualLabel) &&
            component.Props.TryGetValue("value", out var actualValue) &&
            actualLabel == label &&
            actualValue == value)
        {
            return true;
        }

        return component.Children.Any(child => ContainsSurfaceMetric(child, label, value));
    }

    private static bool ContainsSurfaceProp(
        AetheriaRuntimeSurfaceComponent component,
        string key,
        string value)
    {
        if (component.Props.TryGetValue(key, out var actual) &&
            string.Equals(actual, value, StringComparison.Ordinal))
        {
            return true;
        }

        return component.Children.Any(child => ContainsSurfaceProp(child, key, value));
    }
}
