using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

var options = AetheriaDaemonHostOptions.Parse(args);
var startedAtUtc = DateTimeOffset.UtcNow.ToString("O");

Console.WriteLine($"Aetheria Verse daemon starting: {options.StatePath}");

await using var node = await AetheriaStateNode.OpenAsync(
    options.StatePath,
    runtimeId: options.DaemonId,
    startServer: true).ConfigureAwait(false);
using var discoveryHost = new AetheriaVerseDiscoveryHost(node);

await EnsureWorldDocumentAsync(node).ConfigureAwait(false);
var verseHost = await EnsureVerseHostSettingsAsync(node, options, startedAtUtc).ConfigureAwait(false);
discoveryHost.Update(verseHost);
await PublishRuntimeSessionAsync(node, options, startedAtUtc, "starting").ConfigureAwait(false);
await PublishStateSurfacesAsync(node, options, startedAtUtc).ConfigureAwait(false);
var firstTick = await TickAsync(node, options).ConfigureAwait(false);
Console.WriteLine($"Aetheria Verse daemon published frame {firstTick.Frame.FrameId}.");

if (options.Once)
{
    await PublishRuntimeSessionAsync(node, options, startedAtUtc, "completed").ConfigureAwait(false);
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

while (!stopped.Task.IsCompleted)
{
    var completed = await Task.WhenAny(stopped.Task, Task.Delay(options.TickInterval)).ConfigureAwait(false);
    if (completed == stopped.Task)
        break;

    var tick = await TickAsync(node, options).ConfigureAwait(false);
    discoveryHost.Update(await node.GetVerseHostSettingsAsync().ConfigureAwait(false));
    await PublishRuntimeSessionAsync(node, options, startedAtUtc, "running").ConfigureAwait(false);
    Console.WriteLine($"Aetheria Verse daemon published frame {tick.Frame.FrameId}.");
}

await PublishRuntimeSessionAsync(node, options, startedAtUtc, "stopping").ConfigureAwait(false);
Console.WriteLine("Aetheria Verse daemon stopping.");

static async Task<AetheriaRuntimeDaemonTickResult> TickAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options)
{
    await AcceptEveCommandsAsync(node, options).ConfigureAwait(false);

    var currentFrame = AetheriaRuntimeDaemonFrameStore.TryReadFrame(node.StatePath, out var frame)
        ? frame
        : null;
    var fixedDeltaSeconds = currentFrame?.FixedDeltaSeconds > 0
        ? currentFrame.FixedDeltaSeconds
        : options.FixedDeltaSeconds;
    var nextFrameId = (currentFrame?.FrameId ?? -1) + 1;
    var simulationTimeSeconds = (currentFrame?.SimulationTimeSeconds ?? 0) + fixedDeltaSeconds;
    var sessionId = string.IsNullOrWhiteSpace(currentFrame?.SessionId)
        ? options.SessionId
        : currentFrame.SessionId;
    var run = currentFrame?.Run ?? new AetheriaRuntimeRunCheckpointCommit();

    var loadoutTemplates = node.Cache
        .GetAll<AetheriaLoadoutTemplate>()
        .Select(ToLoadoutTemplateCommit)
        .ToArray();
    var observedCommands = node.ReadObservedDaemonCommands();

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
            ObservedCommands = observedCommands,
            AccountedCommandIds = currentFrame?.AccountedCommandIds ?? Array.Empty<string>(),
            OperationContext = new AetheriaRuntimeDaemonOperationContext
            {
                LoadoutTemplates = loadoutTemplates
            }
        });

    await PublishDaemonApiDocumentsAsync(node, result).ConfigureAwait(false);
    await PublishStateSurfacesAsync(node, options, result.Frame.PublishedAtUtc).ConfigureAwait(false);
    return result;
}

static async Task PublishDaemonApiDocumentsAsync(
    AetheriaStateNode node,
    AetheriaRuntimeDaemonTickResult result)
{
    await node.PutDaemonFrameAsync(result.Frame).ConfigureAwait(false);
    if (AetheriaRuntimeDaemonSoaViewStore.TryReadView(node.StatePath, out var soaView) &&
        string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
    {
        await node.PutDaemonSoaViewAsync(soaView).ConfigureAwait(false);
    }

    if (AetheriaRuntimeDaemonPublicationStore.TryReadProviderAdvertisement(node.StatePath, out var provider))
        await node.PutDaemonProviderAdvertisementAsync(provider).ConfigureAwait(false);
    if (AetheriaRuntimeDaemonPublicationStore.TryReadHealth(node.StatePath, out var health))
        await node.PutDaemonHealthAsync(health).ConfigureAwait(false);
    if (AetheriaRuntimeDaemonPublicationStore.TryReadCommandBoundary(node.StatePath, out var commandBoundary))
        await node.PutDaemonCommandBoundaryAsync(commandBoundary).ConfigureAwait(false);
    if (AetheriaRuntimeDaemonPublicationStore.TryReadGameSurface(node.StatePath, out var gameSurface))
        await node.PutDaemonGameSurfaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(gameSurface)).ConfigureAwait(false);
    if (AetheriaRuntimeDaemonPublicationStore.TryReadGameTuiSurface(node.StatePath, out var gameTuiSurface))
        await node.PutDaemonGameTuiSurfaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(gameTuiSurface)).ConfigureAwait(false);
    if (AetheriaRuntimeDaemonPublicationStore.TryReadEditorSurface(node.StatePath, out var editorSurface))
        await node.PutDaemonEditorSurfaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(editorSurface)).ConfigureAwait(false);
    if (AetheriaRuntimeDaemonPublicationStore.TryReadEditorTuiSurface(node.StatePath, out var editorTuiSurface))
        await node.PutDaemonEditorTuiSurfaceAsync(AetheriaRuntimeEveSurfaceStateProjector.ToState(editorTuiSurface)).ConfigureAwait(false);
}

