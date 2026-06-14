using Aetheria.State.Documents;
using GameCult.Aetheria.State.Unity;
using MessagePack;

namespace Aetheria.State;

public sealed class AetheriaEveCommandApplyReport
{
    public int AppliedCatalogRefreshes { get; set; }
    public int AppliedOperationsRefreshes { get; set; }
    public int RejectedCommands { get; set; }
    public string[] AcceptedPaths { get; set; } = [];
    public string[] RejectedPaths { get; set; } = [];
    public string LastRejectedCommand { get; set; } = "";
    public string LastRejectedReason { get; set; } = "";
}

public static class AetheriaEveCommandBridge
{
    public const string CommandSchema = "gamecult.eve.command.v1";

    public static string GetPendingDirectory(string stateFilePath)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath))
            throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

        return stateFilePath + ".eve.pending";
    }

    public static async Task<AetheriaEveCommandApplyReport> ApplyPendingAsync(
        AetheriaStateNode node,
        bool deleteAccounted = true)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        var pendingDirectory = GetPendingDirectory(node.StatePath);
        var report = new AetheriaEveCommandApplyReport();
        var accepted = new List<string>();
        var rejected = new List<string>();

        if (!Directory.Exists(pendingDirectory))
            return report;

        foreach (var path in Directory.EnumerateFiles(pendingDirectory, "*.cc").OrderBy(path => path, StringComparer.Ordinal))
        {
            var command = ReadCommand(path);
            var rejection = Validate(command);
            if (!string.IsNullOrWhiteSpace(rejection))
            {
                RecordRejection(report, command, rejection, path, rejected, deleteAccounted);
                continue;
            }

            switch (command.Command)
            {
                case "aetheria.catalog.refresh":
                    await node.PutCatalogSurfaceAsync(
                        AetheriaCatalogSurfaceProjector.Build(node.ReadCatalogSnapshot(), command.IssuedAtUtc)).ConfigureAwait(false);
                    report.AppliedCatalogRefreshes++;
                    break;
                case "aetheria.operations.refresh":
                    var commitStatus = await node.GetRuntimeCommitDrainStatusAsync().ConfigureAwait(false) ??
                        EmptyCommitDrainStatus(node.StatePath, command.IssuedAtUtc);
                    var eveStatus = await node.GetEveCommandDrainStatusAsync().ConfigureAwait(false) ??
                        EmptyEveCommandDrainStatus(node.StatePath, command.IssuedAtUtc);
                    await node.PutOperationsSurfaceAsync(
                        AetheriaOperationsSurfaceProjector.Build(commitStatus, eveStatus)).ConfigureAwait(false);
                    report.AppliedOperationsRefreshes++;
                    break;
            }

            accepted.Add(path);
            if (deleteAccounted)
                File.Delete(path);
        }

        await node.FlushAsync().ConfigureAwait(false);
        report.AcceptedPaths = accepted.ToArray();
        report.RejectedPaths = rejected.ToArray();
        return report;
    }

    private static AetheriaRuntimeEveCommandDocument ReadCommand(string path)
    {
        return MessagePackSerializer.Deserialize<AetheriaRuntimeEveCommandDocument>(File.ReadAllBytes(path));
    }

    private static string Validate(AetheriaRuntimeEveCommandDocument command)
    {
        if (!string.Equals(command.Schema, CommandSchema, StringComparison.Ordinal))
            return $"Unexpected Eve command schema '{command.Schema}'.";
        if (!string.Equals(command.ProviderId, AetheriaProviderAdvertisementProjector.ProviderId, StringComparison.Ordinal))
            return $"Unexpected Eve provider '{command.ProviderId}'.";
        if (!KnownCommand(command.SurfaceId, command.Command))
            return $"Command '{command.Command}' is not advertised for surface '{command.SurfaceId}'.";

        return "";
    }

    private static bool KnownCommand(string surfaceId, string command)
    {
        return (string.Equals(surfaceId, AetheriaCatalogSurfaceProjector.SurfaceId, StringComparison.Ordinal) &&
                string.Equals(command, "aetheria.catalog.refresh", StringComparison.Ordinal)) ||
            (string.Equals(surfaceId, AetheriaOperationsSurfaceProjector.SurfaceId, StringComparison.Ordinal) &&
             string.Equals(command, "aetheria.operations.refresh", StringComparison.Ordinal));
    }

    private static void RecordRejection(
        AetheriaEveCommandApplyReport report,
        AetheriaRuntimeEveCommandDocument command,
        string reason,
        string path,
        List<string> rejected,
        bool deleteAccounted)
    {
        report.RejectedCommands++;
        report.LastRejectedCommand = string.IsNullOrWhiteSpace(command.Command)
            ? command.CommandId
            : command.Command;
        report.LastRejectedReason = reason;
        rejected.Add(path);
        if (deleteAccounted)
            File.Delete(path);
    }

    private static AetheriaRuntimeCommitDrainStatus EmptyCommitDrainStatus(string statePath, string now)
    {
        return new AetheriaRuntimeCommitDrainStatus
        {
            RuntimeId = "aetheria-state",
            StatePath = statePath,
            LastPollAtUtc = now,
            Status = "idle"
        };
    }

    private static AetheriaEveCommandDrainStatus EmptyEveCommandDrainStatus(string statePath, string now)
    {
        return new AetheriaEveCommandDrainStatus
        {
            RuntimeId = "aetheria-state",
            StatePath = statePath,
            LastPollAtUtc = now,
            Status = "idle"
        };
    }

}
