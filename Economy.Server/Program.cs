using System.Text.Json;
using Aetheria.State;
using Aetheria.State.Documents;

namespace Aetheria.Server;

internal static class Program
{
    private const string RuntimeId = "aetheria-economy-server";

    private static async Task Main(string[] args)
    {
        var statePath = ResolveStatePath(args);
        Console.WriteLine($"Aetheria CultMesh state host starting: {statePath}");

        await using var node = await AetheriaStateNode.OpenAsync(
            statePath,
            runtimeId: RuntimeId,
            startServer: true).ConfigureAwait(false);
        using var verseDiscovery = new AetheriaVerseDiscoveryHost(node);

        await EnsureVerseHostSettingsAsync(node).ConfigureAwait(false);
        await RefreshVerseDiscoveryAsync(node, verseDiscovery).ConfigureAwait(false);
        var startedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        await PublishRuntimeSessionAsync(node, startedAtUtc, "starting").ConfigureAwait(false);
        await EnsureWorldDocumentAsync(node).ConfigureAwait(false);
        await ApplyPendingRuntimeCommitsAsync(node).ConfigureAwait(false);
        await ApplyPendingEveCommandsAsync(node).ConfigureAwait(false);
        await RefreshVerseDiscoveryAsync(node, verseDiscovery).ConfigureAwait(false);
        await PublishRuntimeSessionAsync(node, startedAtUtc, "running").ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);

        if (HasFlag(args, "--apply-pending-once"))
        {
            Console.WriteLine("Aetheria pending runtime and Eve command drains completed.");
            return;
        }

        Console.WriteLine("Aetheria CultMesh state host is running. Press Ctrl+C to stop.");
        await RunUntilShutdownAsync(node, verseDiscovery, startedAtUtc, PendingInterval(args)).ConfigureAwait(false);

