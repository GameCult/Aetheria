using CultMath;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using EveCommandTemplate = GameCult.Eve.Surface.EveCommandTemplate;
using EveStyleToken = GameCult.Eve.Surface.EveStyleToken;
using EveSurfaceCommandRequest = GameCult.Eve.Surface.EveSurfaceCommandRequest;
using EveSurfaceComponent = GameCult.Eve.Surface.EveSurfaceComponent;
using EveSurfaceDocument = GameCult.Eve.Surface.EveSurfaceDocument;
using EveSurfaceTree = GameCult.Eve.Surface.EveSurfaceTree;

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
    public void ManagedLatestFramePublishesAuthoritativeDaemonFrame()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-managed-frame-tests",
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

        PublishLatestFrameThroughVerseClient(statePath, frame);
        using var client = AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "daemon-frame-read-test", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        var published = client
            .MutableDocument<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
            .ReadAsync()
            .GetAwaiter()
            .GetResult();

        Assert.IsNotNull(published);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Frame, published.Schema);
        Assert.AreEqual("aetheria-daemon", published.DaemonId);
        Assert.AreEqual("session-3", published.SessionId);
        Assert.AreEqual(123, published.FrameId);
        Assert.IsTrue(published.IsAuthoritative);
        Assert.AreEqual("daemon", published.StateSource);
        Assert.AreEqual("run-2", published.Run.RunId);
        Assert.AreEqual(7, published.Run.CurrentZoneIndex);
        Assert.AreEqual("entity:ship", published.Run.CurrentEntityKey);
    }

    [Test]
    public void AetheriaClientStateReadsProjectedDocumentsThroughCultMesh()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-client-state-document-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            RunWithTwoEntities(),
            "daemon",
            "session-state",
            7,
            1.25,
            0.02);
        var authorityPolicy = AetheriaRuntimeVerseAuthorityPolicyDocument.TrustedCoop(
            "aetheria.local",
            "daemon");
        PublishLatestFrameThroughVerseClient(statePath, frame);
        PublishVerseAuthorityPolicyThroughVerseClient(statePath, authorityPolicy);

        using var client = AetheriaClient
            .OpenAsync(statePath, "unity-test", pullOnOpen: true)
            .GetAwaiter()
            .GetResult();

        var currentEntity = client.State.Latest<AetheriaRuntimeCurrentEntityDocument>();
        var latestFrame = client.State.Latest<AetheriaRuntimeDaemonFrameDocument>();
        var catalog = client.State.Latest<AetheriaRuntimeCatalogSnapshot>();
        var playerSettings = client.State.Latest<AetheriaRuntimePlayerSettingsDocument>();
        var verseHostSettings = client.State.Latest<AetheriaRuntimeVerseHostSettingsDocument>();
        var currentEntityByType = client.State
            .LatestAsync<AetheriaRuntimeCurrentEntityDocument>()
            .GetAwaiter()
            .GetResult();
        var latestFrameByType = client.State
            .LatestAsync<AetheriaRuntimeDaemonFrameDocument>()
            .GetAwaiter()
            .GetResult();
        var viewport = new AetheriaRuntimeRtsViewportBounds
        {
            MinX = -100,
            MinY = -100,
            MaxX = 100,
            MaxY = 100
        };
        var objectsViewport = client.State
            .Viewports
            .Latest<AetheriaRuntimeObjectsViewportDocument>(viewport);
        var zoneDetails = client.State
            .Details
            .Latest<AetheriaRuntimeZoneDetailsDocument>(0);
        var inventory = client.State
            .Details
            .Latest<AetheriaRuntimeInventoryDocument>(0);
        var currentEntityFromClientType = client.State
            .LatestAsync<AetheriaRuntimeCurrentEntityDocument>()
            .GetAwaiter()
            .GetResult();
        using var currentEntityReactive = client.State
            .ReactiveAsync<AetheriaRuntimeCurrentEntityDocument>()
            .GetAwaiter()
            .GetResult();
        using var zoneRenderReactive = client.State
            .Reactive<AetheriaRuntimeZoneRenderDocument>();
        var observedAuthoritativeFrame = client.State.Latest<AetheriaRuntimeDaemonFrameDocument>();
        using var catalogReactive = client.State.Reactive<AetheriaRuntimeCatalogSnapshot>();
        using var daemonFrameReactive = client.State.Reactive<AetheriaRuntimeDaemonFrameDocument>();
        using var daemonSoaViewReactive = client.State.Reactive<AetheriaRuntimeDaemonSoaViewDocument>();
        Assert.IsTrue(AetheriaRuntimeObservedDaemonState.TryCreateCurrent(
            daemonFrameReactive,
            daemonSoaViewReactive,
            zoneRenderReactive,
            out var observed));
        using var loadoutTemplatesReactive = client.State.Reactive<AetheriaRuntimeLoadoutTemplatesDocument>();
        using var sectorMapReactive = client.State.Reactive<AetheriaRuntimeSectorMapDocument>();
        using var playerSettingsReactive = client.State.Reactive<AetheriaRuntimePlayerSettingsDocument>();
        using var verseHostSettingsReactive = client.State.Reactive<AetheriaRuntimeVerseHostSettingsDocument>();
        using var zoneContactsReactive = client.State.Reactive<AetheriaRuntimeZoneContactsDocument>();
        using var currentZoneReactive = client.State.Reactive<AetheriaRuntimeCurrentZoneDocument>();
        using var currentEntityDocumentReactive = client.State.Reactive<AetheriaRuntimeCurrentEntityDocument>();
        using var currentDockingReactive = client.State.Reactive<AetheriaRuntimeCurrentDockingDocument>();
        using var stationRefitReactive = client.State.Reactive<AetheriaRuntimeStationRefitDocument>();
        using var zoneDetailsReactive = client.State.Reactive<AetheriaRuntimeZoneDetailsDocument>(0);
        using var selectedObjectReactive = client.State.Reactive<AetheriaRuntimeSelectedObjectDocument>(0);
        using var inventoryReactive = client.State.Reactive<AetheriaRuntimeInventoryDocument>(0);
        using var mapViewportReactive = client.State.Reactive<AetheriaRuntimeRtsViewportDocument>(viewport);
        using var objectsViewportReactive = client.State.Reactive<AetheriaRuntimeObjectsViewportDocument>(viewport);
        using var gravityViewportReactive = client.State.Reactive<AetheriaRuntimeGravityViewportDocument>(viewport);
        using var renderSplatsViewportReactive = client.State.Reactive<AetheriaRuntimeRenderSplatsViewportDocument>(viewport);
        using var playerHudCatalog = client.State.Reactive<AetheriaRuntimeCatalogSnapshot>();
        using var playerHudSettings = client.State.Reactive<AetheriaRuntimePlayerSettingsDocument>();
        using var playerHudEntity = client.State.Reactive<AetheriaRuntimeCurrentEntityDocument>();
        var gameSurface = client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Game);
        var gameTuiSurface = client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.GameTui);
        var editorSurface = client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Editor);
        var editorTuiSurface = client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.EditorTui);
        var authorityStatus = client.State.Latest<AetheriaRuntimeVerseAuthorityPolicyDocument>();
        var currentDocking = client.State.CurrentDockingDocument
            .LatestAsync()
            .GetAwaiter()
            .GetResult();
        var stationRefit = client.State.Latest<AetheriaRuntimeStationRefitDocument>();
        Assert.IsTrue(AetheriaRuntimeObservedDockingState.TryCreateCurrent(
            currentEntityDocumentReactive,
            currentDockingReactive,
            stationRefitReactive,
            out var observedDocking));
        using var reactiveGameSurface = client.State.Reactive<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Game);
        using var reactiveGameTuiSurface = client.State.Reactive<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.GameTui);
        using var reactiveEditorSurface = client.State.Reactive<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Editor);
        using var reactiveEditorTuiSurface = client.State.Reactive<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.EditorTui);

        Assert.AreEqual("aetheria.current.entity", client.State.CurrentEntity.DocumentId);
        Assert.AreSame(client.State.CurrentEntity, client.State.Document<AetheriaRuntimeCurrentEntityDocument>());
        Assert.AreSame(client.State.Catalog, client.State.Document<AetheriaRuntimeCatalogSnapshot>());
        Assert.AreSame(client.State.ZoneRender, client.State.Document<AetheriaRuntimeZoneRenderDocument>());
        Assert.AreSame(
            client.State.StarbridgeSummary,
            client.State.Document<AetheriaRuntimeStarbridgeSessionSummaryDocument>());
        Assert.AreSame(
            client.State.StarbridgeSummary,
            client.State.DocumentBySchema(AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary));
        Assert.AreSame(client.State.LatestFrame, client.State.Document<AetheriaRuntimeDaemonFrameDocument>());
        Assert.AreSame(client.State.LatestSoaView, client.State.Document<AetheriaRuntimeDaemonSoaViewDocument>());
        Assert.IsNotNull(client.State.Latest<AetheriaRuntimeDaemonProviderAdvertisementDocument>());
        Assert.IsNotNull(client.State.Latest<AetheriaRuntimeDaemonHealthDocument>());
        Assert.IsNotNull(client.State.Latest<AetheriaRuntimeDaemonCommandBoundaryDocument>());
        Assert.AreSame(
            client.State.Document<AetheriaRuntimeVerseAuthorityPolicyDocument>(),
            client.State.DocumentBySchema(AetheriaRuntimeVerseAuthoritySchemas.Policy));
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, reactiveGameSurface.Current.Surface.Id);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, reactiveGameTuiSurface.Current.Surface.Id);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, reactiveEditorSurface.Current.Surface.Id);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, reactiveEditorTuiSurface.Current.Surface.Id);
        Assert.AreEqual(currentDocking.CurrentEntityKey, currentDockingReactive.Current.CurrentEntityKey);
        Assert.AreEqual(stationRefit.StationEntityKey, stationRefitReactive.Current.StationEntityKey);
        Assert.AreSame(client.State.PlayerSettings, client.State.Document<AetheriaRuntimePlayerSettingsDocument>());
        Assert.AreSame(client.State.VerseHostSettings, client.State.Document<AetheriaRuntimeVerseHostSettingsDocument>());
        Assert.AreSame(
            client.State.CurrentEntity,
            client.State.DocumentBySchema(AetheriaRuntimeDaemonSchemas.CurrentEntity));
        Assert.AreSame(
            client.State.ZoneContacts,
            client.State.DocumentBySchema("gamecult.aetheria.zone_contacts"));
        Assert.AreEqual(
            typeof(AetheriaRuntimeCurrentEntityDocument),
            client.State.DocumentBySchema(AetheriaRuntimeDaemonSchemas.CurrentEntity).DocumentType);
        Assert.IsTrue(client.State.TryGetDocument<AetheriaRuntimeSectorMapDocument>(out var sectorMapDocument));
        Assert.AreSame(client.State.SectorMap, sectorMapDocument);
        Assert.IsTrue(client.State.TryGetDocumentBySchema(
            AetheriaRuntimeDaemonSchemas.SectorMap,
            out var sectorMapBySchema));
        Assert.AreSame(client.State.SectorMap, sectorMapBySchema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.CurrentEntity, currentEntity.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Frame, latestFrame.Schema);
        Assert.AreEqual(frame.FrameId, latestFrame.FrameId);
        Assert.AreEqual(frame.FrameId, latestFrameByType.FrameId);
        Assert.AreEqual(frame.FrameId, observed.Frame.FrameId);
        Assert.AreEqual(frame.FrameId, observedAuthoritativeFrame.FrameId);
        Assert.IsTrue(observedAuthoritativeFrame.IsAuthoritative);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, gameSurface.Surface.Id);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, gameTuiSurface.Surface.Id);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, editorSurface.Surface.Id);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, editorTuiSurface.Surface.Id);
        Assert.IsNotNull(authorityStatus);
        Assert.AreEqual(AetheriaRuntimeVerseAuthoritySchemas.Policy, authorityStatus!.Schema);
        Assert.AreEqual(authorityPolicy.HostRuntimeId, authorityStatus.HostRuntimeId);
        Assert.AreEqual(AetheriaRuntimeCatalogSnapshot.SchemaId, client.State.Catalog.Sources.Last().SchemaId);
        Assert.GreaterOrEqual(catalog.Items.Count, 0);
        Assert.AreEqual(AetheriaRuntimePlayerSettingsDocument.SchemaId, playerSettings.Schema);
        Assert.AreSame(catalog, catalogReactive.Current);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Frame, daemonFrameReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.SoaView, daemonSoaViewReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeLoadoutTemplatesDocument.SchemaId, loadoutTemplatesReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.SectorMap, sectorMapReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimePlayerSettingsDocument.SchemaId, playerSettingsReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeVerseHostSettingsDocument.SchemaId, verseHostSettings.Schema);
        Assert.AreEqual(AetheriaRuntimeVerseHostSettingsDocument.SchemaId, verseHostSettingsReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ObjectsViewport, objectsViewport.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.RtsViewport, mapViewportReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ObjectsViewport, objectsViewportReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.GravityViewport, gravityViewportReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.RenderSplatsViewport, renderSplatsViewportReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ZoneContacts, zoneContactsReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.CurrentZone, currentZoneReactive.Current.Schema);
        Assert.AreEqual(currentEntity.EntityKey, currentEntityDocumentReactive.Current.EntityKey);
        Assert.AreEqual("Player", objectsViewport.Objects.FirstOrDefault()?.DisplayName);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ZoneDetails, zoneDetails.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ZoneDetails, zoneDetailsReactive.Current.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.SelectedObject, selectedObjectReactive.Current.Schema);
        Assert.AreEqual(0, zoneDetails.ZoneIndex);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Inventory, inventory.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Inventory, inventoryReactive.Current.Schema);
        Assert.AreEqual("zone.0.entity.0", inventory.EntityKey);
        Assert.AreEqual("zone.0.entity.0", currentEntity.EntityKey);
        Assert.AreEqual(0, currentEntity.EntityIndex);
        Assert.AreEqual("Player", currentEntity.Entity?.DisplayName);
        Assert.AreEqual(currentEntity.EntityKey, playerHudEntity.Current.EntityKey);
        Assert.AreSame(catalog, playerHudCatalog.Current);
        Assert.AreEqual(AetheriaRuntimePlayerSettingsDocument.SchemaId, playerHudSettings.Current.Schema);
        Assert.AreEqual(currentEntity.Hud.Visibility, playerHudEntity.Current.Hud.Visibility);
        Assert.AreEqual(currentEntity.EntityKey, currentEntityReactive.Current.EntityKey);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ZoneRender, zoneRenderReactive.Current.Schema);
        Assert.AreEqual(currentEntity.EntityKey, currentEntityByType.EntityKey);
        Assert.AreEqual(currentEntity.EntityKey, currentEntityFromClientType.EntityKey);
        Assert.AreEqual(currentEntity.EntityKey, currentDocking.CurrentEntityKey);
        Assert.IsNotNull(observedDocking);
        Assert.AreEqual(currentEntity.EntityKey, observedDocking.CurrentEntityKey);
        Assert.AreEqual(currentEntity.EntityIndex, observedDocking.CurrentEntityIndex);
        Assert.AreEqual(currentDocking.CurrentEntityKey, observedDocking.Docking.CurrentEntityKey);
        Assert.AreEqual(stationRefit.DockParentEntityKey, observedDocking.Refit.DockParentEntityKey);
        Assert.IsFalse(observedDocking.TryResolveCurrentDockingBayRow(out _));
        Assert.AreEqual(currentEntity.EntityKey, currentDockingReactive.Current.CurrentEntityKey);
        Assert.AreEqual("", stationRefit.DockParentEntityKey);
        Assert.AreEqual("", stationRefitReactive.Current.DockParentEntityKey);
        Assert.AreEqual(-1, stationRefit.DockingBayIndex);
        Assert.IsTrue(client.State.CurrentEntity.Sources.Any(source =>
            source.SourceId == AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString()));
        Assert.IsTrue(client.State.StationRefit.Sources.Any(source =>
            source.SourceId == "catalog:aetheria.runtime"));
    }

    [Test]
    public void AetheriaClientSettingsDocumentsDoNotRequireDaemonFrame()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-client-settings-doc-tests",
            Path.GetRandomFileName(),
            "state.cc");

        using var client = AetheriaClient
            .OpenAsync(statePath, "settings-test", pullOnOpen: true)
            .GetAwaiter()
            .GetResult();

        var player = client.State.Latest<AetheriaRuntimePlayerSettingsDocument>();
        var verseHost = client.State.Latest<AetheriaRuntimeVerseHostSettingsDocument>();
        var catalog = client.State.Latest<AetheriaRuntimeCatalogSnapshot>();

        Assert.AreEqual(AetheriaRuntimeCatalogSnapshot.SchemaId, client.State.Catalog.Sources.Last().SchemaId);
        Assert.IsNotNull(catalog);
        Assert.AreEqual(AetheriaRuntimePlayerSettingsDocument.SchemaId, player.Schema);
        Assert.AreEqual(AetheriaRuntimeVerseHostSettingsDocument.SchemaId, verseHost.Schema);
        Assert.AreEqual("", player.PlayerName);
        Assert.AreEqual("", verseHost.VerseId);
        Assert.AreSame(client.State.PlayerSettings, client.State.Document<AetheriaRuntimePlayerSettingsDocument>());
        Assert.AreSame(client.State.VerseHostSettings, client.State.Document<AetheriaRuntimeVerseHostSettingsDocument>());
    }

    [Test]
    public void AetheriaRuntimeVerseClientExposesDomainStateDocuments()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-verse-domain-state-document-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            RunWithTwoEntities(),
            "daemon",
            "session-state",
            8,
            1.5,
            0.02);
        PublishLatestFrameThroughVerseClient(statePath, frame);

        using var verse = AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "tool-test", pullOnOpen: true)
            .GetAwaiter()
            .GetResult();

        var aetheria = verse.Aetheria();
        var currentEntity = aetheria.Latest<AetheriaRuntimeCurrentEntityDocument>();
        var latestFrameDocument = verse.Document<AetheriaRuntimeDaemonFrameDocument>(
            AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        using var latestFrameReactive = latestFrameDocument.Reactive();

        Assert.AreEqual("aetheria.current.entity", aetheria.Current.Entity.DocumentId);
        Assert.AreEqual("zone.0.entity.0", currentEntity.EntityKey);
        Assert.AreEqual("Player", currentEntity.Entity?.DisplayName);
        Assert.AreEqual(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(), latestFrameDocument.DocumentId);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Frame, latestFrameReactive.Current.Schema);
        Assert.AreEqual(8, latestFrameReactive.Current.FrameId);
        Assert.IsTrue(latestFrameDocument.CanSet);
        Assert.IsTrue(aetheria.Current.Entity.Sources.Any(source =>
            source.SourceId == AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString()));
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
        Assert.AreEqual(12.5, result.Run.Zones[0].SimulationTimeSeconds, 0.0001);
        var frame = result.Frame;
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
        Assert.AreEqual(12.5, frame.Run.Zones[0].SimulationTimeSeconds, 0.0001);
        var providerAdvertisement = result.ProviderAdvertisement;
        Assert.IsNotNull(providerAdvertisement);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ProviderAdvertisement, providerAdvertisement.Schema);
        Assert.AreEqual("aetheria.test", providerAdvertisement.VerseId);
        Assert.AreEqual("aetheria.daemon", providerAdvertisement.ProviderId);
        Assert.AreEqual("test-daemon", providerAdvertisement.DaemonId);
        Assert.AreEqual("cultmesh://aetheria.test/eve/providers/aetheria.daemon", providerAdvertisement.CultMeshAddress);
        Assert.AreEqual(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest.ToString(), providerAdvertisement.FrameRecordRef);
        Assert.AreEqual(AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest.ToString(), providerAdvertisement.SoaViewRecordRef);
        Assert.AreEqual(AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString(), providerAdvertisement.HealthRecordRef);
        Assert.AreEqual(
            AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString(),
            providerAdvertisement.CommandBoundaryRecordRef);
        Assert.AreEqual(
            AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString(),
            providerAdvertisement.EveGuiSurfaceRecordRef);
        Assert.AreEqual(
            AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface.ToString(),
            providerAdvertisement.EveTuiSurfaceRecordRef);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, providerAdvertisement.EveGuiSurfaceId);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, providerAdvertisement.EveTuiSurfaceId);
        Assert.AreEqual(
            AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface.ToString(),
            providerAdvertisement.EditorGuiSurfaceRecordRef);
        Assert.AreEqual(
            AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface.ToString(),
            providerAdvertisement.EditorTuiSurfaceRecordRef);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, providerAdvertisement.EditorGuiSurfaceId);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, providerAdvertisement.EditorTuiSurfaceId);
        CollectionAssert.Contains(providerAdvertisement.PublishedSchemas, AetheriaRuntimeDaemonSchemas.Command);
        CollectionAssert.Contains(providerAdvertisement.PublishedSchemas, AetheriaRuntimeDaemonSchemas.CommandBoundary);
        CollectionAssert.Contains(providerAdvertisement.PublishedSchemas, AetheriaRuntimeDaemonSchemas.GameSurface);
        CollectionAssert.Contains(providerAdvertisement.PublishedSchemas, AetheriaRuntimeDaemonSchemas.EditorSurface);
        var health = result.Health;
        Assert.IsNotNull(health);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Health, health.Schema);
        Assert.AreEqual("test-daemon", health.DaemonId);
        Assert.AreEqual("aetheria.test", health.VerseId);
        Assert.AreEqual(42, health.FrameId);
        Assert.AreEqual(2, health.ObservedCommandCount);
        Assert.AreEqual(2, health.AppliedCommandCount);
        Assert.AreEqual(0, health.RejectedCommandCount);
        Assert.AreEqual("daemon-published", health.PublicationSource);
        Assert.AreEqual("cultmesh-managed", health.Transport);
        Assert.AreEqual(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString(), health.CommandBoundaryPath);
        var commandBoundary = result.CommandBoundary;
        Assert.IsNotNull(commandBoundary);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.CommandBoundary, commandBoundary.Schema);
        Assert.AreEqual("aetheria.daemon.commands", commandBoundary.BoundaryId);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Command, commandBoundary.CommandSchema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.Frame, commandBoundary.ReceiptSchema);
        Assert.IsTrue(commandBoundary.Commands.Any(entry => entry.Kind == AetheriaRuntimeDaemonCommandKinds.SetMoveVector));
        Assert.IsTrue(commandBoundary.Commands.Any(entry =>
            entry.Kind == AetheriaRuntimeDaemonCommandKinds.TransferCargoItem &&
            entry.CommandBody == nameof(AetheriaRuntimeCargoTransferCommand)));
        var gameSurface = result.GameSurface;
        Assert.AreEqual("aetheria.daemon", gameSurface.ProviderId);
        Assert.AreEqual("game.daemon", gameSurface.ProviderKind);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, gameSurface.Surface.Id);
        Assert.AreEqual(42, gameSurface.Version);
        Assert.IsTrue(gameSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.SetMoveVector" &&
            command.Transport == "cultmesh"));
        Assert.IsTrue(ContainsSurfaceMetric(gameSurface.Surface.Root, "Name", "Player"));
        Assert.IsTrue(ContainsSurfaceMetric(gameSurface.Surface.Root, "Target", "Target"));
        Assert.IsTrue(ContainsSurfaceProp(
            gameSurface.Surface.Root,
            AetheriaRuntimeSurfaceStateRefs.Source,
            AetheriaRuntimeDaemonStateRefs.CurrentEntityName));
        Assert.IsTrue(ContainsSurfaceStateBinding(
            gameSurface.Surface.Root,
            "value",
            AetheriaRuntimeDaemonStateRefs.CurrentEntityName,
            AetheriaRuntimeDaemonSchemas.Frame));
        Assert.IsTrue(AetheriaRuntimeStateRefResolver.TryResolveDaemonStateRef(
            frame,
            health,
            commandBoundary,
            AetheriaRuntimeDaemonStateRefs.CurrentEntityName,
            out var resolvedEntityName));
        Assert.AreEqual("Player", resolvedEntityName);
        Assert.IsTrue(AetheriaRuntimeStateRefResolver.TryResolveDaemonStateRef(
            frame,
            health,
            commandBoundary,
            AetheriaRuntimeDaemonStateRefs.CurrentTargetName,
            out var resolvedTargetName));
        Assert.AreEqual("Target", resolvedTargetName);
        var gameTuiSurface = result.GameTuiSurface;
        Assert.AreEqual("aetheria.daemon", gameTuiSurface.ProviderId);
        Assert.AreEqual("game.daemon", gameTuiSurface.ProviderKind);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, gameTuiSurface.Surface.Id);
        Assert.AreEqual(42, gameTuiSurface.Version);
        var editorSurface = result.EditorSurface;
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
            command.Command == "aetheria.daemon.commands.SensorPing" &&
            command.Transport == "cultmesh"));
        Assert.IsFalse(editorSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.TransferCargoItem"));
        var editorTuiSurface = result.EditorTuiSurface;
        Assert.AreEqual("aetheria.daemon", editorTuiSurface.ProviderId);
        Assert.AreEqual("editor.daemon", editorTuiSurface.ProviderKind);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, editorTuiSurface.Surface.Id);
        Assert.AreEqual(42, editorTuiSurface.Version);
        PublishDaemonSurfacesThroughVerseClient(statePath, result);
        using var client = AetheriaClient
            .OpenAsync(statePath, "unity-surface-test", pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        var unityGameSurfaceState = client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Game);
        var unityGameTuiSurfaceState = client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.GameTui);
        var unityEditorSurfaceState = client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Editor);
        var unityEditorTuiSurfaceState = client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.EditorTui);
        var surfaceResolver = client.State.CreateEveSurfaceStateRefResolver();
        var unityGameSurface = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
            unityGameSurfaceState,
            surfaceResolver);
        var unityGameTuiSurface = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
            unityGameTuiSurfaceState,
            surfaceResolver);
        var unityEditorSurface = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
            unityEditorSurfaceState,
            surfaceResolver);
        var unityEditorTuiSurface = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
            unityEditorTuiSurfaceState,
            surfaceResolver);

        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, unityGameSurface.Surface.Id);
        Assert.AreEqual("aetheria.daemon", unityGameSurface.ProviderId);
        Assert.IsTrue(unityGameSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.SetMoveVector" &&
            command.Transport == "cultmesh"));
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, unityGameTuiSurface.Surface.Id);
        Assert.AreEqual("game.daemon", unityGameTuiSurface.ProviderKind);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.SurfaceId, unityEditorSurface.Surface.Id);
        Assert.AreEqual("editor.daemon", unityEditorSurface.ProviderKind);
        Assert.IsTrue(unityEditorSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.SensorPing" &&
            command.Transport == "cultmesh"));
        Assert.IsFalse(unityEditorSurface.Commands.Any(command =>
            command.Command == "aetheria.daemon.commands.TransferCargoItem"));
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, unityEditorTuiSurface.Surface.Id);
        Assert.AreEqual("editor.daemon", unityEditorTuiSurface.ProviderKind);
        var genericGameSurface = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
            client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.Game),
            surfaceResolver);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId, genericGameSurface.Surface.Id);
        Assert.AreEqual(unityGameSurface.ProviderId, genericGameSurface.ProviderId);
        Assert.IsTrue(ContainsEveSurfaceMetric(genericGameSurface.Surface.Root, "Target", "Target"));
        Assert.IsTrue(ContainsEveSurfaceProp(
            genericGameSurface.Surface.Root,
            AetheriaRuntimeSurfaceStateRefs.Source,
            AetheriaRuntimeDaemonStateRefs.CurrentTargetName));
        Assert.IsTrue(ContainsEveSurfaceProp(
            genericGameSurface.Surface.Root,
            AetheriaRuntimeSurfaceStateBindings.PropPrefix + "value" + AetheriaRuntimeSurfaceStateBindings.SourceIdSuffix,
            AetheriaRuntimeDaemonStateRefs.CurrentTargetName));
        Assert.IsTrue(ContainsEveSurfaceProp(
            genericGameSurface.Surface.Root,
            AetheriaRuntimeSurfaceStateBindings.PropPrefix + "value" + AetheriaRuntimeSurfaceStateBindings.SchemaIdSuffix,
            AetheriaRuntimeDaemonSchemas.Frame));
        Assert.IsTrue(ContainsEveSurfaceStateBinding(
            genericGameSurface.Surface.Root,
            "value",
            AetheriaRuntimeDaemonStateRefs.CurrentTargetName,
            AetheriaRuntimeDaemonSchemas.Frame));
        var genericGameTuiSurface = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
            client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.GameTui),
            surfaceResolver);
        Assert.AreEqual(AetheriaRuntimeDaemonGameSurfaceBuilder.TuiSurfaceId, genericGameTuiSurface.Surface.Id);
        var genericEditorTuiSurface = AetheriaRuntimeEveSurfaceAdapter.ToEveSurfaceDocument(
            client.State.Latest<global::Aetheria.State.Documents.EveSurfaceState>(AetheriaClientEveSurface.EditorTui),
            surfaceResolver);
        Assert.AreEqual(AetheriaRuntimeDaemonEditorSurfaceBuilder.TuiSurfaceId, genericEditorTuiSurface.Surface.Id);
    }

    [Test]
    public void EveSurfaceAdapterResolvesDaemonStateRefsBeforeLowering()
    {
        var surface = new EveSurfaceDocument(
            "surface-state",
            "gamecult.eve.surface.v1",
            "aetheria.daemon",
            "daemon",
            "Daemon",
            1,
            "",
            new EveSurfaceTree(
                "aetheria.test.surface",
                new EveSurfaceComponent(
                    "aetheria.test.metric",
                    "metric",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["label"] = "Target",
                        ["value"] = "stale",
                        [AetheriaRuntimeSurfaceStateRefs.Source] = AetheriaRuntimeDaemonStateRefs.CurrentTargetName
                    },
                    Array.Empty<EveSurfaceComponent>()),
                Array.Empty<EveStyleToken>()),
            Array.Empty<EveCommandTemplate>());

        var resolved = AetheriaRuntimeEveSurfaceAdapter.ResolveStateRefs(
            surface,
            stateRef => stateRef == AetheriaRuntimeDaemonStateRefs.CurrentTargetName ? "Live Target" : "");

        Assert.IsTrue(ContainsEveSurfaceMetric(resolved.Surface.Root, "Target", "Live Target"));
        Assert.IsTrue(ContainsEveSurfaceProp(
            resolved.Surface.Root,
            AetheriaRuntimeSurfaceStateRefs.Source,
            AetheriaRuntimeDaemonStateRefs.CurrentTargetName));
    }

    [Test]
    public void EveSurfaceAdapterResolvesTypedPointerPropsBeforeLowering()
    {
        var surface = new EveSurfaceDocument(
            "surface-state",
            "gamecult.eve.surface.v1",
            "aetheria.daemon",
            "daemon",
            "Daemon",
            1,
            "",
            new EveSurfaceTree(
                "aetheria.test.surface",
                new EveSurfaceComponent(
                    "aetheria.test.metric",
                    "metric",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["label"] = "Heat",
                        ["description"] = "stale",
                        ["descriptionRef"] = AetheriaRuntimeDaemonStateRefs.CurrentTargetName,
                        [AetheriaRuntimeSurfaceStateRefs.Value] = AetheriaRuntimeDaemonStateRefs.CurrentTargetName
                    },
                    Array.Empty<EveSurfaceComponent>()),
                Array.Empty<EveStyleToken>()),
            Array.Empty<EveCommandTemplate>());

        var resolved = AetheriaRuntimeEveSurfaceAdapter.ResolveStateRefs(
            surface,
            stateRef => stateRef == AetheriaRuntimeDaemonStateRefs.CurrentTargetName ? "Live Target" : "");

        Assert.IsTrue(ContainsEveSurfaceProp(resolved.Surface.Root, "description", "Live Target"));
        Assert.IsTrue(ContainsEveSurfaceProp(resolved.Surface.Root, "descriptionRef", AetheriaRuntimeDaemonStateRefs.CurrentTargetName));
        Assert.IsTrue(ContainsEveSurfaceProp(resolved.Surface.Root, "value", "Live Target"));
        Assert.IsTrue(ContainsEveSurfaceProp(resolved.Surface.Root, AetheriaRuntimeSurfaceStateRefs.Value, AetheriaRuntimeDaemonStateRefs.CurrentTargetName));
    }

    [Test]
    public void RuntimeStateReaderResolvesItemStatRefsFromDaemonFrame()
    {
        var frame = new AetheriaRuntimeDaemonFrameDocument
        {
            Run = new AetheriaRuntimeRunCheckpointCommit
            {
                Zones = new[]
                {
                    new AetheriaRuntimeZoneSnapshotCommit
                    {
                        Entities = new[]
                        {
                            new AetheriaRuntimeEntitySnapshotCommit
                            {
                                Equipment = new[]
                                {
                                    new AetheriaRuntimeLoadoutItemSlotCommit
                                    {
                                        Item = new AetheriaRuntimeLoadoutItemCommit
                                        {
                                            ItemKey = "test-laser",
                                            Quality = 0.5,
                                            Durability = 1.0,
                                            Enabled = true,
                                            Temperature = 0.5
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        var statValue = new AetheriaRuntimeBehaviorValue(
            "performance-stat",
            "",
            0,
            false,
            "",
            "",
            new[]
            {
                NumberValue(10),
                NumberValue(20),
                NumberValue(1),
                NumberValue(0),
                NumberValue(1),
                EmptyBehaviorValue("stat-recipe")
            },
            Array.Empty<AetheriaRuntimeBehaviorMapEntry>());
        var catalog = new AetheriaRuntimeCatalogSnapshot(
            new[]
            {
                CatalogItem(
                    "test-laser",
                    new[]
                    {
                        new AetheriaRuntimeBehaviorPayload(
                            0,
                            "Weapon",
                            2,
                            new[] { new AetheriaRuntimeBehaviorField(7, statValue) })
                    })
            },
            Array.Empty<AetheriaRuntimeCorporation>(),
            Array.Empty<AetheriaRuntimeNameFile>());

        Assert.IsTrue(AetheriaRuntimeStateRefResolver.TryResolveDaemonItemStatRef(
            frame,
            catalog,
            AetheriaRuntimeDaemonItemStatQueries.ItemStatRef("test-laser", "Weapon", 2, 7),
            out var value));
        Assert.AreEqual("12.5", value);
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
        Assert.AreEqual(1, result.Health.ObservedCommandCount);
        Assert.AreEqual(0, result.Health.AppliedCommandCount);
        Assert.AreEqual(0, result.Health.RejectedCommandCount);
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
        var position = new AetheriaRuntimeDaemonSoaColumnDocument
        {
            ColumnId = "position",
            Kind = AetheriaRuntimeDaemonSoaColumnKinds.Position,
            BufferId = buffer.BufferId,
            ScalarType = "float3",
            ByteOffset = 0,
            ElementStride = 12,
            ElementCount = 128,
            Unit = "world_units",
            CoordinateSpace = "zone"
        };
        var dirtyRange = new AetheriaRuntimeDaemonSoaDirtyRangeDocument
        {
            ColumnId = position.ColumnId,
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
            new[] { position },
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
        Assert.AreEqual(AetheriaRuntimeDaemonSoaColumnKinds.Position, view.Columns[0].Kind);
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
                    ColumnId = "position",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.Position,
                    BufferId = "transform-hot",
                    ScalarType = "float3",
                    ByteOffset = 16,
                    ElementStride = 12,
                    ElementCount = 64,
                    Unit = "world_units",
                    CoordinateSpace = "zone"
                }
            },
            new[]
            {
                new AetheriaRuntimeDaemonSoaDirtyRangeDocument
                {
                    ColumnId = "position",
                    StartIndex = 8,
                    Count = 4,
                    Generation = 91
                }
            },
            AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile);

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);

        Assert.IsTrue(index.IsValid);
        Assert.IsTrue(index.TryGetFirstColumnOfKind(AetheriaRuntimeDaemonSoaColumnKinds.Position, out var binding));
        Assert.AreEqual("position", binding.Column.ColumnId);
        Assert.AreEqual("transform-hot", binding.Buffer.BufferId);
        Assert.AreEqual(144, binding.AbsoluteByteOffset);
        Assert.AreEqual(768, binding.ByteLength);
        Assert.IsTrue(binding.DirectMemoryCompatible);
        Assert.AreEqual(1, index.GetDirtyRanges("position").Count);
        Assert.AreEqual(8, index.GetDirtyRanges("position")[0].StartIndex);
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
    public void DaemonRenderQueriesFilterRenderGroupsByBounds()
    {
        var view = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "aetheria-daemon",
            "session-render-query",
            130,
            131,
            new[]
            {
                new AetheriaRuntimeDaemonSoaBufferDocument
                {
                    BufferId = "render-hot",
                    ByteLength = 128
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
                    ElementStride = 4,
                    ElementCount = 16
                }
            },
            renderGroups: new[]
            {
                new AetheriaRuntimeDaemonRenderGroupDocument
                {
                    GroupId = 1,
                    MeshKey = "ships/djinni",
                    MaterialKey = "materials/ships/hull",
                    BoundsCenterX = 0,
                    BoundsCenterY = 0,
                    BoundsCenterZ = 0,
                    BoundsSizeX = 8,
                    BoundsSizeY = 8,
                    BoundsSizeZ = 8
                },
                new AetheriaRuntimeDaemonRenderGroupDocument
                {
                    GroupId = 2,
                    MeshKey = "ships/far",
                    MaterialKey = "materials/ships/hull",
                    BoundsCenterX = 100,
                    BoundsCenterY = 0,
                    BoundsCenterZ = 0,
                    BoundsSizeX = 8,
                    BoundsSizeY = 8,
                    BoundsSizeZ = 8
                }
            });

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);
        var groups = new List<AetheriaRuntimeDaemonRenderGroupDocument>();
        var count = AetheriaRuntimeDaemonRenderQueries.QueryRenderGroups(
            index,
            -10,
            -10,
            -10,
            10,
            10,
            10,
            groups);

        Assert.AreEqual(1, count);
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(1, groups[0].GroupId);
    }

    [Test]
    public void DaemonRenderQueriesFilterGravityInfluencesByXzViewport()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            GravityTerrainRadius = 100,
            GravityTerrainDepth = 2,
            GravityTerrainDepthExponent = 1,
            GravityTerrainWaveFrequency = 1,
            Bodies = new[]
            {
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:near",
                    OrbitKey = "orbit:near",
                    Kind = "gas_giant",
                    GravityInfluenceCenterX = 4,
                    GravityInfluenceCenterZ = 0,
                    GravityInfluenceRadius = 6,
                    GravityWellDepth = 20,
                    GravityDepthExponent = 12,
                    GravityWaveRadius = 9,
                    GravityWaveDepth = 3,
                    GravityWaveSpeed = 2
                },
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:far",
                    OrbitKey = "orbit:far",
                    Kind = "planet",
                    GravityInfluenceCenterX = 100,
                    GravityInfluenceCenterZ = 0,
                    GravityInfluenceRadius = 4,
                    GravityWellDepth = 10
                },
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:legacy-missing-radius",
                    OrbitKey = "orbit:legacy",
                    Kind = "planet",
                    GravityInfluenceCenterX = 0,
                    GravityInfluenceCenterZ = 0
                }
            }
        };

        var brushes = new List<AetheriaRuntimeGravityInfluenceBrush>();
        var count = AetheriaRuntimeDaemonRenderQueries.QueryGravityInfluences(
            zone,
            new rect(-10, -10, 10, 10),
            brushes);

        Assert.AreEqual(1, count);
        Assert.AreEqual("body:near", brushes[0].BodyKey);
        Assert.AreEqual(AetheriaRuntimeGravityInfluenceKind.GasGiant, brushes[0].Kind);
        Assert.AreEqual(9, brushes[0].Radius);
        Assert.AreEqual(20, brushes[0].GravityDepth);
        Assert.AreEqual(12, brushes[0].GravityDepthExponent);
        Assert.AreEqual(9, brushes[0].WaveRadius);
        Assert.AreEqual(3, brushes[0].WaveDepth);
        Assert.AreEqual(2, brushes[0].WaveSpeed);

        var height = AetheriaRuntimeDaemonRenderQueries.EvaluateGravityTerrainHeight(zone, 4, 0, 0);
        Assert.AreEqual(-24.9968, height, 0.0001);
    }

    [Test]
    public void DaemonRenderQueriesResolveZoneRenderRadiusFromDaemonTerrain()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit { GravityTerrainRadius = 128 };

        Assert.AreEqual(128, AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(zone, 2000));
        Assert.AreEqual(2000, AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(null, 2000));
        Assert.AreEqual(0, AetheriaRuntimeDaemonRenderQueries.ResolveZoneRenderRadius(null, -5));
    }

    [Test]
    public void DaemonRenderQueriesPublishGravityTerrainMaterialBand()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            GravityTerrainDepth = 20,
            GravityTerrainDepthExponent = 2
        };

        var band = AetheriaRuntimeDaemonRenderQueries.QueryGravityTerrainBand(zone, 0.25, 3);

        Assert.AreEqual(11.25, band.StartDepth, 0.0001);
        Assert.AreEqual(11.75, band.DepthRange, 0.0001);
    }

    [Test]
    public void ZoneDetailsProjectionReadsMassRadiusAndContentsFromDaemonZone()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Name = "Tir Na Nog",
            GravityTerrainRadius = 420,
            Bodies = new[]
            {
                new AetheriaRuntimeBodySnapshotCommit { Kind = "planet", Mass = 10 },
                new AetheriaRuntimeBodySnapshotCommit { Kind = "gas_giant", Mass = 30 },
                new AetheriaRuntimeBodySnapshotCommit { Kind = "asteroid_belt" }
            },
            Entities = new[]
            {
                new AetheriaRuntimeEntitySnapshotCommit { HullItemKey = "station-hull" },
                new AetheriaRuntimeEntitySnapshotCommit { HullItemKey = "ship-hull" }
            }
        };

        var projection = AetheriaRuntimeZoneDetailsSurfaceBuilder.ProjectDaemonZone(
            zone,
            key => key == "station-hull" ? "Station" : "Ship");
        var state = AetheriaRuntimeZoneDetailsSurfaceBuilder.Project(
            zone.Name,
            "GameCult",
            projection.Mass.ToString("0"),
            projection.Radius.ToString("0"),
            new[] { "A", "B" },
            projection.Bodies,
            projection.Entities,
            projection.HasContents,
            "");

        Assert.AreEqual(40, projection.Mass, 0.0001);
        Assert.AreEqual(420, projection.Radius, 0.0001);
        Assert.IsTrue(projection.HasContents);
        Assert.AreEqual("1", state.Planets);
        Assert.AreEqual("1", state.GasGiants);
        Assert.AreEqual("1", state.AsteroidBelts);
        Assert.AreEqual("1", state.Stations);
        Assert.AreEqual("1", state.Ships);
    }

    [Test]
    public void DaemonRenderQueriesPublishBodyPosesFromZoneSnapshot()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Orbits = new[]
            {
                new AetheriaRuntimeOrbitSnapshotCommit
                {
                    OrbitKey = "orbit:parent",
                    FixedPositionX = 10,
                    FixedPositionY = 20
                },
                new AetheriaRuntimeOrbitSnapshotCommit
                {
                    OrbitKey = "orbit:child",
                    ParentOrbitKey = "orbit:parent",
                    Distance = 5,
                    Phase = 0.25
                }
            },
            Bodies = new[]
            {
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:child",
                    OrbitKey = "orbit:child",
                    Kind = "planet"
                },
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:explicit",
                    OrbitKey = "orbit:missing",
                    Kind = "gas_giant",
                    GravityInfluenceCenterX = -3,
                    GravityInfluenceCenterZ = 7,
                    GravityWaveSpeed = 3.5
                }
            }
        };

        var poses = AetheriaRuntimeDaemonRenderQueries.QueryBodyPoses(zone);

        Assert.AreEqual(2, poses.Length);
        Assert.AreEqual("body:child", poses[0].BodyKey);
        Assert.AreEqual("orbit:parent", poses[0].ParentOrbitKey);
        Assert.AreEqual(10, poses[0].CenterX, 0.0001);
        Assert.AreEqual(25, poses[0].CenterZ, 0.0001);
        Assert.AreEqual(10, poses[0].ParentCenterX, 0.0001);
        Assert.AreEqual(20, poses[0].ParentCenterZ, 0.0001);
        Assert.AreEqual("body:explicit", poses[1].BodyKey);
        Assert.AreEqual(-3, poses[1].CenterX, 0.0001);
        Assert.AreEqual(7, poses[1].CenterZ, 0.0001);
        Assert.AreEqual(3.5, poses[1].GravityWaveSpeed, 0.0001);
    }

    [Test]
    public void DaemonRenderQueriesPublishBodyViewsFromZoneSnapshot()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Orbits = new[]
            {
                new AetheriaRuntimeOrbitSnapshotCommit
                {
                    OrbitKey = "orbit:planet",
                    FixedPositionX = 12,
                    FixedPositionY = -8
                },
                new AetheriaRuntimeOrbitSnapshotCommit
                {
                    OrbitKey = "orbit:belt",
                    Distance = 30
                }
            },
            Bodies = new[]
            {
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:planet",
                    OrbitKey = "orbit:planet",
                    Kind = "planet",
                    GravityInfluenceRadius = 40,
                    GravityWellDepth = 7
                },
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:belt",
                    OrbitKey = "orbit:belt",
                    Kind = "asteroid_belt"
                }
            }
        };

        var views = new List<AetheriaRuntimeDaemonBodyView>();
        var count = AetheriaRuntimeDaemonRenderQueries.QueryBodyViews(zone, views);

        Assert.AreEqual(2, count);
        Assert.AreEqual("body:planet", views[0].Body.BodyKey);
        Assert.IsFalse(views[0].IsAsteroidBelt);
        Assert.AreEqual(12, views[0].Pose.CenterX, 0.0001);
        Assert.AreEqual(-8, views[0].Pose.CenterZ, 0.0001);
        Assert.AreEqual(40, views[0].Body.GravityInfluenceRadius, 0.0001);
        Assert.AreEqual(7, views[0].Body.GravityWellDepth, 0.0001);
        Assert.AreEqual("body:belt", views[1].Body.BodyKey);
        Assert.IsTrue(views[1].IsAsteroidBelt);

        count = AetheriaRuntimeDaemonRenderQueries.QueryBodyViews(
            zone,
            new rect(0, -20, 30, 10),
            views);

        Assert.AreEqual(1, count);
        Assert.AreEqual("body:planet", views[0].Body.BodyKey);
    }

    [Test]
    public void DaemonRenderQueriesPublishAsteroidBeltPosesFromZoneSnapshot()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Orbits = new[]
            {
                new AetheriaRuntimeOrbitSnapshotCommit
                {
                    OrbitKey = "orbit:parent",
                    FixedPositionX = 11,
                    FixedPositionY = -4
                },
                new AetheriaRuntimeOrbitSnapshotCommit
                {
                    OrbitKey = "orbit:belt",
                    ParentOrbitKey = "orbit:parent",
                    Distance = 30
                }
            },
            Bodies = new[]
            {
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:belt",
                    OrbitKey = "orbit:belt",
                    Kind = "asteroid_belt",
                    Asteroids = new[]
                    {
                        new AetheriaRuntimeAsteroidCommit { Distance = 3 },
                        new AetheriaRuntimeAsteroidCommit { Distance = 7 },
                        new AetheriaRuntimeAsteroidCommit { Distance = 5 }
                    }
                },
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:planet",
                    OrbitKey = "orbit:belt",
                    Kind = "planet"
                }
            }
        };

        var poses = AetheriaRuntimeDaemonRenderQueries.QueryAsteroidBeltPoses(zone);

        Assert.AreEqual(1, poses.Length);
        Assert.AreEqual("body:belt", poses[0].BodyKey);
        Assert.AreEqual("orbit:belt", poses[0].OrbitKey);
        Assert.AreEqual(11, poses[0].CenterX, 0.0001);
        Assert.AreEqual(-4, poses[0].CenterZ, 0.0001);
        Assert.AreEqual(7, poses[0].Radius, 0.0001);
        Assert.AreEqual(3, poses[0].AsteroidCount);
    }

    [Test]
    public void DaemonRenderQueriesPublishAsteroidInstancePosesForVisibleBelt()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Orbits = new[]
            {
                new AetheriaRuntimeOrbitSnapshotCommit
                {
                    OrbitKey = "orbit:parent",
                    FixedPositionX = 10,
                    FixedPositionY = 20
                },
                new AetheriaRuntimeOrbitSnapshotCommit
                {
                    OrbitKey = "orbit:belt",
                    ParentOrbitKey = "orbit:parent",
                    Distance = 100
                }
            },
            Bodies = new[]
            {
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:belt",
                    OrbitKey = "orbit:belt",
                    Kind = "asteroid_belt",
                    Asteroids = new[]
                    {
                        new AetheriaRuntimeAsteroidCommit
                        {
                            Distance = 5,
                            Phase = 0,
                            Size = 2,
                            RotationSpeed = 3
                        },
                        new AetheriaRuntimeAsteroidCommit
                        {
                            Distance = 7,
                            Phase = 0.25,
                            Size = 4,
                            Damage = 1,
                            RotationSpeed = 2
                        },
                        new AetheriaRuntimeAsteroidCommit
                        {
                            Distance = 11,
                            Phase = 0.5,
                            Size = 9,
                            RespawnTimer = 12
                        }
                    }
                },
                new AetheriaRuntimeBodySnapshotCommit
                {
                    BodyKey = "body:other",
                    OrbitKey = "orbit:belt",
                    Kind = "asteroid_belt",
                    Asteroids = new[] { new AetheriaRuntimeAsteroidCommit { Distance = 99, Size = 99 } }
                }
            }
        };

        var poses = AetheriaRuntimeDaemonRenderQueries.QueryAsteroidInstancePoses(zone, "body:belt", 4);

        Assert.AreEqual(3, poses.Length);
        Assert.AreEqual("body:belt", poses[0].BodyKey);
        Assert.AreEqual(0, poses[0].AsteroidIndex);
        Assert.AreEqual(15, poses[0].PositionX, 0.0001);
        Assert.AreEqual(20, poses[0].PositionZ, 0.0001);
        Assert.AreEqual(12, poses[0].Rotation, 0.0001);
        Assert.AreEqual(2, poses[0].Size, 0.0001);
        Assert.AreEqual(10, poses[1].PositionX, 0.0001);
        Assert.AreEqual(27, poses[1].PositionZ, 0.0001);
        Assert.AreEqual(3, poses[1].Size, 0.0001);
        Assert.AreEqual(0, poses[2].Size, 0.0001);
    }

    [Test]
    public void DaemonRenderQueriesPublishCompassMarkersFromVisibleContacts()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Entities = new[]
            {
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 0,
                    PositionX = 10,
                    PositionZ = 20,
                    Contacts = new[]
                    {
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 1,
                            InfoGathered = 0.8,
                            Visible = true,
                            Hostile = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 2,
                            InfoGathered = 0.2,
                            Visible = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 3,
                            InfoGathered = 0.9,
                            Visible = false
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 4,
                            InfoGathered = 0.9,
                            Visible = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 99,
                            InfoGathered = 0.9,
                            Visible = true
                        }
                    }
                },
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 1,
                    PositionX = 13,
                    PositionZ = 24
                },
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 2,
                    PositionX = 100,
                    PositionZ = 20
                },
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 3,
                    PositionX = 10,
                    PositionZ = 200
                },
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 4,
                    PositionX = 11,
                    PositionZ = 20
                }
            }
        };

        var markers = new List<AetheriaRuntimeDaemonCompassMarker>();
        var count = AetheriaRuntimeDaemonRenderQueries.QueryCompassMarkers(
            zone,
            0,
            0.5,
            2,
            markers);

        Assert.AreEqual(1, count);
        Assert.AreEqual(1, markers.Count);
        Assert.AreEqual(1, markers[0].TargetEntityIndex);
        Assert.AreEqual(13, markers[0].PositionX, 0.0001);
        Assert.AreEqual(24, markers[0].PositionZ, 0.0001);
        Assert.AreEqual(3, markers[0].DeltaX, 0.0001);
        Assert.AreEqual(4, markers[0].DeltaZ, 0.0001);
        Assert.AreEqual(5, markers[0].Distance, 0.0001);
        Assert.AreEqual(0.8, markers[0].InfoGathered, 0.0001);
        Assert.IsTrue(markers[0].Hostile);
    }

    [Test]
    public void DaemonRenderQueriesPublishVisibleEntityIndicesFromObserverContacts()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Entities = new[]
            {
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 10,
                    Contacts = new[]
                    {
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 11,
                            InfoGathered = 0.7,
                            Visible = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 12,
                            InfoGathered = 0.3,
                            Visible = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 13,
                            InfoGathered = 0.9,
                            Visible = false
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 99,
                            InfoGathered = 0.9,
                            Visible = true
                        }
                    }
                },
                new AetheriaRuntimeEntitySnapshotCommit { EntityIndex = 11 },
                new AetheriaRuntimeEntitySnapshotCommit { EntityIndex = 12 },
                new AetheriaRuntimeEntitySnapshotCommit { EntityIndex = 13 }
            }
        };

        var visible = new List<int>();
        var count = AetheriaRuntimeDaemonRenderQueries.QueryVisibleEntityIndices(
            zone,
            10,
            0.5,
            visible);

        Assert.AreEqual(2, count);
        Assert.AreEqual(10, visible[0]);
        Assert.AreEqual(11, visible[1]);
    }

    [Test]
    public void DaemonRenderQueriesPublishCollectiveVisibleEntitiesInsideObjectsViewport()
    {
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            Entities = new[]
            {
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 1,
                    PositionX = 0,
                    PositionZ = 0,
                    Contacts = new[]
                    {
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 3,
                            InfoGathered = 0.8,
                            Visible = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 4,
                            InfoGathered = 0.4,
                            Visible = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 6,
                            InfoGathered = 0.9,
                            Visible = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 7,
                            InfoGathered = 0.9,
                            Visible = true
                        }
                    }
                },
                new AetheriaRuntimeEntitySnapshotCommit
                {
                    EntityIndex = 2,
                    PositionX = 30,
                    PositionZ = 0,
                    Contacts = new[]
                    {
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 5,
                            InfoGathered = 0.9,
                            Visible = true
                        },
                        new AetheriaRuntimeEntityContactCommit
                        {
                            TargetEntityIndex = 7,
                            InfoGathered = 0.9,
                            Visible = true
                        }
                    }
                },
                new AetheriaRuntimeEntitySnapshotCommit { EntityIndex = 3, PositionX = 8, PositionZ = 8 },
                new AetheriaRuntimeEntitySnapshotCommit { EntityIndex = 4, PositionX = 9, PositionZ = 9 },
                new AetheriaRuntimeEntitySnapshotCommit { EntityIndex = 5, PositionX = 12, PositionZ = 0 },
                new AetheriaRuntimeEntitySnapshotCommit { EntityIndex = 6, PositionX = 100, PositionZ = 100 },
                new AetheriaRuntimeEntitySnapshotCommit { EntityIndex = 7, PositionX = 5, PositionZ = 5 }
            }
        };

        var visible = new List<int>();
        var count = AetheriaRuntimeDaemonRenderQueries.QueryObjectsViewportEntityIndices(
            zone,
            new[] { 1, 2 },
            0.5,
            new rect(-10, -10, 40, 20),
            visible);

        Assert.AreEqual(5, count);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 5, 7 }, visible);

        var objects = AetheriaRuntimeDaemonRenderQueries.QueryObjectsViewport(
            zone,
            new[] { 1, 2 },
            0.5,
            new rect(-10, -10, 40, 20));

        Assert.AreEqual(5, objects.Length);
        Assert.AreEqual(1, objects[0].EntityIndex);
        Assert.IsTrue(objects[0].Controlled);
        Assert.AreEqual(new double3(0, 0, 0), objects[0].Position);
        Assert.AreEqual(new double2(0, 0), objects[0].Xy);
        Assert.AreEqual(7, objects[4].EntityIndex);
        Assert.IsFalse(objects[4].Controlled);
        Assert.AreEqual(new double2(5, 5), objects[4].Xy);
    }

    [Test]
    public void DaemonRenderQueriesPublishWormholeExitsFromZoneAdjacency()
    {
        var currentZone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            PositionX = 10,
            PositionY = 20,
            AdjacentZoneIndices = new[] { 1, 99 }
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            Zones = new[]
            {
                currentZone,
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 1,
                    PositionX = 13,
                    PositionY = 24
                }
            }
        };

        var exits = new List<AetheriaRuntimeDaemonWormholeExit>();
        var count = AetheriaRuntimeDaemonRenderQueries.QueryWormholeExits(
            run,
            currentZone,
            100,
            0.5,
            exits);

        Assert.AreEqual(1, count);
        Assert.AreEqual(1, exits[0].TargetZoneIndex);
        Assert.AreEqual(0.6, exits[0].DirectionX, 0.0001);
        Assert.AreEqual(0.8, exits[0].DirectionZ, 0.0001);
        Assert.AreEqual(30, exits[0].PositionX, 0.0001);
        Assert.AreEqual(40, exits[0].PositionZ, 0.0001);
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
    public void ManagedLatestSoaViewPublishesLatestDaemonView()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-managed-soa-view-tests",
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

        PublishLatestSoaViewThroughVerseClient(statePath, view);

        using var client = AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "daemon-soa-store-read-test", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        var published = client
            .MutableDocument<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest)
            .ReadAsync()
            .GetAwaiter()
            .GetResult();

        Assert.IsNotNull(published);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.SoaView, view.Schema);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.SoaView, published.Schema);
        Assert.AreEqual("aetheria-daemon", published.DaemonId);
        Assert.AreEqual("session-soa-store", published.SessionId);
        Assert.AreEqual(123, published.FrameId);
        Assert.AreEqual(456, published.Generation);
        Assert.IsTrue(published.IsAuthoritative);
        Assert.AreEqual(1, published.Buffers.Count);
        Assert.AreEqual("entity-hot-0", published.Buffers[0].BufferId);
        Assert.IsFalse(published.Buffers[0].ObserverWritable);
        Assert.AreEqual(1, published.Columns.Count);
        Assert.AreEqual(AetheriaRuntimeDaemonSoaColumnKinds.Heat, published.Columns[0].Kind);
        Assert.AreEqual(1, published.DirtyRanges.Count);
        Assert.AreEqual(256, published.DirtyRanges[0].Count);
    }

    [Test]
    public void SoaFramePublisherPublishesMappableCurrentZoneEntitySlab()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-soa-frame-publisher-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = "run-soa-frame",
                CurrentZoneIndex = 2,
                CurrentEntityKey = "entity:player",
                Zones = new[]
                {
                    new AetheriaRuntimeZoneSnapshotCommit
                    {
                        ZoneIndex = 1,
                        Entities = new[]
                        {
                            new AetheriaRuntimeEntitySnapshotCommit
                            {
                                EntityIndex = 99,
                                PositionX = 999
                            }
                        }
                    },
                    new AetheriaRuntimeZoneSnapshotCommit
                    {
                        ZoneIndex = 2,
                        Entities = new[]
                        {
                            new AetheriaRuntimeEntitySnapshotCommit
                            {
                                EntityIndex = 7,
                                PositionX = 12,
                                PositionY = 3,
                                PositionZ = 34,
                                DirectionX = 1,
                                DirectionY = 0,
                                VelocityX = 5,
                                VelocityY = 6,
                                IsActive = true
                            },
                            new AetheriaRuntimeEntitySnapshotCommit
                            {
                                EntityIndex = 3,
                                PositionX = -2,
                                PositionY = 4,
                                PositionZ = -8,
                                IsActive = false
                            }
                        }
                    }
                }
            },
            "aetheria-daemon",
            "session-soa-frame",
            44,
            1.0,
            0.02);

        var view = AetheriaRuntimeDaemonSoaFramePublisher.BuildCurrentZoneEntities(statePath, frame);
        Assert.AreEqual(AetheriaRuntimeDaemonSoaBackends.MemoryMappedFile, view.Backend);
        Assert.AreEqual(1, view.Buffers.Count);
        Assert.IsFalse(view.Buffers[0].ObserverWritable);
        Assert.AreEqual(11, view.Columns.Count);
        Assert.AreEqual(1, view.RenderGroups.Count);
        Assert.AreEqual(2, view.RenderGroups[0].InstanceCount);

        var index = AetheriaRuntimeDaemonSoaViewIndex.Build(view);
        Assert.IsTrue(index.IsValid, string.Join("\n", index.ValidationErrors));
        Assert.IsTrue(index.TryGetFirstColumnOfKind(AetheriaRuntimeDaemonSoaColumnKinds.EntityIndex, out var entityIndex));
        Assert.IsTrue(index.TryGetFirstColumnOfKind(AetheriaRuntimeDaemonSoaColumnKinds.Position, out var position));
        Assert.IsTrue(index.TryGetFirstColumnOfKind(AetheriaRuntimeDaemonSoaColumnKinds.Velocity, out var velocity));
        Assert.IsTrue(index.TryGetFirstColumnOfKind(AetheriaRuntimeDaemonSoaColumnKinds.RenderVisibility, out var visibility));

        using var memory = MemoryMappedFile.OpenExisting(view.Buffers[0].Location, MemoryMappedFileRights.Read);
        using var accessor = memory.CreateViewAccessor(0, view.Buffers[0].ByteLength, MemoryMappedFileAccess.Read);
        Assert.AreEqual(3, accessor.ReadInt32(entityIndex.AbsoluteByteOffset));
        Assert.AreEqual(7, accessor.ReadInt32(entityIndex.AbsoluteByteOffset + entityIndex.Column.ElementStride));
        Assert.AreEqual(-2f, accessor.ReadSingle(position.AbsoluteByteOffset), 0.0001f);
        Assert.AreEqual(-8f, accessor.ReadSingle(position.AbsoluteByteOffset + 8), 0.0001f);
        Assert.AreEqual(12f, accessor.ReadSingle(position.AbsoluteByteOffset + position.Column.ElementStride), 0.0001f);
        Assert.AreEqual(34f, accessor.ReadSingle(position.AbsoluteByteOffset + position.Column.ElementStride + 8), 0.0001f);
        Assert.AreEqual(6f, accessor.ReadSingle(velocity.AbsoluteByteOffset + velocity.Column.ElementStride + 8), 0.0001f);
        Assert.AreEqual(0, accessor.ReadByte(visibility.AbsoluteByteOffset));
        Assert.AreEqual(1, accessor.ReadByte(visibility.AbsoluteByteOffset + visibility.Column.ElementStride));
    }

    [Test]
    public void ObservedDaemonStateReadsManagedFrameAndSoaDocuments()
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
                    ColumnId = "position",
                    Kind = AetheriaRuntimeDaemonSoaColumnKinds.Position,
                    BufferId = "entity-hot",
                    ScalarType = "float3",
                    ElementStride = 12,
                    ElementCount = 64
                }
            });

        PublishLatestFrameThroughVerseClient(statePath, frame);
        PublishLatestSoaViewThroughVerseClient(statePath, soaView);

        using var client = AetheriaClient
            .OpenAsync(statePath, "unity-observer-test", pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        using var observedFrame = client.State.Reactive<AetheriaRuntimeDaemonFrameDocument>();
        using var observedSoaView = client.State.Reactive<AetheriaRuntimeDaemonSoaViewDocument>();
        using var observedZoneRender = client.State.Reactive<AetheriaRuntimeZoneRenderDocument>();
        Assert.IsTrue(AetheriaRuntimeObservedDaemonState.TryCreateCurrent(
            observedFrame,
            observedSoaView,
            observedZoneRender,
            out var observed));

        Assert.IsNotNull(observed);
        Assert.IsTrue(observed.IsAuthoritative);
        Assert.IsTrue(observed.HasSoaView);
        Assert.AreEqual("daemon-run", observed.Run.RunId);
        Assert.AreEqual("entity:observer-target", observed.Run.CurrentEntityKey);
        Assert.AreEqual(300, observed.Frame.FrameId);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ZoneRender, observed.ZoneRender.Schema);
        Assert.AreEqual(300, observed.ZoneRender.FrameId);
        Assert.AreEqual("daemon-run", observed.ZoneRender.RunId);
        Assert.AreEqual("entity:observer-target", observed.ZoneRender.CurrentEntityKey);
        Assert.AreEqual(301, observed.SoaView.Generation);
        Assert.IsFalse(observed.SoaView.Buffers[0].ObserverWritable);
    }

    [Test]
    public void ManagedStateSamplesCurrentObservedDaemonFromReactiveDocuments()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-reactive-observed-daemon-state-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = "reactive-daemon-run",
                CurrentZoneIndex = 3,
                CurrentEntityKey = "entity:reactive-observer-target"
            },
            "aetheria-daemon",
            "session-reactive-observed",
            330,
            13.0,
            0.02);
        var soaView = AetheriaRuntimeDaemonSoaViewDocument.Create(
            "aetheria-daemon",
            "session-reactive-observed",
            330,
            331,
            Array.Empty<AetheriaRuntimeDaemonSoaBufferDocument>(),
            Array.Empty<AetheriaRuntimeDaemonSoaColumnDocument>());

        PublishLatestFrameThroughVerseClient(statePath, frame);
        PublishLatestSoaViewThroughVerseClient(statePath, soaView);

        using var client = AetheriaClient
            .OpenAsync(statePath, "unity-reactive-observer-test", pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        using var observedFrame = client.State.Reactive<AetheriaRuntimeDaemonFrameDocument>();
        using var observedSoaView = client.State.Reactive<AetheriaRuntimeDaemonSoaViewDocument>();
        using var observedZoneRender = client.State.Reactive<AetheriaRuntimeZoneRenderDocument>();
        var observed = AetheriaRuntimeObservedDaemonState.TryCreateCurrent(
            observedFrame,
            observedSoaView,
            observedZoneRender,
            out var currentObserved)
            ? currentObserved
            : null;
        Assert.IsNotNull(observed);
        Assert.IsTrue(observed.IsAuthoritative);
        Assert.AreEqual("reactive-daemon-run", observed.Run.RunId);
        Assert.AreEqual("entity:reactive-observer-target", observed.Run.CurrentEntityKey);
        Assert.AreEqual(330, observed.Frame.FrameId);
        Assert.AreEqual(331, observed.SoaView.Generation);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ZoneRender, observed.ZoneRender.Schema);
        Assert.AreEqual(330, observed.ZoneRender.FrameId);
    }

    [Test]
    public void ObservedDaemonStateReadsManagedFrameWithoutSoaDocument()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-observed-daemon-frame-only-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var frame = AetheriaRuntimeDaemonFrameDocument.Create(
            new AetheriaRuntimeRunCheckpointCommit
            {
                RunId = "daemon-frame-only-run",
                CurrentZoneIndex = 1,
                CurrentEntityKey = "entity:frame-only-target"
            },
            "aetheria-daemon",
            "session-frame-only",
            301,
            12.5,
            0.02);

        PublishLatestFrameThroughVerseClient(statePath, frame);

        using var client = AetheriaClient
            .OpenAsync(statePath, "unity-frame-only-observer-test", pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        using var observedFrame = client.State.Reactive<AetheriaRuntimeDaemonFrameDocument>();
        using var observedSoaView = client.State.Reactive<AetheriaRuntimeDaemonSoaViewDocument>();
        using var observedZoneRender = client.State.Reactive<AetheriaRuntimeZoneRenderDocument>();
        Assert.IsTrue(AetheriaRuntimeObservedDaemonState.TryCreateCurrent(
            observedFrame,
            observedSoaView,
            observedZoneRender,
            out var observed));

        Assert.IsNotNull(observed);
        Assert.IsTrue(observed.IsAuthoritative);
        Assert.IsFalse(observed.HasSoaView);
        Assert.IsNull(observed.SoaView);
        Assert.AreEqual("daemon-frame-only-run", observed.Run.RunId);
        Assert.AreEqual("entity:frame-only-target", observed.Run.CurrentEntityKey);
        Assert.AreEqual(301, observed.Frame.FrameId);
        Assert.AreEqual(AetheriaRuntimeDaemonSchemas.ZoneRender, observed.ZoneRender.Schema);
        Assert.AreEqual(301, observed.ZoneRender.FrameId);
        Assert.AreEqual("daemon-frame-only-run", observed.ZoneRender.RunId);
        Assert.AreEqual("entity:frame-only-target", observed.ZoneRender.CurrentEntityKey);
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
    public void CommandClientUsesRuntimeNeutralDefaultClientId()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            "aetheria-daemon-command-client-tests",
            Path.GetRandomFileName(),
            "state.cc");
        var observed = ObservedState(frameId: 34, soaGeneration: 45);
        var client = new AetheriaRuntimeDaemonOperationClient(statePath, "", "session-default-client");

        var envelope = client.SensorPing(observed);

        Assert.AreEqual(AetheriaRuntimeDaemonOperationClient.DefaultClientId, envelope.ClientId);
        Assert.AreEqual(AetheriaRuntimeDaemonCommandKinds.SensorPing, envelope.Kind);
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
        PublishLatestFrameThroughVerseClient(statePath, frame);
        var request = new EveSurfaceCommandRequest(
            "aetheria.daemon",
            AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
            CultMesh.OperationInvocation("aetheria.daemon.commands.FireWeaponGroup"),
            CultMesh.OperationPayload(),
            DateTimeOffset.UtcNow,
            "unity-uitoolkit");

        using var client = AetheriaClient
            .OpenAsync(statePath, "unity-uitoolkit", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();

        Assert.IsTrue(AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(client, request, out var envelope));

        Assert.AreEqual(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, envelope.Kind);
        Assert.AreEqual("unity-uitoolkit", envelope.ClientId);
        Assert.AreEqual("session-surface-command", envelope.SessionId);
        Assert.AreEqual(77, envelope.ObservedFrameId);
        Assert.AreEqual("entity:surface-player", envelope.ActorEntityKey);
        Assert.IsEmpty(envelope.Path);
    }

    [Test]
    public void DaemonEveSurfaceCommandUsesRuntimeNeutralDefaultClientId()
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
            79,
            1.5,
            0.02);
        PublishLatestFrameThroughVerseClient(statePath, frame);
        var request = new EveSurfaceCommandRequest(
            "aetheria.daemon",
            AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
            CultMesh.OperationInvocation("aetheria.daemon.commands.SensorPing"),
            CultMesh.OperationPayload(),
            DateTimeOffset.UtcNow,
            "");

        using var client = AetheriaClient
            .OpenAsync(
                statePath,
                AetheriaRuntimeDaemonOperationClient.DefaultClientId,
                startServer: false,
                pullOnOpen: true)
            .GetAwaiter()
            .GetResult();

        Assert.IsTrue(AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(client, request, out var envelope));
        Assert.AreEqual(AetheriaRuntimeDaemonOperationClient.DefaultClientId, envelope.ClientId);
        Assert.AreEqual(AetheriaRuntimeDaemonCommandKinds.SensorPing, envelope.Kind);
    }

    [Test]
    public void DaemonEveSurfaceCommandRejectsUnsupportedArgumentlessOperation()
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
            78,
            1.5,
            0.02);
        PublishLatestFrameThroughVerseClient(statePath, frame);
        var request = new EveSurfaceCommandRequest(
            "aetheria.daemon",
            AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
            CultMesh.OperationInvocation("aetheria.daemon.commands.TransferCargoItem"),
            CultMesh.OperationPayload(),
            DateTimeOffset.UtcNow,
            "unity-uitoolkit");

        using var client = AetheriaClient
            .OpenAsync(statePath, "unity-uitoolkit", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();

        Assert.IsFalse(AetheriaRuntimeDaemonSurfaceCommands.TrySubmit(client, request, out var envelope));
        Assert.IsNull(envelope);
    }

    [Test]
    public void DaemonItemStatRefsRoundTripThroughSharedQueryHelper()
    {
        var stateRef = AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(
            "items/weapons/mining-laser",
            "behavior/mining",
            2,
            7);

        Assert.IsTrue(AetheriaRuntimeDaemonItemStatQueries.TryReadItemStatRef(
            stateRef,
            out var itemKey,
            out var behaviorKind,
            out var behaviorGroup,
            out var fieldKey));

        Assert.AreEqual("items/weapons/mining-laser", itemKey);
        Assert.AreEqual("behavior/mining", behaviorKind);
        Assert.AreEqual(2, behaviorGroup);
        Assert.AreEqual(7, fieldKey);
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
    public void DaemonCommandSchemaDoesNotOwnActionBarBindings()
    {
        var commandNames = Enum.GetNames(typeof(AetheriaRuntimeDaemonCommandKinds));
        var boundary = AetheriaRuntimeDaemonCommandBoundaryDocument.Create("codex");
        var commandBodies = boundary.Commands
            .Select(command => command.CommandBody)
            .ToArray();

        Assert.IsFalse(commandNames.Contains("SetActionBarBinding"));
        Assert.IsFalse(commandNames.Contains("ClearActionBarBinding"));
        Assert.IsFalse(commandBodies.Contains("AetheriaRuntimeActionBarBindingCommand"));
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
    public void DaemonOperationsRejectsInvalidWeaponGroupMembership()
    {
        var run = RunWithTwoEntities();
        var missingEquipment = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership,
            "codex",
            "session-weapon-groups",
            21,
            "zone.0.entity.0");
        missingEquipment.TargetEntityKey = "zone.0.entity.0";
        missingEquipment.EquipmentIndex = 99;
        missingEquipment.WeaponGroup = 0;
        missingEquipment.ScalarValue = 1.0;
        var negativeGroup = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership,
            "codex",
            "session-weapon-groups",
            22,
            "zone.0.entity.0");
        negativeGroup.TargetEntityKey = "zone.0.entity.0";
        negativeGroup.EquipmentIndex = 1;
        negativeGroup.WeaponGroup = -1;
        negativeGroup.ScalarValue = 1.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { missingEquipment, negativeGroup });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(2, result.RejectedCommandIds.Count);
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
    public void DaemonOperationsResolvesCargoSourcePositionWhenClientOmitsIt()
    {
        var run = RunWithTwoEntities();
        var transfer = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TransferCargoItem,
            "codex",
            "session-inventory",
            31,
            "zone.0.entity.0");
        transfer.TextValue = "ore";
        transfer.CargoTransfer.OriginEntityKey = "zone.0.entity.0";
        transfer.CargoTransfer.OriginCargoIndex = 0;
        transfer.CargoTransfer.DestinationEntityKey = "zone.0.entity.1";
        transfer.CargoTransfer.DestinationCargoIndex = 0;
        transfer.CargoTransfer.SourceX = int.MinValue;
        transfer.CargoTransfer.SourceY = int.MinValue;

        var transferResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { transfer });
        Assert.AreEqual(1, transferResult.AppliedCommandIds.Count);
        Assert.AreEqual(0, run.Zones[0].Entities[0].CargoContents[0].Items.Count);
        Assert.AreEqual("ore", run.Zones[0].Entities[1].CargoContents[0].Items[0].Item.ItemKey);

        var equip = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.EquipItem,
            "codex",
            "session-inventory",
            32,
            "zone.0.entity.0");
        equip.TextValue = "ore";
        equip.EquipmentTransfer.SourceKind = "cargo";
        equip.EquipmentTransfer.OriginEntityKey = "zone.0.entity.1";
        equip.EquipmentTransfer.OriginIndex = 0;
        equip.EquipmentTransfer.DestinationEntityKey = "zone.0.entity.0";
        equip.EquipmentTransfer.SourceX = int.MinValue;
        equip.EquipmentTransfer.SourceY = int.MinValue;

        var equipResult = AetheriaRuntimeDaemonOperations.Execute(run, new[] { equip });
        Assert.AreEqual(1, equipResult.AppliedCommandIds.Count);
        Assert.AreEqual(3, run.Zones[0].Entities[0].Equipment.Count);
        Assert.AreEqual("ore", run.Zones[0].Entities[0].Equipment[2].Item.ItemKey);
        Assert.AreEqual(0, run.Zones[0].Entities[1].CargoContents[0].Items.Count);
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
    public void DaemonOperationsRejectsInvalidHullConductivityToggle()
    {
        var outOfRange = RunWithTwoEntities();
        var invalidPosition = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity,
            "codex",
            "session-hull",
            29,
            "zone.0.entity.0");
        invalidPosition.TargetEntityKey = "zone.0.entity.0";
        invalidPosition.PositionX = 99;
        invalidPosition.PositionY = 0;
        invalidPosition.ScalarValue = 0;

        var outOfRangeResult = AetheriaRuntimeDaemonOperations.Execute(outOfRange, new[] { invalidPosition });
        Assert.AreEqual(0, outOfRangeResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, outOfRangeResult.RejectedCommandIds.Count);
        CollectionAssert.AreEqual(new[] { 1.0, 0.0, 0.0, 1.0 }, outOfRange.Zones[0].Entities[0].StatGrids[0].Values);

        var invalidAxis = RunWithTwoEntities();
        var invalidAxisCommand = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity,
            "codex",
            "session-hull",
            30,
            "zone.0.entity.0");
        invalidAxisCommand.TargetEntityKey = "zone.0.entity.0";
        invalidAxisCommand.PositionX = 1;
        invalidAxisCommand.PositionY = 0;
        invalidAxisCommand.ScalarValue = 2;

        var invalidAxisResult = AetheriaRuntimeDaemonOperations.Execute(invalidAxis, new[] { invalidAxisCommand });
        Assert.AreEqual(0, invalidAxisResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, invalidAxisResult.RejectedCommandIds.Count);
        CollectionAssert.AreEqual(new[] { 1.0, 0.0, 0.0, 1.0 }, invalidAxis.Zones[0].Entities[0].StatGrids[0].Values);
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
    public void DaemonOperationsCreatesPurchasedDockedShipInDaemonState()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.TradePurchase,
            "codex",
            "session-trade",
            31,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.1";
        command.TextValue = "starter-hull";
        command.ScalarValue = 300;
        command.TradePurchase.PurchaseKind = "docked_ship";
        command.TradePurchase.ItemKey = "starter-hull";
        command.TradePurchase.Quantity = 1;
        command.TradePurchase.UnitPrice = 300;
        command.TradePurchase.TotalPrice = 300;
        command.TradePurchase.TargetEntityKey = "zone.0.entity.1";
        command.TradePurchase.CreatesDockedShip = true;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { command });

        var zone = run.Zones[0];
        Assert.AreEqual(1, result.AppliedCommandIds.Count);
        Assert.AreEqual(700, run.Credits);
        Assert.AreEqual(3, zone.Entities.Count);
        Assert.AreEqual("ship", zone.Entities[2].Kind);
        Assert.AreEqual("starter-hull", zone.Entities[2].HullItemKey);
        CollectionAssert.Contains(zone.Entities[1].ChildEntityIndices, 2);
        CollectionAssert.Contains(zone.Entities[1].DockingBayAssignments, 2);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.0.entity.2.v1", run.CurrentEntityKey);
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
    public void DaemonOperationsRejectsMissingLoadoutTemplateWithoutChargingCredits()
    {
        var run = RunWithTwoEntities();
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.RestoreLoadout,
            "codex",
            "session-loadout",
            32,
            "zone.0.entity.0");
        command.TargetEntityKey = "zone.0.entity.0";
        command.TextValue = "Missing";
        command.ScalarValue = 250;
        command.LoadoutRestore.DockedEntityKey = "zone.0.entity.0";
        command.LoadoutRestore.TemplateName = "Missing";
        command.LoadoutRestore.Price = 250;

        var result = AetheriaRuntimeDaemonOperations.Execute(
            run,
            new[] { command },
            new AetheriaRuntimeDaemonOperationContext
            {
                LoadoutTemplates = Array.Empty<AetheriaRuntimeLoadoutTemplateCommit>()
            });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(1, result.RejectedCommandIds.Count);
        Assert.AreEqual(1000, run.Credits);
        Assert.AreEqual(2, run.Zones[0].Entities.Count);
        Assert.AreEqual("global:aetheria.run_state.daemon-command-apply-run.zone.0.entity.0.v1", run.CurrentEntityKey);
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
    public void DaemonOperationsInteractPrioritizesUndockWormholeThenDock()
    {
        var undockRun = RunWithTwoEntities();
        undockRun.Zones[0].Entities[1].ChildEntityIndices = new[] { 0 };
        undockRun.Zones[0].Entities[1].DockingBayAssignments = new[] { 0, -1 };
        var undock = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.Interact,
            "codex",
            "session-interact",
            46,
            "zone.0.entity.0");
        undock.ScalarValue = 10;
        undock.PositionX = 10;

        var undockResult = AetheriaRuntimeDaemonOperations.Execute(undockRun, new[] { undock });
        Assert.AreEqual(1, undockResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, undockResult.Intents.Docking.Count);
        Assert.IsTrue(undockResult.Intents.Docking[0].Undock);
        CollectionAssert.DoesNotContain(undockRun.Zones[0].Entities[1].DockingBayAssignments, 0);

        var wormholeRun = RunWithTwoEntities();
        wormholeRun.Zones[0].AdjacentZoneIndices = new[] { 2 };
        wormholeRun.Zones[0].Entities[0].PositionX = 0;
        wormholeRun.Zones[0].Entities[0].PositionZ = 0;
        wormholeRun.Zones = wormholeRun.Zones
            .Concat(new[]
            {
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 2,
                    PositionX = 3,
                    PositionY = 4
                }
            })
            .ToArray();
        var wormhole = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.Interact,
            "codex",
            "session-interact",
            47,
            "zone.0.entity.0");
        wormhole.ScalarValue = 10;
        wormhole.PositionX = 10;

        var wormholeResult = AetheriaRuntimeDaemonOperations.Execute(wormholeRun, new[] { wormhole });
        Assert.AreEqual(1, wormholeResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, wormholeResult.Intents.Wormholes.Count);
        Assert.AreEqual(2, wormholeRun.CurrentZoneIndex);
        Assert.AreEqual(3, FindZone(wormholeRun, 2).Entities[0].PositionX, 0.0001);
        Assert.AreEqual(4, FindZone(wormholeRun, 2).Entities[0].PositionZ, 0.0001);

        var dockRun = RunWithTwoEntities();
        dockRun.Zones[0].Entities[0].PositionX = 0;
        dockRun.Zones[0].Entities[0].PositionY = 0;
        dockRun.Zones[0].Entities[1].PositionX = 3;
        dockRun.Zones[0].Entities[1].PositionY = 4;
        var dock = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.Interact,
            "codex",
            "session-interact",
            48,
            "zone.0.entity.0");
        dock.ScalarValue = 10;
        dock.PositionX = 1;

        var dockResult = AetheriaRuntimeDaemonOperations.Execute(dockRun, new[] { dock });
        Assert.AreEqual(1, dockResult.AppliedCommandIds.Count);
        Assert.AreEqual(1, dockResult.Intents.Docking.Count);
        Assert.IsTrue(dockResult.Intents.Docking[0].Dock);
        CollectionAssert.Contains(dockRun.Zones[0].Entities[1].ChildEntityIndices, 0);
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
                        Name = "Tow Station",
                        PositionX = 300,
                        PositionZ = 400
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
        var negative = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup,
            "codex",
            "session-intents",
            33,
            "zone.0.entity.0");
        negative.WeaponGroup = -1;
        var missing = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive,
            "codex",
            "session-intents",
            34,
            "zone.0.entity.0");
        missing.WeaponGroup = 99;
        missing.ScalarValue = 1.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { negative, missing });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(2, result.RejectedCommandIds.Count);
        Assert.AreEqual(0, result.Intents.WeaponGroups.Count);
    }

    [Test]
    public void DaemonOperationsRejectsBehaviorIntentForMissingBehavior()
    {
        var run = RunWithTwoEntities();
        var negative = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive,
            "codex",
            "session-intents",
            34,
            "zone.0.entity.0");
        negative.EquipmentIndex = -1;
        negative.BehaviorIndex = 0;
        negative.ScalarValue = 1.0;
        var missing = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive,
            "codex",
            "session-intents",
            35,
            "zone.0.entity.0");
        missing.EquipmentIndex = 0;
        missing.BehaviorIndex = 99;
        missing.ScalarValue = 1.0;

        var result = AetheriaRuntimeDaemonOperations.Execute(run, new[] { negative, missing });

        Assert.AreEqual(0, result.AppliedCommandIds.Count);
        Assert.AreEqual(2, result.RejectedCommandIds.Count);
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
        Assert.AreEqual(AetheriaRuntimeDaemonOperationIds.SetMoveVector, envelope.OperationId);
        Assert.IsTrue(envelope.Accepted);
        Assert.AreEqual(CultMeshLocalityKind.Network, envelope.Route.Kind);
        Assert.IsNull(envelope.Diagnostic);
    }

    [Test]
    public void EveCommandDocumentPreservesCultMeshInvocationAndPayload()
    {
        var request = new EveSurfaceCommandRequest(
            "aetheria",
            AetheriaRuntimePlayerSettingsCommands.SurfaceId,
            CultMesh.OperationInvocation(
                AetheriaRuntimePlayerSettingsCommands.SetPlayerName,
                AetheriaRuntimeEveCommandDocument.SchemaId,
                new CultMeshRouteHint(CultMeshLocalityKind.Ipc, "eve-ui-test"),
                "idempotent-player-name"),
            CultMesh.OperationPayload(("value", "Raven")),
            DateTimeOffset.UtcNow,
            "unity-raven");

        var envelope = AetheriaRuntimeEveCommandClient.CreatePlayerSettingsCommand(request, "unity-raven");
        var document = AetheriaRuntimeEveCommandClient.ToDocument(envelope);
        var restored = AetheriaRuntimeEveCommandClient.ToEnvelope(document);

        Assert.AreEqual(AetheriaRuntimePlayerSettingsCommands.SetPlayerName, document.OperationId);
        Assert.AreEqual(AetheriaRuntimeEveCommandDocument.SchemaId, document.OperationSchemaId);
        Assert.AreEqual(CultMeshLocalityKind.Ipc.ToString(), document.OperationRouteKind);
        Assert.AreEqual("eve-ui-test", document.OperationRouteDescription);
        Assert.AreEqual("idempotent-player-name", document.OperationIdempotencyKey);
        Assert.AreEqual(AetheriaRuntimePlayerSettingsCommands.SetPlayerName, document.Operation.OperationId);
        Assert.AreEqual(AetheriaRuntimeEveCommandDocument.SchemaId, document.Operation.SchemaId);
        Assert.AreEqual(CultMeshLocalityKind.Ipc.ToString(), document.Operation.RouteKind);
        Assert.AreEqual("eve-ui-test", document.Operation.RouteDescription);
        Assert.AreEqual("idempotent-player-name", document.Operation.IdempotencyKey);
        Assert.IsTrue(document.Payload.TryGetValue("value", out var documentValue));
        Assert.AreEqual("Raven", documentValue);
        Assert.AreEqual(AetheriaRuntimePlayerSettingsCommands.SetPlayerName, restored.Invocation.OperationId);
        Assert.AreEqual(CultMeshLocalityKind.Ipc, restored.Invocation.RouteHint.Kind);
        Assert.AreEqual("Raven", restored.Payload.GetString("value"));
        Assert.AreEqual("Raven", restored.PlayerSettings.PlayerName);
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
            new[] { new AetheriaRuntimeDaemonSoaColumnDocument { ColumnId = "position", BufferId = "hot" } });
        var zoneRender = new AetheriaRuntimeZoneRenderDocument
        {
            RunId = frame.Run.RunId,
            FrameId = frame.FrameId
        };

        return new AetheriaRuntimeObservedDaemonState(
            frame,
            soaView,
            zoneRender);
    }

    private static void PublishLatestFrameThroughVerseClient(
        string statePath,
        AetheriaRuntimeDaemonFrameDocument frame)
    {
        using var client = AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "daemon-surface-command-test", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        client
            .MutableDocument<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
            .ReplaceAsync(frame)
            .GetAwaiter()
            .GetResult();
        client.FlushAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static void PublishLatestSoaViewThroughVerseClient(
        string statePath,
        AetheriaRuntimeDaemonSoaViewDocument soaView)
    {
        using var client = AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "daemon-soa-view-test", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        client
            .MutableDocument<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest)
            .ReplaceAsync(soaView)
            .GetAwaiter()
            .GetResult();
        client.FlushAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static void PublishDaemonSurfacesThroughVerseClient(
        string statePath,
        AetheriaRuntimeDaemonTickResult result)
    {
        using var client = AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "daemon-surfaces-test", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        client
            .MutableDocument<Aetheria.State.Documents.EveSurfaceState>(
                AetheriaRuntimeVerseRecordKeys.DaemonGameSurface)
            .ReplaceAsync(ToEveSurfaceState(result.GameSurface))
            .GetAwaiter()
            .GetResult();
        client
            .MutableDocument<Aetheria.State.Documents.EveSurfaceState>(
                AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface)
            .ReplaceAsync(ToEveSurfaceState(result.GameTuiSurface))
            .GetAwaiter()
            .GetResult();
        client
            .MutableDocument<Aetheria.State.Documents.EveSurfaceState>(
                AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface)
            .ReplaceAsync(ToEveSurfaceState(result.EditorSurface))
            .GetAwaiter()
            .GetResult();
        client
            .MutableDocument<Aetheria.State.Documents.EveSurfaceState>(
                AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface)
            .ReplaceAsync(ToEveSurfaceState(result.EditorTuiSurface))
            .GetAwaiter()
            .GetResult();
        client.FlushAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static Aetheria.State.Documents.EveSurfaceState ToEveSurfaceState(
        AetheriaRuntimeSurfaceDocument document)
    {
        return new Aetheria.State.Documents.EveSurfaceState
        {
            ProviderId = document.ProviderId,
            ProviderKind = document.ProviderKind,
            Title = document.Title,
            Version = document.Version,
            UpdatedAtUtc = document.UpdatedAtUtc,
            Surface = ToEveSurface(document.Surface),
            Commands = document.Commands
                .Select(command =>
                {
                    var record = CultMesh.OperationBindingRecord(command.Operation);
                    return new Aetheria.State.Documents.EveCommandTemplate
                    {
                        Command = record.OperationId,
                        Label = record.Label,
                        Transport = record.RouteDescription,
                        SchemaId = record.SchemaId,
                        RouteKind = record.RouteKind,
                        RouteDescription = record.RouteDescription
                    };
                })
                .ToArray()
        };
    }

    private static Aetheria.State.Documents.EveSurface ToEveSurface(AetheriaRuntimeSurfaceTree surface)
    {
        return new Aetheria.State.Documents.EveSurface
        {
            Id = surface.Id,
            Root = ToEveSurfaceComponent(surface.Root),
            Styles = surface.Styles
                .Select(style => new Aetheria.State.Documents.EveStyleToken
                {
                    Name = style.Name,
                    Value = style.Value
                })
                .ToArray()
        };
    }

    private static Aetheria.State.Documents.EveSurfaceComponent ToEveSurfaceComponent(
        AetheriaRuntimeSurfaceComponent component)
    {
        var props = new Dictionary<string, string>(component.Props, StringComparer.Ordinal);
        AetheriaRuntimeSurfaceStateBindings.AddPointerProps(props, component.StateBindings);

        return new Aetheria.State.Documents.EveSurfaceComponent
        {
            Id = component.Id,
            Kind = component.Kind,
            Props = props,
            Children = component.Children.Select(ToEveSurfaceComponent).ToArray(),
            StateBindings = component.StateBindings
                .Select(binding =>
                {
                    var record = CultMesh.StateBindingRecord(binding);
                    return new Aetheria.State.Documents.EveSurfaceStateBinding
                    {
                        TargetProp = record.TargetProp,
                        PointerId = record.PointerId,
                        SourceId = record.SourceId,
                        SchemaId = record.SchemaId,
                        RouteKind = record.RouteKind,
                        RouteDescription = record.RouteDescription
                    };
                })
                .ToArray()
        };
    }

    private static void PublishVerseAuthorityPolicyThroughVerseClient(
        string statePath,
        AetheriaRuntimeVerseAuthorityPolicyDocument policy)
    {
        using var client = AetheriaRuntimeVerseClient
            .OpenAsync(statePath, "daemon-authority-policy-test", startServer: false, pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        client
            .MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)
            .ReplaceAsync(policy)
            .GetAwaiter()
            .GetResult();
        client.FlushAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static AetheriaRuntimeCatalogItem CatalogItem(
        string itemKey,
        IReadOnlyList<AetheriaRuntimeBehaviorPayload> behaviorPayloads)
    {
        return new AetheriaRuntimeCatalogItem(
            itemKey,
            itemKey,
            "",
            "",
            "",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            Array.Empty<AetheriaRuntimeShapeCell>(),
            0,
            0,
            0,
            Array.Empty<AetheriaRuntimeShapeCell>(),
            Array.Empty<AetheriaRuntimeHardpoint>(),
            behaviorPayloads,
            "",
            "",
            behaviorPayloads.Select(payload => payload.Kind).ToArray(),
            1,
            false,
            0,
            1,
            "",
            "",
            "",
            "",
            "",
            0,
            0,
            Array.Empty<AetheriaRuntimeCurveKey>(),
            "",
            1,
            0,
            0,
            0,
            false,
            0,
            0,
            "",
            Array.Empty<AetheriaRuntimeAudioStat>(),
            Array.Empty<AetheriaRuntimeCurveKey>(),
            "",
            "");
    }

    private static AetheriaRuntimeBehaviorValue NumberValue(double value)
    {
        return new AetheriaRuntimeBehaviorValue(
            "",
            "",
            value,
            false,
            "",
            "",
            Array.Empty<AetheriaRuntimeBehaviorValue>(),
            Array.Empty<AetheriaRuntimeBehaviorMapEntry>());
    }

    private static AetheriaRuntimeBehaviorValue EmptyBehaviorValue(string kind)
    {
        return new AetheriaRuntimeBehaviorValue(
            kind,
            "",
            0,
            false,
            "",
            "",
            Array.Empty<AetheriaRuntimeBehaviorValue>(),
            Array.Empty<AetheriaRuntimeBehaviorMapEntry>());
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

    private static bool ContainsSurfaceStateBinding(
        AetheriaRuntimeSurfaceComponent component,
        string targetProp,
        string sourceId,
        string schemaId)
    {
        if (component.StateBindings.Any(binding =>
                binding.TargetProp == targetProp &&
                binding.SourceId == sourceId &&
                binding.SchemaId == schemaId))
        {
            return true;
        }

        return component.Children.Any(child => ContainsSurfaceStateBinding(child, targetProp, sourceId, schemaId));
    }

    private static bool ContainsEveSurfaceMetric(
        EveSurfaceComponent component,
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

        return component.Children.Any(child => ContainsEveSurfaceMetric(child, label, value));
    }

    private static bool ContainsEveSurfaceProp(
        EveSurfaceComponent component,
        string key,
        string value)
    {
        if (component.Props.TryGetValue(key, out var actual) &&
            string.Equals(actual, value, StringComparison.Ordinal))
        {
            return true;
        }

        return component.Children.Any(child => ContainsEveSurfaceProp(child, key, value));
    }

    private static bool ContainsEveSurfaceStateBinding(
        EveSurfaceComponent component,
        string targetProp,
        string sourceId,
        string schemaId)
    {
        if (component.StateBindings.Any(binding =>
                binding.TargetProp == targetProp &&
                binding.SourceId == sourceId &&
                binding.SchemaId == schemaId))
        {
            return true;
        }

        return component.Children.Any(child => ContainsEveSurfaceStateBinding(child, targetProp, sourceId, schemaId));
    }
}
