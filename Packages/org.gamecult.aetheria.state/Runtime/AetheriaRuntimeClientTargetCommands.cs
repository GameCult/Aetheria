using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeClientTargetCommands
    {
        public const string SurfaceId = "aetheria.client_target";
        public const string Refresh = "aetheria.client_target.refresh";
        public const string CycleTargetKind = "aetheria.client_target.kind.cycle";
        public const string SetTitle = "aetheria.client_target.title.set";
        public const string SetVerseId = "aetheria.client_target.verse_id.set";
        public const string SetRuntimeId = "aetheria.client_target.runtime_id.set";
        public const string SetCultMeshAddress = "aetheria.client_target.cultmesh_address.set";
        public const string SetStateFilePath = "aetheria.client_target.state_file_path.set";
        public const string SetDiscoveryEndpoints = "aetheria.client_target.discovery_endpoints.set";
        public const string DiscoverVerses = "aetheria.client_target.discovery.refresh";
        public const string SelectDiscoveredVerse = "aetheria.client_target.discovery.select";
        public const string SyncReplica = "aetheria.client_target.replica.sync";

        public static bool IsKnown(string command)
        {
            return command == Refresh ||
                command == CycleTargetKind ||
                command == SetTitle ||
                command == SetVerseId ||
                command == SetRuntimeId ||
                command == SetCultMeshAddress ||
                command == SetStateFilePath ||
                command == SetDiscoveryEndpoints ||
                command == DiscoverVerses ||
                command == SelectDiscoveredVerse ||
                command == SyncReplica;
        }
    }

