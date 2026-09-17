/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CultMath.math;

//[ExecuteInEditMode]
public class SunMaterial : MonoBehaviour
{
    public float SpeedMultiplier = 1;
    public float FirstOffsetDomainRotationSpeed = .01f;
    public float FirstOffsetRotationSpeed = .01f;
    public float SecondOffsetDomainRotationSpeed = .01f;
    public float SecondOffsetRotationSpeed = .01f;
    public float AlbedoRotationSpeed = .01f;
    public Vector3 LightingDirection;

    private float _firstOffsetDomainRotation;
    private float _firstOffsetRotation;
    private float _secondOffsetDomainRotation;
    private float _secondOffsetRotation;
    private float _albedoRotation;

    private Material _material;
    private Orbit _orbit;

    void OnEnable()
    {
        var mesh = GetComponent<MeshRenderer>();
        if (mesh == null)
        {
            enabled = false;
            return;
        }

        _material = mesh.material;

        _firstOffsetDomainRotation = Random.value * Mathf.PI;
        _firstOffsetRotation = Random.value * Mathf.PI;
        _secondOffsetDomainRotation = Random.value * Mathf.PI;
        _secondOffsetRotation = Random.value * Mathf.PI;
        _albedoRotation = Random.value * Mathf.PI;
    }
    
    public void ApplyGradient(Gradient gradient)
    {
        var tex = gradient.ToTexture();
        if (_material.HasProperty("_ColorRamp"))
        {
            _material.SetTexture("_ColorRamp", tex);
        }
    }

    private void OnWillRenderObject()
    {
        _firstOffsetDomainRotation += FirstOffsetDomainRotationSpeed * SpeedMultiplier * Time.deltaTime;
        _firstOffsetRotation += FirstOffsetRotationSpeed * SpeedMultiplier * Time.deltaTime;
        _secondOffsetDomainRotation += SecondOffsetDomainRotationSpeed * SpeedMultiplier * Time.deltaTime;
        _secondOffsetRotation += SecondOffsetRotationSpeed * SpeedMultiplier * Time.deltaTime;
        _albedoRotation += AlbedoRotationSpeed * SpeedMultiplier * Time.deltaTime;

        _material.SetVector("_LightingDirection", LightingDirection);
        _material.SetMatrix("_FirstOffsetDomainRotation", Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, Mathf.Rad2Deg * _firstOffsetDomainRotation, 0), Vector3.one));
        _material.SetMatrix("_FirstOffsetRotation", Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(Mathf.Rad2Deg * Mathf.Sin(_firstOffsetRotation), Mathf.Rad2Deg * Mathf.Cos(_firstOffsetRotation), 0), Vector3.one));
        _material.SetMatrix("_SecondOffsetDomainRotation", Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, Mathf.Rad2Deg * _secondOffsetDomainRotation, 0), Vector3.one));
        _material.SetMatrix("_SecondOffsetRotation", Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(Mathf.Rad2Deg * Mathf.Sin(_secondOffsetRotation), Mathf.Rad2Deg * Mathf.Cos(_secondOffsetRotation), 0), Vector3.one));
        _material.SetMatrix("_AlbedoRotation", Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, Mathf.Rad2Deg * _albedoRotation, 0), Vector3.one));
    }
}
