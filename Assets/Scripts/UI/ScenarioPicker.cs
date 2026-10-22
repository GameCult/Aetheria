using System;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The New Game screen: scenario names on the left pane by group, the selected scenario's brief, seed and ambient on a
// docked right pane, one Launch. This class owns which scenario is selected (one index into the rows it built);
// everything the screen shows about a scenario, and what Launch and Enter launch, reads that index. Keys arrive only as
// Input System UI events on the focused Launch button, so Enter and a mouse click are the same Button.onClick.
public sealed class ScenarioPicker
{
    private readonly List<Scenario> _scenarios = new List<Scenario>();
    private readonly List<string> _groupLabels = new List<string>();
    private readonly List<Button> _rows = new List<Button>();
    private readonly List<ColorBlock> _rowColors = new List<ColorBlock>();
    private readonly List<Color> _rowFills = new List<Color>();
    private readonly PropertiesPanel _list;
    private readonly PropertiesPanel _detail;
    private readonly Action<Scenario> _launch;
    private readonly Action _back;
    private Button _launchButton;
    private static readonly Color Accent = new Color(1, .4458f, .05f);
    private int _selected;

    private ScenarioPicker(PropertiesPanel list, PropertiesPanel detail, Action<Scenario> launch, Action back)
    {
        _list = list;
        _detail = detail;
        _launch = launch;
        _back = back;
    }

    // The scenario the detail pane describes and Launch will launch.
    public Scenario Selected => _scenarios[_selected];

    // Parks the detail pane beside the list pane: a child of it, outside its layout, hidden until Show.
    public static void Dock(PropertiesPanel detail, PropertiesPanel list, float width = 480, float gap = 128)
    {
        var rect = (RectTransform) detail.transform;
        rect.SetParent(list.transform, false);
        detail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = new Vector2(gap, 0);
        rect.sizeDelta = new Vector2(width, 0);
        detail.gameObject.SetActive(false);
    }

    // Builds both panes for the groups, shows the detail pane, selects the first scenario and focuses Launch.
    public static ScenarioPicker Show(PropertiesPanel list, PropertiesPanel detail,
        IReadOnlyList<(string label, Scenario[] scenarios)> groups, Action<Scenario> launch, Action back)
    {
        var picker = new ScenarioPicker(list, detail, launch, back);
        foreach (var group in groups)
        {
            if (groups.Count > 1) list.AddSection(group.label);
            foreach (var scenario in group.scenarios)
            {
                var index = picker._scenarios.Count;
                picker._scenarios.Add(scenario);
                picker._groupLabels.Add(group.label);
                var row = list.AddButton(scenario.Name, () => picker.Select(index));
                picker.Track(row.Button);
                picker._rows.Add(row.Button);
                picker._rowColors.Add(row.Button.colors);
                picker._rowFills.Add(row.Button.targetGraphic.color);
            }
        }
        picker.Track(list.AddButton("Back", picker.Back).Button);

        detail.Clear();
        detail.gameObject.SetActive(true);
        detail.AddProperty(() => picker.Selected.Brief);
        detail.AddProperty("Seed", () => picker.Selected.Seed == 0 ? "new each launch" : picker.Selected.Seed.ToString());
        detail.AddProperty("Ambient", () => picker.Selected.Ambient ? "on" : "off");
        picker._launchButton = detail.AddButton("Launch", picker.Activate).Button;
        picker.Track(picker._launchButton);
        picker._launchButton.OnMoveAsObservable().Subscribe(move =>
        {
            if (move.moveDir == MoveDirection.Up) picker.Step(-1);
            else if (move.moveDir == MoveDirection.Down) picker.Step(1);
        });
        picker._launchButton.OnCancelAsObservable().Subscribe(_ => picker.Back());

        picker.Select(0);
        return picker;
    }

    // Takes a button out of Unity's navigation so a click never moves focus and Move never walks automatic navigation.
    private void Track(Button button) => button.navigation = new Navigation { mode = Navigation.Mode.None };

    // Moves the selection one row, stopping at either end.
    private void Step(int delta) => Select(_selected + delta);

    // Makes one row the selection: clamps the index, redraws the highlight and title, and returns focus to Launch.
    private void Select(int index)
    {
        _selected = Mathf.Clamp(index, 0, _scenarios.Count - 1);
        for (var i = 0; i < _rows.Count; i++)
        {
            // The prefab tint barely moves the row colour, so the selected row is drawn in the menu accent.
            var colors = _rowColors[i];
            if (i == _selected) colors.normalColor = Color.white;
            _rows[i].colors = colors;
            _rows[i].targetGraphic.color = i == _selected ? Accent : _rowFills[i];
        }
        _detail.Title.text = MainMenu.TitleSubtitle(_groupLabels[_selected], Selected.Name);
        Focus(_launchButton.gameObject);
        Reveal((RectTransform) _rows[_selected].transform);
    }

    // Scrolls the list pane just far enough that the selected row is inside its viewport.
    private void Reveal(RectTransform row)
    {
        var scroll = (_list.Content ?? _list.transform).GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null) return;
        var view = scroll.viewport != null ? scroll.viewport : (RectTransform) scroll.transform;
        var rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, row);
        var viewBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, view);
        var shift = rowBounds.max.y > viewBounds.max.y ? viewBounds.max.y - rowBounds.max.y
            : rowBounds.min.y < viewBounds.min.y ? viewBounds.min.y - rowBounds.min.y : 0;
        scroll.content.anchoredPosition -= new Vector2(0, shift);
    }

    // Launches the selection; focus is cleared first so a repeated Enter or a second click cannot launch twice.
    private void Activate()
    {
        Focus(null);
        _launch(Selected);
    }

    // Leaves the screen; the same path for the Back button and Cancel.
    private void Back()
    {
        Focus(null);
        _back();
    }

    private static void Focus(GameObject target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
    }
}
