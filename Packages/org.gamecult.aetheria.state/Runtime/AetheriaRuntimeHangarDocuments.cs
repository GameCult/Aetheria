using System;
using System.Linq;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaGameModes
    {
        public const string Terminus = "terminus";
        public const string Starbridge = "starbridge";
        public const string Arena = "arena";

        public static bool IsKnown(string? mode) =>
            string.Equals(mode, Terminus, StringComparison.Ordinal) ||
            string.Equals(mode, Starbridge, StringComparison.Ordinal) ||
            string.Equals(mode, Arena, StringComparison.Ordinal);
    }

    public static class AetheriaModePolicies
    {
        public const string TerminusLocal = "aetheria.mode.terminus.local.v1";
        public const string StarbridgeMixed = "aetheria.mode.starbridge.mixed.v1";
        public const string ArenaServer = "aetheria.mode.arena.server.v1";

        public static string ForMode(string? mode)
        {
            if (string.Equals(mode, AetheriaGameModes.Terminus, StringComparison.Ordinal)) return TerminusLocal;
            if (string.Equals(mode, AetheriaGameModes.Starbridge, StringComparison.Ordinal)) return StarbridgeMixed;
            if (string.Equals(mode, AetheriaGameModes.Arena, StringComparison.Ordinal)) return ArenaServer;
            return "";
        }
    }

    public static class AetheriaHangarShipStatuses
    {
        public const string Available = "available";
        public const string Deployed = "deployed";
        public const string Lost = "lost";
    }

    [CultDocument("gamecult.aetheria.hangar", "gamecult.aetheria.hangar.v1")]
    [CultGlobal]
    [MessagePackObject]
    public sealed class AetheriaHangarState
    {
        [Key(0), CultName] public string Name { get; set; } = "Aetheria Hangar";
        [Key(1)] public string HangarId { get; set; } = "local";
        [Key(2)] public string PlayerKey { get; set; } = "";
        [Key(3)] public long Revision { get; set; }
        [Key(4)] public AetheriaHangarShip[] Ships { get; set; } = Array.Empty<AetheriaHangarShip>();
        [Key(5)] public AetheriaHangarItemStack[] Inventory { get; set; } = Array.Empty<AetheriaHangarItemStack>();
        [Key(6)] public AetheriaHangarCurrency[] Currencies { get; set; } = Array.Empty<AetheriaHangarCurrency>();
        [Key(7)] public string[] UnlockKeys { get; set; } = Array.Empty<string>();
        [Key(8)] public string[] LoadoutTemplateKeys { get; set; } = Array.Empty<string>();
        [Key(9)] public AetheriaDeploymentReceipt[] Deployments { get; set; } = Array.Empty<AetheriaDeploymentReceipt>();
        [Key(10)] public string UpdatedAtUtc { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaHangarShip
    {
        [Key(0)] public string ShipId { get; set; } = "";
        [Key(1)] public string HullItemKey { get; set; } = "";
        [Key(2)] public string LoadoutTemplateKey { get; set; } = "";
        [Key(3)] public string Status { get; set; } = AetheriaHangarShipStatuses.Available;
        [Key(4)] public string ActiveDeploymentId { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaHangarItemStack
    {
        [Key(0)] public string ItemKey { get; set; } = "";
        [Key(1)] public long Quantity { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaHangarCurrency
    {
        [Key(0)] public string CurrencyKey { get; set; } = "";
        [Key(1)] public long Quantity { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaDeploymentRequest
    {
        [Key(0)] public string RequestId { get; set; } = "";
        [Key(1)] public string PlayerKey { get; set; } = "";
        [Key(2)] public string Mode { get; set; } = AetheriaGameModes.Terminus;
        [Key(3)] public string ShipId { get; set; } = "";
        [Key(4)] public string LoadoutTemplateKey { get; set; } = "";
        [Key(5)] public long ExpectedHangarRevision { get; set; }
        [Key(6)] public string ModePolicyId { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaDeploymentReceipt
    {
        [Key(0)] public string DeploymentId { get; set; } = "";
        [Key(1)] public string RequestId { get; set; } = "";
        [Key(2)] public bool Accepted { get; set; }
        [Key(3)] public string Diagnostic { get; set; } = "";
        [Key(4)] public string PlayerKey { get; set; } = "";
        [Key(5)] public string Mode { get; set; } = "";
        [Key(6)] public string ShipId { get; set; } = "";
        [Key(7)] public string LoadoutTemplateKey { get; set; } = "";
        [Key(8)] public long HangarRevision { get; set; }
        [Key(9)] public string ModePolicyId { get; set; } = "";
        [Key(10)] public AetheriaRuntimeEntityLoadoutCommit Loadout { get; set; } = new AetheriaRuntimeEntityLoadoutCommit();
        [Key(11)] public string CommittedAtUtc { get; set; } = "";
    }
}
