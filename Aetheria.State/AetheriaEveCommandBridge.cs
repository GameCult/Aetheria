using System.Buffers;
using Aetheria.State.Documents;
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

    public static void QueueCommand(
        string stateFilePath,
        string providerId,
        string surfaceId,
        string command,
        IReadOnlyDictionary<string, string>? payload = null,
        string clientId = "aetheria-state")
    {
        var issuedAtUtc = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");
        var commandId = Guid.NewGuid().ToString("N");
        var pendingDirectory = GetPendingDirectory(stateFilePath);
        Directory.CreateDirectory(pendingDirectory);

        var finalPath = Path.Combine(
            pendingDirectory,
            $"{issuedAtUtc.Replace(':', '-')}.{StableToken(surfaceId)}.{StableToken(command)}.{commandId}.cc");
        var tempPath = finalPath + ".tmp";

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(8);
        writer.Write(CommandSchema);
        writer.Write(commandId);
        writer.Write(providerId ?? "");
        writer.Write(surfaceId ?? "");
        writer.Write(command ?? "");
        writer.Write(issuedAtUtc);
        writer.Write(clientId ?? "");
        WritePayload(ref writer, payload);
        writer.Flush();

        File.WriteAllBytes(tempPath, buffer.WrittenSpan.ToArray());
        if (File.Exists(finalPath))
            File.Delete(finalPath);
        File.Move(tempPath, finalPath);
    }

    private static AetheriaPendingEveCommand ReadCommand(string path)
    {
        var reader = new MessagePackReader(File.ReadAllBytes(path));
        var fields = reader.ReadArrayHeader();
        var command = new AetheriaPendingEveCommand
        {
            Schema = fields > 0 ? ReadString(ref reader) : "",
            CommandId = fields > 1 ? ReadString(ref reader) : "",
            ProviderId = fields > 2 ? ReadString(ref reader) : "",
            SurfaceId = fields > 3 ? ReadString(ref reader) : "",
            Command = fields > 4 ? ReadString(ref reader) : "",
            IssuedAtUtc = fields > 5 ? ReadString(ref reader) : "",
            ClientId = fields > 6 ? ReadString(ref reader) : "",
            Payload = fields > 7 ? ReadPayload(ref reader) : new Dictionary<string, string>(0, StringComparer.Ordinal),
            Path = path
        };
        for (var field = 8; field < fields; field++)
            reader.Skip();

        return command;
    }

    private static string Validate(AetheriaPendingEveCommand command)
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
        AetheriaPendingEveCommand command,
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

    private static void WritePayload(ref MessagePackWriter writer, IReadOnlyDictionary<string, string>? payload)
    {
        payload ??= new Dictionary<string, string>(0, StringComparer.Ordinal);
        writer.WriteMapHeader(payload.Count);
        foreach (var entry in payload.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            writer.Write(entry.Key ?? "");
            writer.Write(entry.Value ?? "");
        }
    }

    private static IReadOnlyDictionary<string, string> ReadPayload(ref MessagePackReader reader)
    {
        var count = reader.ReadMapHeader();
        var payload = new Dictionary<string, string>(count, StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var key = ReadString(ref reader);
            var value = ReadString(ref reader);
            if (!string.IsNullOrWhiteSpace(key))
                payload[key] = value;
        }

        return payload;
    }

    private static string StableToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "empty";

        var chars = value
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var token = new string(chars).Trim('-').ToLowerInvariant();
        while (token.Contains("--", StringComparison.Ordinal))
            token = token.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(token) ? "empty" : token;
    }

    private static string ReadString(ref MessagePackReader reader)
    {
        return reader.ReadString() ?? "";
    }

    private sealed class AetheriaPendingEveCommand
    {
        public string Schema { get; set; } = "";
        public string CommandId { get; set; } = "";
        public string ProviderId { get; set; } = "";
        public string SurfaceId { get; set; } = "";
        public string Command { get; set; } = "";
        public string IssuedAtUtc { get; set; } = "";
        public string ClientId { get; set; } = "";
        public IReadOnlyDictionary<string, string> Payload { get; set; } =
            new Dictionary<string, string>(0, StringComparer.Ordinal);
        public string Path { get; set; } = "";
    }
}
