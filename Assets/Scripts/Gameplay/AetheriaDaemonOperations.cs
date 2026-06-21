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
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetTarget, command =>
            command.TargetEntityKey = targetEntityKey ?? "");
    }

    public AetheriaRuntimeDaemonCommandEnvelope ClearTarget()
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.ClearTarget);
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetMoveVector(
        double directionX,
        double directionY,
        double scalarValue = 1.0)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetMoveVector, command =>
        {
            command.DirectionX = directionX;
            command.DirectionY = directionY;
            command.ScalarValue = scalarValue;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetLookDirection(double directionX, double directionY, double directionZ)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetLookDirection, command =>
        {
            command.DirectionX = directionX;
            command.DirectionY = directionY;
            command.PositionZ = directionZ;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetTractorPower(double power)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetTractorPower, command =>
            command.ScalarValue = power);
    }

    public AetheriaRuntimeDaemonCommandEnvelope FireWeaponGroup(int weaponGroup)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, command =>
            command.WeaponGroup = weaponGroup);
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupActive(int weaponGroup, bool active)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive, command =>
        {
            command.WeaponGroup = weaponGroup;
            command.ScalarValue = active ? 1.0 : 0.0;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetWeaponGroupMembership(
        string targetEntityKey,
        int equipmentIndex,
        int weaponGroup,
        bool assigned)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership, command =>
        {
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.WeaponGroup = weaponGroup;
            command.ScalarValue = assigned ? 1.0 : 0.0;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetBehaviorActive(int equipmentIndex, int behaviorIndex, bool active)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive, command =>
        {
            command.EquipmentIndex = equipmentIndex;
            command.BehaviorIndex = behaviorIndex;
            command.ScalarValue = active ? 1.0 : 0.0;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope ActivateConsumable(string itemKey)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.ActivateConsumable, command =>
            command.TextValue = itemKey ?? "");
    }

    public AetheriaRuntimeDaemonCommandEnvelope SensorPing()
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SensorPing);
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetHeatsinksEnabled(bool enabled)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled, command =>
            command.ScalarValue = enabled ? 1.0 : 0.0);
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetOverrideShutdown(bool enabled)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown, command =>
            command.ScalarValue = enabled ? 1.0 : 0.0);
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetEntityOverrideShutdown(string targetEntityKey, bool enabled)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown, command =>
        {
            command.TargetEntityKey = targetEntityKey ?? "";
            command.ScalarValue = enabled ? 1.0 : 0.0;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetItemEnabled(int equipmentIndex, bool enabled)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetItemEnabled, command =>
        {
            command.EquipmentIndex = equipmentIndex;
            command.ScalarValue = enabled ? 1.0 : 0.0;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope ToggleShieldEnabled()
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled);
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetItemOverrideShutdown(
        string targetEntityKey,
        int equipmentIndex,
        bool enabled)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetItemOverrideShutdown, command =>
        {
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.ScalarValue = enabled ? 1.0 : 0.0;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetThermotoggleTargetTemperature(
        string targetEntityKey,
        int equipmentIndex,
        int behaviorIndex,
        double targetTemperature)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetThermotoggleTargetTemperature, command =>
        {
            command.TargetEntityKey = targetEntityKey ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.BehaviorIndex = behaviorIndex;
            command.ScalarValue = targetTemperature;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetShutdownPerformance(
        string targetEntityKey,
        double shutdownPerformance)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance, command =>
        {
            command.TargetEntityKey = targetEntityKey ?? "";
            command.ScalarValue = shutdownPerformance;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetActionBarBinding(
        string controlPath,
        string kind,
        string itemKey,
        int equipmentIndex,
        int behaviorIndex,
        int weaponGroup)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetActionBarBinding, command =>
        {
            command.TextValue = controlPath ?? "";
            command.EquipmentIndex = equipmentIndex;
            command.BehaviorIndex = behaviorIndex;
            command.WeaponGroup = weaponGroup;
            command.ActionBarBinding.Kind = kind ?? "";
            command.ActionBarBinding.ItemKey = itemKey ?? "";
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope ClearActionBarBinding(string controlPath)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.ClearActionBarBinding, command =>
            command.TextValue = controlPath ?? "");
    }

    public AetheriaRuntimeDaemonCommandEnvelope ToggleHullConductivity(
        string targetEntityKey,
        int x,
        int y,
        int axis)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity, command =>
        {
            command.TargetEntityKey = targetEntityKey ?? "";
            command.PositionX = x;
            command.PositionY = y;
            command.ScalarValue = axis;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetEntityName(string targetEntityKey, string name)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetEntityName, command =>
        {
            command.TargetEntityKey = targetEntityKey ?? "";
            command.TextValue = name ?? "";
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope Dock(string targetEntityKey)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.Dock, command =>
            command.TargetEntityKey = targetEntityKey ?? "");
    }

    public AetheriaRuntimeDaemonCommandEnvelope Undock()
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.Undock);
    }

    public AetheriaRuntimeDaemonCommandEnvelope SetDockedCurrentShip(string targetEntityKey)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip, command =>
            command.TargetEntityKey = targetEntityKey ?? "");
    }

    public AetheriaRuntimeDaemonCommandEnvelope EnterWormhole(
        int targetZoneIndex,
        double positionX,
        double positionY)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.EnterWormhole, command =>
        {
            command.TargetZoneIndex = targetZoneIndex;
            command.PositionX = positionX;
            command.PositionY = positionY;
        });
    }

    public AetheriaRuntimeDaemonCommandEnvelope TowToStation(
        string stationEntityKey,
        int targetZoneIndex,
        double positionX,
        double positionY)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.TowToStation, command =>
        {
            command.TargetEntityKey = stationEntityKey ?? "";
            command.TargetZoneIndex = targetZoneIndex;
            command.PositionX = positionX;
            command.PositionY = positionY;
        });
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
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.TransferCargoItem, command =>
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
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.TradePurchase, command =>
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
        string targetEntityKey,
        string itemKey,
        int quantity,
        double positionX,
        double positionY,
        double positionZ)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.PickUpLoot, command =>
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
        string dockedEntityKey,
        string templateName,
        int price)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.RestoreLoadout, command =>
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
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.EquipItem, command =>
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
        string originEntityKey,
        int sourceEquipmentIndex,
        string destinationEntityKey,
        int destinationCargoIndex,
        string itemKey,
        int destinationX,
        int destinationY,
        bool hasDestinationPosition)
    {
        return _observer.SendOperation(AetheriaRuntimeDaemonCommandKinds.StoreItem, command =>
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
