using System;
using System.Collections.Generic;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public sealed class AetheriaRuntimeEveCommandEnvelope
    {
        public AetheriaRuntimeEveCommandEnvelope(
            string schema,
            string commandId,
            string providerId,
            string surfaceId,
            string command,
            string issuedAtUtc,
            string clientId,
            IReadOnlyDictionary<string, string> payload,
            string path)
        {
            Schema = schema;
            CommandId = commandId;
            ProviderId = providerId;
            SurfaceId = surfaceId;
            Command = command;
            IssuedAtUtc = issuedAtUtc;
            ClientId = clientId;
            Payload = payload;
            Path = path;
        }

        public string Schema { get; }
        public string CommandId { get; }
        public string ProviderId { get; }
        public string SurfaceId { get; }
        public string Command { get; }
        public string IssuedAtUtc { get; }
        public string ClientId { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }
        public string Path { get; }
    }

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
        public Dictionary<string, string> Payload { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
