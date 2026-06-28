/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using GameCult.Aetheria.State.Verse;

public sealed class AetheriaUnityObservedEntityRestorer
{
    private readonly AetheriaUnityObservedEntityIndex _entityIndex;
    private readonly ItemManager _itemManager;
    private readonly Func<AetheriaRuntimeEntitySnapshot, bool, EntityConstructionBlueprint> _createBlueprint;
    private readonly Func<AetheriaRuntimeLoadoutItemSnapshot, ItemInstance> _createLoadoutItem;
    private readonly Action<string> _logWarning;

    public AetheriaUnityObservedEntityRestorer(
        AetheriaUnityObservedEntityIndex entityIndex,
        ItemManager itemManager,
        Func<AetheriaRuntimeEntitySnapshot, bool, EntityConstructionBlueprint> createBlueprint,
        Func<AetheriaRuntimeLoadoutItemSnapshot, ItemInstance> createLoadoutItem,
        Action<string> logWarning)
    {
        _entityIndex = entityIndex ?? throw new ArgumentNullException(nameof(entityIndex));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _createBlueprint = createBlueprint ?? throw new ArgumentNullException(nameof(createBlueprint));
        _createLoadoutItem = createLoadoutItem ?? throw new ArgumentNullException(nameof(createLoadoutItem));
        _logWarning = logWarning ?? (_ => { });
    }

    public bool TryApplyInPlace(
        string lastRunId,
        int lastZoneIndex,
        string runId,
        int zoneIndex,
        IReadOnlyList<AetheriaRuntimeEntitySnapshot> entitySnapshots,
        string currentEntityKey,
        Entity currentEntity,
        out Entity reboundCurrentEntity)
    {
        reboundCurrentEntity = null;
        if (!string.Equals(lastRunId, runId, StringComparison.Ordinal) ||
            lastZoneIndex != zoneIndex ||
            _entityIndex.Count != entitySnapshots.Count)
        {
            return false;
        }

        foreach (var snapshot in entitySnapshots)
        {
            if (!_entityIndex.ContainsRecordKey(snapshot.RecordKey))
                return false;
        }

        ApplyInPlace(entitySnapshots);
        if (_entityIndex.TryResolveEntityByRecordKey(currentEntityKey, out var resolvedCurrentEntity) &&
            currentEntity != resolvedCurrentEntity)
        {
            reboundCurrentEntity = resolvedCurrentEntity;
        }

        return true;
    }

    public void Replace(
        IReadOnlyList<AetheriaRuntimeEntitySnapshot> entitySnapshots,
        string currentEntityKey,
        Zone zone)
    {
        var restoredEntities = new Dictionary<string, Entity>();
        foreach (var entitySnapshot in entitySnapshots)
        {
            var blueprint = _createBlueprint(
                entitySnapshot,
                string.Equals(entitySnapshot.RecordKey, currentEntityKey, StringComparison.Ordinal));
            if (blueprint == null)
            {
                _logWarning($"Typed entity snapshot {entitySnapshot.RecordKey} could not be lowered into a runtime entity.");
                continue;
            }

            var entity = EntityConstructionBlueprintProjector.ProjectObservedFromBlueprint(_itemManager, zone, blueprint);
            if (entity == null)
                continue;

            ApplyPoseAndSimpleRuntimeState(entity, entitySnapshot);
            entity.RestoreActiveState(entitySnapshot.IsActive);
            restoredEntities[entitySnapshot.RecordKey] = entity;
        }

        foreach (var entitySnapshot in entitySnapshots)
        {
            if (!restoredEntities.TryGetValue(entitySnapshot.RecordKey, out var entity))
                continue;

            entity.HeatsinksEnabled = entitySnapshot.HeatsinksEnabled;
            entity.RestoreStatGrids(entitySnapshot.StatGrids);
            entity.RestoreThermalExposure((float)entitySnapshot.Heatstroke, (float)entitySnapshot.Hypothermia);
            RestoreActiveConsumables(entity, entitySnapshot);
            RestoreRuntimeBehaviorState(entity, entitySnapshot, restoredEntities);
        }

        _entityIndex.Replace(restoredEntities);
    }

    private void ApplyInPlace(IReadOnlyList<AetheriaRuntimeEntitySnapshot> entitySnapshots)
    {
        foreach (var entitySnapshot in entitySnapshots)
        {
            if (!_entityIndex.TryResolveEntityByRecordKey(entitySnapshot.RecordKey, out var entity))
                continue;

            ApplyPoseAndSimpleRuntimeState(entity, entitySnapshot);
            entity.HeatsinksEnabled = entitySnapshot.HeatsinksEnabled;
            if (entity.Settings != null)
                entity.Settings.ShutdownPerformance = (float)entitySnapshot.ShutdownPerformance;
            entity.RestoreThermalExposure((float)entitySnapshot.Heatstroke, (float)entitySnapshot.Hypothermia);
            RestoreRuntimeBehaviorState(entity, entitySnapshot, _entityIndex.EntitiesByRecordKey);
        }

        _entityIndex.RefreshDaemonIndex();
    }

