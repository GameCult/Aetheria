using Aetheria.State;
using Aetheria.State.Documents;
using Aetheria.State.Daemon;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using GameCult.Networking;
using MessagePack;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

var options = AetheriaDaemonHostOptions.Parse(args);
var startedAtUtc = DateTimeOffset.UtcNow.ToString("O");
var projectilePhysics = new AetheriaYmirProjectilePhysics();
var worldPhysics = new AetheriaYmirWorldPhysics();

Console.WriteLine($"Aetheria Verse daemon starting: {options.StatePath}");
Console.WriteLine(options.EnableOdinAnnouncements
    ? "Aetheria Verse daemon peers: Odin/CultMesh discovery"
    : "Aetheria Verse daemon peers: local child-daemon transport");
if (options.EnableOdinAnnouncements)
    Console.WriteLine($"Aetheria Odin announcement target: {options.OdinCultMeshUri}");

await using var node = await AetheriaStateNode.OpenAsync(
    options.StatePath,
    runtimeId: options.DaemonId,
    startServer: true,
    enableDurableShardLogs: false).ConfigureAwait(false);
using var discoveryHost = new AetheriaVerseDiscoveryHost(node);

await EnsureWorldDocumentAsync(node).ConfigureAwait(false);
await EnsureTradeValuePolicyAsync(node, startedAtUtc).ConfigureAwait(false);
await node.FlushAsync().ConfigureAwait(false);
await EnsurePlayableRunDocumentsAsync(node, options, startedAtUtc).ConfigureAwait(false);
await EnsureTerminusGameSessionAsync(node, options, startedAtUtc).ConfigureAwait(false);
var verseHost = await EnsureVerseHostSettingsAsync(node, options, startedAtUtc).ConfigureAwait(false);
await EnsureVerseAuthorityPolicyAsync(node, options).ConfigureAwait(false);
discoveryHost.Update(verseHost);
await PublishRuntimeSessionAsync(node, options, startedAtUtc, "starting").ConfigureAwait(false);
await PublishStateSurfacesAsync(node, options, startedAtUtc).ConfigureAwait(false);
var latestFrame = await node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest).ReadAsync().ConfigureAwait(false);
using var cultMeshRudpHost = StartClientCultMeshHost(node, options, () => latestFrame);
using var clientPumpCancellation = new CancellationTokenSource();
var clientPump = RunClientCultMeshPumpAsync(cultMeshRudpHost, clientPumpCancellation.Token);
var nextApiPublicationUtc = DateTimeOffset.UtcNow;
var firstTick = await TickAsync(node, options, projectilePhysics, worldPhysics, latestFrame, buildPublications: true).ConfigureAwait(false);
ThrowIfClientPumpFaulted(clientPump);
latestFrame = firstTick.Frame;
nextApiPublicationUtc = DateTimeOffset.UtcNow.Add(options.ApiPublicationInterval);
Console.WriteLine($"Aetheria Verse daemon published frame {firstTick.Frame.FrameId}.");
Console.WriteLine($"Aetheria client CultMesh endpoint: rudp://{options.ClientCultMeshAdvertiseHost}:{cultMeshRudpHost.LocalEndPoint.Port}");

if (options.Once)
{
    await PublishRuntimeSessionAsync(node, options, startedAtUtc, "completed").ConfigureAwait(false);
    clientPumpCancellation.Cancel();
    await clientPump.ConfigureAwait(false);
    return;
}
var stopped = new TaskCompletionSource<object?>();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stopped.TrySetResult(null);
};

await PublishRuntimeSessionAsync(node, options, startedAtUtc, "running").ConfigureAwait(false);
Console.WriteLine("Aetheria Verse daemon is running. Press Ctrl+C to stop.");

var nextTickUtc = DateTimeOffset.UtcNow.Add(options.TickInterval);
while (!stopped.Task.IsCompleted)
{
    ThrowIfClientPumpFaulted(clientPump);
    var delay = nextTickUtc - DateTimeOffset.UtcNow;
    if (delay > TimeSpan.Zero)
    {
        var completed = await Task.WhenAny(stopped.Task, Task.Delay(delay)).ConfigureAwait(false);
        if (completed == stopped.Task)
            break;
    }

    var buildPublications = DateTimeOffset.UtcNow >= nextApiPublicationUtc;
    var tick = await TickAsync(node, options, projectilePhysics, worldPhysics, latestFrame, buildPublications).ConfigureAwait(false);
    ThrowIfClientPumpFaulted(clientPump);
    latestFrame = tick.Frame;
    nextTickUtc += options.TickInterval;
    if (nextTickUtc < DateTimeOffset.UtcNow - options.TickInterval)
        nextTickUtc = DateTimeOffset.UtcNow;
    if (buildPublications)
    {
        nextApiPublicationUtc = DateTimeOffset.UtcNow.Add(options.ApiPublicationInterval);
        discoveryHost.Update(await node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey).ReadAsync().ConfigureAwait(false));
        await PublishRuntimeSessionAsync(node, options, startedAtUtc, "running").ConfigureAwait(false);
    }
    if (tick.Frame.FrameId % 120 == 0)
        Console.WriteLine($"Aetheria Verse daemon published frame {tick.Frame.FrameId} at {tick.Frame.SimulationTimeSeconds:0.00}s.");
}

await PublishRuntimeSessionAsync(node, options, startedAtUtc, "stopping").ConfigureAwait(false);
clientPumpCancellation.Cancel();
await clientPump.ConfigureAwait(false);
Console.WriteLine("Aetheria Verse daemon stopping.");

static void ThrowIfClientPumpFaulted(Task clientPump)
{
    if (!clientPump.IsCompleted)
        return;

    if (clientPump.IsFaulted)
        throw new InvalidOperationException("Aetheria client CultMesh pump faulted.", clientPump.Exception);

    throw new InvalidOperationException("Aetheria client CultMesh pump stopped unexpectedly.");
}

