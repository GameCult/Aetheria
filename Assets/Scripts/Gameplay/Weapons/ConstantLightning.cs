using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Cut 4 (docs/fire-control-cut.md): the SphereCast, shield branch and SendHit are deleted -- FireControl
// already rolls this weapon's damage once per GameplaySettings.BeamResolveInterval (ConstantWeapon.Execute)
// before this bolt is ever drawn (R8). What is left is pure endpoint presentation: fixed to the target's
// current position when there is one, out to Range otherwise -- the same convention Lightning.cs (Cut 3)
// uses for a discrete shot.
public class ConstantLightning : MonoBehaviour
{
    public LightningCompute Lightning;
    public float HitRadius;
    public AnimationCurve FadeCurve;
    public float StartWidth = 1;
    public float EndWidth = 1;
    public float FadeDuration;

    public Transform Barrel { get; set; }
    public EntityInstance Source { get; set; }
    public float Range { get; set; }
    public Transform TargetTransform { get; set; }

    private bool _stopping;
    private float _startTime;

    private void OnEnable()
    {
        _stopping = false;
        Lightning.StartAnimation();
    }

    private void Update()
    {
        if (Barrel == null) return;
        if (_stopping)
        {
            var lerp = (Time.time - _startTime) / FadeDuration;
            Lightning.StartWidth = FadeCurve.Evaluate(lerp) * StartWidth;
            Lightning.EndWidth = FadeCurve.Evaluate(lerp) * EndWidth;
            
            if (lerp > 1)
            {
                GetComponent<Prototype>().ReturnToPool();
                return;
            }
        }
        else
        {
            Lightning.StartWidth = StartWidth;
            Lightning.EndWidth = EndWidth;
        }
        
        Lightning.FixedEndpoint = TargetTransform != null;
        Lightning.EndPosition = TargetTransform != null
            ? TargetTransform.position
            : Barrel.position + Barrel.forward * Range;

        Lightning.StartPosition = Barrel.position;
    }

    public void Stop()
    {
        _stopping = true;
        _startTime = Time.time;
    }
}
