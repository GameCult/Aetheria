using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;
using GameCult.Mesh;
using GameCult.Networking;
using MessagePack;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

var options = AetheriaDaemonHostOptions.Parse(args);
var startedAtUtc = DateTimeOffset.UtcNow.ToString("O");

Console.WriteLine($"Aetheria Verse daemon starting: {options.StatePath}");
Console.WriteLine($"Aetheria Verse daemon peers: {options.PeerCultMeshEndpoints.Count} [{string.Join(", ", options.PeerCultMeshEndpoints)}]");

await using var node = await AetheriaStateNode.OpenAsync(
    options.StatePath,
    runtimeId: options.DaemonId,
    startServer: true,
    enableDurableShardLogs: false).ConfigureAwait(false);
using var discoveryHost = new AetheriaVerseDiscoveryHost(node);

await EnsureWorldDocumentAsync(node).ConfigureAwait(false);
await EnsureTradeValuePolicyAsync(node, startedAtUtc).ConfigureAwait(false);
await EnsurePlayableRunDocumentsAsync(node, startedAtUtc).ConfigureAwait(false);
await EnsureStarbridgeSessionDocumentsAsync(node, options).ConfigureAwait(false);
var verseHost = await EnsureVerseHostSettingsAsync(node, options, startedAtUtc).ConfigureAwait(false);
await EnsureVerseAuthorityPolicyAsync(node, options).ConfigureAwait(false);
discoveryHost.Update(verseHost);
await PublishRuntimeSessionAsync(node, options, startedAtUtc, "starting").ConfigureAwait(false);
await PublishStateSurfacesAsync(node, options, startedAtUtc).ConfigureAwait(false);
var latestFrame = await node.LatestFrame().ReadAsync().ConfigureAwait(false);
using var cultMeshRudpHost = StartRtsCultMeshHost(node, options, () => latestFrame);
using var rtsPumpCancellation = new CancellationTokenSource();
var rtsPump = RunRtsCultMeshPumpAsync(cultMeshRudpHost, rtsPumpCancellation.Token);
var nextApiPublicationUtc = DateTimeOffset.UtcNow;
var firstTick = await TickAsync(node, options, latestFrame, buildPublications: true).ConfigureAwait(false);
ThrowIfRtsPumpFaulted(rtsPump);
latestFrame = firstTick.Frame;
nextApiPublicationUtc = DateTimeOffset.UtcNow.Add(options.ApiPublicationInterval);
Console.WriteLine($"Aetheria Verse daemon published frame {firstTick.Frame.FrameId}.");
Console.WriteLine($"Aetheria RTS CultMesh endpoint: rudp://{options.RtsCultMeshAdvertiseHost}:{cultMeshRudpHost.LocalEndPoint.Port}");

if (options.Once)
{
    await PublishRuntimeSessionAsync(node, options, startedAtUtc, "completed").ConfigureAwait(false);
    rtsPumpCancellation.Cancel();
    await rtsPump.ConfigureAwait(false);
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
    ThrowIfRtsPumpFaulted(rtsPump);
    var delay = nextTickUtc - DateTimeOffset.UtcNow;
    if (delay > TimeSpan.Zero)
    {
        var completed = await Task.WhenAny(stopped.Task, Task.Delay(delay)).ConfigureAwait(false);
        if (completed == stopped.Task)
            break;
    }

    var buildPublications = DateTimeOffset.UtcNow >= nextApiPublicationUtc;
    var tick = await TickAsync(node, options, latestFrame, buildPublications).ConfigureAwait(false);
    ThrowIfRtsPumpFaulted(rtsPump);
    latestFrame = tick.Frame;
    nextTickUtc += options.TickInterval;
    if (nextTickUtc < DateTimeOffset.UtcNow - options.TickInterval)
        nextTickUtc = DateTimeOffset.UtcNow;
    if (buildPublications)
    {
        nextApiPublicationUtc = DateTimeOffset.UtcNow.Add(options.ApiPublicationInterval);
        discoveryHost.Update(await node.VerseHostSettings().ReadAsync().ConfigureAwait(false));
        await PublishRuntimeSessionAsync(node, options, startedAtUtc, "running").ConfigureAwait(false);
    }
    if (tick.Frame.FrameId % 120 == 0)
        Console.WriteLine($"Aetheria Verse daemon published frame {tick.Frame.FrameId} at {tick.Frame.SimulationTimeSeconds:0.00}s.");
}

await PublishRuntimeSessionAsync(node, options, startedAtUtc, "stopping").ConfigureAwait(false);
rtsPumpCancellation.Cancel();
await rtsPump.ConfigureAwait(false);
Console.WriteLine("Aetheria Verse daemon stopping.");

static void ThrowIfRtsPumpFaulted(Task rtsPump)
{
    if (!rtsPump.IsCompleted)
        return;

    if (rtsPump.IsFaulted)
        throw new InvalidOperationException("Aetheria RTS CultMesh pump faulted.", rtsPump.Exception);

    throw new InvalidOperationException("Aetheria RTS CultMesh pump stopped unexpectedly.");
}

