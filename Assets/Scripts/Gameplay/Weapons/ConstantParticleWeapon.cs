using System.Linq;
using UnityEngine;

public class ConstantParticleWeapon : MonoBehaviour
{
    public ParticleSystem[] Particles;
    public float CastRadius = 0.25f;
    
    public float Range { get; set; }
    public EntityInstance Source { get; set; }
    public EntityInstance Target { get; set; }

    private bool _stopping;

    private void OnEnable()
    {
        foreach(var p in Particles)
        {
            var emission = p.emission;
            emission.enabled = true;
        }
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
    }

    private void Update()
    {
        if (Source == null) return;
        if (_stopping && Particles.All(p=>p.particleCount == 0))
        {
            GetComponent<Prototype>().ReturnToPool();
            return;
        }

        if (_stopping || Target == null || Range <= 0)
            return;

        if (AetheriaYmirPhysicsBridge.Instance.TryCastTargetHulls(
                Target,
                transform.position,
                transform.forward,
                Range,
                CastRadius,
                out var hits))
        {
            foreach (var hit in hits)
            {
                break;
            }
        }
    }

    public void Stop()
    {
        foreach(var p in Particles)
        {
            var emission = p.emission;
            emission.enabled = false;
        }
        _stopping = true;
    }
}
