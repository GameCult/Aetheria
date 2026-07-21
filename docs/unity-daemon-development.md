# Unity daemon development

Open `Aetheria.Unity` in Unity and choose **Aetheria > Daemon Development**.
This is the interactive development path. The released-package witness remains
the automated integration proof and is not required for ordinary iteration.

The window synchronously builds `Aetheria.State.Daemon` in
`bin/Debug/net10.0` and imports an isolated development state under
`Aetheria.Unity/Build` when necessary. After preparation succeeds, the Unity
editor directly launches and owns the daemon process, its PID, and its output
streams. The generic EveUnity client connects directly to the daemon's local
CultMesh endpoint. Odin is not part of this path.

The editor connects through the daemon's `cultnet+tcp` control endpoint. The
advertised session then selects the dedicated content and QUIC realtime planes;
the editor bootstrap does not choose those planes or retain an RUDP fallback.

The launcher displays and passes the exact source dependency roots used for the
Debug daemon build. CultLib prefers the active
`CultLib-codex-cultmesh-reliability` checkout, then `CultLib-release`, and
finally the canonical `CultLib` sibling.
Ymir still prefers the active `Ymir-aetheria-integration` sibling checkout and
falls back to the canonical `Ymir` sibling when that integration checkout is
absent. This prevents an older sibling assembly from impersonating the daemon
being debugged.

## Normal loop

1. Press **Start & Play**, or press Unity Play with **Start before Play**
   enabled.
2. Edit and inspect the generic EveUnity-lowered world in Play Mode.
3. Use Unity Pause to submit the advertised `simulation.pause` action to the
   daemon. Unity unpause submits `simulation.rate.realtime`.
4. While paused, use **Advance one step** to commit exactly one fixed daemon
   simulation step.
5. Stop, restart, or reimport the isolated state from the same window.

Unity's editor clock is never gameplay authority. The pause and step controls
are ordinary advertised Eve operations accepted by the daemon. If the active
game mode does not advertise its simulation clock, the window reports that
contract failure instead of locally freezing world truth.

## Rider

The window displays the actual `Aetheria.State.Daemon` process ID. Press
**Copy daemon PID**, then in Rider choose **Run > Attach to Process** and select
that PID. **Open daemon source** opens `Aetheria.State.Daemon/Program.cs` through
Unity's configured external editor. The launched process uses Debug assemblies
from the current checkout, so breakpoints bind to the code being run.

Hitting a Rider breakpoint suspends the authoritative daemon process. The Unity
client continues to display its last committed generation until execution
resumes; it does not simulate through the breakpoint.

## Files

- State: `Aetheria.Unity/Build/aetheria-unity-dev.cc`
- Daemon stdout: `Aetheria.Unity/Build/DaemonDevelopment/daemon.log`
- Daemon stderr: `Aetheria.Unity/Build/DaemonDevelopment/daemon.error.log`
- Preparation logs: `Aetheria.Unity/Build/DaemonDevelopment/launcher*.log`

`Reimport state & start` deletes only the isolated development `.cc` file, its
`.cultmesh` (or legacy `.records`) sidecar, and the matching daemon-private
`.ymir.cc` journal store before importing it again. It does not touch
`GameData`, the source catalog, or another witness/run state.
