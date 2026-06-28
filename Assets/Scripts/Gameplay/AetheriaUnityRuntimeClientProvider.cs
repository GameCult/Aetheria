/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.IO;
using GameCult.Aetheria.State.Verse;
using UnityEngine;

public static class AetheriaUnityRuntimeClientProvider
{
    private static AetheriaClient _runtimeClient;
    private static string _runtimeClientStatePath;
    private static string _runtimeClientRuntimeId;
    private static RuntimePlayerSettings _runtimePlayerSettings;

    public static RuntimePlayerSettings PlayerSettings =>
        _runtimePlayerSettings ??= LoadPlayerSettings();

    public static AetheriaClient ResolveClient(string stateFilePath, string runtimeId = "")
    {
        var effectiveRuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? "raven-unity" : runtimeId;
        if (_runtimeClient != null &&
            string.Equals(_runtimeClientStatePath, stateFilePath, StringComparison.Ordinal) &&
            string.Equals(_runtimeClientRuntimeId, effectiveRuntimeId, StringComparison.Ordinal))
        {
            return _runtimeClient;
        }

        Dispose();
        _runtimeClient = AetheriaClient
            .OpenAsync(
                stateFilePath,
                effectiveRuntimeId,
                "local",
                startServer: false,
                pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        _runtimeClientStatePath = stateFilePath;
        _runtimeClientRuntimeId = effectiveRuntimeId;
        return _runtimeClient;
    }

    public static AetheriaClient CurrentClientForStateFile(string stateFilePath)
    {
        return _runtimeClient != null &&
               string.Equals(_runtimeClientStatePath, stateFilePath, StringComparison.Ordinal)
            ? _runtimeClient
            : null;
    }

    public static void Dispose()
    {
        _runtimeClient?.Dispose();
        _runtimeClient = null;
        _runtimeClientStatePath = null;
        _runtimeClientRuntimeId = null;
    }

    private static RuntimePlayerSettings LoadPlayerSettings()
    {
        var settings = CreateDefaultPlayerSettings();
        try
        {
            var stored = ResolveClient(AetheriaUnityRuntimePaths.RuntimeStateFilePath)
                .Aetheria()
                .Settings
                .Player
                .Latest();
            if (stored == null)
                return settings;

            ApplyPlayerSettings(settings, stored);
            Debug.Log("Loaded Aetheria Verse player settings from typed state.");
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load Aetheria Verse player settings from typed state; using defaults: {ex}");
        }

        return settings;
    }

    private static RuntimePlayerSettings CreateDefaultPlayerSettings()
    {
        var settings = new RuntimePlayerSettings();
        settings.Name = Environment.UserName;
        settings.InputSettings.SetActionBarInputEnabled("<Keyboard>/leftShift", true);
        settings.InputSettings.SetActionBarInputEnabled("<Mouse>/leftButton", true);
        settings.InputSettings.SetActionBarInputEnabled("<Mouse>/rightButton", true);
        settings.InputSettings.SetActionBarInputEnabled("<Mouse>/middleButton", true);
        for (var i = 1; i < 6; i++)
            settings.InputSettings.SetActionBarInputEnabled($"<Keyboard>/{i}", true);
        return settings;
    }

    private static void ApplyPlayerSettings(
        RuntimePlayerSettings settings,
        AetheriaRuntimePlayerSettingsDocument stored)
    {
        if (!string.IsNullOrWhiteSpace(stored.PlayerName))
            settings.Name = stored.PlayerName;

        settings.TutorialPassed = stored.TutorialPassed;

        settings.HashedStoryFiles.Clear();
        foreach (var storyFileHash in stored.StoryFileHashes)
        {
            if (!string.IsNullOrWhiteSpace(storyFileHash.StoryPath))
                settings.HashedStoryFiles[storyFileHash.StoryPath] = storyFileHash.Hash;
        }

        if (Enum.TryParse(stored.TemperatureUnit, out TemperatureUnit temperatureUnit))
            settings.GameplaySettings.TemperatureUnit = temperatureUnit;
        settings.GameplaySettings.SignificantDigits = Math.Max(0, stored.SignificantDigits);

        if (Enum.TryParse(stored.NebulaQuality, out Quality nebulaQuality))
            settings.GraphicsSettings.NebulaQuality = nebulaQuality;
        settings.GraphicsSettings.ShowAsteroidsInMinimap = stored.ShowAsteroidsInMinimap;

        settings.InputSettings.InputActionMap.Clear();
        foreach (var binding in stored.BindingOverrides)
        {
            if (string.IsNullOrWhiteSpace(binding.ActionName) ||
                string.IsNullOrWhiteSpace(binding.BindingPath) ||
                binding.BindingIndex < 0)
            {
                continue;
            }

            settings.InputSettings.SetBindingOverride(binding.ActionName, binding.BindingIndex, binding.BindingPath);
        }

        settings.InputSettings.ActionBarInputs.Clear();
        foreach (var input in stored.ActionBarInputs)
        {
            if (!string.IsNullOrWhiteSpace(input))
                settings.InputSettings.SetActionBarInputEnabled(input, true);
        }
    }
}
