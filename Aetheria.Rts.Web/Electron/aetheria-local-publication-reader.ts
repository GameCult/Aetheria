import { CultMesh } from "cultmesh-ts";

export class AetheriaLocalPublicationReader {
  public constructor(private readonly statePath: string) {}

  public get statePathDescription(): string {
    return this.statePath;
  }

  public readDaemonFrame(): Promise<unknown> {
    return this.readSingleDocument(
      `${this.statePath}.daemon.frame.cc`,
      "gamecult.aetheria.daemon_frame.v1",
      "daemon:aetheria.frame.latest.v1");
  }

  public readDaemonHealth(): Promise<unknown> {
    return this.readSingleDocument(
      `${this.statePath}.daemon.health.cc`,
      "gamecult.aetheria.daemon_health.v1",
      "daemon:aetheria.health.latest.v1");
  }

  public readAuthorityPolicy(): Promise<unknown> {
    return this.readSingleDocument(
      `${this.statePath}.authority.policy.cc`,
      "gamecult.aetheria.verse_authority_policy.v1",
      "daemon:aetheria.authority.policy.latest.v1");
  }

  public readStarbridgeSessionSummary(): Promise<unknown> {
    return this.readSingleDocument(
      `${this.statePath}.daemon.starbridge.session.cc`,
      "gamecult.aetheria.starbridge_session_summary.v1",
      "daemon:aetheria.starbridge.session.latest.v1");
  }

  private readSingleDocument(
    path: string,
    schemaId: string,
    documentId: string,
  ): Promise<unknown> {
    return CultMesh.documentFromPublication({
      kind: "single-file",
      path,
    }, schemaId, documentId, {
      documentId,
      sourceId: documentId,
    }).latest();
  }
}
