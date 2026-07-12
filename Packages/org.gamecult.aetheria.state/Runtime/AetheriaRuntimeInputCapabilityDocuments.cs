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
                actions.AddRange((entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>()).Select((_, index) => Action($"weapon-group.{index}.fire", $"Fire Weapon Group {index + 1}", "FireWeaponGroup", "weapon-group", $"{run.CurrentEntityKey}#weapon-group/{index}", ("weaponGroup", index.ToString()))));
                actions.AddRange((entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()).Where(slot => slot?.Item != null).Select((slot, index) => Action($"equipment.{index}.activate", $"Activate {slot.Item.ItemKey}", "SetBehaviorActive", "equipment", $"{run.CurrentEntityKey}#equipment/{index}", ("equipmentIndex", index.ToString()), ("behaviorIndex", "0"), ("active", "true"))));
                actions.AddRange((entity.CargoContents ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>()).SelectMany(bay => bay.Items).Where(slot => slot?.Item != null).Select((slot, index) => Action($"cargo.{slot.Item.ItemKey}.{index}.use", $"Use {slot.Item.ItemKey}", "ActivateConsumable", "consumable", $"{run.CurrentEntityKey}#cargo/{index}", ("itemKey", slot.Item.ItemKey))));
            }
            return new AetheriaRuntimeInputCapabilityDocument { Version = frame?.FrameId ?? 0, Actions = actions.ToArray(), DefaultProfiles = BuildDefaultProfiles(actions) };
        }

        private static IEnumerable<AetheriaRuntimeInputActionDocument> CoreActions()
        {
            yield return Action("pilot.scoop", "Scoop", "SetTractorPower", "ship", "pilot", ("scalarValue", "1"));
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
                Payload = new Dictionary<string, string>(action.Payload, StringComparer.Ordinal)
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
