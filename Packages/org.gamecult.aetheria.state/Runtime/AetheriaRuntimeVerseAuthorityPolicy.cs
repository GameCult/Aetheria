using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVerseAuthoritySchemas
    {
        public const string Policy = "gamecult.aetheria.verse_authority_policy.v1";
        public const string Lease = "gamecult.aetheria.authority_lease.v1";
    }

    public static class AetheriaRuntimeAuthorityModes
    {
        public const string AnyTrustedRuntime = "any-trusted-runtime";
        public const string HostAuthoritative = "host-authoritative";
        public const string DelegatedRuntime = "delegated-runtime";
        public const string OwningRuntime = "owning-runtime";
        public const string InterestLease = "interest-lease";
        public const string WitnessQuorum = "witness-quorum";
        public const string OperatorFinality = "operator-finality";
        public const string MergeableCrdt = "mergeable-crdt";
    }

    public static class AetheriaRuntimeVerseDeploymentModes
    {
        public const string DedicatedDaemon = "dedicated-daemon";
        public const string ElectronLaunchedDaemon = "electron-launched-daemon";
        public const string UnityHost = "unity-host";
        public const string BrowserWasmHost = "browser-wasm-host";
        public const string NativeKernelHost = "native-kernel-host";
        public const string DistributedTrusted = "distributed-trusted";
        public const string ObserverOnly = "observer-only";
    }

    public static class AetheriaRuntimeVerseRuntimeRoles
    {
        public const string SimulationHost = "simulation-host";
        public const string UnityPlayer = "unity-player";
        public const string StarbridgeCommander = "starbridge-commander";
        public const string BrowserObserver = "browser-observer";
        public const string BrowserSimulationHost = "browser-simulation-host";
        public const string NativeSimulationKernel = "native-simulation-kernel";
        public const string DedicatedDaemon = "dedicated-daemon";
    }

    public static class AetheriaRuntimeClaimKinds
    {
        public const string Any = "*";
        public const string Movement = "movement";
        public const string Targeting = "targeting";
        public const string Combat = "combat";
        public const string Inventory = "inventory";
        public const string Economy = "economy";
        public const string Ai = "ai";
        public const string Metadata = "metadata";
        public const string Interaction = "interaction";
        public const string System = "system";
    }

    [CultDocument("gamecult.aetheria.verse_authority_policy", "gamecult.aetheria.verse_authority_policy.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeVerseAuthorityPolicyDocument
    {
        public const string DocumentKey = "global:aetheria.verse_authority_policy.v1";

        [Key(0)] public string Schema { get; set; } = AetheriaRuntimeVerseAuthoritySchemas.Policy;
        [Key(1)] public string VerseId { get; set; } = "aetheria.local";
        [Key(2)] public string PolicyId { get; set; } = "aetheria.trusted-coop.v1";
        [Key(3)] public string RuleVersion { get; set; } = "1";
        [Key(4)] public string HostRuntimeId { get; set; } = "aetheria-daemon";
        [Key(5)] public string DefaultMode { get; set; } = AetheriaRuntimeAuthorityModes.AnyTrustedRuntime;
        [Key(6)] public AetheriaRuntimeAuthorityRule[] Rules { get; set; } = Array.Empty<AetheriaRuntimeAuthorityRule>();
        [Key(7)] public string UpdatedAtUtc { get; set; } = "";
        [Key(8)] public string DeploymentMode { get; set; } = AetheriaRuntimeVerseDeploymentModes.DedicatedDaemon;
        [Key(9)] public AetheriaRuntimeAuthorityRuntimeRole[] RuntimeRoles { get; set; } = Array.Empty<AetheriaRuntimeAuthorityRuntimeRole>();

        public static AetheriaRuntimeVerseAuthorityPolicyDocument TrustedCoop(
            string verseId,
            string hostRuntimeId)
        {
            return new AetheriaRuntimeVerseAuthorityPolicyDocument
            {
                VerseId = string.IsNullOrWhiteSpace(verseId) ? "aetheria.local" : verseId,
                HostRuntimeId = string.IsNullOrWhiteSpace(hostRuntimeId) ? "aetheria-daemon" : hostRuntimeId,
                DefaultMode = AetheriaRuntimeAuthorityModes.AnyTrustedRuntime,
                DeploymentMode = AetheriaRuntimeVerseDeploymentModes.DedicatedDaemon,
                UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
                RuntimeRoles = new[]
                {
                    new AetheriaRuntimeAuthorityRuntimeRole
                    {
                        RuntimeId = string.IsNullOrWhiteSpace(hostRuntimeId) ? "aetheria-daemon" : hostRuntimeId,
                        Roles = new[]
                        {
                            AetheriaRuntimeVerseRuntimeRoles.DedicatedDaemon,
                            AetheriaRuntimeVerseRuntimeRoles.SimulationHost
                        }
                    }
                },
                Rules = new[]
                {
                    new AetheriaRuntimeAuthorityRule
                    {
                        RuleId = "trusted-coop.default",
                        SubjectPrefix = "*",
                        ClaimKinds = new[] { AetheriaRuntimeClaimKinds.Any },
                        Mode = AetheriaRuntimeAuthorityModes.AnyTrustedRuntime
                    }
                }
            };
        }

        public static AetheriaRuntimeVerseAuthorityPolicyDocument UnityHost(
            string verseId,
            string unityRuntimeId)
        {
            var runtimeId = string.IsNullOrWhiteSpace(unityRuntimeId) ? "aetheria-unity-host" : unityRuntimeId;
            return new AetheriaRuntimeVerseAuthorityPolicyDocument
            {
                VerseId = string.IsNullOrWhiteSpace(verseId) ? "aetheria.local" : verseId,
                PolicyId = "aetheria.unity-host.v1",
                HostRuntimeId = runtimeId,
                DefaultMode = AetheriaRuntimeAuthorityModes.HostAuthoritative,
                DeploymentMode = AetheriaRuntimeVerseDeploymentModes.UnityHost,
                UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
                RuntimeRoles = new[]
                {
                    new AetheriaRuntimeAuthorityRuntimeRole
                    {
                        RuntimeId = runtimeId,
                        Roles = new[]
                        {
                            AetheriaRuntimeVerseRuntimeRoles.UnityPlayer,
                            AetheriaRuntimeVerseRuntimeRoles.SimulationHost
                        }
                    }
                },
                Rules = new[]
                {
                    new AetheriaRuntimeAuthorityRule
                    {
                        RuleId = "unity-host.default",
                        SubjectPrefix = "*",
                        ClaimKinds = new[] { AetheriaRuntimeClaimKinds.Any },
                        Mode = AetheriaRuntimeAuthorityModes.HostAuthoritative
                    }
                }
            };
        }
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeAuthorityRuntimeRole
    {
        [Key(0)] public string RuntimeId { get; set; } = "";
        [Key(1)] public string[] Roles { get; set; } = Array.Empty<string>();
    }

    [MessagePackObject]
    public sealed class AetheriaRuntimeAuthorityRule
    {
        [Key(0)] public string RuleId { get; set; } = "";
        [Key(1)] public string SubjectPrefix { get; set; } = "*";
        [Key(2)] public string[] ClaimKinds { get; set; } = Array.Empty<string>();
        [Key(3)] public string Mode { get; set; } = AetheriaRuntimeAuthorityModes.AnyTrustedRuntime;
        [Key(4)] public string[] RuntimeIds { get; set; } = Array.Empty<string>();
        [Key(5)] public string LeaseScope { get; set; } = "";
        [Key(6)] public int Priority { get; set; }
    }

    [CultDocument("gamecult.aetheria.authority_lease", "gamecult.aetheria.authority_lease.v1")]
    [MessagePackObject]
    public sealed class AetheriaRuntimeAuthorityLeaseDocument
    {
        [Key(0)] public string Schema { get; set; } = AetheriaRuntimeVerseAuthoritySchemas.Lease;
        [Key(1)] public string LeaseId { get; set; } = "";
        [Key(2)] public string VerseId { get; set; } = "aetheria.local";
        [Key(3)] public string RuntimeId { get; set; } = "";
        [Key(4)] public string SubjectPrefix { get; set; } = "*";
        [Key(5)] public string[] ClaimKinds { get; set; } = Array.Empty<string>();
        [Key(6)] public string ValidFromUtc { get; set; } = "";
        [Key(7)] public string ExpiresAtUtc { get; set; } = "";
        [Key(8)] public string Scope { get; set; } = "";

        public bool IsActive(DateTimeOffset now)
        {
            return IsAfterOrEqual(now, ValidFromUtc, emptyIsActive: true) &&
                IsBefore(now, ExpiresAtUtc, emptyIsActive: true);
        }

        private static bool IsAfterOrEqual(DateTimeOffset now, string value, bool emptyIsActive)
        {
            if (string.IsNullOrWhiteSpace(value))
                return emptyIsActive;
            return DateTimeOffset.TryParse(value, out var parsed) && now >= parsed;
        }

        private static bool IsBefore(DateTimeOffset now, string value, bool emptyIsActive)
        {
            if (string.IsNullOrWhiteSpace(value))
                return emptyIsActive;
            return DateTimeOffset.TryParse(value, out var parsed) && now < parsed;
        }
    }

    public sealed class AetheriaRuntimeAuthorityDecision
    {
        public AetheriaRuntimeAuthorityDecision(
            bool authorized,
            string reason,
            string mode,
            string subjectKey,
            string claimKind,
            string authorRuntimeId,
            string ruleId)
        {
            Authorized = authorized;
            Reason = reason ?? "";
            Mode = mode ?? "";
            SubjectKey = subjectKey ?? "";
            ClaimKind = claimKind ?? "";
            AuthorRuntimeId = authorRuntimeId ?? "";
            RuleId = ruleId ?? "";
        }

        public bool Authorized { get; }
        public string Reason { get; }
        public string Mode { get; }
        public string SubjectKey { get; }
        public string ClaimKind { get; }
        public string AuthorRuntimeId { get; }
        public string RuleId { get; }
    }

    public static class AetheriaRuntimeAuthorityRouter
    {
        public static AetheriaRuntimeAuthorityDecision Authorize(
            AetheriaRuntimeDaemonCommandDocument command,
            AetheriaRuntimeVerseAuthorityPolicyDocument? policy,
            IEnumerable<AetheriaRuntimeAuthorityLeaseDocument>? leases,
            string localRuntimeId)
        {
            if (command == null)
                return Denied("missing-command", "", "", "", "", "");

            policy ??= AetheriaRuntimeVerseAuthorityPolicyDocument.TrustedCoop("aetheria.local", localRuntimeId);
            var subjectKey = ResolveSubjectKey(command);
            var claimKind = ResolveClaimKind(command.Kind);
            var authorRuntimeId = ResolveAuthorRuntimeId(command, localRuntimeId);
            var rule = SelectRule(policy, subjectKey, claimKind);
            var mode = Normalize(rule?.Mode, policy.DefaultMode);
            var ruleId = rule?.RuleId ?? "default";

            switch (mode)
            {
                case AetheriaRuntimeAuthorityModes.AnyTrustedRuntime:
                    return Allowed(mode, subjectKey, claimKind, authorRuntimeId, ruleId);
                case AetheriaRuntimeAuthorityModes.HostAuthoritative:
                    return string.Equals(authorRuntimeId, policy.HostRuntimeId, StringComparison.Ordinal)
                        ? Allowed(mode, subjectKey, claimKind, authorRuntimeId, ruleId)
                        : Denied("host-authority-required", mode, subjectKey, claimKind, authorRuntimeId, ruleId);
                case AetheriaRuntimeAuthorityModes.DelegatedRuntime:
                    return IsRuntimeAllowed(authorRuntimeId, rule?.RuntimeIds)
                        ? Allowed(mode, subjectKey, claimKind, authorRuntimeId, ruleId)
                        : Denied("delegated-runtime-required", mode, subjectKey, claimKind, authorRuntimeId, ruleId);
                case AetheriaRuntimeAuthorityModes.InterestLease:
                    return HasLease(authorRuntimeId, subjectKey, claimKind, rule?.LeaseScope, leases)
                        ? Allowed(mode, subjectKey, claimKind, authorRuntimeId, ruleId)
                        : Denied("authority-lease-required", mode, subjectKey, claimKind, authorRuntimeId, ruleId);
                case AetheriaRuntimeAuthorityModes.OwningRuntime:
                case AetheriaRuntimeAuthorityModes.WitnessQuorum:
                case AetheriaRuntimeAuthorityModes.OperatorFinality:
                case AetheriaRuntimeAuthorityModes.MergeableCrdt:
                    return Denied("authority-mode-not-implemented", mode, subjectKey, claimKind, authorRuntimeId, ruleId);
                default:
                    return Denied("unknown-authority-mode", mode, subjectKey, claimKind, authorRuntimeId, ruleId);
            }
        }

        public static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> AuthorizedCommands(
            IEnumerable<AetheriaRuntimeDaemonCommandDocument> commands,
            AetheriaRuntimeVerseAuthorityPolicyDocument? policy,
            IEnumerable<AetheriaRuntimeAuthorityLeaseDocument>? leases,
            string localRuntimeId,
            ICollection<string>? rejectedCommandIds = null)
        {
            var accepted = new List<AetheriaRuntimeDaemonCommandDocument>();
            foreach (var command in commands ?? Enumerable.Empty<AetheriaRuntimeDaemonCommandDocument>())
            {
                var decision = Authorize(command, policy, leases, localRuntimeId);
                if (decision.Authorized)
                {
                    accepted.Add(command);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(command?.CommandId))
                    rejectedCommandIds?.Add(command.CommandId);
            }

            return accepted;
        }

        public static string ResolveClaimKind(AetheriaRuntimeDaemonCommandKinds kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeDaemonCommandKinds.SetMoveVector:
                case AetheriaRuntimeDaemonCommandKinds.SetLookDirection:
                case AetheriaRuntimeDaemonCommandKinds.SetTractorPower:
                case AetheriaRuntimeDaemonCommandKinds.Dock:
                case AetheriaRuntimeDaemonCommandKinds.DockNearest:
                case AetheriaRuntimeDaemonCommandKinds.Undock:
                case AetheriaRuntimeDaemonCommandKinds.EnterWormhole:
                case AetheriaRuntimeDaemonCommandKinds.TowToStation:
                    return AetheriaRuntimeClaimKinds.Movement;
                case AetheriaRuntimeDaemonCommandKinds.SetTarget:
                case AetheriaRuntimeDaemonCommandKinds.ClearTarget:
                case AetheriaRuntimeDaemonCommandKinds.TargetNearest:
                case AetheriaRuntimeDaemonCommandKinds.TargetNext:
                case AetheriaRuntimeDaemonCommandKinds.TargetPrevious:
                case AetheriaRuntimeDaemonCommandKinds.TargetReticle:
                    return AetheriaRuntimeClaimKinds.Targeting;
                case AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup:
                case AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupActive:
                case AetheriaRuntimeDaemonCommandKinds.SetWeaponGroupMembership:
                case AetheriaRuntimeDaemonCommandKinds.SetBehaviorActive:
                case AetheriaRuntimeDaemonCommandKinds.ActivateConsumable:
                case AetheriaRuntimeDaemonCommandKinds.SensorPing:
                case AetheriaRuntimeDaemonCommandKinds.SetHeatsinksEnabled:
                case AetheriaRuntimeDaemonCommandKinds.SetOverrideShutdown:
                case AetheriaRuntimeDaemonCommandKinds.SetShutdownPerformance:
                case AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled:
                    return AetheriaRuntimeClaimKinds.Combat;
                case AetheriaRuntimeDaemonCommandKinds.SetItemEnabled:
                case AetheriaRuntimeDaemonCommandKinds.SetItemOverrideShutdown:
                case AetheriaRuntimeDaemonCommandKinds.SetThermotoggleTargetTemperature:
                case AetheriaRuntimeDaemonCommandKinds.PickUpLoot:
                case AetheriaRuntimeDaemonCommandKinds.RestoreLoadout:
                case AetheriaRuntimeDaemonCommandKinds.TransferCargoItem:
                case AetheriaRuntimeDaemonCommandKinds.EquipItem:
                case AetheriaRuntimeDaemonCommandKinds.StoreItem:
                case AetheriaRuntimeDaemonCommandKinds.ToggleHullConductivity:
                    return AetheriaRuntimeClaimKinds.Inventory;
                case AetheriaRuntimeDaemonCommandKinds.TradePurchase:
                    return AetheriaRuntimeClaimKinds.Economy;
                case AetheriaRuntimeDaemonCommandKinds.SetEntityName:
                    return AetheriaRuntimeClaimKinds.Metadata;
                case AetheriaRuntimeDaemonCommandKinds.DestroyEntity:
                    return AetheriaRuntimeClaimKinds.System;
                case AetheriaRuntimeDaemonCommandKinds.Interact:
                case AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip:
                    return AetheriaRuntimeClaimKinds.Interaction;
                default:
                    return AetheriaRuntimeClaimKinds.System;
            }
        }

        public static string ResolveSubjectKey(AetheriaRuntimeDaemonCommandDocument command)
        {
            if (command == null)
                return "";

            if (!string.IsNullOrWhiteSpace(command.SubjectKey))
                return command.SubjectKey;

            if (UsesTargetSubject(command.Kind) && !string.IsNullOrWhiteSpace(command.TargetEntityKey))
                return command.TargetEntityKey;

            if (!string.IsNullOrWhiteSpace(command.ActorEntityKey))
                return command.ActorEntityKey;

            if (!string.IsNullOrWhiteSpace(command.TargetEntityKey))
                return command.TargetEntityKey;

            return "run";
        }

        private static bool UsesTargetSubject(AetheriaRuntimeDaemonCommandKinds kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeDaemonCommandKinds.SetEntityName:
                case AetheriaRuntimeDaemonCommandKinds.DestroyEntity:
                case AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip:
                case AetheriaRuntimeDaemonCommandKinds.TowToStation:
                case AetheriaRuntimeDaemonCommandKinds.TradePurchase:
                case AetheriaRuntimeDaemonCommandKinds.TransferCargoItem:
                case AetheriaRuntimeDaemonCommandKinds.EquipItem:
                case AetheriaRuntimeDaemonCommandKinds.StoreItem:
                case AetheriaRuntimeDaemonCommandKinds.PickUpLoot:
                case AetheriaRuntimeDaemonCommandKinds.RestoreLoadout:
                    return true;
                default:
                    return false;
            }
        }

        private static string ResolveAuthorRuntimeId(
            AetheriaRuntimeDaemonCommandDocument command,
            string localRuntimeId)
        {
            if (!string.IsNullOrWhiteSpace(command.AuthorRuntimeId))
                return command.AuthorRuntimeId;
            if (!string.IsNullOrWhiteSpace(command.ClientId))
                return command.ClientId;
            return localRuntimeId ?? "";
        }

        private static AetheriaRuntimeAuthorityRule? SelectRule(
            AetheriaRuntimeVerseAuthorityPolicyDocument policy,
            string subjectKey,
            string claimKind)
        {
            return (policy.Rules ?? Array.Empty<AetheriaRuntimeAuthorityRule>())
                .Where(rule => MatchesSubject(rule.SubjectPrefix, subjectKey) && MatchesClaim(rule.ClaimKinds, claimKind))
                .OrderByDescending(rule => rule.Priority)
                .ThenByDescending(rule => (rule.SubjectPrefix ?? "").Length)
                .FirstOrDefault();
        }

        private static bool HasLease(
            string runtimeId,
            string subjectKey,
            string claimKind,
            string? requiredScope,
            IEnumerable<AetheriaRuntimeAuthorityLeaseDocument>? leases)
        {
            var now = DateTimeOffset.UtcNow;
            return (leases ?? Enumerable.Empty<AetheriaRuntimeAuthorityLeaseDocument>())
                .Any(lease =>
                    lease != null &&
                    lease.IsActive(now) &&
                    string.Equals(lease.RuntimeId ?? "", runtimeId, StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(requiredScope) ||
                        string.Equals(lease.Scope ?? "", requiredScope, StringComparison.Ordinal)) &&
                    MatchesSubject(lease.SubjectPrefix, subjectKey) &&
                    MatchesClaim(lease.ClaimKinds, claimKind));
        }

        private static bool IsRuntimeAllowed(string runtimeId, IEnumerable<string>? runtimeIds)
        {
            return (runtimeIds ?? Array.Empty<string>())
                .Any(candidate => string.Equals(candidate ?? "", runtimeId, StringComparison.Ordinal));
        }

        private static bool MatchesSubject(string? subjectPrefix, string subjectKey)
        {
            if (string.IsNullOrWhiteSpace(subjectPrefix) || subjectPrefix == "*")
                return true;
            return (subjectKey ?? "").StartsWith(subjectPrefix, StringComparison.Ordinal);
        }

        private static bool MatchesClaim(IEnumerable<string>? claimKinds, string claimKind)
        {
            var claims = (claimKinds ?? Array.Empty<string>()).ToArray();
            return claims.Length == 0 ||
                claims.Any(candidate =>
                    string.Equals(candidate, AetheriaRuntimeClaimKinds.Any, StringComparison.Ordinal) ||
                    string.Equals(candidate, claimKind, StringComparison.Ordinal));
        }

        private static string Normalize(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static AetheriaRuntimeAuthorityDecision Allowed(
            string mode,
            string subjectKey,
            string claimKind,
            string authorRuntimeId,
            string ruleId)
        {
            return new AetheriaRuntimeAuthorityDecision(
                true,
                "authorized",
                mode,
                subjectKey,
                claimKind,
                authorRuntimeId,
                ruleId);
        }

        private static AetheriaRuntimeAuthorityDecision Denied(
            string reason,
            string mode,
            string subjectKey,
            string claimKind,
            string authorRuntimeId,
            string ruleId)
        {
            return new AetheriaRuntimeAuthorityDecision(
                false,
                reason,
                mode,
                subjectKey,
                claimKind,
                authorRuntimeId,
                ruleId);
        }
    }
}
