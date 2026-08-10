using System;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using Random = UnityEngine.Random;

public class GridObject : MonoBehaviour
{
    public float RotationSpeed;
    public float GridOffset;
    public float GridAttraction;
    public float Gravity;
    public float Drag = .1f;
    public float LaunchDrag;
    
    private float2 _selfVelocity;
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
        t.localRotation = Quaternion.Euler(sin(Time.time - _timeOffset * RotationSpeed) * 90, 0, cos(Time.time - _timeOffset * RotationSpeed) * 90);
        
        if (Zone == null) return;

        var position = t.position;
        var gridHeight = Zone.GetHeight(position.Flatland()) + GridOffset;
        Velocity += Vector3.up * (sign(gridHeight - position.y) * GridAttraction * Time.deltaTime);
        Velocity *= max(0, 1 - LaunchDrag * Time.deltaTime);
        var normal = Zone.GetNormal(position.Flatland());
        var force = new float2(normal.x, normal.z);
        var forceMagnitude = lengthsq(force);
        if (forceMagnitude > .001f)
        {
            var fa = 1 / (1 - forceMagnitude) - 1;
            _selfVelocity += normalize(force) * Zone.Settings.GravityStrength * fa * Gravity;
        }

        _selfVelocity *= max(0, 1 - Drag * Time.deltaTime);
        t.position = position + (Velocity + new Vector3(_selfVelocity.x, 0, _selfVelocity.y)) * Time.deltaTime;
    }
}