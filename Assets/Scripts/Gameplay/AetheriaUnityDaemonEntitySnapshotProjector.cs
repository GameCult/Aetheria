/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using Regex = System.Text.RegularExpressions.Regex;

public static class AetheriaUnityDaemonEntitySnapshotProjector
{
    public static AetheriaRuntimeEntitySnapshot[] CreateSnapshots(
        string runId,
        AetheriaRuntimeZoneSnapshotCommit zone)
    {
        if (zone == null)
            return Array.Empty<AetheriaRuntimeEntitySnapshot>();

        return CreateSnapshots(runId, zone.ZoneIndex, zone.Entities);
    }

    public static AetheriaRuntimeEntitySnapshot[] CreateSnapshots(
        string runId,
        int zoneIndex,
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities)
    {
        return (entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            .Where(entity => entity != null)
            .Select(entity => CreateSnapshot(runId, zoneIndex, entity))
            .ToArray();
    }

    public static int EntityIndexFromRecordKey(string recordKey)
    {
        var match = Regex.Match(recordKey ?? "", @"\.entity\.(\d+)\.v1$");
        return match.Success && int.TryParse(match.Groups[1].Value, out var index)
            ? index
            : int.MaxValue;
    }

    public static string DaemonEntityRecordKey(string runId, int zoneIndex, int entityIndex)
    {
        return string.IsNullOrWhiteSpace(runId)
            ? ""
            : $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
    }

    private static AetheriaRuntimeEntitySnapshot CreateSnapshot(
        string runId,
        int zoneIndex,
        AetheriaRuntimeEntitySnapshotCommit entity)
    {
        return new AetheriaRuntimeEntitySnapshot(
            DaemonEntityRecordKey(runId, zoneIndex, entity.EntityIndex),
            entity.Name ?? "",
            entity.Kind ?? "",
            entity.PositionX,
            entity.PositionY,
            entity.PositionZ,
            entity.DirectionX,
            entity.DirectionY,
            entity.FactionKey ?? "",
            entity.HullItemKey ?? "",
            CreateItemSlots(entity.Equipment),
            CreateItemSlots(entity.CargoBays),
            CreateItemSlots(entity.DockingBays),
            (entity.ChildEntityIndices ?? Array.Empty<int>())
                .Where(index => index >= 0)
                .Select(index => DaemonEntityRecordKey(runId, zoneIndex, index))
                .ToArray(),
            (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>()).ToArray())
                .ToArray(),
            (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
                .Select(grid => new AetheriaRuntimeEntityStatGridSnapshot(
                    grid.Name ?? "",
                    grid.Width,
                    grid.Height,
                    (grid.Values ?? Array.Empty<double>()).ToArray()))
                .ToArray(),
            entity.VelocityX,
            entity.VelocityY,
            entity.TargetEntityIndex < 0 ? "" : DaemonEntityRecordKey(runId, zoneIndex, entity.TargetEntityIndex),
            entity.IsActive,
            entity.HeatsinksEnabled,
            entity.OverrideShutdown,
            entity.TractorPower,
            entity.Heatstroke,
            entity.Hypothermia,
            (entity.ActiveConsumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
                .Select(consumable => new AetheriaRuntimeActiveConsumableSnapshot(
                    consumable.ItemKey ?? "",
                    consumable.Quality,
                    consumable.RemainingDuration,
                    consumable.Duration))
                .ToArray(),
            (entity.BehaviorProgress ?? Array.Empty<AetheriaRuntimeBehaviorProgressCommit>())
                .Select(progress => new AetheriaRuntimeBehaviorProgressSnapshot(
                    progress.OwnerKind ?? "",
                    progress.OwnerIndex,
                    progress.BehaviorIndex,
                    progress.BehaviorKind ?? "",
                    progress.Progress))
                .ToArray(),
            CreateWeaponStates(runId, zoneIndex, entity.WeaponStates),
            CreateBehaviorStates(entity.BehaviorStates),
            CreateCargoBays(entity.CargoContents),
            CreateCargoBays(entity.DockingBayContents),
            (entity.DockingBayAssignments ?? Array.Empty<int>()).ToArray(),
            entity.Visibility,
            entity.VisibilitySourceCount,
            (entity.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                .Where(contact => contact != null && contact.TargetEntityIndex >= 0)
                .Select(contact => new AetheriaRuntimeEntityContactSnapshot(
                    DaemonEntityRecordKey(runId, zoneIndex, contact.TargetEntityIndex),
                    contact.InfoGathered,
                    contact.Hostile,
                    contact.Visible))
                .ToArray(),
            entity.ShutdownPerformance);
    }

    private static AetheriaRuntimeEntityItemSlotSnapshot[] CreateItemSlots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            .Where(slot => slot?.Item != null)
            .Select(slot => new AetheriaRuntimeEntityItemSlotSnapshot(
                slot.X,
                slot.Y,
                slot.Item.ItemKey ?? "",
                slot.Item.Quality,
                slot.Item.Durability,
                slot.Item.Quantity,
                slot.Item.Enabled,
                slot.Item.OverrideShutdown,
                slot.Item.Temperature))
            .ToArray();
    }

    private static AetheriaRuntimeCargoBayLoadoutSnapshot[] CreateCargoBays(
        IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> cargoBays)
    {
        return (cargoBays ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
            .Select(bay => new AetheriaRuntimeCargoBayLoadoutSnapshot(
                CreateLoadoutSlots(bay?.Items)))
            .ToArray();
    }

    private static AetheriaRuntimeLoadoutItemSlotSnapshot[] CreateLoadoutSlots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            .Where(slot => slot?.Item != null)
            .Select(slot => new AetheriaRuntimeLoadoutItemSlotSnapshot(
                slot.X,
                slot.Y,
                new AetheriaRuntimeLoadoutItemSnapshot(
                    slot.Item.ItemKey ?? "",
                    slot.Item.Quality,
                    slot.Item.Durability,
                    slot.Item.Quantity,
                    slot.Item.Enabled,
                    slot.Item.OverrideShutdown,
                    slot.Item.Temperature)))
            .ToArray();
    }

    private static AetheriaRuntimeWeaponStateSnapshot[] CreateWeaponStates(
        string runId,
        int zoneIndex,
        IReadOnlyList<AetheriaRuntimeWeaponStateCommit> weaponStates)
    {
        return (weaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
            .Where(state => state != null)
            .Select(state => new AetheriaRuntimeWeaponStateSnapshot(
                state.OwnerKind ?? "",
                state.OwnerIndex,
                state.BehaviorIndex,
                state.BehaviorKind ?? "",
                state.Firing,
                state.Ammo,
                state.BurstRemaining,
                state.BurstTimer,
                state.BurstInterval,
                state.CooldownProgress,
                state.CoolingDown,
                state.Charging,
                state.Charged,
                state.Charge,
                state.Reloading,
                state.ReloadProgress,
                state.AmmoIntervalProgress,
                state.LockProgress,
                state.LockTargetEntityIndex < 0 ? "" : DaemonEntityRecordKey(runId, zoneIndex, state.LockTargetEntityIndex)))
            .ToArray();
    }

    private static AetheriaRuntimeBehaviorStateSnapshot[] CreateBehaviorStates(
        IReadOnlyList<AetheriaRuntimeBehaviorStateCommit> behaviorStates)
    {
        return (behaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
            .Where(state => state != null)
            .Select(state => new AetheriaRuntimeBehaviorStateSnapshot(
                state.OwnerKind ?? "",
                state.OwnerIndex,
                state.BehaviorIndex,
                state.BehaviorKind ?? "",
                state.Pinging,
                state.PingCooldown,
                state.PingLerp,
                state.PingRadius,
                state.PingedEntityCount,
                state.RadiatorTemperature,
                state.Emissivity,
                state.PumpedHeat,
                state.WasteHeat,
                state.EnergyUsage,
                state.ReactorDraw,
                state.ReactorLoadRatio,
                state.CapacitorCharge,
                state.CapacitorCapacity,
                state.CapacitorEfficiency,
                state.AetherDriveAxisX,
                state.AetherDriveAxisY,
                state.AetherDriveAxisZ,
                state.AetherDriveThrustX,
                state.AetherDriveThrustY,
                state.AetherDriveThrustZ,
                state.AetherDriveRpmX,
                state.AetherDriveRpmY,
                state.AetherDriveRpmZ,
                state.AetherDriveMaximumRpm,
                state.AetherDriveThrustDirectionX,
                state.AetherDriveThrustDirectionY,
                state.ResourceScannerTargetBodyKey ?? "",
                state.ResourceScannerAsteroidIndex,
                state.ResourceScannerScanTime,
                state.ResourceScannerRange,
                state.ResourceScannerMinimumDensity,
                state.ResourceScannerScanDuration,
                state.MiningToolAsteroidBeltKey ?? "",
                state.MiningToolAsteroidIndex,
                state.MiningToolRange,
                state.ThrusterAxis,
                state.ThrusterThrust,
                state.ThrusterTorque,
                state.ShieldEfficiency,
                state.ShieldEnergyUsage,
                state.VelocityLimit,
                state.ThermotoggleTargetTemperature,
                state.SwitchActivated,
                state.TriggerPulled,
                state.StatModifierApplied,
                state.StatModifierExecuted,
                state.StatModifierTargetStatCount,
                state.TurretControllerWeaponCount,
                state.TurretControllerShotSpeed,
                state.TurretControllerPredictShots))
            .ToArray();
    }
}