static async Task<AetheriaRuntimeDaemonTickResult> TickAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    AetheriaRuntimeDaemonFrameDocument? currentFrame,
    bool buildPublications)
{
    await AcceptEveCommandsAsync(node, options).ConfigureAwait(false);

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
        : await ReadRuntimeRunCheckpointAsync(node).ConfigureAwait(false) ?? new AetheriaRuntimeRunCheckpointCommit();

    var loadoutTemplates = node.Cache
        .GetAll<AetheriaLoadoutTemplate>()
        .Select(ToLoadoutTemplateCommit)
        .ToArray();
    var observedCommands = node.ReadObservedDaemonCommands();
    var accountedCommandIds = new HashSet<string>(
        currentFrame?.AccountedCommandIds ?? Array.Empty<string>(),
        StringComparer.Ordinal);
    var pendingObservedCommands = observedCommands
        .Where(command => command != null && !accountedCommandIds.Contains(command.CommandId ?? ""))
        .ToArray();
    var policyRejectedCommandIds = new List<string>();
    var authorityPolicy = await node.VerseAuthorityPolicy().ReadAsync().ConfigureAwait(false);
    var starbridgeScenario = await node.StarbridgeScenario().ReadAsync().ConfigureAwait(false);
    var starbridgeSession = await node.StarbridgeSession().ReadAsync().ConfigureAwait(false);
    var authorityLeases = node.ReadAuthorityLeases();
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
            StarbridgeScenario = starbridgeScenario,
            StarbridgeSession = starbridgeSession,
            BuildPublications = buildPublications,
            OperationContext = new AetheriaRuntimeDaemonOperationContext
            {
                LoadoutTemplates = loadoutTemplates
            }
        });
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

    if (options.PeerCultMeshEndpoints.Count > 0)
    {
        result = await ImportRemoteCommittedFactsAsync(
            node,
            options,
            result.Frame,
            result,
            authorityPolicy,
            authorityLeases).ConfigureAwait(false);
    }

    if (buildPublications)
    {
        await PublishDaemonApiDocumentsAsync(node, result).ConfigureAwait(false);
        await PublishStateSurfacesAsync(node, options, result.Frame.PublishedAtUtc).ConfigureAwait(false);
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

        await node.PutCommittedCommandFactAsync(
            AetheriaRuntimeCommittedCommandFactDocument.FromAppliedCommand(
                frame,
                command,
                options.VerseId)).ConfigureAwait(false);
    }

    var policyRejectedIds = new HashSet<string>(
        policyRejectedCommandIds ?? Array.Empty<string>(),
        StringComparer.Ordinal);
    foreach (var commandId in frame.RejectedCommandIds ?? Array.Empty<string>())
    {
        if (string.IsNullOrWhiteSpace(commandId) ||
            !policyRejectedIds.Contains(commandId) ||
            !byCommandId.TryGetValue(commandId, out var command))
            continue;

        await node.PutCommittedCommandFactAsync(
            AetheriaRuntimeCommittedCommandFactDocument.FromRejectedCommand(
                frame,
                command,
                options.VerseId)).ConfigureAwait(false);
    }
}