static async Task<AetheriaRuntimeDaemonTickResult> TickAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    IAetheriaRuntimeProjectilePhysics projectilePhysics,
    IAetheriaRuntimeWorldPhysics worldPhysics,
    AetheriaRuntimeDaemonFrameDocument? currentFrame,
    bool buildPublications)
{
    await AcceptCoreEveInvocationsAsync(node, options, currentFrame).ConfigureAwait(false);
    await AcceptEveCommandsAsync(node, options).ConfigureAwait(false);
    if (await ApplyRequestedTerminusSessionAsync(node, options).ConfigureAwait(false))
        currentFrame = null;

    var fixedDeltaSeconds = currentFrame?.FixedDeltaSeconds > 0
        ? currentFrame.FixedDeltaSeconds
        : options.FixedDeltaSeconds;
    var nextFrameId = (currentFrame?.FrameId ?? -1) + 1;
    var simulationTimeSeconds = (currentFrame?.SimulationTimeSeconds ?? 0) + fixedDeltaSeconds;
    var sessionId = string.IsNullOrWhiteSpace(currentFrame?.SessionId)
        ? options.SessionId
        : currentFrame.SessionId;
    var run = HasPlayableRun(currentFrame?.Run)
        ? currentFrame!.Run
        : await ReadRuntimeRunCheckpointAsync(node, options.RenderSettings).ConfigureAwait(false) ?? new AetheriaRuntimeRunCheckpointCommit();
    ApplyDaemonRenderSettings(run, options.RenderSettings);

    var loadoutTemplates = node.Cache
        .GetAll<AetheriaLoadoutTemplate>()
        .Select(ToLoadoutTemplateCommit)
        .ToArray();
    var observedCommands = node.Documents<AetheriaRuntimeDaemonCommandDocument>()
        .OrderBy(command => command.IssuedAtUtc ?? "", StringComparer.Ordinal)
        .ThenBy(command => command.CommandId ?? "", StringComparer.Ordinal)
        .ToArray();
    var accountedCommandIds = new HashSet<string>(
        currentFrame?.AccountedCommandIds ?? Array.Empty<string>(),
        StringComparer.Ordinal);
    var pendingObservedCommands = observedCommands
        .Where(command => command != null && !accountedCommandIds.Contains(command.CommandId ?? ""))
        .ToArray();
    var policyRejectedCommandIds = new List<string>();
    var authorityPolicy = await node.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy).ReadAsync().ConfigureAwait(false);
    var starbridgeScenario = await node.MutableDocument<AetheriaRuntimeStarbridgeScenarioDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest).ReadAsync().ConfigureAwait(false);
    var starbridgeSession = await node.MutableDocument<AetheriaRuntimeStarbridgeSessionDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest).ReadAsync().ConfigureAwait(false);
    var authorityLeases = node.Documents<AetheriaRuntimeAuthorityLeaseDocument>();
    var authorizedCommands = AetheriaRuntimeAuthorityRouter.AuthorizedCommands(
        pendingObservedCommands,
        authorityPolicy,
        authorityLeases,
        options.DaemonId,
        policyRejectedCommandIds);

    var result = AetheriaRuntimeDaemonTickRunner.Tick(
        node.StatePath,
        run,
        new AetheriaRuntimeDaemonTickOptions
        {
            DaemonId = options.DaemonId,
            SessionId = sessionId,
            VerseId = options.VerseId,
            CultMeshAddress = options.CultMeshAddress,
            FrameId = nextFrameId,
            SimulationTimeSeconds = simulationTimeSeconds,
            FixedDeltaSeconds = fixedDeltaSeconds,
            ObservedCommands = authorizedCommands,
            AccountedCommandIds = accountedCommandIds.ToArray(),
            PreRejectedCommandIds = policyRejectedCommandIds,
            CumulativeAppliedCommandIds = currentFrame?.CumulativeAppliedCommandIds ?? currentFrame?.AppliedCommandIds ?? Array.Empty<string>(),
            CumulativeRejectedCommandIds = currentFrame?.CumulativeRejectedCommandIds ?? currentFrame?.RejectedCommandIds ?? Array.Empty<string>(),
            Catalog = node.RuntimeCatalog().Latest(),
            RenderSettings = options.RenderSettings,
            SimulationSettings = options.SimulationSettings,
            ProjectilePhysics = projectilePhysics,
            WorldPhysics = worldPhysics,
            StarbridgeScenario = starbridgeScenario,
            StarbridgeSession = starbridgeSession,
            BuildPublications = buildPublications,
            OperationContext = new AetheriaRuntimeDaemonOperationContext
            {
                LoadoutTemplates = loadoutTemplates
            }
        });
    if (string.Equals(Environment.GetEnvironmentVariable("AETHERIA_TRACE_EVE_SNAPSHOTS"), "1", StringComparison.Ordinal) &&
        result.Frame.AppliedCommandIds.Count > 0)
    {
        foreach (var entity in (result.Frame.Run?.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                     .SelectMany(zone => zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                     .Where(entity => string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(
                $"Eve command result entity={entity.EntityIndex} position={entity.PositionX},{entity.PositionZ} " +
                $"velocity={entity.VelocityX},{entity.VelocityY}");
        }
    }
    result.Frame.CumulativeImportedFactIds =
        currentFrame?.CumulativeImportedFactIds ?? Array.Empty<string>();
    result.Frame.CumulativeRejectedImportedFactIds =
        currentFrame?.CumulativeRejectedImportedFactIds ?? Array.Empty<string>();

    await PublishCommittedCommandFactsAsync(
        node,
        options,
        result.Frame,
        pendingObservedCommands,
        authorizedCommands,
        policyRejectedCommandIds).ConfigureAwait(false);

    if (buildPublications)
    {
        await PublishDaemonApiDocumentsAsync(node, options, result).ConfigureAwait(false);
        await PublishStateSurfacesAsync(node, options, result.Frame.PublishedAtUtc).ConfigureAwait(false);
        await PublishOdinSurfaceAnnouncementsAsync(node, options, result.Frame.PublishedAtUtc).ConfigureAwait(false);
    }

    return result;
}

static async Task PublishCommittedCommandFactsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    AetheriaRuntimeDaemonFrameDocument frame,
    IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> pendingObservedCommands,
    IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> authorizedCommands,
    IReadOnlyList<string> policyRejectedCommandIds)
{
    var byCommandId = (pendingObservedCommands ?? Array.Empty<AetheriaRuntimeDaemonCommandDocument>())
        .Where(command => command != null && !string.IsNullOrWhiteSpace(command.CommandId))
        .ToDictionary(command => command.CommandId, StringComparer.Ordinal);

    foreach (var commandId in frame.AppliedCommandIds ?? Array.Empty<string>())
    {
        if (string.IsNullOrWhiteSpace(commandId) || !byCommandId.TryGetValue(commandId, out var command))
            continue;

        await PublishCommittedFactAsync(
            node,
            AetheriaRuntimeCommittedCommandFactDocument.FromAppliedCommand(
                frame,
                command,
                options.VerseId)).ConfigureAwait(false);
    }

    foreach (var commandId in frame.RejectedCommandIds ?? Array.Empty<string>())
    {
        if (string.IsNullOrWhiteSpace(commandId) ||
            !byCommandId.TryGetValue(commandId, out var command))
            continue;

        await PublishCommittedFactAsync(
            node,
            AetheriaRuntimeCommittedCommandFactDocument.FromRejectedCommand(
                frame,
                command,
                options.VerseId)).ConfigureAwait(false);
    }
}

static async Task PublishCommittedFactAsync(
    AetheriaStateNode node,
    AetheriaRuntimeCommittedCommandFactDocument fact)
{
    await node.PutCommittedCommandFactAsync(fact).ConfigureAwait(false);
    var applied = string.Equals(
        fact.Outcome,
        AetheriaRuntimeCommandFactOutcomes.Applied,
        StringComparison.Ordinal);
    var receipt = new EveCommandReceiptDocument(
        fact.FactId,
        fact.CommandId,
        fact.CommandKind.ToString(),
        applied ? "reconciled" : "denied",
        "Aetheria",
        fact.SourceDaemonId,
        "aetheria.daemon",
        AetheriaRuntimeDaemonGameSurfaceBuilder.SurfaceId,
        applied ? "Command applied by authoritative daemon." : "Command rejected by authoritative daemon.",
        fact.CommittedAtUtc,
        Math.Max(fact.SourceFrameId, 0));
    await node.Database.PutAsync(AetheriaRuntimeVerseRecordKeys.EveReceiptForCommand(receipt.CommandId), receipt)
        .ConfigureAwait(false);
}

static RudpCultNetSchemaServer StartClientCultMeshHost(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    Func<AetheriaRuntimeDaemonFrameDocument?> latestFrame)
{
    var bundleCdnDocuments = BuildBundleCdnDocuments(node, options);
    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    socket.Bind(new IPEndPoint(ParseBindAddress(options.ClientCultMeshHost), options.ClientCultMeshPort));
    var server = new RudpCultNetSchemaServer(new RudpCultNetSchemaServerOptions
    {
        RuntimeId = $"{options.DaemonId}.client",
        Socket = socket,
        ConnectionId = 0x43554c54,
        TransportId = "aetheria-client-rudp",
        MaxFragmentBytes = 2048,
        MaxPendingReliablePackets = 512
    });
    var advertisedEndpoint = $"rudp://{options.ClientCultMeshAdvertiseHost}:{((IPEndPoint)socket.LocalEndPoint!).Port}";
    server.OnCultNet<CultMeshVerseCatalogRequestMessage>((request, peer) =>
    {
        var descriptor = new CultMeshVerseDescriptor(
            options.VerseId,
            "Aetheria",
            CultMeshVerseAuthorityModel.OperatorCluster,
            new CultMeshVerseCompatibility(
                "cultmesh.v0",
                CultMeshVerseDescriptor.ComputeRulesHash("aetheria", "runtime-world.v1")),
            discoveryEndpoints: new[] { advertisedEndpoint },
            authorityRuntimeIds: new[] { options.DaemonId },
            description: "Aetheria provider Verse");
        peer.SendCultNet(new CultMeshVerseCatalogResponseMessage
        {
            MessageId = string.IsNullOrWhiteSpace(request.MessageId) ? Guid.NewGuid().ToString("N") : request.MessageId,
            Verses = new[] { descriptor.ToMessage() }
        });
    });
    server.OnCultNet<CultNetSnapshotRequestMessage>(async (request, peer) =>
    {
        try
        {
            var response = node.Database.Documents.CreateRawSnapshotResponse(
                node.Cache,
                string.IsNullOrWhiteSpace(request.MessageId) ? Guid.NewGuid().ToString("N") : request.MessageId,
                request);
            var frame = latestFrame();
            var hasFrame = frame != null;
            if (hasFrame && frame != null && SnapshotWants(
                request,
                AetheriaRuntimeDaemonSchemas.Frame,
                "daemon:aetheria.frame.latest.v1"))
            {
                var framePut = node.Database.Documents.CreateRawDocumentPutMessage(
                    response.MessageId,
                    new CultRecordHandle<AetheriaRuntimeDaemonFrameDocument>(
                        new CultRecordKey("daemon:aetheria.frame.latest.v1")),
                    frame,
                    new CultNetDocumentMessageOptions
                    {
                        SourceRuntimeId = options.DaemonId,
                        SourceRole = "aetheria-daemon"
                    });
                response.Documents = response.Documents
                    .Where(document => !string.Equals(document.SchemaId, framePut.Document.SchemaId, StringComparison.Ordinal) ||
                        !string.Equals(document.RecordKey, framePut.Document.RecordKey, StringComparison.Ordinal))
                    .Concat(new[] { framePut.Document })
                    .ToArray();
            }

            var health = await node.MutableDocument<AetheriaRuntimeDaemonHealthDocument>(AetheriaRuntimeVerseRecordKeys.DaemonHealth).ReadAsync().ConfigureAwait(false);
            if (health != null && SnapshotWants(
                request,
                AetheriaRuntimeDaemonSchemas.Health,
                AetheriaRuntimeVerseRecordKeys.DaemonHealth.ToString()))
            {
                var healthPut = node.Database.Documents.CreateRawDocumentPutMessage(
                    response.MessageId,
                    new CultRecordHandle<AetheriaRuntimeDaemonHealthDocument>(
                        AetheriaRuntimeVerseRecordKeys.DaemonHealth),
                    health,
                    new CultNetDocumentMessageOptions
                    {
                        SourceRuntimeId = options.DaemonId,
                        SourceRole = "aetheria-daemon"
                    });
                response.Documents = response.Documents
                    .Where(document => !string.Equals(document.SchemaId, healthPut.Document.SchemaId, StringComparison.Ordinal) ||
                        !string.Equals(document.RecordKey, healthPut.Document.RecordKey, StringComparison.Ordinal))
                    .Concat(new[] { healthPut.Document })
                    .ToArray();
            }

            var authorityPolicy = await node.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy).ReadAsync().ConfigureAwait(false);
            if (authorityPolicy != null && SnapshotWants(
                request,
                AetheriaRuntimeDaemonSchemas.VerseAuthorityPolicy,
                AetheriaRuntimeVerseAuthorityPolicyDocument.DocumentKey))
            {
                var authorityPut = node.Database.Documents.CreateRawDocumentPutMessage(
                    response.MessageId,
                    new CultRecordHandle<AetheriaRuntimeVerseAuthorityPolicyDocument>(
                        new CultRecordKey(AetheriaRuntimeVerseAuthorityPolicyDocument.DocumentKey)),
                    authorityPolicy,
                    new CultNetDocumentMessageOptions
                    {
                        SourceRuntimeId = options.DaemonId,
                        SourceRole = "aetheria-daemon"
                    });
                response.Documents = response.Documents
                    .Where(document => !string.Equals(document.SchemaId, authorityPut.Document.SchemaId, StringComparison.Ordinal) ||
                        !string.Equals(document.RecordKey, authorityPut.Document.RecordKey, StringComparison.Ordinal))
                    .Concat(new[] { authorityPut.Document })
                    .ToArray();
            }

            var starbridgeScenario = await node.MutableDocument<AetheriaRuntimeStarbridgeScenarioDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest).ReadAsync().ConfigureAwait(false);
            if (starbridgeScenario != null && SnapshotWants(
                request,
                AetheriaRuntimeDaemonSchemas.StarbridgeScenario,
                AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest.ToString()))
            {
                var scenarioPut = node.Database.Documents.CreateRawDocumentPutMessage(
                    response.MessageId,
                    new CultRecordHandle<AetheriaRuntimeStarbridgeScenarioDocument>(
                        AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest),
                    starbridgeScenario,
                    new CultNetDocumentMessageOptions
                    {
                        SourceRuntimeId = options.DaemonId,
                        SourceRole = "aetheria-daemon"
                    });
                response.Documents = response.Documents
                    .Where(document => !string.Equals(document.SchemaId, scenarioPut.Document.SchemaId, StringComparison.Ordinal) ||
                        !string.Equals(document.RecordKey, scenarioPut.Document.RecordKey, StringComparison.Ordinal))
                    .Concat(new[] { scenarioPut.Document })
                    .ToArray();
            }

            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString(),
                "game").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface.ToString(),
                "game-tui").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface.ToString(),
                "editor").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface.ToString(),
                "editor-tui").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.MainMenuSurface.ToString(),
                "main-menu").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.MainMenuSettingsSurface.ToString(),
                "main-menu-settings").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.MainMenuInputSettingsSurface.ToString(),
                "main-menu-input-settings").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.MainMenuPlayerSettingsSurface.ToString(),
                "main-menu-player-settings").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.MainMenuVerseSettingsSurface.ToString(),
                "main-menu-verse-settings").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.InventoryPanelSurface.ToString(),
                "inventory-panel").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.InventoryDropdownSurface.ToString(),
                "inventory-dropdown").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.MapMenuSurface.ToString(),
                "map-menu").ConfigureAwait(false);
            await InjectEveSurfaceSnapshotAsync(
                node,
                options,
                request,
                response,
                AetheriaRuntimeVerseRecordKeys.TradeMenuSurface.ToString(),
                "trade-menu").ConfigureAwait(false);

            var starbridgeSession = await node.MutableDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionSummary).ReadAsync().ConfigureAwait(false);
            if (starbridgeSession != null &&
                SnapshotWants(
                    request,
                    AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary,
                    AetheriaRuntimeVerseRecordKeys.StarbridgeSessionSummary.ToString()))
            {
                var starbridgePut = node.Database.Documents.CreateRawDocumentPutMessage(
                    response.MessageId,
                    new CultRecordHandle<AetheriaRuntimeStarbridgeSessionSummaryDocument>(
                        AetheriaRuntimeVerseRecordKeys.StarbridgeSessionSummary),
                    starbridgeSession,
                    new CultNetDocumentMessageOptions
                    {
                        SourceRuntimeId = options.DaemonId,
                        SourceRole = "aetheria-daemon"
                    });
                response.Documents = response.Documents
                    .Where(document => !string.Equals(document.SchemaId, starbridgePut.Document.SchemaId, StringComparison.Ordinal) ||
                        !string.Equals(document.RecordKey, starbridgePut.Document.RecordKey, StringComparison.Ordinal))
                    .Concat(new[] { starbridgePut.Document })
                    .ToArray();
            }

            var assetManifest = await node.MutableDocument<AetheriaRuntimeAssetManifestDocument>(AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest).ReadAsync().ConfigureAwait(false);
            if (assetManifest != null &&
                SnapshotWants(
                    request,
                    AetheriaRuntimeDaemonSchemas.AssetManifest,
                    AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest.ToString()))
            {
                var assetManifestPut = node.Database.Documents.CreateRawDocumentPutMessage(
                    response.MessageId,
                    new CultRecordHandle<AetheriaRuntimeAssetManifestDocument>(
                        AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest),
                    assetManifest,
                    new CultNetDocumentMessageOptions
                    {
                        SourceRuntimeId = options.DaemonId,
                        SourceRole = "aetheria-daemon"
                    });
                response.Documents = response.Documents
                    .Where(document => !string.Equals(document.SchemaId, assetManifestPut.Document.SchemaId, StringComparison.Ordinal) ||
                        !string.Equals(document.RecordKey, assetManifestPut.Document.RecordKey, StringComparison.Ordinal))
                    .Concat(new[] { assetManifestPut.Document })
                    .ToArray();
            }

            var factPuts = node.Documents<AetheriaRuntimeCommittedCommandFactDocument>()
                .OrderBy(fact => fact.CommittedAtUtc ?? "", StringComparer.Ordinal)
                .ThenBy(fact => fact.FactId ?? "", StringComparer.Ordinal)
                .Select(fact => new
                {
                    Fact = fact,
                    RecordKey = AetheriaRuntimeCommittedCommandFactDocument.CreateRecordKey(fact.FactId)
                })
                .Where(item => SnapshotWants(
                    request,
                    AetheriaRuntimeDaemonSchemas.CommittedCommandFact,
                    item.RecordKey))
                .Select(item => node.Database.Documents.CreateRawDocumentPutMessage(
                    response.MessageId,
                    new CultRecordHandle<AetheriaRuntimeCommittedCommandFactDocument>(
                        new CultRecordKey(item.RecordKey)),
                    item.Fact,
                    new CultNetDocumentMessageOptions
                    {
                        SourceRuntimeId = options.DaemonId,
                        SourceRole = "aetheria-daemon"
                    }).Document)
                .ToArray();
            if (factPuts.Length > 0)
            {
                var factKeys = factPuts
                    .Select(document => document.RecordKey)
                    .ToHashSet(StringComparer.Ordinal);
                response.Documents = response.Documents
                    .Where(document => !string.Equals(document.SchemaId, AetheriaRuntimeDaemonSchemas.CommittedCommandFact, StringComparison.Ordinal) ||
                        !factKeys.Contains(document.RecordKey))
                    .Concat(factPuts)
                    .ToArray();
            }

            if (hasFrame && frame != null && TryGetGameViewportRequest(request, out var viewportRecordKey, out var viewportSchemaId, out var viewport))
            {
                if (string.Equals(viewportSchemaId, AetheriaRuntimeDaemonSchemas.ObjectsViewport, StringComparison.Ordinal))
                {
                    var viewportPut = node.Database.Documents.CreateRawDocumentPutMessage(
                        response.MessageId,
                        new CultRecordHandle<AetheriaRuntimeObjectsViewportDocument>(
                            new CultRecordKey(viewportRecordKey)),
                        AetheriaRuntimeGameDocuments.ObjectsViewport(frame, viewport),
                        new CultNetDocumentMessageOptions
                        {
                            SourceRuntimeId = options.DaemonId,
                            SourceRole = "aetheria-daemon"
                        });
                    response.Documents = response.Documents
                        .Where(document => !string.Equals(document.SchemaId, viewportPut.Document.SchemaId, StringComparison.Ordinal) ||
                            !string.Equals(document.RecordKey, viewportPut.Document.RecordKey, StringComparison.Ordinal))
                        .Concat(new[] { viewportPut.Document })
                        .ToArray();
                }
                else if (string.Equals(viewportSchemaId, AetheriaRuntimeDaemonSchemas.GravityViewport, StringComparison.Ordinal))
                {
                    var viewportPut = node.Database.Documents.CreateRawDocumentPutMessage(
                        response.MessageId,
                        new CultRecordHandle<AetheriaRuntimeGravityViewportDocument>(
                            new CultRecordKey(viewportRecordKey)),
                        AetheriaRuntimeGameDocuments.GravityViewport(frame, viewport),
                        new CultNetDocumentMessageOptions
                        {
                            SourceRuntimeId = options.DaemonId,
                            SourceRole = "aetheria-daemon"
                        });
                    response.Documents = response.Documents
                        .Where(document => !string.Equals(document.SchemaId, viewportPut.Document.SchemaId, StringComparison.Ordinal) ||
                            !string.Equals(document.RecordKey, viewportPut.Document.RecordKey, StringComparison.Ordinal))
                        .Concat(new[] { viewportPut.Document })
                        .ToArray();
                }
                else if (string.Equals(viewportSchemaId, AetheriaRuntimeDaemonSchemas.RenderSplatsViewport, StringComparison.Ordinal))
                {
                    var viewportPut = node.Database.Documents.CreateRawDocumentPutMessage(
                        response.MessageId,
                        new CultRecordHandle<AetheriaRuntimeRenderSplatsViewportDocument>(
                            new CultRecordKey(viewportRecordKey)),
                        AetheriaRuntimeGameDocuments.RenderSplatsViewport(frame, viewport),
                        new CultNetDocumentMessageOptions
                        {
                            SourceRuntimeId = options.DaemonId,
                            SourceRole = "aetheria-daemon"
                        });
                    response.Documents = response.Documents
                        .Where(document => !string.Equals(document.SchemaId, viewportPut.Document.SchemaId, StringComparison.Ordinal) ||
                            !string.Equals(document.RecordKey, viewportPut.Document.RecordKey, StringComparison.Ordinal))
                        .Concat(new[] { viewportPut.Document })
                        .ToArray();
                }
                else
                {
                    var viewportPut = node.Database.Documents.CreateRawDocumentPutMessage(
                        response.MessageId,
                        new CultRecordHandle<AetheriaRuntimeGameViewportDocument>(
                            new CultRecordKey(viewportRecordKey)),
                        AetheriaRuntimeGameDocuments.Viewport(frame, viewport),
                        new CultNetDocumentMessageOptions
                        {
                            SourceRuntimeId = options.DaemonId,
                            SourceRole = "aetheria-daemon"
                        });
                    response.Documents = response.Documents
                        .Where(document => !string.Equals(document.SchemaId, viewportPut.Document.SchemaId, StringComparison.Ordinal) ||
                            !string.Equals(document.RecordKey, viewportPut.Document.RecordKey, StringComparison.Ordinal))
                        .Concat(new[] { viewportPut.Document })
                        .ToArray();
                }
            }

            if (hasFrame && frame != null && TryGetIndexedGameDocumentRequest(request, out var indexedRecordKey, out var indexedSchemaId, out var indexedEntityIndex))
            {
                if (string.Equals(indexedSchemaId, AetheriaRuntimeDaemonSchemas.SelectedObject, StringComparison.Ordinal))
                {
                    var selectedObjectPut = node.Database.Documents.CreateRawDocumentPutMessage(
                        response.MessageId,
                        new CultRecordHandle<AetheriaRuntimeSelectedObjectDocument>(
                            new CultRecordKey(indexedRecordKey)),
                        AetheriaRuntimeGameDocuments.SelectedObject(frame, indexedEntityIndex),
                        new CultNetDocumentMessageOptions
                        {
                            SourceRuntimeId = options.DaemonId,
                            SourceRole = "aetheria-daemon"
                        });
                    response.Documents = response.Documents
                        .Where(document => !string.Equals(document.SchemaId, selectedObjectPut.Document.SchemaId, StringComparison.Ordinal) ||
                            !string.Equals(document.RecordKey, selectedObjectPut.Document.RecordKey, StringComparison.Ordinal))
                        .Concat(new[] { selectedObjectPut.Document })
                        .ToArray();
                }
                else if (string.Equals(indexedSchemaId, AetheriaRuntimeDaemonSchemas.Inventory, StringComparison.Ordinal))
                {
                    var inventoryPut = node.Database.Documents.CreateRawDocumentPutMessage(
                        response.MessageId,
                        new CultRecordHandle<AetheriaRuntimeInventoryDocument>(
                            new CultRecordKey(indexedRecordKey)),
                        AetheriaRuntimeGameDocuments.Inventory(frame, indexedEntityIndex),
                        new CultNetDocumentMessageOptions
                        {
                            SourceRuntimeId = options.DaemonId,
                            SourceRole = "aetheria-daemon"
                        });
                    response.Documents = response.Documents
                        .Where(document => !string.Equals(document.SchemaId, inventoryPut.Document.SchemaId, StringComparison.Ordinal) ||
                            !string.Equals(document.RecordKey, inventoryPut.Document.RecordKey, StringComparison.Ordinal))
                        .Concat(new[] { inventoryPut.Document })
                        .ToArray();
                }
            }

            await InjectCultMeshCdnAssetSnapshotsAsync(options, bundleCdnDocuments, request, response).ConfigureAwait(false);
            peer.SendCultNet(response);
        }
        catch (Exception ex)
        {
            peer.SendCultNet(new CultNetErrorMessage
            {
                Error = $"Aetheria snapshot response failed: {ex.GetType().Name}: {ex.Message}"
            });
        }
    });
    server.OnCultNet<CultNetDocumentPutRawMessage>(async (message, _) =>
    {
        if (string.Equals(message.Document.SchemaId, AetheriaRuntimeDaemonSchemas.Command, StringComparison.Ordinal))
        {
            var command = MessagePackSerializer.Deserialize<AetheriaRuntimeDaemonCommandDocument>(
                message.Document.Payload);
            await node.SubmitDaemonCommandAsync(command).ConfigureAwait(false);
            return;
        }

        await node.Database.ApplyPutAsync(message).ConfigureAwait(false);
    });
    return server;
}

