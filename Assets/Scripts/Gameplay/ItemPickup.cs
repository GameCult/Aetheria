using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

public class ItemPickup : MonoBehaviour
{
    public float LabelFadeDuration = .5f;
    public float LabelPersistDuration = 3;
    public float LabelDisplayAngle = 15;
    public float LabelDisplayMaxDistance = 500;
    public Transform ScanLabelContainer;
    public TextMeshPro ScanLabel;
    
    public ItemInstance Item { get; set; }
    public ZoneRenderer ZoneRenderer { get; set; }
    public float3 ViewOrigin { get; set; }
    public float3 ViewDirection { get; set; }

    private float _displayTime;

    private void Update()
    {
        var diff = (float3) transform.position - ViewOrigin;
        var toThis = normalize(diff);
        var viewAngle = acos(Vector3.Dot(toThis, ViewDirection)) * Mathf.Rad2Deg;
        if (length(diff) < LabelDisplayMaxDistance && viewAngle < LabelDisplayAngle)
            _displayTime = Time.time;
        var targetAlpha = Time.time - _displayTime < LabelPersistDuration ? 1 : 0;
        var c = ScanLabel.color;
        c.a = c.a + sign(targetAlpha - c.a) * (Time.deltaTime / LabelFadeDuration);
        ScanLabel.color = c;
        ScanLabelContainer.rotation = Quaternion.LookRotation(-toThis);
    }

    private void OnDestroy()
    {
        ZoneRenderer?.DestroyLoot(this);
    }
}