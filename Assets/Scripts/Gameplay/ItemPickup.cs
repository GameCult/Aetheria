using System;
using TMPro;
using UnityEngine;
using static CultMath.math;
using cfloat3 = CultMath.float3;

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
    public cfloat3 ViewOrigin { get; set; }
    public cfloat3 ViewDirection { get; set; }

    private float _displayTime;

    private void Update()
    {
        var position = transform.position;
        var diff = new cfloat3(position.x, position.y, position.z) - ViewOrigin;
        var toThis = normalize(diff);
        var viewAngle = acos(dot(toThis, ViewDirection)) * AetheriaMath.Rad2Deg;
        if (length(diff) < LabelDisplayMaxDistance && viewAngle < LabelDisplayAngle)
            _displayTime = Time.time;
        var targetAlpha = Time.time - _displayTime < LabelPersistDuration ? 1 : 0;
        var c = ScanLabel.color;
        c.a = c.a + sign(targetAlpha - c.a) * (Time.deltaTime / LabelFadeDuration);
        ScanLabel.color = c;
        ScanLabelContainer.rotation = Quaternion.LookRotation((Vector3)AetheriaMath.ToUnity(-toThis));
    }

    private void OnDestroy()
    {
        ZoneRenderer?.DestroyLoot(this);
    }
}
