using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The picker's selection and keyboard rules, driven through the events the game delivers: Move and Cancel reach the
// focused Launch button through ExecuteEvents, as the Input System UI module sends them, and a click is Button.onClick.
//
//   Unity -batchmode -projectPath <project> -executeMethod ScenarioPickerVerify.Run -logFile <log>
//
// Exits the Editor itself, 0 when every rule holds and 1 on the first that does not.
public static class ScenarioPickerVerify
{
    private sealed class Fake : Scenario
    {
        private readonly uint _seed;
        private readonly bool _ambient;
        public Fake(string name, uint seed = 0, bool ambient = true) { Name = name; _seed = seed; _ambient = ambient; }
        public override string Name { get; }
        public override string Id => Name.ToLowerInvariant();
        public override string Brief => $"brief of {Name}";
        public override uint Seed => _seed;
        public override bool Ambient => _ambient;
        public override Galaxy Generate(GalaxyStage stage) => throw new NotSupportedException();
        public override void Stage(ScenarioStage stage) => throw new NotSupportedException();
    }

    private sealed class Screen
    {
        public ScenarioPicker Picker;
        public PropertiesPanel List, Detail;
        public Button[] Rows;
        public Button Back, Launch;
        public readonly List<Scenario> Launched = new List<Scenario>();
        public int Backs;
        public Scenario[] A, B, C;
    }

