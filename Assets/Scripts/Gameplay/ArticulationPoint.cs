using UnityEngine;

public class ArticulationPoint : MonoBehaviour
{
    public Transform Target;

    public int Group;

    public float YawMin;
    public float YawMax;

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

            var targetYaw = Mathf.Clamp(_yaw - yaw, YawMin, YawMax);
            var targetPitch = Mathf.Clamp(_pitch - pitch, PitchMin, PitchMax);

            if (Mathf.Abs(targetYaw - _yaw) < Speed * Time.deltaTime)
                _yaw = targetYaw;
            else _yaw = _yaw + Mathf.Sign(targetYaw - _yaw) * Speed * Time.deltaTime;

            if (_yaw < -360)
                _yaw += 360;
            if (_yaw > 360)
                _yaw -= 360;

            if (Mathf.Abs(targetPitch - _pitch) < Speed * Time.deltaTime)
                _pitch = targetPitch;
            else _pitch = _pitch + Mathf.Sign(targetPitch - _pitch) * Speed * Time.deltaTime;

            transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0);
        }
    }
}
