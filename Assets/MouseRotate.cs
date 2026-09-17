using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MouseRotate : MonoBehaviour
{
    public float Speed;
    public ClickRaycaster Click;

    private bool _rotating = false;
    private Vector3 _euler;

    void Start()
    {
        Click.OnClickMiss += data =>
        {
            _rotating = true;
            //Debug.Log("Started Rotating");
        };
        _euler = transform.localEulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        if (_rotating && !Mouse.current.leftButton.isPressed)
        {
            _rotating = false;
            //Debug.Log("Stopped Rotating");
        }
        if (_rotating)
        {
            var mouse = Mouse.current.delta.ReadValue();
            _euler.x -= mouse.y*Speed;
            _euler.y += mouse.x*Speed;
            transform.rotation = Quaternion.Euler(_euler);
        }
    }
}
