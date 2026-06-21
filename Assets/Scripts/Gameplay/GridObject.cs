using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class GridObject : MonoBehaviour
{
    public float RotationSpeed;
    public float GridOffset;
    public float GridAttraction;
    public float Gravity;
    public float Drag = .1f;
    public float LaunchDrag;
    
    private float _timeOffset;
    
    public Zone Zone { get; set; }
    public Vector3 Velocity { get; set; }

    private void Start()
    {
        _timeOffset = Random.value * 100;
    }

    void Update()
    {
        var t = transform;
        t.localRotation = Quaternion.Euler(Mathf.Sin(Time.time - _timeOffset * RotationSpeed) * 90, 0, Mathf.Cos(Time.time - _timeOffset * RotationSpeed) * 90);
    }
}