static async Task InjectCultMeshCdnAssetSnapshotsAsync(
    AetheriaDaemonHostOptions options,
    IReadOnlyDictionary<string, CultNetRawDocumentRecord> bundleCdnDocuments,
    CultNetSnapshotRequestMessage request,
    CultNetSnapshotResponseRawMessage response)
{
    var schemaIds = request.SchemaIds ?? Array.Empty<string>();

    var recordKeys = request.RecordKeys ?? Array.Empty<string>();
    if (recordKeys.Length == 0)
        return;

    var documents = new List<CultNetRawDocumentRecord>();
    foreach (var recordKey in recordKeys.Distinct(StringComparer.Ordinal))
    {
        if (bundleCdnDocuments.TryGetValue(recordKey, out var bundleDocument))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("AETHERIA_TRACE_EVE_SNAPSHOTS"), "1", StringComparison.Ordinal))
                Console.WriteLine($"Eve CDN snapshot schema={bundleDocument.SchemaId} record={recordKey}");
            documents.Add(bundleDocument);
            continue;
        }
        if (schemaIds.Length > 0 &&
            !schemaIds.Contains(AetheriaRuntimeDaemonSchemas.CultMeshCdnAssetBlob, StringComparer.Ordinal))
            continue;
        if (!TryResolveCultMeshCdnAssetPath(options, recordKey, out var filePath, out var mimeType, out var canonicalUri))
            continue;

        var bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
        documents.Add(new CultNetRawDocumentRecord
        {
            SchemaId = AetheriaRuntimeDaemonSchemas.CultMeshCdnAssetBlob,
            RecordKey = recordKey,
            StoredAt = DateTimeOffset.UtcNow.ToString("O"),
            PayloadEncoding = "messagepack",
            Payload = MessagePackSerializer.Serialize(bytes),
            SourceRuntimeId = options.DaemonId,
            SourceRole = "aetheria-cultmesh-cdn",
            Tags =
            [
                "aetheria",
                "cultmesh-cdn",
                "asset",
                $"mime:{mimeType}",
                $"canonical:{canonicalUri}"
            ]
        });
    }

    if (documents.Count == 0)
        return;

    var keys = documents.Select(document => document.RecordKey).ToHashSet(StringComparer.Ordinal);
    response.Documents = response.Documents
        .Where(document => !keys.Contains(document.RecordKey))
        .Concat(documents)
        .ToArray();
}

static IReadOnlyDictionary<string, CultNetRawDocumentRecord> BuildBundleCdnDocuments(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options)
{
    var documents = new Dictionary<string, CultNetRawDocumentRecord>(StringComparer.Ordinal);
    foreach (var bundle in FindAssetBundles(options))
    {
        var artifact = PackAssetBundle(bundle.Path, bundle.Platform);
        Add(artifact.ManifestKey, artifact.Manifest);
        foreach (var chunk in artifact.Chunks)
            Add(CultMeshCdnArtifactChunk.CreateRecordKey(chunk), chunk);
    }
    return documents;

    void Add<T>(CultRecordKey recordKey, T document) where T : class
    {
        var put = node.Database.Documents.CreateRawDocumentPutMessage(
            $"aetheria-cdn:{recordKey.Value}",
            new CultRecordHandle<T>(recordKey),
            document,
            new CultNetDocumentMessageOptions
            {
                SourceRuntimeId = options.DaemonId,
                SourceRole = "aetheria-cultmesh-cdn",
                Tags = ["aetheria", "cultmesh-cdn", "asset-bundle"]
            });
        documents[recordKey.Value] = put.Document;
    }
}

static bool TryResolveCultMeshCdnAssetPath(
    AetheriaDaemonHostOptions options,
    string recordKey,
    out string filePath,
    out string mimeType,
    out string canonicalUri)
{
    filePath = "";
    mimeType = "application/octet-stream";
    canonicalUri = "";

    var assetPath = ParseCultMeshAssetPath(recordKey);
    if (string.IsNullOrWhiteSpace(assetPath))
        return false;

    var relative = ResolveCultMeshAssetResourcePath(assetPath);
    if (string.IsNullOrWhiteSpace(relative))
        return false;

    var candidates = new[]
    {
        Path.Combine(options.AetheriaResourcesRoot, relative),
        Path.Combine(options.AetheriaResourcesRoot, relative + ".png"),
        Path.Combine(options.AetheriaResourcesRoot, relative + ".PNG"),
        Path.Combine(options.AetheriaResourcesRoot, relative + ".jpg"),
        Path.Combine(options.AetheriaResourcesRoot, relative + ".jpeg"),
        Path.Combine(options.AetheriaResourcesRoot, relative + ".psd")
    };
    filePath = candidates.FirstOrDefault(path =>
        File.Exists(path) &&
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(options.AetheriaResourcesRoot), StringComparison.OrdinalIgnoreCase)) ?? "";
    if (string.IsNullOrWhiteSpace(filePath))
        return false;

    mimeType = ContentTypeForPath(filePath);
    canonicalUri = recordKey.StartsWith("cultmesh://", StringComparison.OrdinalIgnoreCase)
        ? recordKey
        : $"cultmesh://aetheria/assets/{assetPath.Trim('/')}";
    return true;
}

static string ParseCultMeshAssetPath(string recordKey)
{
    var text = (recordKey ?? "").Trim();
    if (text.Length == 0)
        return "";

    if (!text.StartsWith("cultmesh://", StringComparison.OrdinalIgnoreCase))
        return text.Trim('/');

    if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        return "";

    var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    var assetIndex = Array.FindIndex(parts, part => string.Equals(part, "assets", StringComparison.OrdinalIgnoreCase));
    return string.Join("/", assetIndex >= 0 ? parts.Skip(assetIndex + 1) : parts);
}

static string ResolveCultMeshAssetResourcePath(string assetPath)
{
    var path = (assetPath ?? "").Trim('/').Replace('\\', '/');
    var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["icons/ui/sun"] = "Sprites/Icons/Stroked/Sun",
        ["icons/ui/star"] = "Sprites/Icons/Stroked/Sun",
        ["icons/ui/planet"] = "Sprites/Icons/Stroked/Planet",
        ["icons/ui/gasgiant"] = "Sprites/Icons/Stroked/gasgiant",
        ["icons/ui/asteroid"] = "Sprites/Icons/Stroked/Planet",
        ["icons/ui/ship"] = "Sprites/Icons/Stroked/Ship",
        ["icons/ui/player"] = "Sprites/Icons/Stroked/Ship",
        ["icons/ui/station"] = "Sprites/Icons/station1",
        ["icons/ui/orbital"] = "Sprites/Icons/Stroked/orbital",
        ["map/entity/player"] = "Sprites/Icons/Stroked/Ship",
        ["map/entity/ship"] = "Sprites/Icons/Stroked/Ship",
        ["map/entity/orbital"] = "Sprites/Icons/Stroked/orbital",
        ["map/entity/station"] = "Sprites/Icons/station1",
        ["map/entity/projectile"] = "Sprites/Icons/Lightning Bolt",
        ["map/body/planet"] = "Sprites/Icons/Stroked/Planet",
        ["map/body/sun"] = "Sprites/Icons/Stroked/Sun",
        ["map/body/asteroid"] = "Sprites/Icons/Stroked/Planet",
        ["textures/tint_splat"] = "Sprites/Flat UI/areaFade2",
        ["textures/perlines-nebula"] = "Sprites/Icons/Tech/Cloud",
        ["inventory/cell/background_atlas"] = "Sprites/Flat UI/Nodes/Nodes8BG",
        ["inventory/cell/foreground_atlas"] = "Sprites/Flat UI/Nodes/Nodes8",
        ["inventory/cell/thermal_layer_atlas"] = "Sprites/Flat UI/pipes"
    };
    if (aliases.TryGetValue(path, out var relative))
        return relative;

    if (path.StartsWith("icons/star.", StringComparison.OrdinalIgnoreCase))
        return "Sprites/Icons/Stroked/Sun";
    if (path.StartsWith("icons/body.", StringComparison.OrdinalIgnoreCase))
        return path.Contains("vesper", StringComparison.OrdinalIgnoreCase)
            ? "Sprites/Icons/Stroked/gasgiant"
            : "Sprites/Icons/Stroked/Planet";
    if (path.StartsWith("icons/ship.", StringComparison.OrdinalIgnoreCase))
        return "Sprites/Icons/Stroked/Ship";
    if (path.StartsWith("icons/station.", StringComparison.OrdinalIgnoreCase))
        return "Sprites/Icons/station1";

    return path;
}

static string ContentTypeForPath(string filePath)
{
    return Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".svg" => "image/svg+xml",
        ".psd" => "image/vnd.adobe.photoshop",
        _ => "application/octet-stream"
    };
}

static async Task InjectEveSurfaceSnapshotAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    CultNetSnapshotRequestMessage request,
    CultNetSnapshotResponseRawMessage response,
    string recordKey,
    string surfaceKind)
{
    const string EveSurfaceSchema = "gamecult.eve.surface.v1";
    var trace = string.Equals(Environment.GetEnvironmentVariable("AETHERIA_TRACE_EVE_SNAPSHOTS"), "1", StringComparison.Ordinal);
    if (!SnapshotWants(request, EveSurfaceSchema, recordKey))
        return;

    var surfaceState = await ReadEveSurfacePublicationAsync(node, surfaceKind).ConfigureAwait(false);
    if (trace)
    {
        Console.WriteLine(
            $"Eve snapshot request kind={surfaceKind} schemas=[{string.Join(",", request.SchemaIds ?? Array.Empty<string>())}] " +
            $"records=[{string.Join(",", request.RecordKeys ?? Array.Empty<string>())}] surface={(surfaceState == null ? "missing" : surfaceState.Surface.Id)} " +
            $"playerPosition={(surfaceState == null ? "missing" : FindControllablePosition(surfaceState.Surface.Root))}");
    }
    if (surfaceState == null)
        return;

    var surfacePut = node.Database.Documents.CreateRawDocumentPutMessage(
        response.MessageId,
        new CultRecordHandle<EveSurfaceDocument>(new CultRecordKey(recordKey)),
        surfaceState,
        new CultNetDocumentMessageOptions
        {
            SourceRuntimeId = options.DaemonId,
            SourceRole = "aetheria-daemon"
        });
    response.Documents = response.Documents
        .Where(document => !string.Equals(document.RecordKey, surfacePut.Document.RecordKey, StringComparison.Ordinal))
        .Concat(new[] { surfacePut.Document })
        .ToArray();
    if (trace)
        Console.WriteLine($"Eve snapshot response schema={surfacePut.Document.SchemaId} record={surfacePut.Document.RecordKey}");
}

static string FindControllablePosition(EveSurfaceComponent component)
{
    if (component.Props.TryGetValue("controllable", out var controllable) &&
        string.Equals(controllable, "true", StringComparison.OrdinalIgnoreCase))
        return component.GetProp("position", "missing");
    foreach (var child in component.Children)
    {
        var position = FindControllablePosition(child);
        if (!string.Equals(position, "missing", StringComparison.Ordinal))
            return position;
    }
    return "missing";
}

static Task<EveSurfaceDocument?> ReadEveSurfacePublicationAsync(AetheriaStateNode node, string surfaceKind)
{
    var key = surfaceKind switch
    {
        "game" => AetheriaRuntimeVerseRecordKeys.DaemonGameSurface,
        "game-tui" => AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface,
        "editor" => AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface,
        "editor-tui" => AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface,
        "main-menu" => AetheriaRuntimeVerseRecordKeys.MainMenuSurface,
        "main-menu-settings" => AetheriaRuntimeVerseRecordKeys.MainMenuSettingsSurface,
        "main-menu-input-settings" => AetheriaRuntimeVerseRecordKeys.MainMenuInputSettingsSurface,
        "main-menu-player-settings" => AetheriaRuntimeVerseRecordKeys.MainMenuPlayerSettingsSurface,
        "main-menu-verse-settings" => AetheriaRuntimeVerseRecordKeys.MainMenuVerseSettingsSurface,
        "inventory-panel" => AetheriaRuntimeVerseRecordKeys.InventoryPanelSurface,
        "inventory-dropdown" => AetheriaRuntimeVerseRecordKeys.InventoryDropdownSurface,
        "map-menu" => AetheriaRuntimeVerseRecordKeys.MapMenuSurface,
        "trade-menu" => AetheriaRuntimeVerseRecordKeys.TradeMenuSurface,
        _ => throw new ArgumentOutOfRangeException(nameof(surfaceKind), surfaceKind, "Unknown Eve surface publication.")
    };
    return ReadPortableSurfaceAsync(node, key);
}

static async Task<EveSurfaceDocument?> ReadPortableSurfaceAsync(AetheriaStateNode node, CultRecordKey key)
{
    var portableSurface = await node.MutableDocument<EveSurfaceDocument>(key).ReadAsync().ConfigureAwait(false);
    if (portableSurface != null)
        return portableSurface;
    var providerSurface = await node.MutableDocument<AetheriaRuntimeSurfaceDocument>(key).ReadAsync().ConfigureAwait(false);
    return providerSurface == null ? null : AetheriaRuntimeSurfaceDocuments.ToEveSurfaceDocument(providerSurface);
}

static async Task RunClientCultMeshPumpAsync(
    RudpCultNetSchemaServer server,
    CancellationToken cancellationToken)
{
    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delivered = false;
            for (var i = 0; i < 64; i++)
            {
                delivered |= await server.PollOnceAsync().ConfigureAwait(false);
            }

            server.PollResends();
            if (!delivered)
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
}

static IPAddress ParseBindAddress(string host)
{
    if (string.IsNullOrWhiteSpace(host) ||
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return IPAddress.Loopback;
    }

    if (string.Equals(host, "*", StringComparison.Ordinal) ||
        string.Equals(host, "0.0.0.0", StringComparison.Ordinal) ||
        string.Equals(host, "any", StringComparison.OrdinalIgnoreCase))
    {
        return IPAddress.Any;
    }

    return IPAddress.Parse(host);
}

static bool SnapshotWants(
    CultNetSnapshotRequestMessage request,
    string schemaId,
    string recordKey)
{
    var schemaIds = request.SchemaIds ?? Array.Empty<string>();
    var recordKeys = request.RecordKeys ?? Array.Empty<string>();
    var schemaMatches = schemaIds.Length == 0 || schemaIds.Contains(schemaId, StringComparer.Ordinal);
    var recordMatches = recordKeys.Length == 0 || recordKeys.Contains(recordKey, StringComparer.Ordinal);
    return schemaMatches && recordMatches;
}

static bool TryGetGameViewportRequest(
    CultNetSnapshotRequestMessage request,
    out string recordKey,
    out string schemaId,
    out AetheriaRuntimeViewportBounds viewport)
{
    const string prefix = "daemon:aetheria.game.viewport.v1;";
    recordKey = "";
    schemaId = "";
    viewport = new AetheriaRuntimeViewportBounds();

    var schemaIds = request.SchemaIds ?? Array.Empty<string>();
    var allowedSchemas = new[]
    {
        AetheriaRuntimeDaemonSchemas.GameViewport,
        AetheriaRuntimeDaemonSchemas.ObjectsViewport,
        AetheriaRuntimeDaemonSchemas.GravityViewport,
        AetheriaRuntimeDaemonSchemas.RenderSplatsViewport
    };
    if (schemaIds.Length > 0 && !schemaIds.Any(candidate => allowedSchemas.Contains(candidate, StringComparer.Ordinal)))
        return false;

    foreach (var candidate in request.RecordKeys ?? Array.Empty<string>())
    {
        if (candidate.StartsWith(prefix, StringComparison.Ordinal))
        {
            var values = candidate
                .Substring(prefix.Length)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            if (!TryGetDouble(values, "minX", out var minX) ||
                !TryGetDouble(values, "minY", out var minY) ||
                !TryGetDouble(values, "maxX", out var maxX) ||
                !TryGetDouble(values, "maxY", out var maxY))
            {
                continue;
            }

            recordKey = candidate;
            schemaId = schemaIds.FirstOrDefault(known => allowedSchemas.Contains(known, StringComparer.Ordinal))
                ?? AetheriaRuntimeDaemonSchemas.GameViewport;
            viewport = new AetheriaRuntimeViewportBounds
            {
                MinX = Math.Min(minX, maxX),
                MinY = Math.Min(minY, maxY),
                MaxX = Math.Max(minX, maxX),
                MaxY = Math.Max(minY, maxY)
            };
            return true;
        }

        if (TryGetManagedViewportRequest(candidate, out var managedSchemaId, out viewport))
        {
            if (schemaIds.Length > 0 && !schemaIds.Contains(managedSchemaId, StringComparer.Ordinal))
                continue;

            recordKey = candidate;
            schemaId = managedSchemaId;
            return true;
        }
    }

    return false;
}

