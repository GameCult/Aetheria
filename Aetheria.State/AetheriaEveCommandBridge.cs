using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

namespace Aetheria.State;

public sealed class AetheriaEveCommandAcceptanceReport
{
    public int AcceptedCatalogRefreshes { get; set; }
    public int AcceptedOperationsRefreshes { get; set; }
    public int AcceptedPlayerSettingsCommands { get; set; }
    public int AcceptedInputSettingsCommands { get; set; }
    public int AcceptedLoadoutTemplateCommands { get; set; }
    public int AcceptedVerseHostCommands { get; set; }
    public int RejectedCommands { get; set; }
    public string[] AcceptedCommandIds { get; set; } = [];
    public string[] RejectedCommandIds { get; set; } = [];
    public string[] AccountedCommandIds { get; set; } = [];
    public string LastRejectedCommand { get; set; } = "";
    public string LastRejectedReason { get; set; } = "";
}

public static class AetheriaEveCommandBridge
{
    public const string CommandSchema = "gamecult.eve.command.v1";

    public static async Task<AetheriaEveCommandAcceptanceReport> AcceptObservedAsync(
        AetheriaStateNode node,
        IEnumerable<string>? accountedCommandIds = null)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        var report = new AetheriaEveCommandAcceptanceReport();
        var accepted = new List<string>();
        var rejected = new List<string>();
        var accounted = new HashSet<string>(accountedCommandIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (var command in node.ReadObservedEveCommands()
                     .Where(command => !string.IsNullOrWhiteSpace(command.CommandId))
                     .Where(command => !accounted.Contains(command.CommandId)))
        {
            var rejection = Validate(command);
            if (!string.IsNullOrWhiteSpace(rejection))
            {
                RecordRejection(report, command, rejection, rejected);
                continue;
            }

            switch (command.Kind)
            {
                case AetheriaRuntimeEveCommandKind.CatalogRefresh:
                    await node.PutCatalogSurfaceAsync(
                        AetheriaCatalogSurfaceProjector.Build(node.ReadCatalogSnapshot(), command.IssuedAtUtc)).ConfigureAwait(false);
                    report.AcceptedCatalogRefreshes++;
                    break;
                case AetheriaRuntimeEveCommandKind.OperationsRefresh:
                    var eveStatus = await node.GetEveCommandAcceptanceStatusAsync().ConfigureAwait(false) ??
                        EmptyEveCommandAcceptanceStatus(node.StatePath, command.IssuedAtUtc);
                    var verseHostSettings = await node.GetVerseHostSettingsAsync().ConfigureAwait(false);
                    var runtimeSession = await node.GetRuntimeSessionAsync(eveStatus.RuntimeId).ConfigureAwait(false);
                    await node.PutOperationsSurfaceAsync(
                        AetheriaOperationsSurfaceProjector.Build(
                            eveStatus,
                            verseHostSettings,
                            runtimeSession)).ConfigureAwait(false);
                    report.AcceptedOperationsRefreshes++;
                    break;
                case AetheriaRuntimeEveCommandKind.PlayerSettingsRefresh:
                case AetheriaRuntimeEveCommandKind.SetPlayerName:
                case AetheriaRuntimeEveCommandKind.CycleTemperatureUnit:
                case AetheriaRuntimeEveCommandKind.DecrementSignificantDigits:
                case AetheriaRuntimeEveCommandKind.IncrementSignificantDigits:
                case AetheriaRuntimeEveCommandKind.CycleNebulaQuality:
                case AetheriaRuntimeEveCommandKind.ToggleShowAsteroidsInMinimap:
                    await ExecutePlayerSettingsCommandAsync(node, command).ConfigureAwait(false);
                    report.AcceptedPlayerSettingsCommands++;
                    break;
                case AetheriaRuntimeEveCommandKind.InputSettingsRefresh:
                case AetheriaRuntimeEveCommandKind.SetBindingOverride:
                case AetheriaRuntimeEveCommandKind.SetActionBarEnabled:
                    await ExecuteInputSettingsCommandAsync(node, command).ConfigureAwait(false);
                    report.AcceptedInputSettingsCommands++;
                    break;
                case AetheriaRuntimeEveCommandKind.SaveLoadoutTemplate:
                    await ExecuteLoadoutTemplateCommandAsync(node, command).ConfigureAwait(false);
                    report.AcceptedLoadoutTemplateCommands++;
                    break;
                case AetheriaRuntimeEveCommandKind.VerseHostRefresh:
                case AetheriaRuntimeEveCommandKind.CycleVerseHostVisibility:
                    await ExecuteVerseHostCommandAsync(node, command).ConfigureAwait(false);
                    report.AcceptedVerseHostCommands++;
                    break;
            }

            accepted.Add(command.CommandId ?? "");
        }

        await node.FlushAsync().ConfigureAwait(false);
        report.AcceptedCommandIds = accepted.ToArray();
        report.RejectedCommandIds = rejected.ToArray();
        report.AccountedCommandIds = accounted
            .Concat(report.AcceptedCommandIds)
            .Concat(report.RejectedCommandIds)
            .Where(commandId => !string.IsNullOrWhiteSpace(commandId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return report;
    }

    private static string Validate(AetheriaRuntimeEveCommandDocument command)
    {
        if (!string.Equals(command.Schema, CommandSchema, StringComparison.Ordinal))
            return $"Unexpected Eve command schema '{command.Schema}'.";
        if (!string.Equals(command.ProviderId, AetheriaProviderAdvertisementProjector.ProviderId, StringComparison.Ordinal))
            return $"Unexpected Eve provider '{command.ProviderId}'.";
        if (command.Kind == AetheriaRuntimeEveCommandKind.Unknown)
            return $"Unknown typed Eve command kind for surface '{command.SurfaceId}' command '{command.Command}'.";
        if (AetheriaRuntimeEveCommandClient.CommandKindForSurface(command.SurfaceId, command.Command) != command.Kind)
            return $"Eve command '{command.Command}' does not match typed command kind '{command.Kind}'.";

        return "";
    }

    private static async Task ExecutePlayerSettingsCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var settings = await node.GetPlayerSettingsAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
        settings.Gameplay ??= new AetheriaPlayerGameplaySettings();
        settings.Graphics ??= new AetheriaPlayerGraphicsSettings();
        var persistSettings = false;

        switch (command.Kind)
        {
            case AetheriaRuntimeEveCommandKind.SetPlayerName:
                settings.PlayerName = command.PlayerSettings.PlayerName ?? "";
                persistSettings = true;
                break;
            case AetheriaRuntimeEveCommandKind.CycleTemperatureUnit:
                settings.Gameplay.TemperatureUnit = Cycle(
                    settings.Gameplay.TemperatureUnit,
                    "Kelvin",
                    "Celsius",
                    "Fahrenheit");
                persistSettings = true;
                break;
            case AetheriaRuntimeEveCommandKind.DecrementSignificantDigits:
                settings.Gameplay.SignificantDigits = Math.Max(0, settings.Gameplay.SignificantDigits - 1);
                persistSettings = true;
                break;
            case AetheriaRuntimeEveCommandKind.IncrementSignificantDigits:
                if (settings.Gameplay.SignificantDigits < int.MaxValue)
                    settings.Gameplay.SignificantDigits++;
                persistSettings = true;
                break;
            case AetheriaRuntimeEveCommandKind.CycleNebulaQuality:
                settings.Graphics.NebulaQuality = Cycle(
                    settings.Graphics.NebulaQuality,
                    "Low",
                    "Normal",
                    "High",
                    "Ultra");
                persistSettings = true;
                break;
            case AetheriaRuntimeEveCommandKind.ToggleShowAsteroidsInMinimap:
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

    private static async Task ExecuteInputSettingsCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var settings = await node.GetPlayerSettingsAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
        settings.Input ??= new AetheriaPlayerInputSettings();
        var persistSettings = false;

        switch (command.Kind)
        {
            case AetheriaRuntimeEveCommandKind.SetBindingOverride:
                var actionName = command.InputSettings.ActionName ?? "";
                var inputSystemPath = command.InputSettings.InputSystemPath ?? "";
                var bindingIndex = command.InputSettings.BindingIndex;
                if (!string.IsNullOrWhiteSpace(actionName) && bindingIndex >= 0)
                {
                    settings.Input.BindingOverrides = settings.Input.BindingOverrides
                        .Where(binding => !string.Equals(binding.ActionName, actionName, StringComparison.Ordinal) ||
                            binding.BindingIndex != bindingIndex)
                        .Concat(new[]
                        {
                            new AetheriaInputBindingOverride
                            {
                                ActionName = actionName,
                                BindingIndex = bindingIndex,
                                BindingPath = inputSystemPath
                            }
                        })
                        .OrderBy(binding => binding.ActionName, StringComparer.Ordinal)
                        .ThenBy(binding => binding.BindingIndex)
                        .ToArray();
                    persistSettings = true;
                }
                break;
            case AetheriaRuntimeEveCommandKind.SetActionBarEnabled:
                var inputPath = command.InputSettings.InputSystemPath ?? "";
                var enabled = command.InputSettings.Enabled;
                if (!string.IsNullOrWhiteSpace(inputPath))
                {
                    var inputs = settings.Input.ActionBarInputs
                        .Where(path => !string.Equals(path, inputPath, StringComparison.Ordinal))
                        .ToList();
                    if (enabled)
                        inputs.Add(inputPath);
                    settings.Input.ActionBarInputs = inputs
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();
                    persistSettings = true;
                }
                break;
        }

        if (persistSettings)
        {
            settings.LastUpdatedAtUtc = command.IssuedAtUtc;
            await node.PutPlayerSettingsAsync(settings).ConfigureAwait(false);
        }
    }

    private static async Task ExecuteVerseHostCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var settings = await node.GetVerseHostSettingsAsync().ConfigureAwait(false) ?? new AetheriaVerseHostSettings();
        var normalized = AetheriaVerseHostSettingsNormalizer.Normalize(settings);
        var persistSettings = false;

        switch (command.Kind)
        {
            case AetheriaRuntimeEveCommandKind.CycleVerseHostVisibility:
                normalized.Visibility = Cycle(normalized.Visibility, "private", "public");
                persistSettings = true;
                break;
        }

        if (persistSettings)
        {
            normalized.LastUpdatedAtUtc = command.IssuedAtUtc;
            await node.PutVerseHostSettingsAsync(normalized).ConfigureAwait(false);
        }

        var eveStatus = await node.GetEveCommandAcceptanceStatusAsync().ConfigureAwait(false) ??
            EmptyEveCommandAcceptanceStatus(node.StatePath, command.IssuedAtUtc);
        var runtimeSession = await node.GetRuntimeSessionAsync(eveStatus.RuntimeId).ConfigureAwait(false);

        await node.PutOperationsSurfaceAsync(
            AetheriaOperationsSurfaceProjector.Build(
                eveStatus,
                normalized,
                runtimeSession)).ConfigureAwait(false);
        await node.PutProviderAdvertisementAsync(
            AetheriaProviderAdvertisementProjector.Build(normalized, node.StatePath, command.IssuedAtUtc)).ConfigureAwait(false);
    }

    private static async Task ExecuteLoadoutTemplateCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var commit = command.LoadoutTemplate ?? throw new InvalidOperationException(
            $"Loadout template command '{command.CommandId}' is missing its typed payload.");
        var loadout = AetheriaRuntimeStateMapper.ToLoadoutTemplate(commit, command.IssuedAtUtc);
        await node.PutLoadoutTemplateAsync(
            AetheriaRuntimeStateMapper.LoadoutKey(loadout.Name),
            loadout).ConfigureAwait(false);
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
        AetheriaEveCommandAcceptanceReport report,
        AetheriaRuntimeEveCommandDocument command,
        string reason,
        List<string> rejected)
    {
        report.RejectedCommands++;
        report.LastRejectedCommand = string.IsNullOrWhiteSpace(command.Command)
            ? command.CommandId
            : command.Command;
        report.LastRejectedReason = reason;
        rejected.Add(command.CommandId ?? "");
    }

    private static AetheriaEveCommandAcceptanceStatus EmptyEveCommandAcceptanceStatus(string statePath, string now)
    {
        return new AetheriaEveCommandAcceptanceStatus
        {
            RuntimeId = "aetheria-state",
            StatePath = statePath,
            LastPollAtUtc = now,
            Status = "idle"
        };
    }

}
