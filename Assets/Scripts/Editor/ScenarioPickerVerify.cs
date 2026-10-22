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
            EventSystem.current = new GameObject("verify EventSystem").AddComponent<EventSystem>();
            SelectionFollowsClicksAndArrows();
            ArrowsStopAtTheEnds();
            LeftAndRightAreIgnored();
            LaunchIsOnePathAndFocusClears();
            CancelAndBackAreOnePath();
            DetailPaneFollowsSelection();
            OneGroupHasNoSectionLabel();
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

    private static Screen Build(bool oneGroup = false)
    {
        var (_, list, detail) = ScenarioPickerCapture.Build();
        var screen = new Screen
        {
            List = list, Detail = detail,
            A = new Scenario[] { new Fake("Alpha", 7), new Fake("Beta") },
            B = new Scenario[] { new Fake("Gamma", ambient: false) },
            C = new Scenario[] { new Fake("Delta"), new Fake("Echo") }
        };
        var groups = oneGroup
            ? new List<(string label, Scenario[] scenarios)> { ("modes", screen.A) }
            : new List<(string label, Scenario[] scenarios)> { ("modes", screen.A), ("development", screen.B), ("test arenas", screen.C) };
        screen.Picker = ScenarioPicker.Show(list, detail, groups, screen.Launched.Add, () => screen.Backs++);
        var buttons = list.GetComponentsInChildren<PropertyButton>(true).Select(button => button.Button).ToArray();
        screen.Rows = buttons.Take(buttons.Length - 1).ToArray();
        screen.Back = buttons.Last();
        screen.Launch = detail.GetComponentsInChildren<PropertyButton>(true).Single().Button;
        return screen;
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
        var selected = screen.Rows[3].colors;
        var other = screen.Rows[0].colors;
        Require(selected.normalColor == selected.highlightedColor && other.normalColor != other.highlightedColor,
            "Only the selected row is drawn highlighted.");
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
        return string.Join("|", screen.Detail.GetComponentsInChildren<TextMeshProUGUI>(true).Select(text => text.text));
    }

    private static void DetailPaneFollowsSelection()
    {
        var screen = Build();
        var first = DetailText(screen);
        Require(first.Contains("brief of Alpha") && first.Contains("7") && first.Contains("on"), $"Detail does not describe Alpha: {first}");
        Require(screen.Detail.Title.text == MainMenu.TitleSubtitle("Alpha", "modes"), "Detail title is not the name and group.");
        Move(screen, MoveDirection.Down);
        Move(screen, MoveDirection.Down);
        var third = DetailText(screen);
        Require(third.Contains("brief of Gamma") && third.Contains("new each launch") && third.Contains("off"), $"Detail does not describe Gamma: {third}");
        Require(screen.Detail.Title.text == MainMenu.TitleSubtitle("Gamma", "development"), "Detail title did not follow the selection.");
        Require(screen.Detail.GetComponentsInChildren<PropertyButton>(true).Single().Button == screen.Launch, "Selecting rebuilt the Launch button.");
    }

    private static void OneGroupHasNoSectionLabel()
    {
        var many = Build();
        var one = Build(oneGroup: true);
        Require(many.List.Children == 5 + 3 + 1, $"Three groups should list 5 rows, 3 section labels and Back, not {many.List.Children} children.");
        Require(one.List.Children == 2 + 1, $"One group should list its 2 rows and Back, not {one.List.Children} children.");
    }
}
