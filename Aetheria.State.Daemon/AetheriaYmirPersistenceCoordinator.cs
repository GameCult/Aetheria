using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using GameCult.Mesh;
using Ymir.Core;

namespace Aetheria.State.Daemon;

/// <summary>
/// Owns daemon-private Ymir restart material. The public Aetheria frame remains the
/// sole durable commit marker; private chunks and resumes are never advertised.
/// </summary>
public sealed class AetheriaYmirPersistenceCoordinator : IDisposable
{
    private const string WorldChannel = "world";
    private const string PayloadChannel = "payloads";
    private readonly CultCache _cache;
    private readonly AetheriaYmirWorldPhysics _physics;
    private readonly Dictionary<CursorKey, Cursor> _cursors = new();
    private bool _activated;

    private AetheriaYmirPersistenceCoordinator(
        CultCache cache,
        AetheriaYmirWorldPhysics physics,
        bool activated)
    {
        _cache = cache;
        _physics = physics;
        _activated = activated;
    }

    public static string PrivateStatePath(string publicStatePath) => publicStatePath + ".ymir.cc";

    public static async Task<AetheriaYmirPersistenceCoordinator> OpenAsync(
        AetheriaStateNode node,
        AetheriaYmirWorldPhysics physics,
        AetheriaRuntimeDaemonFrameDocument? frame)
    {
        var trace = string.Equals(
            Environment.GetEnvironmentVariable("AETHERIA_TRACE_STARTUP_PHASES"),
            "1",
            StringComparison.Ordinal);
        var phase = Stopwatch.StartNew();
        void Trace(string name)
        {
            if (trace)
                Console.WriteLine($"Aetheria Ymir restore phase {name} took {phase.Elapsed.TotalMilliseconds:0.###}ms.");
            phase.Restart();
        }
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(physics);
        var registry = CultMesh.CreateCultCacheDocumentRegistry(
            typeof(AetheriaYmirPersistenceMarkerDocument),
            typeof(AetheriaYmirJournalChunkDocument),
            typeof(AetheriaYmirResumeDocument));
        var ownedSchemaIds = registry.AllDescriptors
            .Select(descriptor => descriptor.SchemaId)
            .ToHashSet(StringComparer.Ordinal);
        var currentResumeKeys = frame?.Run == null || string.IsNullOrWhiteSpace(frame.Run.RunId)
            ? new HashSet<string>(StringComparer.Ordinal)
            : (frame.Run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .Select(zone => ResumeRecordKey(frame.Run.RunId, zone.ZoneIndex, frame.FrameId))
                .ToHashSet(StringComparer.Ordinal);
        var cache = await CultCacheMessagePack.OpenAsync(
            PrivateStatePath(node.StatePath),
            new CultCacheOpenOptions
            {
                Registry = registry,
                PullOnOpen = true,
                UseDirectoryStore = true,
                DirectoryStoreHydrationFilter = record =>
                    ownedSchemaIds.Contains(record.SchemaId) &&
                    (string.Equals(record.Key, AetheriaYmirPersistenceMarkerDocument.RecordKey, StringComparison.Ordinal) ||
                     currentResumeKeys.Contains(record.Key)),
                FlushOnDispose = true,
                StoreFlushOnDispose = true
            }).ConfigureAwait(false);
        Trace("open-private-cache");
        try
        {
            var activated = cache.Get<AetheriaYmirPersistenceMarkerDocument>(
                new CultRecordKey(AetheriaYmirPersistenceMarkerDocument.RecordKey)) != null;
            var coordinator = new AetheriaYmirPersistenceCoordinator(cache, physics, activated);
            if (frame?.Run == null || string.IsNullOrWhiteSpace(frame.Run.RunId))
                return coordinator;

            var zones = (frame.Run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .OrderBy(zone => zone.ZoneIndex)
                .ToArray();
            var resumes = zones
                .Select(zone => cache.Get<AetheriaYmirResumeDocument>(
                    new CultRecordKey(ResumeRecordKey(frame.Run.RunId, zone.ZoneIndex, frame.FrameId))))
                .ToArray();
            Trace("resolve-resumes");
            if (resumes.All(value => value == null) && !activated)
                return coordinator;
            if (resumes.Any(value => value == null))
                throw new InvalidOperationException(
                    $"Aetheria frame {frame.FrameId} has incomplete daemon-private Ymir resume state.");

            var journalRanges = zones.Zip(resumes, (zone, resume) =>
                    JournalRanges(frame.Run.RunId, zone.ZoneIndex, resume!))
                .SelectMany(value => value)
                .ToArray();
            await cache.PullBackingStoreRecordsAsync(metadata =>
                journalRanges.Any(range => range.Contains(metadata.Key))).ConfigureAwait(false);
            Trace("hydrate-journals");

            foreach (var pair in zones.Zip(resumes, (zone, resume) => (zone, resume: resume!)))
                coordinator.RestoreZone(frame.Run.RunId, frame.FrameId, pair.zone.ZoneIndex, pair.resume);
            Trace("restore-zones");
            return coordinator;
        }
        catch
        {
            cache.Dispose();
            throw;
        }
    }

    public IReadOnlyList<AetheriaYmirZonePersistenceCapture> Capture(
        AetheriaRuntimeDaemonFrameDocument frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var run = frame.Run ?? throw new InvalidOperationException("Ymir persistence requires an authoritative run.");
        if (string.IsNullOrWhiteSpace(run.RunId))
            throw new InvalidOperationException("Ymir persistence requires a stable run id.");
        var zones = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            .Where(zone => zone != null)
            .OrderBy(zone => zone.ZoneIndex)
            .ToArray();
        foreach (var zone in zones)
            _physics.SynchronizePersistenceFrame(
                run.RunId,
                frame.FrameId,
                zone,
                zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>());
        return zones
            .Select(zone =>
            {
                var world = CursorFor(run.RunId, zone.ZoneIndex, WorldChannel);
                var payload = CursorFor(run.RunId, zone.ZoneIndex, PayloadChannel);
                return _physics.CapturePersistence(
                    run.RunId,
                    zone.ZoneIndex,
                    frame.FrameId,
                    world.Generation,
                    world.EntryCount,
                    payload.Generation,
                    payload.EntryCount);
            })
            .ToArray();
    }

    public async Task PersistPrivateAsync(IReadOnlyList<AetheriaYmirZonePersistenceCapture> captures)
    {
        ArgumentNullException.ThrowIfNull(captures);
        foreach (var capture in captures)
        {
            await PutChunkAsync(capture, WorldChannel, capture.World.JournalChunk).ConfigureAwait(false);
            if (capture.Payload != null)
                await PutChunkAsync(capture, PayloadChannel, capture.Payload.JournalChunk).ConfigureAwait(false);
        }
        await _cache.FlushAsync(soft: false).ConfigureAwait(false);

        foreach (var capture in captures)
        {
            var world = capture.World.ResumeDescriptor;
            var payload = capture.Payload?.ResumeDescriptor;
            var resume = new AetheriaYmirResumeDocument
            {
                RunId = capture.RunId,
                ZoneIndex = capture.ZoneIndex,
                FrameId = capture.FrameId,
                SimulationStepIndex = capture.SimulationStepIndex,
                WorldSessionGeneration = world.SessionGeneration,
                WorldJournalEntryCount = world.JournalEntryCount,
                WorldDescriptorPayload = YmirSessionCheckpointCodec.Encode(world),
                PayloadSessionGeneration = payload?.SessionGeneration ?? "",
                PayloadJournalEntryCount = payload?.JournalEntryCount ?? 0,
                PayloadDescriptorPayload = payload == null ? [] : YmirSessionCheckpointCodec.Encode(payload)
            };
            await PutImmutableAsync(
                new CultRecordKey(ResumeRecordKey(capture.RunId, capture.ZoneIndex, capture.FrameId)),
                resume,
                Equivalent).ConfigureAwait(false);
        }
        await _cache.FlushAsync(soft: false).ConfigureAwait(false);

        foreach (var capture in captures)
        {
            Remember(capture.RunId, capture.ZoneIndex, WorldChannel, capture.World.ResumeDescriptor);
            if (capture.Payload != null)
                Remember(capture.RunId, capture.ZoneIndex, PayloadChannel, capture.Payload.ResumeDescriptor);
        }
    }

    /// <summary>
    /// Activates fail-closed restart only after the matching public frame has been hard-flushed.
    /// </summary>
    public async Task ActivateAsync()
    {
        if (_activated)
            return;
        await _cache.UpsertAsync(
            new AetheriaYmirPersistenceMarkerDocument
            {
                ActivatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            },
            new CultRecordHandle<AetheriaYmirPersistenceMarkerDocument>(
                new CultRecordKey(AetheriaYmirPersistenceMarkerDocument.RecordKey))).ConfigureAwait(false);
        await _cache.FlushAsync(soft: false).ConfigureAwait(false);
        _activated = true;
    }

    public static string ResumeRecordKey(string runId, int zoneIndex, long frameId) =>
        $"private:aetheria.ymir.resume.v1:{RunKey(runId)}:{zoneIndex}:{frameId:D20}";

    public void Dispose() => _cache.Dispose();

    private async Task PutChunkAsync(
        AetheriaYmirZonePersistenceCapture capture,
        string channel,
        YmirSessionJournalChunk? chunk)
    {
        if (chunk == null)
            return;
        var document = new AetheriaYmirJournalChunkDocument
        {
            RunId = capture.RunId,
            ZoneIndex = capture.ZoneIndex,
            Channel = channel,
            SessionGeneration = chunk.SessionGeneration,
            FirstEntryIndex = chunk.FirstEntryIndex,
            EntryCount = chunk.Entries.Count,
            Payload = YmirSessionCheckpointCodec.Encode(chunk)
        };
        await PutImmutableAsync(
            new CultRecordKey(JournalRecordKey(
                capture.RunId,
                capture.ZoneIndex,
                channel,
                chunk.SessionGeneration,
                chunk.FirstEntryIndex)),
            document,
            Equivalent).ConfigureAwait(false);
    }

    private async Task PutImmutableAsync<T>(
        CultRecordKey key,
        T document,
        Func<T, T, bool> equivalent)
        where T : class
    {
        var existing = _cache.Get<T>(key);
        if (existing != null)
        {
            if (!equivalent(existing, document))
                throw new InvalidOperationException($"Immutable Ymir persistence record {key.Value} conflicts with durable state.");
            return;
        }
        await _cache.UpsertAsync(document, new CultRecordHandle<T>(key)).ConfigureAwait(false);
    }

    private void RestoreZone(string runId, long frameId, int zoneIndex, AetheriaYmirResumeDocument resume)
    {
        if (!string.Equals(resume.RunId, runId, StringComparison.Ordinal) ||
            resume.ZoneIndex != zoneIndex || resume.FrameId != frameId ||
            resume.WorldDescriptorPayload.Length == 0)
            throw new InvalidOperationException("Aetheria private Ymir resume identity does not match the durable frame.");
        var world = YmirSessionCheckpointCodec.DecodeResumeDescriptor(resume.WorldDescriptorPayload);
        var payload = resume.PayloadDescriptorPayload.Length == 0
            ? null
            : YmirSessionCheckpointCodec.DecodeResumeDescriptor(resume.PayloadDescriptorPayload);
        if (!string.Equals(world.SessionGeneration, resume.WorldSessionGeneration, StringComparison.Ordinal) ||
            world.JournalEntryCount != resume.WorldJournalEntryCount ||
            !string.Equals(payload?.SessionGeneration ?? "", resume.PayloadSessionGeneration, StringComparison.Ordinal) ||
            (payload?.JournalEntryCount ?? 0) != resume.PayloadJournalEntryCount)
            throw new InvalidOperationException("Aetheria private Ymir resume metadata disagrees with its checksummed payload.");

        var worldChunks = Chunks(runId, zoneIndex, WorldChannel, world.SessionGeneration, world.JournalEntryCount);
        var payloadChunks = payload == null
            ? Array.Empty<YmirSessionJournalChunk>()
            : Chunks(runId, zoneIndex, PayloadChannel, payload.SessionGeneration, payload.JournalEntryCount);
        _physics.RestorePersistence(
            runId, zoneIndex, frameId, resume.SimulationStepIndex,
            world, worldChunks, payload, payloadChunks);
        Remember(runId, zoneIndex, WorldChannel, world);
        if (payload != null)
            Remember(runId, zoneIndex, PayloadChannel, payload);
    }

    private YmirSessionJournalChunk[] Chunks(
        string runId,
        int zoneIndex,
        string channel,
        string generation,
        long entryCount) =>
        _cache.GetAll<AetheriaYmirJournalChunkDocument>()
            .Where(value => value != null &&
                string.Equals(value.RunId, runId, StringComparison.Ordinal) &&
                value.ZoneIndex == zoneIndex &&
                string.Equals(value.Channel, channel, StringComparison.Ordinal) &&
                string.Equals(value.SessionGeneration, generation, StringComparison.Ordinal) &&
                value.FirstEntryIndex < entryCount)
            .OrderBy(value => value.FirstEntryIndex)
            .Select(value =>
            {
                var chunk = YmirSessionCheckpointCodec.DecodeJournalChunk(value.Payload);
                if (chunk.FirstEntryIndex != value.FirstEntryIndex || chunk.Entries.Count != value.EntryCount)
                    throw new InvalidOperationException("Aetheria Ymir journal metadata disagrees with its checksummed payload.");
                return chunk;
            })
            .ToArray();

    private Cursor CursorFor(string runId, int zoneIndex, string channel) =>
        _cursors.TryGetValue(new CursorKey(runId, zoneIndex, channel), out var cursor)
            ? cursor
            : new Cursor("", 0);

    private void Remember(
        string runId,
        int zoneIndex,
        string channel,
        YmirSessionResumeDescriptor descriptor) =>
        _cursors[new CursorKey(runId, zoneIndex, channel)] =
            new Cursor(descriptor.SessionGeneration, descriptor.JournalEntryCount);

    private static bool Equivalent(AetheriaYmirJournalChunkDocument left, AetheriaYmirJournalChunkDocument right) =>
        left.RunId == right.RunId && left.ZoneIndex == right.ZoneIndex && left.Channel == right.Channel &&
        left.SessionGeneration == right.SessionGeneration && left.FirstEntryIndex == right.FirstEntryIndex &&
        left.EntryCount == right.EntryCount && left.Payload.AsSpan().SequenceEqual(right.Payload);

    private static bool Equivalent(AetheriaYmirResumeDocument left, AetheriaYmirResumeDocument right) =>
        left.RunId == right.RunId && left.ZoneIndex == right.ZoneIndex && left.FrameId == right.FrameId &&
        left.SimulationStepIndex == right.SimulationStepIndex &&
        left.WorldSessionGeneration == right.WorldSessionGeneration &&
        left.WorldJournalEntryCount == right.WorldJournalEntryCount &&
        left.WorldDescriptorPayload.AsSpan().SequenceEqual(right.WorldDescriptorPayload) &&
        left.PayloadSessionGeneration == right.PayloadSessionGeneration &&
        left.PayloadJournalEntryCount == right.PayloadJournalEntryCount &&
        left.PayloadDescriptorPayload.AsSpan().SequenceEqual(right.PayloadDescriptorPayload);

    private static string JournalRecordKey(
        string runId,
        int zoneIndex,
        string channel,
        string generation,
        long firstEntryIndex) =>
        $"private:aetheria.ymir.journal.v1:{RunKey(runId)}:{zoneIndex}:{channel}:{generation}:{firstEntryIndex:D20}";

    private static JournalRange[] JournalRanges(
        string runId,
        int zoneIndex,
        AetheriaYmirResumeDocument resume)
    {
        var world = YmirSessionCheckpointCodec.DecodeResumeDescriptor(resume.WorldDescriptorPayload);
        var ranges = new List<JournalRange>
        {
            new(JournalRecordPrefix(runId, zoneIndex, WorldChannel, world.SessionGeneration), world.JournalEntryCount)
        };
        if (resume.PayloadDescriptorPayload.Length > 0)
        {
            var payload = YmirSessionCheckpointCodec.DecodeResumeDescriptor(resume.PayloadDescriptorPayload);
            ranges.Add(new JournalRange(
                JournalRecordPrefix(runId, zoneIndex, PayloadChannel, payload.SessionGeneration),
                payload.JournalEntryCount));
        }
        return ranges.ToArray();
    }

    private static string JournalRecordPrefix(
        string runId,
        int zoneIndex,
        string channel,
        string generation) =>
        $"private:aetheria.ymir.journal.v1:{RunKey(runId)}:{zoneIndex}:{channel}:{generation}:";

    private static string RunKey(string runId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(runId))).ToLowerInvariant();

    private readonly record struct CursorKey(string RunId, int ZoneIndex, string Channel);
    private readonly record struct Cursor(string Generation, long EntryCount);
    private readonly record struct JournalRange(string Prefix, long EntryCount)
    {
        public bool Contains(string key) =>
            key.StartsWith(Prefix, StringComparison.Ordinal) &&
            long.TryParse(key.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var firstEntryIndex) &&
            firstEntryIndex < EntryCount;
    }
}
