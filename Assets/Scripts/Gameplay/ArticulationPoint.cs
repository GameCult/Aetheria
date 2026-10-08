using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CultMath;
using static CultMath.math;

// Presentation only: the barrel follows the point its group's solution places (EntityInstance), which FireControl has
// already clamped to the arc. This decides no arc; Speed and the pitch clamp are how the picture slews.
public class ArticulationPoint : MonoBehaviour
{
    public Transform Target;

    public int Group;
    
    public float PitchMin;
    public float PitchMax;

    public float Speed;

    private float _yaw;
    private float _pitch;

    void Update()
    {
        if (Target)
        {
            var targetLocal = transform.InverseTransformPoint(Target.position);
            
            var yaw = Vector2.SignedAngle(new Vector2(0, 1), new Vector2(targetLocal.x, targetLocal.z));
            var pitch = Vector2.SignedAngle(new Vector2(1, 0), new Vector2(targetLocal.z, targetLocal.y));

            var targetYaw = _yaw - yaw;
            var targetPitch = clamp(_pitch - pitch, PitchMin, PitchMax);

            if (abs(targetYaw - _yaw) < Speed * Time.deltaTime)
                _yaw = targetYaw;
            else _yaw = _yaw + sign(targetYaw - _yaw) * Speed * Time.deltaTime;

            if(_yaw < -360)
                _yaw += 360;
            if(_yaw > 360)
                _yaw -= 360;

            if (abs(targetPitch - _pitch) < Speed * Time.deltaTime)
                _pitch = targetPitch;
            else _pitch = _pitch + sign(targetPitch - _pitch) * Speed * Time.deltaTime;
            
            transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0);
        }
    }
}