static async Task<AetheriaRuntimeDaemonTickResult> ImportRemoteCommittedFactsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    AetheriaRuntimeDaemonFrameDocument frame,
    AetheriaRuntimeDaemonTickResult currentResult,
    AetheriaRuntimeVerseAuthorityPolicyDocument? authorityPolicy,
    IReadOnlyList<AetheriaRuntimeAuthorityLeaseDocument> authorityLeases)
{
    var remoteFacts = new List<AetheriaRuntimeCommittedCommandFactDocument>();
    foreach (var endpoint in options.PeerCultMeshEndpoints)
    {
        try
        {
            var endpointFacts = await AetheriaVerseReplica.FetchScopedDocumentsAsync<AetheriaRuntimeCommittedCommandFactDocument>(
                endpoint,
                schemaIds:
                [
                    AetheriaRuntimeDaemonSchemas.CommittedCommandFact
                ],
                connectTimeout: options.PeerSyncTimeout,
                responseTimeout: options.PeerSyncTimeout).ConfigureAwait(false);
            remoteFacts.AddRange(endpointFacts);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is SocketException ||
            ex is TimeoutException ||
            ex is InvalidOperationException)
        {
            Console.WriteLine($"Aetheria peer fact sync skipped for {endpoint}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    var facts = remoteFacts
        .Where(fact => fact != null)
        .Where(fact => string.Equals(fact.VerseId, options.VerseId, StringComparison.Ordinal))
        .Where(fact => !string.Equals(fact.SourceDaemonId, options.DaemonId, StringComparison.Ordinal))
        .GroupBy(fact => fact.FactId ?? "", StringComparer.Ordinal)
        .Where(group => !string.IsNullOrWhiteSpace(group.Key))
        .Select(group => group.First())
        .ToArray();
    if (facts.Length == 0)
        return currentResult;

    var importedFactIds = (frame.CumulativeImportedFactIds ?? Array.Empty<string>())
        .Concat(frame.CumulativeRejectedImportedFactIds ?? Array.Empty<string>())
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    var import = AetheriaRuntimeCommittedFactImporter.ImportIntoFrame(
        node.StatePath,
        frame,
        facts,
        authorityPolicy,
        authorityLeases,
        options.DaemonId,
        options.DaemonId,
        frame.SessionId,
        options.VerseId,
        importedFactIds,
        node.RuntimeCatalog().Latest());

    var importedFrame = import.Frame;
    importedFrame.ImportedFactIds = import.AcceptedFactIds;
    importedFrame.RejectedImportedFactIds = import.RejectedFactIds;
    importedFrame.DuplicateImportedFactIds = import.DuplicateFactIds;
    importedFrame.CumulativeImportedFactIds = (frame.CumulativeImportedFactIds ?? Array.Empty<string>())
        .Concat(import.AcceptedFactIds)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    importedFrame.CumulativeRejectedImportedFactIds = (frame.CumulativeRejectedImportedFactIds ?? Array.Empty<string>())
        .Concat(import.RejectedFactIds)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    importedFrame.CumulativeAppliedCommandIds = (frame.CumulativeAppliedCommandIds ?? Array.Empty<string>())
        .Concat(importedFrame.CumulativeAppliedCommandIds ?? Array.Empty<string>())
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    importedFrame.CumulativeRejectedCommandIds = (frame.CumulativeRejectedCommandIds ?? Array.Empty<string>())
        .Concat(importedFrame.CumulativeRejectedCommandIds ?? Array.Empty<string>())
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    if (import.AcceptedFactIds.Count > 0 || import.RejectedFactIds.Count > 0)
    {
        Console.WriteLine(
            $"Aetheria imported peer facts: accepted={import.AcceptedFactIds.Count}, rejected={import.RejectedFactIds.Count}, duplicates={import.DuplicateFactIds.Count}.");
    }

    return import.Tick;
}

static RudpCultNetSchemaServer StartRtsCultMeshHost(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    Func<AetheriaRuntimeDaemonFrameDocument?> latestFrame)
{
    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    socket.Bind(new IPEndPoint(ParseBindAddress(options.RtsCultMeshHost), options.RtsCultMeshPort));
    var server = new RudpCultNetSchemaServer(new RudpCultNetSchemaServerOptions
    {
        RuntimeId = $"{options.DaemonId}.rts",
        Socket = socket,
        ConnectionId = 0x43554c54,
        TransportId = "aetheria-rts-rudp",
        MaxFragmentBytes = 1200,
        MaxPendingReliablePackets = 512
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

            var health = await node.Health().ReadAsync().ConfigureAwait(false);
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

            var authorityPolicy = await node.VerseAuthorityPolicy().ReadAsync().ConfigureAwait(false);
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

            var starbridgeScenario = await node.StarbridgeScenario().ReadAsync().ConfigureAwait(false);
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

            var starbridgeSession = await node.StarbridgeSessionSummary().ReadAsync().ConfigureAwait(false);
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

            var factPuts = node.ReadCommittedCommandFacts()
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

            if (hasFrame && frame != null && TryGetRtsViewportRequest(request, out var viewportRecordKey, out var viewport))
            {
                var viewportDocument = AetheriaRuntimeRtsProjection.ProjectViewport(frame, viewport);
                var viewportPut = node.Database.Documents.CreateRawDocumentPutMessage(
                    response.MessageId,
                    new CultRecordHandle<AetheriaRuntimeRtsViewportDocument>(
                        new CultRecordKey(viewportRecordKey)),
                    viewportDocument,
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

static async Task InjectEveSurfaceSnapshotAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    CultNetSnapshotRequestMessage request,
    CultNetSnapshotResponseRawMessage response,
    string recordKey,
    string surfaceKind)
{
    const string EveSurfaceSchema = "gamecult.eve.surface.v1";
    if (!SnapshotWants(request, EveSurfaceSchema, recordKey))
        return;

    var surfaceState = await ReadEveSurfacePublicationAsync(node, surfaceKind).ConfigureAwait(false);
    if (surfaceState == null)
        return;

    var surfacePut = node.Database.Documents.CreateRawDocumentPutMessage(
        response.MessageId,
        new CultRecordHandle<EveSurfaceState>(new CultRecordKey(recordKey)),
        surfaceState,
        new CultNetDocumentMessageOptions
        {
            SourceRuntimeId = options.DaemonId,
            SourceRole = "aetheria-daemon"
        });
    response.Documents = response.Documents
        .Where(document => !string.Equals(document.SchemaId, surfacePut.Document.SchemaId, StringComparison.Ordinal) ||
            !string.Equals(document.RecordKey, surfacePut.Document.RecordKey, StringComparison.Ordinal))
        .Concat(new[] { surfacePut.Document })
        .ToArray();
}

static Task<EveSurfaceState?> ReadEveSurfacePublicationAsync(AetheriaStateNode node, string surfaceKind)
{
    return surfaceKind switch
    {
        "game" => node.DaemonGameSurface().ReadAsync(),
        "game-tui" => node.DaemonGameTuiSurface().ReadAsync(),
        "editor" => node.DaemonEditorSurface().ReadAsync(),
        "editor-tui" => node.DaemonEditorTuiSurface().ReadAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(surfaceKind), surfaceKind, "Unknown Eve surface publication.")
    };
}

static async Task RunRtsCultMeshPumpAsync(
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

static bool TryGetRtsViewportRequest(
    CultNetSnapshotRequestMessage request,
    out string recordKey,
    out AetheriaRuntimeRtsViewportBounds viewport)
{
    const string prefix = "daemon:aetheria.rts.viewport.v1;";
    recordKey = "";
    viewport = new AetheriaRuntimeRtsViewportBounds();

    var schemaIds = request.SchemaIds ?? Array.Empty<string>();
    if (schemaIds.Length > 0 && !schemaIds.Contains(AetheriaRuntimeDaemonSchemas.RtsViewport, StringComparer.Ordinal))
        return false;

    foreach (var candidate in request.RecordKeys ?? Array.Empty<string>())
    {
        if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
            continue;

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
        viewport = new AetheriaRuntimeRtsViewportBounds
        {
            MinX = Math.Min(minX, maxX),
            MinY = Math.Min(minY, maxY),
            MaxX = Math.Max(minX, maxX),
            MaxY = Math.Max(minY, maxY)
        };
        return true;
    }

    return false;
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
    AetheriaRuntimeDaemonTickResult result)
{
    await node.LatestFrame()
        .ReplaceAsync(result.Frame)
        .ConfigureAwait(false);
    if (result.SoaView != null &&
        string.Equals(result.SoaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
    {
        await node.LatestSoaView()
            .ReplaceAsync(result.SoaView)
            .ConfigureAwait(false);
    }

    if (result.ProviderAdvertisement != null)
        await node.ProviderAdvertisement()
            .ReplaceAsync(result.ProviderAdvertisement)
            .ConfigureAwait(false);
    if (result.Health != null)
        await node.Health()
            .ReplaceAsync(result.Health)
            .ConfigureAwait(false);
    if (result.CommandBoundary != null)
        await node.CommandBoundary()
            .ReplaceAsync(result.CommandBoundary)
            .ConfigureAwait(false);
    if (result.StarbridgeSessionSummary != null)
        await node.StarbridgeSessionSummary()
            .ReplaceAsync(result.StarbridgeSessionSummary)
            .ConfigureAwait(false);
    if (result.GameSurface != null)
        await node.DaemonGameSurface()
            .ReplaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(result.GameSurface))
            .ConfigureAwait(false);
    if (result.GameTuiSurface != null)
        await node.DaemonGameTuiSurface()
            .ReplaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(result.GameTuiSurface))
            .ConfigureAwait(false);
    if (result.EditorSurface != null)
        await node.DaemonEditorSurface()
            .ReplaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(result.EditorSurface))
            .ConfigureAwait(false);
    if (result.EditorTuiSurface != null)
        await node.DaemonEditorTuiSurface()
            .ReplaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(result.EditorTuiSurface))
            .ConfigureAwait(false);
}

static async Task AcceptEveCommandsAsync(AetheriaStateNode node, AetheriaDaemonHostOptions options)
{
    var commandCountBefore = node.ReadObservedEveCommands().Count;
    var now = DateTimeOffset.UtcNow.ToString("O");
    try
    {
        var existingStatus = await node.EveCommandAcceptanceStatus().ReadAsync().ConfigureAwait(false);
        var report = await AetheriaEveCommandBridge.AcceptObservedAsync(
                node,
                existingStatus?.AccountedCommandIds)
            .ConfigureAwait(false);
        await node.EveCommandAcceptanceStatus()
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
        var existing = await node.EveCommandAcceptanceStatus().ReadAsync().ConfigureAwait(false);
        await node.EveCommandAcceptanceStatus()
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
    var eveStatus = await node.EveCommandAcceptanceStatus().ReadAsync().ConfigureAwait(false);
    var runtimeSession = await node.RuntimeSession(options.DaemonId).ReadAsync().ConfigureAwait(false);
    var playerSettings = await node.PlayerSettings().ReadAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
    var playerSettingsUpdatedAt = string.IsNullOrWhiteSpace(playerSettings.LastUpdatedAtUtc)
        ? updatedAtUtc
        : playerSettings.LastUpdatedAtUtc;

    await node.OperationsSurface()
        .ReplaceAsync(AetheriaOperationsSurfaceProjector.Build(eveStatus, verseHost, runtimeSession))
        .ConfigureAwait(false);
    await node.PlayerSettingsSurface()
        .ReplaceAsync(AetheriaPlayerSettingsSurfaceProjector.Build(playerSettings, playerSettingsUpdatedAt))
        .ConfigureAwait(false);
    await node.ProviderAdvertisementSurface()
        .ReplaceAsync(AetheriaProviderAdvertisementProjector.Build(verseHost, node.StatePath, updatedAtUtc))
        .ConfigureAwait(false);
    await node.FlushAsync().ConfigureAwait(false);
}

static async Task PublishRuntimeSessionAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string startedAtUtc,
    string status)
{
    var now = DateTimeOffset.UtcNow.ToString("O");
    await node.RuntimeSession(options.DaemonId)
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
    var world = node.World();
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
    var tradeValuePolicy = node.TradeValuePolicy();
    var existing = await tradeValuePolicy.ReadAsync().ConfigureAwait(false);
    if (existing != null)
        return;

    await tradeValuePolicy.ReplaceAsync(
        AetheriaRuntimeStateMapper.ToTradeValuePolicy(
            AetheriaRuntimeTradeValueSettings.Default,
            now)).ConfigureAwait(false);
}

static async Task EnsurePlayableRunDocumentsAsync(AetheriaStateNode node, string now)
{
    var settings = await node.PlayerSettings().ReadAsync().ConfigureAwait(false);
    if (!string.IsNullOrWhiteSpace(settings?.ActiveRunKey))
    {
        var existingRun = await ReadRuntimeRunCheckpointAsync(node).ConfigureAwait(false);
        if (HasPlayableRun(existingRun) && HasRtsScenario(existingRun))
            return;
    }

    const string runId = "local-rts";
    var runKey = new CultRecordKey("global:aetheria.run_state.local-rts.v1");
    var zoneKey = new CultRecordKey("global:aetheria.zone_state.local-rts.0.v1");
    var entityKeys = Enumerable.Range(0, 8)
        .Select(index => EntityKey(runId, 0, index))
        .ToArray();

    settings ??= new AetheriaPlayerSettings();
    settings.ActiveRunKey = runKey.ToString();
    settings.PlayerName = string.IsNullOrWhiteSpace(settings.PlayerName) ? "Codex RTS" : settings.PlayerName;
    settings.TutorialPassed = true;
    settings.LastUpdatedAtUtc = now;
    await node.PlayerSettings()
        .ReplaceAsync(settings)
        .ConfigureAwait(false);

    await node.RunState(runKey).ReplaceAsync(new AetheriaRunState
    {
        RunId = runId,
        EntranceZoneIndex = 0,
        ExitZoneIndex = 0,
        CurrentZoneIndex = 0,
        DiscoveredZoneIndices = [0],
        ZoneKeys = [zoneKey.ToString()],
        GenerationSeed = 0xA37E_12u,
        CurrentEntityKey = entityKeys[0],
        UpdatedAtUtc = now
    }).ConfigureAwait(false);

    await node.ZoneState(zoneKey).ReplaceAsync(new AetheriaZoneState
    {
        Name = "Daemon Local Testbed",
        Position = Vec2(0, 0),
        EntityKeys = entityKeys,
        FactionIndices = [0, 1],
        OwnerFactionIndex = 0,
        Orbits =
        [
            new AetheriaOrbitSnapshot
            {
                OrbitKey = "local.sun.orbit",
                FixedPosition = Vec2(0, 0)
            },
            new AetheriaOrbitSnapshot
            {
                OrbitKey = "local.belt.orbit",
                ParentOrbitKey = "local.sun.orbit",
                FixedPosition = Vec2(360, -180),
                Distance = 402,
                Phase = 0.33
            }
        ],
        Bodies =
        [
            new AetheriaBodySnapshot
            {
                BodyKey = "local.sun",
                Kind = "sun",
                Name = "Local Sun",
                OrbitKey = "local.sun.orbit",
                Mass = 1000,
                BodyRadiusMultiplier = 2.2,
                GravityInfluenceCenterX = 0,
                GravityInfluenceCenterZ = 0,
                GravityInfluenceRadius = 900,
                GravityWellDepth = -80,
                GravityDepthExponent = 3,
                GravityWaveRadius = 450,
                GravityWaveDepth = 10,
                GravityWaveSpeed = 2,
                SunVisual = new AetheriaSunVisualState
                {
                    LightColor = Vec3(1, 0.86, 0.58),
                    FogTintColor = Vec3(0.45, 0.28, 0.16),
                    LightRadiusMultiplier = 1.8
                }
            },
            new AetheriaBodySnapshot
            {
                BodyKey = "local.belt",
                Kind = "asteroid_belt",
                Name = "Cinder Belt",
                OrbitKey = "local.belt.orbit",
                Mass = 120,
                BodyRadiusMultiplier = 1.0,
                GravityInfluenceCenterX = 360,
                GravityInfluenceCenterZ = -180,
                GravityInfluenceRadius = 260,
                GravityWellDepth = -22,
                GravityDepthExponent = 2.2,
                GravityWaveRadius = 120,
                GravityWaveDepth = 4,
                GravityWaveSpeed = 0.8,
                Asteroids =
                [
                    new AetheriaAsteroidSnapshot { Distance = 42, Phase = 0.10, Size = 1.1, RotationSpeed = 0.2 },
                    new AetheriaAsteroidSnapshot { Distance = 78, Phase = 1.70, Size = 0.8, RotationSpeed = -0.1 },
                    new AetheriaAsteroidSnapshot { Distance = 118, Phase = 3.35, Size = 1.6, RotationSpeed = 0.08 }
                ]
            }
        ],
        GravityTerrainRadius = 1200,
        GravityTerrainDepth = -8,
        GravityTerrainDepthExponent = 1.2,
        GravityTerrainWaveFrequency = 0.6
    }).ConfigureAwait(false);

    await node.EntitySnapshot(new CultRecordKey(entityKeys[0])).ReplaceAsync(SeedEntity(
        "Anchor Station",
        "station",
        Vec3(-220, 0, -90),
        Vec2(1, 0),
        Vec2(0, 0),
        "player",
        "anchor-station-hull",
        720,
        entityKeys[4],
        [
            Contact(entityKeys[1], 1, false),
            Contact(entityKeys[2], 1, false),
            Contact(entityKeys[3], 1, false),
            Contact(entityKeys[4], 1, true),
            Contact(entityKeys[5], 0.8, true),
            Contact(entityKeys[7], 0.6, false)
        ],
        ["repair-parts", "reactor-fuel", "drone-core"])).ConfigureAwait(false);
    await node.EntitySnapshot(new CultRecordKey(entityKeys[1])).ReplaceAsync(SeedEntity(
        "Vanguard One",
        "ship",
        Vec3(-60, 0, -40),
        Vec2(1, 0.2),
        Vec2(12, 4),
        "player",
        "ship-hull",
        520,
        entityKeys[4],
        [
            Contact(entityKeys[0], 1, false),
            Contact(entityKeys[2], 1, false),
            Contact(entityKeys[4], 1, true),
            Contact(entityKeys[5], 0.85, true)
        ],
        ["coilgun-ammo", "field-rations"])).ConfigureAwait(false);
    await node.EntitySnapshot(new CultRecordKey(entityKeys[2])).ReplaceAsync(SeedEntity(
        "Wing Two",
        "ship",
        Vec3(180, 0, 120),
        Vec2(-0.4, 0.9),
        Vec2(-6, 8),
        "player",
        "frigate-hull",
        420,
        entityKeys[4],
        [
            Contact(entityKeys[0], 1, false),
            Contact(entityKeys[1], 1, false),
            Contact(entityKeys[4], 0.85, true),
            Contact(entityKeys[6], 0.6, true)
        ],
        ["sensor-buoy", "shield-cell"])).ConfigureAwait(false);
    await node.EntitySnapshot(new CultRecordKey(entityKeys[3])).ReplaceAsync(SeedEntity(
        "Torch Three",
        "ship",
        Vec3(-160, 0, 210),
        Vec2(0.6, -0.8),
        Vec2(8, -5),
        "player",
        "interceptor-hull",
        480,
        entityKeys[5],
        [
            Contact(entityKeys[0], 1, false),
            Contact(entityKeys[1], 1, false),
            Contact(entityKeys[5], 0.9, true)
        ],
        ["micro-missile", "coolant-pack"])).ConfigureAwait(false);
    await node.EntitySnapshot(new CultRecordKey(entityKeys[4])).ReplaceAsync(SeedEntity(
        "Ash Raider",
        "ship",
        Vec3(420, 0, 180),
        Vec2(-0.8, -0.1),
        Vec2(-4, -2),
        "raider",
        "raider-hull",
        300,
        entityKeys[1],
        [
            Contact(entityKeys[0], 1, true),
            Contact(entityKeys[1], 1, true),
            Contact(entityKeys[2], 0.5, true)
        ],
        ["scrap-metal", "stolen-capacitor"])).ConfigureAwait(false);
    await node.EntitySnapshot(new CultRecordKey(entityKeys[5])).ReplaceAsync(SeedEntity(
        "Cinder Knife",
        "ship",
        Vec3(560, 0, -110),
        Vec2(-0.9, 0.2),
        Vec2(-7, 1),
        "raider",
        "raider-interceptor-hull",
        260,
        entityKeys[3],
        [
            Contact(entityKeys[0], 0.8, true),
            Contact(entityKeys[3], 1, true)
        ],
        ["volatile-fuel"])).ConfigureAwait(false);
    await node.EntitySnapshot(new CultRecordKey(entityKeys[6])).ReplaceAsync(SeedEntity(
        "Blackwake",
        "ship",
        Vec3(680, 0, 260),
        Vec2(-0.7, -0.4),
        Vec2(-4, -3),
        "raider",
        "raider-heavy-hull",
        220,
        entityKeys[2],
        [
            Contact(entityKeys[2], 0.8, true)
        ],
        ["ore-cache", "burned-relay-core"])).ConfigureAwait(false);
    await node.EntitySnapshot(new CultRecordKey(entityKeys[7])).ReplaceAsync(SeedEntity(
        "Derelict Relay",
        "station",
        Vec3(-260, 0, 260),
        Vec2(0, 1),
        Vec2(0, 0),
        "neutral",
        "station-hull",
        140,
        "",
        [],
        ["ancient-transponder"])).ConfigureAwait(false);

    await node.FlushAsync().ConfigureAwait(false);
}

static async Task EnsureStarbridgeSessionDocumentsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options)
{
    var existingScenario = await node.StarbridgeScenario().ReadAsync().ConfigureAwait(false);
    var existingSession = await node.StarbridgeSession().ReadAsync().ConfigureAwait(false);
    if (existingScenario != null &&
        string.Equals(existingScenario.Schema, AetheriaRuntimeDaemonSchemas.StarbridgeScenario, StringComparison.Ordinal) &&
        existingSession != null &&
        string.Equals(existingSession.Schema, AetheriaRuntimeDaemonSchemas.StarbridgeSession, StringComparison.Ordinal))
    {
        return;
    }

    const string runId = "local-rts";
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
                RuntimeId = "starfire-rts",
                Role = "commander"
            },
            new AetheriaRuntimeStarbridgeRuntimeRole
            {
                RuntimeId = "raven-unity",
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

    await node.StarbridgeScenario()
        .ReplaceAsync(scenario)
        .ConfigureAwait(false);
    await node.StarbridgeSession()
        .ReplaceAsync(session)
        .ConfigureAwait(false);
    await node.FlushAsync().ConfigureAwait(false);
}

static async Task EnsureVerseAuthorityPolicyAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options)
{
    var existing = await node.VerseAuthorityPolicy().ReadAsync().ConfigureAwait(false);
    if (existing != null && string.Equals(existing.Schema, AetheriaRuntimeVerseAuthoritySchemas.Policy, StringComparison.Ordinal))
        return;

    var policy = AetheriaRuntimeVerseAuthorityPolicyDocument.TrustedCoop(options.VerseId, options.DaemonId);
    await node.VerseAuthorityPolicy()
        .ReplaceAsync(policy)
        .ConfigureAwait(false);
}

static async Task<AetheriaRuntimeRunCheckpointCommit?> ReadRuntimeRunCheckpointAsync(AetheriaStateNode node)
{
    var settings = await node.PlayerSettings().ReadAsync().ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(settings?.ActiveRunKey))
        return null;

    var run = await node.RunState(new CultRecordKey(settings.ActiveRunKey)).ReadAsync().ConfigureAwait(false);
    if (run == null)
        return null;

    var zones = new List<AetheriaRuntimeZoneSnapshotCommit>();
    var zoneKeys = run.ZoneKeys ?? Array.Empty<string>();
    for (var zoneIndex = 0; zoneIndex < zoneKeys.Length; zoneIndex++)
    {
        var zone = await node.ZoneState(new CultRecordKey(zoneKeys[zoneIndex])).ReadAsync().ConfigureAwait(false);
        if (zone == null)
            continue;

        zones.Add(await ToRuntimeZoneAsync(node, zone, zoneIndex).ConfigureAwait(false));
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
    int zoneIndex)
{
    var entityKeys = zone.EntityKeys ?? Array.Empty<string>();
    var entityIndices = entityKeys
        .Select((key, index) => new { key, index })
        .Where(pair => !string.IsNullOrWhiteSpace(pair.key))
        .ToDictionary(pair => pair.key, pair => pair.index, StringComparer.Ordinal);
    var entities = new List<AetheriaRuntimeEntitySnapshotCommit>();
    for (var entityIndex = 0; entityIndex < entityKeys.Length; entityIndex++)
    {
        var entity = await node.EntitySnapshot(new CultRecordKey(entityKeys[entityIndex])).ReadAsync().ConfigureAwait(false);
        if (entity != null)
            entities.Add(ToRuntimeEntity(entity, entityIndex, entityIndices));
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
        Bodies = (zone.Bodies ?? Array.Empty<AetheriaBodySnapshot>()).Select(body => ToRuntimeBody(body, entityIndices)).ToArray(),
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
    IReadOnlyDictionary<string, int> entityIndices)
{
    return new AetheriaRuntimeEntitySnapshotCommit
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
        Equipment = ToEntitySlotCommits(entity.Equipment),
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
        BehaviorStates = Array.Empty<AetheriaRuntimeBehaviorStateCommit>(),
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
    IReadOnlyDictionary<string, int> entityIndices)
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
        GravityWaveSpeed = body.GravityWaveSpeed
    };
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

static bool HasRtsScenario(AetheriaRuntimeRunCheckpointCommit? run)
{
    var entities = (run?.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
        .SelectMany(zone => zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
        .ToArray();
    return entities.Any(entity => string.Equals(entity.Name, "Anchor Station", StringComparison.Ordinal)) &&
        entities.Count(entity => string.Equals(entity.FactionKey, "player", StringComparison.OrdinalIgnoreCase)) >= 4 &&
        entities.Count(entity => string.Equals(entity.FactionKey, "raider", StringComparison.OrdinalIgnoreCase)) >= 3;
}

static AetheriaEntitySnapshot SeedEntity(
    string name,
    string kind,
    AetheriaVector3 position,
    AetheriaVector2 direction,
    AetheriaVector2 velocity,
    string factionKey,
    string hullItemKey,
    double visibility,
    string targetEntityKey,
    AetheriaEntityContactSnapshot[] contacts,
    string[] cargoItems)
{
    return new AetheriaEntitySnapshot
    {
        Name = name,
        Kind = kind,
        Position = position,
        Direction = direction,
        Velocity = velocity,
        FactionKey = factionKey,
        HullItemKey = hullItemKey,
        IsActive = true,
        HeatsinksEnabled = true,
        TractorPower = 1,
        Visibility = visibility,
        VisibilitySourceCount = 1,
        TargetEntityKey = targetEntityKey,
        Contacts = contacts,
        StatGrids =
        [
            StatGrid("hull", string.Equals(kind, "station", StringComparison.OrdinalIgnoreCase) ? 420 : string.Equals(factionKey, "raider", StringComparison.OrdinalIgnoreCase) ? 80 : 120),
            StatGrid("shield", string.Equals(kind, "station", StringComparison.OrdinalIgnoreCase) ? 120 : 45),
            StatGrid("heat", 0)
        ],
        Equipment =
        [
            new AetheriaEntityItemSlot
            {
                Position = new AetheriaGridCoord { X = 0, Y = 0 },
                ItemKey = "sensor-array",
                Quality = 1,
                Durability = 1,
                Quantity = 1,
                Enabled = true
            }
        ],
        CargoContents =
        [
            new AetheriaCargoBayLoadout
            {
                Items = cargoItems
                    .Select((itemKey, index) => new AetheriaLoadoutItemSlot
                    {
                        Position = new AetheriaGridCoord { X = index % 4, Y = index / 4 },
                        Item = new AetheriaLoadoutItem
                        {
                            ItemKey = itemKey,
                            Quality = 1,
                            Durability = 1,
                            Quantity = 1,
                            Enabled = true
                        }
                    })
                    .ToArray()
            }
        ]
    };
}

static AetheriaEntityStatGrid StatGrid(string name, double value)
{
    return new AetheriaEntityStatGrid
    {
        Name = name,
        Width = 1,
        Height = 1,
        Values = [value]
    };
}

static AetheriaEntityContactSnapshot Contact(string targetEntityKey, double infoGathered, bool hostile)
{
    return new AetheriaEntityContactSnapshot
    {
        TargetEntityKey = targetEntityKey,
        InfoGathered = infoGathered,
        Hostile = hostile,
        Visible = true
    };
}

static string EntityKey(string runId, int zoneIndex, int entityIndex)
{
    return $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
}

static AetheriaVector2 Vec2(double x, double y)
{
    return new AetheriaVector2 { X = x, Y = y };
}

static AetheriaVector3 Vec3(double x, double y, double z)
{
    return new AetheriaVector3 { X = x, Y = y, Z = z };
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
    var existing = await node.VerseHostSettings().ReadAsync().ConfigureAwait(false);
    var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(existing);
    normalized.ServiceId = options.DaemonId;
    normalized.VerseId = options.VerseId;
    normalized.CultMeshAddress = options.CultMeshAddress;

    if (existing == null ||
        string.IsNullOrWhiteSpace(existing.LastUpdatedAtUtc) ||
        !AetheriaVerseHostSettingsNormalizer.Equivalent(existing, normalized))
    {
        normalized.LastUpdatedAtUtc = now;
        await node.VerseHostSettings()
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
    public string RtsCultMeshHost { get; init; } = "127.0.0.1";
    public string RtsCultMeshAdvertiseHost { get; init; } = "127.0.0.1";
    public int RtsCultMeshPort { get; init; } = 3076;
    public IReadOnlyList<string> PeerCultMeshEndpoints { get; init; } = Array.Empty<string>();
    public TimeSpan PeerSyncTimeout { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(20);
    public TimeSpan ApiPublicationInterval { get; init; } = TimeSpan.FromSeconds(1);
    public double FixedDeltaSeconds { get; init; } = 0.02;
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
        var rtsCultMeshHost = ReadOption(args, "--rts-cultmesh-host");
        var rtsCultMeshAdvertiseHost = ReadOption(args, "--rts-cultmesh-advertise-host");
        var rtsCultMeshPort = ReadNonNegativeInt(args, "--rts-cultmesh-port") ?? 3076;
        var peerCultMeshEndpoints = ReadOptions(args, "--peer-cultmesh-endpoint");
        var peerSyncTimeoutMs = ReadPositiveInt(args, "--peer-sync-timeout-ms") ?? 250;
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
            RtsCultMeshHost = string.IsNullOrWhiteSpace(rtsCultMeshHost) ? "127.0.0.1" : rtsCultMeshHost,
            RtsCultMeshAdvertiseHost = string.IsNullOrWhiteSpace(rtsCultMeshAdvertiseHost)
                ? (string.IsNullOrWhiteSpace(rtsCultMeshHost) || rtsCultMeshHost == "0.0.0.0" || rtsCultMeshHost == "*" ? "127.0.0.1" : rtsCultMeshHost)
                : rtsCultMeshAdvertiseHost,
            RtsCultMeshPort = rtsCultMeshPort,
            PeerCultMeshEndpoints = peerCultMeshEndpoints,
            PeerSyncTimeout = TimeSpan.FromMilliseconds(peerSyncTimeoutMs),
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
