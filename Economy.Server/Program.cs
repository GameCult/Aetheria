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

        await EnsureWorldDocumentAsync(node).ConfigureAwait(false);
        await ApplyPendingRuntimeCommitsAsync(node).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);

        if (HasFlag(args, "--apply-pending-once"))
        {
            Console.WriteLine("Aetheria pending runtime commit drain completed.");
            return;
        }

        Console.WriteLine("Aetheria CultMesh state host is running. Press Ctrl+C to stop.");
        await RunUntilShutdownAsync(node, PendingInterval(args)).ConfigureAwait(false);

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
        await node.PutOperationsSurfaceAsync(AetheriaOperationsSurfaceProjector.Build(status)).ConfigureAwait(false);
        await node.PutProviderAdvertisementAsync(
            AetheriaProviderAdvertisementProjector.Build(node.StatePath, status.LastPollAtUtc)).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
    }

    private static int CountPendingRuntimeCommits(string statePath)
    {
        var pendingDirectory = statePath + ".pending";
        return Directory.Exists(pendingDirectory)
            ? Directory.EnumerateFiles(pendingDirectory, "*.cc").Count()
            : 0;
    }

    private static async Task RunUntilShutdownAsync(AetheriaStateNode node, TimeSpan pendingInterval)
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
        }
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
}
