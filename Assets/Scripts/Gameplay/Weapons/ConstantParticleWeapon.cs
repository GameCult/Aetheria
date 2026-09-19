using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Cut 4 (docs/fire-control-cut.md): the trigger-collider plumbing and SendSplash are deleted -- FireControl
// already rolls this weapon's damage once per GameplaySettings.BeamResolveInterval (ConstantWeapon.Execute)
// before these particles ever collide with anything (R8). What is left is pure visual emission; nothing here
// decides or applies damage any more.
public class ConstantParticleWeapon : MonoBehaviour
{
    public ParticleSystem[] Particles;

    public EntityInstance Source { get; set; }
    public EntityInstance Target { get; set; }

    private bool _stopping;
    private float _emission;

    private void Start()
    {
        _emission = Particles[0].emission.rateOverTime.constant;
    }

    private void OnEnable()
    {
        foreach(var p in Particles)
            p.enableEmission = true;
        _stopping = false;
    }

    public void Initialize()
    {
        foreach(var p in Particles)
        {
            var main = p.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = Source.LocalSpace;
        }

        var trigger = Particles[0].trigger;
        while(trigger.colliderCount > 0)
            trigger.RemoveCollider(0);
    }

    private void Update()
    {
        if (Source == null) return;
        if (_stopping && Particles.All(p=>p.particleCount == 0))
        {
            GetComponent<Prototype>().ReturnToPool();
            return;
        }
    }

    public void Stop()
    {
        foreach(var p in Particles)
            p.enableEmission = false;
        _stopping = true;
    }
}
