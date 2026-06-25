using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GameCult.Aetheria.State.Verse
{
    public readonly struct AetheriaRuntimeVerseReplicaSyncResult
    {
        public AetheriaRuntimeVerseReplicaSyncResult(
            string replicaStateFilePath,
            string standardOutput,
            string standardError)
        {
            ReplicaStateFilePath = replicaStateFilePath ?? "";
            StandardOutput = standardOutput ?? "";
            StandardError = standardError ?? "";
        }

        public string ReplicaStateFilePath { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
    }

    public static class AetheriaRuntimeVerseReplicaBridge
    {
        public const string ReplicaWorkerPathEnvironmentVariable = "AETHERIA_REPLICA_WORKER_PATH";

        public static AetheriaRuntimeVerseReplicaSyncResult Sync(
            DirectoryInfo gameDataDirectory,
            AetheriaRuntimeClientTargetDocument target,
            TimeSpan? timeout = null)
        {
            if (gameDataDirectory == null) throw new ArgumentNullException(nameof(gameDataDirectory));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var endpoint = target.CultMeshAddress ?? "";
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new InvalidOperationException("Cannot sync a Verse replica without a CultMesh endpoint.");

            var verseId = string.IsNullOrWhiteSpace(target.VerseId) ? "unknown-verse" : target.VerseId;
            var replicaStateFilePath = string.IsNullOrWhiteSpace(target.ReplicaStateFilePath)
                ? AetheriaRuntimeStateBoundary.GetReplicaStateFilePath(gameDataDirectory, verseId)
                : Path.GetFullPath(target.ReplicaStateFilePath);

            Directory.CreateDirectory(Path.GetDirectoryName(replicaStateFilePath) ?? gameDataDirectory.FullName);

            var startInfo = BuildStartInfo(gameDataDirectory, endpoint, replicaStateFilePath, verseId);
            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                    output.AppendLine(eventArgs.Data);
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                    error.AppendLine(eventArgs.Data);
            };

            if (!process.Start())
                throw new InvalidOperationException("Failed to start the Aetheria Verse replica sync process.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var waitTimeout = timeout ?? TimeSpan.FromSeconds(90);
            if (!process.WaitForExit((int)Math.Ceiling(waitTimeout.TotalMilliseconds)))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }

                throw new TimeoutException(
                    $"Timed out waiting {waitTimeout.TotalSeconds:0} seconds for the Aetheria Verse replica sync process.");
            }

            process.WaitForExit();
            var outputText = output.ToString().Trim();
            var errorText = error.ToString().Trim();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(errorText)
                        ? (string.IsNullOrWhiteSpace(outputText)
                            ? $"Aetheria Verse replica sync exited with code {process.ExitCode}."
                            : outputText)
                        : errorText);
            }

            return new AetheriaRuntimeVerseReplicaSyncResult(replicaStateFilePath, outputText, errorText);
        }

        private static ProcessStartInfo BuildStartInfo(
            DirectoryInfo gameDataDirectory,
            string endpoint,
            string replicaStateFilePath,
            string verseId)
        {
            var baselineStateFilePath = AetheriaRuntimeStateBoundary.GetStateFilePath(gameDataDirectory);
            foreach (var candidate in EnumerateWorkerCandidates(gameDataDirectory))
            {
                var path = candidate.Path;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                var workingDirectory = string.IsNullOrWhiteSpace(candidate.WorkingDirectory)
                    ? (Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory())
                    : candidate.WorkingDirectory;

                var extension = Path.GetExtension(path);
                if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateStartInfo(
                        path,
                        BuildArguments("sync", "--endpoint", endpoint, "--replica", replicaStateFilePath, "--verse-id", verseId, "--baseline-state", baselineStateFilePath),
                        workingDirectory);
                }

                if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateStartInfo(
                        "dotnet",
                        BuildArguments(path, "sync", "--endpoint", endpoint, "--replica", replicaStateFilePath, "--verse-id", verseId, "--baseline-state", baselineStateFilePath),
                        workingDirectory);
                }

                if (string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateStartInfo(
                        "dotnet",
                        BuildArguments("run", "--project", path, "--", "sync", "--endpoint", endpoint, "--replica", replicaStateFilePath, "--verse-id", verseId, "--baseline-state", baselineStateFilePath),
                        workingDirectory);
                }
            }

            throw new FileNotFoundException(
                $"Cannot find the Aetheria.State.Replica worker. Publish it beside the Unity build, keep the repo-local project available, or set {ReplicaWorkerPathEnvironmentVariable}.");
        }

        private static WorkerCandidate[] EnumerateWorkerCandidates(DirectoryInfo gameDataDirectory)
        {
            var candidates = new System.Collections.Generic.List<WorkerCandidate>();
            var configured = Environment.GetEnvironmentVariable(ReplicaWorkerPathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var configuredPath = Path.GetFullPath(configured);
                if (Directory.Exists(configuredPath))
                {
                    AddPublishedWorkerCandidates(candidates, configuredPath);
                    AddRepoWorkerCandidates(candidates, configuredPath);
                }
                else
                {
                    candidates.Add(new WorkerCandidate(
                        configuredPath,
                        Path.GetDirectoryName(configuredPath) ?? Directory.GetCurrentDirectory()));
                }
            }

            foreach (var root in EnumerateSearchRoots(gameDataDirectory))
            {
                AddPublishedWorkerCandidates(candidates, root);
                AddRepoWorkerCandidates(candidates, root);
            }

            return candidates
                .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        private static System.Collections.Generic.IEnumerable<string> EnumerateSearchRoots(DirectoryInfo gameDataDirectory)
        {
            var roots = new[]
            {
                gameDataDirectory?.Parent?.FullName,
                AppDomain.CurrentDomain.BaseDirectory,
                Directory.GetCurrentDirectory()
            };

            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                var directory = new DirectoryInfo(Path.GetFullPath(root));
                for (var depth = 0; depth < 6 && directory != null; depth++, directory = directory.Parent)
                    yield return directory.FullName;
            }
        }

        private static void AddPublishedWorkerCandidates(
            System.Collections.Generic.List<WorkerCandidate> candidates,
            string root)
        {
            AddWorkerCandidate(candidates, root, Path.Combine(root, "Aetheria.State.Replica.exe"));
            AddWorkerCandidate(candidates, root, Path.Combine(root, "Aetheria.State.Replica.dll"));
            AddWorkerCandidate(candidates, root, Path.Combine(root, "Aetheria.State.Replica", "Aetheria.State.Replica.exe"));
            AddWorkerCandidate(candidates, root, Path.Combine(root, "Aetheria.State.Replica", "Aetheria.State.Replica.dll"));
        }

        private static void AddRepoWorkerCandidates(
            System.Collections.Generic.List<WorkerCandidate> candidates,
            string root)
        {
            var projectRoot = Path.Combine(root, "Aetheria.State.Replica");
            AddWorkerCandidate(candidates, root, Path.Combine(projectRoot, "bin", "Debug", "net10.0", "Aetheria.State.Replica.exe"));
            AddWorkerCandidate(candidates, root, Path.Combine(projectRoot, "bin", "Debug", "net10.0", "Aetheria.State.Replica.dll"));
            AddWorkerCandidate(candidates, root, Path.Combine(projectRoot, "Aetheria.State.Replica.csproj"));
        }

        private static void AddWorkerCandidate(
            System.Collections.Generic.List<WorkerCandidate> candidates,
            string workingDirectory,
            string path)
        {
            candidates.Add(new WorkerCandidate(path, workingDirectory));
        }

        private static ProcessStartInfo CreateStartInfo(string fileName, string arguments, string workingDirectory)
        {
            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        private static string BuildArguments(params string[] values)
        {
            return string.Join(" ", values.Select(QuoteArgument));
        }

        private static string QuoteArgument(string value)
        {
            var text = value ?? "";
            if (text.Length == 0)
                return "\"\"";

            if (text.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) < 0)
                return text;

            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private readonly struct WorkerCandidate
        {
            public WorkerCandidate(string path, string workingDirectory)
            {
                Path = path ?? "";
                WorkingDirectory = workingDirectory ?? "";
            }

            public string Path { get; }
            public string WorkingDirectory { get; }
        }
    }
}
