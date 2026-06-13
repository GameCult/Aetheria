using Aetheria.State;
using Aetheria.State.Documents;

namespace Aetheria.Server;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var statePath = ResolveStatePath(args);
        Console.WriteLine($"Aetheria CultMesh state host starting: {statePath}");

        await using var node = await AetheriaStateNode.OpenAsync(
            statePath,
            runtimeId: "aetheria-economy-server",
            startServer: true).ConfigureAwait(false);

        await EnsureWorldDocumentAsync(node).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);

        Console.WriteLine("Aetheria CultMesh state host is running. Press Ctrl+C to stop.");
        await WaitForShutdownAsync().ConfigureAwait(false);

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

    private static Task WaitForShutdownAsync()
    {
        var stopped = new TaskCompletionSource<object?>();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopped.TrySetResult(null);
        };

        return stopped.Task;
    }
}
