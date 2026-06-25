using Aetheria.State;

return await ProgramMainAsync(args).ConfigureAwait(false);

static async Task<int> ProgramMainAsync(string[] args)
{
    if (args.Length == 0)
    {
        PrintUsage();
        return 1;
    }

    var mode = args[0].Trim();
    var options = ParseOptions(args.Skip(1));
    var endpoint = RequireOption(options, "endpoint");
    var runtimeId = ReadOption(options, "runtime-id", "aetheria-verse-replica");
    var replicaPath = ResolveReplicaPath(options);
    SeedBaselineReplica(options, replicaPath);

    switch (mode)
    {
        case "sync":
        {
            var appliedSequence = await AetheriaVerseReplica
                .SyncSnapshotAsync(replicaPath, endpoint, runtimeId)
                .ConfigureAwait(false);
            Console.WriteLine($"Synced Aetheria replica to {replicaPath} from {endpoint} at shard sequence {appliedSequence}.");
            return 0;
        }
        case "follow":
        {
            var pollSeconds = ReadDoubleOption(options, "poll-seconds", 1d);
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            Console.WriteLine(
                $"Following Aetheria replica at {replicaPath} from {endpoint} every {pollSeconds:0.###} seconds. Press Ctrl+C to stop.");
            await AetheriaVerseReplica
                .RunReplicaAsync(replicaPath, endpoint, TimeSpan.FromSeconds(pollSeconds), cancellation.Token, runtimeId)
                .ConfigureAwait(false);
            return 0;
        }
        default:
            Console.Error.WriteLine($"Unknown mode '{mode}'.");
            PrintUsage();
            return 1;
    }
}

static string ResolveReplicaPath(IReadOnlyDictionary<string, string> options)
{
    var explicitPath = ReadOption(options, "replica", "");
    if (!string.IsNullOrWhiteSpace(explicitPath))
        return Path.GetFullPath(explicitPath);

    var gameDataRoot = RequireOption(options, "game-data-root");
    var verseId = RequireOption(options, "verse-id");
    return Path.Combine(
        Path.GetFullPath(gameDataRoot),
        "Verses",
        $"{SanitizeVerseId(verseId)}.cc");
}

static void SeedBaselineReplica(IReadOnlyDictionary<string, string> options, string replicaPath)
{
    var baselinePath = ReadOption(options, "baseline-state", "");
    if (string.IsNullOrWhiteSpace(baselinePath) ||
        File.Exists(replicaPath) ||
        !File.Exists(baselinePath))
    {
        return;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(replicaPath) ?? ".");
    File.Copy(baselinePath, replicaPath, overwrite: false);
}

static string SanitizeVerseId(string verseId)
{
    var safeVerseId = string.IsNullOrWhiteSpace(verseId)
        ? "unknown-verse"
        : new string((verseId ?? "")
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.'
                ? ch
                : '-')
            .ToArray())
            .Trim('-');

    return string.IsNullOrWhiteSpace(safeVerseId)
        ? "unknown-verse"
        : safeVerseId;
}

static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    string? pendingKey = null;
    foreach (var arg in args)
    {
        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            pendingKey = arg[2..];
            options[pendingKey] = "true";
            continue;
        }

        if (!string.IsNullOrWhiteSpace(pendingKey))
        {
            options[pendingKey] = arg;
            pendingKey = null;
        }
    }

    return options;
}

static string RequireOption(IReadOnlyDictionary<string, string> options, string key)
{
    var value = ReadOption(options, key, "");
    if (!string.IsNullOrWhiteSpace(value))
        return value;

    throw new InvalidOperationException($"Missing required option --{key}.");
}

static string ReadOption(IReadOnlyDictionary<string, string> options, string key, string fallback)
{
    return options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : fallback;
}

static double ReadDoubleOption(IReadOnlyDictionary<string, string> options, string key, double fallback)
{
    var value = ReadOption(options, key, "");
    return double.TryParse(value, out var parsed) && parsed > 0
        ? parsed
        : fallback;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  sync --endpoint cultnet://host:3075 --replica path-to-replica.cc [--runtime-id aetheria-verse-replica]");
    Console.WriteLine("  sync --endpoint cultnet://host:3075 --game-data-root GameData --verse-id verse.id [--runtime-id aetheria-verse-replica]");
    Console.WriteLine("  follow --endpoint cultnet://host:3075 --replica path-to-replica.cc [--poll-seconds 1] [--runtime-id aetheria-verse-replica]");
}