        await PublishRuntimeSessionAsync(node, startedAtUtc, "stopping").ConfigureAwait(false);
        Console.WriteLine("Aetheria CultMesh state host stopping.");
    }

    private static string ResolveStatePath(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], "--state", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(args[i + 1]);
            }
        }

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return AetheriaStatePaths.ResolveDefaultStatePath(repoRoot);
    }

    private static async Task EnsureWorldDocumentAsync(AetheriaStateNode node)
    {
        var existing = await node.GetWorldAsync().ConfigureAwait(false);
        if (existing != null)
        {
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            await node.PutWorldAsync(existing).ConfigureAwait(false);
            return;
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        await node.PutWorldAsync(new AetheriaWorldState
        {
            Name = "Aetheria",
            WorldId = "aetheria",
            SchemaEpoch = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }).ConfigureAwait(false);
    }

    private static async Task ApplyPendingRuntimeCommitsAsync(AetheriaStateNode node)
    {
        var pendingBefore = CountPendingRuntimeCommits(node.StatePath);
        var now = DateTimeOffset.UtcNow.ToString("O");

        try
        {
            var report = await AetheriaRuntimeCommitLogApplier.ApplyPendingAsync(node).ConfigureAwait(false);
            var status = new AetheriaRuntimeCommitDrainStatus
            {
                RuntimeId = RuntimeId,
                StatePath = node.StatePath,
                LastPollAtUtc = now,
                LastAppliedAtUtc = report.AppliedPaths.Length > 0 ? now : "",
                PendingBeforeApply = pendingBefore,
                CommandsApplied = report.AppliedPaths.Length,
                AppliedPlayerSettings = report.AppliedPlayerSettings,
                AppliedLoadoutTemplates = report.AppliedLoadoutTemplates,
                AppliedRunCheckpoints = report.AppliedRunCheckpoints,
                ConsecutiveFailures = 0,
                Status = "ok"
            };
            await PublishDrainStatusAsync(node, status).ConfigureAwait(false);

            if (report.AppliedPaths.Length == 0)
            {
                Console.WriteLine("No pending Aetheria runtime commits.");
                return;
            }

            Console.WriteLine(
                "Applied pending Aetheria runtime commits: " +
                $"settings={report.AppliedPlayerSettings}, " +
                $"loadouts={report.AppliedLoadoutTemplates}, " +
                $"runs={report.AppliedRunCheckpoints}, " +
                $"files={report.AppliedPaths.Length}");
        }
        catch (Exception ex)
        {
            var existing = await node.GetRuntimeCommitDrainStatusAsync().ConfigureAwait(false);
            var status = new AetheriaRuntimeCommitDrainStatus
            {
                RuntimeId = RuntimeId,
                StatePath = node.StatePath,
                LastPollAtUtc = now,
                LastAppliedAtUtc = existing?.LastAppliedAtUtc ?? "",
                PendingBeforeApply = pendingBefore,
                ConsecutiveFailures = (existing?.ConsecutiveFailures ?? 0) + 1,
                LastError = ex.ToString(),
                Status = "error"
            };
            await PublishDrainStatusAsync(node, status).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task PublishDrainStatusAsync(
        AetheriaStateNode node,
        AetheriaRuntimeCommitDrainStatus status)
    {
        await node.PutRuntimeCommitDrainStatusAsync(status).ConfigureAwait(false);
        await PublishOperationsStateAsync(node, status.LastPollAtUtc).ConfigureAwait(false);
    }

    private static async Task ApplyPendingEveCommandsAsync(AetheriaStateNode node)
    {
        var pendingBefore = CountPendingEveCommands(node.StatePath);
        var now = DateTimeOffset.UtcNow.ToString("O");

        try
        {
            var report = await AetheriaEveCommandBridge.ApplyPendingAsync(node).ConfigureAwait(false);
            var status = new AetheriaEveCommandDrainStatus
            {
                RuntimeId = RuntimeId,
                StatePath = node.StatePath,
                LastPollAtUtc = now,
                LastAcceptedAtUtc = report.AcceptedPaths.Length > 0 ? now : "",
                PendingBeforeApply = pendingBefore,
                CommandsAccepted = report.AcceptedPaths.Length,
                CommandsRejected = report.RejectedCommands,
                AppliedCatalogRefreshes = report.AppliedCatalogRefreshes,
                AppliedOperationsRefreshes = report.AppliedOperationsRefreshes,
                AppliedPlayerSettingsCommands = report.AppliedPlayerSettingsCommands,
                AppliedVerseHostCommands = report.AppliedVerseHostCommands,
                LastRejectedCommand = report.LastRejectedCommand,
                LastRejectedReason = report.LastRejectedReason,
                ConsecutiveFailures = 0,
                Status = report.RejectedCommands > 0 ? "rejected" : "ok"
            };
            await PublishEveCommandStatusAsync(node, status).ConfigureAwait(false);

            if (report.AcceptedPaths.Length == 0 && report.RejectedCommands == 0)
            {
                Console.WriteLine("No pending Aetheria Eve commands.");
                return;
            }

            Console.WriteLine(
                "Drained pending Aetheria Eve commands: " +
                $"accepted={report.AcceptedPaths.Length}, " +
                $"rejected={report.RejectedCommands}, " +
                $"catalogRefreshes={report.AppliedCatalogRefreshes}, " +
                $"operationsRefreshes={report.AppliedOperationsRefreshes}, " +
                $"playerSettings={report.AppliedPlayerSettingsCommands}, " +
                $"verseHost={report.AppliedVerseHostCommands}");
        }
        catch (Exception ex)
        {
            var existing = await node.GetEveCommandDrainStatusAsync().ConfigureAwait(false);
            var status = new AetheriaEveCommandDrainStatus
            {
                RuntimeId = RuntimeId,
                StatePath = node.StatePath,
                LastPollAtUtc = now,
                LastAcceptedAtUtc = existing?.LastAcceptedAtUtc ?? "",
                PendingBeforeApply = pendingBefore,
                ConsecutiveFailures = (existing?.ConsecutiveFailures ?? 0) + 1,
                LastError = ex.ToString(),
                Status = "error"
            };
            await PublishEveCommandStatusAsync(node, status).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task PublishEveCommandStatusAsync(
        AetheriaStateNode node,
        AetheriaEveCommandDrainStatus status)
    {
        await node.PutEveCommandDrainStatusAsync(status).ConfigureAwait(false);
        await PublishOperationsStateAsync(node, status.LastPollAtUtc).ConfigureAwait(false);
    }

    private static async Task PublishOperationsStateAsync(
        AetheriaStateNode node,
        string updatedAtUtc)
    {
        var commitStatus = await node.GetRuntimeCommitDrainStatusAsync().ConfigureAwait(false) ??
            new AetheriaRuntimeCommitDrainStatus
            {
                RuntimeId = RuntimeId,
                StatePath = node.StatePath,
                LastPollAtUtc = updatedAtUtc,
                Status = "idle"
            };
        var eveStatus = await node.GetEveCommandDrainStatusAsync().ConfigureAwait(false);
        var verseHostSettings = await node.GetVerseHostSettingsAsync().ConfigureAwait(false) ??
            new AetheriaVerseHostSettings();
        var runtimeSession = await node.GetRuntimeSessionAsync(RuntimeId).ConfigureAwait(false);
        await node.PutOperationsSurfaceAsync(
                AetheriaOperationsSurfaceProjector.Build(
                    commitStatus,
                    eveStatus,
                    verseHostSettings,
                    runtimeSession))
            .ConfigureAwait(false);
        await PublishPlayerSettingsSurfaceAsync(node, updatedAtUtc).ConfigureAwait(false);
        await node.PutProviderAdvertisementAsync(
                AetheriaProviderAdvertisementProjector.Build(verseHostSettings, node.StatePath, updatedAtUtc))
            .ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
    }

    private static async Task PublishPlayerSettingsSurfaceAsync(
        AetheriaStateNode node,
        string updatedAtUtc)
    {
        var settings = await node.GetPlayerSettingsAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
        var publishedAtUtc = string.IsNullOrWhiteSpace(settings.LastUpdatedAtUtc)
            ? updatedAtUtc
            : settings.LastUpdatedAtUtc;
        await node.PutPlayerSettingsSurfaceAsync(
            AetheriaPlayerSettingsSurfaceProjector.Build(settings, publishedAtUtc)).ConfigureAwait(false);
    }

    private static async Task PublishRuntimeSessionAsync(
        AetheriaStateNode node,
        string startedAtUtc,
        string status)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        await node.PutRuntimeSessionAsync(new AetheriaRuntimeSession
        {
            RuntimeId = RuntimeId,
            Role = "cultmesh-state-host",
            StartedAtUtc = startedAtUtc,
            LastSeenAtUtc = now,
            Status = status
        }).ConfigureAwait(false);
        await PublishOperationsStateAsync(node, now).ConfigureAwait(false);
    }

    private static int CountPendingRuntimeCommits(string statePath)
    {
        var pendingDirectory = statePath + ".pending";
        return Directory.Exists(pendingDirectory)
            ? Directory.EnumerateFiles(pendingDirectory, "*.cc").Count()
            : 0;
    }

    private static int CountPendingEveCommands(string statePath)
    {
        var pendingDirectory = AetheriaEveCommandBridge.GetPendingDirectory(statePath);
        return Directory.Exists(pendingDirectory)
            ? Directory.EnumerateFiles(pendingDirectory, "*.cc").Count()
            : 0;
    }

    private static async Task RunUntilShutdownAsync(
        AetheriaStateNode node,
        AetheriaVerseDiscoveryHost verseDiscovery,
        string startedAtUtc,
        TimeSpan pendingInterval)
    {
        var stopped = new TaskCompletionSource<object?>();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopped.TrySetResult(null);
        };

        while (!stopped.Task.IsCompleted)
        {
            var delay = Task.Delay(pendingInterval);
            var completed = await Task.WhenAny(stopped.Task, delay).ConfigureAwait(false);
            if (completed == stopped.Task)
                break;

            await ApplyPendingRuntimeCommitsAsync(node).ConfigureAwait(false);
            await ApplyPendingEveCommandsAsync(node).ConfigureAwait(false);
            await RefreshVerseDiscoveryAsync(node, verseDiscovery).ConfigureAwait(false);
            await PublishRuntimeSessionAsync(node, startedAtUtc, "running").ConfigureAwait(false);
        }
    }

    private static async Task RefreshVerseDiscoveryAsync(
        AetheriaStateNode node,
        AetheriaVerseDiscoveryHost verseDiscovery)
    {
        var settings = await node.GetVerseHostSettingsAsync().ConfigureAwait(false);
        verseDiscovery.Update(settings);
    }

    private static TimeSpan PendingInterval(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], "--pending-interval-ms", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(args[i + 1], out var milliseconds) &&
                milliseconds > 0)
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }
        }

        return TimeSpan.FromSeconds(5);
    }

    private static bool HasFlag(IReadOnlyList<string> args, string flag)
    {
        return args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task EnsureVerseHostSettingsAsync(AetheriaStateNode node)
    {
        var existing = await node.GetVerseHostSettingsAsync().ConfigureAwait(false);
        var candidate = AetheriaVerseHostSettingsNormalizer.Normalize(existing);
        ApplyVerseHostOverrides(candidate, LoadVerseHostOverrides());

        if (existing == null ||
            string.IsNullOrWhiteSpace(existing.LastUpdatedAtUtc) ||
            !AetheriaVerseHostSettingsNormalizer.Equivalent(existing, candidate))
        {
            candidate.LastUpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            await node.PutVerseHostSettingsAsync(candidate).ConfigureAwait(false);
            await node.FlushAsync().ConfigureAwait(false);
        }
    }

    private static VerseHostOverrides LoadVerseHostOverrides()
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Economy.Server", "appsettings.json"))
        };

        var settingsPath = candidatePaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            return new VerseHostOverrides();
        }

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        if (!TryGetPropertyIgnoreCase(document.RootElement, "Aetheria", out var aetheriaSection) ||
            !TryGetPropertyIgnoreCase(aetheriaSection, "VerseHost", out var verseHostSection))
        {
            return new VerseHostOverrides();
        }

        return new VerseHostOverrides
        {
            ServiceId = ReadOptionalString(verseHostSection, "ServiceId"),
            VerseId = ReadOptionalString(verseHostSection, "VerseId"),
            RootVerse = ReadOptionalString(verseHostSection, "RootVerse"),
            CanonicalService = ReadOptionalString(verseHostSection, "CanonicalService"),
            LocatedService = ReadOptionalString(verseHostSection, "LocatedService"),
            CultMeshAddress = ReadOptionalString(verseHostSection, "CultMeshAddress"),
            Title = ReadOptionalString(verseHostSection, "Title"),
            Visibility = ReadOptionalString(verseHostSection, "Visibility")
        };
    }

    private static void ApplyVerseHostOverrides(AetheriaVerseHostSettings settings, VerseHostOverrides overrides)
    {
        settings.ServiceId = ChooseOverride(overrides.ServiceId, settings.ServiceId);
        settings.VerseId = ChooseOverride(overrides.VerseId, settings.VerseId);
        settings.RootVerse = ChooseOverride(overrides.RootVerse, settings.RootVerse);
        settings.CanonicalService = ChooseOverride(overrides.CanonicalService, settings.CanonicalService);
        settings.LocatedService = ChooseOverride(overrides.LocatedService, settings.LocatedService);
        settings.CultMeshAddress = ChooseOverride(overrides.CultMeshAddress, settings.CultMeshAddress);
        settings.Title = ChooseOverride(overrides.Title, settings.Title);
        settings.Visibility = ChooseOverride(overrides.Visibility, settings.Visibility);
    }

    private static string ChooseOverride(string? candidate, string fallback)
    {
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate.Trim();
    }

    private static string ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return "";
        }

        return value.GetString()?.Trim() ?? "";
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private sealed class VerseHostOverrides
    {
        public string ServiceId { get; init; } = "";
        public string VerseId { get; init; } = "";
        public string RootVerse { get; init; } = "";
        public string CanonicalService { get; init; } = "";
        public string LocatedService { get; init; } = "";
        public string CultMeshAddress { get; init; } = "";
        public string Title { get; init; } = "";
        public string Visibility { get; init; } = "";
    }
}
