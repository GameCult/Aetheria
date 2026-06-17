using Aetheria.State;
using Aetheria.State.Documents;

var root = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();
var explicitStatePath = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal)
    ? args[1]
    : "";
var statePath = string.IsNullOrWhiteSpace(explicitStatePath)
    ? AetheriaStatePaths.ResolveDefaultStatePath(root)
    : Path.GetFullPath(explicitStatePath);
var deleteApplied = !args.Any(arg => string.Equals(arg, "--keep", StringComparison.OrdinalIgnoreCase));
var runtimeId = "aetheria-state-apply-pending";
var runtimePendingBefore = CountPendingRuntimeCommits(statePath);
var evePendingBefore = CountPendingEveCommands(statePath);
var now = DateTimeOffset.UtcNow.ToString("O");

await using var node = await AetheriaStateNode.OpenAsync(statePath, runtimeId);
var verseHostSettings = await EnsureVerseHostSettingsAsync(node, now);
await node.PutRuntimeSessionAsync(new AetheriaRuntimeSession
{
    RuntimeId = runtimeId,
    Role = "pending-drain",
    StartedAtUtc = now,
    LastSeenAtUtc = now,
    Status = "running"
});
var report = await AetheriaRuntimeCommitLogApplier.ApplyPendingAsync(node, deleteApplied);
var eveReport = await AetheriaEveCommandBridge.ApplyPendingAsync(node, deleteApplied);
var commitStatus = new AetheriaRuntimeCommitDrainStatus
{
    RuntimeId = runtimeId,
    StatePath = statePath,
    LastPollAtUtc = now,
    LastAppliedAtUtc = report.AppliedPaths.Length > 0 ? now : "",
    PendingBeforeApply = runtimePendingBefore,
    CommandsApplied = report.AppliedPaths.Length,
    AppliedPlayerSettings = report.AppliedPlayerSettings,
    AppliedLoadoutTemplates = report.AppliedLoadoutTemplates,
    AppliedRunCheckpoints = report.AppliedRunCheckpoints,
    Status = "ok"
};
var eveStatus = new AetheriaEveCommandDrainStatus
{
    RuntimeId = runtimeId,
    StatePath = statePath,
    LastPollAtUtc = now,
    LastAcceptedAtUtc = eveReport.AcceptedPaths.Length > 0 ? now : "",
    PendingBeforeApply = evePendingBefore,
    CommandsAccepted = eveReport.AcceptedPaths.Length,
    CommandsRejected = eveReport.RejectedCommands,
    AppliedCatalogRefreshes = eveReport.AppliedCatalogRefreshes,
    AppliedOperationsRefreshes = eveReport.AppliedOperationsRefreshes,
    AppliedPlayerSettingsCommands = eveReport.AppliedPlayerSettingsCommands,
    LastRejectedCommand = eveReport.LastRejectedCommand,
    LastRejectedReason = eveReport.LastRejectedReason,
    Status = eveReport.RejectedCommands > 0 ? "rejected" : "ok"
};
await node.PutRuntimeCommitDrainStatusAsync(commitStatus);
await node.PutEveCommandDrainStatusAsync(eveStatus);
var completedSession = new AetheriaRuntimeSession
{
    RuntimeId = runtimeId,
    Role = "pending-drain",
    StartedAtUtc = now,
    LastSeenAtUtc = DateTimeOffset.UtcNow.ToString("O"),
    Status = "completed"
};
await node.PutRuntimeSessionAsync(completedSession);
await node.PutOperationsSurfaceAsync(
    AetheriaOperationsSurfaceProjector.Build(
        commitStatus,
        eveStatus,
        verseHostSettings,
        completedSession));
await PublishPlayerSettingsSurfaceAsync(node, now);
await node.PutProviderAdvertisementAsync(
    AetheriaProviderAdvertisementProjector.Build(verseHostSettings, statePath, now));
await node.FlushAsync();

Console.WriteLine($"Applied pending Aetheria runtime commits: {statePath}");
Console.WriteLine($"Player settings: {report.AppliedPlayerSettings}");
Console.WriteLine($"Loadout templates: {report.AppliedLoadoutTemplates}");
Console.WriteLine($"Run checkpoints: {report.AppliedRunCheckpoints}");
Console.WriteLine($"Commands applied: {report.AppliedPaths.Length}");
Console.WriteLine($"Eve commands accepted: {eveReport.AcceptedPaths.Length}");
Console.WriteLine($"Eve commands rejected: {eveReport.RejectedCommands}");
Console.WriteLine($"Eve catalog refreshes: {eveReport.AppliedCatalogRefreshes}");
Console.WriteLine($"Eve operations refreshes: {eveReport.AppliedOperationsRefreshes}");
Console.WriteLine($"Eve player settings commands: {eveReport.AppliedPlayerSettingsCommands}");
if (!deleteApplied)
    Console.WriteLine("Applied command files were kept because --keep was supplied.");

static int CountPendingRuntimeCommits(string statePath)
{
    var pendingDirectory = statePath + ".pending";
    return Directory.Exists(pendingDirectory)
        ? Directory.EnumerateFiles(pendingDirectory, "*.cc").Count()
        : 0;
}

static int CountPendingEveCommands(string statePath)
{
    var pendingDirectory = AetheriaEveCommandBridge.GetPendingDirectory(statePath);
    return Directory.Exists(pendingDirectory)
        ? Directory.EnumerateFiles(pendingDirectory, "*.cc").Count()
        : 0;
}

static async Task PublishPlayerSettingsSurfaceAsync(AetheriaStateNode node, string updatedAtUtc)
{
    var settings = await node.GetPlayerSettingsAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
    var publishedAtUtc = string.IsNullOrWhiteSpace(settings.LastUpdatedAtUtc)
        ? updatedAtUtc
        : settings.LastUpdatedAtUtc;
    await node.PutPlayerSettingsSurfaceAsync(
        AetheriaPlayerSettingsSurfaceProjector.Build(settings, publishedAtUtc)).ConfigureAwait(false);
}

static async Task<AetheriaVerseHostSettings> EnsureVerseHostSettingsAsync(AetheriaStateNode node, string now)
{
    var existing = await node.GetVerseHostSettingsAsync().ConfigureAwait(false);
    var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(existing);
    if (existing == null || string.IsNullOrWhiteSpace(existing.LastUpdatedAtUtc))
    {
        normalized.LastUpdatedAtUtc = now;
        await node.PutVerseHostSettingsAsync(normalized).ConfigureAwait(false);
    }

    return normalized;
}