#if UNITY_5_3_OR_NEWER
    public static class AetheriaRuntimeClientTargetSurfaceCommands
    {
        public static bool TryRequest(
            AetheriaClientTarget target,
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeClientTargetDocument? document)
        {
            document = null;
            if (!TryRead(request, out var operation))
                return false;

            document = Request(target, operation);
            return true;
        }

        private static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaClientTargetOperation operation)
        {
            operation = default;
            if (request == null)
                return false;

            var command = request.Operation?.OperationId ?? "";
            switch (command)
            {
                case AetheriaRuntimeClientTargetCommands.Refresh:
                    operation = new AetheriaClientTargetOperation(AetheriaClientTargetOperationKind.Refresh);
                    return true;
                case AetheriaRuntimeClientTargetCommands.CycleTargetKind:
                    operation = new AetheriaClientTargetOperation(AetheriaClientTargetOperationKind.CycleTransport);
                    return true;
                case AetheriaRuntimeClientTargetCommands.SetTitle:
                    operation = new AetheriaClientTargetOperation(
                        AetheriaClientTargetOperationKind.SetTitle,
                        value: ReadPayloadValue(request, "value"));
                    return true;
                case AetheriaRuntimeClientTargetCommands.SetVerseId:
                    operation = new AetheriaClientTargetOperation(
                        AetheriaClientTargetOperationKind.SetVerseId,
                        value: ReadPayloadValue(request, "value"));
                    return true;
                case AetheriaRuntimeClientTargetCommands.SetRuntimeId:
                    operation = new AetheriaClientTargetOperation(
                        AetheriaClientTargetOperationKind.SetRuntimeId,
                        value: ReadPayloadValue(request, "value"));
                    return true;
                case AetheriaRuntimeClientTargetCommands.SetCultMeshAddress:
                    operation = new AetheriaClientTargetOperation(
                        AetheriaClientTargetOperationKind.SetCultMeshAddress,
                        value: ReadPayloadValue(request, "value"));
                    return true;
                case AetheriaRuntimeClientTargetCommands.SetStateFilePath:
                    operation = new AetheriaClientTargetOperation(
                        AetheriaClientTargetOperationKind.SetStateFilePath,
                        value: ReadPayloadValue(request, "value"));
                    return true;
                case AetheriaRuntimeClientTargetCommands.SetDiscoveryEndpoints:
                    operation = new AetheriaClientTargetOperation(
                        AetheriaClientTargetOperationKind.SetDiscoveryEndpoints,
                        discoveryEndpoints: ParseDiscoveryEndpoints(ReadPayloadValue(request, "value")));
                    return true;
                case AetheriaRuntimeClientTargetCommands.DiscoverVerses:
                    operation = new AetheriaClientTargetOperation(AetheriaClientTargetOperationKind.DiscoverVerses);
                    return true;
                case AetheriaRuntimeClientTargetCommands.SelectDiscoveredVerse:
                    operation = new AetheriaClientTargetOperation(
                        AetheriaClientTargetOperationKind.SelectDiscoveredVerse,
                        verseId: ReadPayloadValue(request, "verseId"),
                        title: ReadPayloadValue(request, "title"),
                        cultMeshAddress: ReadPayloadValue(request, "cultMeshAddress"),
                        discoveryEndpoints: ParseDiscoveryEndpoints(ReadPayloadValue(request, "discoveryEndpoints")));
                    return true;
                case AetheriaRuntimeClientTargetCommands.SyncReplica:
                    operation = new AetheriaClientTargetOperation(AetheriaClientTargetOperationKind.SyncReplica);
                    return true;
                default:
                    return false;
            }
        }

        private static AetheriaRuntimeClientTargetDocument Request(
            AetheriaClientTarget target,
            AetheriaClientTargetOperation operation)
        {
            switch (operation.Kind)
            {
                case AetheriaClientTargetOperationKind.Refresh:
                    return target.Refresh();
                case AetheriaClientTargetOperationKind.CycleTransport:
                    return target.CycleTransport();
                case AetheriaClientTargetOperationKind.SetTitle:
                    return target.RequestTitle(operation.Value);
                case AetheriaClientTargetOperationKind.SetVerseId:
                    return target.RequestVerseId(operation.Value);
                case AetheriaClientTargetOperationKind.SetRuntimeId:
                    return target.RequestRuntimeId(operation.Value);
                case AetheriaClientTargetOperationKind.SetCultMeshAddress:
                    return target.RequestCultMeshAddress(operation.Value);
                case AetheriaClientTargetOperationKind.SetStateFilePath:
                    return target.RequestStateFilePath(operation.Value);
                case AetheriaClientTargetOperationKind.SetDiscoveryEndpoints:
                    return target.RequestDiscoveryEndpoints(operation.DiscoveryEndpoints);
                case AetheriaClientTargetOperationKind.DiscoverVerses:
                    return target.DiscoverVerses();
                case AetheriaClientTargetOperationKind.SelectDiscoveredVerse:
                    return target.SelectDiscoveredVerse(
                        operation.VerseId,
                        operation.Title,
                        operation.CultMeshAddress,
                        operation.DiscoveryEndpoints);
                case AetheriaClientTargetOperationKind.SyncReplica:
                    return target.SyncReplica();
                default:
                    throw new ArgumentException(
                        $"Unknown Aetheria client-target operation '{operation.Kind}'.",
                        nameof(operation));
            }
        }

        private static string ReadPayloadValue(EveSurfaceCommandRequest request, string key)
        {
            return request.Payload.GetString(key);
        }

        private static string[] ParseDiscoveryEndpoints(string value)
        {
            return (value ?? "")
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(endpoint => endpoint.Trim())
                .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private enum AetheriaClientTargetOperationKind
        {
            Unknown = 0,
            Refresh = 1,
            CycleTransport = 2,
            SetTitle = 3,
            SetVerseId = 4,
            SetRuntimeId = 5,
            SetCultMeshAddress = 6,
            SetStateFilePath = 7,
            SetDiscoveryEndpoints = 8,
            DiscoverVerses = 9,
            SelectDiscoveredVerse = 10,
            SyncReplica = 11
        }

        private readonly struct AetheriaClientTargetOperation
        {
            public AetheriaClientTargetOperation(
                AetheriaClientTargetOperationKind kind,
                string value = "",
                string verseId = "",
                string title = "",
                string cultMeshAddress = "",
                IReadOnlyList<string>? discoveryEndpoints = null)
            {
                Kind = kind;
                Value = value ?? "";
                VerseId = verseId ?? "";
                Title = title ?? "";
                CultMeshAddress = cultMeshAddress ?? "";
                DiscoveryEndpoints = discoveryEndpoints?.ToArray() ?? Array.Empty<string>();
            }

            public AetheriaClientTargetOperationKind Kind { get; }
            public string Value { get; }
            public string VerseId { get; }
            public string Title { get; }
            public string CultMeshAddress { get; }
            public IReadOnlyList<string> DiscoveryEndpoints { get; }
        }
    }
#endif
}
