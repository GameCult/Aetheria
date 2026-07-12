using System;
using System.Linq;
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

    public static bool TryCreateSurfaceCommandDocument(
        EveSurfaceCommandRequest request,
        AetheriaRuntimeDaemonFrameDocument? frame,
        string stateFilePath,
        string clientId,
        string sessionId,
        out AetheriaRuntimeDaemonCommandDocument? command)
    {
        command = null;
        if (request == null)
            return false;

        AetheriaRuntimeDaemonCommandDocument? translated = null;
        var operations = new AetheriaRuntimeDaemonOperationsClient(submit =>
        {
            var client = new AetheriaRuntimeDaemonOperationClient(
                string.IsNullOrWhiteSpace(stateFilePath) ? "." : stateFilePath,
                clientId,
                sessionId,
                document =>
                {
                    translated = document;
                    return AetheriaRuntimeDaemonOperationClient.ToEnvelope(document);
                });
            return submit(client, frame);
        });
        var accepted = operations.TrySubmitSurfaceCommand(request, out _);
        var actorEntityKey = ReadPayloadString(request, "entityId", "");
        if (translated != null && !string.IsNullOrWhiteSpace(actorEntityKey))
            translated.ActorEntityKey = actorEntityKey;
        if (string.Equals(Environment.GetEnvironmentVariable("AETHERIA_TRACE_EVE_SNAPSHOTS"), "1", StringComparison.Ordinal))
            Console.WriteLine(
                $"Eve command translated command={request.Command} actor={translated?.ActorEntityKey ?? "missing"} " +
                $"direction={translated?.DirectionX},{translated?.DirectionY} scalar={translated?.ScalarValue}");
        command = translated;
        return accepted && command != null;
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

    public AetheriaRuntimeDaemonCommandEnvelope IssueAgentTask(AetheriaRuntimeAgentTaskCommand task)
    {
        return Submit((client, frame) => client.IssueAgentTask(frame, task));
    }

    public AetheriaRuntimeDaemonCommandEnvelope CancelAgentTask(string taskId)
    {
        return Submit((client, frame) => client.CancelAgentTask(frame, taskId));
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
        double positionZ,
        int pickupIndex = -1)
    {
        return Send((client, frame) => client.PickUpLoot(
            frame,
            targetEntityKey,
            itemKey,
            quantity,
            positionX,
            positionY,
            positionZ,
            pickupIndex));
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
                TrySubmitSurfaceCommand(client, frame, kind, request, out var submitted)
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
        var command = NormalizeSurfaceCommandName(request.Operation?.OperationId ?? "");
        if (!Enum.TryParse(command, ignoreCase: false, out kind) ||
            kind == AetheriaRuntimeDaemonCommandKinds.None)
        {
            command = NormalizeSurfaceCommandName(ReadPayloadString(request, "commandId", ""));
        }

        return Enum.TryParse(command, ignoreCase: false, out kind) &&
               kind != AetheriaRuntimeDaemonCommandKinds.None;
    }

    private static string NormalizeSurfaceCommandName(string command)
    {
        if (command.StartsWith(AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandPrefix, StringComparison.Ordinal))
            command = command.Substring(AetheriaRuntimeDaemonSurfaceCommandCatalog.CommandPrefix.Length);
        return command;
    }

    private static bool TrySubmitSurfaceCommand(
        AetheriaRuntimeDaemonOperationClient client,
        AetheriaRuntimeDaemonFrameDocument? frame,
        AetheriaRuntimeDaemonCommandKinds kind,
        EveSurfaceCommandRequest request,
        out AetheriaRuntimeDaemonCommandEnvelope? envelope)
    {
        envelope = null;
        if (client == null)
            return false;

        envelope = kind switch
        {
            AetheriaRuntimeDaemonCommandKinds.SetMoveVector => client.SetMoveVector(
                frame,
                ReadPayloadDouble(request, "directionX", 0.0),
                ReadPayloadDouble(request, "directionY", 0.0),
                ReadPayloadDouble(request, "scalarValue", 1.0)),
            AetheriaRuntimeDaemonCommandKinds.SetLookDirection => client.SetLookDirection(
                frame,
                ReadPayloadDouble(request, "directionX", 0.0),
                ReadPayloadDouble(request, "directionY", 1.0),
                ReadPayloadDouble(request, "directionZ", 0.0)),
            AetheriaRuntimeDaemonCommandKinds.SetTractorPower => client.SetTractorPower(
                frame,
                ReadPayloadDouble(request, "scalarValue", 1.0)),
            AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup => client.FireWeaponGroup(
                frame,
                ReadWeaponGroup(request)),
            AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive => client.SetWeaponGroupActive(
                frame,
                ReadWeaponGroup(request),
                ReadPayloadBool(request, "active", true)),
            AetheriaRuntimeDaemonCommandKinds.SetTarget => client.SetTarget(
                frame,
                ReadPayloadString(request, "targetEntityId", ReadPayloadString(request, "entityId", ""))),
            AetheriaRuntimeDaemonCommandKinds.IssueAgentTask => client.IssueAgentTask(
                frame,
                new AetheriaRuntimeAgentTaskCommand
                {
                    TaskId = ReadPayloadString(request, "taskId", ""),
                    CorporationKey = ReadPayloadString(request, "corporation", ""),
                    TaskType = ReadPayloadString(request, "taskType", ""),
                    Priority = (int)ReadPayloadDouble(request, "priority", 0),
                    ZoneIndex = (int)ReadPayloadDouble(request, "zoneIndex", -1),
                    TargetEntityIndex = (int)ReadPayloadDouble(request, "targetEntityIndex", -1),
                    TargetPositionX = ReadPayloadDouble(request, "targetPositionX", 0),
                    TargetPositionZ = ReadPayloadDouble(request, "targetPositionZ", 0),
                    CompletionRadius = ReadPayloadDouble(request, "completionRadius", 10),
                    WeaponGroup = (int)ReadPayloadDouble(request, "weaponGroup", 0),
                    OriginEntityIndex = (int)ReadPayloadDouble(request, "originEntityIndex", -1),
                    ItemKey = ReadPayloadString(request, "itemKey", ""),
                    Quantity = (int)ReadPayloadDouble(request, "quantity", 0),
                    TargetBodyKeys = ReadPayloadString(request, "targetBodyKeys", "")
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(key => key.Trim())
                        .Where(key => !string.IsNullOrWhiteSpace(key))
                        .ToArray(),
                    OrbitParentKey = ReadPayloadString(request, "orbitParentKey", ""),
                    OrbitDistance = ReadPayloadDouble(request, "orbitDistance", 0)
                }),
            AetheriaRuntimeDaemonCommandKinds.CancelAgentTask => client.CancelAgentTask(
                frame,
                ReadPayloadString(request, "taskId", "")),
            _ => AetheriaRuntimeDaemonSurfaceCommandCatalog.TrySubmitArgumentless(
                    client,
                    frame,
                    kind,
                    out var submitted)
                ? submitted
                : null
        };

        return envelope != null;
    }

    private static string ReadPayloadString(
        EveSurfaceCommandRequest request,
        string key,
        string defaultValue)
    {
        var value = request.Payload.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static double ReadPayloadDouble(
        EveSurfaceCommandRequest request,
        string key,
        double defaultValue)
    {
        return request.Payload.GetDouble(key, defaultValue);
    }

    private static bool ReadPayloadBool(
        EveSurfaceCommandRequest request,
        string key,
        bool defaultValue)
    {
        var value = request.Payload.GetString(key);
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static int ReadWeaponGroup(EveSurfaceCommandRequest request)
    {
        var value = request.Payload.GetString("weaponGroup");
        if (int.TryParse(value, out var parsed))
            return parsed;

        value = request.Payload.GetString("actionId");
        return int.TryParse(value, out parsed) ? parsed : 0;
    }

    private sealed class UnsupportedSurfaceCommandException : Exception
    {
    }
}
}
