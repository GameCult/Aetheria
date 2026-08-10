using System;
using UnityEngine;

public class TimedDestroy : MonoBehaviour
{
    public float Duration;

    private float _startTime;

    private void Start()
    {
        _startTime = Time.time;
    }

    public void Update()
    {
        if(Time.time - _startTime > Duration) Destroy(gameObject);
    }
}