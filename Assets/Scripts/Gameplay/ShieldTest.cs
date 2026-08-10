using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class ShieldTest : MonoBehaviour
{
    public float Acceleration;
    private float _velocity;

    // Update is called once per frame
    void Update()
    {
        _velocity += -sign(transform.position.x) * Acceleration * Time.deltaTime;
        transform.position += Vector3.right * (_velocity * Time.deltaTime);
    }

    private void OnCollisionEnter(Collision other)
    {
        _velocity = -_velocity;
    }
}
