using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using static Unity.Mathematics.math;

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

    private BezierCurve _curve;
    //private Func<float, string> _xFunc;

    public void SetCurrent(float x)
    {
        CurrentX.gameObject.SetActive(true);
        CurrentY.gameObject.SetActive(true);
        var xrect = CurrentX.GetComponent<RectTransform>();
        var yrect = CurrentY.GetComponent<RectTransform>();
        var y = _curve.Evaluate(x);
        xrect.anchorMin = new Vector2(x, 0);
        xrect.anchorMax = new Vector2(x, y);
        yrect.anchorMin = new Vector2(x, y);
        yrect.anchorMax = new Vector2(x, y);
    }

    public void Show(string label, BezierCurve curve, Func<float, string> xFunc, bool showMax = false)
    {
        _curve = curve;
        //_xFunc = xFunc;
        CurrentX.gameObject.SetActive(false);
        CurrentY.gameObject.SetActive(false);
        Title.text = label;
        var points = new List<Vector2>();
        var keys = curve.Keys.Select(v => new Vector2(v.x, v.y));
        if (curve.Keys[0].x > .01f) keys = keys.Prepend(new Vector2(0, curve.Keys[0].y));
        if (curve.Keys[curve.Keys.Length-1].x < .99f) keys = keys.Append(new Vector2(1, curve.Keys[curve.Keys.Length-1].y));
        var keysArray = keys.ToArray();
        for (int i = 0; i < keysArray.Length - 1; i++)
        {
            for (int j = 0; j < Segments; j++)
            {
                var x = lerp(keysArray[i].x, keysArray[i + 1].x, (float) j / Segments);
                points.Add(new Vector2(x, curve.Evaluate(x)));
            }
        }
        points.Add(new Vector2(1, curve.Evaluate(1)));

        Line.Points = points.ToArray();
        MinLabel.text = xFunc(0);
        MaxLabel.text = xFunc(1);
        if (showMax)
        {
            var midX = curve.Maximum;
            MidLabel.text = xFunc(midX);
            var rect = MidLabel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(midX, rect.anchorMin.y);
            rect.anchorMax = new Vector2(midX, rect.anchorMax.y);
        }
        else MidLabel.text = xFunc(.5f);
    }
}
