using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using GameCult.Eve.Surface;
using MessagePack;

namespace GameCult.Aetheria.State.Verse
{
    [MessagePackObject]
    public sealed class AetheriaRuntimeInputGestureDocument
    {
        [Key(0)] public string Kind { get; set; } = "direct";
        [Key(1)] public string[] Controls { get; set; } = Array.Empty<string>();
        [Key(2)] public int MaxStepIntervalMs { get; set; } = 650;
        [Key(3)] public string CompletionControl { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputBindingDocument
    {
        [Key(0)] public string BindingId { get; set; } = "";
        [Key(1)] public string ActionId { get; set; } = "";
        [Key(2)] public AetheriaRuntimeInputGestureDocument Gesture { get; set; } = new AetheriaRuntimeInputGestureDocument();
        [Key(3)] public bool ActionBar { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputActionDocument
    {
        [Key(0)] public string ActionId { get; set; } = "";
        [Key(1)] public string Label { get; set; } = "";
        [Key(2)] public string Operation { get; set; } = "";
        [Key(3)] public string Context { get; set; } = "pilot";
        [Key(4)] public string Category { get; set; } = "ship";
        [Key(5)] public string Availability { get; set; } = "available";
        [Key(6)] public string SourceRef { get; set; } = "";
        [Key(7)] public Dictionary<string, string> Payload { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        [Key(8)] public AetheriaRuntimeInputValueDocument? InputValue { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputValueDocument
    {
        [Key(0)] public string Model { get; set; } = "";
        [Key(1)] public string PayloadKey { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputProfileDocument
    {
        [Key(0)] public string ProfileId { get; set; } = "";
        [Key(1)] public string DeviceClass { get; set; } = "";
        [Key(2)] public AetheriaRuntimeInputBindingDocument[] Bindings { get; set; } = Array.Empty<AetheriaRuntimeInputBindingDocument>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputCapabilityDocument
    {
        public const string SchemaId = "gamecult.eve.input_capability.v1";
        [Key(0)] public string Schema { get; set; } = SchemaId;
        [Key(1)] public string ProviderId { get; set; } = "aetheria";
        [Key(2)] public string CapabilityId { get; set; } = "aetheria.pilot.input";
        [Key(3)] public long Version { get; set; }
        [Key(4)] public AetheriaRuntimeInputActionDocument[] Actions { get; set; } = Array.Empty<AetheriaRuntimeInputActionDocument>();
        [Key(5)] public AetheriaRuntimeInputProfileDocument[] DefaultProfiles { get; set; } = Array.Empty<AetheriaRuntimeInputProfileDocument>();

        public static AetheriaRuntimeInputCapabilityDocument FromFrame(
            AetheriaRuntimeDaemonFrameDocument frame,
            bool includeSimulationClock = false,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            var run = frame?.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var entity = run.Zones.SelectMany(zone => zone.Entities).FirstOrDefault(candidate =>
                string.Equals(run.EntityRecordKey(run.CurrentZoneIndex, candidate.EntityIndex), run.CurrentEntityKey, StringComparison.Ordinal));
            var actions = CoreActions().ToList();
            if (includeSimulationClock)
            {
                actions.Add(Action("simulation.pause", "Pause", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "0")));
                actions.Add(Action("simulation.step", "Advance One Step", "AdvanceSimulationStep", "simulation", "terminus-clock"));
                actions.Add(Action("simulation.rate.quarter", "Quarter Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "0.25")));
                actions.Add(Action("simulation.rate.half", "Half Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "0.5")));
                actions.Add(Action("simulation.rate.realtime", "Real Time", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "1")));
                actions.Add(Action("simulation.rate.double", "2x Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "2")));
                actions.Add(Action("simulation.rate.quadruple", "4x Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "4")));
                actions.Add(Action("simulation.rate.eight", "8x Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "8")));
                actions.Add(Action("simulation.rate.sixteen", "16x Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "16")));
                actions.Add(Action("simulation.rate.thirty-two", "32x Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "32")));
                actions.Add(Action("simulation.rate.sixty-four", "64x Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "64")));
                actions.Add(Action("simulation.rate.one-twenty-eight", "128x Speed", "SetSimulationRate", "simulation", "terminus-clock", ("scalarValue", "128")));
            }
            if (entity != null)
            {
                actions.AddRange((entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>()).Select((_, index) => Action($"weapon-group.{index}.fire", $"Fire Weapon Group {index + 1}", "FireWeaponGroup", "weapon-group", $"{run.CurrentEntityKey}#weapon-group/{index}", ("weaponGroup", index.ToString()))));
                actions.AddRange(EquipmentBehaviorActions(run, entity, catalog));
                actions.AddRange((entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
                    .SelectMany(bay => bay.Items)
                    .Where(slot => slot?.Item != null && AetheriaRuntimeConsumableSimulation.CanActivate(entity, catalog, slot.Item.ItemKey))
                    .GroupBy(slot => slot.Item.ItemKey, StringComparer.Ordinal)
                    .Select((group, index) =>
                    {
                        var itemKey = group.Key;
                        var item = catalog?.FindItem(itemKey);
                        return Action($"cargo.{itemKey}.use", $"Use {item?.Name ?? itemKey}", "ActivateConsumable", "consumable", $"{run.CurrentEntityKey}#cargo/{index}", ("itemKey", itemKey));
                    }));
                actions.AddRange(TradeActions(run, entity, catalog));
            }
            return new AetheriaRuntimeInputCapabilityDocument { Version = frame?.FrameId ?? 0, Actions = actions.ToArray(), DefaultProfiles = BuildDefaultProfiles(actions) };
        }

        private static IEnumerable<AetheriaRuntimeInputActionDocument> EquipmentBehaviorActions(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (catalog == null)
                yield break;

            var states = (entity.BehaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
                .Where(value => value != null && string.Equals(value.OwnerKind,
                    AetheriaRuntimeBehaviorStateProjector.EquipmentOwnerKind, StringComparison.Ordinal))
                .ToDictionary(value => (value.OwnerIndex, value.BehaviorIndex));
            var online = (entity.EquipmentStates ?? Array.Empty<AetheriaRuntimeEquipmentStateCommit>())
                .Where(value => value != null).ToDictionary(value => value.EquipmentIndex);
            var equipment = entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
            for (var equipmentIndex = 0; equipmentIndex < equipment.Count; equipmentIndex++)
            {
                var installed = equipment[equipmentIndex]?.Item;
                var item = catalog.FindItem(installed?.ItemKey ?? "");
                if (installed == null || item == null)
                    continue;
                var available = installed.Enabled && installed.Durability > 0.01 &&
                    (!online.TryGetValue(equipmentIndex, out var equipmentState) || equipmentState.Online);
                var payloads = item.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>();
                for (var behaviorIndex = 0; behaviorIndex < payloads.Count; behaviorIndex++)
                {
                    var payload = payloads[behaviorIndex];
                    if (payload == null || !states.TryGetValue((equipmentIndex, behaviorIndex), out var state))
                        continue;
                    var source = $"{run.CurrentEntityKey}#equipment/{equipmentIndex}/behavior/{behaviorIndex}";
                    var actionId = $"equipment.{equipmentIndex}.behavior.{behaviorIndex}";
                    if (string.Equals(payload.Kind, "Switch", StringComparison.Ordinal) ||
                        string.Equals(payload.Kind, "Trigger", StringComparison.Ordinal))
                    {
                        var action = Action(
                            actionId,
                            $"{(payload.Kind == "Trigger" ? "Trigger" : "Activate")} {item.Name}",
                            "SetBehaviorActive",
                            "equipment",
                            source,
                            ("equipmentIndex", equipmentIndex.ToString()),
                            ("behaviorIndex", behaviorIndex.ToString()),
                            ("active", "0"),
                            ("currentValue", state.SwitchActivated ? "1" : "0"));
                        action.Availability = available ? "available" : "unavailable";
                        action.InputValue = new AetheriaRuntimeInputValueDocument
                        {
                            Model = "button-hold.v1",
                            PayloadKey = "active"
                        };
                        yield return action;
                        continue;
                    }

                    if (!string.Equals(payload.Kind, "Thermotoggle", StringComparison.Ordinal) ||
                        !ReadBool(payload, 3))
                        continue;
                    var thermostat = Action(
                        actionId,
                        $"Set {item.Name} Target Temperature",
                        "SetThermotoggleTargetTemperature",
                        "equipment",
                        source,
                        ("targetEntityKey", run.CurrentEntityKey),
                        ("equipmentIndex", equipmentIndex.ToString()),
                        ("behaviorIndex", behaviorIndex.ToString()),
                        ("scalarValue", state.ThermotoggleTargetTemperature.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                        ("currentValue", state.ThermotoggleTargetTemperature.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                        ("unit", "kelvin"));
                    thermostat.Availability = available ? "available" : "unavailable";
                    thermostat.InputValue = new AetheriaRuntimeInputValueDocument
                    {
                        Model = "scalar.v1",
                        PayloadKey = "scalarValue"
                    };
                    yield return thermostat;
                }
            }
        }

        private static bool ReadBool(AetheriaRuntimeBehaviorPayload payload, int key) =>
            (payload.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
            .FirstOrDefault(field => field != null && field.Key == key)?.Value?.BoolValue ?? false;

        private static IEnumerable<AetheriaRuntimeInputActionDocument> TradeActions(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeEntitySnapshotCommit entity,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (catalog == null)
                yield break;
            var zone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null && candidate.ZoneIndex == run.CurrentZoneIndex);
            var parent = (zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .FirstOrDefault(candidate => candidate != null &&
                    (candidate.DockingBayAssignments ?? Array.Empty<int>()).Contains(entity.EntityIndex));
            if (parent == null)
                yield break;

            var stationKey = AetheriaRuntimeRunCheckpointCommit.EntityRecordKey(
                run.RunId,
                run.CurrentZoneIndex,
                parent.EntityIndex);
            var cargo = parent.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            for (var bayIndex = 0; bayIndex < cargo.Count; bayIndex++)
            {
                foreach (var slot in cargo[bayIndex]?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                {
                    var itemKey = slot?.Item?.ItemKey ?? "";
                    var typedItem = catalog.FindItem(itemKey);
                    if (typedItem == null || slot?.Item == null || slot.Item.Quantity <= 0)
                        continue;
                    var value = AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
                        typedItem,
                        slot.Item,
                        catalog.TradeValueSettings).Price;
                    var createsShip = !string.IsNullOrWhiteSpace(typedItem.HullType);
                    var canReceive = createsShip
                        ? (parent.DockingBayAssignments ?? Array.Empty<int>()).Any(index => index < 0)
                        : AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(entity, catalog, itemKey, 0) > 0;
                    var action = Action(
                        $"trade.buy.{StableToken(itemKey)}.{bayIndex}.{slot.X}.{slot.Y}",
                        $"Buy {typedItem.Name} ({value})",
                        "TradePurchase",
                        "trade",
                        $"{stationKey}#cargo/{bayIndex}/{slot.X}/{slot.Y}",
                        ("itemKey", itemKey),
                        ("quantity", "1"),
                        ("stationEntityKey", stationKey),
                        ("stationCargoIndex", bayIndex.ToString()),
                        ("targetEntityKey", run.CurrentEntityKey),
                        ("targetCargoIndex", "0"),
                        ("sourceX", slot.X.ToString()),
                        ("sourceY", slot.Y.ToString()));
                    action.Availability = value >= 0 && run.Credits >= value && canReceive
                        ? "available"
                        : "unavailable";
                    yield return action;
                }
            }

            var sellerCargo = entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
            for (var bayIndex = 0; bayIndex < sellerCargo.Count; bayIndex++)
            {
                foreach (var slot in sellerCargo[bayIndex]?.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
                {
                    var itemKey = slot?.Item?.ItemKey ?? "";
                    var typedItem = catalog.FindItem(itemKey);
                    if (typedItem == null || slot?.Item == null || slot.Item.Quantity <= 0)
                        continue;

                    var value = AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
                        typedItem,
                        slot.Item,
                        catalog.TradeValueSettings).Price;
                    var stationCanReceive = Enumerable.Range(0, parent.CargoBays?.Count ?? 0)
                        .Any(stationBayIndex =>
                            stationBayIndex < (parent.CargoContents?.Count ?? 0) &&
                            AetheriaRuntimeCargoCapacityQueries.UnitsThatFit(
                                parent,
                                catalog,
                                itemKey,
                                stationBayIndex) > 0);
                    var action = Action(
                        $"trade.sell.{StableToken(itemKey)}.{bayIndex}.{slot.X}.{slot.Y}",
                        $"Sell {typedItem.Name} (+{value})",
                        "TradeSale",
                        "trade",
                        $"{run.CurrentEntityKey}#cargo/{bayIndex}/{slot.X}/{slot.Y}",
                        ("itemKey", itemKey),
                        ("quantity", "1"),
                        ("sourceCargoIndex", bayIndex.ToString()),
                        ("sourceX", slot.X.ToString()),
                        ("sourceY", slot.Y.ToString()));
                    action.Availability = value >= 0 && stationCanReceive
                        ? "available"
                        : "unavailable";
                    yield return action;
                }
            }
        }

        private static string StableToken(string value) =>
            new string((value ?? "").Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-').ToArray()).Trim('-');

        private static IEnumerable<AetheriaRuntimeInputActionDocument> CoreActions()
        {
            var scoop = Action("pilot.scoop", "Scoop", "SetTractorPower", "ship", "pilot", ("scalarValue", "0"));
            scoop.InputValue = new AetheriaRuntimeInputValueDocument
            {
                Model = "button-hold.v1",
                PayloadKey = "scalarValue"
            };
            yield return scoop;
            yield return Action("pilot.dock", "Dock", "DockNearest", "ship", "pilot");
            yield return Action("pilot.undock", "Undock", "Undock", "ship", "pilot");
            yield return Action("pilot.target-nearest", "Target Nearest", "TargetNearest", "targeting", "pilot");
        }

        private static AetheriaRuntimeInputActionDocument Action(string id, string label, string operation, string category, string source, params (string Key, string Value)[] payload) =>
            new AetheriaRuntimeInputActionDocument { ActionId = id, Label = label, Operation = "aetheria.daemon.commands." + operation, Category = category, SourceRef = source, Payload = payload.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal) };

        private static AetheriaRuntimeInputProfileDocument[] BuildDefaultProfiles(IReadOnlyCollection<AetheriaRuntimeInputActionDocument> actions)
        {
            return new[]
            {
                Profile("keyboard-mouse", "keyboard-mouse", DefaultBindings(actions, "mouse.primary", "gamepad.rightTrigger", keyboard: true)),
                Profile("gamepad", "gamepad", DefaultBindings(actions, "mouse.primary", "gamepad.rightTrigger", keyboard: false))
            };
        }

        private static AetheriaRuntimeInputBindingDocument[] DefaultBindings(
            IReadOnlyCollection<AetheriaRuntimeInputActionDocument> actions,
            string keyboardFireControl,
            string gamepadFireControl,
            bool keyboard)
        {
            var bindings = new List<AetheriaRuntimeInputBindingDocument>();
            var fire = actions.FirstOrDefault(action => string.Equals(action.Category, "weapon-group", StringComparison.Ordinal));
            if (fire != null)
                bindings.Add(Binding("fire.default", fire.ActionId, "direct", keyboard ? keyboardFireControl : gamepadFireControl));
            bindings.Add(keyboard
                ? Binding("scoop.shift", "pilot.scoop", "direct", "keyboard.leftShift")
                : Binding("scoop.sequence", "pilot.scoop", "sequence", "gamepad.dpad.down", "gamepad.dpad.right"));
            bindings.Add(keyboard
                ? Binding("dock.r", "pilot.dock", "direct", "keyboard.r")
                : Binding("dock.sequence", "pilot.dock", "sequence", "gamepad.dpad.down", "gamepad.dpad.up"));
            if (actions.Any(action => string.Equals(action.ActionId, "simulation.pause", StringComparison.Ordinal)))
                bindings.Add(keyboard
                    ? Binding("simulation.pause", "simulation.pause", "direct", "keyboard.pause")
                    : Binding("simulation.pause.sequence", "simulation.pause", "sequence", "gamepad.dpad.left", "gamepad.dpad.left"));
            return bindings.ToArray();
        }

        public EveInputCapabilityDocument ToEveDocument() => new EveInputCapabilityDocument
        {
            ProviderId = ProviderId,
            CapabilityId = CapabilityId,
            Version = Version,
            Actions = Actions.Select(action => new EveInputActionDocument
            {
                ActionId = action.ActionId,
                Label = action.Label,
                Operation = action.Operation,
                Context = action.Context,
                Category = action.Category,
                Availability = action.Availability,
                SourceRef = action.SourceRef,
                Payload = new Dictionary<string, string>(action.Payload, StringComparer.Ordinal),
                InputValue = action.InputValue == null
                    ? null
                    : new EveInputValueDocument
                    {
                        Model = action.InputValue.Model,
                        PayloadKey = action.InputValue.PayloadKey
                    }
            }).ToArray(),
            DefaultProfiles = DefaultProfiles.Select(profile => new EveInputProfileDocument
            {
                ProfileId = profile.ProfileId,
                DeviceClass = profile.DeviceClass,
                Bindings = profile.Bindings.Select(binding => new EveInputBindingDocument
                {
                    BindingId = binding.BindingId,
                    ActionId = binding.ActionId,
                    ActionBar = binding.ActionBar,
                    Gesture = new EveInputGestureDocument
                    {
                        Kind = binding.Gesture.Kind,
                        Controls = binding.Gesture.Controls,
                        MaxStepIntervalMs = binding.Gesture.MaxStepIntervalMs,
                        CompletionControl = binding.Gesture.CompletionControl
                    }
                }).ToArray()
            }).ToArray()
        };

        private static AetheriaRuntimeInputProfileDocument Profile(string id, string device, params AetheriaRuntimeInputBindingDocument[] bindings) => new AetheriaRuntimeInputProfileDocument { ProfileId = id, DeviceClass = device, Bindings = bindings };
        private static AetheriaRuntimeInputBindingDocument Binding(string id, string action, string kind, params string[] controls) => new AetheriaRuntimeInputBindingDocument { BindingId = id, ActionId = action, Gesture = new AetheriaRuntimeInputGestureDocument { Kind = kind, Controls = controls }, ActionBar = true };
    }
}
