# Unity daemon development

Open `Aetheria.Unity` in Unity and choose **Aetheria > Daemon Development**.
This is the interactive development path. The released-package witness remains
the automated integration proof and is not required for ordinary iteration.

**Build daemon** explicitly builds `Aetheria.State.Daemon` in
`bin/Debug/net10.0`. **Reimport state & build** also replaces the isolated
development state under `Aetheria.Unity/Build`. **Start daemon** launches the
already-prepared Debug apphost immediately; it never performs a build. The editor owns
the daemon process, its PID, and its output streams. The generic EveUnity
client connects directly to the daemon's local CultMesh endpoint. Odin is not
part of this path.

The named daemon apphost is launched with both system .NET runtime-root
variables pinned explicitly rather than inheriting Unity's embedded runtime
environment. This keeps .NET 10 host resolution deterministic while preserving
the `Aetheria.State.Daemon` process identity used for ownership, Rider attach,
and domain-reload reattachment.

The daemon lifecycle is independent of Unity Play Mode. Play never builds,
starts, restarts, or stops the daemon. Entering Play connects a generic client;
leaving Play drops that client while the authoritative daemon remains alive and
ready for the next connection.

Starting the daemon does not create, load, or step a game world. It starts the
typed state boundary and CultMesh transport in `ready` mode. A playable-world
subscription loads an existing run; the advertised New Game command generates
a new run. Only that activation boundary opens Ymir persistence and starts the
fixed simulation clock. A dropped client therefore leaves the daemon and saved
world alive without making Unity Play Mode a lifecycle authority.

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

1. Press **Build daemon** after changing daemon code. Use **Reimport state &
   build** only when the isolated development state must be replaced.
2. Press **Start daemon** and wait for the window to report the live endpoint.
3. Enter and leave Unity Play Mode normally. The daemon remains running across
   client sessions.
4. Edit and inspect the generic EveUnity-lowered world in Play Mode.
5. Use Unity Pause to submit the advertised `simulation.pause` action to the
   daemon. Unity unpause submits `simulation.rate.realtime`.
6. While paused, use **Advance one step** to commit exactly one fixed daemon
   simulation step.
7. Stop or restart the daemon explicitly from the same
   window.

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

`Reimport state & build` deletes only the isolated development `.cc` file, its
`.cultmesh` (or legacy `.records`) sidecar, and the matching daemon-private
`.ymir.cc` journal store before importing it again. It does not touch
`GameData`, the source catalog, or another witness/run state.
