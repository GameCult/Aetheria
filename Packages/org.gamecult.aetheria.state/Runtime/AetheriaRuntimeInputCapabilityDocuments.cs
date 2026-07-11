using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
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
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputProfileDocument
    {
        [Key(0)] public string ProfileId { get; set; } = "";
        [Key(1)] public string DeviceClass { get; set; } = "";
        [Key(2)] public AetheriaRuntimeInputBindingDocument[] Bindings { get; set; } = Array.Empty<AetheriaRuntimeInputBindingDocument>();
    }

    [CultDocument("gamecult.eve.input_capability", "gamecult.eve.input_capability.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeInputCapabilityDocument
    {
        public const string SchemaId = "gamecult.eve.input_capability.v1";
        [Key(0)] public string Schema { get; set; } = SchemaId;
        [Key(1)] public string ProviderId { get; set; } = "aetheria.daemon";
        [Key(2)] public string CapabilityId { get; set; } = "aetheria.pilot.input";
        [Key(3)] public long Version { get; set; }
        [Key(4)] public AetheriaRuntimeInputActionDocument[] Actions { get; set; } = Array.Empty<AetheriaRuntimeInputActionDocument>();
        [Key(5)] public AetheriaRuntimeInputProfileDocument[] DefaultProfiles { get; set; } = Array.Empty<AetheriaRuntimeInputProfileDocument>();

        public static AetheriaRuntimeInputCapabilityDocument FromFrame(AetheriaRuntimeDaemonFrameDocument frame)
        {
            var run = frame?.Run ?? new AetheriaRuntimeRunCheckpointCommit();
            var entity = run.Zones.SelectMany(zone => zone.Entities).FirstOrDefault(candidate =>
                string.Equals(run.EntityRecordKey(run.CurrentZoneIndex, candidate.EntityIndex), run.CurrentEntityKey, StringComparison.Ordinal));
            var actions = CoreActions().ToList();
            if (entity != null)
            {
                actions.AddRange((entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>()).Select((_, index) => Action($"weapon-group.{index}.fire", $"Fire Weapon Group {index + 1}", "FireWeaponGroup", "weapon-group", $"{run.CurrentEntityKey}#weapon-group/{index}")));
                actions.AddRange((entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).Where(slot => slot?.Item != null).Select((slot, index) => Action($"equipment.{index}.activate", $"Activate {slot.Item.ItemKey}", "SetBehaviorActive", "equipment", $"{run.CurrentEntityKey}#equipment/{index}")));
                actions.AddRange((entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).SelectMany(bay => bay.Items).Where(slot => slot?.Item != null).Select((slot, index) => Action($"cargo.{slot.Item.ItemKey}.{index}.use", $"Use {slot.Item.ItemKey}", "ActivateConsumable", "consumable", $"{run.CurrentEntityKey}#cargo/{index}")));
            }
            return new AetheriaRuntimeInputCapabilityDocument { Version = frame?.FrameId ?? 0, Actions = actions.ToArray(), DefaultProfiles = BuildDefaultProfiles() };
        }

        private static IEnumerable<AetheriaRuntimeInputActionDocument> CoreActions()
        {
            yield return Action("pilot.fire", "Fire", "FireWeaponGroup", "combat", "pilot");
            yield return Action("pilot.scoop", "Scoop", "SetTractorPower", "ship", "pilot");
            yield return Action("pilot.dock", "Dock", "DockNearest", "ship", "pilot");
            yield return Action("pilot.undock", "Undock", "Undock", "ship", "pilot");
            yield return Action("pilot.target-nearest", "Target Nearest", "TargetNearest", "targeting", "pilot");
        }

        private static AetheriaRuntimeInputActionDocument Action(string id, string label, string operation, string category, string source) =>
            new AetheriaRuntimeInputActionDocument { ActionId = id, Label = label, Operation = "aetheria.daemon.commands." + operation, Category = category, SourceRef = source };

        private static AetheriaRuntimeInputProfileDocument[] BuildDefaultProfiles() => new[]
        {
            Profile("keyboard-mouse", "keyboard-mouse", Binding("fire.mouse", "pilot.fire", "direct", "mouse.primary"), Binding("scoop.shift", "pilot.scoop", "direct", "keyboard.leftShift"), Binding("dock.r", "pilot.dock", "direct", "keyboard.r")),
            Profile("gamepad", "gamepad", Binding("fire.trigger", "pilot.fire", "direct", "gamepad.rightTrigger"), Binding("scoop.sequence", "pilot.scoop", "sequence", "gamepad.dpad.down", "gamepad.dpad.right"), Binding("dock.sequence", "pilot.dock", "sequence", "gamepad.dpad.down", "gamepad.dpad.up"))
        };

        private static AetheriaRuntimeInputProfileDocument Profile(string id, string device, params AetheriaRuntimeInputBindingDocument[] bindings) => new AetheriaRuntimeInputProfileDocument { ProfileId = id, DeviceClass = device, Bindings = bindings };
        private static AetheriaRuntimeInputBindingDocument Binding(string id, string action, string kind, params string[] controls) => new AetheriaRuntimeInputBindingDocument { BindingId = id, ActionId = action, Gesture = new AetheriaRuntimeInputGestureDocument { Kind = kind, Controls = controls }, ActionBar = true };
    }
}