static bool TryGetManagedViewportRequest(
    string recordKey,
    out string schemaId,
    out AetheriaRuntimeViewportBounds viewport)
{
    schemaId = "";
    viewport = new AetheriaRuntimeViewportBounds();
    var prefix = "";
    if (recordKey.StartsWith("aetheria.viewport.map.", StringComparison.Ordinal))
    {
        prefix = "aetheria.viewport.map.";
        schemaId = AetheriaRuntimeDaemonSchemas.GameViewport;
    }
    else if (recordKey.StartsWith("aetheria.viewport.objects.", StringComparison.Ordinal))
    {
        prefix = "aetheria.viewport.objects.";
        schemaId = AetheriaRuntimeDaemonSchemas.ObjectsViewport;
    }
    else if (recordKey.StartsWith("aetheria.viewport.gravity.", StringComparison.Ordinal))
    {
        prefix = "aetheria.viewport.gravity.";
        schemaId = AetheriaRuntimeDaemonSchemas.GravityViewport;
    }
    else if (recordKey.StartsWith("aetheria.viewport.render_splats.", StringComparison.Ordinal))
    {
        prefix = "aetheria.viewport.render_splats.";
        schemaId = AetheriaRuntimeDaemonSchemas.RenderSplatsViewport;
    }
    else
    {
        return false;
    }

    var parts = recordKey.Substring(prefix.Length).Split('.');
    if (parts.Length != 4 ||
        !TryParseViewportToken(parts[0], out var minX) ||
        !TryParseViewportToken(parts[1], out var minY) ||
        !TryParseViewportToken(parts[2], out var maxX) ||
        !TryParseViewportToken(parts[3], out var maxY))
    {
        return false;
    }

    viewport = new AetheriaRuntimeViewportBounds
    {
        MinX = Math.Min(minX, maxX),
        MinY = Math.Min(minY, maxY),
        MaxX = Math.Max(minX, maxX),
        MaxY = Math.Max(minY, maxY)
    };
    return true;
}

static bool TryGetIndexedGameDocumentRequest(
    CultNetSnapshotRequestMessage request,
    out string recordKey,
    out string schemaId,
    out int entityIndex)
{
    recordKey = "";
    schemaId = "";
    entityIndex = -1;
    var schemaIds = request.SchemaIds ?? Array.Empty<string>();
    var allowedSchemas = new[]
    {
        AetheriaRuntimeDaemonSchemas.SelectedObject,
        AetheriaRuntimeDaemonSchemas.Inventory
    };
    if (schemaIds.Length > 0 && !schemaIds.Any(candidate => allowedSchemas.Contains(candidate, StringComparer.Ordinal)))
        return false;

    foreach (var candidate in request.RecordKeys ?? Array.Empty<string>())
    {
        var candidateSchemaId = "";
        var prefix = "";
        if (candidate.StartsWith("aetheria.object.selected.", StringComparison.Ordinal))
        {
            prefix = "aetheria.object.selected.";
            candidateSchemaId = AetheriaRuntimeDaemonSchemas.SelectedObject;
        }
        else if (candidate.StartsWith("aetheria.inventory.", StringComparison.Ordinal))
        {
            prefix = "aetheria.inventory.";
            candidateSchemaId = AetheriaRuntimeDaemonSchemas.Inventory;
        }
        else
        {
            continue;
        }

        if (schemaIds.Length > 0 && !schemaIds.Contains(candidateSchemaId, StringComparer.Ordinal))
            continue;
        if (!int.TryParse(candidate.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedEntityIndex))
            continue;

        recordKey = candidate;
        schemaId = candidateSchemaId;
        entityIndex = parsedEntityIndex;
        return true;
    }

    return false;
}

static bool TryParseViewportToken(string token, out double value)
{
    return double.TryParse(
        (token ?? "").Replace('n', '-').Replace('p', '.'),
        NumberStyles.Float,
        CultureInfo.InvariantCulture,
        out value);
}

static bool TryGetDouble(
    IReadOnlyDictionary<string, string> values,
    string key,
    out double value)
{
    value = 0;
    return values.TryGetValue(key, out var text) &&
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}

static async Task PublishDaemonApiDocumentsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    AetheriaRuntimeDaemonTickResult result)
{
    await node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
        .ReplaceAsync(result.Frame)
        .ConfigureAwait(false);
    if (result.SoaView != null &&
        string.Equals(result.SoaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
    {
        await node.MutableDocument<AetheriaRuntimeDaemonSoaViewDocument>(AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest)
            .ReplaceAsync(result.SoaView)
            .ConfigureAwait(false);
    }

    if (result.ProviderAdvertisement != null)
        await node.MutableDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement)
            .ReplaceAsync(result.ProviderAdvertisement)
            .ConfigureAwait(false);
    await node.MutableDocument<EveProviderAdvertisementDocument>(AetheriaRuntimeVerseRecordKeys.EveProviderAdvertisement)
        .ReplaceAsync(BuildCoreProviderAdvertisement(options, result.Frame.PublishedAtUtc))
        .ConfigureAwait(false);
    if (result.Health != null)
        await node.MutableDocument<AetheriaRuntimeDaemonHealthDocument>(AetheriaRuntimeVerseRecordKeys.DaemonHealth)
            .ReplaceAsync(result.Health)
            .ConfigureAwait(false);
    if (result.CommandBoundary != null)
        await node.MutableDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary)
            .ReplaceAsync(result.CommandBoundary)
            .ConfigureAwait(false);
    if (result.AssetManifest != null)
    {
        await node.MutableDocument<AetheriaRuntimeAssetManifestDocument>(AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest)
            .ReplaceAsync(result.AssetManifest)
            .ConfigureAwait(false);
        await node.MutableDocument<EveAssetCatalogDocument>(AetheriaRuntimeVerseRecordKeys.EveAssetCatalog)
            .ReplaceAsync(BuildCoreAssetCatalog(options, result.AssetManifest))
            .ConfigureAwait(false);
    }
    if (result.StarbridgeSessionSummary != null)
        await node.MutableDocument<AetheriaRuntimeStarbridgeSessionSummaryDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionSummary)
            .ReplaceAsync(result.StarbridgeSessionSummary)
            .ConfigureAwait(false);
    await node.MutableDocument<AetheriaRuntimeInputCapabilityDocument>(AetheriaRuntimeVerseRecordKeys.PilotInputCapability)
        .ReplaceAsync(AetheriaRuntimeInputCapabilityDocument.FromFrame(result.Frame))
        .ConfigureAwait(false);
    var mainMenuState = await node.MutableDocument<AetheriaMainMenuState>(AetheriaStateNode.MainMenuStateKey)
        .ReadAsync()
        .ConfigureAwait(false);
    var activeMainMenuSurfaceId = string.IsNullOrWhiteSpace(mainMenuState?.ActiveSurfaceId)
        ? AetheriaRuntimeMainMenuCommands.RootSurfaceId
        : mainMenuState.ActiveSurfaceId;
    var gameSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
        result.Frame,
        result.Health ?? new AetheriaRuntimeDaemonHealthDocument(),
        result.CommandBoundary ?? AetheriaRuntimeDaemonCommandBoundaryDocument.Create(options.DaemonId),
        activeMainMenuSurfaceId);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(gameSurface))
        .ConfigureAwait(false);
    var commanderSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.BuildCommander(
        result.Frame,
        result.Health ?? new AetheriaRuntimeDaemonHealthDocument(),
        result.CommandBoundary ?? AetheriaRuntimeDaemonCommandBoundaryDocument.Create(options.DaemonId),
        result.StarbridgeSessionSummary);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeCommanderSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(commanderSurface))
        .ConfigureAwait(false);
    if (result.GameTuiSurface != null)
        await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface)
            .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(result.GameTuiSurface))
            .ConfigureAwait(false);
    if (result.EditorSurface != null)
        await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface)
            .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(result.EditorSurface))
            .ConfigureAwait(false);
    if (result.EditorTuiSurface != null)
        await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface)
            .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(result.EditorTuiSurface))
            .ConfigureAwait(false);

    await PublishDaemonMenuSurfacesAsync(node, options, result.Frame).ConfigureAwait(false);
}

static EveProviderAdvertisementDocument BuildCoreProviderAdvertisement(
    AetheriaDaemonHostOptions options,
    string updatedAtUtc)
{
    var interaction = new EveWorldInteractionAdvertisement(
        "provider-authored-world-surface",
        new[] { AetheriaRuntimeDaemonSchemas.Frame },
        "aetheria.daemon.commands",
        AetheriaRuntimeVerseRecordKeys.EveCommandRecordPrefix,
        EveCommandReceiptDocument.SchemaId,
        AetheriaRuntimeVerseRecordKeys.EveReceiptRecordPrefix,
        AetheriaRuntimeVerseRecordKeys.EveAssetCatalog.ToString(),
        new[] { "unity-scene", "web-reference", "electron-shell", "tui" },
        "provider-owns-world-state-command-acceptance-and-receipts");
    return new EveProviderAdvertisementDocument(
        "aetheria.daemon",
        options.DaemonId,
        options.VerseId,
        "Aetheria Daemon",
        "game.daemon",
        options.CultMeshAddress,
        updatedAtUtc,
        new GameCult.Eve.Surface.EveProviderFreshness("fresh", updatedAtUtc, 5000),
        new[]
        {
            EveSurfaceDocument.SchemaId,
            EveSurfaceCommandRequest.SchemaId,
            EveCommandReceiptDocument.SchemaId,
            EveAssetCatalogDocument.SchemaId
        },
        Array.Empty<GameCult.Eve.Surface.EveProviderWitness>(),
        new[]
        {
            new EveAdvertisedSurface(
                AetheriaRuntimeDaemonGameSurfaceBuilder.PilotSurfaceId,
                EveSurfaceDocument.SchemaId,
                AetheriaRuntimeVerseRecordKeys.DaemonGameSurface.ToString(),
                "cultmesh-record",
                "active",
                "interactive-world",
                interaction),
            new EveAdvertisedSurface(
                AetheriaRuntimeDaemonGameSurfaceBuilder.CommanderSurfaceId,
                EveSurfaceDocument.SchemaId,
                AetheriaRuntimeVerseRecordKeys.StarbridgeCommanderSurface.ToString(),
                "cultmesh-record",
                "active",
                "interactive-world",
                interaction)
        },
        Array.Empty<EveAdvertisedCommand>());
}

static async Task PublishDaemonMenuSurfacesAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    AetheriaRuntimeDaemonFrameDocument frame)
{
    if (frame == null)
        return;

    var updatedAtUtc = string.IsNullOrWhiteSpace(frame.PublishedAtUtc)
        ? DateTimeOffset.UtcNow.ToString("O")
        : frame.PublishedAtUtc;
    var catalog = AetheriaRuntimeCatalogStore.OpenReadOnly(node.StatePath);
    var assetManifest = AetheriaRuntimeAssets.ProjectManifest(
        catalog,
        frame.Run?.RunId ?? frame.SessionId,
        "cultmesh://aetheria/assets");
    var loadoutTemplates = AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(node.StatePath);
    var playerSettings = await ReadRuntimePlayerSettingsAsync(node).ConfigureAwait(false);
    var currentEntity = AetheriaRuntimeGameDocuments.CurrentEntity(frame);
    var stationRefit = AetheriaRuntimeGameDocuments.StationRefit(frame, loadoutTemplates, catalog);
    var dropdownRequest = new AetheriaRuntimeInventoryDropdownSurfaceRequest
    {
        CurrentView = "Current Entity",
        DisplayedEntityKey = currentEntity.EntityKey,
        DisplayedCargoEntityKey = currentEntity.EntityKey,
        DisplayedCargoIndex = 0,
        CanSaveLoadout = !string.IsNullOrWhiteSpace(currentEntity.EntityKey)
    };
    var inventoryRequest = new AetheriaRuntimeInventoryPanelSurfaceRequest
    {
        ViewTitle = string.IsNullOrWhiteSpace(currentEntity.Entity?.DisplayName)
            ? "Current Entity Inventory"
            : currentEntity.Entity.DisplayName,
        DisplayedEntityKey = currentEntity.EntityKey,
        DisplayedEntityIndex = currentEntity.EntityIndex,
        DisplayedCargoEntityKey = currentEntity.EntityKey,
        DisplayedCargoEntityIndex = currentEntity.EntityIndex,
        DisplayedCargoIndex = 0
    };
    var inventory = currentEntity.EntityIndex < 0
        ? new AetheriaRuntimeInventoryDocument()
        : AetheriaRuntimeGameDocuments.Inventory(frame, currentEntity.EntityIndex);

    var verseHost = await EnsureVerseHostSettingsAsync(node, options, updatedAtUtc)
        .ConfigureAwait(false);
    var stateBoot = AetheriaRuntimeStateBoot.Inspect(
        new DirectoryInfo(Path.GetDirectoryName(node.StatePath) ?? "."),
        node.StatePath);
    var mainMenu = AetheriaRuntimeMainMenuSurfaceBuilder.BuildRoot(
        stateBoot,
        frame,
        AetheriaRuntimeVerseHostSettingsDocument.FromSnapshot(new AetheriaRuntimeVerseHostSettingsSnapshot(
            verseHost.ServiceId,
            verseHost.VerseId,
            verseHost.RootVerse,
            verseHost.CanonicalService,
            verseHost.LocatedService,
            verseHost.CultMeshAddress,
            verseHost.Title,
            verseHost.Visibility,
            verseHost.LastUpdatedAtUtc)),
        playerSettings,
        canOpenRuntimeInputScreen: true,
        inGame: true,
        updatedAtUtc);
    var mainMenuSettings = AetheriaRuntimeMainMenuSurfaceBuilder.BuildSettings(updatedAtUtc);
    var mainMenuInputSettings = AetheriaRuntimeMainMenuSurfaceBuilder.BuildInputSettings(
        stateBoot,
        playerSettings,
        canOpenRuntimeInputScreen: true,
        inGame: true,
        updatedAtUtc);
    var mainMenuPlayerSettings = AetheriaRuntimeMainMenuSurfaceBuilder.BuildPlayerSettings(
        playerSettings,
        updatedAtUtc);
    var mainMenuVerseSettings = AetheriaRuntimeMainMenuSurfaceBuilder.BuildVerseSettings(
        AetheriaRuntimeClientTargetSurfaceBuilder.Build(
            stateBoot,
            AetheriaRuntimeVerseHostSettingsDocument.FromSnapshot(new AetheriaRuntimeVerseHostSettingsSnapshot(
                verseHost.ServiceId,
                verseHost.VerseId,
                verseHost.RootVerse,
                verseHost.CanonicalService,
                verseHost.LocatedService,
                verseHost.CultMeshAddress,
                verseHost.Title,
                verseHost.Visibility,
                verseHost.LastUpdatedAtUtc)),
            updatedAtUtc));
    var inventoryPanel = AetheriaRuntimeInventoryPanelSurfaceBuilder.BuildFromDocuments(
        currentEntity,
        stationRefit,
        inventory,
        catalog,
        playerSettings,
        inventoryRequest,
        AetheriaRuntimeVerseRecordKeys.InventoryDropdownSurface.ToString(),
        updatedAtUtc);
    var inventoryDropdown = AetheriaRuntimeInventoryDropdownSurfaceBuilder.BuildFromDocuments(
        stationRefit,
        dropdownRequest,
        updatedAtUtc);
    var mapMenu = AetheriaRuntimeZoneDetailsSurfaceBuilder.BuildFromDocuments(
        AetheriaRuntimeGameDocuments.ZoneDetails(frame, currentEntity.ZoneIndex),
        AetheriaRuntimeGameDocuments.SectorMap(frame),
        catalog,
        playerSettings,
        updatedAtUtc);
    var tradeMenu = BuildTradeMenuSurface(stationRefit, catalog, updatedAtUtc, frame.FrameId);

    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.MainMenuSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(mainMenu))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.MainMenuSettingsSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(mainMenuSettings))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.MainMenuInputSettingsSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(mainMenuInputSettings))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.MainMenuPlayerSettingsSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(mainMenuPlayerSettings))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.MainMenuVerseSettingsSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(mainMenuVerseSettings))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.InventoryPanelSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(inventoryPanel))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.InventoryDropdownSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(inventoryDropdown))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.MapMenuSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(mapMenu))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaRuntimeVerseRecordKeys.TradeMenuSurface)
        .ReplaceAsync(AetheriaRuntimeSurfaceDocuments.ToPortableSurface(tradeMenu))
        .ConfigureAwait(false);
    await node.MutableDocument<AetheriaRuntimeAssetManifestDocument>(AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest)
        .ReplaceAsync(assetManifest)
        .ConfigureAwait(false);
}

