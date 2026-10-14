using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VisibleTargetIndicator : MonoBehaviour
{
    public RectTransform Transform;
    public Image Fill;
    public PlaceUIElementWorldspace Place;
    public TextMeshProUGUI Label;

    // Null or empty hides the label; the first non-empty text creates it as a child (no scene or prefab edit) and keeps it.
    // Pools are shared, so hide the label before the indicator returns to its pool.
    public void ShowLabel(string text)
    {
        var show = !string.IsNullOrEmpty(text);
        if (Label == null)
        {
            if (!show) return;
            var child = new GameObject("Label", typeof(RectTransform));
            child.transform.SetParent(transform, false);
            Label = child.AddComponent<TextMeshProUGUI>();
            Label.fontSize = 14;
            Label.alignment = TextAlignmentOptions.Top;
            Label.raycastTarget = false;
            var rect = Label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(200, 24);
        }
        Label.gameObject.SetActive(show);
        if (show && Label.text != text) Label.text = text;
    }
}
