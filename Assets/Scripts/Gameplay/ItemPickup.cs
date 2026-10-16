using System;
using TMPro;
using CultMath;
using CultMath.UnityBridge;
using UnityEngine;
using static CultMath.math;
using Random = UnityEngine.Random;

// How one floating item looks. Zone owns the item's every fact (existence, position, expiry, pickup); once bound,
// this reads the latest sim body and draws, spun for show, and writes nothing back (rulings unity-presents-only-r3
// and sim-speed-presentation). An unbound pickup (FieldTester's test scene) leaves its transform alone.
public class ItemPickup : MonoBehaviour
{
    public float RotationSpeed = 1f;
    public float LabelFadeDuration = .5f;
    public float LabelPersistDuration = 3;
    public float LabelDisplayAngle = 15;
    public float LabelDisplayMaxDistance = 500;
    public Transform ScanLabelContainer;
    public TextMeshPro ScanLabel;
    
    public ItemInstance Item { get; set; }
    public float3 ViewOrigin { get; set; }
    public float3 ViewDirection { get; set; }

    private float _displayTime;
    private float _timeOffset;
    private Zone _zone;
    private GameplaySettings _settings;
    private FloatingItem _floating;

    public void Bind(Zone zone, FloatingItem floating, GameplaySettings settings)
    {
        _zone = zone;
        _settings = settings;
        _floating = floating;
        Item = floating.Item;
        _timeOffset = Random.value * 100;
        Place();
    }

    private void Place()
    {
        var lead = ActionGameManager.Clock.Lead;
        var p = DrawAhead.Position(_floating.Body, lead);
        transform.position = new Vector3(p.x, DrawAhead.FloaterHeight(_zone, _settings, p, lead), p.y);
    }

    private void Update()
    {
        if (_floating != null)
        {
            Place();
            transform.localRotation = Quaternion.Euler(
                sin(Time.time - _timeOffset * RotationSpeed) * 90, 0, cos(Time.time - _timeOffset * RotationSpeed) * 90);
        }

        var diff = transform.position.ToCultMath() - ViewOrigin;
        var toThis = normalize(diff);
        var viewAngle = acos(dot(toThis, ViewDirection)) * Mathf.Rad2Deg;
        if (length(diff) < LabelDisplayMaxDistance && viewAngle < LabelDisplayAngle)
            _displayTime = Time.time;
        if (_floating?.Holder != null) _displayTime = Time.time;
        var targetAlpha = Time.time - _displayTime < LabelPersistDuration ? 1 : 0;
        var c = ScanLabel.color;
        c.a = c.a + sign(targetAlpha - c.a) * (Time.deltaTime / LabelFadeDuration);
        ScanLabel.color = c;
        ScanLabelContainer.rotation = Quaternion.LookRotation((-toThis).ToUnity());
    }
}
