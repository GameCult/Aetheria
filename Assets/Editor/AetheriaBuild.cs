using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

// The one way to make a Win64 player directory. Owns what the directory contains: Aetheria.exe and its data,
// GameData/Aetheria.cc, GameData/Narrative/**, GameData/Mods/** and ModTools/AetherDb. Manual builds, smokes and
// release cuts all call this and stage nothing themselves.
//
//   Unity -batchmode -quit -projectPath <repo> -executeMethod AetheriaBuild.Build
//         -aetheriaVersion <semver> -aetheriaOut <dir> -logFile <log>
//
// AetherDb is published from tools/AetherDb, so the headless build's CULTLIB_ROOT / CULTMATH_ROOT (Directory.Build.props)
// must be set for dotnet when the defaults do not hold the pinned revisions.
// Any failure logs "AetheriaBuild FAILED at <step>" and exits 1.
public static class AetheriaBuild
{
    private static readonly Regex SemVer = new Regex(@"^\d+\.\d+\.\d+([-+][0-9A-Za-z.-]+)?$");

    public static void Build()
    {
        var step = "arguments";
        try
        {
            var version = Argument("-aetheriaVersion");
            if (!SemVer.IsMatch(version)) throw new InvalidOperationException("-aetheriaVersion is not a semver");
            var output = Path.GetFullPath(Argument("-aetheriaOut"));
            var repoRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;

            step = "version";
            UnityEditor.PlayerSettings.bundleVersion = version; // the game has its own global PlayerSettings

            step = "addressables";
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) throw new InvalidOperationException("Addressables build reported an error: " + content.Error);

            step = "player";
            Directory.CreateDirectory(output);
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("EditorBuildSettings has no enabled scene");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(output, "Aetheria.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                throw new InvalidOperationException($"BuildPlayer ended {report.summary.result} with {report.summary.totalErrors} errors");

            step = "gamedata";
            PlayerStaging.Stage(repoRoot, output);

            step = "aetherdb";
            Publish(repoRoot, Path.Combine(output, "ModTools"));

            step = "burst";
            var burst = Path.Combine(output, "Aetheria_BurstDebugInformation_DoNotShip");
            if (Directory.Exists(burst)) Directory.Delete(burst, true);

            Debug.Log($"AetheriaBuild OK {version} {output}");
        }
        catch (Exception error)
        {
            Debug.LogError($"AetheriaBuild FAILED at {step}: {error.GetType().Name}: {error.Message}");
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.Exit(0);
    }

    private static void Publish(string repoRoot, string modTools)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            Arguments = $"publish tools/AetherDb -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o \"{modTools}\"",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using (var process = Process.Start(info))
        {
            var errors = process.StandardError.ReadToEndAsync();
            var text = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"dotnet publish exited {process.ExitCode}:\n{text}\n{errors.Result}");
        }
        if (!File.Exists(Path.Combine(modTools, "AetherDb.exe")))
            throw new InvalidOperationException("dotnet publish produced no ModTools/AetherDb.exe");
    }

    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(args, name);
        if (at < 0 || at + 1 >= args.Length) throw new InvalidOperationException(name + " is required");
        return args[at + 1];
    }
}
