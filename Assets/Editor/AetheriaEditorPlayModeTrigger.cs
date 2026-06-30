using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AetheriaEditorPlayModeTrigger
{
    private const string FlagPath = "Temp/request-playmode.flag";
    private const string ScreenshotFlagPath = "Temp/request-screenshot.flag";
    private const string ScreenshotPath = "Temp/aetheria-game-view-capture.png";
    private const string ScreenshotStatusPath = "Temp/aetheria-game-view-capture-status.txt";
    private const string EnsureUrpFlagPath = "Temp/request-ensure-urp.flag";

    static AetheriaEditorPlayModeTrigger()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (TryConsumeFlag(FlagPath))
        {
            Debug.Log("Aetheria editor playmode trigger consumed Temp/request-playmode.flag.");
            RequestPlayMode();
        }

        if (TryConsumeFlag(ScreenshotFlagPath))
        {
            EditorApplication.delayCall += CaptureScreenshot;
        }

        if (TryConsumeFlag(EnsureUrpFlagPath))
        {
            Debug.Log("Aetheria editor URP bootstrap trigger consumed Temp/request-ensure-urp.flag.");
            EditorApplication.delayCall += AetheriaUrpBootstrap.EnsureUrpAssetsAndAssign;
        }
    }

    private static bool TryConsumeFlag(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void RequestPlayMode()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RequestPlayMode;
            return;
        }

        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.isPlaying = true;
    }

    private static void CaptureScreenshot()
    {
        File.WriteAllText(
            ScreenshotStatusPath,
            $"CaptureScreenshot invoked. compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating} playing={EditorApplication.isPlaying}");

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CaptureScreenshot;
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            RequestPlayMode();
            EditorApplication.delayCall += CaptureScreenshot;
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
        ScreenCapture.CaptureScreenshot(ScreenshotPath);
        File.WriteAllText(
            ScreenshotStatusPath,
            $"CaptureScreenshot requested {ScreenshotPath} at {System.DateTime.UtcNow:O}");
        Debug.Log($"Aetheria editor screenshot trigger captured {ScreenshotPath}.");
    }
}
