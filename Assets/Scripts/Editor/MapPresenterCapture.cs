using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Proof that a planet icon and a ping tint reach the pixels of the real map cameras when the planet's well is deeper
// than 1000 below the camera. The ARPG scene's own "Map Overlays" and "Map Tint" cameras (position, orthographic size,
// clip planes and culling mask as authored) render into a render texture on a solid black background. For each, in
// order: a control render with nothing drawn; a render with the thing placed 1100 below the camera (a plain quad, and the
// PingEffect prefab), which must equal the control because the far plane clips it; then MapPresenter's draw of the sun
// (well deeper than 2000) or the PingEffect prefab placed where EntityInstance places it, which must differ from the
// control at its pixel. A capture that draws nothing fails the last check, and one that cannot tell drawn from clipped
// fails the middle one.
//
//   Unity -batchmode -projectPath <project> -executeMethod MapPresenterCapture.Run -logFile <log>
//
// Needs a GPU device; do not pass -nographics or -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
public static class MapPresenterCapture
{
    private const int Size = 64;

    public static void Run()
    {
        var work = Path.Combine(Path.GetTempPath(), $"map-presenter-capture-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(work);
            Check(work);
            Debug.Log("[MapPresenterCapture] ok");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException(what);
    }

    private static Camera SceneCamera(string name)
    {
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (camera.name == name) return camera;
        throw new InvalidOperationException($"No camera named {name} in the scene.");
    }

    private static Color Render(Camera camera, RenderTexture target, Vector3 at)
    {
        camera.targetTexture = target;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.Render();
        var viewport = camera.WorldToViewportPoint(new Vector3(at.x, 0, at.z));
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        RenderTexture.active = previous;
        var pixel = read.GetPixel(Mathf.Clamp((int)(viewport.x * Size), 0, Size - 1), Mathf.Clamp((int)(viewport.y * Size), 0, Size - 1));
        UnityEngine.Object.DestroyImmediate(read);
        return pixel;
    }

    private static float Distance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

    private static void Check(string work)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ARPG.unity", OpenSceneMode.Single);
        var f = MapPresenterSmoke.Build(work, flatIcons: true);
        f.Map.SetMinimapScale(300f);
        var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        var layer = LayerMask.NameToLayer("Minimap");

        // Planet: the sun's icon through the map overlays camera.
        var overlays = SceneCamera("Map Overlays");
        Require((overlays.cullingMask & (1 << layer)) != 0, "The Map Overlays camera does not see the Minimap layer.");
        var well = f.Zone.GetHeight(DrawAhead.Orbit(f.Zone, f.Zone.PlanetInstances[f.SunKey].BodyData.Orbit.Key, 0), 0);
        Require(well < -1000f, $"The fixture sun's well is {well}, not deeper than 1000.");
        Require(f.Map.TryDrawn(f.SunKey, out var sun), "The sun registered no icon.");
        var sunAt = sun.GetPosition();
        // Control: the pixel with nothing queued. Then a quad 1100 below the camera, deeper than the far plane, which must
        // leave the pixel as it was. Then the presenter's draw, which must change it. Draws queue for the cameras that
        // render after them, so the order is empty, deep, drawn.
        var empty = Render(overlays, target, sunAt);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var deepMesh = quad.GetComponent<MeshFilter>().sharedMesh;
        var deepMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.white };
        UnityEngine.Object.DestroyImmediate(quad);
        Graphics.RenderMesh(new RenderParams(deepMaterial) { layer = layer }, deepMesh, 0,
            Matrix4x4.TRS(new Vector3(sunAt.x, -1100f, sunAt.z), Quaternion.Euler(90, 0, 0), Vector3.one * 300f));
        var deep = Render(overlays, target, sunAt);
        Require(Distance(deep, empty) < .01f, $"A mesh 1100 below the camera drew: {deep} against {empty}.");
        f.Map.Draw(f.Zone, 0);
        var drawn = Render(overlays, target, sunAt);
        Require(Distance(drawn, empty) > .3f, $"The sun's icon is not in the pixels: drawn {drawn}, empty {empty}.");

        // Ping tint: the PingEffect prefab where EntityInstance places it, through the map tint camera.
        var tint = SceneCamera("Map Tint");
        var ping = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Content/Prefabs/PingEffect.prefab"));
        ping.transform.localScale = Vector3.one * 300f;
        var pingAt = new Vector3(sunAt.x, MapPresenter.PlaneY, sunAt.z);
        ping.transform.position = pingAt;
        ping.SetActive(false);
        var untinted = Render(tint, target, pingAt);
        ping.SetActive(true);
        ping.transform.position = new Vector3(sunAt.x, -1100f, sunAt.z);
        var deepPing = Render(tint, target, pingAt);
        Require(Distance(deepPing, untinted) < .01f, $"A ping 1100 below the camera drew: {deepPing} against {untinted}.");
        ping.transform.position = pingAt;
        var tinted = Render(tint, target, pingAt);
        Require(Distance(tinted, untinted) > .02f, $"The ping tint is not in the pixels: with {tinted}, without {untinted}.");
        Debug.Log($"[MapPresenterCapture] well={well} sun drawn={drawn} empty={empty} deep={deep}; ping tinted={tinted} untinted={untinted} deep={deepPing}");
    }
}
