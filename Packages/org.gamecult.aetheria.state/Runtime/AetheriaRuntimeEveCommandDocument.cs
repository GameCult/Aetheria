using System;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public enum AetheriaRuntimeEveCommandKind
    {
        Unknown = 0,
        CatalogRefresh = 1,
        OperationsRefresh = 2,
        PlayerSettingsRefresh = 3,
        SetPlayerName = 4,
        CycleTemperatureUnit = 5,
        DecrementSignificantDigits = 6,
        IncrementSignificantDigits = 7,
        CycleNebulaQuality = 8,
        ToggleShowAsteroidsInMinimap = 9,
        InputSettingsRefresh = 10,
        BeginInputCapture = 11,
        CancelInputCapture = 12,
        SetBindingOverride = 13,
        ToggleActionBar = 14,
        SetActionBarEnabled = 15,
        SaveLoadoutTemplate = 16,
        VerseHostRefresh = 17,
        CycleVerseHostVisibility = 18
    }

    public sealed class AetheriaRuntimeEveCommandEnvelope
    {
        public AetheriaRuntimeEveCommandEnvelope(
            string schema,
            string commandId,
            string providerId,
            string surfaceId,
            string command,
            AetheriaRuntimeEveCommandKind kind,
            string issuedAtUtc,
            string clientId,
            AetheriaRuntimePlayerSettingsCommandBody playerSettings,
            AetheriaRuntimeInputSettingsCommandBody inputSettings,
            string path,
            AetheriaRuntimeLoadoutTemplateCommit? loadoutTemplate = null)
        {
            Schema = schema;
            CommandId = commandId;
            ProviderId = providerId;
            SurfaceId = surfaceId;
            Command = command;
            Kind = kind;
            IssuedAtUtc = issuedAtUtc;
            ClientId = clientId;
            PlayerSettings = playerSettings ?? new AetheriaRuntimePlayerSettingsCommandBody();
            InputSettings = inputSettings ?? new AetheriaRuntimeInputSettingsCommandBody();
            Path = path;
            LoadoutTemplate = loadoutTemplate;
        }

        public string Schema { get; }
        public string CommandId { get; }
        public string ProviderId { get; }
        public string SurfaceId { get; }
        public string Command { get; }
        public AetheriaRuntimeEveCommandKind Kind { get; }
        public string IssuedAtUtc { get; }
        public string ClientId { get; }
        public AetheriaRuntimePlayerSettingsCommandBody PlayerSettings { get; }
        public AetheriaRuntimeInputSettingsCommandBody InputSettings { get; }
        public AetheriaRuntimeLoadoutTemplateCommit? LoadoutTemplate { get; }
        public string Path { get; }
    }

    [CultDocument("gamecult.eve.command", "gamecult.eve.command.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeEveCommandDocument
    {
        public const string SchemaId = "gamecult.eve.command.v1";

        [Key(0)]
        public string Schema { get; set; } = SchemaId;

        [Key(1)]
        public string CommandId { get; set; } = "";

        [Key(2)]
        public string ProviderId { get; set; } = "";

        [Key(3)]
        public string SurfaceId { get; set; } = "";

        [Key(4)]
        public string Command { get; set; } = "";

        [Key(5)]
        public string IssuedAtUtc { get; set; } = "";

        [Key(6)]
        public string ClientId { get; set; } = "";

        [Key(7)]
        public AetheriaRuntimePlayerSettingsCommandBody PlayerSettings { get; set; } = new AetheriaRuntimePlayerSettingsCommandBody();

        [Key(8)]
        public AetheriaRuntimeLoadoutTemplateCommit? LoadoutTemplate { get; set; }

        [Key(9)]
        public AetheriaRuntimeInputSettingsCommandBody InputSettings { get; set; } = new AetheriaRuntimeInputSettingsCommandBody();

        [IgnoreMember]
        public AetheriaRuntimeEveCommandKind Kind { get; set; }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimePlayerSettingsCommandBody
    {
        [Key(0)]
        public string PlayerName { get; set; } = "";
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeInputSettingsCommandBody
    {
        [Key(0)]
        public string ActionName { get; set; } = "";

        [Key(1)]
        public int BindingIndex { get; set; } = -1;

        [Key(2)]
        public string InputSystemPath { get; set; } = "";

        [Key(3)]
        public bool Enabled { get; set; }
    }
}
