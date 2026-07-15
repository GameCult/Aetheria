using Ymir.Core;

namespace Aetheria.State.Daemon;

public sealed record AetheriaYmirZonePersistenceCapture(
    string RunId,
    int ZoneIndex,
    long FrameId,
    int SimulationStepIndex,
    YmirSessionPersistenceCapture World,
    YmirSessionPersistenceCapture? Payload);
