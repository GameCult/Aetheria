using System;
using System.Collections.Generic;
using GameCult.Caching;
using GameCult.Mesh;
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
        CycleVerseHostVisibility = 18,
        TradeValuePolicyRefresh = 19,
        SetTradeValueQualityMinimum = 20,
        SetTradeValueQualityMaximum = 21,
        SetTradeValueQualityExponent = 22,
        SetTradeValueTierQuality = 23
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
            AetheriaRuntimeLoadoutTemplateCommit? loadoutTemplate = null,
            AetheriaRuntimeTradeValuePolicyCommandBody? tradeValuePolicy = null,
            CultMeshOperationReceipt? receipt = null,
            CultMeshOperationInvocationDescriptor? invocation = null,
            CultMeshOperationPayload? payload = null)
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
            TradeValuePolicy = tradeValuePolicy ?? new AetheriaRuntimeTradeValuePolicyCommandBody();
            Receipt = receipt ?? AetheriaRuntimeEveOperationIds.CreateReceipt(kind);
            Invocation = invocation ?? CultMesh.OperationInvocation(
                string.IsNullOrWhiteSpace(command) ? AetheriaRuntimeEveOperationIds.ForKind(kind) : command,
                schema,
                Receipt.Route,
                string.IsNullOrWhiteSpace(commandId) ? null : commandId);
            Payload = payload ?? CultMesh.OperationPayload();
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
        public AetheriaRuntimeTradeValuePolicyCommandBody TradeValuePolicy { get; }
        public string Path { get; }
        public CultMeshOperationReceipt Receipt { get; }
        public CultMeshOperationInvocationDescriptor Invocation { get; }
        public CultMeshOperationPayload Payload { get; }
        public string OperationId => Receipt.OperationId;
        public bool Accepted => Receipt.Accepted;
        public CultMeshRouteHint Route => Receipt.Route;
        public string? Diagnostic => Receipt.Diagnostic;

        public static implicit operator CultMeshOperationReceipt(AetheriaRuntimeEveCommandEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            return envelope.Receipt;
        }
    }

    public static class AetheriaRuntimeEveOperationIds
    {
        public static string ForKind(AetheriaRuntimeEveCommandKind kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeEveCommandKind.Unknown:
                    return "gamecult.aetheria.eve.unknown.v1";
                default:
                    return "gamecult.aetheria.eve." + ToSnakeCase(kind.ToString()) + ".v1";
            }
        }

        public static CultMeshOperationReceipt CreateReceipt(
            AetheriaRuntimeEveCommandKind kind,
            bool accepted = true,
            CultMeshRouteHint? route = null,
            string? diagnostic = null)
        {
            return new CultMeshOperationReceipt(
                ForKind(kind),
                accepted,
                route ?? new CultMeshRouteHint(CultMeshLocalityKind.Network, "aetheria-eve-command"),
                diagnostic);
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var chars = new System.Collections.Generic.List<char>(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                        chars.Add('_');
                    chars.Add(char.ToLowerInvariant(c));
                }
                else
                {
                    chars.Add(c);
                }
            }

            return new string(chars.ToArray());
        }
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

        [Key(10)]
        public AetheriaRuntimeTradeValuePolicyCommandBody TradeValuePolicy { get; set; } = new AetheriaRuntimeTradeValuePolicyCommandBody();

        [Key(11)]
        public string OperationId { get; set; } = "";

        [Key(12)]
        public string OperationSchemaId { get; set; } = "";

        [Key(13)]
        public string OperationRouteKind { get; set; } = "";

        [Key(14)]
        public string OperationRouteDescription { get; set; } = "";

        [Key(15)]
        public string OperationIdempotencyKey { get; set; } = "";

        [Key(16)]
        public Dictionary<string, string> Payload { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        [Key(17)]
        public CultMeshOperationInvocationRecord Operation { get; set; } = new CultMeshOperationInvocationRecord();

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

    [MessagePackObject]
    public sealed class AetheriaRuntimeTradeValuePolicyCommandBody
    {
        [Key(0)]
        public double Value { get; set; }

        [Key(1)]
        public int TierIndex { get; set; } = -1;
    }
}
