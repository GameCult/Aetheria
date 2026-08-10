using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = UnityEngine.Random;

public class FieldTester : MonoBehaviour
{
    public int PickupCount;
    public ItemPickup[] PickupPrefabs;
    public float PickupTravelDistance;
    public float PickupTravelTimeMin;
    public float PickupTravelTimeMax;
    public float PickupSizeMin;
    public float PickupSizeMax;
    public float ScaleExponent;
    public float PickupSpawnDistanceMin;
    public float PickupSpawnDistanceMax;
    public Camera Camera;
    public FieldDriver TestField;
    public PropertiesPanel Properties;
    
    private AetheriaInput _input;
    private bool _forceThrust;
    private float _throttleDecay = 2;
    private bool _directionalPush;
    private float _twistFront;
    private float _twistRear;

    private List<(float lerp, float time, Transform transform)> _pickups = new List<(float lerp, float time, Transform transform)>();

    private void Start()
    {
        _input = new AetheriaInput();
        _input.Player.Enable();
        Properties.AddField("Time Scale", () => Time.timeScale, f => Time.timeScale = f, 0, 2);
        Properties.AddField("FOV", () => Camera.fieldOfView, f => Camera.fieldOfView = f, 15, 45);
        Properties.AddButton("Melee", () => TestField.Melee());
        Properties.AddField("Force Thrust", () => _forceThrust, b => _forceThrust = b);
        Properties.AddField("Directional Push", () => _directionalPush, b => _directionalPush = b);
        Properties.AddField("Throttle Decay", () => _throttleDecay, f => _throttleDecay = f);
        Properties.Inspect(TestField, true, true);
        Properties.AddProperty("Current Hits", () => TestField.HitCount.ToString());
        Properties.AddProperty("Push X", () => $"{(int)(TestField.Push.x * 100)}%");
        Properties.AddProperty("Push Y", () => $"{(int)(TestField.Push.y * 100)}%");
        Properties.AddProperty("Front Twist", () => $"{(int)(TestField.FrontTwist * 100)}%");
        Properties.AddProperty("Rear Twist", () => $"{(int)(TestField.RearTwist * 100)}%");

        for (int i = 0; i < PickupCount; i++)
        {
            var l = (float)i / PickupCount;
            var pickup = Instantiate(PickupPrefabs[(int)(l * PickupPrefabs.Length)], transform);
            var i1 = i;
            var click = pickup.GetComponent<ClickableCollider>();
            click.OnClick += (collider, data, ray, hit) =>
            {
                if (!TestField.CanGrab) return;
                click.Clear();
                var p = _pickups[i1];
                var travelTime = lerp(PickupTravelTimeMin, PickupTravelTimeMax, l);
                TestField.GrabObject(p.transform, Vector3.forward * (PickupTravelDistance / travelTime));
                p.transform = null;
                _pickups[i1] = p;
            };
            
            pickup.ScanLabelContainer.gameObject.SetActive(false);
            pickup.enabled = false;
            var pickupTransform = pickup.transform;
            var time = Random.value;
            var circle = Random.insideUnitCircle.normalized * Random.Range(PickupSpawnDistanceMin,PickupSpawnDistanceMax);
            pickupTransform.position = new Vector3(circle.x,circle.y, (time-.5f)*PickupTravelDistance);
            _pickups.Add((l, time, pickupTransform));
        }
    }

    private void Update()
    {
        var move = _forceThrust ? float2(0,1) : (float2)_input.Player.Move.ReadValue<Vector2>();
        var turn = _input.Player.Turn.ReadValue<float>();
        if (_directionalPush)
        {
            TestField.Push = AetheriaMath.Damp(TestField.Push, move, _throttleDecay, Time.deltaTime);
            TestField.FrontTwist = AetheriaMath.Damp(TestField.FrontTwist,
                turn,
                _throttleDecay, Time.deltaTime);
            TestField.RearTwist = AetheriaMath.Damp(TestField.RearTwist,
                turn,
                _throttleDecay, Time.deltaTime);
        }
        else
        {
            TestField.Push = AetheriaMath.Damp(TestField.Push, float2(0,move.y), _throttleDecay, Time.deltaTime);
            TestField.FrontTwist = AetheriaMath.Damp(TestField.FrontTwist,
                clamp(turn + move.x, -1, 1) * (1+min(move.y,0)),
                _throttleDecay, Time.deltaTime);
            TestField.RearTwist = AetheriaMath.Damp(TestField.RearTwist,
                clamp(turn - move.x, -1, 1) * (1+min(-move.y,0)),
                _throttleDecay, Time.deltaTime);
        }

        for (var i = 0; i < _pickups.Count; i++)
        {
            var (l, time, t) = _pickups[i];
            if (t == null) continue;
            time = frac(time + Time.deltaTime / lerp(PickupTravelTimeMin, PickupTravelTimeMax, l));
            t.localScale = Vector3.one * lerp(PickupSizeMin, PickupSizeMax, l) * Zone.PowerPulse(time - .5f, ScaleExponent);
            t.position = new Vector3(t.position.x, t.position.y, (time - .5f) * PickupTravelDistance);
            _pickups[i] = (l, time, t);
        }
    }
}