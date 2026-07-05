/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using UnityEngine;

public static class AetheriaUnityRuntimeClientProvider
{
    private static readonly Dictionary<string, AetheriaClient> RuntimeClients =
        new Dictionary<string, AetheriaClient>(StringComparer.Ordinal);
    private static RuntimePlayerSettings _runtimePlayerSettings;
    private static bool _playerSettingsLoaded;

    public static RuntimePlayerSettings PlayerSettings
    {
        get
        {
            _runtimePlayerSettings ??= CreateDefaultPlayerSettings();
            RefreshPlayerSettings(_runtimePlayerSettings);
            return _runtimePlayerSettings;
        }
    }

    private static AetheriaClient ResolveClient(string stateFilePath, string runtimeId = "")
    {
        var effectiveRuntimeId = string.IsNullOrWhiteSpace(runtimeId)
            ? AetheriaRuntimeStateBoundary.DefaultClientRuntimeId
            : runtimeId;
        var cacheKey = CacheKey(stateFilePath, effectiveRuntimeId);
        if (RuntimeClients.TryGetValue(cacheKey, out var runtimeClient))
            return runtimeClient;

        runtimeClient = AetheriaClient
            .OpenAsync(
                stateFilePath,
                effectiveRuntimeId,
                "local",
                startServer: false,
                pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        RuntimeClients[cacheKey] = runtimeClient;
        return runtimeClient;
    }

    private static AetheriaClient ResolveClient(AetheriaRuntimeStateBootReport stateBoot, string runtimeId = "")
    {
        return ResolveClient(
            stateBoot.StateFilePath,
            string.IsNullOrWhiteSpace(runtimeId) ? stateBoot.RuntimeId : runtimeId);
    }

    private static AetheriaClient RuntimeClient(string runtimeId = "")
    {
        return ResolveClient(AetheriaUnityRuntimePaths.RuntimeStateFilePath, runtimeId);
    }

    private static AetheriaClient RuntimeClient(
        AetheriaRuntimeStateBootReport stateBoot,
        string runtimeId = "")
    {
        return ResolveClient(stateBoot, runtimeId);
    }

    public static AetheriaClientState RuntimeState(string runtimeId = "")
    {
        return RuntimeClient(runtimeId).State;
    }

    public static AetheriaClientState RuntimeState(
        AetheriaRuntimeStateBootReport stateBoot,
        string runtimeId = "")
    {
        return RuntimeClient(stateBoot, runtimeId).State;
    }

    public static AetheriaUnityGameViewportDocuments MapViewportDocuments(
        AetheriaRuntimeViewportBounds viewport,
        string runtimeId = "")
    {
        return AetheriaUnityGameViewportDocuments.OpenMap(RuntimeState(runtimeId), viewport);
    }

    public static AetheriaUnityGameViewportDocuments ZonePresentationDocuments(
        AetheriaRuntimeViewportBounds viewport,
        string runtimeId = "")
    {
        return AetheriaUnityGameViewportDocuments.OpenZonePresentation(RuntimeState(runtimeId), viewport);
    }

    public static AetheriaUnityGameViewportDocuments RenderSplatViewportDocuments(
        AetheriaRuntimeViewportBounds viewport,
        string runtimeId = "")
    {
        return AetheriaUnityGameViewportDocuments.OpenRenderSplats(RuntimeState(runtimeId), viewport);
    }

    public static AetheriaUnityDaemonRenderDocuments DaemonRenderDocuments(
        AetheriaRuntimeStateBootReport stateBoot,
        string runtimeId = "")
    {
        return AetheriaUnityDaemonRenderDocuments.Open(RuntimeState(stateBoot, runtimeId));
    }

    public static AetheriaControl Control(string runtimeId = "")
    {
        return RuntimeClient(runtimeId).Control;
    }

    public static AetheriaControl Control(
        AetheriaRuntimeStateBootReport stateBoot,
        string runtimeId = "")
    {
        return RuntimeClient(stateBoot, runtimeId).Control;
    }

    public static AetheriaUi Ui(string runtimeId = "")
    {
        return RuntimeClient(runtimeId).Ui;
    }

    public static AetheriaUi Ui(
        AetheriaRuntimeStateBootReport stateBoot,
        string runtimeId = "")
    {
        return RuntimeClient(stateBoot, runtimeId).Ui;
    }

    public static CultMeshStateRefResolver EveSurfaceCultMeshStateRefResolver(string runtimeId = "")
    {
        return RuntimeState(runtimeId).CreateEveSurfaceCultMeshStateRefResolver();
    }

    public static CultMeshStateRefResolver EveSurfaceCultMeshStateRefResolver(
        AetheriaRuntimeStateBootReport stateBoot,
        string runtimeId = "")
    {
        return RuntimeState(stateBoot, runtimeId).CreateEveSurfaceCultMeshStateRefResolver();
    }

    public static void Dispose()
    {
        _runtimePlayerSettings = null;
        _playerSettingsLoaded = false;

        foreach (var client in RuntimeClients.Values)
            client.Dispose();
        RuntimeClients.Clear();
    }

    private static string CacheKey(string stateFilePath, string runtimeId)
    {
        return (stateFilePath ?? "") + "\n" + runtimeId;
    }

    private static void RefreshPlayerSettings(RuntimePlayerSettings settings)
    {
        try
        {
            var stored = RuntimeState().PlayerSettings.Latest();
            if (stored == null)
                return;

            ApplyPlayerSettings(settings, stored);
            if (!_playerSettingsLoaded)
            {
                Debug.Log("Loaded Aetheria Verse player settings from typed state.");
                _playerSettingsLoaded = true;
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load Aetheria Verse player settings from typed state; using defaults: {ex}");
        }
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
