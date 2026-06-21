using System;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonOperationClient
    {
        public const string DefaultClientId = "aetheria-daemon-client";

        public AetheriaRuntimeDaemonOperationClient(
            string stateFilePath,
            string clientId = DefaultClientId,
            string sessionId = "local")
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            StateFilePath = stateFilePath;
            ClientId = string.IsNullOrWhiteSpace(clientId) ? DefaultClientId : clientId;
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? "local" : sessionId;
        }

        public string StateFilePath { get; }
        public string ClientId { get; }
        public string SessionId { get; }

        private AetheriaRuntimeDaemonCommandEnvelope Send(AetheriaRuntimeDaemonCommandDocument command)
        {
            if (!TrySend(command, out var envelope, out var error))
            {
                throw new InvalidOperationException(
                    $"Failed to submit Aetheria daemon operation {command.Kind}: {error}");
            }

            return envelope!;
        }

        private bool TrySend(
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

        private AetheriaRuntimeDaemonCommandDocument Create(
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetTarget, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope ClearTarget(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.ClearTarget, observed));
        }

        public AetheriaRuntimeDaemonCommandEnvelope TargetNearest(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.TargetNearest, observed));
        }

        public AetheriaRuntimeDaemonCommandEnvelope TargetNext(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.TargetNext, observed));
        }

        public AetheriaRuntimeDaemonCommandEnvelope TargetPrevious(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.TargetPrevious, observed));
        }

        public AetheriaRuntimeDaemonCommandEnvelope TargetReticle(
            AetheriaRuntimeObservedDaemonState? observed,
            double directionX,
            double directionY,
            double directionZ)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.TargetReticle, observed);
            command.DirectionX = directionX;
            command.DirectionY = directionY;
            command.PositionZ = directionZ;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
            AetheriaRuntimeObservedDaemonState? observed,
            double directionX,
            double directionY,
            double scalarValue = 1.0)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetMoveVector, observed);
            command.DirectionX = directionX;
            command.DirectionY = directionY;
            command.ScalarValue = scalarValue;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetLookDirection(
            AetheriaRuntimeObservedDaemonState? observed,
            double directionX,
            double directionY,
            double directionZ)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetLookDirection, observed);
            command.DirectionX = directionX;
            command.DirectionY = directionY;
            command.PositionZ = directionZ;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetTractorPower(
            AetheriaRuntimeObservedDaemonState? observed,
            double power)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetTractorPower, observed);
            command.ScalarValue = power;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope FireWeaponGroup(
            AetheriaRuntimeObservedDaemonState? observed,
            int weaponGroup)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, observed);
            command.WeaponGroup = weaponGroup;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupActive(
            AetheriaRuntimeObservedDaemonState? observed,
            int weaponGroup,
            bool active)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive, observed);
            command.WeaponGroup = weaponGroup;
            command.ScalarValue = active ? 1.0 : 0.0;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupMembership(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            int equipmentIndex,
            int weaponGroup,
            bool assigned)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.WeaponGroup = weaponGroup;
            command.ScalarValue = assigned ? 1.0 : 0.0;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetBehaviorActive(
            AetheriaRuntimeObservedDaemonState? observed,
            int equipmentIndex,
            int behaviorIndex,
            bool active)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive, observed);
            command.EquipmentIndex = equipmentIndex;
            command.BehaviorIndex = behaviorIndex;
            command.ScalarValue = active ? 1.0 : 0.0;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope ActivateConsumable(
            AetheriaRuntimeObservedDaemonState? observed,
            string itemKey)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.ActivateConsumable, observed);
            command.TextValue = itemKey ?? "";
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SensorPing(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.SensorPing, observed));
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetHeatsinksEnabled(
            AetheriaRuntimeObservedDaemonState? observed,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled, observed);
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetOverrideShutdown(
            AetheriaRuntimeObservedDaemonState? observed,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown, observed);
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetOverrideShutdown(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetItemEnabled(
            AetheriaRuntimeObservedDaemonState? observed,
            int equipmentIndex,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetItemEnabled, observed);
            command.EquipmentIndex = equipmentIndex;
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope ToggleShieldEnabled(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled, observed));
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetItemOverrideShutdown(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            int equipmentIndex,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetItemOverrideShutdown, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetThermotoggleTargetTemperature(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            int equipmentIndex,
            int behaviorIndex,
            double targetTemperature)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetThermotoggleTargetTemperature, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.BehaviorIndex = behaviorIndex;
            command.ScalarValue = targetTemperature;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetShutdownPerformance(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            double shutdownPerformance)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.ScalarValue = shutdownPerformance;
            return Send(command);
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetActionBarBinding, observed);
            command.TextValue = controlPath ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.BehaviorIndex = behaviorIndex;
            command.WeaponGroup = weaponGroup;
            command.ActionBarBinding.Kind = kind ?? "";
            command.ActionBarBinding.ItemKey = itemKey ?? "";
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope ClearActionBarBinding(
            AetheriaRuntimeObservedDaemonState? observed,
            string controlPath)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.ClearActionBarBinding, observed);
            command.TextValue = controlPath ?? "";
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope ToggleHullConductivity(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            int x,
            int y,
            int axis)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.PositionX = x;
            command.PositionY = y;
            command.ScalarValue = axis;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetEntityName(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey,
            string name)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetEntityName, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.TextValue = name ?? "";
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope Dock(AetheriaRuntimeObservedDaemonState? observed, string targetEntityKey)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.Dock, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope DockNearest(AetheriaRuntimeObservedDaemonState? observed)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.DockNearest, observed);
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope Undock(AetheriaRuntimeObservedDaemonState? observed)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.Undock, observed));
        }

        public AetheriaRuntimeDaemonCommandEnvelope Interact(AetheriaRuntimeObservedDaemonState? observed)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.Interact, observed);
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetDockedCurrentShip(
            AetheriaRuntimeObservedDaemonState? observed,
            string targetEntityKey)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip, observed);
            command.TargetEntityKey = targetEntityKey ?? "";
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope EnterWormhole(
            AetheriaRuntimeObservedDaemonState? observed,
            int targetZoneIndex,
            double positionX,
            double positionY)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.EnterWormhole, observed);
            command.TargetZoneIndex = targetZoneIndex;
            command.PositionX = positionX;
            command.PositionY = positionY;
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope TowToStation(
            AetheriaRuntimeObservedDaemonState? observed,
            string stationEntityKey,
            int targetZoneIndex,
            double positionX,
            double positionY)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.TowToStation, observed);
            command.TargetEntityKey = stationEntityKey ?? "";
            command.TargetZoneIndex = targetZoneIndex;
            command.PositionX = positionX;
            command.PositionY = positionY;
            return Send(command);
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.TransferCargoItem, observed);
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
            return Send(command);
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.TradePurchase, observed);
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
            return Send(command);
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.PickUpLoot, observed);
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
            return Send(command);
        }

        public AetheriaRuntimeDaemonCommandEnvelope RestoreLoadout(
            AetheriaRuntimeObservedDaemonState? observed,
            string dockedEntityKey,
            string templateName,
            int price)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.RestoreLoadout, observed);
            command.TargetEntityKey = dockedEntityKey ?? "";
            command.TextValue = templateName ?? "";
            command.ScalarValue = price;
            command.LoadoutRestore.DockedEntityKey = dockedEntityKey ?? "";
            command.LoadoutRestore.TemplateName = templateName ?? "";
            command.LoadoutRestore.Price = price;
            return Send(command);
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.EquipItem, observed);
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
            return Send(command);
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.StoreItem, observed);
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
            return Send(command);
        }
    }
}
