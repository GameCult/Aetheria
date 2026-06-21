using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Mesh;
using GameCult.Networking;
using GameCult.Aetheria.State.Verse;

#nullable enable

namespace Aetheria.State
{
    public sealed class AetheriaCommandPort : IDisposable
    {
        private readonly CultMeshNode _node;

        private AetheriaCommandPort(string stateFilePath, CultMeshNode node)
        {
            StateFilePath = stateFilePath;
            _node = node;
        }

        public string StateFilePath { get; }

        public static async Task<AetheriaCommandPort> OpenAsync(
            string stateFilePath,
            string runtimeId = "aetheria-command-client")
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            var fullPath = Path.GetFullPath(stateFilePath);
            var registry = CreateCultCacheRegistry();
            var node = await CultMesh.CreateNodeAsync(
                    fullPath,
                    new CultMeshNodeOptions
                    {
                        StartServer = false,
                        EnableDurableShardLogs = true,
                        CacheOptions = new CultCacheOpenOptions
                        {
                            Registry = registry,
                            PullOnOpen = false,
                            StoreFlushOnDispose = true,
                            UseDirectoryStore = true
                        },
                        DatabaseOptions = new CultNetDatabaseOptions
                        {
                            RuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? "aetheria-command-client" : runtimeId,
                            DocumentRegistry = CreateCultNetRegistry(registry)
                        }
                    })
                .ConfigureAwait(false);

            return new AetheriaCommandPort(fullPath, node);
        }

        public async Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(
            AetheriaRuntimeDaemonCommandDocument command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            command.Schema = AetheriaRuntimeDaemonSchemas.Command;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
                command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

            await _node.Database.PutAsync(DaemonCommandKey(command.CommandId), command).ConfigureAwait(false);
            await _node.FlushAsync().ConfigureAwait(false);
            return AetheriaRuntimeDaemonOperationClient.ToEnvelope(command);
        }

        public async Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync(
            AetheriaRuntimeEveCommandDocument command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            AetheriaRuntimeEveCommandClient.NormalizeDocument(command);
            command.Schema = AetheriaRuntimeEveCommandDocument.SchemaId;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
                command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

            await _node.Database.PutAsync(EveCommandKey(command.CommandId), command).ConfigureAwait(false);
            await _node.FlushAsync().ConfigureAwait(false);
            return AetheriaRuntimeEveCommandClient.ToEnvelope(command);
        }

        private static CultDocumentRegistry CreateCultCacheRegistry()
        {
            var registry = new CultDocumentRegistry();
            registry.GetRequired(typeof(AetheriaRuntimeDaemonCommandDocument));
            registry.GetRequired(typeof(AetheriaRuntimeEveCommandDocument));
            return registry;
        }

        private static CultNetDocumentRegistry CreateCultNetRegistry(CultDocumentRegistry registry)
        {
            return new CultNetDocumentRegistry(
                registry,
                new[]
                {
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeEveCommandDocument>(registry)
                });
        }

        private static CultRecordKey DaemonCommandKey(string commandId)
        {
            return new CultRecordKey($"daemon:commands:{StableToken(commandId)}:gamecult.aetheria.daemon_command.v1");
        }

        private static CultRecordKey EveCommandKey(string commandId)
        {
            return new CultRecordKey($"eve:commands:{StableToken(commandId)}:gamecult.eve.command.v1");
        }

        private static string StableToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            var chars = value
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray();
            var token = new string(chars).Trim('-').ToLowerInvariant();
            while (token.Contains("--", StringComparison.Ordinal))
                token = token.Replace("--", "-", StringComparison.Ordinal);
            return string.IsNullOrWhiteSpace(token) ? "empty" : token;
        }

        public void Dispose()
        {
            _node.Dispose();
        }
    }
}

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeCommandPort : IDisposable
    {
        public const string DefaultRuntimeId = "aetheria-command-client";

        private readonly global::Aetheria.State.AetheriaCommandPort _port;

        private AetheriaRuntimeCommandPort(global::Aetheria.State.AetheriaCommandPort port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        public string StateFilePath => _port.StateFilePath;

        public static async Task<AetheriaRuntimeCommandPort> OpenAsync(
            string stateFilePath,
            string runtimeId = DefaultRuntimeId)
        {
            var port = await global::Aetheria.State.AetheriaCommandPort.OpenAsync(
                    stateFilePath,
                    string.IsNullOrWhiteSpace(runtimeId) ? DefaultRuntimeId : runtimeId)
                .ConfigureAwait(false);
            return new AetheriaRuntimeCommandPort(port);
        }

        public Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(
            AetheriaRuntimeDaemonCommandDocument command)
        {
            return _port.SubmitDaemonCommandAsync(command);
        }

        public Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync(
            AetheriaRuntimeEveCommandDocument command)
        {
            return _port.SubmitEveCommandAsync(command);
        }

        public void Dispose()
        {
            _port.Dispose();
        }
    }

    internal static class AetheriaRuntimeCommandSubmitter
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, AetheriaRuntimeCommandPort> Ports =
            new Dictionary<string, AetheriaRuntimeCommandPort>(StringComparer.OrdinalIgnoreCase);

        public static bool TrySubmitDaemonCommand(
            string stateFilePath,
            AetheriaRuntimeDaemonCommandDocument command,
            string runtimeId,
            out AetheriaRuntimeDaemonCommandEnvelope? envelope,
            out string error)
        {
            envelope = null;
            error = "";

            try
            {
                envelope = GetOrOpen(stateFilePath, runtimeId)
                    .SubmitDaemonCommandAsync(command)
                    .GetAwaiter()
                    .GetResult();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        public static bool TrySubmitEveCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandDocument command,
            string runtimeId,
            out AetheriaRuntimeEveCommandEnvelope? envelope,
            out string error)
        {
            envelope = null;
            error = "";

            try
            {
                envelope = GetOrOpen(stateFilePath, runtimeId)
                    .SubmitEveCommandAsync(command)
                    .GetAwaiter()
                    .GetResult();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private static AetheriaRuntimeCommandPort GetOrOpen(string stateFilePath, string runtimeId)
        {
            var fullPath = Path.GetFullPath(stateFilePath ?? "");
            var key = $"{fullPath}|{(string.IsNullOrWhiteSpace(runtimeId) ? AetheriaRuntimeCommandPort.DefaultRuntimeId : runtimeId)}";
            lock (Sync)
            {
                if (Ports.TryGetValue(key, out var existing))
                    return existing;
            }

            var created = AetheriaRuntimeCommandPort.OpenAsync(fullPath, runtimeId)
                .GetAwaiter()
                .GetResult();
            lock (Sync)
            {
                if (Ports.TryGetValue(key, out var existing))
                {
                    created.Dispose();
                    return existing;
                }

                Ports.Add(key, created);
                return created;
            }
        }
    }
}
