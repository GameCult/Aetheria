using System;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeStarbridgePlayerSeatConnectionStates
    {
        public const string Connected = "connected";
        public const string Grace = "grace";
        public const string Disconnected = "disconnected";
        public const string Abandoned = "abandoned";
        public const string Replaced = "replaced";
    }

    public static class AetheriaRuntimeStarbridgePlayerSeatRoles
    {
        public const string Commander = "commander";
        public const string Pilot = "pilot";
        public const string Support = "support";
        public const string Observer = "observer";
    }

    [CultDocument("gamecult.aetheria.starbridge_player_seat", "gamecult.aetheria.starbridge_player_seat.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeStarbridgePlayerSeatDocument
    {
        [Key(0)] public string Schema { get; set; } = AetheriaRuntimeDaemonSchemas.StarbridgePlayerSeat;
        [Key(1)] public string SeatId { get; set; } = "";
        [Key(2)] public string PlayerId { get; set; } = "";
        [Key(3)] public string PlayerDisplayName { get; set; } = "";
        [Key(4)] public string SessionId { get; set; } = "";
        [Key(5)] public string ScenarioId { get; set; } = "";
        [Key(6)] public string RunId { get; set; } = "";
        [Key(7)] public string Role { get; set; } = AetheriaRuntimeStarbridgePlayerSeatRoles.Pilot;
        [Key(8)] public string RuntimeId { get; set; } = "";
        [Key(9)] public string[] PreviousRuntimeIds { get; set; } = Array.Empty<string>();
        [Key(10)] public string ControlledEntityKey { get; set; } = "";
        [Key(11)] public string ShipEntityKey { get; set; } = "";
        [Key(12)] public string CockpitItemKey { get; set; } = "";
        [Key(13)] public string EscapePodEntityKey { get; set; } = "";
        [Key(14)] public string LoadoutDocumentKey { get; set; } = "";
        [Key(15)] public string AuthorityLeaseId { get; set; } = "";
        [Key(16)] public string[] ClaimKinds { get; set; } = Array.Empty<string>();
        [Key(17)] public string ConnectionState { get; set; } =
            AetheriaRuntimeStarbridgePlayerSeatConnectionStates.Disconnected;
        [Key(18)] public string ConnectedAtUtc { get; set; } = "";
        [Key(19)] public string LastSeenAtUtc { get; set; } = "";
        [Key(20)] public string DisconnectedAtUtc { get; set; } = "";
        [Key(21)] public string ResumeTokenHash { get; set; } = "";
        [Key(22)] public string UpdatedAtUtc { get; set; } = "";

        public static string RecordKey(string seatId)
        {
            return $"starbridge:player-seats:{AetheriaRuntimeVerseRecordKeys.StableToken(seatId)}:v1";
        }

        public static AetheriaRuntimeStarbridgePlayerSeatDocument Create(
            string seatId,
            string playerId,
            string sessionId,
            string scenarioId,
            string runId,
            string role,
            string runtimeId,
            DateTimeOffset now)
        {
            var stamp = now.ToString("O");
            return new AetheriaRuntimeStarbridgePlayerSeatDocument
            {
                SeatId = seatId ?? "",
                PlayerId = playerId ?? "",
                SessionId = sessionId ?? "",
                ScenarioId = scenarioId ?? "",
                RunId = runId ?? "",
                Role = string.IsNullOrWhiteSpace(role)
                    ? AetheriaRuntimeStarbridgePlayerSeatRoles.Pilot
                    : role,
                RuntimeId = runtimeId ?? "",
                ConnectionState = AetheriaRuntimeStarbridgePlayerSeatConnectionStates.Connected,
                ConnectedAtUtc = stamp,
                LastSeenAtUtc = stamp,
                UpdatedAtUtc = stamp
            };
        }

        public AetheriaRuntimeStarbridgePlayerSeatDocument AttachRuntime(
            string runtimeId,
            DateTimeOffset now)
        {
            var previousRuntimeId = RuntimeId ?? "";
            if (!string.IsNullOrWhiteSpace(previousRuntimeId) &&
                !string.Equals(previousRuntimeId, runtimeId, StringComparison.Ordinal))
            {
                PreviousRuntimeIds = AppendDistinct(PreviousRuntimeIds, previousRuntimeId);
            }

            RuntimeId = runtimeId ?? "";
            ConnectionState = AetheriaRuntimeStarbridgePlayerSeatConnectionStates.Connected;
            ConnectedAtUtc = now.ToString("O");
            LastSeenAtUtc = ConnectedAtUtc;
            DisconnectedAtUtc = "";
            UpdatedAtUtc = ConnectedAtUtc;
            return this;
        }

        public AetheriaRuntimeStarbridgePlayerSeatDocument MarkSeen(DateTimeOffset now)
        {
            LastSeenAtUtc = now.ToString("O");
            UpdatedAtUtc = LastSeenAtUtc;
            return this;
        }

        public AetheriaRuntimeStarbridgePlayerSeatDocument MarkDisconnected(DateTimeOffset now)
        {
            DisconnectedAtUtc = now.ToString("O");
            LastSeenAtUtc = DisconnectedAtUtc;
            UpdatedAtUtc = DisconnectedAtUtc;
            ConnectionState = AetheriaRuntimeStarbridgePlayerSeatConnectionStates.Grace;
            return this;
        }

        public bool IsResumeGraceActive(DateTimeOffset now, TimeSpan gracePeriod)
        {
            if (!string.Equals(
                ConnectionState,
                AetheriaRuntimeStarbridgePlayerSeatConnectionStates.Grace,
                StringComparison.Ordinal))
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(DisconnectedAtUtc, out var disconnectedAt))
                return false;

            return now - disconnectedAt <= gracePeriod;
        }

        private static string[] AppendDistinct(string[]? values, string value)
        {
            values ??= Array.Empty<string>();
            foreach (var candidate in values)
            {
                if (string.Equals(candidate, value, StringComparison.Ordinal))
                    return values;
            }

            var next = new string[values.Length + 1];
            Array.Copy(values, next, values.Length);
            next[next.Length - 1] = value;
            return next;
        }
    }
}