    public static void Run()
    {
        try
        {
            SelectionFollowsClicksAndArrows();
            ArrowsStopAtTheEnds();
            LeftAndRightAreIgnored();
            LaunchIsOnePathAndFocusClears();
            CancelAndBackAreOnePath();
            DetailPaneFollowsSelection();
            OneGroupHasNoSectionLabel();
            LaunchesOnceHoweverItIsReached();
            ArrowingThroughTheRealListKeepsTheSelectionVisible();
            Debug.Log("[ScenarioPickerVerify] ok");
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

    private static Screen Build(bool oneGroup = false, bool real = false)
    {
        var (menu, list, detail) = ScenarioPickerCapture.Build();
        // Build opens the scene afresh, which destroys the previous EventSystem.
        var events = new GameObject("verify EventSystem").AddComponent<EventSystem>();
        // Edit mode never calls OnEnable, which is what registers an EventSystem as current.
        typeof(EventSystem).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(events, null);
        var screen = new Screen
        {
            List = list, Detail = detail,
            A = new Scenario[] { new Fake("Alpha", 7), new Fake("Beta") },
            B = new Scenario[] { new Fake("Gamma", ambient: false) },
            C = new Scenario[] { new Fake("Delta"), new Fake("Echo") }
        };
        var groups = real ? ScenarioPickerCapture.AllGroups() : oneGroup
            ? new List<(string label, Scenario[] scenarios)> { ("modes", screen.A) }
            : new List<(string label, Scenario[] scenarios)> { ("modes", screen.A), ("development", screen.B), ("test arenas", screen.C) };
        screen.Picker = ScenarioPicker.Show(list, detail, groups, screen.Launched.Add, () => screen.Backs++, menu.Dialog.Confirm);
        var buttons = list.GetComponentsInChildren<PropertyButton>(false).Where(button => !button.transform.IsChildOf(detail.transform)).Select(button => button.Button).ToArray();
        screen.Rows = buttons.Take(buttons.Length - 1).ToArray();
        screen.Back = buttons.Last();
        screen.Launch = detail.GetComponentsInChildren<Button>(false).Single();
        if (real) Layout(menu, list, detail);
        return screen;
    }

    // A live 1920x1080 layout, as the game has one a frame after Show; edit mode has no frames, so it is forced here.
    private static void Layout(MainMenu menu, PropertiesPanel list, PropertiesPanel detail)
    {
        menu.Dialog.gameObject.SetActive(false);
        var canvas = menu.GetComponentInParent<Canvas>(true).rootCanvas;
        var camera = new GameObject("verify camera").AddComponent<Camera>();
        camera.targetTexture = new RenderTexture(1920, 1080, 24);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10;
        canvas.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) canvas.transform);
        foreach (var text in list.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        Scroll(list).onValueChanged.Invoke(Vector2.zero);
    }

    private static ScrollRect Scroll(PropertiesPanel list) => list.Content.GetComponentInParent<ScrollRect>();

    // Wholly inside the viewport, in world space, and not hidden by FitViewport's CanvasGroup.
    private static bool Shown(Button row, ScrollRect scroll)
    {
        var view = new Vector3[4];
        ((RectTransform) scroll.viewport).GetWorldCorners(view);
        var rect = new Vector3[4];
        ((RectTransform) row.transform).GetWorldCorners(rect);
        var alpha = 1f;
        for (var t = row.transform; t != null && t != scroll.content; t = t.parent)
            if (t.TryGetComponent<CanvasGroup>(out var group)) alpha = Mathf.Min(alpha, group.alpha);
        return rect[0].y >= view[0].y - .01f && rect[1].y <= view[1].y + .01f && alpha == 1;
    }

    private static void Move(Screen screen, MoveDirection direction) =>
        ExecuteEvents.Execute(screen.Launch.gameObject, new AxisEventData(EventSystem.current) { moveDir = direction }, ExecuteEvents.moveHandler);

    private static void Cancel(Screen screen) =>
        ExecuteEvents.Execute(screen.Launch.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);

    private static string Selected(Screen screen) => screen.Picker.Selected.Name;

    private static void SelectionFollowsClicksAndArrows()
    {
        var screen = Build();
        Require(screen.Rows.Length == 5, $"Expected 5 scenario rows, found {screen.Rows.Length}.");
        Require(Selected(screen) == "Alpha", "The first scenario is not selected on Show.");
        Require(EventSystem.current.currentSelectedGameObject == screen.Launch.gameObject, "Launch is not focused on Show.");
        Move(screen, MoveDirection.Down);
        Require(Selected(screen) == "Beta", "Down did not select the next row.");
        Move(screen, MoveDirection.Down);
        Require(Selected(screen) == "Gamma", "Down did not cross into the next group.");
        Move(screen, MoveDirection.Up);
        Require(Selected(screen) == "Beta", "Up did not select the previous row.");
        screen.Rows[3].onClick.Invoke();
        Require(Selected(screen) == "Delta", "Clicking a name did not select it.");
        Require(screen.Launched.Count == 0, "Clicking a name launched it.");
        Require(EventSystem.current.currentSelectedGameObject == screen.Launch.gameObject, "A click on a name took focus off Launch.");
        Require(screen.Rows.All(row => row.navigation.mode == Navigation.Mode.None) && screen.Launch.navigation.mode == Navigation.Mode.None
            && screen.Back.navigation.mode == Navigation.Mode.None, "A picker button still takes Unity navigation.");
        var accent = screen.Rows[3].targetGraphic.color;
        Require(screen.Rows.Where((row, i) => i != 3).All(row => row.targetGraphic.color != accent), "Only the selected row is drawn in the accent.");
        Move(screen, MoveDirection.Up);
        Require(screen.Rows[2].targetGraphic.color == accent && screen.Rows[3].targetGraphic.color != accent, "The accent did not follow the selection.");
    }

    private static void ArrowsStopAtTheEnds()
    {
        var screen = Build();
        Move(screen, MoveDirection.Up);
        Require(Selected(screen) == "Alpha", "Up wrapped off the first row.");
        for (var i = 0; i < 10; i++) Move(screen, MoveDirection.Down);
        Require(Selected(screen) == "Echo", "Down wrapped or stopped short of the last row.");
    }

    private static void LeftAndRightAreIgnored()
    {
        var screen = Build();
        Move(screen, MoveDirection.Down);
        Move(screen, MoveDirection.Left);
        Move(screen, MoveDirection.Right);
        Require(Selected(screen) == "Beta", "Left or Right moved the selection.");
    }

    private static void LaunchIsOnePathAndFocusClears()
    {
        var screen = Build();
        Move(screen, MoveDirection.Down);
        screen.Launch.onClick.Invoke();
        Require(screen.Launched.Count == 1 && screen.Launched[0] == screen.A[1], "Launch did not launch the selected scenario once.");
        Require(EventSystem.current.currentSelectedGameObject == null, "Focus survived Launch, so a second Enter could launch again.");
        Require(screen.Backs == 0, "Launch went back.");
    }

    private static void CancelAndBackAreOnePath()
    {
        var screen = Build();
        Cancel(screen);
        Require(screen.Backs == 1 && screen.Launched.Count == 0, "Cancel did not go back exactly once.");
        Require(EventSystem.current.currentSelectedGameObject == null, "Focus survived Cancel.");
        screen = Build();
        screen.Back.onClick.Invoke();
        Require(screen.Backs == 1 && screen.Launched.Count == 0, "The Back button did not go back exactly once.");
        Require(EventSystem.current.currentSelectedGameObject == null, "Focus survived Back.");
    }

    private static string DetailText(Screen screen)
    {
        screen.Detail.RefreshValues();
        return string.Join("|", screen.Detail.GetComponentsInChildren<TextMeshProUGUI>(false).Select(text => text.text));
    }

    private static void DetailPaneFollowsSelection()
    {
        var screen = Build();
        var first = DetailText(screen);
        Require(first.Contains("brief of Alpha") && first.Contains("7") && first.Contains("on"), $"Detail does not describe Alpha: {first}");
        Require(first.Contains("modes") && first.Contains("Alpha"), "Detail heading is not the group and name.");
        Move(screen, MoveDirection.Down);
        Move(screen, MoveDirection.Down);
        var third = DetailText(screen);
        Require(third.Contains("brief of Gamma") && third.Contains("new each launch") && third.Contains("off"), $"Detail does not describe Gamma: {third}");
        Require(third.Contains("development") && !third.Contains("Alpha"), "Detail heading did not follow the selection.");
        Require(screen.Detail.GetComponentsInChildren<Button>(false).Single() == screen.Launch, "Selecting rebuilt the Launch button.");
    }

    private static void OneGroupHasNoSectionLabel()
    {
        var many = Build();
        var one = Build(oneGroup: true);
        Require(many.List.Children == 5 + 3 + 2 + 1, $"Three groups should list 5 rows, 3 section labels, 2 spacers and Back, not {many.List.Children} children.");
        Require(one.List.Children == 2 + 1, $"One group should list its 2 rows and Back, not {one.List.Children} children.");
    }

    private static void LaunchesOnceHoweverItIsReached()
    {
        var screen = Build();
        screen.Launch.onClick.Invoke();
        screen.Launch.onClick.Invoke();
        ExecuteEvents.Execute(screen.Launch.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        Require(screen.Launched.Count == 1, $"Launch ran {screen.Launched.Count} times after a click, a second click and Submit.");
        Require(screen.Launch.navigation.mode == Navigation.Mode.None && Scroll(screen.List).verticalScrollbar.navigation.mode == Navigation.Mode.None,
            "The list scrollbar or Launch takes Unity navigation.");
    }

    // The real scenario groups overflow the list's viewport; every row the arrows reach must end wholly visible.
    private static void ArrowingThroughTheRealListKeepsTheSelectionVisible()
    {
        var screen = Build(real: true);
        var scroll = Scroll(screen.List);
        var content = (RectTransform) scroll.content;
        var viewport = (RectTransform) scroll.viewport;
        Require(content.rect.height > viewport.rect.height + 1, $"The real list ({content.rect.height}) does not overflow its viewport ({viewport.rect.height}); the scroll rules are unmeasured.");
        Require(Shown(screen.Rows[0], scroll), "The first row is not shown on Show.");
        for (var i = 1; i < screen.Rows.Length; i++)
        {
            Move(screen, MoveDirection.Down);
            Canvas.ForceUpdateCanvases();
            Require(Shown(screen.Rows[i], scroll), $"Down to row {i} ({Selected(screen)}) left it hidden or cut off.");
        }
        for (var i = screen.Rows.Length - 2; i >= 0; i--)
        {
            Move(screen, MoveDirection.Up);
            Canvas.ForceUpdateCanvases();
            Require(Shown(screen.Rows[i], scroll), $"Up to row {i} ({Selected(screen)}) left it hidden or cut off.");
        }
    }
}