static async Task<AetheriaRuntimePlayerSettingsDocument> ReadRuntimePlayerSettingsAsync(AetheriaStateNode node)
{
    var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
        .ReadAsync()
        .ConfigureAwait(false);
    settings ??= new AetheriaPlayerSettings();
    return new AetheriaRuntimePlayerSettingsDocument
    {
        PlayerName = settings.PlayerName ?? "",
        TutorialPassed = settings.TutorialPassed,
        TemperatureUnit = settings.Gameplay?.TemperatureUnit ?? "",
        SignificantDigits = settings.Gameplay?.SignificantDigits ?? 3,
        DefaultShutdownPerformance = settings.Gameplay?.DefaultShutdownPerformance ?? 0,
        NebulaQuality = settings.Graphics?.NebulaQuality ?? "",
        ShowAsteroidsInMinimap = settings.Graphics?.ShowAsteroidsInMinimap ?? false,
        BindingOverrides = Array.Empty<AetheriaRuntimeInputBindingOverrideDocument>(),
        ActionBarInputs = settings.Input?.ActionBarInputs ?? Array.Empty<string>()
    };
}

static AetheriaRuntimeSurfaceDocument BuildTradeMenuSurface(
    AetheriaRuntimeStationRefitDocument stationRefit,
    AetheriaRuntimeCatalogSnapshot catalog,
    string updatedAtUtc,
    long version)
{
    stationRefit ??= new AetheriaRuntimeStationRefitDocument();
    var rows = (stationRefit.StationStock ?? Array.Empty<AetheriaRuntimeStationStockItem>())
        .Take(12)
        .Select((item, index) =>
        {
            var typedItem = catalog?.FindItem(item.ItemKey);
            return SurfaceLeaf(
                $"aetheria.trade.menu.stock.{index}",
                "row",
                ("item", typedItem?.Name ?? item.ItemKey),
                ("key", item.ItemKey ?? ""),
                ("qty", item.Quantity.ToString(CultureInfo.InvariantCulture)),
                ("price", item.Price.ToString("N0", CultureInfo.InvariantCulture)),
                ("owned", item.OwnedQuantity.ToString(CultureInfo.InvariantCulture)),
                ("afford", item.CanAfford ? "yes" : "no"));
        })
        .DefaultIfEmpty(SurfaceLeaf(
            "aetheria.trade.menu.stock.empty",
            "text",
            ("value", "No station stock is available in the current daemon frame.")))
        .ToArray();

    return new AetheriaRuntimeSurfaceDocument(
        providerId: "aetheria.daemon",
        providerKind: "trade.menu",
        title: "Trade Menu",
        version: version,
        updatedAtUtc: updatedAtUtc ?? "",
        surface: new AetheriaRuntimeSurfaceTree(
            "aetheria.trade.menu",
            SurfaceNode(
                "aetheria.trade.menu.root",
                "surface",
                Array.Empty<(string Key, string Value)>(),
                SurfaceNode(
                    "aetheria.trade.menu.summary",
                    "card",
                    new[] { ("title", "Station Trade") },
                    SurfaceLeaf("aetheria.trade.menu.summary.docked", "metric", ("label", "Docked"), ("value", stationRefit.IsDocked ? "yes" : "no")),
                    SurfaceLeaf("aetheria.trade.menu.summary.station", "metric", ("label", "Station"), ("value", stationRefit.DockParent?.DisplayName ?? stationRefit.DockParentEntityKey ?? "")),
                    SurfaceLeaf("aetheria.trade.menu.summary.credits", "metric", ("label", "Credits"), ("value", stationRefit.Credits.ToString("N0", CultureInfo.InvariantCulture)))),
                SurfaceNode(
                    "aetheria.trade.menu.stock",
                    "card",
                    new[] { ("title", "Station Stock") },
                    rows)),
            Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
        commands: Array.Empty<AetheriaRuntimeSurfaceCommandTemplate>());
}

static AetheriaRuntimeSurfaceComponent SurfaceLeaf(
    string id,
    string kind,
    params (string Key, string Value)[] props)
{
    return SurfaceNode(id, kind, props, Array.Empty<AetheriaRuntimeSurfaceComponent>());
}

static AetheriaRuntimeSurfaceComponent SurfaceNode(
    string id,
    string kind,
    (string Key, string Value)[] props,
    params AetheriaRuntimeSurfaceComponent[] children)
{
    return new AetheriaRuntimeSurfaceComponent(
        id,
        kind,
        props.ToDictionary(prop => prop.Key, prop => prop.Value),
        children);
}

static async Task AcceptCoreEveInvocationsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    AetheriaRuntimeDaemonFrameDocument? currentFrame)
{
    var accounted = new HashSet<string>(
        currentFrame?.AccountedCommandIds ?? Array.Empty<string>(),
        StringComparer.Ordinal);
    var receipted = node.Documents<EveCommandReceiptDocument>()
        .Where(receipt => !string.IsNullOrWhiteSpace(receipt.CommandId))
        .Select(receipt => receipt.CommandId)
        .ToHashSet(StringComparer.Ordinal);
    foreach (var request in node.Documents<EveSurfaceCommandRequest>()
                 .Where(request => request != null && !string.IsNullOrWhiteSpace(request.CommandId))
                 .OrderBy(request => request.IssuedAt))
    {
        if (accounted.Contains(request.CommandId) || receipted.Contains(request.CommandId))
            continue;

        if (AetheriaRuntimeDaemonOperationsClient.TryCreateSurfaceCommandDocument(
                request,
                currentFrame,
                node.StatePath,
                request.ClientId,
                currentFrame?.SessionId ?? options.SessionId,
                out var command) && command != null)
        {
            command.CommandId = request.CommandId;
            command.ClientId = request.ClientId;
            command.AuthorRuntimeId = request.ClientId;
            await node.SubmitDaemonCommandAsync(command).ConfigureAwait(false);
            continue;
        }

        var denied = new EveCommandReceiptDocument(
            $"receipt:{request.CommandId}:denied",
            request.CommandId,
            request.Command,
            "denied",
            "Aetheria",
            options.DaemonId,
            request.ProviderId,
            request.SurfaceId,
            "Command is not advertised by the Aetheria daemon surface.",
            DateTimeOffset.UtcNow.ToString("O"),
            Math.Max(currentFrame?.FrameId ?? 0, 0));
        await node.Database.PutAsync(AetheriaRuntimeVerseRecordKeys.EveReceiptForCommand(denied.CommandId), denied)
            .ConfigureAwait(false);
    }
}

static async Task AcceptEveCommandsAsync(AetheriaStateNode node, AetheriaDaemonHostOptions options)
{
    var commandCountBefore = node.Documents<AetheriaRuntimeEveCommandDocument>().Count;
    var now = DateTimeOffset.UtcNow.ToString("O");
    try
    {
        var existingStatus = await node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey).ReadAsync().ConfigureAwait(false);
        var report = await AetheriaEveCommandBridge.AcceptObservedAsync(
                node,
                existingStatus?.AccountedCommandIds)
            .ConfigureAwait(false);
        await node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey)
            .ReplaceAsync(new AetheriaEveCommandAcceptanceStatus
            {
                RuntimeId = options.DaemonId,
                StatePath = node.StatePath,
                LastPollAtUtc = now,
                LastAcceptedAtUtc = report.AcceptedCommandIds.Length > 0 ? now : "",
                ObservedBeforeAccept = commandCountBefore,
                CommandsAccepted = report.AcceptedCommandIds.Length,
                CommandsRejected = report.RejectedCommands,
                AppliedCatalogRefreshes = report.AcceptedCatalogRefreshes,
                AppliedOperationsRefreshes = report.AcceptedOperationsRefreshes,
                AppliedPlayerSettingsCommands = report.AcceptedPlayerSettingsCommands,
                AppliedInputSettingsCommands = report.AcceptedInputSettingsCommands,
                AppliedLoadoutTemplateCommands = report.AcceptedLoadoutTemplateCommands,
                AppliedVerseHostCommands = report.AcceptedVerseHostCommands,
                AppliedMainMenuCommands = report.AcceptedMainMenuCommands,
                AccountedCommandIds = report.AccountedCommandIds,
                LastRejectedCommand = report.LastRejectedCommand,
                LastRejectedReason = report.LastRejectedReason,
                ConsecutiveFailures = 0,
                Status = report.RejectedCommands > 0 ? "rejected" : "ok"
            })
            .ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        var existing = await node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey).ReadAsync().ConfigureAwait(false);
        await node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey)
            .ReplaceAsync(new AetheriaEveCommandAcceptanceStatus
            {
                RuntimeId = options.DaemonId,
                StatePath = node.StatePath,
                LastPollAtUtc = now,
                LastAcceptedAtUtc = existing?.LastAcceptedAtUtc ?? "",
                ObservedBeforeAccept = commandCountBefore,
                AccountedCommandIds = existing?.AccountedCommandIds ?? [],
                ConsecutiveFailures = (existing?.ConsecutiveFailures ?? 0) + 1,
                LastError = ex.ToString(),
                Status = "error"
            })
            .ConfigureAwait(false);
        throw;
    }
}

static async Task PublishStateSurfacesAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string updatedAtUtc)
{
    var verseHost = await EnsureVerseHostSettingsAsync(node, options, updatedAtUtc).ConfigureAwait(false);
    var eveStatus = await node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey).ReadAsync().ConfigureAwait(false);
    var runtimeSession = await node.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey(options.DaemonId)).ReadAsync().ConfigureAwait(false);
    var playerSettings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReadAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
    var playerSettingsUpdatedAt = string.IsNullOrWhiteSpace(playerSettings.LastUpdatedAtUtc)
        ? updatedAtUtc
        : playerSettings.LastUpdatedAtUtc;

    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.OperationsSurfaceKey)
        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildOperationsSurface(eveStatus, verseHost, runtimeSession))
        .ConfigureAwait(false);
    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.PlayerSettingsSurfaceKey)
        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildPlayerSettingsSurface(playerSettings, playerSettingsUpdatedAt))
        .ConfigureAwait(false);
    await node.MutableDocument<EveProviderAdvertisementState>(AetheriaStateNode.ProviderAdvertisementSurfaceKey)
        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildProviderAdvertisement(verseHost, node.StatePath, updatedAtUtc))
        .ConfigureAwait(false);
    await node.FlushAsync().ConfigureAwait(false);
}

static async Task PublishOdinSurfaceAnnouncementsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string updatedAtUtc)
{
    if (!options.EnableOdinAnnouncements || string.IsNullOrWhiteSpace(options.OdinCultMeshUri))
        return;

    var surfaces = new[]
    {
        ("aetheria.operations", "Aetheria Operations", AetheriaStateNode.OperationsSurfaceKey),
        ("aetheria.player_settings", "Aetheria Player Settings", AetheriaStateNode.PlayerSettingsSurfaceKey),
        ("aetheria.daemon.game", "Aetheria Daemon Game", AetheriaRuntimeVerseRecordKeys.DaemonGameSurface),
        ("aetheria.daemon.game.tui", "Aetheria Daemon Game TUI", AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface),
        ("aetheria.daemon.editor", "Aetheria Daemon Editor", AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface),
        ("aetheria.daemon.editor.tui", "Aetheria Daemon Editor TUI", AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface),
        ("aetheria.main_menu.root", "Main Menu", AetheriaRuntimeVerseRecordKeys.MainMenuSurface),
        ("aetheria.main_menu.settings", "Main Menu Settings", AetheriaRuntimeVerseRecordKeys.MainMenuSettingsSurface),
        ("aetheria.main_menu.input_settings", "Main Menu Input Settings", AetheriaRuntimeVerseRecordKeys.MainMenuInputSettingsSurface),
        ("aetheria.main_menu.player_settings", "Main Menu Player Settings", AetheriaRuntimeVerseRecordKeys.MainMenuPlayerSettingsSurface),
        ("aetheria.main_menu.verse_settings", "Main Menu Verse Settings", AetheriaRuntimeVerseRecordKeys.MainMenuVerseSettingsSurface),
        ("aetheria.inventory.panel", "Inventory Panel", AetheriaRuntimeVerseRecordKeys.InventoryPanelSurface),
        ("aetheria.inventory.panel.dropdown", "Inventory Dropdown", AetheriaRuntimeVerseRecordKeys.InventoryDropdownSurface),
        ("aetheria.map.zone_details", "Map Menu", AetheriaRuntimeVerseRecordKeys.MapMenuSurface),
        ("aetheria.trade.menu", "Trade Menu", AetheriaRuntimeVerseRecordKeys.TradeMenuSurface)
    };

    var documents = new List<CultNetDocumentPutRawMessage>();
    var assetManifest = await node.MutableDocument<AetheriaRuntimeAssetManifestDocument>(AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest)
        .ReadAsync()
        .ConfigureAwait(false);
    if (assetManifest != null)
    {
        documents.Add(CreateOdinRawPut(
            "gamecult.aetheria.asset_manifest.v1",
            AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest.ToString(),
            assetManifest));
    }
    foreach (var (providerId, title, recordKey) in surfaces)
    {
        var surface = await node.MutableDocument<EveSurfaceDocument>(recordKey)
            .ReadAsync()
            .ConfigureAwait(false);
        if (surface?.Surface?.Root == null)
            continue;

        documents.Add(CreateOdinRawPut(
            "gamecult.eve.provider_advertisement.v1",
            providerId,
            BuildOdinProviderAdvertisement(providerId, title, options, updatedAtUtc)));
        documents.Add(CreateOdinRawPut(
            "gamecult.eve.surface_state.v1",
            providerId,
            BuildOdinSurfaceState(providerId, title, surface, updatedAtUtc, assetManifest)));
    }

    if (documents.Count == 0)
        return;

    try
    {
        await PublishOdinRawPutsAsync(options, options.OdinCultMeshUri, documents).ConfigureAwait(false);
    }
    catch (Exception ex) when (
        ex is IOException ||
        ex is SocketException ||
        ex is TimeoutException ||
        ex is InvalidOperationException)
    {
        Console.WriteLine($"Aetheria Odin announcement skipped for {options.OdinCultMeshUri}: {ex.GetType().Name}: {ex.Message}");
    }
}

