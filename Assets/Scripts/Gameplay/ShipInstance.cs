using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using CultMath;
using CultMath.UnityBridge;
using static CultMath.math;

public class ShipInstance : EntityInstance
{
    private static int _shipIndex;
    public TractorBeam TractorBeam;
    private class ThrusterInstance
    {
        public Thruster Thruster;
        public ParticleSystem System;
        public float BaseEmission;
        public int MaxParticleCount;
    }

    private ThrusterInstance[] _thrusters;
    
    public Ship Ship { get; private set; }

    private void Start()
    {
        var tractorBeamMain = TractorBeam.ParticleSystem.main;
        tractorBeamMain.customSimulationSpace = LocalSpace;
    }

    public override void SetEntity(ZoneRenderer zoneRenderer, Entity entity)
    {
        base.SetEntity(zoneRenderer, entity);
        var ship = entity as Ship;
        if (ship == null)
        {
            Debug.LogError($"Attempted to assign non-ship entity to {gameObject.name} ship instance prefab!");
            return;
        }
        Ship = ship;
        _thrusters = ship.GetBehaviors<Thruster>().Select(thruster =>
            {
                var effectData = (ThrusterData) thruster.Data;
                var particles = Instantiate(EngineAssets.Load<ParticleSystem>(effectData.ParticlesPrefab), transform, false);
                var thrusterHardpoint = ThrusterHardpoints
                    .FirstOrDefault(t => t.name == thruster.Item.Hardpoint.Transform);
                ThrusterHardpoint.WireExhaust(particles, thrusterHardpoint);
                // if (!string.IsNullOrEmpty(thruster.Item.Data.SoundEffectTrigger) && thrusterHardpoint != null)
                // {
                //     AkSoundEngine.RegisterGameObj(thrusterHardpoint.gameObject);
                //     AkSoundEngine.PostEvent(thruster.Item.Data.SoundEffectTrigger, thrusterHardpoint.gameObject);
                // }

                return new ThrusterInstance
                {
                    Thruster = thruster,
                    System = particles,
                    BaseEmission = particles.emission.rateOverTimeMultiplier,
                    MaxParticleCount = 0
                };
            })
            .ToArray();

        foreach (var particle in _thrusters)
        {
            particle.System.gameObject.SetActive(false);
        }
    }

    protected override void ShowUnfadedElements()
    {
        base.ShowUnfadedElements();
        foreach (var particle in _thrusters)
        {
            particle.System.gameObject.SetActive(true);
        }
    }

    protected override void HideUnfadedElements()
    {
        base.HideUnfadedElements();
        foreach (var particle in _thrusters)
        {
            particle.System.gameObject.SetActive(false);
        }
    }

    public override void Update()
    {
        base.Update();

        TractorBeam.Power = Entity.TractorPower;
        TractorBeam.Direction = Entity.Aim.ToUnity();

        foreach (var thrusterInstance in _thrusters)
        {
            var emissionModule = thrusterInstance.System.emission;
            thrusterInstance.MaxParticleCount = thrusterInstance.System.particleCount;
            // Cut 8 (operator ask 2026-09-19): intent (Axis) times condition (Thruster.Condition, the item's
            // resolved Thrust against its own perfect-conditions Thrust) -- no durability/heat/power arithmetic
            // recomputed here, only the same emission math that already existed.
            emissionModule.rateOverTimeMultiplier = thrusterInstance.BaseEmission * thrusterInstance.Thruster.Axis * thrusterInstance.Thruster.Condition;
        }

        transform.rotation = DrawAhead.Rotation(Ship, ActionGameManager.Clock.Lead).ToUnity();
    }
}