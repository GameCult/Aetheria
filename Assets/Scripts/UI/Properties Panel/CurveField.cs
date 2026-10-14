using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using static CultMath.math;

public class CurveField : MonoBehaviour
{
    public int Segments = 16;
    public int SigDigs = 3;
    public TextMeshProUGUI Title;
    public TextMeshProUGUI MinLabel;
    public TextMeshProUGUI MidLabel;
    public TextMeshProUGUI MaxLabel;
    public UILineRenderer Line;
    public Image CurrentX;
    public Image CurrentY;

    private Func<float, float> _y;

    public void SetCurrent(float x)
    {
        CurrentX.gameObject.SetActive(true);
        CurrentY.gameObject.SetActive(true);
        var xrect = CurrentX.GetComponent<RectTransform>();
        var yrect = CurrentY.GetComponent<RectTransform>();
        var y = _y(x);
        xrect.anchorMin = new Vector2(x, 0);
        xrect.anchorMax = new Vector2(x, y);
        yrect.anchorMin = new Vector2(x, y);
        yrect.anchorMax = new Vector2(x, y);
    }

    // Segments + 1 uniform samples of y over [0, 1], labelled by xFunc; the middle label sits at midX.
    public void Show(string label, Func<float, float> y, Func<float, string> xFunc, float midX = .5f)
    {
        _y = y;
        CurrentX.gameObject.SetActive(false);
        CurrentY.gameObject.SetActive(false);
        Title.text = label;
        var points = new List<Vector2>();
        for (int j = 0; j <= Segments; j++)
        {
            var x = (float) j / Segments;
            points.Add(new Vector2(x, y(x)));
        }

        Line.Points = points.ToArray();
        MinLabel.text = xFunc(0);
        MaxLabel.text = xFunc(1);
        MidLabel.text = xFunc(midX);
        var rect = MidLabel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(midX, rect.anchorMin.y);
        rect.anchorMax = new Vector2(midX, rect.anchorMax.y);
    }
}