static EveAssetCatalogDocument BuildCoreAssetCatalog(
    AetheriaDaemonHostOptions options,
    AetheriaRuntimeAssetManifestDocument source)
{
    var variants = FindAssetBundles(options).Select(bundle =>
    {
        var artifact = PackAssetBundle(bundle.Path, bundle.Platform);
        return new
        {
            bundle.Platform,
            Uri = artifact.ManifestKey.Value,
            Hash = $"sha256:{artifact.Manifest.ContentHash}",
            Size = artifact.Manifest.SizeBytes
        };
    }).ToArray();

    var assets = (source.Assets ?? Array.Empty<AetheriaRuntimeAssetManifestEntry>())
        .Where(entry => entry?.Ref != null &&
            string.Equals(entry.Ref.Kind, AetheriaRuntimeAssetKinds.Prefab, StringComparison.Ordinal) &&
            (entry.Ref.Metadata.TryGetValue("resourcesPath", out _) || entry.Ref.Metadata.TryGetValue("unityAssetPath", out _)))
        .Select(entry =>
        {
            var unityAssetPath = entry.Ref.Metadata.TryGetValue("unityAssetPath", out var explicitPath)
                ? explicitPath
                : $"Assets/Resources/{entry.Ref.Metadata["resourcesPath"]}.prefab";
            return new EveAssetCatalogEntry(
                entry.Ref.AssetKey,
                entry.Ref.Kind,
                variants.Select(bundle => new EveAssetVariant(
                    "unity-scene",
                    bundle.Platform,
                    "unity-assetbundle",
                    bundle.Uri,
                    bundle.Hash,
                    bundle.Size,
                    unityAssetPath,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["view.pilot.excludeUnityLayers"] = "14"
                    }))
                    .ToArray(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["mimeType"] = entry.Ref.MimeType ?? ""
                });
        })
        .OrderBy(entry => entry.AssetRef, StringComparer.Ordinal)
        .ToArray();
    return new EveAssetCatalogDocument(
        "aetheria.daemon",
        AetheriaRuntimeVerseRecordKeys.EveAssetCatalog.ToString(),
        1,
        source.PublishedAtUtc,
        assets);
}

static IReadOnlyList<(string Path, string Platform)> FindAssetBundles(AetheriaDaemonHostOptions options)
{
    const string bundleName = "aetheria-world";
    return Directory.Exists(options.AssetBundleRoot)
        ? Directory.GetFiles(options.AssetBundleRoot, bundleName, SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (path, new DirectoryInfo(Path.GetDirectoryName(path)!).Name))
            .ToArray()
        : Array.Empty<(string, string)>();
}

static CultMeshCdnArtifact PackAssetBundle(string path, string platform)
{
    return CultMeshCdn.PackArtifact(
        $"aetheria/world/{platform}/aetheria-world",
        File.ReadAllBytes(path),
        new CultMeshCdnPackOptions
        {
            ChunkSizeBytes = 24 * 1024,
            Kind = CultMeshCdnArtifactKinds.Asset,
            Version = "1",
            MimeType = "application/vnd.unity.assetbundle",
            Tags = ["aetheria", "unity-scene", platform]
        });
}

static CultNetDocumentPutRawMessage CreateOdinRawPut(
    string schemaId,
    string recordKey,
    object payload)
{
    var now = DateTimeOffset.UtcNow.ToString("O");
    return new CultNetDocumentPutRawMessage
    {
        MessageId = $"aetheria-odin-put:{recordKey}:{Guid.NewGuid():N}",
        Document = new CultNetRawDocumentRecord
        {
            SchemaId = schemaId,
            RecordKey = recordKey,
            StoredAt = now,
            PayloadEncoding = "messagepack",
            Payload = MessagePackSerializer.Serialize(payload),
            SourceRuntimeId = "aetheria-daemon",
            SourceRole = "aetheria-daemon",
            Tags = ["aetheria", "eve", "odin"]
        }
    };
}

static Dictionary<string, object?> BuildOdinProviderAdvertisement(
    string providerId,
    string title,
    AetheriaDaemonHostOptions options,
    string updatedAtUtc)
{
    return new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["schema"] = "gamecult.eve.provider_advertisement.v1",
        ["providerId"] = providerId,
        ["serviceId"] = options.DaemonId,
        ["verseId"] = options.VerseId,
        ["rootVerse"] = "asgard",
        ["canonicalService"] = "aetheria",
        ["locatedService"] = options.DaemonId,
        ["cultMeshAddress"] = options.CultMeshAddress,
        ["title"] = title,
        ["kind"] = "game.runtime",
        ["status"] = "active",
        ["updatedAt"] = string.IsNullOrWhiteSpace(updatedAtUtc) ? DateTimeOffset.UtcNow.ToString("O") : updatedAtUtc,
        ["capabilities"] = new[] { "cultui-surface", "eve-surface", "cultmesh-cdn-assets", "aetheria-game-surface" },
        ["routes"] = new[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["transport"] = "cultmesh",
                ["address"] = options.OdinCultMeshUri,
                ["resolver"] = "odin-cultmesh",
                ["role"] = "odin-provider-announcement"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["transport"] = "cultmesh-rudp",
                ["address"] = $"rudp://{options.ClientCultMeshAdvertiseHost}:{options.ClientCultMeshPort}",
                ["resolver"] = "provider-cultmesh-rudp",
                ["role"] = "cultmesh-snapshot"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["transport"] = "cultmesh-rudp",
                ["address"] = $"rudp://{options.ClientCultMeshAdvertiseHost}:{options.ClientCultMeshPort}",
                ["schemaId"] = AetheriaRuntimeDaemonSchemas.CultMeshCdnAssetBlob,
                ["resolver"] = "provider-cultmesh-rudp",
                ["role"] = "cultmesh-cdn"
            }
        }
    };
}

static object[] BuildOdinSurfaceState(
    string providerId,
    string title,
    EveSurfaceDocument document,
    string updatedAtUtc,
    AetheriaRuntimeAssetManifestDocument? assetManifest)
{
    var surface = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["schema"] = "gamecult.eve.surface.v1",
        ["id"] = document.Surface.Id,
        ["title"] = string.IsNullOrWhiteSpace(document.Title) ? title : document.Title,
        ["root"] = ToOdinSurfaceComponent(document.Surface.Root),
        ["assetManifestDocumentId"] = AetheriaRuntimeVerseRecordKeys.DaemonAssetManifest.ToString(),
        ["assetManifestSchemaId"] = AetheriaRuntimeDaemonSchemas.AssetManifest,
        ["assets"] = ToOdinAssetRefs(assetManifest)
    };
    return
    [
        providerId,
        title,
        document.Version,
        string.IsNullOrWhiteSpace(document.UpdatedAtUtc)
            ? (string.IsNullOrWhiteSpace(updatedAtUtc) ? DateTimeOffset.UtcNow.ToString("O") : updatedAtUtc)
            : document.UpdatedAtUtc,
        surface
    ];
}

static object[] ToOdinAssetRefs(AetheriaRuntimeAssetManifestDocument? assetManifest)
{
    return assetManifest?.Assets?
        .Where(entry => !string.IsNullOrWhiteSpace(entry?.Ref?.AssetKey))
        .Select(entry => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["assetKey"] = entry.Ref.AssetKey,
            ["kind"] = entry.Ref.Kind,
            ["uri"] = entry.Ref.Uri,
            ["transport"] = entry.Ref.Transport,
            ["mimeType"] = entry.Ref.MimeType,
            ["contentHash"] = entry.Ref.ContentHash,
            ["tags"] = entry.Tags?.ToArray() ?? Array.Empty<string>()
        })
        .Cast<object>()
        .ToArray()
        ?? Array.Empty<object>();
}

static Dictionary<string, object?> ToOdinSurfaceComponent(EveSurfaceComponent component)
{
    var result = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["id"] = component.Id,
        ["kind"] = component.Kind,
        ["props"] = component.Props.ToDictionary(prop => prop.Key, prop => (object?)prop.Value, StringComparer.Ordinal),
        ["children"] = component.Children.Select(ToOdinSurfaceComponent).ToArray()
    };
    if (component.Layout.Count > 0)
    {
        result["layout"] = component.Layout.ToDictionary(prop => prop.Key, prop => (object?)prop.Value, StringComparer.Ordinal);
    }

    if (component.Style.Count > 0)
    {
        result["style"] = component.Style.ToDictionary(prop => prop.Key, prop => (object?)prop.Value, StringComparer.Ordinal);
    }

    if (component.EmbeddedDocuments.Count > 0)
    {
        result["embeddedDocuments"] = component.EmbeddedDocuments
            .Select(document => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["slotId"] = document.SlotId,
                ["documentId"] = document.DocumentId,
                ["schemaId"] = document.SchemaId,
                ["presentationKind"] = document.PresentationKind
            })
            .ToArray();
    }

    return result;
}

static async Task PublishOdinRawPutsAsync(
    AetheriaDaemonHostOptions options,
    string cultMeshUri,
    IReadOnlyList<CultNetDocumentPutRawMessage> documents)
{
    var endpoint = CultMesh.ResolveRudpEndpoint(cultMeshUri);
    using var client = CultMesh.ConnectRudpClient(
        $"{options.DaemonId}-odin-publisher",
        0x0d1d0002,
        endpoint,
        new CultMeshRudpClientOptions
        {
            ConnectPayload = System.Text.Encoding.UTF8.GetBytes("aetheria-odin-surface-announcer"),
            ConnectTimeout = TimeSpan.FromMilliseconds(500),
            PollInterval = TimeSpan.FromMilliseconds(5),
            SocketOptions = new CultMeshRudpSocketOptions
            {
                BindHost = "0.0.0.0",
                BindPort = 0,
                TransportId = "aetheria-odin-surface-announcer",
                MaxFragmentBytes = 1200,
                MaxPendingReliablePackets = 512,
                ResendDelayMs = 25
            }
        });

    foreach (var document in documents)
    {
        client.SendSchemaMessage(document);
        var drainUntil = DateTimeOffset.UtcNow.AddMilliseconds(40);
        while (DateTimeOffset.UtcNow < drainUntil)
        {
            _ = client.ReceiveOnce();
            client.PollResends();
            await Task.Delay(5).ConfigureAwait(false);
        }
    }

    var deadline = DateTimeOffset.UtcNow.AddMilliseconds(500);
    while (DateTimeOffset.UtcNow < deadline)
    {
        _ = client.ReceiveOnce();
        client.PollResends();
        await Task.Delay(10).ConfigureAwait(false);
    }
}

static async Task PublishRuntimeSessionAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string startedAtUtc,
    string status)
{
    var now = DateTimeOffset.UtcNow.ToString("O");
    await node.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey(options.DaemonId))
        .ReplaceAsync(new AetheriaRuntimeSession
        {
            RuntimeId = options.DaemonId,
            Role = "verse-daemon",
            StartedAtUtc = startedAtUtc,
            LastSeenAtUtc = now,
            Status = status
        })
        .ConfigureAwait(false);
    await PublishStateSurfacesAsync(node, options, now).ConfigureAwait(false);
}

static async Task EnsureWorldDocumentAsync(AetheriaStateNode node)
{
    var now = DateTimeOffset.UtcNow.ToString("O");
    var world = node.MutableDocument<AetheriaWorldState>(AetheriaStateNode.WorldKey);
    var existing = await world.ReadAsync().ConfigureAwait(false);
    if (existing != null)
    {
        existing.UpdatedAtUtc = now;
        await world.ReplaceAsync(existing).ConfigureAwait(false);
        return;
    }

    await world.ReplaceAsync(new AetheriaWorldState
    {
        Name = "Aetheria",
        WorldId = "aetheria",
        SchemaEpoch = 1,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    }).ConfigureAwait(false);
}

static async Task EnsureTradeValuePolicyAsync(AetheriaStateNode node, string now)
{
    var tradeValuePolicy = node.MutableDocument<AetheriaTradeValuePolicy>(AetheriaStateNode.TradeValuePolicyKey);
    var existing = await tradeValuePolicy.ReadAsync().ConfigureAwait(false);
    if (existing != null)
        return;

    await tradeValuePolicy.ReplaceAsync(
        AetheriaRuntimeStateMapper.ToTradeValuePolicy(
            AetheriaRuntimeTradeValueSettings.Default,
            now)).ConfigureAwait(false);
}

static async Task EnsurePlayableRunDocumentsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string now)
{
    var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReadAsync().ConfigureAwait(false);
    if (!string.IsNullOrWhiteSpace(settings?.ActiveRunKey))
    {
        var existingRun = await ReadRuntimeRunCheckpointAsync(node, options.RenderSettings).ConfigureAwait(false);
        if (HasPlayableRun(existingRun) && HasTerminusRun(existingRun))
            return;
    }

    await AetheriaDaemonZoneGenerator.WritePlayableRunAsync(
            node,
            node.RuntimeCatalog().Latest(),
            now)
        .ConfigureAwait(false);
}

static async Task EnsureTerminusGameSessionAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string now)
{
    var existing = await node.MutableDocument<AetheriaGameSessionState>(AetheriaStateNode.GameSessionStateKey)
        .ReadAsync().ConfigureAwait(false);
    if (existing != null &&
        string.Equals(existing.Mode, AetheriaGameSessionState.TerminusMode, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(existing.ControlledEntityKey))
        return;

    await node.MutableDocument<AetheriaGameSessionState>(AetheriaStateNode.GameSessionStateKey)
        .ReplaceAsync(new AetheriaGameSessionState
        {
            Mode = AetheriaGameSessionState.TerminusMode,
            SessionId = options.SessionId,
            RunId = AetheriaDaemonZoneGenerator.RunId,
            ControlledEntityKey = AetheriaDaemonZoneGenerator.EntityKey(0, 1),
            EntrySurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.PilotSurfaceId,
            UpdatedAtUtc = now
        }).ConfigureAwait(false);
    await node.FlushAsync().ConfigureAwait(false);
}

static async Task<bool> ApplyRequestedTerminusSessionAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options)
{
    var menu = await node.MutableDocument<AetheriaMainMenuState>(AetheriaStateNode.MainMenuStateKey)
        .ReadAsync().ConfigureAwait(false);
    if (menu == null ||
        !string.Equals(menu.LastCommand, AetheriaRuntimeMainMenuCommands.NewGame, StringComparison.Ordinal) ||
        string.IsNullOrWhiteSpace(menu.LastCommandId))
        return false;

    var session = await node.MutableDocument<AetheriaGameSessionState>(AetheriaStateNode.GameSessionStateKey)
        .ReadAsync().ConfigureAwait(false) ?? new AetheriaGameSessionState();
    if (string.Equals(session.LastStartCommandId, menu.LastCommandId, StringComparison.Ordinal))
        return false;

    var now = DateTimeOffset.UtcNow.ToString("O");
    await AetheriaDaemonZoneGenerator.WritePlayableRunAsync(node, node.RuntimeCatalog().Latest(), now)
        .ConfigureAwait(false);
    session.Mode = AetheriaGameSessionState.TerminusMode;
    session.SessionId = options.SessionId;
    session.RunId = AetheriaDaemonZoneGenerator.RunId;
    session.ControlledEntityKey = AetheriaDaemonZoneGenerator.EntityKey(0, 1);
    session.EntrySurfaceId = AetheriaRuntimeDaemonGameSurfaceBuilder.PilotSurfaceId;
    session.LastStartCommandId = menu.LastCommandId;
    session.UpdatedAtUtc = now;
    await node.MutableDocument<AetheriaGameSessionState>(AetheriaStateNode.GameSessionStateKey)
        .ReplaceAsync(session).ConfigureAwait(false);
    await node.FlushAsync().ConfigureAwait(false);
    return true;
}

