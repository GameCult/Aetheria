using System;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    internal sealed class AetheriaRuntimeDaemonOperationClient
    {
        public const string DefaultClientId = "aetheria-daemon-client";
        private readonly Func<AetheriaRuntimeDaemonCommandDocument, AetheriaRuntimeDaemonCommandEnvelope>? _submit;

        internal AetheriaRuntimeDaemonOperationClient(
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

        internal AetheriaRuntimeDaemonOperationClient(
            string stateFilePath,
            string clientId,
            string sessionId,
            Func<AetheriaRuntimeDaemonCommandDocument, AetheriaRuntimeDaemonCommandEnvelope> submit)
            : this(stateFilePath, clientId, sessionId)
        {
            _submit = submit ?? throw new ArgumentNullException(nameof(submit));
        }

        public string StateFilePath { get; }
        public string ClientId { get; }
        public string SessionId { get; }

        private AetheriaRuntimeDaemonCommandEnvelope Send(AetheriaRuntimeDaemonCommandDocument command)
        {
            if (_submit != null)
                return _submit(command);

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
            envelope = null;
            error = "";

            try
            {
                using var client = AetheriaClient
                    .OpenAsync(StateFilePath, ClientId, startServer: false, pullOnOpen: true)
                    .GetAwaiter()
                    .GetResult();
                envelope = client
                    .SubmitDaemonCommandDocument(command);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private AetheriaRuntimeDaemonCommandDocument Create(
            AetheriaRuntimeDaemonCommandKinds kind,
            AetheriaRuntimeDaemonFrameDocument? frame)
        {
            return AetheriaRuntimeDaemonCommandDocument.Create(
                kind,
                ClientId,
                frame?.SessionId ?? SessionId,
                frame?.FrameId ?? -1,
                frame?.Run.CurrentEntityKey ?? "");
        }

        internal static AetheriaRuntimeDaemonCommandEnvelope ToEnvelope(AetheriaRuntimeDaemonCommandDocument command)
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

        internal AetheriaRuntimeDaemonCommandEnvelope SetTarget(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetTarget, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope ClearTarget(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.ClearTarget, frame));
        }

        internal AetheriaRuntimeDaemonCommandEnvelope TargetNearest(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.TargetNearest, frame));
        }

        internal AetheriaRuntimeDaemonCommandEnvelope TargetNext(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.TargetNext, frame));
        }

        internal AetheriaRuntimeDaemonCommandEnvelope TargetPrevious(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.TargetPrevious, frame));
        }

        internal AetheriaRuntimeDaemonCommandEnvelope TargetReticle(
            AetheriaRuntimeDaemonFrameDocument? frame,
            double directionX,
            double directionY,
            double directionZ)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.TargetReticle, frame);
            command.DirectionX = directionX;
            command.DirectionY = directionY;
            command.PositionZ = directionZ;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
            AetheriaRuntimeDaemonFrameDocument? frame,
            double directionX,
            double directionY,
            double scalarValue = 1.0)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetMoveVector, frame);
            command.DirectionX = directionX;
            command.DirectionY = directionY;
            command.ScalarValue = scalarValue;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetLookDirection(
            AetheriaRuntimeDaemonFrameDocument? frame,
            double directionX,
            double directionY,
            double directionZ)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetLookDirection, frame);
            command.DirectionX = directionX;
            command.DirectionY = directionY;
            command.PositionZ = directionZ;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetTractorPower(
            AetheriaRuntimeDaemonFrameDocument? frame,
            double power)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetTractorPower, frame);
            command.ScalarValue = power;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope FireWeaponGroup(
            AetheriaRuntimeDaemonFrameDocument? frame,
            int weaponGroup)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, frame);
            command.WeaponGroup = weaponGroup;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupActive(
            AetheriaRuntimeDaemonFrameDocument? frame,
            int weaponGroup,
            bool active)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive, frame);
            command.WeaponGroup = weaponGroup;
            command.ScalarValue = active ? 1.0 : 0.0;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupMembership(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey,
            int equipmentIndex,
            int weaponGroup,
            bool assigned)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.WeaponGroup = weaponGroup;
            command.ScalarValue = assigned ? 1.0 : 0.0;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetBehaviorActive(
            AetheriaRuntimeDaemonFrameDocument? frame,
            int equipmentIndex,
            int behaviorIndex,
            bool active)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive, frame);
            command.EquipmentIndex = equipmentIndex;
            command.BehaviorIndex = behaviorIndex;
            command.ScalarValue = active ? 1.0 : 0.0;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope ActivateConsumable(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string itemKey)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.ActivateConsumable, frame);
            command.TextValue = itemKey ?? "";
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SensorPing(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.SensorPing, frame));
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetHeatsinksEnabled(
            AetheriaRuntimeDaemonFrameDocument? frame,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled, frame);
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetOverrideShutdown(
            AetheriaRuntimeDaemonFrameDocument? frame,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown, frame);
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetOverrideShutdown(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetItemEnabled(
            AetheriaRuntimeDaemonFrameDocument? frame,
            int equipmentIndex,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetItemEnabled, frame);
            command.EquipmentIndex = equipmentIndex;
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope ToggleShieldEnabled(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled, frame));
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetItemOverrideShutdown(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey,
            int equipmentIndex,
            bool enabled)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetItemOverrideShutdown, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.ScalarValue = enabled ? 1.0 : 0.0;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetThermotoggleTargetTemperature(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey,
            int equipmentIndex,
            int behaviorIndex,
            double targetTemperature)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetThermotoggleTargetTemperature, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.BehaviorIndex = behaviorIndex;
            command.ScalarValue = targetTemperature;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetShutdownPerformance(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey,
            double shutdownPerformance)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.ScalarValue = shutdownPerformance;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope ToggleHullConductivity(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey,
            int x,
            int y,
            int axis)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.PositionX = x;
            command.PositionY = y;
            command.ScalarValue = axis;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetEntityName(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey,
            string name)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetEntityName, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            command.TextValue = name ?? "";
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope Dock(AetheriaRuntimeDaemonFrameDocument? frame, string targetEntityKey)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.Dock, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope DockNearest(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.DockNearest, frame);
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope Undock(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            return Send(Create(AetheriaRuntimeDaemonCommandKinds.Undock, frame));
        }

        internal AetheriaRuntimeDaemonCommandEnvelope Interact(AetheriaRuntimeDaemonFrameDocument? frame)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.Interact, frame);
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope SetDockedCurrentShip(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip, frame);
            command.TargetEntityKey = targetEntityKey ?? "";
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope EnterWormhole(
            AetheriaRuntimeDaemonFrameDocument? frame,
            int targetZoneIndex,
            double positionX,
            double positionY)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.EnterWormhole, frame);
            command.TargetZoneIndex = targetZoneIndex;
            command.PositionX = positionX;
            command.PositionY = positionY;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope TowToStation(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string stationEntityKey)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.TowToStation, frame);
            command.TargetEntityKey = stationEntityKey ?? "";
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope TransferCargoItem(
            AetheriaRuntimeDaemonFrameDocument? frame,
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.TransferCargoItem, frame);
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
            command.CargoTransfer.Quantity = quantity;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope TradePurchase(
            AetheriaRuntimeDaemonFrameDocument? frame,
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.TradePurchase, frame);
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

        internal AetheriaRuntimeDaemonCommandEnvelope PickUpLoot(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string targetEntityKey,
            string itemKey,
            int quantity,
            double positionX,
            double positionY,
            double positionZ,
            int pickupIndex = -1)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.PickUpLoot, frame);
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
            command.LootPickup.PickupIndex = pickupIndex;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope RestoreLoadout(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string dockedEntityKey,
            string templateName,
            int price)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.RestoreLoadout, frame);
            command.TargetEntityKey = dockedEntityKey ?? "";
            command.TextValue = templateName ?? "";
            command.ScalarValue = price;
            command.LoadoutRestore.DockedEntityKey = dockedEntityKey ?? "";
            command.LoadoutRestore.TemplateName = templateName ?? "";
            command.LoadoutRestore.Price = price;
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope EquipItem(
            AetheriaRuntimeDaemonFrameDocument? frame,
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
            var command = Create(AetheriaRuntimeDaemonCommandKinds.EquipItem, frame);
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

        internal AetheriaRuntimeDaemonCommandEnvelope StoreItem(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string originEntityKey,
            int sourceEquipmentIndex,
            string destinationEntityKey,
            int destinationCargoIndex,
            string itemKey,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.StoreItem, frame);
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

        internal AetheriaRuntimeDaemonCommandEnvelope IssueAgentTask(
            AetheriaRuntimeDaemonFrameDocument? frame,
            AetheriaRuntimeAgentTaskCommand task)
        {
            task ??= new AetheriaRuntimeAgentTaskCommand();
            var command = Create(AetheriaRuntimeDaemonCommandKinds.IssueAgentTask, frame);
            command.AgentTask = task;
            command.SubjectKey = string.IsNullOrWhiteSpace(task.CorporationKey)
                ? "aetheria.tasks"
                : $"aetheria.tasks.{task.CorporationKey}";
            return Send(command);
        }

        internal AetheriaRuntimeDaemonCommandEnvelope CancelAgentTask(
            AetheriaRuntimeDaemonFrameDocument? frame,
            string taskId)
        {
            var command = Create(AetheriaRuntimeDaemonCommandKinds.CancelAgentTask, frame);
            command.AgentTask.TaskId = taskId ?? "";
            command.SubjectKey = "aetheria.tasks";
            return Send(command);
        }
    }
}
