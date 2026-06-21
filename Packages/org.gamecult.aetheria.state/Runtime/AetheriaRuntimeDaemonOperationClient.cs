using System;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonOperationClient
    {
        public AetheriaRuntimeDaemonOperationClient(
            string stateFilePath,
            string clientId = "unity-observer",
            string sessionId = "local")
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            StateFilePath = stateFilePath;
            ClientId = string.IsNullOrWhiteSpace(clientId) ? "unity-observer" : clientId;
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? "local" : sessionId;
        }

        public string StateFilePath { get; }
        public string ClientId { get; }
        public string SessionId { get; }

        public AetheriaRuntimeDaemonCommandEnvelope Send(
            AetheriaRuntimeDaemonCommandKinds kind,
            AetheriaRuntimeObservedDaemonState? observed,
            Action<AetheriaRuntimeDaemonCommandDocument>? configure = null)
        {
            var command = Create(kind, observed);
            configure?.Invoke(command);
            if (!TrySend(command, out var envelope, out var error))
            {
                throw new InvalidOperationException(
                    $"Failed to submit Aetheria daemon operation {kind}: {error}");
            }

            return envelope!;
        }

        public bool TrySend(
            AetheriaRuntimeDaemonCommandKinds kind,
            AetheriaRuntimeObservedDaemonState? observed,
            Action<AetheriaRuntimeDaemonCommandDocument>? configure,
            out AetheriaRuntimeDaemonCommandEnvelope? envelope,
            out string error)
        {
            var command = Create(kind, observed);
            configure?.Invoke(command);
            return TrySend(command, out envelope, out error);
        }

        public bool TrySend(
            AetheriaRuntimeDaemonCommandDocument command,
            out AetheriaRuntimeDaemonCommandEnvelope? envelope,
            out string error)
        {
            return AetheriaRuntimeCommandSubmitter.TrySubmitDaemonCommand(
                StateFilePath,
                command,
                ClientId,
                out envelope,
                out error);
        }

        public AetheriaRuntimeDaemonCommandDocument Create(
            AetheriaRuntimeDaemonCommandKinds kind,
            AetheriaRuntimeObservedDaemonState? observed)
        {
            return AetheriaRuntimeDaemonCommandDocument.Create(
                kind,
                ClientId,
                observed?.Frame.SessionId ?? SessionId,
                observed?.Frame.FrameId ?? -1,
                observed?.Run.CurrentEntityKey ?? "");
        }

        public static AetheriaRuntimeDaemonCommandEnvelope ToEnvelope(AetheriaRuntimeDaemonCommandDocument command)
        {
            return new AetheriaRuntimeDaemonCommandEnvelope(
                command.Schema ?? "",
                command.CommandId ?? "",
                command.ClientId ?? "",
                command.IssuedAtUtc ?? "",
                command.SessionId ?? "",
                command.ObservedFrameId,
                command.Kind,
                command.ActorEntityKey ?? "",
                "");
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetTarget(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetTarget, observed, command =>
                command.TargetEntityKey = targetEntityKey ?? "");
        }

        public AetheriaRuntimeDaemonCommandEnvelope ClearTarget(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.ClearTarget, observed);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
            AetheriaRuntimeObservedDaemonState? observed,
            double directionX,
            double directionY,
            double scalarValue = 1.0)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetMoveVector, observed, command =>
            {
                command.DirectionX = directionX;
                command.DirectionY = directionY;
                command.ScalarValue = scalarValue;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetLookDirection(
            AetheriaRuntimeObservedDaemonState? observed,
            double directionX,
            double directionY,
            double directionZ)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetLookDirection, observed, command =>
            {
                command.DirectionX = directionX;
                command.DirectionY = directionY;
                command.PositionZ = directionZ;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetTractorPower(
            AetheriaRuntimeObservedDaemonState? observed,
            double power)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetTractorPower, observed, command =>
                command.ScalarValue = power);
        }

        public AetheriaRuntimeDaemonCommandEnvelope FireWeaponGroup(
            AetheriaRuntimeObservedDaemonState? observed,
            int weaponGroup)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, observed, command =>
                command.WeaponGroup = weaponGroup);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupActive(
            AetheriaRuntimeObservedDaemonState? observed,
            int weaponGroup,
            bool active)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive, observed, command =>
            {
                command.WeaponGroup = weaponGroup;
                command.ScalarValue = active ? 1.0 : 0.0;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupMembership(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            int equipmentIndex,
            int weaponGroup,
            bool assigned)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.EquipmentIndex = equipmentIndex;
                command.WeaponGroup = weaponGroup;
                command.ScalarValue = assigned ? 1.0 : 0.0;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetBehaviorActive(
            AetheriaRuntimeObservedDaemonState? observed,
            int equipmentIndex,
            int behaviorIndex,
            bool active)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive, observed, command =>
            {
                command.EquipmentIndex = equipmentIndex;
                command.BehaviorIndex = behaviorIndex;
                command.ScalarValue = active ? 1.0 : 0.0;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope ActivateConsumable(
            AetheriaRuntimeObservedDaemonState? observed,
            string itemKey)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.ActivateConsumable, observed, command =>
                command.TextValue = itemKey ?? "");
        }

        public AetheriaRuntimeDaemonCommandEnvelope SensorPing(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SensorPing, observed);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetHeatsinksEnabled(
            AetheriaRuntimeObservedDaemonState? observed,
            bool enabled)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled, observed, command =>
                command.ScalarValue = enabled ? 1.0 : 0.0);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetOverrideShutdown(
            AetheriaRuntimeObservedDaemonState? observed,
            bool enabled)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown, observed, command =>
                command.ScalarValue = enabled ? 1.0 : 0.0);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetOverrideShutdown(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            bool enabled)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.ScalarValue = enabled ? 1.0 : 0.0;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetItemEnabled(
            AetheriaRuntimeObservedDaemonState? observed,
            int equipmentIndex,
            bool enabled)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetItemEnabled, observed, command =>
            {
                command.EquipmentIndex = equipmentIndex;
                command.ScalarValue = enabled ? 1.0 : 0.0;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope ToggleShieldEnabled(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled, observed);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetItemOverrideShutdown(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            int equipmentIndex,
            bool enabled)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetItemOverrideShutdown, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.EquipmentIndex = equipmentIndex;
                command.ScalarValue = enabled ? 1.0 : 0.0;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetThermotoggleTargetTemperature(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            int equipmentIndex,
            int behaviorIndex,
            double targetTemperature)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetThermotoggleTargetTemperature, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.EquipmentIndex = equipmentIndex;
                command.BehaviorIndex = behaviorIndex;
                command.ScalarValue = targetTemperature;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetShutdownPerformance(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            double shutdownPerformance)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.ScalarValue = shutdownPerformance;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetActionBarBinding(
            AetheriaRuntimeObservedDaemonState? observed,
            string controlPath,
            string kind,
            string itemKey,
            int equipmentIndex,
            int behaviorIndex,
            int weaponGroup)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetActionBarBinding, observed, command =>
            {
                command.TextValue = controlPath ?? "";
                command.EquipmentIndex = equipmentIndex;
                command.BehaviorIndex = behaviorIndex;
                command.WeaponGroup = weaponGroup;
                command.ActionBarBinding.Kind = kind ?? "";
                command.ActionBarBinding.ItemKey = itemKey ?? "";
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope ClearActionBarBinding(
            AetheriaRuntimeObservedDaemonState? observed,
            string controlPath)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.ClearActionBarBinding, observed, command =>
                command.TextValue = controlPath ?? "");
        }

        public AetheriaRuntimeDaemonCommandEnvelope ToggleHullConductivity(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            int x,
            int y,
            int axis)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.PositionX = x;
                command.PositionY = y;
                command.ScalarValue = axis;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetEntityName(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            string name)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetEntityName, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.TextValue = name ?? "";
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope Dock(AetheriaRuntimeObservedDaemonState? observed, string targetEntityKey)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.Dock, observed, command =>
                command.TargetEntityKey = targetEntityKey ?? "");
        }

        public AetheriaRuntimeDaemonCommandEnvelope Undock(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.Undock, observed);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetDockedCurrentShip(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip, observed, command =>
                command.TargetEntityKey = targetEntityKey ?? "");
        }

        public AetheriaRuntimeDaemonCommandEnvelope EnterWormhole(
            AetheriaRuntimeObservedDaemonState? observed,
            int targetZoneIndex,
            double positionX,
            double positionY)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.EnterWormhole, observed, command =>
            {
                command.TargetZoneIndex = targetZoneIndex;
                command.PositionX = positionX;
                command.PositionY = positionY;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope TowToStation(
            AetheriaRuntimeObservedDaemonState? observed,
            string stationEntityKey,
            int targetZoneIndex,
            double positionX,
            double positionY)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.TowToStation, observed, command =>
            {
                command.TargetEntityKey = stationEntityKey ?? "";
                command.TargetZoneIndex = targetZoneIndex;
                command.PositionX = positionX;
                command.PositionY = positionY;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope TransferCargoItem(
            AetheriaRuntimeObservedDaemonState? observed,
            string originEntityKey,
            int originCargoIndex,
            string destinationEntityKey,
            int destinationCargoIndex,
            string itemKey,
            int quantity,
            int sourceX,
            int sourceY,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.TransferCargoItem, observed, command =>
            {
                command.TargetEntityKey = destinationEntityKey ?? "";
                command.EquipmentIndex = destinationCargoIndex;
                command.PositionX = destinationX;
                command.PositionY = destinationY;
                command.ScalarValue = quantity;
                command.TextValue = itemKey ?? "";
                command.CargoTransfer.OriginEntityKey = originEntityKey ?? "";
                command.CargoTransfer.OriginCargoIndex = originCargoIndex;
                command.CargoTransfer.DestinationEntityKey = destinationEntityKey ?? "";
                command.CargoTransfer.DestinationCargoIndex = destinationCargoIndex;
                command.CargoTransfer.SourceX = sourceX;
                command.CargoTransfer.SourceY = sourceY;
                command.CargoTransfer.DestinationX = destinationX;
                command.CargoTransfer.DestinationY = destinationY;
                command.CargoTransfer.HasDestinationPosition = hasDestinationPosition;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope TradePurchase(
            AetheriaRuntimeObservedDaemonState? observed,
            string purchaseKind,
            string itemKey,
            int quantity,
            int unitPrice,
            int totalPrice,
            string stationEntityKey,
            int stationCargoIndex,
            string targetEntityKey,
            int targetCargoIndex,
            int sourceX,
            int sourceY,
            bool createsDockedShip)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.TradePurchase, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.EquipmentIndex = targetCargoIndex;
                command.PositionX = sourceX;
                command.PositionY = sourceY;
                command.ScalarValue = totalPrice;
                command.TextValue = itemKey ?? "";
                command.TradePurchase.PurchaseKind = purchaseKind ?? "";
                command.TradePurchase.ItemKey = itemKey ?? "";
                command.TradePurchase.Quantity = quantity;
                command.TradePurchase.UnitPrice = unitPrice;
                command.TradePurchase.TotalPrice = totalPrice;
                command.TradePurchase.StationEntityKey = stationEntityKey ?? "";
                command.TradePurchase.StationCargoIndex = stationCargoIndex;
                command.TradePurchase.TargetEntityKey = targetEntityKey ?? "";
                command.TradePurchase.TargetCargoIndex = targetCargoIndex;
                command.TradePurchase.SourceX = sourceX;
                command.TradePurchase.SourceY = sourceY;
                command.TradePurchase.CreatesDockedShip = createsDockedShip;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope PickUpLoot(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            string itemKey,
            int quantity,
            double positionX,
            double positionY,
            double positionZ)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.PickUpLoot, observed, command =>
            {
                command.TargetEntityKey = targetEntityKey ?? "";
                command.TextValue = itemKey ?? "";
                command.ScalarValue = quantity;
                command.PositionX = positionX;
                command.PositionY = positionY;
                command.PositionZ = positionZ;
                command.LootPickup.ItemKey = itemKey ?? "";
                command.LootPickup.Quantity = quantity;
                command.LootPickup.PositionX = positionX;
                command.LootPickup.PositionY = positionY;
                command.LootPickup.PositionZ = positionZ;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope RestoreLoadout(
            AetheriaRuntimeObservedDaemonState? observed,
            string dockedEntityKey,
            string templateName,
            int price)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.RestoreLoadout, observed, command =>
            {
                command.TargetEntityKey = dockedEntityKey ?? "";
                command.TextValue = templateName ?? "";
                command.ScalarValue = price;
                command.LoadoutRestore.DockedEntityKey = dockedEntityKey ?? "";
                command.LoadoutRestore.TemplateName = templateName ?? "";
                command.LoadoutRestore.Price = price;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope EquipItem(
            AetheriaRuntimeObservedDaemonState? observed,
            string sourceKind,
            string originEntityKey,
            int originIndex,
            string destinationEntityKey,
            string itemKey,
            int sourceX,
            int sourceY,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.EquipItem, observed, command =>
            {
                command.TargetEntityKey = destinationEntityKey ?? "";
                command.PositionX = destinationX;
                command.PositionY = destinationY;
                command.TextValue = itemKey ?? "";
                command.EquipmentTransfer.SourceKind = sourceKind ?? "";
                command.EquipmentTransfer.OriginEntityKey = originEntityKey ?? "";
                command.EquipmentTransfer.OriginIndex = originIndex;
                command.EquipmentTransfer.DestinationEntityKey = destinationEntityKey ?? "";
                command.EquipmentTransfer.SourceX = sourceX;
                command.EquipmentTransfer.SourceY = sourceY;
                command.EquipmentTransfer.DestinationX = destinationX;
                command.EquipmentTransfer.DestinationY = destinationY;
                command.EquipmentTransfer.HasDestinationPosition = hasDestinationPosition;
            });
        }

        public AetheriaRuntimeDaemonCommandEnvelope StoreItem(
            AetheriaRuntimeObservedDaemonState? observed,
            string originEntityKey,
            int sourceEquipmentIndex,
            string destinationEntityKey,
            int destinationCargoIndex,
            string itemKey,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition)
        {
            return Send(AetheriaRuntimeDaemonCommandKinds.StoreItem, observed, command =>
            {
                command.TargetEntityKey = destinationEntityKey ?? "";
                command.EquipmentIndex = sourceEquipmentIndex;
                command.PositionX = destinationX;
                command.PositionY = destinationY;
                command.TextValue = itemKey ?? "";
                command.StoreItem.OriginEntityKey = originEntityKey ?? "";
                command.StoreItem.SourceEquipmentIndex = sourceEquipmentIndex;
                command.StoreItem.DestinationEntityKey = destinationEntityKey ?? "";
                command.StoreItem.DestinationCargoIndex = destinationCargoIndex;
                command.StoreItem.DestinationX = destinationX;
                command.StoreItem.DestinationY = destinationY;
                command.StoreItem.HasDestinationPosition = hasDestinationPosition;
            });
        }
    }
}
