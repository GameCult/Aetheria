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
        // The beam draws nothing until it presents a sim grab (loot-3): no particles, and no pull; the simulation
        // decides every floating item's motion.
        var emission = ParticleSystem.emission;
        emission.rateOverTimeMultiplier = 0;
    }
}