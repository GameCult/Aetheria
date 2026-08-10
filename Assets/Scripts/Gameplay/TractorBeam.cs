using System;
using UnityEngine;

public class TractorBeam : MonoBehaviour
{
    public ParticleSystem ParticleSystem;
    public float Radius;
    public float Traction;
    public float Distance;
    
    public float Power { get; set; }
    public Vector3 Direction { get; set; }

    public void Update()
    {
        transform.rotation = Quaternion.LookRotation(Direction);
        var emission = ParticleSystem.emission;
        if (Power > .01f)
        {
            var objectFound = false;
            foreach (var o in Physics.SphereCastAll(transform.position, Radius, Direction, Distance, 1 << 21))
            {
                var gridObject = o.collider.GetComponent<GridObject>();
                if (gridObject == null) continue;
                objectFound = true;
                gridObject.Velocity += Vector3.Normalize(transform.position - gridObject.transform.position) * (Traction * Time.deltaTime);
            }

            emission.rateOverTimeMultiplier = objectFound ? Power : 0;
        }
        else emission.rateOverTimeMultiplier = 0;
    }
}