using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using EveProviderAdvertisementDocument = GameCult.Eve.Surface.EveProviderAdvertisementDocument;
using EveSurfaceDocument = GameCult.Eve.Surface.EveSurfaceDocument;

namespace Aetheria.State;

public sealed class AetheriaEveCommandAcceptanceReport
{
    public int AcceptedCatalogRefreshes { get; set; }
    public int AcceptedOperationsRefreshes { get; set; }
    public int AcceptedPlayerSettingsCommands { get; set; }
    public int AcceptedInputSettingsCommands { get; set; }
    public int AcceptedLoadoutTemplateCommands { get; set; }
    public int AcceptedVerseHostCommands { get; set; }
    public int AcceptedTradeValuePolicyCommands { get; set; }
    public int AcceptedMainMenuCommands { get; set; }
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
        foreach (var command in node.Documents<AetheriaRuntimeEveCommandDocument>()
                     .Select(AetheriaRuntimeEveCommandClient.NormalizeDocument)
                     .OrderBy(command => command.IssuedAtUtc ?? "", StringComparer.Ordinal)
                     .ThenBy(command => command.CommandId ?? "", StringComparer.Ordinal)
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
                    await node.FlushAsync().ConfigureAwait(false);
                    var catalog = await node.RefreshRuntimeCatalogAsync().ConfigureAwait(false);
                    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.CatalogSurfaceKey)
                        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildCatalogSurface(catalog, command.IssuedAtUtc))
                        .ConfigureAwait(false);
                    report.AcceptedCatalogRefreshes++;
                    break;
                case AetheriaRuntimeEveCommandKind.OperationsRefresh:
                    var eveStatus = await node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey).ReadAsync().ConfigureAwait(false) ??
                        EmptyEveCommandAcceptanceStatus(node.StatePath, command.IssuedAtUtc);
                    var verseHostSettings = await node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey).ReadAsync().ConfigureAwait(false);
                    var runtimeSession = await node.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey(eveStatus.RuntimeId)).ReadAsync().ConfigureAwait(false);
                    await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.OperationsSurfaceKey)
                        .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildOperationsSurface(
                            eveStatus,
                            verseHostSettings,
                            runtimeSession))
                        .ConfigureAwait(false);
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
                case AetheriaRuntimeEveCommandKind.TradeValuePolicyRefresh:
                case AetheriaRuntimeEveCommandKind.SetTradeValueQualityMinimum:
                case AetheriaRuntimeEveCommandKind.SetTradeValueQualityMaximum:
                case AetheriaRuntimeEveCommandKind.SetTradeValueQualityExponent:
                case AetheriaRuntimeEveCommandKind.SetTradeValueTierQuality:
                    await ExecuteTradeValuePolicyCommandAsync(node, command).ConfigureAwait(false);
                    report.AcceptedTradeValuePolicyCommands++;
                    break;
                case AetheriaRuntimeEveCommandKind.MainMenuContinueRun:
                case AetheriaRuntimeEveCommandKind.MainMenuNewGame:
                case AetheriaRuntimeEveCommandKind.MainMenuShowSettings:
                case AetheriaRuntimeEveCommandKind.MainMenuQuit:
                case AetheriaRuntimeEveCommandKind.MainMenuShowPlayerSettings:
                case AetheriaRuntimeEveCommandKind.MainMenuShowVerseSettings:
                case AetheriaRuntimeEveCommandKind.MainMenuShowInputSettings:
                case AetheriaRuntimeEveCommandKind.MainMenuBackToMain:
                case AetheriaRuntimeEveCommandKind.MainMenuBackToSettings:
                case AetheriaRuntimeEveCommandKind.MainMenuOpenRuntimeInputScreen:
                    await ExecuteMainMenuCommandAsync(node, command).ConfigureAwait(false);
                    report.AcceptedMainMenuCommands++;
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
        if (!string.Equals(command.ProviderId, AetheriaEveSurfaceDocuments.ProviderId, StringComparison.Ordinal))
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
        var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReadAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
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
            await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
                .ReplaceAsync(settings)
                .ConfigureAwait(false);
        }

        await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.PlayerSettingsSurfaceKey)
            .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildPlayerSettingsSurface(settings, command.IssuedAtUtc))
            .ConfigureAwait(false);
    }

    private static async Task ExecuteInputSettingsCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey).ReadAsync().ConfigureAwait(false) ?? new AetheriaPlayerSettings();
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
            await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
                .ReplaceAsync(settings)
                .ConfigureAwait(false);
        }
    }

    private static async Task ExecuteVerseHostCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var settings = await node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey).ReadAsync().ConfigureAwait(false) ?? new AetheriaVerseHostSettings();
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
            await node.MutableDocument<AetheriaVerseHostSettings>(AetheriaStateNode.VerseHostSettingsKey)
                .ReplaceAsync(normalized)
                .ConfigureAwait(false);
        }

        var eveStatus = await node.MutableDocument<AetheriaEveCommandAcceptanceStatus>(AetheriaStateNode.EveCommandAcceptanceStatusKey).ReadAsync().ConfigureAwait(false) ??
            EmptyEveCommandAcceptanceStatus(node.StatePath, command.IssuedAtUtc);
        var runtimeSession = await node.MutableDocument<AetheriaRuntimeSession>(AetheriaStateNode.RuntimeSessionKey(eveStatus.RuntimeId)).ReadAsync().ConfigureAwait(false);

        await node.MutableDocument<EveSurfaceDocument>(AetheriaStateNode.OperationsSurfaceKey)
            .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildOperationsSurface(
                eveStatus,
                normalized,
                runtimeSession))
            .ConfigureAwait(false);
        await node.MutableDocument<EveProviderAdvertisementDocument>(AetheriaStateNode.ProviderAdvertisementSurfaceKey)
            .ReplaceAsync(AetheriaEveSurfaceDocuments.BuildProviderAdvertisement(normalized, node.StatePath, command.IssuedAtUtc))
            .ConfigureAwait(false);
    }

    private static async Task ExecuteLoadoutTemplateCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var commit = command.LoadoutTemplate ?? throw new InvalidOperationException(
            $"Loadout template command '{command.CommandId}' is missing its typed payload.");
        var loadout = AetheriaRuntimeStateMapper.ToLoadoutTemplate(commit, command.IssuedAtUtc);
        await node.MutableDocument<AetheriaLoadoutTemplate>(AetheriaRuntimeStateMapper.LoadoutKey(loadout.Name))
            .ReplaceAsync(loadout)
            .ConfigureAwait(false);
    }

    private static async Task ExecuteTradeValuePolicyCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var tradeValuePolicy = node.MutableDocument<AetheriaTradeValuePolicy>(AetheriaStateNode.TradeValuePolicyKey);
        var policy = await tradeValuePolicy.ReadAsync().ConfigureAwait(false) ??
            AetheriaRuntimeStateMapper.ToTradeValuePolicy(
                AetheriaRuntimeTradeValueSettings.Default,
                command.IssuedAtUtc);
        policy.QualityPriceModifier ??= new AetheriaExponentialLerp();
        policy.Tiers ??= Array.Empty<AetheriaItemRarityTier>();
        var persistPolicy = false;
        var value = command.TradeValuePolicy.Value;

        switch (command.Kind)
        {
            case AetheriaRuntimeEveCommandKind.SetTradeValueQualityMinimum:
                policy.QualityPriceModifier.Minimum = ClampFinite(value, 0, double.MaxValue);
                persistPolicy = true;
                break;
            case AetheriaRuntimeEveCommandKind.SetTradeValueQualityMaximum:
                policy.QualityPriceModifier.Maximum = ClampFinite(value, 0, double.MaxValue);
                persistPolicy = true;
                break;
            case AetheriaRuntimeEveCommandKind.SetTradeValueQualityExponent:
                policy.QualityPriceModifier.Exponent = ClampFinite(value, 0.001, double.MaxValue);
                persistPolicy = true;
                break;
            case AetheriaRuntimeEveCommandKind.SetTradeValueTierQuality:
                var tierIndex = command.TradeValuePolicy.TierIndex;
                if (tierIndex >= 0 && tierIndex < policy.Tiers.Length)
                {
                    policy.Tiers[tierIndex].Quality = ClampFinite(value, 0, 1);
                    persistPolicy = true;
                }
                break;
        }

        if (persistPolicy)
        {
            policy.UpdatedAtUtc = command.IssuedAtUtc;
            await tradeValuePolicy.ReplaceAsync(policy).ConfigureAwait(false);
        }
    }

    private static async Task ExecuteMainMenuCommandAsync(
        AetheriaStateNode node,
        AetheriaRuntimeEveCommandDocument command)
    {
        var state = await node.MutableDocument<AetheriaMainMenuState>(AetheriaStateNode.MainMenuStateKey).ReadAsync().ConfigureAwait(false) ??
            new AetheriaMainMenuState();
        var activeSurfaceId = ActiveMainMenuSurfaceFor(command.Kind, state.ActiveSurfaceId);
        state.ActiveSurfaceId = activeSurfaceId;
        state.LastCommandId = command.CommandId ?? "";
        state.LastCommand = command.Command ?? "";
        state.UpdatedAtUtc = string.IsNullOrWhiteSpace(command.IssuedAtUtc)
            ? DateTimeOffset.UtcNow.ToString("O")
            : command.IssuedAtUtc;
        await node.MutableDocument<AetheriaMainMenuState>(AetheriaStateNode.MainMenuStateKey)
            .ReplaceAsync(state)
            .ConfigureAwait(false);
    }

    private static string ActiveMainMenuSurfaceFor(
        AetheriaRuntimeEveCommandKind kind,
        string currentSurfaceId)
    {
        switch (kind)
        {
            case AetheriaRuntimeEveCommandKind.MainMenuShowSettings:
            case AetheriaRuntimeEveCommandKind.MainMenuBackToSettings:
                return AetheriaRuntimeMainMenuCommands.SettingsSurfaceId;
            case AetheriaRuntimeEveCommandKind.MainMenuShowPlayerSettings:
                return AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId;
            case AetheriaRuntimeEveCommandKind.MainMenuShowVerseSettings:
                return AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId;
            case AetheriaRuntimeEveCommandKind.MainMenuShowInputSettings:
            case AetheriaRuntimeEveCommandKind.MainMenuOpenRuntimeInputScreen:
                return AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId;
            case AetheriaRuntimeEveCommandKind.MainMenuBackToMain:
            case AetheriaRuntimeEveCommandKind.MainMenuContinueRun:
            case AetheriaRuntimeEveCommandKind.MainMenuNewGame:
                return "";
            case AetheriaRuntimeEveCommandKind.MainMenuQuit:
                return AetheriaRuntimeMainMenuCommands.RootSurfaceId;
            default:
                return NormalizeMainMenuSurfaceId(currentSurfaceId);
        }
    }

    private static string NormalizeMainMenuSurfaceId(string surfaceId)
    {
        switch (surfaceId ?? "")
        {
            case AetheriaRuntimeMainMenuCommands.SettingsSurfaceId:
            case AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId:
            case AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId:
            case AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId:
                return surfaceId;
            default:
                return AetheriaRuntimeMainMenuCommands.RootSurfaceId;
        }
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

    private static double ClampFinite(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return min;
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
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
