using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using GameCult.Eve.UnityScene;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Aetheria.Editor
{
    public sealed class AetheriaDaemonDevelopmentWindow : EditorWindow
    {
        private const string MenuPath = "Aetheria/Daemon Development";
        private const string PortPreference = "Aetheria.DaemonDevelopment.Port";
        private const string AutoStartPreference = "Aetheria.DaemonDevelopment.AutoStart";
        private const string StopAfterPlayPreference = "Aetheria.DaemonDevelopment.StopAfterPlay";

        private Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open() => GetWindow<AetheriaDaemonDevelopmentWindow>("Aetheria Daemon");

        private void OnEnable()
        {
            minSize = new Vector2(470, 390);
            AetheriaDaemonDevelopmentController.Changed += Repaint;
        }

        private void OnDisable() => AetheriaDaemonDevelopmentController.Changed -= Repaint;

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Authoritative daemon", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This window owns only the local development process. Gameplay, pause, stepping, and world truth remain daemon-owned Eve operations.",
                MessageType.Info);

            var port = EditorPrefs.GetInt(PortPreference, 3076);
            var nextPort = EditorGUILayout.IntField("CultMesh port", port);
            if (nextPort != port && nextPort is > 0 and <= 65535)
                EditorPrefs.SetInt(PortPreference, nextPort);

            var autoStart = EditorPrefs.GetBool(AutoStartPreference, true);
            var nextAutoStart = EditorGUILayout.Toggle("Start before Play", autoStart);
            if (nextAutoStart != autoStart)
                EditorPrefs.SetBool(AutoStartPreference, nextAutoStart);

            var stopAfterPlay = EditorPrefs.GetBool(StopAfterPlayPreference, false);
            var nextStopAfterPlay = EditorGUILayout.Toggle("Stop after Play", stopAfterPlay);
            if (nextStopAfterPlay != stopAfterPlay)
                EditorPrefs.SetBool(StopAfterPlayPreference, nextStopAfterPlay);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Status", AetheriaDaemonDevelopmentController.Status);
            EditorGUILayout.LabelField("Endpoint", AetheriaDaemonDevelopmentController.Endpoint);
            EditorGUILayout.LabelField("PID", AetheriaDaemonDevelopmentController.ProcessIdText);
            EditorGUILayout.LabelField("State", AetheriaDaemonDevelopmentController.StatePath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("CultLib source", AetheriaDaemonDevelopmentController.CultLibRoot, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Ymir source", AetheriaDaemonDevelopmentController.YmirRoot, EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !AetheriaDaemonDevelopmentController.IsStarting &&
                              !AetheriaDaemonDevelopmentController.IsRunning;
                if (GUILayout.Button("Start daemon"))
                    AetheriaDaemonDevelopmentController.Start(enterPlayWhenReady: false);
                if (GUILayout.Button("Start & Play"))
                    AetheriaDaemonDevelopmentController.Start(enterPlayWhenReady: true);
                GUI.enabled = AetheriaDaemonDevelopmentController.IsRunning ||
                              AetheriaDaemonDevelopmentController.IsStarting;
                if (GUILayout.Button("Stop"))
                    AetheriaDaemonDevelopmentController.Stop();
                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = AetheriaDaemonDevelopmentController.IsRunning;
                if (GUILayout.Button("Restart"))
                    AetheriaDaemonDevelopmentController.Restart();
                GUI.enabled = !AetheriaDaemonDevelopmentController.IsRunning &&
                              !AetheriaDaemonDevelopmentController.IsStarting;
                if (GUILayout.Button("Reimport state & start"))
                    AetheriaDaemonDevelopmentController.Start(enterPlayWhenReady: false, forceImport: true);
                GUI.enabled = true;
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Authoritative simulation", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = EditorApplication.isPlaying && AetheriaDaemonDevelopmentController.IsRunning;
                if (GUILayout.Button("Pause daemon"))
                    AetheriaDaemonDevelopmentController.SubmitClockAction("simulation.pause");
                if (GUILayout.Button("Advance one step"))
                    AetheriaDaemonDevelopmentController.SubmitClockAction("simulation.step");
                if (GUILayout.Button("Resume real time"))
                    AetheriaDaemonDevelopmentController.SubmitClockAction("simulation.rate.realtime");
                GUI.enabled = true;
            }
            EditorGUILayout.HelpBox(
                "Unity's Pause button is mirrored to simulation.pause. Unpause submits simulation.rate.realtime. Advance One Step remains available here while the Unity player is paused.",
                MessageType.None);

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Rider", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = AetheriaDaemonDevelopmentController.IsRunning;
                if (GUILayout.Button("Copy daemon PID"))
                    AetheriaDaemonDevelopmentController.CopyProcessId();
                GUI.enabled = true;
                if (GUILayout.Button("Open daemon source"))
                    AetheriaDaemonDevelopmentController.OpenDaemonSource();
            }
            EditorGUILayout.HelpBox(
                "In Rider use Run | Attach to Process and select the copied Aetheria.State.Daemon PID. The daemon is built and launched from bin/Debug/net10.0, so source breakpoints bind to this checkout.",
                MessageType.None);

            EditorGUILayout.Space(12);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open daemon log"))
                    AetheriaDaemonDevelopmentController.OpenLog();
                if (GUILayout.Button("Open error log"))
                    AetheriaDaemonDevelopmentController.OpenErrorLog();
            }

            if (!string.IsNullOrWhiteSpace(AetheriaDaemonDevelopmentController.LastError))
                EditorGUILayout.HelpBox(AetheriaDaemonDevelopmentController.LastError, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
    }

    [InitializeOnLoad]
    internal static class AetheriaDaemonDevelopmentController
    {
        private const string ProcessIdSessionKey = "Aetheria.DaemonDevelopment.ProcessId";
        private const string PortPreference = "Aetheria.DaemonDevelopment.Port";
        private const string AutoStartPreference = "Aetheria.DaemonDevelopment.AutoStart";
        private const string StopAfterPlayPreference = "Aetheria.DaemonDevelopment.StopAfterPlay";
        private static Process _launcher;
        private static Process _daemon;
        private static bool _enterPlayWhenReady;
        private static bool _restartWhenStopped;
        private static string _pendingClockAction = "";
        private static string _status = "Stopped";
        private static string _lastError = "";

        public static event Action Changed;

        static AetheriaDaemonDevelopmentController()
        {
            EditorApplication.update += Update;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.pauseStateChanged += OnPauseStateChanged;
            Reattach();
            ApplyClientEnvironment();
        }

        public static bool IsRunning => TryRefreshDaemon();
        public static bool IsStarting => _launcher != null && !_launcher.HasExited;
        public static string Status => _status;
        public static string LastError => _lastError;
        public static string Endpoint => $"rudp://127.0.0.1:{Port}";
        public static string ProcessIdText => IsRunning ? _daemon.Id.ToString(CultureInfo.InvariantCulture) : "-";
        public static string StatePath => Path.Combine(ProjectRoot, "Aetheria.Unity", "Build", "aetheria-unity-dev.cc");
        public static string CultLibRoot => ResolveSibling("CultLib-codex-cultmesh-reliability", "CultLib");
        public static string YmirRoot => ResolveSibling("Ymir-aetheria-integration", "Ymir");
        public static string EveUnityRoot => ResolveSibling("EveUnity");
        private static int Port => EditorPrefs.GetInt(PortPreference, 3076);
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string ArtifactsRoot => Path.Combine(ProjectRoot, "Aetheria.Unity", "Build", "DaemonDevelopment");
        private static string PidPath => Path.Combine(ArtifactsRoot, "daemon.pid");
        private static string LogPath => Path.Combine(ArtifactsRoot, "daemon.log");
        private static string ErrorLogPath => Path.Combine(ArtifactsRoot, "daemon.error.log");
        private static string LauncherLogPath => Path.Combine(ArtifactsRoot, "launcher.log");
        private static string LauncherErrorPath => Path.Combine(ArtifactsRoot, "launcher.error.log");

        public static void Start(bool enterPlayWhenReady, bool forceImport = false)
        {
            if (TryRefreshDaemon())
            {
                ApplyClientEnvironment();
                if (enterPlayWhenReady && !EditorApplication.isPlaying)
                    EditorApplication.isPlaying = true;
                return;
            }
            if (IsStarting)
            {
                _enterPlayWhenReady |= enterPlayWhenReady;
                return;
            }

            Directory.CreateDirectory(ArtifactsRoot);
            File.Delete(PidPath);
            _lastError = "";
            _enterPlayWhenReady = enterPlayWhenReady;
            ApplyClientEnvironment();

            var script = Path.Combine(ProjectRoot, "scripts", "start-aetheria-daemon-editor.ps1");
            var arguments = $"-NoProfile -ExecutionPolicy Bypass -File {Quote(script)} " +
                            $"-Root {Quote(ProjectRoot)} -State {Quote(StatePath)} -Port {Port} " +
                            $"-PidFile {Quote(PidPath)} -LogFile {Quote(LogPath)} -ErrorLogFile {Quote(ErrorLogPath)} " +
                            $"-CultLibRoot {Quote(CultLibRoot)} -YmirRoot {Quote(YmirRoot)} -EveUnityRoot {Quote(EveUnityRoot)}" +
                            (forceImport ? " -ForceImport" : "");
            _launcher = Process.Start(new ProcessStartInfo("powershell.exe", arguments)
            {
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            _launcher.OutputDataReceived += (_, value) => AppendLine(LauncherLogPath, value.Data);
            _launcher.ErrorDataReceived += (_, value) => AppendLine(LauncherErrorPath, value.Data);
            _launcher.BeginOutputReadLine();
            _launcher.BeginErrorReadLine();
            _status = forceImport ? "Reimporting state and building Debug daemon..." : "Building Debug daemon...";
            Changed?.Invoke();
        }

        public static void Stop()
        {
            _enterPlayWhenReady = false;
            _pendingClockAction = "";
            try
            {
                if (_launcher != null && !_launcher.HasExited)
                    _launcher.Kill();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not stop Aetheria daemon launcher: {exception.Message}");
            }
            try
            {
                if (TryRefreshDaemon())
                    _daemon.Kill();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not stop Aetheria daemon: {exception.Message}");
            }
            _launcher?.Dispose();
            _launcher = null;
            _daemon?.Dispose();
            _daemon = null;
            SessionState.EraseInt(ProcessIdSessionKey);
            TryDelete(PidPath);
            _status = "Stopped";
            Changed?.Invoke();
        }

        public static void Restart()
        {
            _restartWhenStopped = true;
            Stop();
        }

        public static void SubmitClockAction(string actionId)
        {
            _pendingClockAction = actionId ?? "";
            TrySubmitPendingClockAction();
        }

        public static void CopyProcessId()
        {
            if (!TryRefreshDaemon()) return;
            EditorGUIUtility.systemCopyBuffer = _daemon.Id.ToString(CultureInfo.InvariantCulture);
            _status = $"Copied daemon PID {_daemon.Id} for Rider Attach to Process";
            Changed?.Invoke();
        }

        public static void OpenDaemonSource()
        {
            var source = Path.Combine(ProjectRoot, "Aetheria.State.Daemon", "Program.cs");
            EditorUtility.OpenWithDefaultApp(source);
        }

        public static void OpenLog() => OpenFile(LogPath);
        public static void OpenErrorLog() => OpenFile(ErrorLogPath);

        private static void Update()
        {
            if (_restartWhenStopped)
            {
                _restartWhenStopped = false;
                Start(enterPlayWhenReady: EditorApplication.isPlaying);
                return;
            }

            if (_launcher != null && _launcher.HasExited)
            {
                var exitCode = _launcher.ExitCode;
                _launcher.Dispose();
                _launcher = null;
                if (exitCode != 0 && !TryRefreshDaemon())
                {
                    _lastError = ReadTail(LauncherErrorPath, 24);
                    _status = $"Daemon preparation failed (exit {exitCode})";
                    _enterPlayWhenReady = false;
                    Changed?.Invoke();
                    return;
                }
            }

            if (!TryRefreshDaemon() && File.Exists(PidPath) &&
                int.TryParse(File.ReadAllText(PidPath).Trim(), out var processId))
            {
                try
                {
                    _daemon = Process.GetProcessById(processId);
                    SessionState.SetInt(ProcessIdSessionKey, processId);
                    _status = $"Daemon {processId} starting...";
                    Changed?.Invoke();
                }
                catch (ArgumentException)
                {
                    // The launcher published a PID just as the process exited. The error log owns the explanation.
                }
            }

            if (TryRefreshDaemon() && IsEndpointReady())
            {
                if (!_status.StartsWith("Running", StringComparison.Ordinal))
                {
                    _status = $"Running Debug daemon (PID {_daemon.Id})";
                    Changed?.Invoke();
                }
                ApplyClientEnvironment();
                if (_enterPlayWhenReady && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    _enterPlayWhenReady = false;
                    EditorApplication.isPlaying = true;
                }
                TrySubmitPendingClockAction();
            }
            else if (_daemon != null && _daemon.HasExited)
            {
                var exitCode = _daemon.ExitCode;
                _daemon.Dispose();
                _daemon = null;
                SessionState.EraseInt(ProcessIdSessionKey);
                _status = $"Daemon exited ({exitCode})";
                _lastError = ReadTail(ErrorLogPath, 24);
                Changed?.Invoke();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode &&
                EditorPrefs.GetBool(AutoStartPreference, true) && !TryRefreshDaemon())
            {
                EditorApplication.isPlaying = false;
                Start(enterPlayWhenReady: true);
            }
            else if (state == PlayModeStateChange.EnteredEditMode &&
                     EditorPrefs.GetBool(StopAfterPlayPreference, false))
            {
                Stop();
            }
        }

        private static void OnPauseStateChanged(PauseState state)
        {
            if (!EditorApplication.isPlaying || !TryRefreshDaemon()) return;
            SubmitClockAction(state == PauseState.Paused
                ? "simulation.pause"
                : "simulation.rate.realtime");
        }

        private static void TrySubmitPendingClockAction()
        {
            if (string.IsNullOrWhiteSpace(_pendingClockAction) || !EditorApplication.isPlaying) return;
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<EveUnityPlayableWorldClientBootstrap>();
            var host = bootstrap?.Host;
            var entityId = host?.ActiveWorld?.PlayerEntityId;
            if (host == null || string.IsNullOrWhiteSpace(entityId) || host.InputCapability == null) return;
            try
            {
                var request = host.SubmitAdvertisedActionIntent(entityId, _pendingClockAction);
                _status = $"Submitted {_pendingClockAction} ({request.CommandId})";
                _pendingClockAction = "";
                Changed?.Invoke();
            }
            catch (Exception exception)
            {
                _lastError = $"Could not submit daemon clock action '{_pendingClockAction}': {exception.Message}";
                _pendingClockAction = "";
                Changed?.Invoke();
            }
        }

        private static bool TryRefreshDaemon()
        {
            if (_daemon == null) return false;
            try { return !_daemon.HasExited; }
            catch (InvalidOperationException) { return false; }
        }

        private static void Reattach()
        {
            var processId = SessionState.GetInt(ProcessIdSessionKey, -1);
            if (processId <= 0) return;
            try
            {
                _daemon = Process.GetProcessById(processId);
                _status = $"Reattached to Debug daemon (PID {processId})";
            }
            catch (ArgumentException)
            {
                SessionState.EraseInt(ProcessIdSessionKey);
            }
        }

        private static bool IsEndpointReady()
        {
            if (!File.Exists(LogPath)) return false;
            try
            {
                var marker = $"Aetheria client CultMesh endpoint: {Endpoint}";
                using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Contains(marker, StringComparison.Ordinal);
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void ApplyClientEnvironment()
        {
            Environment.SetEnvironmentVariable("EVEUNITY_RENDEZVOUS_ENDPOINT", Endpoint);
            Environment.SetEnvironmentVariable("EVEUNITY_SURFACE_ID", "aetheria.pilot");
            Environment.SetEnvironmentVariable("EVEUNITY_ASSET_CACHE_PATH",
                Path.Combine(ProjectRoot, "Aetheria.Unity", "Build", "AssetCache"));
        }

        private static void AppendLine(string path, string value)
        {
            if (value == null) return;
            try { File.AppendAllText(path, value + Environment.NewLine); }
            catch (IOException) { }
        }

        private static string ReadTail(string path, int maximumLines)
        {
            if (!File.Exists(path)) return "";
            try
            {
                var lines = File.ReadAllLines(path);
                return string.Join(Environment.NewLine, lines, Math.Max(0, lines.Length - maximumLines),
                    Math.Min(maximumLines, lines.Length));
            }
            catch (IOException exception)
            {
                return exception.Message;
            }
        }

        private static void OpenFile(string path)
        {
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else _lastError = $"File does not exist yet: {path}";
            Changed?.Invoke();
        }

        private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

        private static string ResolveSibling(params string[] names)
        {
            var projectsRoot = Directory.GetParent(ProjectRoot)?.FullName ?? ProjectRoot;
            foreach (var name in names)
            {
                var candidate = Path.Combine(projectsRoot, name);
                if (Directory.Exists(candidate)) return candidate;
            }
            return Path.Combine(projectsRoot, names[0]);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
        }
    }
}
