using Aetheria.State.Documents;
using GameCult.Aetheria.State.Unity;

namespace Aetheria.State;

public sealed class AetheriaEveCommandApplyReport
{
    public int AppliedCatalogRefreshes { get; set; }
    public int AppliedOperationsRefreshes { get; set; }
    public int AppliedPlayerSettingsCommands { get; set; }
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
                case "aetheria.player_settings.refresh":
                case "aetheria.player_settings.gameplay.temperature_unit.cycle":
                case "aetheria.player_settings.gameplay.significant_digits.decrement":
                case "aetheria.player_settings.gameplay.significant_digits.increment":
                case "aetheria.player_settings.graphics.nebula_quality.cycle":
                case "aetheria.player_settings.graphics.show_asteroids.toggle":
                    await ApplyPlayerSettingsCommandAsync(node, command).ConfigureAwait(false);
                    report.AppliedPlayerSettingsCommands++;
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
        return AetheriaRuntimePendingCultCacheStore.ReadEveCommand(path);
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
             string.Equals(command, "aetheria.operations.refresh", StringComparison.Ordinal)) ||
            (string.Equals(surfaceId, AetheriaPlayerSettingsSurfaceProjector.SurfaceId, StringComparison.Ordinal) &&
             KnownPlayerSettingsCommand(command));
    }

    private static bool KnownPlayerSettingsCommand(string command)
    {
        return string.Equals(command, "aetheria.player_settings.refresh", StringComparison.Ordinal) ||
            string.Equals(command, "aetheria.player_settings.gameplay.temperature_unit.cycle", StringComparison.Ordinal) ||
            string.Equals(command, "aetheria.player_settings.gameplay.significant_digits.decrement", StringComparison.Ordinal) ||
            string.Equals(command, "aetheria.player_settings.gameplay.significant_digits.increment", StringComparison.Ordinal) ||
            string.Equals(command, "aetheria.player_settings.graphics.nebula_quality.cycle", StringComparison.Ordinal) ||
            string.Equals(command, "aetheria.player_settings.graphics.show_asteroids.toggle", StringComparison.Ordinal);
    }

    private static async Task ApplyPlayerSettingsCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var settings = await node.GetPlayerSettingsAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
        settings.Gameplay ??= new AetheriaPlayerGameplaySettings();
        settings.Graphics ??= new AetheriaPlayerGraphicsSettings();
        var persistSettings = false;

        switch (command.Command)
        {
            case "aetheria.player_settings.gameplay.temperature_unit.cycle":
                settings.Gameplay.TemperatureUnit = Cycle(
                    settings.Gameplay.TemperatureUnit,
                    "Kelvin",
                    "Celsius",
                    "Fahrenheit");
                persistSettings = true;
                break;
            case "aetheria.player_settings.gameplay.significant_digits.decrement":
                settings.Gameplay.SignificantDigits = Math.Max(0, settings.Gameplay.SignificantDigits - 1);
                persistSettings = true;
                break;
            case "aetheria.player_settings.gameplay.significant_digits.increment":
                if (settings.Gameplay.SignificantDigits < int.MaxValue)
                    settings.Gameplay.SignificantDigits++;
                persistSettings = true;
                break;
            case "aetheria.player_settings.graphics.nebula_quality.cycle":
                settings.Graphics.NebulaQuality = Cycle(
                    settings.Graphics.NebulaQuality,
                    "Low",
                    "Normal",
                    "High",
                    "Ultra");
                persistSettings = true;
                break;
            case "aetheria.player_settings.graphics.show_asteroids.toggle":
                settings.Graphics.ShowAsteroidsInMinimap = !settings.Graphics.ShowAsteroidsInMinimap;
                persistSettings = true;
                break;
        }

        if (persistSettings)
        {
            settings.LastUpdatedAtUtc = command.IssuedAtUtc;
            await node.PutPlayerSettingsAsync(settings).ConfigureAwait(false);
        }

        await node.PutPlayerSettingsSurfaceAsync(
            AetheriaPlayerSettingsSurfaceProjector.Build(settings, command.IssuedAtUtc))
            .ConfigureAwait(false);
    }

    private static string Cycle(string current, params string[] values)
    {
        if (values.Length == 0)
            return current;

        var index = Array.FindIndex(values, value => string.Equals(value, current, StringComparison.Ordinal));
        if (index < 0)
            return values[0];

        return values[(index + 1) % values.Length];
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
