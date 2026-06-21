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
        emission.rateOverTimeMultiplier = Power > .01f ? Power : 0;
    }
}
