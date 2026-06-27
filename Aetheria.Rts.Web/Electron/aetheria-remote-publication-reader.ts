import { CultMesh } from "cultmesh-ts";
import type { CultNetPeer } from "cultnet-ts";

const connectionId = 0x43554c54;

export class AetheriaRemotePublicationReader {
  #peer: CultNetPeer | null = null;

  public constructor(
    private readonly endpoint: string,
    private readonly runtimeId = "aetheria-rts-remote-reader",
    private readonly timeoutMs = 2_500,
  ) {}

  public get statePathDescription(): string {
    return this.endpoint;
  }

  public async close(): Promise<void> {
    this.#peer?.close();
    this.#peer = null;
  }

  public readDaemonFrame(): Promise<unknown> {
    return this.readSingleDocument(
      "gamecult.aetheria.daemon_frame.v1",
      "daemon:aetheria.frame.latest.v1");
  }

  public readDaemonHealth(): Promise<unknown> {
    return this.readSingleDocument(
      "gamecult.aetheria.daemon_health.v1",
      "daemon:aetheria.health.v1");
  }

  public readAuthorityPolicy(): Promise<unknown> {
    return this.readSingleDocument(
      "gamecult.aetheria.verse_authority_policy.v1",
      "global:aetheria.verse_authority_policy.v1");
  }

  public readStarbridgeSessionSummary(): Promise<unknown> {
    return this.readSingleDocument(
      "gamecult.aetheria.starbridge_session_summary.v1",
      "daemon:aetheria.starbridge.session.latest.v1");
  }

  private async readSingleDocument(schemaId: string, recordKey: string): Promise<unknown> {
    return CultMesh.documentFromPublication(
      {
        kind: "peer-snapshot",
        peer: () => this.peer(),
        endpoint: this.endpoint,
      },
      schemaId,
      recordKey,
      {
        documentId: recordKey,
        routeHint: CultMesh.routeHint("network", this.endpoint),
        timeoutMs: this.timeoutMs,
        messageIdPrefix: `${this.runtimeId}:snapshot`,
      },
    ).latest();
  }

  private async peer(): Promise<CultNetPeer> {
    if (this.#peer)
      return this.#peer;

    this.#peer = await CultMesh.createRudpPeer(this.runtimeId, connectionId, this.endpoint, {
      connectTimeoutMs: 2_000,
      maxFragmentBytes: 1200,
      maxPendingReliablePackets: 512,
    });
    this.#peer.on("close", () => {
      this.#peer = null;
    });
    return this.#peer;
  }
}
