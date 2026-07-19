using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;
using GameCult.Mesh.Quic;
using GameCult.Networking;

var controlEndpoint = args.ElementAtOrDefault(0) ?? "cultnet+tcp://127.0.0.1:3076";
var verseId = args.ElementAtOrDefault(1) ?? "aetheria.local";
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
var discovery = new CultMeshVerseDiscoveryClient();
var catalog = await discovery.FetchAsync(
    controlEndpoint,
    new CultMeshVerseCatalogRequestMessage
    {
        VerseIds = new[] { verseId },
        TransportVersion = "cultmesh.v0"
    }).ConfigureAwait(false);
var advertised = catalog.Verses
    .SingleOrDefault(candidate => string.Equals(candidate.VerseId, verseId, StringComparison.Ordinal))
    ?? throw new InvalidDataException($"Aetheria Verse '{verseId}' was not advertised by {controlEndpoint}.");
Console.WriteLine($"Aetheria advertised routes: {string.Join(", ", advertised.DiscoveryEndpoints)}");
using var client = new CultMeshClient(new CultMeshClientOptions
{
    RendezvousEndpoints = new[] { controlEndpoint },
    RealtimeConnectors = new ICultMeshRealtimeTransportConnector[]
    {
        new CultMeshQuicRealtimeTransportConnector()
    }
});
using var session = await client.ConnectRealtimeAsync(verseId, deadline.Token).ConfigureAwait(false);
var frame = await session.ReceiveAsync(deadline.Token).ConfigureAwait(false);

if (!string.Equals(frame.BodyId, AetheriaRuntimeDaemonSoaFramePublisher.BodyId, StringComparison.Ordinal) ||
    !string.Equals(frame.SchemaId, AetheriaRuntimeDaemonSoaFramePublisher.BodySchemaId, StringComparison.Ordinal) ||
    frame.Payload.IsEmpty)
    throw new InvalidDataException(
        $"Aetheria QUIC frame mismatch: body={frame.BodyId} schema={frame.SchemaId} bytes={frame.Payload.Length}.");

Console.WriteLine(
    $"Aetheria realtime transport={session.TransportId} delivery={frame.Delivery} " +
    $"epoch={frame.ProducerEpoch} sequence={frame.Sequence} bytes={frame.Payload.Length}.");
