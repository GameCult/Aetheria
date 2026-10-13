using System;
using UnityEngine;
using CultMath.UnityBridge;

// Presents the ship's grab: while the simulation holds an item, the beam looks along the ground-plane vector to the
// point the item was held from and emits at Power. Zone pulls the held item along the segment from the ship to that
// point (docs/aetheria-release-map.md L11), which is why GrabEvent.TargetPosition on Started is the exact aim and no
// pose lookup is needed. Idle, the beam turns with Direction and emits nothing. The simulation decides every grab fact.
public class TractorBeam : MonoBehaviour, IGrabPresenter
{
    public ParticleSystem ParticleSystem;

    public float Power { get; set; }
    public Vector3 Direction { get; set; }

    private bool _grabbing;
    private Vector3 _heldPoint;
    private Vector3 _heldDirection;

    // Cancelled can carry no position (a vanished item), so only Started is read for one.
    public void Grab(GrabEvent e)
    {
        switch (e.Phase)
        {
            case GrabPhase.Started:
                _grabbing = true;
                _heldPoint = e.TargetPosition.ToUnity();
                _heldDirection = Direction;
                break;
            case GrabPhase.Completed:
            case GrabPhase.Cancelled:
                _grabbing = false;
                break;
        }
    }

    public void Update()
    {
        var look = Direction;
        if (_grabbing)
        {
            var toHeld = _heldPoint - transform.position;
            toHeld.y = 0;
            if (toHeld.sqrMagnitude > 1e-6f) _heldDirection = toHeld;
            look = _heldDirection;
        }
        transform.rotation = Quaternion.LookRotation(look);
        var emission = ParticleSystem.emission;
        emission.rateOverTimeMultiplier = _grabbing ? Power : 0;
    }
}
