using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using Random = UnityEngine.Random;

public class Mine : MonoBehaviour
{
    public GridObject GridObject;
    public MeshRenderer MeshRenderer;
    public int EmissionSubmesh;
    public AnimationCurve EmissionCurve;
    public float ActiveCycleDuration;
    public float ArmedCycleDuration;
    public float ActiveEmission;
    public float ArmedEmission;
    public Prototype HitEffect;
    public float Lifetime;

    public float ActivationDelay;
    public float BlastRange;
    public float BlastDelay;

    private float _startTime;
    private float _blastTime;
    private bool _blastCountdown;
    private bool _active;
    private Material _material;
    private float _pulseLerp;
    private float _emission;
    public float Damage { get; set; }
    public DamageType DamageType { get; set; }
    public EntityInstance Source { get; set; }
    public float Range { get; set; }

    private void Start()
    {
        _material = MeshRenderer.materials[EmissionSubmesh];
    }

    private void OnEnable()
    {
        _startTime = Time.time;
        _blastCountdown = false;
        _active = false;
        _emission = 0;
    }

    // Update is called once per frame
    void Update()
    {
        var position = transform.position;
        
        if (Time.time - _startTime > ActivationDelay && !_active)
        {
            _active = true;
        }
        
        if(_active)
        {
            _emission = lerp(_emission, _blastCountdown ? ArmedEmission : ActiveEmission, Time.deltaTime * 10);
            _pulseLerp += Time.deltaTime / (_blastCountdown ? ArmedCycleDuration : ActiveCycleDuration);
            _pulseLerp %= 1;
            if(!_blastCountdown)
            {
                foreach (var collider in Physics.OverlapSphere(position, BlastRange, 1))
                {
                    var hull = collider.GetComponent<HullCollider>();
                    if (hull)
                    {
                        _blastCountdown = true;
                        _blastTime = Time.time + BlastDelay;
                    }
                }
            }

            if ((position - Source.transform.position).magnitude > Range ||
                Time.time - _startTime > Lifetime ||
                _blastCountdown && Time.time > _blastTime)
            {
                Explode();
            }
        }
        _material.SetFloat("_Emission", _emission * EmissionCurve.Evaluate(_pulseLerp));
    }

    public void Explode()
    {
        var position = transform.position;
        foreach (var collider in Physics.OverlapSphere(position, BlastRange, 1 | (1 << 17)))
        {
            var shield = collider.GetComponent<ShieldManager>();
            if (shield && (shield.Entity.Shield != null && shield.Entity.Shield.Item.Active.Value && shield.Entity.Shield.CanTakeHit(DamageType, Damage)))
            {
                shield.Entity.Shield.TakeHit(DamageType, Damage);
                shield.ShowHit(position, sqrt(Damage));
            }
            var hull = collider.GetComponent<HullCollider>();
            if (hull && !(hull.Entity.Shield != null && hull.Entity.Shield.Item.Active.Value && hull.Entity.Shield.CanTakeHit(DamageType, Damage)))
            {
                hull.SendSplash(Damage, DamageType, Source.Entity, (collider.transform.position - position).normalized);
            }
        }

        var ht = HitEffect.Instantiate<Transform>();
        ht.position = position;
        GetComponent<Prototype>().ReturnToPool();
    }
}
