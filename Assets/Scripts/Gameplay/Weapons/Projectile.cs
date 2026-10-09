using System;
using System.Collections;
using UniRx;
using UnityEngine;

// A ballistic round, drawn by DrawAhead's round statics and deciding nothing. The simulation owns the round's whole
// flight (the PendingShot copy taken at spawn, replaced by the sim's record at ShotCommitted, and the outcome it
// publishes), so this object stores no motion state of its own and computes no position, time or range: each frame it
// asks DrawAhead.Round where the round is at sim time plus the clock's Lead (the same draw-ahead ships use), and the
// round ends on the sim's word: ShotResolved stops a round that hit or burst where DrawAhead.RoundStop puts it, and
// DrawAhead.RoundOver ends one the sim published as a miss. The barrel is only where the round is first seen: its
// offset from the sim line decays to zero over BlendTime sim seconds. The trail's fade after the end stays real time
// (pure presentation, ruling sim-speed-presentation).
public class Projectile : MonoBehaviour
{
    public TrailRenderer Trail;
    public Prototype HitEffect;

    // Sim seconds over which the barrel's offset from the sim line decays to nothing.
    public float BlendTime = .1f;

    private PendingShot _shot;
    private Zone _zone;
    private CultMath.float3 _barrel;
    private ShotResult? _known;
    private bool _alive;
    private CompositeDisposable _binding;

    // Takes the shot as the sim holds it, the zone whose sim time it is drawn at, and where the barrel is as this round is first seen.
    public void Launch(Zone zone, PendingShot shot, Vector3 barrel)
    {
        _zone = zone;
        _shot = shot;
        _barrel = new CultMath.float3(barrel.x, barrel.y, barrel.z);
        _known = null;
        _binding?.Dispose();
        _binding = new CompositeDisposable(
            zone.ShotCommitted.Where(o => o.ShotId == shot.ShotId).Subscribe(Committed),
            zone.ShotResolved.Where(o => o.ShotId == shot.ShotId).Subscribe(Resolve));
        _alive = true;
        transform.forward = new Vector3(shot.TravelDirection.x, 0, shot.TravelDirection.y);
        Draw();
    }

    void Update()
    {
        if (_alive) Draw();
    }

    private void Draw()
    {
        var time = _zone.Time + ActionGameManager.Clock.Lead;
        transform.position = V(DrawAhead.Round(_shot, _known, _barrel, BlendTime, time));
        if (DrawAhead.RoundOver(_shot, _known, time)) Finish();
    }

    // The sim's commit: the record it holds now (a contact burst's arrival is shortened here) and what it decided.
    private void Committed(ShotOutcome outcome)
    {
        if (_zone.TryGetShot(outcome.ShotId, out var record)) _shot = record;
        _known = outcome.Result;
    }

    // The simulation's verdict. A round that hit or burst stops where the sim put it; a miss keeps flying.
    private void Resolve(ShotOutcome outcome)
    {
        if (!_alive) return;
        _known = outcome.Result;
        if (outcome.Result == ShotResult.Miss) return;
        transform.position = V(DrawAhead.RoundStop(_shot, outcome, _barrel, BlendTime));
        if (HitEffect != null) HitEffect.Instantiate<Transform>().position = transform.position;
        Finish();
    }

    private static Vector3 V(CultMath.float3 p) => new Vector3(p.x, p.y, p.z);

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