static async Task EnsureStarbridgeSessionDocumentsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options)
{
    var existingScenario = await node.MutableDocument<AetheriaRuntimeStarbridgeScenarioDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest).ReadAsync().ConfigureAwait(false);
    var existingSession = await node.MutableDocument<AetheriaRuntimeStarbridgeSessionDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest).ReadAsync().ConfigureAwait(false);
    if (existingScenario != null &&
        string.Equals(existingScenario.Schema, AetheriaRuntimeDaemonSchemas.StarbridgeScenario, StringComparison.Ordinal) &&
        existingSession != null &&
        string.Equals(existingSession.Schema, AetheriaRuntimeDaemonSchemas.StarbridgeSession, StringComparison.Ordinal))
    {
        return;
    }

    const string runId = "local-starbridge";
    var baseEntityKey = EntityKey(runId, 0, 0);
    var pilotOneKey = EntityKey(runId, 0, 1);
    var pilotTwoKey = EntityKey(runId, 0, 2);
    var pilotThreeKey = EntityKey(runId, 0, 3);
    var scenario = new AetheriaRuntimeStarbridgeScenarioDocument
    {
        ScenarioId = "starbridge.frontier-fabricator",
        DisplayName = "Frontier Fabricator Defense",
        StartingBaseKey = baseEntityKey,
        StationStock =
        [
            new AetheriaRuntimeStarbridgeStationStockItem
            {
                ItemKey = "repair-parts",
                Quantity = 4,
                Quality = 1,
                Durability = 1
            },
            new AetheriaRuntimeStarbridgeStationStockItem
            {
                ItemKey = "drone-core",
                Quantity = 2,
                Quality = 0.9,
                Durability = 1
            },
            new AetheriaRuntimeStarbridgeStationStockItem
            {
                ItemKey = "coolant-pack",
                Quantity = 3,
                Quality = 0.85,
                Durability = 1
            },
            new AetheriaRuntimeStarbridgeStationStockItem
            {
                ItemKey = "shield-cell",
                Quantity = 2,
                Quality = 0.95,
                Durability = 1
            }
        ],
        AvailableShipKeys = [pilotOneKey, pilotTwoKey, pilotThreeKey],
        AttackerMixKeys = ["scout", "skirmisher", "bomber", "siege-craft", "breach-drone"],
        RecoveredTechnologyPoolKeys =
        [
            "sensor-calibration",
            "missile-rack-burst",
            "coolant-chain",
            "repair-drone-armor"
        ],
        Waves =
        [
            new AetheriaRuntimeStarbridgeWaveDefinition
            {
                WaveIndex = 0,
                DisplayName = "Scout Probe",
                AttackerKeys = ["scout"],
                StartsAfterSeconds = 30,
                BossKey = "scout-captain",
                RecoveredTechnologyKeys = ["sensor-calibration"]
            },
            new AetheriaRuntimeStarbridgeWaveDefinition
            {
                WaveIndex = 1,
                DisplayName = "Bomber Line",
                AttackerKeys = ["bomber", "skirmisher"],
                StartsAfterSeconds = 150,
                BossKey = "bomber-frame",
                RecoveredTechnologyKeys = ["missile-rack-burst", "coolant-chain"]
            },
            new AetheriaRuntimeStarbridgeWaveDefinition
            {
                WaveIndex = 2,
                DisplayName = "Breach Mother",
                AttackerKeys = ["breach-drone", "siege-craft"],
                StartsAfterSeconds = 300,
                BossKey = "breach-mother",
                RecoveredTechnologyKeys = ["repair-drone-armor"]
            }
        ],
        RuntimeRoles =
        [
            new AetheriaRuntimeStarbridgeRuntimeRole
            {
                RuntimeId = "starbridge.commander",
                Role = "commander"
            },
            new AetheriaRuntimeStarbridgeRuntimeRole
            {
                RuntimeId = "starbridge.pilot.0",
                Role = "pilot",
                EntityKey = pilotOneKey
            }
        ]
    };

    var session = new AetheriaRuntimeStarbridgeSessionDocument
    {
        SessionId = options.SessionId,
        ScenarioId = scenario.ScenarioId,
        RunId = runId,
        BaseEntityKey = baseEntityKey,
        StationEntityKey = baseEntityKey,
        Phase = "pre-wave",
        CurrentWaveIndex = 0,
        RuntimeRoles = scenario.RuntimeRoles
    };

    await node.MutableDocument<AetheriaRuntimeStarbridgeScenarioDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeScenarioLatest)
        .ReplaceAsync(scenario)
        .ConfigureAwait(false);
    await node.MutableDocument<AetheriaRuntimeStarbridgeSessionDocument>(AetheriaRuntimeVerseRecordKeys.StarbridgeSessionLatest)
        .ReplaceAsync(session)
        .ConfigureAwait(false);
    await node.FlushAsync().ConfigureAwait(false);
}

static async Task EnsureVerseAuthorityPolicyAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options)
{
    var existing = await node.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy).ReadAsync().ConfigureAwait(false);
    if (existing != null && string.Equals(existing.Schema, AetheriaRuntimeVerseAuthoritySchemas.Policy, StringComparison.Ordinal))
        return;

    var policy = AetheriaRuntimeVerseAuthorityPolicyDocument.TrustedCoop(options.VerseId, options.DaemonId);
    await node.MutableDocument<AetheriaRuntimeVerseAuthorityPolicyDocument>(AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy)
        .ReplaceAsync(policy)
        .ConfigureAwait(false);
}

static async Task<AetheriaRuntimeRunCheckpointCommit?> ReadRuntimeRunCheckpointAsync(
    AetheriaStateNode node,
    AetheriaRuntimeDaemonRenderSettings renderSettings)
{
    var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReadAsync().ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(settings?.ActiveRunKey))
        return null;

    var run = await node.MutableDocument<AetheriaRunState>(new CultRecordKey(settings.ActiveRunKey)).ReadAsync().ConfigureAwait(false);
    if (run == null)
        return null;

    var catalog = node.RuntimeCatalog().Latest() ?? new AetheriaRuntimeCatalogSnapshot();
    var zones = new List<AetheriaRuntimeZoneSnapshotCommit>();
    var zoneKeys = run.ZoneKeys ?? Array.Empty<string>();
    for (var zoneIndex = 0; zoneIndex < zoneKeys.Length; zoneIndex++)
    {
        var zone = await node.MutableDocument<AetheriaZoneState>(new CultRecordKey(zoneKeys[zoneIndex])).ReadAsync().ConfigureAwait(false);
        if (zone == null)
            continue;

        zones.Add(await ToRuntimeZoneAsync(node, zone, zoneIndex, renderSettings, catalog).ConfigureAwait(false));
    }

    return new AetheriaRuntimeRunCheckpointCommit
    {
        RunId = run.RunId ?? "",
        IsTutorial = run.IsTutorial,
        EntranceZoneIndex = run.EntranceZoneIndex,
        ExitZoneIndex = run.ExitZoneIndex,
        CurrentZoneIndex = run.CurrentZoneIndex,
        DiscoveredZoneIndices = (run.DiscoveredZoneIndices ?? Array.Empty<int>()).ToArray(),
        Zones = zones,
        FactionRelationships = (run.FactionRelationships ?? Array.Empty<AetheriaFactionRelationshipState>())
            .Select(relationship => new AetheriaRuntimeFactionRelationshipCommit
            {
                Relationship = relationship.Relationship ?? "",
                Standing = relationship.Standing,
                FactionKey = relationship.FactionKey ?? ""
            })
            .ToArray(),
        GenerationSeed = run.GenerationSeed,
        CurrentEntityKey = run.CurrentEntityKey ?? "",
        Credits = 1000
    };
}

static async Task<AetheriaRuntimeZoneSnapshotCommit> ToRuntimeZoneAsync(
    AetheriaStateNode node,
    AetheriaZoneState zone,
    int zoneIndex,
    AetheriaRuntimeDaemonRenderSettings renderSettings,
    AetheriaRuntimeCatalogSnapshot catalog)
{
    var entityKeys = zone.EntityKeys ?? Array.Empty<string>();
    var entityIndices = entityKeys
        .Select((key, index) => new { key, index })
        .Where(pair => !string.IsNullOrWhiteSpace(pair.key))
        .ToDictionary(pair => pair.key, pair => pair.index, StringComparer.Ordinal);
    var entities = new List<AetheriaRuntimeEntitySnapshotCommit>();
    for (var entityIndex = 0; entityIndex < entityKeys.Length; entityIndex++)
    {
        var entity = await node.MutableDocument<AetheriaEntitySnapshot>(new CultRecordKey(entityKeys[entityIndex])).ReadAsync().ConfigureAwait(false);
        if (entity != null)
            entities.Add(ToRuntimeEntity(entity, entityIndex, entityIndices, catalog));
    }

    return new AetheriaRuntimeZoneSnapshotCommit
    {
        ZoneIndex = zoneIndex,
        Name = zone.Name ?? "",
        PositionX = zone.Position?.X ?? 0,
        PositionY = zone.Position?.Y ?? 0,
        AdjacentZoneIndices = (zone.AdjacentZoneIndices ?? Array.Empty<int>()).ToArray(),
        FactionIndices = (zone.FactionIndices ?? Array.Empty<int>()).ToArray(),
        OwnerFactionIndex = zone.OwnerFactionIndex,
        Entities = entities,
        Orbits = (zone.Orbits ?? Array.Empty<AetheriaOrbitSnapshot>()).Select(ToRuntimeOrbit).ToArray(),
        Bodies = (zone.Bodies ?? Array.Empty<AetheriaBodySnapshot>()).Select(body => ToRuntimeBody(body, entityIndices, renderSettings)).ToArray(),
        DroppedPickups = (zone.DroppedPickups ?? Array.Empty<AetheriaDroppedPickupSnapshot>()).Select(ToRuntimePickup).ToArray(),
        GravityTerrainRadius = zone.GravityTerrainRadius,
        GravityTerrainDepth = zone.GravityTerrainDepth,
        GravityTerrainDepthExponent = zone.GravityTerrainDepthExponent,
        GravityTerrainBoundaryFog = zone.GravityTerrainBoundaryFog,
        GravityTerrainWaveFrequency = zone.GravityTerrainWaveFrequency,
        SimulationTimeSeconds = zone.SimulationTimeSeconds
    };
}

static AetheriaRuntimeEntitySnapshotCommit ToRuntimeEntity(
    AetheriaEntitySnapshot entity,
    int entityIndex,
    IReadOnlyDictionary<string, int> entityIndices,
    AetheriaRuntimeCatalogSnapshot catalog)
{
    var equipment = ToEntitySlotCommits(entity.Equipment);
    var runtimeEntity = new AetheriaRuntimeEntitySnapshotCommit
    {
        EntityIndex = entityIndex,
        Name = entity.Name ?? "",
        Kind = entity.Kind ?? "",
        PositionX = entity.Position?.X ?? 0,
        PositionY = entity.Position?.Y ?? 0,
        PositionZ = entity.Position?.Z ?? 0,
        DirectionX = entity.Direction?.X ?? 0,
        DirectionY = entity.Direction?.Y ?? 1,
        FactionKey = entity.FactionKey ?? "",
        HullItemKey = entity.HullItemKey ?? "",
        Equipment = equipment,
        CargoBays = ToEntitySlotCommits(entity.CargoBays),
        DockingBays = ToEntitySlotCommits(entity.DockingBays),
        ChildEntityIndices = (entity.ChildEntityKeys ?? Array.Empty<string>())
            .Select(key => entityIndices.TryGetValue(key, out var index) ? index : -1)
            .Where(index => index >= 0)
            .ToArray(),
        WeaponGroups = (entity.WeaponGroups ?? Array.Empty<AetheriaWeaponGroupSnapshot>())
            .Select(group => (IReadOnlyList<int>)(group.EquipmentIndices ?? Array.Empty<int>()).ToArray())
            .ToArray(),
        StatGrids = (entity.StatGrids ?? Array.Empty<AetheriaEntityStatGrid>())
            .Select(grid => new AetheriaRuntimeEntityStatGridCommit
            {
                Name = grid.Name ?? "",
                Width = grid.Width,
                Height = grid.Height,
                Values = (grid.Values ?? Array.Empty<double>()).ToArray()
            })
            .ToArray(),
        VelocityX = entity.Velocity?.X ?? 0,
        VelocityY = entity.Velocity?.Y ?? 0,
        TargetEntityIndex = entityIndices.TryGetValue(entity.TargetEntityKey ?? "", out var targetIndex) ? targetIndex : -1,
        IsActive = entity.IsActive,
        HeatsinksEnabled = entity.HeatsinksEnabled,
        OverrideShutdown = entity.OverrideShutdown,
        TractorPower = entity.TractorPower,
        Heatstroke = entity.Heatstroke,
        Hypothermia = entity.Hypothermia,
        ActiveConsumables = (entity.ActiveConsumables ?? Array.Empty<AetheriaActiveConsumableSnapshot>())
            .Select(consumable => new AetheriaRuntimeActiveConsumableCommit
            {
                ItemKey = consumable.ItemKey ?? "",
                Quality = consumable.Quality,
                RemainingDuration = consumable.RemainingDuration,
                Duration = consumable.Duration
            })
            .ToArray(),
        BehaviorProgress = (entity.BehaviorProgress ?? Array.Empty<AetheriaBehaviorProgressSnapshot>())
            .Select(progress => new AetheriaRuntimeBehaviorProgressCommit
            {
                OwnerKind = progress.OwnerKind ?? "",
                OwnerIndex = progress.OwnerIndex,
                BehaviorIndex = progress.BehaviorIndex,
                BehaviorKind = progress.BehaviorKind ?? "",
                Progress = progress.Progress
            })
            .ToArray(),
        WeaponStates = Array.Empty<AetheriaRuntimeWeaponStateCommit>(),
        BehaviorStates = AetheriaRuntimeBehaviorStateProjector.CreateEquipmentBehaviorStates(equipment, catalog),
        CargoContents = ToCargoBayCommits(entity.CargoContents),
        DockingBayContents = ToCargoBayCommits(entity.DockingBayContents),
        DockingBayAssignments = (entity.DockingBayAssignments ?? Array.Empty<int>()).ToArray(),
        Visibility = entity.Visibility,
        VisibilitySourceCount = entity.VisibilitySourceCount,
        Contacts = (entity.Contacts ?? Array.Empty<AetheriaEntityContactSnapshot>())
            .Select(contact => ToRuntimeContact(contact, entityIndices))
            .Where(contact => contact.TargetEntityIndex >= 0)
            .ToArray()
    };
    AetheriaRuntimeBehaviorStateProjector.EnsureEquipmentBehaviorStates(runtimeEntity, catalog);
    return runtimeEntity;
}

static AetheriaRuntimeEntityContactCommit ToRuntimeContact(
    AetheriaEntityContactSnapshot contact,
    IReadOnlyDictionary<string, int> entityIndices)
{
    return new AetheriaRuntimeEntityContactCommit
    {
        TargetEntityIndex = entityIndices.TryGetValue(contact.TargetEntityKey ?? "", out var index) ? index : -1,
        InfoGathered = contact.InfoGathered,
        Hostile = contact.Hostile,
        Visible = contact.Visible
    };
}

static AetheriaRuntimeOrbitSnapshotCommit ToRuntimeOrbit(AetheriaOrbitSnapshot orbit)
{
    return new AetheriaRuntimeOrbitSnapshotCommit
    {
        OrbitKey = orbit.OrbitKey ?? "",
        ParentOrbitKey = orbit.ParentOrbitKey ?? "",
        Distance = orbit.Distance,
        Phase = orbit.Phase,
        FixedPositionX = orbit.FixedPosition?.X ?? 0,
        FixedPositionY = orbit.FixedPosition?.Y ?? 0
    };
}

