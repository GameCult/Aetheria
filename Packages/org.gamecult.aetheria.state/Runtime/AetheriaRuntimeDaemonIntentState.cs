using System;
using System.Collections.Generic;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonIntentState
    {
        public List<AetheriaRuntimeDaemonMovementIntent> Movements { get; } =
            new List<AetheriaRuntimeDaemonMovementIntent>();
        public List<AetheriaRuntimeDaemonWeaponGroupIntent> WeaponGroups { get; } =
            new List<AetheriaRuntimeDaemonWeaponGroupIntent>();
        public List<AetheriaRuntimeDaemonBehaviorIntent> Behaviors { get; } =
            new List<AetheriaRuntimeDaemonBehaviorIntent>();
        public List<AetheriaRuntimeDaemonConsumableIntent> Consumables { get; } =
            new List<AetheriaRuntimeDaemonConsumableIntent>();
        public List<AetheriaRuntimeDaemonDockingIntent> Docking { get; } =
            new List<AetheriaRuntimeDaemonDockingIntent>();
        public List<AetheriaRuntimeDaemonWormholeIntent> Wormholes { get; } =
            new List<AetheriaRuntimeDaemonWormholeIntent>();
        public bool SensorPingRequested { get; set; }

        public bool HasAny =>
            Movements.Count > 0 ||
            SensorPingRequested ||
            WeaponGroups.Count > 0 ||
            Behaviors.Count > 0 ||
            Consumables.Count > 0 ||
            Docking.Count > 0 ||
            Wormholes.Count > 0;
    }

    public sealed class AetheriaRuntimeDaemonMovementIntent
    {
        public string ActorEntityKey { get; set; } = "";
        public double DirectionX { get; set; }
        public double DirectionY { get; set; }
        public double Magnitude { get; set; }
    }

    public sealed class AetheriaRuntimeDaemonWeaponGroupIntent
    {
        public string ActorEntityKey { get; set; } = "";
        public int WeaponGroup { get; set; } = -1;
        public bool Fire { get; set; }
        public bool Active { get; set; }
    }

    public sealed class AetheriaRuntimeDaemonBehaviorIntent
    {
        public string ActorEntityKey { get; set; } = "";
        public int EquipmentIndex { get; set; } = -1;
        public int BehaviorIndex { get; set; } = -1;
        public bool Active { get; set; }
        public string TargetBodyKey { get; set; } = "";
        public int TargetAsteroidIndex { get; set; } = -1;
    }

    public sealed class AetheriaRuntimeDaemonConsumableIntent
    {
        public string ActorEntityKey { get; set; } = "";
        public string ItemKey { get; set; } = "";
    }

    public sealed class AetheriaRuntimeDaemonDockingIntent
    {
        public string ActorEntityKey { get; set; } = "";
        public string TargetEntityKey { get; set; } = "";
        public bool Dock { get; set; }
        public bool Undock { get; set; }
    }

    public sealed class AetheriaRuntimeDaemonWormholeIntent
    {
        public string ActorEntityKey { get; set; } = "";
        public int TargetZoneIndex { get; set; } = -1;
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }

}
