using System;
using System.Collections.Generic;
using UnityEngine;
using CultMath;
using CultMath.UnityBridge;
using static CultMath.math;
using Random = UnityEngine.Random;

// Drives the field ONLY by publishing capability events into a CapabilityEvents source that
// CapabilityPresentationBinder (a component alongside FieldDriver) feeds to it. This scene stands in
// for the capability owner the simulation does not have yet (docs/headless-playground-cut.md fork L:
// "Simulation emits capability events; presentations subscribe and decide nothing"). FieldTester
// itself decides nothing about how those events render -- that stays FieldDriver's job as a presenter.
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
    // The scene's slow viewing timings for a grab, published as the events' phase durations.
    public float GrabExtendTime;
    public float GrabEnvelopTime;
    public float GrabPullTime;

    private AetheriaInput _input;
    private bool _forceThrust;
    private float _throttleDecay = 2;
    private bool _directionalPush;

    // Damped thrust state: previously lived on TestField.Push/FrontTwist/RearTwist and was written
    // there directly every frame. Now that FieldTester only ever publishes a ThrustEvent, the damped
    // state itself has to live here instead -- FieldDriver's own Push/FrontTwist/RearTwist fields are
    // set from the event by FieldDriver.Thrust() and read only for the shader and the properties panel.
    private float2 _push;
    private float _frontTwist;
    private float _rearTwist;

    private CapabilityEvents _capabilityEvents;

    // Cycles through every DamageType on each absorbed hit so the operator can see how a presenter
    // that varies by type would behave; the field shield presentation itself ignores it today.
    private static readonly DamageType[] DamageTypeCycle = (DamageType[])Enum.GetValues(typeof(DamageType));
    private int _nextDamageTypeIndex;

    private List<(float lerp, float time, Transform transform)> _pickups = new List<(float lerp, float time, Transform transform)>();

    // This scene stands in for the simulation, so it owns its pickup's grab timeline and pose: one grab at a time.
    private bool _grabbing;
    private int _grabIndex;
    private float _grabElapsed;
    private float3 _grabStart;
    private bool _envelopPublished;
    private bool _pullPublished;

    private void Start()
    {
        _input = new AetheriaInput();
        _input.Player.Enable();

        _capabilityEvents = new CapabilityEvents();
        var binder = TestField.GetComponent<CapabilityPresentationBinder>();
        if (binder == null)
            Debug.LogError("FieldTester expects a CapabilityPresentationBinder on TestField's GameObject; no capability events will reach the field.");
        else
            binder.Bind(_capabilityEvents);

        // Impact absorption (fork L): what used to be FieldDriver's own click handler is now this
        // scene's stand-in for whatever will eventually detect a hit and publish AbsorbEvent.
        var clickableCollider = TestField.GetComponent<ClickableCollider>();
        if (clickableCollider != null)
        {
            clickableCollider.OnClick += (_, _, ray, hit) =>
            {
                var damageType = DamageTypeCycle[_nextDamageTypeIndex];
                _nextDamageTypeIndex = (_nextDamageTypeIndex + 1) % DamageTypeCycle.Length;
                _capabilityEvents.PublishAbsorb(new AbsorbEvent(hit.point.ToCultMath(), ray.direction.ToCultMath(), TestField.TestMagnitude, damageType, AbsorbLayer.Shield));
            };
        }

        Properties.AddField("Time Scale", () => Time.timeScale, f => Time.timeScale = f, 0, 2);
        Properties.AddField("FOV", () => Camera.fieldOfView, f => Camera.fieldOfView = f, 15, 45);
        Properties.AddButton("Melee", () => _capabilityEvents.PublishMelee(
            new MeleeEvent(float3(0, 0, 1), TestField.MeleeRange, TestField.MeleeAngle, TestField.MeleeDuration)));
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
                if (_grabbing) return;
                click.Clear();
                _grabbing = true;
                _grabIndex = i1;
                _grabElapsed = 0;
                _envelopPublished = _pullPublished = false;
                _grabStart = _pickups[i1].transform.position.ToCultMath();
                var id = new FloatingBodyId((uint) i1);
                _capabilityEvents.PublishGrab(new GrabEvent(id, GrabPhase.Started, _grabStart, 0));
                _capabilityEvents.PublishGrab(new GrabEvent(id, GrabPhase.Extend, _grabStart, GrabExtendTime));
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

    // Publishes Envelop and Pull once each at their cumulative boundaries, holds the pickup still until the pull and
    // then moves it along the same smoothstep as Zone's held pose, and ends with Completed and the pickup's removal.
    private void AdvanceGrab()
    {
        _grabElapsed += Time.deltaTime;
        var id = new FloatingBodyId((uint) _grabIndex);
        var pullStart = GrabExtendTime + GrabEnvelopTime;
        if (!_envelopPublished && _grabElapsed >= GrabExtendTime)
        {
            _envelopPublished = true;
            _capabilityEvents.PublishGrab(new GrabEvent(id, GrabPhase.Envelop, _grabStart, GrabEnvelopTime));
        }
        if (!_pullPublished && _grabElapsed >= pullStart)
        {
            _pullPublished = true;
            _capabilityEvents.PublishGrab(new GrabEvent(id, GrabPhase.Pull, _grabStart, GrabPullTime));
        }
        var pickup = _pickups[_grabIndex].transform;
        if (_pullPublished)
        {
            var progress = saturate((_grabElapsed - pullStart) / max(GrabPullTime, 1e-4f));
            pickup.position = lerp(_grabStart, TestField.transform.position.ToCultMath(), smoothstep(0, 1, progress)).ToUnity();
            if (progress >= 1)
            {
                _capabilityEvents.PublishGrab(new GrabEvent(id, GrabPhase.Completed, pickup.position.ToCultMath(), 0));
                Destroy(pickup.gameObject);
                var p = _pickups[_grabIndex];
                p.transform = null;
                _pickups[_grabIndex] = p;
                _grabbing = false;
            }
        }
    }

    private void Update()
    {
        var move = _forceThrust ? float2(0,1) : _input.Player.Move.ReadValue<Vector2>().ToCultMath();
        var turn = _input.Player.Turn.ReadValue<float>();

        float2 targetPush;
        float targetFrontTwist;
        float targetRearTwist;
        if (_directionalPush)
        {
            targetPush = move;
            targetFrontTwist = turn;
            targetRearTwist = turn;
        }
        else
        {
            targetPush = float2(0, move.y);
            targetFrontTwist = clamp(turn + move.x, -1, 1) * (1 + min(move.y, 0));
            targetRearTwist = clamp(turn - move.x, -1, 1) * (1 + min(-move.y, 0));
        }

        _push = damp(_push, targetPush, _throttleDecay, Time.deltaTime);
        _frontTwist = damp(_frontTwist, targetFrontTwist, _throttleDecay, Time.deltaTime);
        _rearTwist = damp(_rearTwist, targetRearTwist, _throttleDecay, Time.deltaTime);

        _capabilityEvents.PublishThrust(new ThrustEvent(_push, min(length(_push), 1), float2(_frontTwist, _rearTwist)));

        if (_grabbing) AdvanceGrab();

        for (var i = 0; i < _pickups.Count; i++)
        {
            var (l, time, t) = _pickups[i];
            if (t == null || (_grabbing && i == _grabIndex)) continue;
            time = frac(time + Time.deltaTime / lerp(PickupTravelTimeMin, PickupTravelTimeMax, l));
            t.localScale = Vector3.one * lerp(PickupSizeMin, PickupSizeMax, l) * Zone.PowerPulse(time - .5f, ScaleExponent);
            t.position = new Vector3(t.position.x, t.position.y, (time - .5f) * PickupTravelDistance);
            _pickups[i] = (l, time, t);
        }
    }
}