static AetheriaRuntimeBodySnapshotCommit ToRuntimeBody(
    AetheriaBodySnapshot body,
    IReadOnlyDictionary<string, int> entityIndices,
    AetheriaRuntimeDaemonRenderSettings renderSettings)
{
    return new AetheriaRuntimeBodySnapshotCommit
    {
        BodyKey = body.BodyKey ?? "",
        Kind = body.Kind ?? "",
        Name = body.Name ?? "",
        OrbitKey = body.OrbitKey ?? "",
        Mass = body.Mass,
        Resources = (body.Resources ?? Array.Empty<AetheriaBodyResource>())
            .Select(resource => new AetheriaRuntimeBodyResourceCommit
            {
                ItemKey = resource.ItemKey ?? "",
                Amount = resource.Amount
            })
            .ToArray(),
        BodyRadiusMultiplier = body.BodyRadiusMultiplier,
        GravityRadiusMultiplier = body.GravityRadiusMultiplier,
        GravityDepthMultiplier = body.GravityDepthMultiplier,
        GravityDepthExponent = body.GravityDepthExponent,
        Asteroids = (body.Asteroids ?? Array.Empty<AetheriaAsteroidSnapshot>())
            .Select(asteroid => new AetheriaRuntimeAsteroidCommit
            {
                Distance = asteroid.Distance,
                Phase = asteroid.Phase,
                Size = asteroid.Size,
                RotationSpeed = asteroid.RotationSpeed,
                Damage = asteroid.Damage,
                RespawnTimer = asteroid.RespawnTimer,
                MiningAccumulators = (asteroid.MiningAccumulators ?? Array.Empty<AetheriaAsteroidMiningAccumulatorSnapshot>())
                    .Select(accumulator => new AetheriaRuntimeAsteroidMiningAccumulatorCommit
                    {
                        MinerEntityIndex = entityIndices.TryGetValue(accumulator.MinerEntityKey ?? "", out var index) ? index : -1,
                        Amount = accumulator.Amount
                    })
                    .ToArray()
            })
            .ToArray(),
        GasGiantVisual = new AetheriaRuntimeGasGiantVisualCommit
        {
            FirstOffsetDomainRotationSpeed = body.GasGiantVisual?.FirstOffsetDomainRotationSpeed ?? 1,
            FirstOffsetRotationSpeed = body.GasGiantVisual?.FirstOffsetRotationSpeed ?? 1,
            SecondOffsetDomainRotationSpeed = body.GasGiantVisual?.SecondOffsetDomainRotationSpeed ?? 1,
            SecondOffsetRotationSpeed = body.GasGiantVisual?.SecondOffsetRotationSpeed ?? 1,
            AlbedoRotationSpeed = body.GasGiantVisual?.AlbedoRotationSpeed ?? 1,
            WaveRadiusMultiplier = body.GasGiantVisual?.WaveRadiusMultiplier ?? 1,
            WaveDepthMultiplier = body.GasGiantVisual?.WaveDepthMultiplier ?? 1,
            WaveDepthExponent = body.GasGiantVisual?.WaveDepthExponent ?? 8,
            WaveSpeedMultiplier = body.GasGiantVisual?.WaveSpeedMultiplier ?? 8,
            MaterialOverrides = (body.GasGiantVisual?.MaterialOverrides ?? Array.Empty<string>()).ToArray(),
            Colors = (body.GasGiantVisual?.Colors ?? Array.Empty<AetheriaColor>())
                .Select(color => new AetheriaRuntimeColorCommit { X = color.X, Y = color.Y, Z = color.Z, W = color.W })
                .ToArray()
        },
        SunVisual = new AetheriaRuntimeSunVisualCommit
        {
            LightColorX = body.SunVisual?.LightColor?.X ?? 0,
            LightColorY = body.SunVisual?.LightColor?.Y ?? 0,
            LightColorZ = body.SunVisual?.LightColor?.Z ?? 0,
            FogTintColorX = body.SunVisual?.FogTintColor?.X ?? 0,
            FogTintColorY = body.SunVisual?.FogTintColor?.Y ?? 0,
            FogTintColorZ = body.SunVisual?.FogTintColor?.Z ?? 0,
            LightRadiusMultiplier = body.SunVisual?.LightRadiusMultiplier ?? 1
        },
        GravityInfluenceCenterX = body.GravityInfluenceCenterX,
        GravityInfluenceCenterZ = body.GravityInfluenceCenterZ,
        GravityInfluenceRadius = body.GravityInfluenceRadius,
        GravityWellDepth = body.GravityWellDepth,
        GravityWaveRadius = body.GravityWaveRadius,
        GravityWaveDepth = body.GravityWaveDepth,
        GravityWaveSpeed = body.GravityWaveSpeed,
        IconSize = renderSettings.ResolveBodyIconSize(body.Mass)
    };
}

static void ApplyDaemonRenderSettings(
    AetheriaRuntimeRunCheckpointCommit run,
    AetheriaRuntimeDaemonRenderSettings renderSettings)
{
    foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
    {
        foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            body.IconSize = renderSettings.ResolveBodyIconSize(body.Mass);
    }
}

static AetheriaRuntimeDroppedPickupCommit ToRuntimePickup(AetheriaDroppedPickupSnapshot pickup)
{
    return new AetheriaRuntimeDroppedPickupCommit
    {
        PickupIndex = pickup.PickupIndex,
        PositionX = pickup.Position?.X ?? 0,
        PositionY = pickup.Position?.Y ?? 0,
        PositionZ = pickup.Position?.Z ?? 0,
        VelocityX = pickup.Velocity?.X ?? 0,
        VelocityY = pickup.Velocity?.Y ?? 0,
        VelocityZ = pickup.Velocity?.Z ?? 0,
        Item = ToLoadoutItemCommit(pickup.Item)
    };
}

static bool HasPlayableRun(AetheriaRuntimeRunCheckpointCommit? run)
{
    return (run?.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
        .Any(zone => (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).Count > 0);
}

static bool HasTerminusRun(AetheriaRuntimeRunCheckpointCommit? run)
{
    var zones = run?.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>();
    var entities = zones.SelectMany(zone => zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToArray();
    var bodies = zones.SelectMany(zone => zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>()).ToArray();
    return run?.GenerationSeed == AetheriaDaemonZoneGenerator.GenerationSeed &&
        zones.Any(zone => string.Equals(zone.Name, "Daemon Generated Terminus", StringComparison.Ordinal)) &&
        entities.Any(entity => string.Equals(entity.Name, "Anchor Station", StringComparison.Ordinal)) &&
        entities.Count(entity => string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase)) >= 4 &&
        entities.Count(entity => string.Equals(entity.FactionKey, "raider", StringComparison.OrdinalIgnoreCase)) >= 3 &&
        bodies.Any(body => string.Equals(body.BodyKey, "local.outer", StringComparison.Ordinal)) &&
        bodies.Length >= 10;
}

static string EntityKey(string runId, int zoneIndex, int entityIndex)
{
    return $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
}

static AetheriaRuntimeLoadoutItemSlotCommit[] ToEntitySlotCommits(
    IReadOnlyList<AetheriaEntityItemSlot>? slots)
{
    return (slots ?? Array.Empty<AetheriaEntityItemSlot>())
        .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
        {
            X = slot.Position?.X ?? 0,
            Y = slot.Position?.Y ?? 0,
            Item = new AetheriaRuntimeLoadoutItemCommit
            {
                ItemKey = slot.ItemKey ?? "",
                Quality = slot.Quality,
                Durability = slot.Durability,
                Quantity = slot.Quantity,
                Enabled = slot.Enabled,
                OverrideShutdown = slot.OverrideShutdown,
                Temperature = slot.Temperature
            }
        })
        .ToArray();
}

static async Task<AetheriaVerseHostSettings> EnsureVerseHostSettingsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string now)
{
    var existing = await node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey).ReadAsync().ConfigureAwait(false);
    var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(existing);
    normalized.ServiceId = options.DaemonId;
    normalized.VerseId = options.VerseId;
    normalized.CultMeshAddress = options.CultMeshAddress;

    if (existing == null ||
        string.IsNullOrWhiteSpace(existing.LastUpdatedAtUtc) ||
        !AetheriaVerseHostSettingsNormalizer.Equivalent(existing, normalized))
    {
        normalized.LastUpdatedAtUtc = now;
        await node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey)
            .ReplaceAsync(normalized)
            .ConfigureAwait(false);
    }

    return normalized;
}

static AetheriaRuntimeLoadoutTemplateCommit ToLoadoutTemplateCommit(AetheriaLoadoutTemplate template)
{
    return new AetheriaRuntimeLoadoutTemplateCommit
    {
        Name = template.Name ?? "",
        OwnerPlayerKey = template.OwnerPlayerKey ?? "",
        RootEntity = ToEntityLoadoutCommit(template.RootEntity)
    };
}

static AetheriaRuntimeEntityLoadoutCommit ToEntityLoadoutCommit(AetheriaEntityLoadout? entity)
{
    entity ??= new AetheriaEntityLoadout();
    return new AetheriaRuntimeEntityLoadoutCommit
    {
        Name = entity.Name ?? "",
        Kind = entity.Kind ?? "",
        FactionKey = entity.FactionKey ?? "",
        Hull = ToLoadoutItemCommit(entity.Hull),
        Equipment = ToSlotCommits(entity.Equipment),
        CargoBays = ToSlotCommits(entity.CargoBays),
        DockingBays = ToSlotCommits(entity.DockingBays),
        CargoContents = ToCargoBayCommits(entity.CargoContents),
        DockingBayContents = ToCargoBayCommits(entity.DockingBayContents),
        DockingBayAssignments = (entity.DockingBayAssignments ?? Array.Empty<int>()).ToArray(),
        WeaponGroups = (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
            .Select(group => (group ?? Array.Empty<int>()).ToArray())
            .ToArray(),
        Children = (entity.Children ?? Array.Empty<AetheriaEntityLoadout>())
            .Select(ToEntityLoadoutCommit)
            .ToArray()
    };
}

static AetheriaRuntimeLoadoutItemSlotCommit[] ToSlotCommits(
    IReadOnlyList<AetheriaLoadoutItemSlot>? slots)
{
    return (slots ?? Array.Empty<AetheriaLoadoutItemSlot>())
        .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
        {
            X = slot.Position?.X ?? 0,
            Y = slot.Position?.Y ?? 0,
            Item = ToLoadoutItemCommit(slot.Item)
        })
        .ToArray();
}

static AetheriaRuntimeCargoBayLoadoutCommit[] ToCargoBayCommits(
    IReadOnlyList<AetheriaCargoBayLoadout>? cargoBays)
{
    return (cargoBays ?? Array.Empty<AetheriaCargoBayLoadout>())
        .Select(cargoBay => new AetheriaRuntimeCargoBayLoadoutCommit
        {
            Items = ToSlotCommits(cargoBay.Items)
        })
        .ToArray();
}

static AetheriaRuntimeLoadoutItemCommit ToLoadoutItemCommit(AetheriaLoadoutItem? item)
{
    return new AetheriaRuntimeLoadoutItemCommit
    {
        ItemKey = item?.ItemKey ?? "",
        Quality = item?.Quality ?? 0,
        Durability = item?.Durability ?? 0,
        Quantity = item?.Quantity ?? 0,
        Enabled = item?.Enabled ?? false,
        OverrideShutdown = item?.OverrideShutdown ?? false
    };
}

internal sealed class AetheriaDaemonHostOptions
{
    public string StatePath { get; init; } = "";
    public string DaemonId { get; init; } = "aetheria-daemon";
    public string SessionId { get; init; } = "local";
    public string VerseId { get; init; } = "aetheria.local";
    public string CultMeshAddress { get; init; } = "cultmesh://aetheria.local/eve/providers/aetheria.daemon";
    public string ClientCultMeshHost { get; init; } = "127.0.0.1";
    public string ClientCultMeshAdvertiseHost { get; init; } = "127.0.0.1";
    public int ClientCultMeshPort { get; init; } = 3076;
    public string AetheriaResourcesRoot { get; init; } = "";
    public string AssetBundleRoot { get; init; } = "";
    public string OdinCultMeshUri { get; init; } = "cultmesh://odin/rendezvous/provider-catalog";
    public bool EnableOdinAnnouncements { get; init; } = true;
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(20);
    public TimeSpan ApiPublicationInterval { get; init; } = TimeSpan.FromSeconds(1);
    public double FixedDeltaSeconds { get; init; } = 0.02;
    public AetheriaRuntimeDaemonRenderSettings RenderSettings { get; init; } =
        AetheriaRuntimeDaemonRenderSettings.AetheriaDefault;
    public AetheriaRuntimeDaemonSimulationSettings SimulationSettings { get; init; } =
        AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault;
    public bool Once { get; init; }

    public static AetheriaDaemonHostOptions Parse(IReadOnlyList<string> args)
    {
        var root = ReadOption(args, "--root");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        var state = ReadOption(args, "--state");
        var intervalMs = ReadPositiveInt(args, "--tick-interval-ms") ?? 20;
        var fixedDeltaMs = ReadPositiveInt(args, "--fixed-delta-ms") ?? 20;
        var daemonId = ReadOption(args, "--daemon-id");
        var verseId = ReadOption(args, "--verse-id");
        var cultMeshAddress = ReadOption(args, "--cultmesh-address");
        var clientCultMeshHost = ReadOption(args, "--client-cultmesh-host");
        var clientCultMeshAdvertiseHost = ReadOption(args, "--client-cultmesh-advertise-host");
        var clientCultMeshPort = ReadNonNegativeInt(args, "--client-cultmesh-port") ?? 3076;
        var aetheriaResourcesRoot = ReadOption(args, "--aetheria-resources-root");
        var assetBundleRoot = ReadOption(args, "--asset-bundle-root");
        RejectRemovedOption(args, "--rts-cultmesh-port", "--client-cultmesh-port");
        RejectRemovedOption(args, "--peer-cultmesh-endpoint", "Odin-discovered CultMesh peer documents");
        RejectRemovedOption(args, "--odin-cultmesh-rudp", "--odin-cultmesh-uri");
        RejectRemovedOption(args, "--odin-cultnet-rudp", "--odin-cultmesh-uri");
        var odinCultMeshUri = ReadOption(args, "--odin-cultmesh-uri");
        var noOdinAnnouncements = HasFlag(args, "--no-odin-announcements");
        RejectRemovedOption(args, "--peer-sync-timeout-ms", "Odin-discovered CultMesh peer documents");
        var apiPublicationIntervalMs = ReadPositiveInt(args, "--api-publication-interval-ms") ?? 1000;
        var sessionId = ReadOption(args, "--session-id");

        return new AetheriaDaemonHostOptions
        {
            StatePath = string.IsNullOrWhiteSpace(state)
                ? AetheriaStatePaths.ResolveDefaultStatePath(root)
                : Path.GetFullPath(state),
            DaemonId = string.IsNullOrWhiteSpace(daemonId) ? "aetheria-daemon" : daemonId,
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? "local" : sessionId,
            VerseId = string.IsNullOrWhiteSpace(verseId) ? "aetheria.local" : verseId,
            CultMeshAddress = string.IsNullOrWhiteSpace(cultMeshAddress)
                ? "cultmesh://aetheria.local/eve/providers/aetheria.daemon"
                : cultMeshAddress,
            ClientCultMeshHost = string.IsNullOrWhiteSpace(clientCultMeshHost) ? "127.0.0.1" : clientCultMeshHost,
            ClientCultMeshAdvertiseHost = string.IsNullOrWhiteSpace(clientCultMeshAdvertiseHost)
                ? (string.IsNullOrWhiteSpace(clientCultMeshHost) || clientCultMeshHost == "0.0.0.0" || clientCultMeshHost == "*" ? "127.0.0.1" : clientCultMeshHost)
                : clientCultMeshAdvertiseHost,
            ClientCultMeshPort = clientCultMeshPort,
            AetheriaResourcesRoot = string.IsNullOrWhiteSpace(aetheriaResourcesRoot)
                ? Path.GetFullPath(Path.Combine(root, "Assets", "Resources"))
                : Path.GetFullPath(aetheriaResourcesRoot),
            AssetBundleRoot = string.IsNullOrWhiteSpace(assetBundleRoot)
                ? Path.GetFullPath(Path.Combine(root, "Build", "EveAssets"))
                : Path.GetFullPath(assetBundleRoot),
            OdinCultMeshUri = noOdinAnnouncements
                ? ""
                : string.IsNullOrWhiteSpace(odinCultMeshUri)
                ? "cultmesh://odin/rendezvous/provider-catalog"
                : odinCultMeshUri,
            EnableOdinAnnouncements = !noOdinAnnouncements,
            TickInterval = TimeSpan.FromMilliseconds(intervalMs),
            ApiPublicationInterval = TimeSpan.FromMilliseconds(apiPublicationIntervalMs),
            FixedDeltaSeconds = fixedDeltaMs / 1000.0,
            Once = HasFlag(args, "--once")
        };
    }

    private static string ReadOption(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return "";
    }

    private static IReadOnlyList<string> ReadOptions(IReadOnlyList<string> args, string name)
    {
        var values = new List<string>();
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                values.Add(args[i + 1].Trim());
            }
        }

        return values
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void RejectRemovedOption(IReadOnlyList<string> args, string removed, string replacement)
    {
        if (args.Any(arg => string.Equals(arg, removed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{removed} was removed. Use {replacement} with a cultmesh:// Odin route.");
        }
    }

    private static int? ReadPositiveInt(IReadOnlyList<string> args, string name)
    {
        return int.TryParse(ReadOption(args, name), out var value) && value > 0
            ? value
            : null;
    }

    private static int? ReadNonNegativeInt(IReadOnlyList<string> args, string name)
    {
        return int.TryParse(ReadOption(args, name), out var value) && value >= 0
            ? value
            : null;
    }

    private static bool HasFlag(IReadOnlyList<string> args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }
}
