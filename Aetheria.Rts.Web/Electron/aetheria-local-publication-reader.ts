import { decode } from "@msgpack/msgpack";
import { SingleFileMessagePackBackingStore } from "cultcache-ts";

export class AetheriaLocalPublicationReader {
  public constructor(private readonly statePath: string) {}

  public get statePathDescription(): string {
    return this.statePath;
  }

  public readDaemonFrame(): Promise<unknown> {
    return this.readSingleDocument(`${this.statePath}.daemon.frame.cc`, "gamecult.aetheria.daemon_frame.v1");
  }

  public readDaemonHealth(): Promise<unknown> {
    return this.readSingleDocument(`${this.statePath}.daemon.health.cc`, "gamecult.aetheria.daemon_health.v1");
  }

  public readAuthorityPolicy(): Promise<unknown> {
    return this.readSingleDocument(`${this.statePath}.authority.policy.cc`, "gamecult.aetheria.verse_authority_policy.v1");
  }

  public readStarbridgeSessionSummary(): Promise<unknown> {
    return this.readSingleDocument(
      `${this.statePath}.daemon.starbridge.session.cc`,
      "gamecult.aetheria.starbridge_session_summary.v1");
  }

  private async readSingleDocument(path: string, schemaId: string): Promise<unknown> {
    const records = await new SingleFileMessagePackBackingStore(path).pullAll();
    const record = records.find(candidate => candidate.schemaId === schemaId);
    if (!record)
      throw new Error(`Aetheria local publication ${path} did not contain schema ${schemaId}.`);
    return decode(record.payload);
  }
}
