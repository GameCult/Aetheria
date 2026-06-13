using Aetheria.State;

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

await using var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-apply-pending");
var report = await AetheriaRuntimeCommitLogApplier.ApplyPendingAsync(node, deleteApplied);

Console.WriteLine($"Applied pending Aetheria runtime commits: {statePath}");
Console.WriteLine($"Player settings: {report.AppliedPlayerSettings}");
Console.WriteLine($"Loadout templates: {report.AppliedLoadoutTemplates}");
Console.WriteLine($"Run checkpoints: {report.AppliedRunCheckpoints}");
Console.WriteLine($"Commands applied: {report.AppliedPaths.Length}");
if (!deleteApplied)
    Console.WriteLine("Applied command files were kept because --keep was supplied.");
