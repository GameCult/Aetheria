using System;
using GameCult.Aetheria.State.Verse;

public sealed class AetheriaDaemonOperations
{
    private readonly AetheriaDaemonObserver _observer;

    internal AetheriaDaemonOperations(AetheriaDaemonObserver observer)
    {
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetTarget(string targetEntityKey)
    {
        return Send((client, observed) => client.SetTarget(observed, targetEntityKey));
    }

    public AetheriaRuntimeDaemonCommandEnvelope ClearTarget()
    {
        return Send((client, observed) => client.ClearTarget(observed));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
        double directionX,
        double directionY,
        double scalarValue = 1.0)
    {
        return Send((client, observed) => client.SetMoveVector(observed, directionX, directionY, scalarValue));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetLookDirection(double directionX, double directionY, double directionZ)
    {
        return Send((client, observed) => client.SetLookDirection(observed, directionX, directionY, directionZ));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetTractorPower(double power)
    {
        return Send((client, observed) => client.SetTractorPower(observed, power));
    }

    public AetheriaRuntimeDaemonCommandEnvelope FireWeaponGroup(int weaponGroup)
    {
        return Send((client, observed) => client.FireWeaponGroup(observed, weaponGroup));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupActive(int weaponGroup, bool active)
    {
        return Send((client, observed) => client.SetWeaponGroupActive(observed, weaponGroup, active));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupMembership(
        string targetEntityKey,
        int equipmentIndex,
        int weaponGroup,
        bool assigned)
    {
        return Send((client, observed) => client.SetWeaponGroupMembership(
            observed,
            targetEntityKey,
            equipmentIndex,
            weaponGroup,
            assigned));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetBehaviorActive(int equipmentIndex, int behaviorIndex, bool active)
    {
        return Send((client, observed) => client.SetBehaviorActive(observed, equipmentIndex, behaviorIndex, active));
    }

    public AetheriaRuntimeDaemonCommandEnvelope ActivateConsumable(string itemKey)
    {
        return Send((client, observed) => client.ActivateConsumable(observed, itemKey));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SensorPing()
    {
        return Send((client, observed) => client.SensorPing(observed));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetHeatsinksEnabled(bool enabled)
    {
        return Send((client, observed) => client.SetHeatsinksEnabled(observed, enabled));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetOverrideShutdown(bool enabled)
    {
        return Send((client, observed) => client.SetOverrideShutdown(observed, enabled));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetEntityOverrideShutdown(string targetEntityKey, bool enabled)
    {
        return Send((client, observed) => client.SetOverrideShutdown(observed, targetEntityKey, enabled));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetItemEnabled(int equipmentIndex, bool enabled)
    {
        return Send((client, observed) => client.SetItemEnabled(observed, equipmentIndex, enabled));
    }

    public AetheriaRuntimeDaemonCommandEnvelope ToggleShieldEnabled()
    {
        return Send((client, observed) => client.ToggleShieldEnabled(observed));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetItemOverrideShutdown(
        string targetEntityKey,
        int equipmentIndex,
        bool enabled)
    {
        return Send((client, observed) => client.SetItemOverrideShutdown(
            observed,
            targetEntityKey,
            equipmentIndex,
            enabled));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetThermotoggleTargetTemperature(
        string targetEntityKey,
        int equipmentIndex,
        int behaviorIndex,
        double targetTemperature)
    {
        return Send((client, observed) => client.SetThermotoggleTargetTemperature(
            observed,
            targetEntityKey,
            equipmentIndex,
            behaviorIndex,
            targetTemperature));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetShutdownPerformance(
        string targetEntityKey,
        double shutdownPerformance)
    {
        return Send((client, observed) => client.SetShutdownPerformance(
            observed,
            targetEntityKey,
            shutdownPerformance));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetActionBarBinding(
        string controlPath,
        string kind,
        string itemKey,
        int equipmentIndex,
        int behaviorIndex,
        int weaponGroup)
    {
        return Send((client, observed) => client.SetActionBarBinding(
            observed,
            controlPath,
            kind,
            itemKey,
            equipmentIndex,
            behaviorIndex,
            weaponGroup));
    }

    public AetheriaRuntimeDaemonCommandEnvelope ClearActionBarBinding(string controlPath)
    {
        return Send((client, observed) => client.ClearActionBarBinding(observed, controlPath));
    }

    public AetheriaRuntimeDaemonCommandEnvelope ToggleHullConductivity(
        string targetEntityKey,
        int x,
        int y,
        int axis)
    {
        return Send((client, observed) => client.ToggleHullConductivity(observed, targetEntityKey, x, y, axis));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetEntityName(string targetEntityKey, string name)
    {
        return Send((client, observed) => client.SetEntityName(observed, targetEntityKey, name));
    }

    public AetheriaRuntimeDaemonCommandEnvelope Dock(string targetEntityKey)
    {
        return Send((client, observed) => client.Dock(observed, targetEntityKey));
    }

    public AetheriaRuntimeDaemonCommandEnvelope Undock()
    {
        return Send((client, observed) => client.Undock(observed));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetDockedCurrentShip(string targetEntityKey)
    {
        return Send((client, observed) => client.SetDockedCurrentShip(observed, targetEntityKey));
    }

    public AetheriaRuntimeDaemonCommandEnvelope EnterWormhole(
        int targetZoneIndex,
        double positionX,
        double positionY)
    {
        return Send((client, observed) => client.EnterWormhole(observed, targetZoneIndex, positionX, positionY));
    }

    public AetheriaRuntimeDaemonCommandEnvelope TowToStation(
        string stationEntityKey,
        int targetZoneIndex,
        double positionX,
        double positionY)
    {
        return Send((client, observed) => client.TowToStation(
            observed,
            stationEntityKey,
            targetZoneIndex,
            positionX,
            positionY));
    }

    public AetheriaRuntimeDaemonCommandEnvelope TransferCargoItem(
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
        return Send((client, observed) => client.TransferCargoItem(
            observed,
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
            hasDestinationPosition));
    }

    public AetheriaRuntimeDaemonCommandEnvelope TradePurchase(
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
        return Send((client, observed) => client.TradePurchase(
            observed,
            purchaseKind,
            itemKey,
            quantity,
            unitPrice,
            totalPrice,
            stationEntityKey,
            stationCargoIndex,
            targetEntityKey,
            targetCargoIndex,
            sourceX,
            sourceY,
            createsDockedShip));
    }

    public AetheriaRuntimeDaemonCommandEnvelope PickUpLoot(
        string targetEntityKey,
        string itemKey,
        int quantity,
        double positionX,
        double positionY,
        double positionZ)
    {
        return Send((client, observed) => client.PickUpLoot(
            observed,
            targetEntityKey,
            itemKey,
            quantity,
            positionX,
            positionY,
            positionZ));
    }

    public AetheriaRuntimeDaemonCommandEnvelope RestoreLoadout(
        string dockedEntityKey,
        string templateName,
        int price)
    {
        return Send((client, observed) => client.RestoreLoadout(observed, dockedEntityKey, templateName, price));
    }

    public AetheriaRuntimeDaemonCommandEnvelope EquipItem(
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
        return Send((client, observed) => client.EquipItem(
            observed,
            sourceKind,
            originEntityKey,
            originIndex,
            destinationEntityKey,
            itemKey,
            sourceX,
            sourceY,
            destinationX,
            destinationY,
            hasDestinationPosition));
    }

    public AetheriaRuntimeDaemonCommandEnvelope StoreItem(
        string originEntityKey,
        int sourceEquipmentIndex,
        string destinationEntityKey,
        int destinationCargoIndex,
        string itemKey,
        int destinationX,
        int destinationY,
        bool hasDestinationPosition)
    {
        return Send((client, observed) => client.StoreItem(
            observed,
            originEntityKey,
            sourceEquipmentIndex,
            destinationEntityKey,
            destinationCargoIndex,
            itemKey,
            destinationX,
            destinationY,
            hasDestinationPosition));
    }

    private AetheriaRuntimeDaemonCommandEnvelope Send(
        Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeObservedDaemonState, AetheriaRuntimeDaemonCommandEnvelope> submit)
    {
        return _observer.SendOperation(submit);
    }
}
