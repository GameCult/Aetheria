using System;
using GameCult.Eve.Surface;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaControl
    {
        private readonly AetheriaRuntimeDaemonOperationsClient _operations;

        internal AetheriaControl(AetheriaRuntimeDaemonOperationsClient operations)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public AetheriaRuntimeDaemonCommandEnvelope SetTarget(string targetEntityKey) =>
            _operations.SetTarget(targetEntityKey);

        public CultMeshOperationReceipt ClearTarget() =>
            _operations.ClearTarget();

        public CultMeshOperationReceipt TargetNearest() =>
            _operations.TargetNearest();

        public CultMeshOperationReceipt TargetNext() =>
            _operations.TargetNext();

        public CultMeshOperationReceipt TargetPrevious() =>
            _operations.TargetPrevious();

        public CultMeshOperationReceipt TargetReticle(double directionX, double directionY, double directionZ) =>
            _operations.TargetReticle(directionX, directionY, directionZ);

        public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
            double directionX,
            double directionY,
            double scalarValue = 1.0) =>
            _operations.SetMoveVector(directionX, directionY, scalarValue);

        public CultMeshOperationReceipt SetLookDirection(double directionX, double directionY, double directionZ) =>
            _operations.SetLookDirection(directionX, directionY, directionZ);

        public CultMeshOperationReceipt SetTractorPower(double power) =>
            _operations.SetTractorPower(power);

        public CultMeshOperationReceipt FireWeaponGroup(int weaponGroup) =>
            _operations.FireWeaponGroup(weaponGroup);

        public CultMeshOperationReceipt SetWeaponGroupActive(int weaponGroup, bool active) =>
            _operations.SetWeaponGroupActive(weaponGroup, active);

        public CultMeshOperationReceipt SetWeaponGroupMembership(
            string targetEntityKey,
            int equipmentIndex,
            int weaponGroup,
            bool assigned) =>
            _operations.SetWeaponGroupMembership(targetEntityKey, equipmentIndex, weaponGroup, assigned);

        public CultMeshOperationReceipt SetBehaviorActive(int equipmentIndex, int behaviorIndex, bool active) =>
            _operations.SetBehaviorActive(equipmentIndex, behaviorIndex, active);

        public CultMeshOperationReceipt ActivateConsumable(string itemKey) =>
            _operations.ActivateConsumable(itemKey);

        public CultMeshOperationReceipt SensorPing() =>
            _operations.SensorPing();

        public CultMeshOperationReceipt SetHeatsinksEnabled(bool enabled) =>
            _operations.SetHeatsinksEnabled(enabled);

        public CultMeshOperationReceipt SetOverrideShutdown(bool enabled) =>
            _operations.SetOverrideShutdown(enabled);

        public CultMeshOperationReceipt SetEntityOverrideShutdown(string targetEntityKey, bool enabled) =>
            _operations.SetEntityOverrideShutdown(targetEntityKey, enabled);

        public CultMeshOperationReceipt SetItemEnabled(int equipmentIndex, bool enabled) =>
            _operations.SetItemEnabled(equipmentIndex, enabled);

        public CultMeshOperationReceipt ToggleShieldEnabled() =>
            _operations.ToggleShieldEnabled();

        public CultMeshOperationReceipt SetItemOverrideShutdown(
            string targetEntityKey,
            int equipmentIndex,
            bool enabled) =>
            _operations.SetItemOverrideShutdown(targetEntityKey, equipmentIndex, enabled);

        public CultMeshOperationReceipt SetThermotoggleTargetTemperature(
            string targetEntityKey,
            int equipmentIndex,
            int behaviorIndex,
            double targetTemperature) =>
            _operations.SetThermotoggleTargetTemperature(
                targetEntityKey,
                equipmentIndex,
                behaviorIndex,
                targetTemperature);

        public CultMeshOperationReceipt SetShutdownPerformance(
            string targetEntityKey,
            double shutdownPerformance) =>
            _operations.SetShutdownPerformance(targetEntityKey, shutdownPerformance);

        public CultMeshOperationReceipt ToggleHullConductivity(
            string targetEntityKey,
            int x,
            int y,
            int axis) =>
            _operations.ToggleHullConductivity(targetEntityKey, x, y, axis);

        public CultMeshOperationReceipt SetEntityName(string targetEntityKey, string name) =>
            _operations.SetEntityName(targetEntityKey, name);

        public CultMeshOperationReceipt Dock(string targetEntityKey) =>
            _operations.Dock(targetEntityKey);

        public CultMeshOperationReceipt DockNearest() =>
            _operations.DockNearest();

        public CultMeshOperationReceipt Undock() =>
            _operations.Undock();

        public CultMeshOperationReceipt Interact() =>
            _operations.Interact();

        public CultMeshOperationReceipt SetDockedCurrentShip(string targetEntityKey) =>
            _operations.SetDockedCurrentShip(targetEntityKey);

        public CultMeshOperationReceipt EnterWormhole(
            int targetZoneIndex,
            double positionX,
            double positionY) =>
            _operations.EnterWormhole(targetZoneIndex, positionX, positionY);

        public CultMeshOperationReceipt TowToStation(string stationEntityKey) =>
            _operations.TowToStation(stationEntityKey);

        public CultMeshOperationReceipt TransferCargoItem(
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
            bool hasDestinationPosition) =>
            _operations.TransferCargoItem(
                originEntityKey,
                originCargoIndex,
                destinationEntityKey,
                destinationCargoIndex,
                itemKey,
                quantity,
                sourceX,
                sourceY,
                destinationX,
                destinationY,
                hasDestinationPosition);

        public CultMeshOperationReceipt TradePurchase(
            string itemKey,
            int quantity,
            int stationCargoIndex,
            int targetCargoIndex,
            int sourceX,
            int sourceY) =>
            _operations.TradePurchase(
                itemKey,
                quantity,
                stationCargoIndex,
                targetCargoIndex,
                sourceX,
                sourceY);

        public CultMeshOperationReceipt TradeSale(
            string itemKey,
            int quantity,
            int sourceCargoIndex,
            int sourceX,
            int sourceY) =>
            _operations.TradeSale(itemKey, quantity, sourceCargoIndex, sourceX, sourceY);

        public CultMeshOperationReceipt RestoreLoadout(
            string dockedEntityKey,
            string templateName,
            int price) =>
            _operations.RestoreLoadout(dockedEntityKey, templateName, price);

        public CultMeshOperationReceipt EquipItem(
            string sourceKind,
            string originEntityKey,
            int originIndex,
            string destinationEntityKey,
            string itemKey,
            int sourceX,
            int sourceY,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition) =>
            _operations.EquipItem(
                sourceKind,
                originEntityKey,
                originIndex,
                destinationEntityKey,
                itemKey,
                sourceX,
                sourceY,
                destinationX,
                destinationY,
                hasDestinationPosition);

        public CultMeshOperationReceipt StoreItem(
            string originEntityKey,
            int sourceEquipmentIndex,
            string destinationEntityKey,
            int destinationCargoIndex,
            string itemKey,
            int destinationX,
            int destinationY,
            bool hasDestinationPosition,
            string sourceKind = AetheriaRuntimeRefitSourceKinds.Equipment) =>
            _operations.StoreItem(
                originEntityKey,
                sourceEquipmentIndex,
                destinationEntityKey,
                destinationCargoIndex,
                itemKey,
                destinationX,
                destinationY,
                hasDestinationPosition,
                sourceKind);

        public bool TrySubmitSurfaceCommand(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeDaemonCommandEnvelope? envelope) =>
            _operations.TrySubmitSurfaceCommand(request, out envelope);
    }
}