    private static void ApplyPoseAndSimpleRuntimeState(Entity entity, AetheriaRuntimeEntitySnapshot entitySnapshot)
    {
        entity.DaemonEntityIndex = entitySnapshot.EntityIndex;
        entity.Name = entitySnapshot.Name ?? "";
        entity.CultPosition = new CultMath.float3((float)entitySnapshot.PositionX, (float)entitySnapshot.PositionY, (float)entitySnapshot.PositionZ);
        entity.CultDirection = new CultMath.float2((float)entitySnapshot.DirectionX, (float)entitySnapshot.DirectionY);
        entity.CultVelocity = new CultMath.float2((float)entitySnapshot.VelocityX, (float)entitySnapshot.VelocityY);
        entity.OverrideShutdown = entitySnapshot.OverrideShutdown;
        entity.TractorPower = (float)entitySnapshot.TractorPower;
    }

    private void RestoreActiveConsumables(Entity entity, AetheriaRuntimeEntitySnapshot snapshot)
    {
        foreach (var activeConsumable in snapshot.ActiveConsumables)
        {
            var item = _createLoadoutItem(new AetheriaRuntimeLoadoutItemSnapshot(
                activeConsumable.ItemKey,
                activeConsumable.Quality,
                1,
                1,
                true,
                false)) as ConsumableItem;
            if (item == null)
            {
                _logWarning($"Typed active consumable {activeConsumable.ItemKey} could not be lowered for restored entity {snapshot.RecordKey}.");
                continue;
            }

            entity.RestoreActiveConsumable(
                item,
                (float)activeConsumable.RemainingDuration,
                (float)activeConsumable.Duration);
        }
    }

