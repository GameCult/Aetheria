using System;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
public sealed class AetheriaRuntimeDaemonOperationsClient
{
    private readonly Func<Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeObservedDaemonState?, AetheriaRuntimeDaemonCommandEnvelope>, AetheriaRuntimeDaemonCommandEnvelope> _submit;

    internal AetheriaRuntimeDaemonOperationsClient(
        Func<Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeObservedDaemonState?, AetheriaRuntimeDaemonCommandEnvelope>, AetheriaRuntimeDaemonCommandEnvelope> submit)
    {
        _submit = submit ?? throw new ArgumentNullException(nameof(submit));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetTarget(string targetEntityKey)
    {
        return Submit((client, observed) => client.SetTarget(observed, targetEntityKey));
    }

    public CultMeshOperationReceipt ClearTarget()
    {
        return Send((client, observed) => client.ClearTarget(observed));
    }

    public CultMeshOperationReceipt TargetNearest()
    {
        return Send((client, observed) => client.TargetNearest(observed));
    }

    public CultMeshOperationReceipt TargetNext()
    {
        return Send((client, observed) => client.TargetNext(observed));
    }

    public CultMeshOperationReceipt TargetPrevious()
    {
        return Send((client, observed) => client.TargetPrevious(observed));
    }

    public CultMeshOperationReceipt TargetReticle(double directionX, double directionY, double directionZ)
    {
        return Send((client, observed) => client.TargetReticle(observed, directionX, directionY, directionZ));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
        double directionX,
        double directionY,
        double scalarValue = 1.0)
    {
        return Submit((client, observed) => client.SetMoveVector(observed, directionX, directionY, scalarValue));
    }

    public CultMeshOperationReceipt SetLookDirection(double directionX, double directionY, double directionZ)
    {
        return Send((client, observed) => client.SetLookDirection(observed, directionX, directionY, directionZ));
    }

    public CultMeshOperationReceipt SetTractorPower(double power)
    {
        return Send((client, observed) => client.SetTractorPower(observed, power));
    }

    public CultMeshOperationReceipt FireWeaponGroup(int weaponGroup)
    {
        return Send((client, observed) => client.FireWeaponGroup(observed, weaponGroup));
    }

    public CultMeshOperationReceipt SetWeaponGroupActive(int weaponGroup, bool active)
    {
        return Send((client, observed) => client.SetWeaponGroupActive(observed, weaponGroup, active));
    }

    public CultMeshOperationReceipt SetWeaponGroupMembership(
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

    public CultMeshOperationReceipt SetBehaviorActive(int equipmentIndex, int behaviorIndex, bool active)
    {
        return Send((client, observed) => client.SetBehaviorActive(observed, equipmentIndex, behaviorIndex, active));
    }

    public CultMeshOperationReceipt ActivateConsumable(string itemKey)
    {
        return Send((client, observed) => client.ActivateConsumable(observed, itemKey));
    }

    public CultMeshOperationReceipt SensorPing()
    {
        return Send((client, observed) => client.SensorPing(observed));
    }

    public CultMeshOperationReceipt SetHeatsinksEnabled(bool enabled)
    {
        return Send((client, observed) => client.SetHeatsinksEnabled(observed, enabled));
    }

    public CultMeshOperationReceipt SetOverrideShutdown(bool enabled)
    {
        return Send((client, observed) => client.SetOverrideShutdown(observed, enabled));
    }

    public CultMeshOperationReceipt SetEntityOverrideShutdown(string targetEntityKey, bool enabled)
    {
        return Send((client, observed) => client.SetOverrideShutdown(observed, targetEntityKey, enabled));
    }

    public CultMeshOperationReceipt SetItemEnabled(int equipmentIndex, bool enabled)
    {
        return Send((client, observed) => client.SetItemEnabled(observed, equipmentIndex, enabled));
    }

    public CultMeshOperationReceipt ToggleShieldEnabled()
    {
        return Send((client, observed) => client.ToggleShieldEnabled(observed));
    }

    public CultMeshOperationReceipt SetItemOverrideShutdown(
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

    public CultMeshOperationReceipt SetThermotoggleTargetTemperature(
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

    public CultMeshOperationReceipt SetShutdownPerformance(
        string targetEntityKey,
        double shutdownPerformance)
    {
        return Send((client, observed) => client.SetShutdownPerformance(
            observed,
            targetEntityKey,
            shutdownPerformance));
    }

    public CultMeshOperationReceipt ToggleHullConductivity(
        string targetEntityKey,
        int x,
        int y,
        int axis)
    {
        return Send((client, observed) => client.ToggleHullConductivity(observed, targetEntityKey, x, y, axis));
    }

    public CultMeshOperationReceipt SetEntityName(string targetEntityKey, string name)
    {
        return Send((client, observed) => client.SetEntityName(observed, targetEntityKey, name));
    }

    public CultMeshOperationReceipt Dock(string targetEntityKey)
    {
        return Send((client, observed) => client.Dock(observed, targetEntityKey));
    }

    public CultMeshOperationReceipt DockNearest()
    {
        return Send((client, observed) => client.DockNearest(observed));
    }

    public CultMeshOperationReceipt Undock()
    {
        return Send((client, observed) => client.Undock(observed));
    }

    public CultMeshOperationReceipt Interact()
    {
        return Send((client, observed) => client.Interact(observed));
    }

    public CultMeshOperationReceipt SetDockedCurrentShip(string targetEntityKey)
    {
        return Send((client, observed) => client.SetDockedCurrentShip(observed, targetEntityKey));
    }

    public CultMeshOperationReceipt EnterWormhole(
        int targetZoneIndex,
        double positionX,
        double positionY)
    {
        return Send((client, observed) => client.EnterWormhole(observed, targetZoneIndex, positionX, positionY));
    }

    public CultMeshOperationReceipt TowToStation(string stationEntityKey)
    {
        return Send((client, observed) => client.TowToStation(observed, stationEntityKey));
    }

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

    public CultMeshOperationReceipt TradePurchase(
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

    public CultMeshOperationReceipt PickUpLoot(
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

    public CultMeshOperationReceipt RestoreLoadout(
        string dockedEntityKey,
        string templateName,
        int price)
    {
        return Send((client, observed) => client.RestoreLoadout(observed, dockedEntityKey, templateName, price));
    }

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

    public CultMeshOperationReceipt StoreItem(
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

    private CultMeshOperationReceipt Send(
        Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeObservedDaemonState?, AetheriaRuntimeDaemonCommandEnvelope> submit)
    {
        return Submit(submit);
    }

    private AetheriaRuntimeDaemonCommandEnvelope Submit(
        Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeObservedDaemonState?, AetheriaRuntimeDaemonCommandEnvelope> submit)
    {
        return _submit(submit);
    }
}
}
