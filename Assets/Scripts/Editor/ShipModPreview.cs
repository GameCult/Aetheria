using System;
using UnityEditor;
using UnityEngine;

public static class ShipModPreview
{
    // Batch proof: -executeMethod ShipModPreview.Smoke -shipModPath <package/ship.cc>
    public static async void Smoke()
    {
        var args = Environment.GetCommandLineArgs();
        var option = Array.IndexOf(args, "-shipModPath");
        if (option < 0 || option + 1 >= args.Length)
        {
            Debug.LogError("-shipModPath <package/ship.cc> is required");
            EditorApplication.Exit(1);
            return;
        }
        ShipModVisual.Instance visual = null;
        try
        {
            visual = await ShipModVisual.LoadAsync(args[option + 1], null);
            if (visual.Anchors.Count == 0 || visual.LineMesh == null || visual.LineMesh.vertexCount == 0)
                throw new InvalidOperationException("Mod visual did not import anchors and line points.");
            Debug.Log($"SHIP_MOD_VISUAL_SMOKE anchors={visual.Anchors.Count} points={visual.LineMesh.vertexCount} segments={visual.LineMesh.GetIndexCount(0) / 2}");
            visual.Destroy();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            visual?.Destroy();
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Aetheria/Preview Mod Ship Package")]
    private static async void Open()
    {
        var path = EditorUtility.OpenFilePanel("Ship authoring record", "GameData/Mods", "cc");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var visual = await ShipModVisual.LoadAsync(path, null);
            Undo.RegisterCreatedObjectUndo(visual.Root, "Preview mod ship");
            Selection.activeGameObject = visual.Root;
            SceneView.FrameLastActiveSceneView();
            Debug.Log($"Previewed {path}: {visual.Anchors.Count} anchors, {visual.LineMesh?.vertexCount ?? 0} line points.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }
}
