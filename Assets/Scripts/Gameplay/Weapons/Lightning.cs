using System;
using UnityEngine;

public class Lightning : MonoBehaviour
{
    public LightningCompute LightningCompute;
    public float HitRadius;
    
    public float ImpactIntensity { get; set; } = 1;
    public EntityInstance Source { get; set; }
    public float Range { get; set; }
    public EntityInstance Target { get; set; }
    public Transform Barrel { get; set; }

    private bool _colliderHit;
    private Vector3 _colliderLocalPosition;
    private Transform _colliderTransform;
    private Vector3 _endpoint;

    public void Fire()
    {
        _colliderHit = false;
        LightningCompute.OnLeaderComplete = null;
        LightningCompute.FixedEndpoint = false;
        _endpoint = Barrel.position + Barrel.forward * Range;
        if (AetheriaYmirPhysicsBridge.Instance.TryCastZoneHulls(
                Source?.ZoneRenderer,
                Source?.Entity,
                Barrel.position,
                Barrel.forward,
                Range,
                HitRadius,
                out var hits))
        {
            foreach (var hit in hits)
            {
                var hull = hit.Hull;
                var entity = hull.Entity;
                if (entity.Shield != null && entity.Shield.Item.Active.Value)
                {
                    LightningCompute.OnLeaderComplete = () =>
                    {
                        hit.Shield?.ShowHit(hit.Point, Mathf.Max(0.01f, ImpactIntensity));
                    };
                }
                else
                {
                    LightningCompute.OnLeaderComplete = null;
                }

                _colliderHit = true;
                _colliderLocalPosition = hull.transform.InverseTransformPoint(hit.Point);
                _colliderTransform = hull.transform;
                LightningCompute.FixedEndpoint = true;
                _endpoint = hit.Point;
                break;
            }
        }

        LightningCompute.OnPulseComplete = () => 
            GetComponent<Prototype>().ReturnToPool();
        LightningCompute.StartAnimation();
    }

    private void Update()
    {
        if (Barrel == null) return;

        if (_colliderHit)
        {
            if (_colliderTransform) _endpoint = _colliderTransform.TransformPoint(_colliderLocalPosition);
        }

        LightningCompute.EndPosition = _endpoint;
        LightningCompute.StartPosition = Barrel.position;
    }
}
