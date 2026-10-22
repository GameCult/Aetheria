using System;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
    private ScrollRect _scroll;
    private UnityAction<Vector2> _fit;
    private static readonly Color Accent = new Color(1, .4458f, .05f);
    private int _selected;
    private bool _launched;
    private bool _restoreDeselect;

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

    private static readonly Color Muted = new Color(1, 1, 1, .55f);

    // Builds both panes for the groups, shows the detail pane, selects the first scenario and focuses Launch. The
    // launchChrome is a button of the menu's own styling, cloned for Launch.
    public static ScenarioPicker Show(PropertiesPanel list, PropertiesPanel detail,
        IReadOnlyList<(string label, Scenario[] scenarios)> groups, Action<Scenario> launch, Action back, Button launchChrome)
    {
        var picker = new ScenarioPicker(list, detail, launch, back);
        foreach (var group in groups)
        {
            if (groups.Count > 1)
            {
                if (picker._scenarios.Count > 0) list.AddSpacer();
                var header = list.AddSection(group.label);
                header.GetComponent<LayoutElement>().preferredHeight = 26;
                var text = header.GetComponentInChildren<TMP_Text>();
                text.fontSize = 14;
                text.fontStyle = FontStyles.SmallCaps;
                text.alignment = TextAlignmentOptions.Left;
                text.color = Muted;
            }
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
        // The pane's own title is an invisible copy of the page title, so both panes stack their content from the same
        // line; the scenario is headed by its group label and name in the body.
        detail.Title.text = list.Title.text;
        detail.Title.color = Color.clear;
        Style(detail.AddProperty(() => $"<smallcaps>{picker._groupLabels[picker._selected]}"), 14, Muted);
        Style(detail.AddProperty(() => picker.Selected.Name), 30, Color.white);
        detail.AddProperty(() => $"<margin-right=20%>{picker.Selected.Brief}");
        detail.AddSpacer();
        detail.AddProperty(() => $"<color=#ffffff8c>Seed</color><pos=22%>{(picker.Selected.Seed == 0 ? "new each launch" : picker.Selected.Seed.ToString())}");
        detail.AddProperty(() => $"<color=#ffffff8c>Ambient</color><pos=22%>{(picker.Selected.Ambient ? "on" : "off")}");
        detail.AddSpacer();
        picker._launchButton = AddLaunch(detail, launchChrome, picker.Activate);
        picker.Track(picker._launchButton);
        picker._launchButton.OnMoveAsObservable().Subscribe(move =>
        {
            if (move.moveDir == MoveDirection.Up) picker.Step(-1);
            else if (move.moveDir == MoveDirection.Down) picker.Step(1);
        });
        picker._launchButton.OnCancelAsObservable().Subscribe(_ => picker.Back());

        picker.StyleScrollbars();
        picker.HoldFocus();
        picker.Select(0);
        // Layout is not live until the frame after Show; edit mode has no frames.
        if (Application.isPlaying) Observable.NextFrame().Subscribe(_ => picker.FitViewport());
        return picker;
    }

    private static void Style(Property property, float size, Color color)
    {
        property.Label.enableAutoSizing = false;
        property.Label.fontSize = size;
        property.Label.color = color;
    }

    // Launch is a clone of the menu's own chrome button, filled with the accent so it reads as the default action. The
    // clone lives under a spacer row, so the pane's Clear destroys it.
    private static Button AddLaunch(PropertiesPanel detail, Button chrome, Action onClick)
    {
        var holder = detail.AddSpacer();
        holder.GetComponent<LayoutElement>().preferredHeight = 40;
        var button = UnityEngine.Object.Instantiate(chrome, holder, false);
        var rect = (RectTransform) button.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(220, 0);
        // The visible chrome is the button's Fill child, which the button's own layout group sizes to its text and
        // centres; with the group off, Fill stretches to the button.
        button.GetComponent<LayoutGroup>().enabled = false;
        var fill = button.targetGraphic.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        button.targetGraphic.color = Accent;
        button.GetComponentInChildren<TMP_Text>().text = "Launch";
        return button;
    }

    // The detail pane needs no scrollbar; the list's gets a visible track, so its handle reads as a scrollbar rather
    // than a bar that stops partway.
    private void StyleScrollbars()
    {
        var detailScroll = ScrollOf(_detail);
        if (detailScroll != null && detailScroll.verticalScrollbar != null) detailScroll.verticalScrollbar.gameObject.SetActive(false);
        _scroll = ScrollOf(_list);
        if (_scroll == null) return;
        if (_scroll.verticalScrollbar != null)
        {
            if (!_scroll.verticalScrollbar.TryGetComponent<Image>(out var track)) track = _scroll.verticalScrollbar.gameObject.AddComponent<Image>();
            track.color = new Color(Accent.r, Accent.g, Accent.b, .2f);
            // A click on the bar selects it and takes focus off Launch, which would silence the arrows.
            _scroll.verticalScrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        _scroll.onValueChanged.RemoveAllListeners();
        _fit = _ => FitViewport();
        _scroll.onValueChanged.AddListener(_fit);
    }

    private static ScrollRect ScrollOf(PropertiesPanel panel) => (panel.Content ?? panel.transform).GetComponentInParent<ScrollRect>();

    // Hides every list entry the viewport only partly shows, so the list ends on a whole row instead of slicing text.
    private void FitViewport()
    {
        if (_scroll == null || _scroll.content == null) return;
        if (_rows[0] == null)
        {
            _scroll.onValueChanged.RemoveListener(_fit);
            return;
        }
        var (low, high) = ViewSpan(_scroll);
        foreach (RectTransform entry in _list.Content ?? _list.transform)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_scroll.content, entry);
            var whole = bounds.min.y >= low - .5f && bounds.max.y <= high + .5f;
            if (!entry.TryGetComponent<CanvasGroup>(out var group)) group = entry.gameObject.AddComponent<CanvasGroup>();
            group.alpha = whole ? 1 : 0;
            group.blocksRaycasts = whole;
        }
    }

    // The viewport's vertical extent in content space. CalculateRelativeRectTransformBounds cannot give it: it also
    // folds in the rects of the viewport's children, which makes the content's own bounds the answer.
    private static (float low, float high) ViewSpan(ScrollRect scroll)
    {
        var view = scroll.viewport != null ? scroll.viewport : (RectTransform) scroll.transform;
        var rect = view.rect;
        var low = scroll.content.InverseTransformPoint(view.TransformPoint(new Vector3(0, rect.yMin))).y;
        var high = scroll.content.InverseTransformPoint(view.TransformPoint(new Vector3(0, rect.yMax))).y;
        return (Mathf.Min(low, high), Mathf.Max(low, high));
    }

    // Takes a button out of Unity's navigation so a click never moves focus and Move never walks automatic navigation.
    private void Track(Button button) => button.navigation = new Navigation { mode = Navigation.Mode.None };

    // Moves the selection one row, stopping at either end.
    private void Step(int delta) => Select(_selected + delta);

    // Makes one row the selection: clamps the index, redraws the highlight, and returns focus to Launch.
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
        Focus(_launchButton.gameObject);
        Reveal((RectTransform) _rows[_selected].transform);
    }

    // Scrolls the list pane just far enough that the selected row is inside its viewport.
    private void Reveal(RectTransform row)
    {
        var scroll = _scroll;
        if (scroll == null || scroll.content == null) return;
        var (low, high) = ViewSpan(scroll);
        var rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, row);
        var shift = rowBounds.max.y > high ? rowBounds.max.y - high
            : rowBounds.min.y < low ? rowBounds.min.y - low : 0;
        // Content moves against the row: a row above the viewport needs the content to move down, and the reverse.
        scroll.content.anchoredPosition += new Vector2(0, shift);
        if (shift != 0) FitViewport();
    }

    // Launches the selection, once: Enter, Submit and a mouse click all arrive here, and nothing launches again until
    // the picker is shown afresh, however long the menu takes to fade out.
    private void Activate()
    {
        if (_launched) return;
        _launched = true;
        Focus(null);
        ReleaseFocus();
        _launch(Selected);
    }

    // Leaves the screen; the same path for the Back button and Cancel.
    private void Back()
    {
        Focus(null);
        ReleaseFocus();
        _back();
    }

    // While the picker is up, a click on empty space must not drop focus from Launch, or the arrows and Enter go dead.
    // The input module's flag is scene-wide, so the picker takes it for its own lifetime and gives it back on leaving.
    private void HoldFocus()
    {
        if (EventSystem.current == null || !(EventSystem.current.currentInputModule is InputSystemUIInputModule module) || !module.deselectOnBackgroundClick) return;
        module.deselectOnBackgroundClick = false;
        _restoreDeselect = true;
    }

    private void ReleaseFocus()
    {
        if (!_restoreDeselect) return;
        _restoreDeselect = false;
        if (EventSystem.current != null && EventSystem.current.currentInputModule is InputSystemUIInputModule module) module.deselectOnBackgroundClick = true;
    }

    private static void Focus(GameObject target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
    }
}
