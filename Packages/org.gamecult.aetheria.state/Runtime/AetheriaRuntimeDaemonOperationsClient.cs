using System;
using GameCult.Eve.Surface;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
public sealed class AetheriaRuntimeDaemonOperationsClient
{
    private readonly Func<Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeDaemonFrameDocument?, AetheriaRuntimeDaemonCommandEnvelope>, AetheriaRuntimeDaemonCommandEnvelope> _submit;

    internal AetheriaRuntimeDaemonOperationsClient(
        Func<Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeDaemonFrameDocument?, AetheriaRuntimeDaemonCommandEnvelope>, AetheriaRuntimeDaemonCommandEnvelope> submit)
    {
        _submit = submit ?? throw new ArgumentNullException(nameof(submit));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetTarget(string targetEntityKey)
    {
        return Submit((client, frame) => client.SetTarget(frame, targetEntityKey));
    }

    public CultMeshOperationReceipt ClearTarget()
    {
        return Send((client, frame) => client.ClearTarget(frame));
    }

    public CultMeshOperationReceipt TargetNearest()
    {
        return Send((client, frame) => client.TargetNearest(frame));
    }

    public CultMeshOperationReceipt TargetNext()
    {
        return Send((client, frame) => client.TargetNext(frame));
    }

    public CultMeshOperationReceipt TargetPrevious()
    {
        return Send((client, frame) => client.TargetPrevious(frame));
    }

    public CultMeshOperationReceipt TargetReticle(double directionX, double directionY, double directionZ)
    {
        return Send((client, frame) => client.TargetReticle(frame, directionX, directionY, directionZ));
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
        double directionX,
        double directionY,
        double scalarValue = 1.0)
    {
        return Submit((client, frame) => client.SetMoveVector(frame, directionX, directionY, scalarValue));
    }

    public CultMeshOperationReceipt SetLookDirection(double directionX, double directionY, double directionZ)
    {
        return Send((client, frame) => client.SetLookDirection(frame, directionX, directionY, directionZ));
    }

    public CultMeshOperationReceipt SetTractorPower(double power)
    {
        return Send((client, frame) => client.SetTractorPower(frame, power));
    }

    public CultMeshOperationReceipt FireWeaponGroup(int weaponGroup)
    {
        return Send((client, frame) => client.FireWeaponGroup(frame, weaponGroup));
    }

    public CultMeshOperationReceipt SetWeaponGroupActive(int weaponGroup, bool active)
    {
        return Send((client, frame) => client.SetWeaponGroupActive(frame, weaponGroup, active));
    }

    public CultMeshOperationReceipt SetWeaponGroupMembership(
        string targetEntityKey,
        int equipmentIndex,
        int weaponGroup,
        bool assigned)
    {
        return Send((client, frame) => client.SetWeaponGroupMembership(
            frame,
            targetEntityKey,
            equipmentIndex,
            weaponGroup,
            assigned));
    }

    public CultMeshOperationReceipt SetBehaviorActive(int equipmentIndex, int behaviorIndex, bool active)
    {
        return Send((client, frame) => client.SetBehaviorActive(frame, equipmentIndex, behaviorIndex, active));
    }

    public CultMeshOperationReceipt ActivateConsumable(string itemKey)
    {
        return Send((client, frame) => client.ActivateConsumable(frame, itemKey));
    }

    public CultMeshOperationReceipt SensorPing()
    {
        return Send((client, frame) => client.SensorPing(frame));
    }

    public CultMeshOperationReceipt SetHeatsinksEnabled(bool enabled)
    {
        return Send((client, frame) => client.SetHeatsinksEnabled(frame, enabled));
    }

    public CultMeshOperationReceipt SetOverrideShutdown(bool enabled)
    {
        return Send((client, frame) => client.SetOverrideShutdown(frame, enabled));
    }

    public CultMeshOperationReceipt SetEntityOverrideShutdown(string targetEntityKey, bool enabled)
    {
        return Send((client, frame) => client.SetOverrideShutdown(frame, targetEntityKey, enabled));
    }

    public CultMeshOperationReceipt SetItemEnabled(int equipmentIndex, bool enabled)
    {
        return Send((client, frame) => client.SetItemEnabled(frame, equipmentIndex, enabled));
    }

    public CultMeshOperationReceipt ToggleShieldEnabled()
    {
        return Send((client, frame) => client.ToggleShieldEnabled(frame));
    }

    public CultMeshOperationReceipt SetItemOverrideShutdown(
        string targetEntityKey,
        int equipmentIndex,
        bool enabled)
    {
        return Send((client, frame) => client.SetItemOverrideShutdown(
            frame,
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
        return Send((client, frame) => client.SetThermotoggleTargetTemperature(
            frame,
            targetEntityKey,
            equipmentIndex,
            behaviorIndex,
            targetTemperature));
    }

    public CultMeshOperationReceipt SetShutdownPerformance(
        string targetEntityKey,
        double shutdownPerformance)
    {
        return Send((client, frame) => client.SetShutdownPerformance(
            frame,
            targetEntityKey,
            shutdownPerformance));
    }

    public CultMeshOperationReceipt ToggleHullConductivity(
        string targetEntityKey,
        int x,
        int y,
        int axis)
    {
        return Send((client, frame) => client.ToggleHullConductivity(frame, targetEntityKey, x, y, axis));
    }

    public CultMeshOperationReceipt SetEntityName(string targetEntityKey, string name)
    {
        return Send((client, frame) => client.SetEntityName(frame, targetEntityKey, name));
    }

    public CultMeshOperationReceipt Dock(string targetEntityKey)
    {
        return Send((client, frame) => client.Dock(frame, targetEntityKey));
    }

    public CultMeshOperationReceipt DockNearest()
    {
        return Send((client, frame) => client.DockNearest(frame));
    }

    public CultMeshOperationReceipt Undock()
    {
        return Send((client, frame) => client.Undock(frame));
    }

    public CultMeshOperationReceipt Interact()
    {
        return Send((client, frame) => client.Interact(frame));
    }

    public CultMeshOperationReceipt SetDockedCurrentShip(string targetEntityKey)
    {
        return Send((client, frame) => client.SetDockedCurrentShip(frame, targetEntityKey));
    }

    public CultMeshOperationReceipt EnterWormhole(
        int targetZoneIndex,
        double positionX,
        double positionY)
    {
        return Send((client, frame) => client.EnterWormhole(frame, targetZoneIndex, positionX, positionY));
    }

    public CultMeshOperationReceipt TowToStation(string stationEntityKey)
    {
        return Send((client, frame) => client.TowToStation(frame, stationEntityKey));
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
        return Send((client, frame) => client.TransferCargoItem(
            frame,
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
        return Send((client, frame) => client.TradePurchase(
            frame,
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
        return Send((client, frame) => client.PickUpLoot(
            frame,
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
        return Send((client, frame) => client.RestoreLoadout(frame, dockedEntityKey, templateName, price));
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
        return Send((client, frame) => client.EquipItem(
            frame,
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
        return Send((client, frame) => client.StoreItem(
            frame,
            originEntityKey,
            sourceEquipmentIndex,
            destinationEntityKey,
            destinationCargoIndex,
            itemKey,
            destinationX,
            destinationY,
            hasDestinationPosition));
    }

    public bool TrySubmitSurfaceCommand(
        EveSurfaceCommandRequest request,
        out AetheriaRuntimeDaemonCommandEnvelope? envelope)
    {
        envelope = null;
        if (request == null ||
            !string.Equals(request.ProviderId, "aetheria.daemon", StringComparison.Ordinal) ||
            !TryResolveSurfaceCommandKind(request, out var kind))
        {
            return false;
        }

        try
        {
            envelope = Submit((client, frame) =>
                AetheriaRuntimeDaemonSurfaceCommandCatalog.TrySubmitArgumentless(
                    client,
                    frame,
                    kind,
                    out var submitted)
                    ? submitted!
                    : throw new UnsupportedSurfaceCommandException());
            return true;
        }
        catch (UnsupportedSurfaceCommandException)
        {
            envelope = null;
            return false;
        }
    }

    private CultMeshOperationReceipt Send(
        Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeDaemonFrameDocument?, AetheriaRuntimeDaemonCommandEnvelope> submit)
    {
        return Submit(submit);
    }

    private AetheriaRuntimeDaemonCommandEnvelope Submit(
        Func<AetheriaRuntimeDaemonOperationClient, AetheriaRuntimeDaemonFrameDocument?, AetheriaRuntimeDaemonCommandEnvelope> submit)
    {
        return _submit(submit);
    }

    private static bool TryResolveSurfaceCommandKind(
        EveSurfaceCommandRequest request,
        out AetheriaRuntimeDaemonCommandKinds kind)
    {
        kind = AetheriaRuntimeDaemonCommandKinds.None;
        var command = request.Operation?.OperationId ?? "";
        if (command.StartsWith(AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandPrefix, StringComparison.Ordinal))
            command = command.Substring(AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandPrefix.Length);

        return Enum.TryParse(command, ignoreCase: false, out kind) &&
               kind != AetheriaRuntimeDaemonCommandKinds.None;
    }

    private sealed class UnsupportedSurfaceCommandException : Exception
    {
    }
}
}
