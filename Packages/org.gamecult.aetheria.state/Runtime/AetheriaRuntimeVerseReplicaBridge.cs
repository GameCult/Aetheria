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
            var repoRoot = gameDataDirectory.Parent?.FullName;
            if (string.IsNullOrWhiteSpace(repoRoot))
                throw new InvalidOperationException("Cannot resolve the Aetheria repo root from the GameData directory.");

            Directory.CreateDirectory(Path.GetDirectoryName(replicaStateFilePath) ?? gameDataDirectory.FullName);

            var startInfo = BuildStartInfo(repoRoot, endpoint, replicaStateFilePath, verseId);
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
            string repoRoot,
            string endpoint,
            string replicaStateFilePath,
            string verseId)
        {
            var executablePath = Path.Combine(repoRoot, "Aetheria.State.Replica", "bin", "Debug", "net10.0", "Aetheria.State.Replica.exe");
            if (File.Exists(executablePath))
            {
                return CreateStartInfo(
                    executablePath,
                    BuildArguments("sync", "--endpoint", endpoint, "--replica", replicaStateFilePath, "--verse-id", verseId),
                    repoRoot);
            }

            var dllPath = Path.Combine(repoRoot, "Aetheria.State.Replica", "bin", "Debug", "net10.0", "Aetheria.State.Replica.dll");
            if (File.Exists(dllPath))
            {
                return CreateStartInfo(
                    "dotnet",
                    BuildArguments(dllPath, "sync", "--endpoint", endpoint, "--replica", replicaStateFilePath, "--verse-id", verseId),
                    repoRoot);
            }

            var projectPath = Path.Combine(repoRoot, "Aetheria.State.Replica", "Aetheria.State.Replica.csproj");
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("Cannot find the Aetheria.State.Replica project.", projectPath);

            return CreateStartInfo(
                "dotnet",
                BuildArguments("run", "--project", projectPath, "--", "sync", "--endpoint", endpoint, "--replica", replicaStateFilePath, "--verse-id", verseId),
                repoRoot);
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
    }
}
