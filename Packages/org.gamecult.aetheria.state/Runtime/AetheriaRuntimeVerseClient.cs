using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aetheria.State.Documents;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Mesh;
using GameCult.Networking;
using R3;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeVerseRecordKeys
    {
        public static CultRecordKey DaemonProviderAdvertisement { get; } =
            new CultRecordKey("daemon:aetheria.provider_advertisement.v1");

        public static CultRecordKey DaemonHealth { get; } =
            new CultRecordKey("daemon:aetheria.health.v1");

        public static CultRecordKey DaemonCommandBoundary { get; } =
            new CultRecordKey("daemon:aetheria.command_boundary.v1");

        public static CultRecordKey DaemonFrameLatest { get; } =
            new CultRecordKey("daemon:aetheria.frame.latest.v1");

        public static CultRecordKey DaemonSoaViewLatest { get; } =
            new CultRecordKey("daemon:aetheria.soa_view.latest.v1");

        public static CultRecordKey DaemonGameSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.game");

        public static CultRecordKey DaemonGameTuiSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.game.tui");

        public static CultRecordKey DaemonEditorSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.editor");

        public static CultRecordKey DaemonEditorTuiSurface { get; } =
            new CultRecordKey("eve:surface:aetheria.daemon.editor.tui");

        public static CultRecordKey DaemonCommand(string commandId)
        {
            return new CultRecordKey($"daemon:commands:{StableToken(commandId)}:gamecult.aetheria.daemon_command.v1");
        }

        public static CultRecordKey EveCommand(string commandId)
        {
            return new CultRecordKey($"eve:commands:{StableToken(commandId)}:gamecult.eve.command.v1");
        }

        public static string StableToken(string value)
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
    }

    public static class AetheriaRuntimeVerseContractRegistry
    {
        private static readonly Type[] RuntimeDocumentTypes =
        {
            typeof(AetheriaRuntimeDaemonProviderAdvertisementDocument),
            typeof(AetheriaRuntimeDaemonHealthDocument),
            typeof(AetheriaRuntimeDaemonCommandBoundaryDocument),
            typeof(AetheriaRuntimeDaemonFrameDocument),
            typeof(AetheriaRuntimeDaemonSoaViewDocument),
            typeof(AetheriaRuntimeDaemonCommandDocument),
            typeof(AetheriaRuntimeEveCommandDocument),
            typeof(EveSurfaceState)
        };

        public static CultDocumentRegistry CreateCultCacheRegistry()
        {
            var registry = new CultDocumentRegistry();
            foreach (var documentType in RuntimeDocumentTypes)
            {
                registry.GetRequired(documentType);
            }

            return registry;
        }

        public static CultNetDocumentRegistry CreateCultNetRegistry(CultDocumentRegistry? cacheRegistry = null)
        {
            var registry = cacheRegistry ?? CreateCultCacheRegistry();
            return new CultNetDocumentRegistry(
                registry,
                new[]
                {
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonHealthDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandBoundaryDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonFrameDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonSoaViewDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeDaemonCommandDocument>(registry),
                    CultNetDocumentBinding.ForDocument<AetheriaRuntimeEveCommandDocument>(registry),
                    CultNetDocumentBinding.ForDocument<EveSurfaceState>(registry)
                });
        }
    }

    public sealed class AetheriaRuntimeVerseDocument<T> where T : class
    {
        private readonly Func<Task<T?>> _read;
        private readonly Func<T, Task> _replace;
        private readonly Observable<T> _watch;

        public AetheriaRuntimeVerseDocument(
            CultRecordKey key,
            Func<Task<T?>> read,
            Func<T, Task> replace,
            Observable<T> watch)
        {
            Key = key;
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace));
            _watch = watch ?? throw new ArgumentNullException(nameof(watch));
        }

        public CultRecordKey Key { get; }

        public Task<T?> ReadAsync()
        {
            return _read();
        }

        public Task ReplaceAsync(T value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return _replace(value);
        }

        public Observable<T> Watch()
        {
            return _watch;
        }
    }

    public sealed class AetheriaRuntimeVerseClient : IDisposable
    {
        public const string DefaultRuntimeId = "aetheria-verse-client";

        private readonly CultMeshNode _node;
        private bool _disposed;

        private AetheriaRuntimeVerseClient(string statePath, string runtimeId, CultMeshNode node)
        {
            StatePath = statePath;
            RuntimeId = runtimeId;
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public string StatePath { get; }

        public string RuntimeId { get; }

        public CultMeshNode Node => _node;

        public CultCache Cache => _node.Cache;

        public CultNetDatabase Database => _node.Database;

        public static async Task<AetheriaRuntimeVerseClient> OpenAsync(
            string statePath,
            string runtimeId = DefaultRuntimeId,
            bool startServer = false,
            bool pullOnOpen = true)
        {
            if (string.IsNullOrWhiteSpace(statePath))
                throw new ArgumentException("State path must be non-empty.", nameof(statePath));

            var fullPath = Path.GetFullPath(statePath);
            var effectiveRuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? DefaultRuntimeId : runtimeId;
            var registry = AetheriaRuntimeVerseContractRegistry.CreateCultCacheRegistry();
            var node = await CultMesh.CreateNodeAsync(
                    fullPath,
                    new CultMeshNodeOptions
                    {
                        StartServer = startServer,
                        EnableDurableShardLogs = true,
                        CacheOptions = new CultCacheOpenOptions
                        {
                            Registry = registry,
                            PullOnOpen = pullOnOpen,
                            StoreFlushOnDispose = true,
                            UseDirectoryStore = true
                        },
                        DatabaseOptions = new CultNetDatabaseOptions
                        {
                            RuntimeId = effectiveRuntimeId,
                            DocumentRegistry = AetheriaRuntimeVerseContractRegistry.CreateCultNetRegistry(registry)
                        }
                    })
                .ConfigureAwait(false);

            return new AetheriaRuntimeVerseClient(fullPath, effectiveRuntimeId, node);
        }

        public Task<AetheriaRuntimeDaemonProviderAdvertisementDocument?> GetProviderAdvertisementAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public Task<AetheriaRuntimeDaemonHealthDocument?> GetHealthAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public Task<AetheriaRuntimeDaemonCommandBoundaryDocument?> GetCommandBoundaryAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public Task<AetheriaRuntimeDaemonFrameDocument?> GetLatestFrameAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public Task<AetheriaRuntimeDaemonSoaViewDocument?> GetLatestSoaViewAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public AetheriaRuntimeCatalogSnapshot OpenRuntimeCatalog()
        {
            ThrowIfDisposed();
            return AetheriaRuntimeCatalogStore.OpenReadOnly(StatePath);
        }

        public Task<AetheriaRuntimePlayerSettingsSnapshot?> GetPlayerSettingsAsync()
        {
            ThrowIfDisposed();
            return Task.FromResult(AetheriaRuntimeCatalogStore.ReadPlayerSettings(StatePath));
        }

        public Task<AetheriaRuntimeVerseHostSettingsSnapshot?> GetVerseHostSettingsAsync()
        {
            ThrowIfDisposed();
            return Task.FromResult(AetheriaRuntimeCatalogStore.ReadVerseHostSettings(StatePath));
        }

        public Task<IReadOnlyList<AetheriaRuntimeLoadoutTemplateSnapshot>> GetLoadoutTemplatesAsync()
        {
            ThrowIfDisposed();
            return Task.FromResult(AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(StatePath));
        }

        public async Task<AetheriaRuntimeObservedDaemonState?> GetObservedDaemonStateAsync()
        {
            ThrowIfDisposed();

            var frame = await GetLatestFrameAsync().ConfigureAwait(false);
            if (frame == null)
                return null;

            var soaView = await GetLatestSoaViewAsync().ConfigureAwait(false);
            if (soaView == null ||
                !string.Equals(soaView.Schema, AetheriaRuntimeDaemonSchemas.SoaView, StringComparison.Ordinal))
            {
                soaView = null;
            }

            return new AetheriaRuntimeObservedDaemonState(
                frame,
                soaView,
                AetheriaRuntimeDaemonFrameStore.GetFramePath(StatePath),
                AetheriaRuntimeDaemonSoaViewStore.GetViewPath(StatePath));
        }

        public async Task<AetheriaRuntimeDaemonFrameDocument?> GetLatestAuthoritativeRunFrameAsync()
        {
            ThrowIfDisposed();

            var frame = await GetLatestFrameAsync().ConfigureAwait(false);
            if (frame == null ||
                !frame.IsAuthoritative ||
                frame.Run == null ||
                frame.Run.Zones == null ||
                frame.Run.Zones.Count == 0)
            {
                return null;
            }

            return frame;
        }

        public Task<EveSurfaceState?> GetDaemonGameSurfaceAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public Task<EveSurfaceState?> GetDaemonGameTuiSurfaceAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public Task<EveSurfaceState?> GetDaemonEditorSurfaceAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public Task<EveSurfaceState?> GetDaemonEditorTuiSurfaceAsync()
        {
            ThrowIfDisposed();
            return Database.GetAsync<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        public AetheriaRuntimeVerseDocument<AetheriaRuntimeDaemonProviderAdvertisementDocument> ProviderAdvertisement()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public AetheriaRuntimeVerseDocument<AetheriaRuntimeDaemonHealthDocument> Health()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public AetheriaRuntimeVerseDocument<AetheriaRuntimeDaemonCommandBoundaryDocument> CommandBoundary()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public AetheriaRuntimeVerseDocument<AetheriaRuntimeDaemonFrameDocument> LatestFrame()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public AetheriaRuntimeVerseDocument<AetheriaRuntimeDaemonSoaViewDocument> LatestSoaView()
        {
            ThrowIfDisposed();
            return Document<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public AetheriaRuntimeVerseDocument<EveSurfaceState> DaemonGameSurface()
        {
            ThrowIfDisposed();
            return Document<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public AetheriaRuntimeVerseDocument<EveSurfaceState> DaemonGameTuiSurface()
        {
            ThrowIfDisposed();
            return Document<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public AetheriaRuntimeVerseDocument<EveSurfaceState> DaemonEditorSurface()
        {
            ThrowIfDisposed();
            return Document<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public AetheriaRuntimeVerseDocument<EveSurfaceState> DaemonEditorTuiSurface()
        {
            ThrowIfDisposed();
            return Document<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonProviderAdvertisementDocument>>
            WatchProviderAdvertisements()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonProviderAdvertisementDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonHealthDocument>> WatchHealth()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonHealthDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonHealth);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonCommandBoundaryDocument>>
            WatchCommandBoundary()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonCommandBoundaryDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonFrameDocument>> WatchLatestFrames()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonFrameDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest);
        }

        public Observable<CultNetDatabaseChange<AetheriaRuntimeDaemonSoaViewDocument>> WatchLatestSoaViews()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<AetheriaRuntimeDaemonSoaViewDocument>(
                AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonGameSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonGameTuiSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonGameTuiSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonEditorSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorSurface);
        }

        public Observable<CultNetDatabaseChange<EveSurfaceState>> WatchDaemonEditorTuiSurfaces()
        {
            ThrowIfDisposed();
            return Database.WatchRecord<EveSurfaceState>(AetheriaRuntimeVerseRecordKeys.DaemonEditorTuiSurface);
        }

        public async Task<AetheriaRuntimeDaemonCommandEnvelope> SubmitDaemonCommandAsync(
            AetheriaRuntimeDaemonCommandDocument command,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (command == null) throw new ArgumentNullException(nameof(command));

            command.Schema = AetheriaRuntimeDaemonSchemas.Command;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(command.ClientId))
                command.ClientId = RuntimeId;
            if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
                command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.DaemonCommand(command.CommandId), command)
                .ConfigureAwait(false);
            if (flush)
                await FlushAsync().ConfigureAwait(false);

            return AetheriaRuntimeDaemonOperationClient.ToEnvelope(command);
        }

        public async Task<AetheriaRuntimeEveCommandEnvelope> SubmitEveCommandAsync(
            AetheriaRuntimeEveCommandDocument command,
            bool flush = true)
        {
            ThrowIfDisposed();
            if (command == null) throw new ArgumentNullException(nameof(command));

            AetheriaRuntimeEveCommandClient.NormalizeDocument(command);
            command.Schema = AetheriaRuntimeEveCommandDocument.SchemaId;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(command.IssuedAtUtc))
                command.IssuedAtUtc = DateTime.UtcNow.ToString("O");

            await Database.PutAsync(AetheriaRuntimeVerseRecordKeys.EveCommand(command.CommandId), command)
                .ConfigureAwait(false);
            if (flush)
                await FlushAsync().ConfigureAwait(false);

            return AetheriaRuntimeEveCommandClient.ToEnvelope(command);
        }

        public Task FlushAsync(bool soft = false)
        {
            ThrowIfDisposed();
            return _node.FlushAsync(soft);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _node.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AetheriaRuntimeVerseClient));
        }

        private AetheriaRuntimeVerseDocument<T> Document<T>(CultRecordKey key) where T : class
        {
            return new AetheriaRuntimeVerseDocument<T>(
                key,
                () => Database.GetAsync<T>(key),
                async value => { await Database.PutAsync(key, value).ConfigureAwait(false); },
                Database.WatchRecord<T>(key)
                    .Where(change => change.Document != null)
                    .Select(change => change.Document!));
        }
    }
}
