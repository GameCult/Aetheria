using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// A render of the New Game screen as the game builds it: the real Main Menu scene's panel prototype, docked and filled
// by ScenarioPicker.Dock and ScenarioPicker.Show with every scenario group, on a plain dark background (the cloud
// backdrop is a play-mode renderer). Writes <project>/Temp/scenario-picker.png at 1920x1080, or the path after -pngOut (Unity empties Temp on exit).
//
//   Unity -batchmode -projectPath <project> -executeMethod ScenarioPickerCapture.Run [-pngOut <file>] -logFile <log>
//
// Needs a GPU device; do not pass -nographics or -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
public static class ScenarioPickerCapture
{
    private const int Width = 1920, Height = 1080;

    public static void Run()
    {
        try
        {
            Capture();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // The menu's two panes built the way MainMenu.Start builds them, against the Main Menu scene.
    internal static (MainMenu menu, PropertiesPanel list, PropertiesPanel detail) Build()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity", OpenSceneMode.Single);
        var menu = UnityEngine.Object.FindObjectsByType<MainMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        menu.PanelPrototype.gameObject.SetActive(false);
        var list = menu.PanelPrototype.Instantiate<PropertiesPanel>();
        var detail = menu.PanelPrototype.Instantiate<PropertiesPanel>();
        // Edit mode runs no Awake, which is what activates each row a panel adds.
        list.Awake();
        detail.Awake();
        ScenarioPicker.Dock(detail, list);
        return (menu, list, detail);
    }

    internal static List<(string label, Scenario[] scenarios)> AllGroups() => new List<(string label, Scenario[] scenarios)>
    {
        ("modes", Scenarios.Modes), ("development", Scenarios.Development), ("test arenas", Scenarios.Tests)
    };

    private static void Capture()
    {
        var (menu, list, detail) = Build();
        menu.Dialog.gameObject.SetActive(false);
        list.Title.text = MainMenu.TitleSubtitle("new game", "scenarios");
        ScenarioPicker.Show(list, detail, AllGroups(), _ => { }, () => { }, menu.Dialog.Confirm);
        list.RefreshValues();
        detail.RefreshValues();

        var canvas = menu.GetComponentInParent<Canvas>(true).rootCanvas;
        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var cameraObject = new GameObject("ScenarioPickerCapture camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.04f, .05f, .08f);
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10;
        canvas.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) list.transform);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) detail.transform);
        foreach (var text in list.transform.GetComponentsInChildren<TMPro.TMP_Text>())
            text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        list.Content.GetComponentInParent<ScrollRect>().onValueChanged.Invoke(Vector2.zero);
        camera.Render();

        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var read = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        RenderTexture.active = previous;

        var background = read.GetPixel(0, 0);
        var differing = 0;
        foreach (var pixel in read.GetPixels32())
            if (Mathf.Abs(pixel.r / 255f - background.r) + Mathf.Abs(pixel.g / 255f - background.g) + Mathf.Abs(pixel.b / 255f - background.b) > .05f)
                differing++;
        if (differing < Width * Height / 100)
            throw new InvalidOperationException($"The capture is uniform: only {differing} pixels differ from the background.");

        var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "scenario-picker.png"));
        var args = Environment.GetCommandLineArgs();
        var outIndex = Array.IndexOf(args, "-pngOut");
        if (outIndex >= 0) path = Path.GetFullPath(args[outIndex + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, read.EncodeToPNG());
        Debug.Log($"[ScenarioPickerCapture] {path} ({differing} of {Width * Height} pixels differ from the background)");
    }
}
