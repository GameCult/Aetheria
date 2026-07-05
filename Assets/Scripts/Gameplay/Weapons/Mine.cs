using System;
using System.Collections;
using System.Collections.Generic;
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
    public float ImpactIntensity { get; set; } = 1;
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
        if (Time.time - _startTime > ActivationDelay && !_active)
        {
            _active = true;
        }
        
        if(_active)
        {
            _emission = Mathf.Lerp(_emission, _blastCountdown ? ArmedEmission : ActiveEmission, Time.deltaTime * 10);
            _pulseLerp += Time.deltaTime / (_blastCountdown ? ArmedCycleDuration : ActiveCycleDuration);
            _pulseLerp %= 1;

            if (Time.time - _startTime > Lifetime)
            {
                Explode();
            }
        }
        _material.SetFloat("_Emission", _emission * EmissionCurve.Evaluate(_pulseLerp));
    }

    public void Explode()
    {
        var position = transform.position;
        var ht = HitEffect.Instantiate<Transform>();
        ht.position = position;
        GetComponent<Prototype>().ReturnToPool();
    }
}
