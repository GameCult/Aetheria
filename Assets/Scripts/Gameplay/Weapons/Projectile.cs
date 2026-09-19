using System.Collections;
using UnityEngine;
using static CultMath.math;

// Cut 3 (docs/fire-control-cut.md): the raycast, shield branch and SendHit are deleted -- FireControl already
// decided this shot's fate before this object was ever spawned (R8). What is left is pure flight-path
// presentation: fly, and disappear at Range or ShotId's resolution, whichever comes first.
// Cut 6b, 6.2: the flak-round blast now resolves in the simulation (FireControl.Step calls Splash instead of
// Apply at arrival) -- a MonoBehaviour calling Splash would be exactly the authority Cut 3 spent itself
// deleting, so the blast-distance/blast-radius fields this object used to carry for that are gone. This
// object's job stays fly-and-disappear.
public class Projectile : MonoBehaviour
{
    public TrailRenderer Trail;
    public float Gravity;
    public float Drag = .1f;
    public Prototype HitEffect;

    private bool _alive;

    // Cut 3: FireControl.Fire's ShotId -- this projectile's handle onto Zone.ShotCommitted/ShotResolved.
    public int ShotId { get; set; }
    public Zone Zone { get; set; }

    public Vector3 StartPosition { get; set; }
    public Vector3 Velocity { get; set; }
    public Entity SourceEntity { get; set; }
    public float Range { get; set; }

    private void OnEnable()
    {
        _alive = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (SourceEntity == null) return;

        if(_alive)
        {
            var t = transform;
            Velocity -= Vector3.up * (Gravity * Time.deltaTime);
            Velocity *= max(0, 1 - Drag * Time.deltaTime);
            t.forward = Velocity.normalized;

            transform.position += Velocity * Time.deltaTime;
            var distanceTraveled = (transform.position - StartPosition).magnitude;
            if(distanceTraveled > Range)
                StartCoroutine(Kill());
        }
    }

    IEnumerator Kill()
    {
        _alive = false;
        var startTime = Time.time;
        var lifetime = Trail.time;
        while (Time.time - startTime < lifetime)
            yield return null;
        GetComponent<Prototype>().ReturnToPool();
    }
}
