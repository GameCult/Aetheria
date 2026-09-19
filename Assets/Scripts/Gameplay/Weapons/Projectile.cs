using System.Collections;
using UnityEngine;
using static CultMath.math;

// Cut 3 (docs/fire-control-cut.md): the raycast, shield branch and SendHit are deleted -- FireControl already
// decided this shot's fate before this object was ever spawned (R8). What is left is pure flight-path
// presentation: fly, and disappear at Range or ShotId's resolution, whichever comes first. Airburst
// (AirburstDistance/AirburstRange) is Cut 4's -- untouched here.
public class Projectile : MonoBehaviour
{
    public TrailRenderer Trail;
    public float Gravity;
    public float Drag = .1f;
    public Prototype HitEffect;

    public float AirburstDistance;
    public float AirburstRange;

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