    private static void RestoreRuntimeBehaviorState(
        Entity entity,
        AetheriaRuntimeEntitySnapshot snapshot,
        IReadOnlyDictionary<string, Entity> restoredEntities)
    {
        foreach (var weaponState in snapshot.WeaponStates)
        {
            if (!(ResolveRuntimeBehavior(entity, weaponState.OwnerKind, weaponState.OwnerIndex, weaponState.BehaviorIndex) is Weapon weapon))
                continue;

            if (weapon is LockWeapon lockWeapon)
            {
                restoredEntities.TryGetValue(weaponState.LockTargetEntityKey, out var lockTarget);
                lockWeapon.RestoreRuntimeState(
                    weaponState.Firing,
                    weaponState.Ammo,
                    weaponState.BurstRemaining,
                    (float)weaponState.BurstTimer,
                    (float)weaponState.BurstInterval,
                    (float)weaponState.CooldownProgress,
                    weaponState.CoolingDown,
                    (float)weaponState.LockProgress,
                    lockTarget);
            }
            else if (weapon is ChargedWeapon chargedWeapon)
            {
                chargedWeapon.RestoreRuntimeState(
                    weaponState.Firing,
                    weaponState.Ammo,
                    weaponState.BurstRemaining,
                    (float)weaponState.BurstTimer,
                    (float)weaponState.BurstInterval,
                    (float)weaponState.CooldownProgress,
                    weaponState.CoolingDown,
                    weaponState.Charging,
                    weaponState.Charged,
                    (float)weaponState.Charge);
            }
            else if (weapon is ConstantWeapon constantWeapon)
            {
                constantWeapon.RestoreRuntimeState(
                    weaponState.Firing,
                    weaponState.Ammo,
                    (float)weaponState.AmmoIntervalProgress,
                    (float)weaponState.ReloadProgress,
                    weaponState.Reloading);
            }
            else if (weapon is InstantWeapon instantWeapon)
            {
                instantWeapon.RestoreRuntimeState(
                    weaponState.Firing,
                    weaponState.Ammo,
                    weaponState.BurstRemaining,
                    (float)weaponState.BurstTimer,
                    (float)weaponState.BurstInterval,
                    (float)weaponState.CooldownProgress,
                    weaponState.CoolingDown);
            }
            else
            {
                weapon.RestoreRuntimeState(weaponState.Firing);
            }
        }

        foreach (var behaviorState in snapshot.BehaviorStates)
        {
            var behavior = ResolveRuntimeBehavior(entity, behaviorState.OwnerKind, behaviorState.OwnerIndex, behaviorState.BehaviorIndex);
            switch (behavior)
            {
                case Sensor sensor:
                    sensor.RestoreRuntimeState(
                        behaviorState.Pinging,
                        (float)behaviorState.PingCooldown,
                        (float)behaviorState.PingLerp,
                        (float)behaviorState.PingRadius);
                    break;
                case Radiator radiator:
                    radiator.RestoreRuntimeState(
                        (float)behaviorState.RadiatorTemperature,
                        (float)behaviorState.Emissivity,
                        (float)behaviorState.PumpedHeat,
                        (float)behaviorState.WasteHeat,
                        (float)behaviorState.EnergyUsage);
                    break;
                case Reactor reactor:
                    reactor.RestoreRuntimeState(
                        (float)behaviorState.ReactorDraw,
                        (float)behaviorState.ReactorLoadRatio);
                    break;
                case Capacitor capacitor:
                    capacitor.RestoreRuntimeState(
                        (float)behaviorState.CapacitorCharge,
                        (float)behaviorState.CapacitorCapacity,
                        (float)behaviorState.CapacitorEfficiency);
                    break;
                case AetherDrive drive:
                    drive.RestoreRuntimeState(
                        new CultMath.float3((float)behaviorState.AetherDriveAxisX, (float)behaviorState.AetherDriveAxisY, (float)behaviorState.AetherDriveAxisZ),
                        new CultMath.float3((float)behaviorState.AetherDriveThrustX, (float)behaviorState.AetherDriveThrustY, (float)behaviorState.AetherDriveThrustZ),
                        new CultMath.float3((float)behaviorState.AetherDriveRpmX, (float)behaviorState.AetherDriveRpmY, (float)behaviorState.AetherDriveRpmZ),
                        (float)behaviorState.AetherDriveMaximumRpm,
                        new CultMath.float2((float)behaviorState.AetherDriveThrustDirectionX, (float)behaviorState.AetherDriveThrustDirectionY));
                    break;
                case ResourceScanner resourceScanner:
                    resourceScanner.RestoreRuntimeState(
                        behaviorState.ResourceScannerTargetBodyKey,
                        behaviorState.ResourceScannerAsteroidIndex,
                        (float)behaviorState.ResourceScannerScanTime,
                        (float)behaviorState.ResourceScannerRange,
                        (float)behaviorState.ResourceScannerMinimumDensity,
                        (float)behaviorState.ResourceScannerScanDuration);
                    break;
                case MiningTool miningTool:
                    miningTool.RestoreRuntimeState(
                        behaviorState.MiningToolAsteroidBeltKey,
                        behaviorState.MiningToolAsteroidIndex,
                        (float)behaviorState.MiningToolRange);
                    break;
                case Thruster thruster:
                    thruster.RestoreRuntimeState(
                        (float)behaviorState.ThrusterAxis,
                        (float)behaviorState.ThrusterThrust);
                    break;
                case Shield shield:
                    shield.RestoreRuntimeState(
                        (float)behaviorState.ShieldEfficiency,
                        (float)behaviorState.ShieldEnergyUsage);
                    break;
                case VelocityLimit velocityLimit:
                    velocityLimit.RestoreRuntimeState((float)behaviorState.VelocityLimit);
                    break;
                case Thermotoggle thermotoggle:
                    thermotoggle.TargetTemperature = (float)behaviorState.ThermotoggleTargetTemperature;
                    break;
                case Switch switchBehavior:
                    switchBehavior.Activated = behaviorState.SwitchActivated;
                    break;
                case Trigger trigger:
                    trigger.RestoreRuntimeState(behaviorState.TriggerPulled);
                    break;
                case StatModifier statModifier:
                    statModifier.RestoreRuntimeState(
                        behaviorState.StatModifierApplied,
                        behaviorState.StatModifierExecuted);
                    break;
                case TurretController turretController:
                    turretController.RestoreRuntimeState(
                        (float)behaviorState.TurretControllerShotSpeed,
                        behaviorState.TurretControllerPredictShots);
                    break;
            }
        }
    }

    private static Behavior ResolveRuntimeBehavior(Entity entity, string ownerKind, int ownerIndex, int behaviorIndex)
    {
        var behaviors = ResolveRuntimeBehaviorList(entity, ownerKind, ownerIndex);
        return behaviors != null && behaviorIndex >= 0 && behaviorIndex < behaviors.Count
            ? behaviors[behaviorIndex]
            : null;
    }

    private static IReadOnlyList<Behavior> ResolveRuntimeBehaviorList(Entity entity, string ownerKind, int ownerIndex)
    {
        if (entity == null || ownerIndex < 0)
            return null;

        switch (ownerKind)
        {
            case "equipment":
                return ownerIndex < entity.Equipment.Count ? entity.Equipment[ownerIndex].Behaviors : null;
            case "active_consumable":
                return ownerIndex < entity.ActiveConsumables.Count ? entity.ActiveConsumables[ownerIndex].Behaviors : null;
            default:
                return null;
        }
    }
}
