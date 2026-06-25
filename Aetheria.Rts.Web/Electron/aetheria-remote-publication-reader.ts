import { decode } from "@msgpack/msgpack";
import { CultMesh } from "cultmesh-ts";
import type { CultNetPeer } from "cultnet-ts";
import type {
  CultNetErrorMessage,
  CultNetSnapshotResponseRawMessage,
} from "cultnet-ts";

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
    const response = await this.requestSnapshot([recordKey]);
    const document = response.documents.find(candidate =>
      candidate.recordKey === recordKey);
    if (!document) {
      throw new Error(`Aetheria remote publication ${this.endpoint} did not return ${schemaId} at ${recordKey}.`);
    }

    return decode(toBytes(document.payload));
  }

  private async requestSnapshot(
    recordKeys: readonly string[],
  ): Promise<CultNetSnapshotResponseRawMessage> {
    const peer = await this.peer();
    const messageId = `${this.runtimeId}:snapshot:${Date.now()}:${Math.random().toString(16).slice(2)}`;

    return new Promise<CultNetSnapshotResponseRawMessage>((resolve, reject) => {
      const cleanup = (): void => {
        clearTimeout(timer);
        peer.off("message", onMessage);
        peer.off("invalidMessage", onInvalidMessage);
        peer.off("error", onError);
        peer.off("close", onClose);
      };
      const rejectWith = (error: Error): void => {
        cleanup();
        reject(error);
      };
      const onMessage = (message: unknown): void => {
        const response = message as CultNetSnapshotResponseRawMessage | CultNetErrorMessage;
        if (response.schemaVersion === "cultnet.error.v0") {
          cleanup();
          reject(new Error(response.error));
          return;
        }

        if (response.schemaVersion !== "cultnet.snapshot_response_raw.v0" ||
            response.messageId !== messageId) {
          return;
        }

        cleanup();
        resolve(response);
      };
      const onInvalidMessage = (error: Error): void => rejectWith(error);
      const onError = (error: Error): void => rejectWith(error);
      const onClose = (): void => rejectWith(new Error("CultMesh peer closed before snapshot response."));
      const timer = setTimeout(
        () => rejectWith(new Error(`Timed out waiting for Aetheria remote snapshot ${messageId}.`)),
        this.timeoutMs);

      peer.on("message", onMessage);
      peer.on("invalidMessage", onInvalidMessage);
      peer.on("error", onError);
      peer.on("close", onClose);
      peer.sendSnapshotRequest({
        schemaVersion: "cultnet.snapshot_request.v0",
        messageId,
        recordKeys: [...recordKeys],
      });
    });
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

function toBytes(value: unknown): Uint8Array {
  if (value instanceof Uint8Array)
    return value;
  if (Array.isArray(value))
    return Uint8Array.from(value);
  throw new Error("CultNet raw document payload was not binary.");
}
