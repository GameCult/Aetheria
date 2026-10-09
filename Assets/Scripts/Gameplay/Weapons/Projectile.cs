using System;
using System.Collections;
using UniRx;
using UnityEngine;
using static CultMath.math;

// A ballistic round, drawn on FireControl.RoundAt and deciding nothing. The simulation owns the round's whole flight
// (origin, direction, speed, fire time, arrival, range: the PendingShot copy taken at spawn), so this object stores no
// velocity or position of its own and integrates nothing: each frame it asks RoundAt where the round is at sim time
// plus the clock's Lead (the same draw-ahead ships use), and the simulation's ShotResolved ends a round that hit or
// burst. A round that missed flies on to the MaxRange point RoundAt clamps at, and is done there. The barrel is only
// where the round is first seen: its offset from the sim line decays to zero over BlendTime sim seconds. The trail's
// fade after the end stays real time (pure presentation, ruling sim-speed-presentation).
public class Projectile : MonoBehaviour
{
    public TrailRenderer Trail;
    public Prototype HitEffect;

    // Sim seconds over which the barrel's offset from the sim line decays to nothing.
    public float BlendTime = .1f;

    private PendingShot _shot;
    private Zone _zone;
    private Vector3 _barrelOffset;
    private bool _alive;
    private IDisposable _binding;

    // Takes the frozen shot, the zone whose sim time it is drawn at, and where the barrel is as this round is first seen.
    public void Launch(Zone zone, PendingShot shot, Vector3 barrel)
    {
        _zone = zone;
        _shot = shot;
        var origin = shot.FireOrigin;
        _barrelOffset = barrel - new Vector3(origin.x, origin.y, origin.z);
        _binding?.Dispose();
        _binding = zone.ShotResolved.Where(o => o.ShotId == shot.ShotId).Subscribe(Resolve);
        _alive = true;
        transform.forward = new Vector3(shot.TravelDirection.x, 0, shot.TravelDirection.y);
        transform.position = barrel;
    }

    private Vector3 At(CultMath.float2 planar, float time)
    {
        var blend = BlendTime > 0 ? saturate(1 - (time - _shot.FireTime) / BlendTime) : 0f;
        return new Vector3(planar.x, _shot.FireOrigin.y, planar.y) + _barrelOffset * blend;
    }

    void Update()
    {
        if (!_alive) return;
        var time = _zone.Time + ActionGameManager.Clock.Lead;
        transform.position = At(FireControl.RoundAt(_shot, time), time);
        var end = _shot.Speed > .01f ? _shot.FireTime + _shot.MaxRange / _shot.Speed : _shot.FireTime;
        if (time >= end) Finish();
    }

    // The simulation's verdict. A round that hit or burst stops where the sim put it, at the outcome's burst point,
    // else where it arrives on its line; a miss keeps flying.
    private void Resolve(ShotOutcome outcome)
    {
        if (!_alive || (outcome.Result != ShotResult.Hit && outcome.Result != ShotResult.Burst)) return;
        var planar = outcome.HasBurstPoint ? outcome.BurstPoint : FireControl.RoundAt(_shot, _shot.ArrivalTime);
        transform.position = At(planar, _shot.ArrivalTime);
        if (HitEffect != null) HitEffect.Instantiate<Transform>().position = transform.position;
        Finish();
    }

    private void Finish()
    {
        _alive = false;
        _binding?.Dispose();
        _binding = null;
        StartCoroutine(Fade());
    }

    void OnDisable()
    {
        _alive = false;
        _binding?.Dispose();
        _binding = null;
    }

    IEnumerator Fade()
    {
        var startTime = Time.time;
        var lifetime = Trail.time;
        while (Time.time - startTime < lifetime)
            yield return null;
        GetComponent<Prototype>().ReturnToPool();
    }
}