static async Task AcceptEveCommandsAsync(AetheriaStateNode node, AetheriaDaemonHostOptions options)
{
    var commandCountBefore = node.ReadObservedEveCommands().Count;
    var now = DateTimeOffset.UtcNow.ToString("O");
    try
    {
        var existingStatus = await node.GetEveCommandAcceptanceStatusAsync().ConfigureAwait(false);
        var report = await AetheriaEveCommandBridge.AcceptObservedAsync(
                node,
                existingStatus?.AccountedCommandIds)
            .ConfigureAwait(false);
        await node.PutEveCommandAcceptanceStatusAsync(new AetheriaEveCommandAcceptanceStatus
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
        }).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        var existing = await node.GetEveCommandAcceptanceStatusAsync().ConfigureAwait(false);
        await node.PutEveCommandAcceptanceStatusAsync(new AetheriaEveCommandAcceptanceStatus
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
        }).ConfigureAwait(false);
        throw;
    }
}

static async Task PublishStateSurfacesAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string updatedAtUtc)
{
    var verseHost = await EnsureVerseHostSettingsAsync(node, options, updatedAtUtc).ConfigureAwait(false);
    var eveStatus = await node.GetEveCommandAcceptanceStatusAsync().ConfigureAwait(false);
    var runtimeSession = await node.GetRuntimeSessionAsync(options.DaemonId).ConfigureAwait(false);
    var playerSettings = await node.GetPlayerSettingsAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
    var playerSettingsUpdatedAt = string.IsNullOrWhiteSpace(playerSettings.LastUpdatedAtUtc)
        ? updatedAtUtc
        : playerSettings.LastUpdatedAtUtc;

    await node.PutOperationsSurfaceAsync(
        AetheriaOperationsSurfaceProjector.Build(eveStatus, verseHost, runtimeSession)).ConfigureAwait(false);
    await node.PutPlayerSettingsSurfaceAsync(
        AetheriaPlayerSettingsSurfaceProjector.Build(playerSettings, playerSettingsUpdatedAt)).ConfigureAwait(false);
    await node.PutProviderAdvertisementAsync(
        AetheriaProviderAdvertisementProjector.Build(verseHost, node.StatePath, updatedAtUtc)).ConfigureAwait(false);
    await node.FlushAsync().ConfigureAwait(false);
}

static async Task PublishRuntimeSessionAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string startedAtUtc,
    string status)
{
    var now = DateTimeOffset.UtcNow.ToString("O");
    await node.PutRuntimeSessionAsync(new AetheriaRuntimeSession
    {
        RuntimeId = options.DaemonId,
        Role = "verse-daemon",
        StartedAtUtc = startedAtUtc,
        LastSeenAtUtc = now,
        Status = status
    }).ConfigureAwait(false);
    await PublishStateSurfacesAsync(node, options, now).ConfigureAwait(false);
}

static async Task EnsureWorldDocumentAsync(AetheriaStateNode node)
{
    var now = DateTimeOffset.UtcNow.ToString("O");
    var existing = await node.GetWorldAsync().ConfigureAwait(false);
    if (existing != null)
    {
        existing.UpdatedAtUtc = now;
        await node.PutWorldAsync(existing).ConfigureAwait(false);
        return;
    }

    await node.PutWorldAsync(new AetheriaWorldState
    {
        Name = "Aetheria",
        WorldId = "aetheria",
        SchemaEpoch = 1,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    }).ConfigureAwait(false);
}

static async Task<AetheriaVerseHostSettings> EnsureVerseHostSettingsAsync(
    AetheriaStateNode node,
    AetheriaDaemonHostOptions options,
    string now)
{
    var existing = await node.GetVerseHostSettingsAsync().ConfigureAwait(false);
    var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(existing);
    normalized.ServiceId = options.DaemonId;
    normalized.VerseId = options.VerseId;
    normalized.CultMeshAddress = options.CultMeshAddress;

    if (existing == null ||
        string.IsNullOrWhiteSpace(existing.LastUpdatedAtUtc) ||
        !AetheriaVerseHostSettingsNormalizer.Equivalent(existing, normalized))
    {
        normalized.LastUpdatedAtUtc = now;
        await node.PutVerseHostSettingsAsync(normalized).ConfigureAwait(false);
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
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(50);
    public double FixedDeltaSeconds { get; init; } = 0.02;
    public bool Once { get; init; }

    public static AetheriaDaemonHostOptions Parse(IReadOnlyList<string> args)
    {
        var root = ReadOption(args, "--root");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        var state = ReadOption(args, "--state");
        var intervalMs = ReadPositiveInt(args, "--tick-interval-ms") ?? 50;
        var fixedDeltaMs = ReadPositiveInt(args, "--fixed-delta-ms") ?? 20;
        var daemonId = ReadOption(args, "--daemon-id");
        var verseId = ReadOption(args, "--verse-id");
        var cultMeshAddress = ReadOption(args, "--cultmesh-address");
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
            TickInterval = TimeSpan.FromMilliseconds(intervalMs),
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

    private static int? ReadPositiveInt(IReadOnlyList<string> args, string name)
    {
        return int.TryParse(ReadOption(args, name), out var value) && value > 0
            ? value
            : null;
    }

    private static bool HasFlag(IReadOnlyList<string> args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }
}
