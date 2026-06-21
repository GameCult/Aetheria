using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonOperationResult
    {
        public AetheriaRuntimeDaemonOperationResult(
            AetheriaRuntimeRunCheckpointCommit run,
            IReadOnlyList<string> appliedCommandIds,
            IReadOnlyList<string> rejectedCommandIds,
            AetheriaRuntimeDaemonIntentState? intents = null)
        {
            Run = run ?? new AetheriaRuntimeRunCheckpointCommit();
            AppliedCommandIds = appliedCommandIds ?? Array.Empty<string>();
            RejectedCommandIds = rejectedCommandIds ?? Array.Empty<string>();
            Intents = intents ?? new AetheriaRuntimeDaemonIntentState();
        }

        public AetheriaRuntimeRunCheckpointCommit Run { get; }
        public IReadOnlyList<string> AppliedCommandIds { get; }
        public IReadOnlyList<string> RejectedCommandIds { get; }
        public AetheriaRuntimeDaemonIntentState Intents { get; }
    }

    public sealed class AetheriaRuntimeDaemonOperationContext
    {
        public IReadOnlyList<AetheriaRuntimeLoadoutTemplateCommit> LoadoutTemplates { get; set; } =
            Array.Empty<AetheriaRuntimeLoadoutTemplateCommit>();

        public AetheriaRuntimeDaemonIntentState Intents { get; set; } = new AetheriaRuntimeDaemonIntentState();
    }

    public static class AetheriaRuntimeDaemonOperations
    {
        public static AetheriaRuntimeDaemonOperationResult Execute(
            AetheriaRuntimeRunCheckpointCommit run,
            IEnumerable<AetheriaRuntimeDaemonCommandDocument> commands)
        {
            return Execute(run, commands, new AetheriaRuntimeDaemonOperationContext());
        }

        public static AetheriaRuntimeDaemonOperationResult Execute(
            AetheriaRuntimeRunCheckpointCommit run,
            IEnumerable<AetheriaRuntimeDaemonCommandDocument> commands,
            AetheriaRuntimeDaemonOperationContext context)
        {
            run ??= new AetheriaRuntimeRunCheckpointCommit();
            context ??= new AetheriaRuntimeDaemonOperationContext();
            var applied = new List<string>();
            var rejected = new List<string>();

            foreach (var command in commands ?? Enumerable.Empty<AetheriaRuntimeDaemonCommandDocument>())
            {
                if (command == null || string.IsNullOrWhiteSpace(command.CommandId))
                    continue;

                if (ApplyOne(run, command, context))
                    applied.Add(command.CommandId);
                else
                    rejected.Add(command.CommandId);
            }

            return new AetheriaRuntimeDaemonOperationResult(run, applied, rejected, context.Intents);
        }

        private static bool ApplyOne(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            switch (command.Kind)
            {
                case AetheriaRuntimeDaemonCommandKinds.SetTarget:
                    return ApplySetTarget(run, command);
                case AetheriaRuntimeDaemonCommandKinds.ClearTarget:
                    return ApplyClearTarget(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetLookDirection:
                    return ApplySetLookDirection(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetTractorPower:
                    if (!IsNormalizedScalar(command.ScalarValue))
                        return false;

                    return ApplyCurrentEntity(run, command, entity =>
                        entity.TractorPower = command.ScalarValue);
                case AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled:
                    return ApplyCurrentEntity(run, command, entity =>
                        entity.HeatsinksEnabled = command.ScalarValue > 0.5);
                case AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown:
                    return ApplyTargetOrCurrentEntity(run, command, entity =>
                        entity.OverrideShutdown = command.ScalarValue > 0.5);
                case AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance:
                    if (command.ScalarValue < 0.0 || command.ScalarValue > 1.0)
                        return false;

                    return ApplyTargetEntity(run, command.TargetEntityKey, entity =>
                        entity.ShutdownPerformance = command.ScalarValue);
                case AetheriaRuntimeDaemonCommandKinds.SetItemEnabled:
                    return ApplyCurrentEquipmentItem(run, command, item =>
                        item.Enabled = command.ScalarValue > 0.5);
                case AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled:
                    return ApplyToggleEquipmentBehaviorItem(run, command, "Shield", item =>
                        item.Enabled = !item.Enabled);
                case AetheriaRuntimeDaemonCommandKinds.SetItemOverrideShutdown:
                    return ApplyTargetEquipmentItem(run, command, item =>
                        item.OverrideShutdown = command.ScalarValue > 0.5);
                case AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership:
                    return ApplySetWeaponGroupMembership(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetThermotoggleTargetTemperature:
                    return ApplySetThermotoggleTargetTemperature(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetEntityName:
                    return ApplyTargetEntity(run, command.TargetEntityKey, entity =>
                        entity.Name = command.TextValue ?? "");
                case AetheriaRuntimeDaemonCommandKinds.DestroyEntity:
                    return ApplyDestroyEntity(run, command);
                case AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip:
                    return ApplySetDockedCurrentShip(run, command.TargetEntityKey);
                case AetheriaRuntimeDaemonCommandKinds.SetActionBarBinding:
                    return ApplySetActionBarBinding(run, command);
                case AetheriaRuntimeDaemonCommandKinds.ClearActionBarBinding:
                    return ApplyClearActionBarBinding(run, command.TextValue);
                case AetheriaRuntimeDaemonCommandKinds.TransferCargoItem:
                    return ApplyTransferCargoItem(run, command);
                case AetheriaRuntimeDaemonCommandKinds.EquipItem:
                    return ApplyEquipItem(run, command);
                case AetheriaRuntimeDaemonCommandKinds.StoreItem:
                    return ApplyStoreItem(run, command);
                case AetheriaRuntimeDaemonCommandKinds.PickUpLoot:
                    return ApplyPickUpLoot(run, command);
                case AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity:
                    return ApplyToggleHullConductivity(run, command);
                case AetheriaRuntimeDaemonCommandKinds.TradePurchase:
                    return ApplyTradePurchase(run, command);
                case AetheriaRuntimeDaemonCommandKinds.RestoreLoadout:
                    return ApplyRestoreLoadout(run, command, context);
                case AetheriaRuntimeDaemonCommandKinds.SetMoveVector:
                    return ApplySetMoveVector(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup:
                    return ApplyFireWeaponGroup(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive:
                    return ApplySetWeaponGroupActive(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive:
                    return ApplySetBehaviorActive(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.ActivateConsumable:
                    return ApplyActivateConsumable(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.SensorPing:
                    return ApplySensorPing(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.Dock:
                    return ApplyDockIntent(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.DockNearest:
                    return ApplyDockNearestIntent(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.Undock:
                    return ApplyUndockIntent(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.EnterWormhole:
                    return ApplyEnterWormholeIntent(run, command, context.Intents);
                case AetheriaRuntimeDaemonCommandKinds.TowToStation:
                    return ApplyTowToStationIntent(run, command, context.Intents);
                default:
                    return false;
            }
        }

        private static bool ApplySetTarget(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var actorKey = string.IsNullOrWhiteSpace(command.ActorEntityKey)
                ? run.CurrentEntityKey
                : command.ActorEntityKey;
            if (!TryResolveEntity(run, actorKey, out var actorZone, out var actorIndex, out var actor) ||
                !TryResolveEntity(run, command.TargetEntityKey, out var targetZone, out var targetIndex, out _) ||
                actorZone != targetZone ||
                actorIndex == targetIndex)
            {
                return false;
            }

            if (!(actor.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    .Any(contact => contact != null && contact.TargetEntityIndex == targetIndex && contact.Visible))
            {
                return false;
            }

            actor.TargetEntityIndex = targetIndex;
            return true;
        }

        private static bool ApplyClearTarget(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            return ApplyCurrentEntity(run, command, entity => entity.TargetEntityIndex = -1);
        }

        private static bool ApplySetLookDirection(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            return ApplyCurrentEntity(run, command, entity =>
            {
                entity.DirectionX = command.DirectionX;
                entity.DirectionY = command.PositionZ;
            });
        }

        private static bool ApplySetCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey)
        {
            if (!TryResolveEntity(run, entityKey, out var zoneIndex, out var entityIndex, out _))
                return false;

            run.CurrentZoneIndex = zoneIndex;
            run.CurrentEntityKey = BuildEntityKey(run.RunId, zoneIndex, entityIndex);
            return true;
        }

        private static bool ApplySetDockedCurrentShip(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey)
        {
            if (!TryResolveEntity(run, entityKey, out var zoneIndex, out var entityIndex, out _))
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var isDocked = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Any(entity =>
                    entity != null &&
                    entity.EntityIndex != entityIndex &&
                    (entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(entityIndex));
            if (!isDocked)
                return false;

            run.CurrentZoneIndex = zoneIndex;
            run.CurrentEntityKey = BuildEntityKey(run.RunId, zoneIndex, entityIndex);
            return true;
        }

        private static bool ApplySetActionBarBinding(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var controlPath = command.TextValue ?? "";
            if (string.IsNullOrWhiteSpace(controlPath))
                return false;

            var bindingKind = command.ActionBarBinding.Kind ?? "";
            if (string.Equals(bindingKind, "weapon_group", StringComparison.Ordinal))
            {
                var entityKey = string.IsNullOrWhiteSpace(command.ActorEntityKey)
                    ? run.CurrentEntityKey
                    : command.ActorEntityKey;
                if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                    return false;

                var weaponGroups = entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>();
                if (command.WeaponGroup < 0 || command.WeaponGroup >= weaponGroups.Count)
                    return false;
            }

            var binding = new AetheriaRuntimeActionBarBindingCommit
            {
                ControlPath = controlPath,
                Kind = bindingKind,
                ItemKey = command.ActionBarBinding.ItemKey ?? "",
                EquipmentIndex = command.EquipmentIndex,
                BehaviorIndex = command.BehaviorIndex,
                WeaponGroup = command.WeaponGroup
            };

            var bindings = (run.ActionBarBindings ?? Array.Empty<AetheriaRuntimeActionBarBindingCommit>())
                .Where(existing => !string.Equals(existing?.ControlPath ?? "", controlPath, StringComparison.Ordinal))
                .Concat(new[] { binding })
                .ToArray();
            run.ActionBarBindings = bindings;
            return true;
        }

        private static bool ApplyClearActionBarBinding(
            AetheriaRuntimeRunCheckpointCommit run,
            string controlPath)
        {
            if (string.IsNullOrWhiteSpace(controlPath))
                return false;

            run.ActionBarBindings = (run.ActionBarBindings ?? Array.Empty<AetheriaRuntimeActionBarBindingCommit>())
                .Where(existing => !string.Equals(existing?.ControlPath ?? "", controlPath, StringComparison.Ordinal))
                .ToArray();
            return true;
        }

        private static bool ApplyTargetOrCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            Action<AetheriaRuntimeEntitySnapshotCommit> mutate)
        {
            return string.IsNullOrWhiteSpace(command.TargetEntityKey)
                ? ApplyCurrentEntity(run, command, mutate)
                : ApplyTargetEntity(run, command.TargetEntityKey, mutate);
        }

        private static bool ApplyCurrentEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            Action<AetheriaRuntimeEntitySnapshotCommit> mutate)
        {
            var entityKey = string.IsNullOrWhiteSpace(command.ActorEntityKey)
                ? run.CurrentEntityKey
                : command.ActorEntityKey;
            return ApplyTargetEntity(run, entityKey, mutate);
        }

        private static bool ApplyCurrentEquipmentItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            Action<AetheriaRuntimeLoadoutItemCommit> mutate)
        {
            var entityKey = string.IsNullOrWhiteSpace(command.TargetEntityKey)
                ? command.ActorEntityKey
                : command.TargetEntityKey;
            if (string.IsNullOrWhiteSpace(entityKey))
                entityKey = run.CurrentEntityKey;

            return ApplyTargetEquipmentItem(run, entityKey, command.EquipmentIndex, mutate);
        }

        private static bool ApplyTargetEquipmentItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            Action<AetheriaRuntimeLoadoutItemCommit> mutate)
        {
            return ApplyTargetEquipmentItem(run, command.TargetEntityKey, command.EquipmentIndex, mutate);
        }

        private static bool ApplyTargetEquipmentItem(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            int equipmentIndex,
            Action<AetheriaRuntimeLoadoutItemCommit> mutate)
        {
            if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                return false;

            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (equipmentIndex < 0 || equipmentIndex >= equipment.Count)
                return false;

            var item = equipment[equipmentIndex]?.Item;
            if (item == null)
                return false;

            mutate(item);
            return true;
        }

        private static bool ApplySetWeaponGroupMembership(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TryResolveEntity(run, command.TargetEntityKey, out _, out _, out var entity) ||
                command.EquipmentIndex < 0 ||
                command.WeaponGroup < 0)
            {
                return false;
            }

            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (command.EquipmentIndex >= equipment.Count ||
                equipment[command.EquipmentIndex]?.Item == null)
            {
                return false;
            }

            var sourceGroups = entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>();
            var groupCount = Math.Max(sourceGroups.Count, command.WeaponGroup + 1);
            var groups = new IReadOnlyList<int>[groupCount];
            for (var i = 0; i < groupCount; i++)
            {
                groups[i] = i < sourceGroups.Count
                    ? (sourceGroups[i] ?? Array.Empty<int>()).ToArray()
                    : Array.Empty<int>();
            }

            var members = groups[command.WeaponGroup]
                .Where(index => index != command.EquipmentIndex)
                .ToList();
            if (command.ScalarValue > 0.5)
                members.Add(command.EquipmentIndex);

            groups[command.WeaponGroup] = members
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            entity.WeaponGroups = groups;
            return true;
        }

        private static bool ApplySetThermotoggleTargetTemperature(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TryResolveEntity(run, command.TargetEntityKey, out _, out _, out var entity))
                return false;

            var behavior = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.OwnerKind, "equipment", StringComparison.Ordinal) &&
                    candidate.OwnerIndex == command.EquipmentIndex &&
                    candidate.BehaviorIndex == command.BehaviorIndex);
            if (behavior == null)
                return false;

            behavior.ThermotoggleTargetTemperature = command.ScalarValue;
            return true;
        }

        private static bool ApplyToggleEquipmentBehaviorItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            string behaviorKind,
            Action<AetheriaRuntimeLoadoutItemCommit> mutate)
        {
            var entityKey = string.IsNullOrWhiteSpace(command.TargetEntityKey)
                ? command.ActorEntityKey
                : command.TargetEntityKey;
            if (string.IsNullOrWhiteSpace(entityKey))
                entityKey = run.CurrentEntityKey;

            if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                return false;

            var behavior = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.OwnerKind, "equipment", StringComparison.Ordinal) &&
                    string.Equals(candidate.BehaviorKind, behaviorKind, StringComparison.Ordinal));
            if (behavior == null || behavior.OwnerIndex < 0)
                return false;

            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            if (behavior.OwnerIndex >= equipment.Count)
                return false;

            var item = equipment[behavior.OwnerIndex]?.Item;
            if (item == null)
                return false;

            mutate(item);
            return true;
        }

        private static bool ApplyTransferCargoItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var transfer = command.CargoTransfer ?? new AetheriaRuntimeCargoTransferCommand();
            if (!TryResolveCargoBay(
                    run,
                    transfer.OriginEntityKey,
                    transfer.OriginCargoIndex,
                    out var originEntity,
                    out var originCargoIndex,
                    out _) ||
                !TryResolveCargoBay(
                    run,
                    transfer.DestinationEntityKey,
                    transfer.DestinationCargoIndex,
                    out var destinationEntity,
                    out var destinationCargoIndex,
                    out _))
            {
                return false;
            }

            if (!transfer.HasDestinationPosition &&
                string.Equals(transfer.OriginEntityKey ?? "", transfer.DestinationEntityKey ?? "", StringComparison.Ordinal) &&
                transfer.OriginCargoIndex == transfer.DestinationCargoIndex)
            {
                return false;
            }

            var sourceX = transfer.SourceX;
            var sourceY = transfer.SourceY;
            if (!TryRemoveCargoItem(
                    originEntity,
                    originCargoIndex,
                    command.TextValue,
                    sourceX,
                    sourceY,
                    out var slot))
            {
                return false;
            }

            slot.X = transfer.HasDestinationPosition
                ? transfer.DestinationX
                : slot.X;
            slot.Y = transfer.HasDestinationPosition
                ? transfer.DestinationY
                : slot.Y;
            AddCargoItem(destinationEntity, destinationCargoIndex, slot);
            return true;
        }

        private static bool ApplyEquipItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var transfer = command.EquipmentTransfer ?? new AetheriaRuntimeEquipmentTransferCommand();
            var sourceKind = transfer.SourceKind ?? "";
            var originEntityKey = transfer.OriginEntityKey ?? "";
            var originIndex = transfer.OriginIndex;
            var sourceX = transfer.SourceX;
            var sourceY = transfer.SourceY;

            AetheriaRuntimeLoadoutItemSlotCommit slot;
            if (string.Equals(sourceKind, "equipment", StringComparison.Ordinal))
            {
                if (!TryRemoveEquipmentItem(run, originEntityKey, originIndex, command.TextValue, out slot))
                    return false;
            }
            else
            {
                if (!TryResolveCargoBay(run, originEntityKey, originIndex, out var originEntity, out var originCargoIndex, out _) ||
                    !TryRemoveCargoItem(originEntity, originCargoIndex, command.TextValue, sourceX, sourceY, out slot))
                {
                    return false;
                }
            }

            if (!TryResolveEntity(run, transfer.DestinationEntityKey, out _, out _, out var destinationEntity))
                return false;

            slot.X = transfer.HasDestinationPosition
                ? transfer.DestinationX
                : slot.X;
            slot.Y = transfer.HasDestinationPosition
                ? transfer.DestinationY
                : slot.Y;
            AddEquipmentItem(destinationEntity, slot);
            return true;
        }

        private static bool ApplyStoreItem(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var store = command.StoreItem ?? new AetheriaRuntimeStoreItemCommand();
            if (!TryRemoveEquipmentItem(
                    run,
                    store.OriginEntityKey,
                    store.SourceEquipmentIndex,
                    command.TextValue,
                    out var slot) ||
                !TryResolveCargoBay(
                    run,
                    store.DestinationEntityKey,
                    store.DestinationCargoIndex,
                    out var destinationEntity,
                    out var destinationCargoIndex,
                    out _))
            {
                return false;
            }

            slot.X = store.HasDestinationPosition
                ? store.DestinationX
                : slot.X;
            slot.Y = store.HasDestinationPosition
                ? store.DestinationY
                : slot.Y;
            AddCargoItem(destinationEntity, destinationCargoIndex, slot);
            return true;
        }

        private static bool ApplyPickUpLoot(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TryResolveEntity(run, command.TargetEntityKey, out var zoneIndex, out _, out var entity))
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var pickups = (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>()).ToList();
            var pickupIndex = pickups.FindIndex(pickup => IsPickupMatch(pickup, command));
            if (pickupIndex < 0)
                return false;

            var pickup = pickups[pickupIndex];
            pickups.RemoveAt(pickupIndex);
            zone.DroppedPickups = pickups.ToArray();

            AddCargoItem(entity, 0, new AetheriaRuntimeLoadoutItemSlotCommit
            {
                Item = pickup.Item ?? new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemKey = command.LootPickup.ItemKey ?? "",
                    Quantity = command.LootPickup.Quantity
                }
            });
            return true;
        }

        private static bool ApplyToggleHullConductivity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TryResolveEntity(run, command.TargetEntityKey, out _, out _, out var entity))
                return false;

            var x = (int)command.PositionX;
            var y = (int)command.PositionY;
            var axis = (int)command.ScalarValue;
            var gridName = axis == 0 ? "hull_conductivity_x" : "hull_conductivity_y";
            var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToArray();
            var grid = grids.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.Name ?? "", gridName, StringComparison.Ordinal));
            if (grid == null ||
                x < 0 ||
                y < 0 ||
                x >= grid.Width ||
                y >= grid.Height)
            {
                return false;
            }

            var values = (grid.Values ?? Array.Empty<double>()).ToArray();
            var index = y * grid.Width + x;
            if (index < 0 || index >= values.Length)
                return false;

            values[index] = values[index] > 0.5 ? 0.0 : 1.0;
            grid.Values = values;
            entity.StatGrids = grids;
            return true;
        }

        private static bool ApplyTradePurchase(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var purchase = command.TradePurchase ?? new AetheriaRuntimeTradePurchaseCommand();
            var totalPrice = purchase.TotalPrice;
            if (totalPrice < 0 || run.Credits < totalPrice)
                return false;

            if (purchase.CreatesDockedShip)
            {
                run.Credits -= totalPrice;
                return true;
            }

            if (!TryResolveCargoBay(
                    run,
                    purchase.StationEntityKey,
                    purchase.StationCargoIndex,
                    out var stationEntity,
                    out var stationCargoIndex,
                    out _) ||
                !TryResolveCargoBay(
                    run,
                    purchase.TargetEntityKey,
                    purchase.TargetCargoIndex,
                    out var targetEntity,
                    out var targetCargoIndex,
                    out _))
            {
                return false;
            }

            var itemKey = purchase.ItemKey ?? "";
            var quantity = Math.Max(1, purchase.Quantity);
            if (!TryRemoveCargoItemQuantity(
                    stationEntity,
                    stationCargoIndex,
                    itemKey,
                    purchase.SourceX,
                    purchase.SourceY,
                    quantity,
                    out var purchasedSlot))
            {
                return false;
            }

            purchasedSlot.X = 0;
            purchasedSlot.Y = 0;
            AddCargoItem(targetEntity, targetCargoIndex, purchasedSlot);
            run.Credits -= totalPrice;
            return true;
        }

        private static bool ApplyRestoreLoadout(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonOperationContext context)
        {
            var restore = command.LoadoutRestore ?? new AetheriaRuntimeLoadoutRestoreCommand();
            var price = restore.Price;
            if (price < 0 || run.Credits < price)
                return false;

            var templateName = restore.TemplateName ?? "";
            if (string.IsNullOrWhiteSpace(command.TargetEntityKey) ||
                string.IsNullOrWhiteSpace(templateName))
            {
                return false;
            }

            var template = (context.LoadoutTemplates ?? Array.Empty<AetheriaRuntimeLoadoutTemplateCommit>())
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.Name ?? "", templateName, StringComparison.Ordinal));
            if (template != null)
            {
                if (!TryParseEntityKey(command.TargetEntityKey, out var zoneIndex, out _))
                    return false;

                var newEntityKey = AetheriaRuntimeLoadoutSnapshotProjector.AppendToZone(
                    run,
                    zoneIndex,
                    command.TargetEntityKey,
                    template);
                if (string.IsNullOrWhiteSpace(newEntityKey))
                    return false;

                run.CurrentEntityKey = newEntityKey;
            }

            run.Credits -= price;
            return true;
        }

        private static bool ApplySetMoveVector(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (!TryResolveEntity(run, actor, out _, out _, out _) ||
                !IsNormalizedScalar(command.ScalarValue) ||
                !IsFinite(command.DirectionX) ||
                !IsFinite(command.DirectionY))
            {
                return false;
            }

            intents.Movement = new AetheriaRuntimeDaemonMovementIntent
            {
                ActorEntityKey = actor,
                DirectionX = command.DirectionX,
                DirectionY = command.DirectionY,
                Magnitude = command.ScalarValue
            };
            return true;
        }

        private static bool ApplyFireWeaponGroup(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (!TryResolveEntity(run, actor, out _, out _, out var entity) ||
                !HasWeaponGroup(entity, command.WeaponGroup))
            {
                return false;
            }

            intents.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
            {
                ActorEntityKey = actor,
                WeaponGroup = command.WeaponGroup,
                Fire = true,
                Active = true
            });
            return true;
        }

        private static bool ApplySetWeaponGroupActive(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (!TryResolveEntity(run, actor, out _, out _, out var entity) ||
                !HasWeaponGroup(entity, command.WeaponGroup))
            {
                return false;
            }

            intents.WeaponGroups.Add(new AetheriaRuntimeDaemonWeaponGroupIntent
            {
                ActorEntityKey = actor,
                WeaponGroup = command.WeaponGroup,
                Active = command.ScalarValue > 0.5
            });
            return true;
        }

        private static bool ApplySetBehaviorActive(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) ||
                command.EquipmentIndex < 0 ||
                command.BehaviorIndex < 0 ||
                !TryResolveEntity(run, actor, out _, out _, out var entity) ||
                !HasEquipmentBehavior(entity, command.EquipmentIndex, command.BehaviorIndex))
            {
                return false;
            }

            intents.Behaviors.Add(new AetheriaRuntimeDaemonBehaviorIntent
            {
                ActorEntityKey = actor,
                EquipmentIndex = command.EquipmentIndex,
                BehaviorIndex = command.BehaviorIndex,
                Active = command.ScalarValue > 0.5
            });
            return true;
        }

        private static bool HasWeaponGroup(AetheriaRuntimeEntitySnapshotCommit entity, int weaponGroup)
        {
            var weaponGroups = entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>();
            return weaponGroup >= 0 && weaponGroup < weaponGroups.Count;
        }

        private static bool HasEquipmentBehavior(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int equipmentIndex,
            int behaviorIndex)
        {
            return (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Any(candidate =>
                    candidate != null &&
                    string.Equals(candidate.OwnerKind, "equipment", StringComparison.Ordinal) &&
                    candidate.OwnerIndex == equipmentIndex &&
                    candidate.BehaviorIndex == behaviorIndex);
        }

        private static bool IsNormalizedScalar(double value)
        {
            return IsFinite(value) && value >= 0.0 && value <= 1.0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool ApplyActivateConsumable(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(command.TextValue))
                return false;

            intents.Consumables.Add(new AetheriaRuntimeDaemonConsumableIntent
            {
                ActorEntityKey = actor,
                ItemKey = command.TextValue ?? ""
            });
            return true;
        }

        private static bool ApplySensorPing(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            if (string.IsNullOrWhiteSpace(ResolveActorEntityKey(run, command)))
                return false;

            intents.SensorPingRequested = true;
            return true;
        }

        private static bool ApplyDockIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(command.TargetEntityKey))
                return false;

            if (!ApplyDockState(run, actor, command.TargetEntityKey))
                return false;

            intents.Docking.Add(new AetheriaRuntimeDaemonDockingIntent
            {
                ActorEntityKey = actor,
                TargetEntityKey = command.TargetEntityKey ?? "",
                Dock = true
            });
            return true;
        }

        private static bool ApplyDockNearestIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actorKey = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actorKey) ||
                !TryFindNearestDockTarget(run, actorKey, command.ScalarValue, out var targetKey))
            {
                return false;
            }

            command.TargetEntityKey = targetKey;
            return ApplyDockIntent(run, command, intents);
        }

        private static bool ApplyUndockIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor))
                return false;

            if (!ApplyUndockState(run, actor))
                return false;

            intents.Docking.Add(new AetheriaRuntimeDaemonDockingIntent
            {
                ActorEntityKey = actor,
                Undock = true
            });
            return true;
        }

        private static bool TryFindNearestDockTarget(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            double maxDistance,
            out string targetEntityKey)
        {
            targetEntityKey = "";
            if (!TryResolveEntity(run, actorEntityKey, out var zoneIndex, out var actorIndex, out var actor))
                return false;

            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var maxDistanceSq = maxDistance > 0.0 ? maxDistance * maxDistance : double.PositiveInfinity;
            var closestDistanceSq = double.PositiveInfinity;
            var closestIndex = -1;
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            for (var index = 0; index < entities.Count; index++)
            {
                var candidate = entities[index];
                if (candidate == null || index == actorIndex)
                    continue;

                var deltaX = candidate.PositionX - actor.PositionX;
                var deltaY = candidate.PositionY - actor.PositionY;
                var distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                if (distanceSq >= maxDistanceSq || distanceSq >= closestDistanceSq)
                    continue;

                closestDistanceSq = distanceSq;
                closestIndex = index;
            }

            if (closestIndex < 0)
                return false;

            targetEntityKey = BuildEntityKey(run.RunId, zoneIndex, closestIndex);
            return true;
        }

        private static bool ApplyDockState(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey,
            string targetEntityKey)
        {
            if (!TryResolveEntity(run, actorEntityKey, out var actorZoneIndex, out var actorIndex, out _) ||
                !TryResolveEntity(run, targetEntityKey, out var targetZoneIndex, out var targetIndex, out var target))
            {
                return false;
            }

            if (actorZoneIndex != targetZoneIndex || actorIndex == targetIndex)
                return false;

            if (IsChildReferencedInZone(run, actorZoneIndex, actorIndex))
                return false;

            RemoveChildReferenceFromZone(run, actorZoneIndex, actorIndex);

            var childIndices = (target.ChildEntityIndices ?? Array.Empty<int>()).ToList();
            if (!childIndices.Contains(actorIndex))
                childIndices.Add(actorIndex);
            target.ChildEntityIndices = childIndices.ToArray();

            var assignments = (target.DockingBayAssignments ?? Array.Empty<int>()).ToList();
            var assigned = false;
            for (var index = 0; index < assignments.Count; index++)
            {
                if (assignments[index] >= 0)
                    continue;

                assignments[index] = actorIndex;
                assigned = true;
                break;
            }

            if (!assigned)
                assignments.Add(actorIndex);
            target.DockingBayAssignments = assignments.ToArray();
            return true;
        }

        private static bool ApplyUndockState(
            AetheriaRuntimeRunCheckpointCommit run,
            string actorEntityKey)
        {
            if (!TryResolveEntity(run, actorEntityKey, out var zoneIndex, out var actorIndex, out _))
                return false;

            return RemoveChildReferenceFromZone(run, zoneIndex, actorIndex);
        }

        private static bool IsChildReferencedInZone(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int childEntityIndex)
        {
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null)
                    continue;

                if ((entity.ChildEntityIndices ?? Array.Empty<int>()).Contains(childEntityIndex))
                    return true;

                if ((entity.DockingBayAssignments ?? Array.Empty<int>()).Contains(childEntityIndex))
                    return true;
            }

            return false;
        }

        private static bool RemoveChildReferenceFromZone(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int childEntityIndex)
        {
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var removed = false;
            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null)
                    continue;

                var childIndices = (entity.ChildEntityIndices ?? Array.Empty<int>()).ToList();
                if (childIndices.RemoveAll(index => index == childEntityIndex) > 0)
                {
                    entity.ChildEntityIndices = childIndices.ToArray();
                    removed = true;
                }

                var assignments = (entity.DockingBayAssignments ?? Array.Empty<int>()).ToArray();
                for (var index = 0; index < assignments.Length; index++)
                {
                    if (assignments[index] != childEntityIndex)
                        continue;

                    assignments[index] = -1;
                    removed = true;
                }

                entity.DockingBayAssignments = assignments;
            }

            return removed;
        }

        private static bool ApplyEnterWormholeIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) || command.TargetZoneIndex < 0)
                return false;

            if (!MoveEntityToZone(run, actor, command.TargetZoneIndex, command.PositionX, command.PositionY, out var movedEntityKey))
                return false;

            intents.Wormholes.Add(new AetheriaRuntimeDaemonWormholeIntent
            {
                ActorEntityKey = movedEntityKey,
                TargetZoneIndex = command.TargetZoneIndex,
                PositionX = command.PositionX,
                PositionY = command.PositionY
            });
            return true;
        }

        private static bool ApplyTowToStationIntent(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeDaemonIntentState intents)
        {
            var actor = ResolveActorEntityKey(run, command);
            if (string.IsNullOrWhiteSpace(actor) ||
                string.IsNullOrWhiteSpace(command.TargetEntityKey) ||
                command.TargetZoneIndex < 0)
            {
                return false;
            }

            if (!MoveEntityToZone(run, actor, command.TargetZoneIndex, command.PositionX, command.PositionY, out var movedEntityKey))
                return false;

            intents.Towing.Add(new AetheriaRuntimeDaemonTowIntent
            {
                ActorEntityKey = movedEntityKey,
                StationEntityKey = command.TargetEntityKey ?? "",
                TargetZoneIndex = command.TargetZoneIndex,
                PositionX = command.PositionX,
                PositionY = command.PositionY
            });
            return true;
        }

        private static bool MoveEntityToZone(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            int targetZoneIndex,
            double positionX,
            double positionY,
            out string movedEntityKey)
        {
            movedEntityKey = "";

            if (!TryParseEntityKey(entityKey, out var sourceZoneIndex, out var sourceEntityIndex))
                return false;

            var zones = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>()).ToList();
            var sourceZone = zones.FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == sourceZoneIndex);
            if (sourceZone == null)
                return false;

            var sourceEntities = (sourceZone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToList();
            if (sourceEntityIndex < 0 || sourceEntityIndex >= sourceEntities.Count)
                return false;

            var movedEntity = sourceEntities[sourceEntityIndex];
            if (movedEntity == null)
                return false;

            if (IsChildReferencedInZone(run, sourceZoneIndex, sourceEntityIndex))
                return false;

            var movedCurrentEntity = TryParseEntityKey(run.CurrentEntityKey, out var currentZoneIndex, out var currentEntityIndex) &&
                currentZoneIndex == sourceZoneIndex &&
                currentEntityIndex == sourceEntityIndex;

            RemoveChildReferenceFromZone(run, sourceZoneIndex, sourceEntityIndex);
            sourceEntities.RemoveAt(sourceEntityIndex);
            sourceZone.Entities = sourceEntities.ToArray();
            ReindexZoneAfterEntityRemoval(sourceZone, sourceEntityIndex);

            var targetZone = zones.FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == targetZoneIndex);
            if (targetZone == null)
            {
                targetZone = new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = targetZoneIndex,
                    Entities = Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()
                };
                zones.Add(targetZone);
                run.Zones = zones.ToArray();
            }

            var targetEntities = (targetZone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToList();
            var targetEntityIndex = targetEntities.Count;
            movedEntity.EntityIndex = targetEntityIndex;
            movedEntity.TargetEntityIndex = -1;
            movedEntity.PositionX = positionX;
            movedEntity.PositionZ = positionY;
            targetEntities.Add(movedEntity);
            targetZone.Entities = targetEntities.ToArray();

            movedEntityKey = BuildEntityKey(run.RunId, targetZoneIndex, targetEntityIndex);
            if (movedCurrentEntity)
            {
                run.CurrentZoneIndex = targetZoneIndex;
                run.CurrentEntityKey = movedEntityKey;
            }
            else
            {
                ReindexCurrentEntityKeyAfterRemoval(run, sourceZoneIndex, sourceEntityIndex);
            }

            var discovered = (run.DiscoveredZoneIndices ?? Array.Empty<int>()).ToList();
            if (!discovered.Contains(targetZoneIndex))
            {
                discovered.Add(targetZoneIndex);
                run.DiscoveredZoneIndices = discovered.ToArray();
            }

            return true;
        }

        private static bool ApplyDestroyEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TryParseEntityKey(command.TargetEntityKey, out var zoneIndex, out var entityIndex))
                return false;

            var zones = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>()).ToList();
            var zone = zones.FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == zoneIndex);
            if (zone == null)
                return false;

            var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()).ToList();
            if (entityIndex < 0 || entityIndex >= entities.Count)
                return false;

            entities.RemoveAt(entityIndex);
            zone.Entities = entities.ToArray();
            ReindexZoneAfterEntityRemoval(zone, entityIndex);
            ReindexCurrentEntityKeyAfterRemoval(run, zoneIndex, entityIndex);

            return true;
        }

        private static void ReindexZoneAfterEntityRemoval(AetheriaRuntimeZoneSnapshotCommit zone, int removedEntityIndex)
        {
            var entities = zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            for (var index = 0; index < entities.Count; index++)
            {
                var entity = entities[index];
                if (entity == null)
                    continue;

                entity.EntityIndex = index;
                entity.TargetEntityIndex = ReindexEntityReference(entity.TargetEntityIndex, removedEntityIndex);
                entity.ChildEntityIndices = (entity.ChildEntityIndices ?? Array.Empty<int>())
                    .Select(childIndex => ReindexEntityReference(childIndex, removedEntityIndex))
                    .Where(childIndex => childIndex >= 0)
                    .ToArray();
                entity.DockingBayAssignments = (entity.DockingBayAssignments ?? Array.Empty<int>())
                    .Select(childIndex => ReindexEntityReference(childIndex, removedEntityIndex))
                    .ToArray();
                entity.Contacts = (entity.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                    .Select(contact =>
                    {
                        if (contact != null)
                            contact.TargetEntityIndex = ReindexEntityReference(contact.TargetEntityIndex, removedEntityIndex);
                        return contact;
                    })
                    .Where(contact => contact != null && contact.TargetEntityIndex >= 0)
                    .ToArray();
            }
        }

        private static void ReindexCurrentEntityKeyAfterRemoval(
            AetheriaRuntimeRunCheckpointCommit run,
            int zoneIndex,
            int removedEntityIndex)
        {
            if (!TryParseEntityKey(run.CurrentEntityKey, out var currentZoneIndex, out var currentEntityIndex) ||
                currentZoneIndex != zoneIndex)
            {
                return;
            }

            var reindexedCurrent = ReindexEntityReference(currentEntityIndex, removedEntityIndex);
            run.CurrentEntityKey = reindexedCurrent < 0
                ? ""
                : BuildEntityKey(run.RunId, zoneIndex, reindexedCurrent);
        }

        private static bool ApplyTargetEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            Action<AetheriaRuntimeEntitySnapshotCommit> mutate)
        {
            if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                return false;

            mutate(entity);
            return true;
        }

        private static bool TryResolveEntity(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            out int zoneIndex,
            out int entityIndex,
            out AetheriaRuntimeEntitySnapshotCommit entity)
        {
            entity = null!;
            zoneIndex = -1;
            entityIndex = -1;

            if (!TryParseEntityKey(entityKey, out zoneIndex, out entityIndex))
                return false;

            var parsedZoneIndex = zoneIndex;
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == parsedZoneIndex);
            var entities = zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>();
            if (entityIndex < 0 || entityIndex >= entities.Count)
                return false;

            entity = entities[entityIndex];
            return entity != null;
        }

        private static int ReindexEntityReference(int referencedEntityIndex, int removedEntityIndex)
        {
            if (referencedEntityIndex < 0)
                return referencedEntityIndex;

            if (referencedEntityIndex == removedEntityIndex)
                return -1;

            return referencedEntityIndex > removedEntityIndex
                ? referencedEntityIndex - 1
                : referencedEntityIndex;
        }

        private static string BuildEntityKey(string runId, int zoneIndex, int entityIndex)
        {
            var normalizedRunId = string.IsNullOrWhiteSpace(runId) ? "local" : runId;
            return $"global:aetheria.run_state.{normalizedRunId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
        }

        private static string ResolveActorEntityKey(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            return string.IsNullOrWhiteSpace(command.ActorEntityKey)
                ? run.CurrentEntityKey ?? ""
                : command.ActorEntityKey ?? "";
        }

        private static bool TryResolveCargoBay(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            int cargoIndex,
            out AetheriaRuntimeEntitySnapshotCommit entity,
            out int resolvedCargoIndex,
            out AetheriaRuntimeCargoBayLoadoutCommit cargo)
        {
            entity = null!;
            cargo = null!;
            resolvedCargoIndex = -1;

            if (!TryResolveEntity(run, entityKey, out _, out _, out entity))
                return false;

            var cargoContents = entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            if (cargoIndex < 0 || cargoIndex >= cargoContents.Count)
                return false;

            cargo = cargoContents[cargoIndex];
            if (cargo == null)
                return false;

            resolvedCargoIndex = cargoIndex;
            return true;
        }

        private static bool TryRemoveCargoItem(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int cargoIndex,
            string itemKey,
            int x,
            int y,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            slot = null!;
            var cargoContents = (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToArray();
            if (cargoIndex < 0 || cargoIndex >= cargoContents.Length || cargoContents[cargoIndex] == null)
                return false;

            var items = (cargoContents[cargoIndex].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            var itemIndex = items.FindIndex(candidate => IsCargoSlotMatch(candidate, itemKey, x, y));
            if (itemIndex < 0)
                return false;

            slot = items[itemIndex];
            items.RemoveAt(itemIndex);
            cargoContents[cargoIndex] = new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = items.ToArray()
            };
            entity.CargoContents = cargoContents;
            return true;
        }

        private static bool TryRemoveCargoItemQuantity(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int cargoIndex,
            string itemKey,
            int x,
            int y,
            int quantity,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            slot = null!;
            var cargoContents = (entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).ToArray();
            if (cargoIndex < 0 || cargoIndex >= cargoContents.Length || cargoContents[cargoIndex] == null)
                return false;

            var items = (cargoContents[cargoIndex].Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            var itemIndex = items.FindIndex(candidate => IsCargoSlotMatch(candidate, itemKey, x, y));
            if (itemIndex < 0)
                return false;

            var source = items[itemIndex];
            var sourceQuantity = Math.Max(1, source.Item?.Quantity ?? 1);
            if (quantity <= 0 || quantity > sourceQuantity)
                return false;

            slot = CloneSlot(source);
            slot.Item.Quantity = quantity;
            if (quantity == sourceQuantity)
            {
                items.RemoveAt(itemIndex);
            }
            else
            {
                source.Item.Quantity = sourceQuantity - quantity;
            }

            cargoContents[cargoIndex] = new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = items.ToArray()
            };
            entity.CargoContents = cargoContents;
            return true;
        }

        private static void AddCargoItem(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int cargoIndex,
            AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            var cargoContents = EnsureCargoContents(entity, cargoIndex + 1);
            var cargo = cargoContents[cargoIndex] ?? new AetheriaRuntimeCargoBayLoadoutCommit();
            cargoContents[cargoIndex] = new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = (cargo.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                    .Concat(new[] { slot })
                    .ToArray()
            };
            entity.CargoContents = cargoContents;
        }

        private static AetheriaRuntimeLoadoutItemSlotCommit CloneSlot(AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            var item = slot?.Item;
            return new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot?.X ?? 0,
                Y = slot?.Y ?? 0,
                Item = new AetheriaRuntimeLoadoutItemCommit
                {
                    ItemKey = item?.ItemKey ?? "",
                    Quality = item?.Quality ?? 1.0,
                    Durability = item?.Durability ?? 1.0,
                    Quantity = item?.Quantity ?? 1,
                    Enabled = item?.Enabled ?? true,
                    OverrideShutdown = item?.OverrideShutdown ?? false
                }
            };
        }

        private static bool TryRemoveEquipmentItem(
            AetheriaRuntimeRunCheckpointCommit run,
            string entityKey,
            int equipmentIndex,
            string itemKey,
            out AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            slot = null!;
            if (!TryResolveEntity(run, entityKey, out _, out _, out var entity))
                return false;

            var equipment = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).ToList();
            if (equipmentIndex < 0 || equipmentIndex >= equipment.Count)
                return false;

            slot = equipment[equipmentIndex];
            if (!IsItemMatch(slot?.Item, itemKey))
                return false;

            equipment.RemoveAt(equipmentIndex);
            entity.Equipment = equipment.ToArray();
            entity.WeaponGroups = (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>())
                    .Where(index => index != equipmentIndex)
                    .Select(index => index > equipmentIndex ? index - 1 : index)
                    .ToArray())
                .ToArray();
            return true;
        }

        private static void AddEquipmentItem(
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeLoadoutItemSlotCommit slot)
        {
            entity.Equipment = (entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                .Concat(new[] { slot })
                .ToArray();
        }

        private static AetheriaRuntimeCargoBayLoadoutCommit[] EnsureCargoContents(
            AetheriaRuntimeEntitySnapshotCommit entity,
            int count)
        {
            var source = entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            var cargoContents = new AetheriaRuntimeCargoBayLoadoutCommit[Math.Max(source.Count, count)];
            for (var i = 0; i < cargoContents.Length; i++)
            {
                cargoContents[i] = i < source.Count
                    ? source[i] ?? new AetheriaRuntimeCargoBayLoadoutCommit()
                    : new AetheriaRuntimeCargoBayLoadoutCommit();
            }

            return cargoContents;
        }

        private static bool IsCargoSlotMatch(
            AetheriaRuntimeLoadoutItemSlotCommit slot,
            string itemKey,
            int x,
            int y)
        {
            if (slot == null || !IsItemMatch(slot.Item, itemKey))
                return false;

            return x == int.MinValue ||
                   y == int.MinValue ||
                   (slot.X == x && slot.Y == y);
        }

        private static bool IsItemMatch(AetheriaRuntimeLoadoutItemCommit item, string itemKey)
        {
            return item != null &&
                   (string.IsNullOrWhiteSpace(itemKey) ||
                    string.Equals(item.ItemKey ?? "", itemKey ?? "", StringComparison.Ordinal));
        }

        private static bool IsPickupMatch(
            AetheriaRuntimeDroppedPickupCommit pickup,
            AetheriaRuntimeDaemonCommandDocument command)
        {
            var pickupCommand = command.LootPickup ?? new AetheriaRuntimeLootPickupCommand();
            if (pickup == null || !IsItemMatch(pickup.Item, pickupCommand.ItemKey))
                return false;

            var expectedX = pickupCommand.PositionX;
            var expectedY = pickupCommand.PositionY;
            var expectedZ = pickupCommand.PositionZ;
            return Math.Abs(pickup.PositionX - expectedX) < 0.001 &&
                   Math.Abs(pickup.PositionY - expectedY) < 0.001 &&
                   Math.Abs(pickup.PositionZ - expectedZ) < 0.001;
        }

        private static bool TryParseEntityKey(
            string entityKey,
            out int zoneIndex,
            out int entityIndex)
        {
            zoneIndex = -1;
            entityIndex = -1;

            if (string.IsNullOrWhiteSpace(entityKey))
                return false;

            var parts = entityKey.Split('.');
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (string.Equals(parts[i], "zone", StringComparison.Ordinal) &&
                    int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out zoneIndex))
                {
                    continue;
                }

                if (string.Equals(parts[i], "entity", StringComparison.Ordinal) &&
                    int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out entityIndex))
                {
                    continue;
                }
            }

            return zoneIndex >= 0 && entityIndex >= 0;
        }

    }
}
