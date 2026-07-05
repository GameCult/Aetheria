/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class AetheriaUnityGameplayBootShell
{
    public GameSettings Settings { get; set; }
    public ZoneRenderer ZoneRenderer { get; set; }
    public AetheriaUnityCockpitHudShell CockpitHudShell { get; set; }
    public Action<string> Log { get; set; }

    public AetheriaUnityGameplayBootResult Boot()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
        Log?.Invoke($"Aetheria runtime target: {stateBoot.TargetLabel} via {stateBoot.TargetKind} ({stateBoot.TargetSource})");
        Log?.Invoke($"Aetheria runtime id: {stateBoot.RuntimeId}");
        Log?.Invoke($"Aetheria runtime state file: {stateBoot.StateFilePath}");
        stateBoot = SyncRemoteReplicaBeforeBoot(stateBoot);

        if (!stateBoot.SupportsLocalStateFileRead)
        {
            throw new InvalidOperationException(
                $"Aetheria runtime target cannot read the daemon mirror state required for gameplay boot: {stateBoot.FailureMessage}");
        }

        if (!stateBoot.StateFileExists)
        {
            throw new InvalidOperationException(
                $"Aetheria runtime state file is missing at {stateBoot.StateFilePath}; gameplay requires an authoritative daemon mirror.");
        }

        var runtimeState = AetheriaUnityRuntimeClientProvider.RuntimeState(stateBoot, stateBoot.RuntimeId);
        var runtimeCatalog = runtimeState.Catalog.Latest();
        if (runtimeCatalog == null)
            throw new InvalidOperationException("Aetheria typed runtime catalog is required before gameplay boot.");
        var sectorMap = runtimeState.SectorMap.Latest();
        if (sectorMap == null)
            throw new InvalidOperationException("Aetheria typed sector map is required before gameplay boot.");

        Log?.Invoke($"Aetheria runtime catalog: {runtimeCatalog.Items.Count} items, {runtimeCatalog.Corporations.Count} corporations, {runtimeCatalog.NameFiles.Count} name files");
        Log?.Invoke($"Aetheria observed sector-map frame: {sectorMap.FrameId}");

        var backgroundSettings = sectorMap.IsTutorial
            ? Settings.TutorialBackgroundSettings
            : Settings.SectorBackgroundSettings;
        backgroundSettings.NoisePosition = sectorMap.GenerationSeed == 0 ? 1 : sectorMap.GenerationSeed;
        var observedGalaxy = global::Galaxy.ProjectObservedSectorMap(
            sectorMap,
            backgroundSettings,
            runtimeCatalog,
            Log);

        var itemManager = new ItemManager(
            runtimeCatalog,
            Settings.GameplaySettings,
            message => Log?.Invoke(message));
        var loadoutItemFactory = new AetheriaUnityLoadoutItemFactory(itemManager, runtimeCatalog);
        ZoneRenderer.SetDroppedPickupItemFactory(loadoutItemFactory.CreateLoadoutItem);
        ZoneRenderer.BodySettingsCollections = Settings.BodySettingsCollections;
        ZoneRenderer.RenderSettings = ResolveDaemonRenderSettings(runtimeState);
        CockpitHudShell.SetRenderSettings(ZoneRenderer.RenderSettings);

        if (!AetheriaUnityRuntimeClientProvider.PlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap)
            ZoneRenderer.ShowAsteroidUI = false;

        return new AetheriaUnityGameplayBootResult(
            runtimeCatalog,
            observedGalaxy,
            itemManager,
            loadoutItemFactory);
    }

    private AetheriaRuntimeDaemonRenderSettings ResolveDaemonRenderSettings(AetheriaClientState runtimeState)
    {
        var frame = runtimeState?.DaemonFrame?.Latest();
        if (frame == null)
            throw new InvalidOperationException("Aetheria daemon frame is required before gameplay boot; render/game-feel settings are daemon authority.");

        Log?.Invoke($"Aetheria render/game-feel settings: daemon frame {frame.FrameId}");
        return frame.RenderSettings;
    }

    private AetheriaRuntimeStateBootReport SyncRemoteReplicaBeforeBoot(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!ShouldSyncRemoteReplicaBeforeBoot(stateBoot))
            return stateBoot;

        Log?.Invoke($"Aetheria remote Verse replica missing; syncing {stateBoot.TargetLabel} from {stateBoot.CultMeshAddress}");
        try
        {
            var target = AetheriaState.At(AetheriaUnityRuntimePaths.GameDataDirectory)
                .ClientTarget
                .SyncReplica();
            AetheriaUnityRuntimePaths.ClearRuntimeStateFilePathCache();

            var refreshed = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
            Log?.Invoke($"Aetheria remote Verse replica synced: {refreshed.StateFilePath}");
            if (!string.IsNullOrWhiteSpace(target.LastReplicaSyncAtUtc))
                Log?.Invoke($"Aetheria remote Verse replica sync time: {target.LastReplicaSyncAtUtc}");
            return refreshed;
        }
        catch (Exception ex)
        {
            AetheriaUnityRuntimePaths.ClearRuntimeStateFilePathCache();
            throw new InvalidOperationException(
                $"Aetheria remote Verse replica sync failed before gameplay boot: {ex.Message}",
                ex);
        }
    }

    private static bool ShouldSyncRemoteReplicaBeforeBoot(AetheriaRuntimeStateBootReport stateBoot)
    {
        return string.Equals(stateBoot.TargetSource, "client-target", StringComparison.Ordinal)
            && string.Equals(stateBoot.TargetKind, AetheriaRuntimeClientTargetKinds.CultMeshVerse, StringComparison.Ordinal)
            && (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists);
    }
}

public readonly struct AetheriaUnityGameplayBootResult
{
    public AetheriaUnityGameplayBootResult(
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Galaxy observedGalaxy,
        ItemManager itemManager,
        AetheriaUnityLoadoutItemFactory loadoutItemFactory)
    {
        RuntimeCatalog = runtimeCatalog;
        ObservedGalaxy = observedGalaxy;
        ItemManager = itemManager;
        LoadoutItemFactory = loadoutItemFactory;
    }

    public AetheriaRuntimeCatalogSnapshot RuntimeCatalog { get; }
    public Galaxy ObservedGalaxy { get; }
    public ItemManager ItemManager { get; }
    public AetheriaUnityLoadoutItemFactory LoadoutItemFactory { get; }
}
