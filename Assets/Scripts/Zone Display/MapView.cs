/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class MapView : MonoBehaviour
{
    public Camera Main;
    public Camera MinimapGravity;
    public Camera Minimap;
    public MeshRenderer GravityRenderer;
    public float ZoomSpeed;
    [FormerlySerializedAs("SectorRenderer")] public ZoneRenderer ZoneRenderer;

    private Transform _minimapTransform;
    private CopyTransform _minimapCopyTransform;
    private RenderTexture _minimapTexture;
    private Transform _minimapGravityTransform;
    private CopyTransform _minimapGravityCopyTransform;
    private RenderTexture _minimapGravityTexture;
    private Transform _gravityRendererTransform;
    private CopyTransform _gravityRendererCopyTransform;
    private RenderTexture _fullscreenGravityTexture;
    private AetheriaInput.UIActions _input;
    private float _viewDistance = 1024;
    private float _aspectRatio;
    private Vector3 _previousGravityRendererScale;
    
    void OnEnable()
    {
        _aspectRatio = (float) Screen.width / Screen.height;
        Main.gameObject.SetActive(false);
        _minimapTexture = Minimap.targetTexture;
        _minimapTransform = Minimap.transform;
        _minimapCopyTransform = Minimap.GetComponent<CopyTransform>();
        _minimapCopyTransform.enabled = false;
        _minimapGravityTexture = MinimapGravity.targetTexture;
        _minimapGravityTransform = MinimapGravity.transform;
        _minimapGravityCopyTransform = MinimapGravity.GetComponent<CopyTransform>();
        _minimapGravityCopyTransform.enabled = false;
        _gravityRendererTransform = GravityRenderer.transform;
        _gravityRendererCopyTransform = GravityRenderer.GetComponent<CopyTransform>();
        _gravityRendererCopyTransform.enabled = false;
        _previousGravityRendererScale = _gravityRendererTransform.localScale;

        _fullscreenGravityTexture = new RenderTexture(Screen.width / 2, Screen.height / 2, 1, RenderTextureFormat.RFloat);
        GravityRenderer.material.mainTexture = _fullscreenGravityTexture;
        MinimapGravity.targetTexture = _fullscreenGravityTexture;
        Minimap.targetTexture = null;
        SetZoom();
    }

    void SetZoom()
    {
        //ZoneRenderer.ViewDistance = _viewDistance * _aspectRatio;
        Minimap.orthographicSize = MinimapGravity.orthographicSize = _viewDistance;
        _gravityRendererTransform.localScale = new Vector3(_viewDistance*_aspectRatio*2, _viewDistance*2, 1);
    }

    // public void BindInput(AetheriaInput.UIActions input)
    // {
    //     _input = input;
    //     _input.ScrollWheel.performed += OnScroll;
    // }

    // private void OnScroll(InputAction.CallbackContext context)
    // {
    //     _viewDistance *= (1 - context.ReadValue<Vector2>().y * ZoomSpeed);
    //     SetZoom();
    // }

    void Update()
    {
        if (_input.Click.ReadValue<float>() > .5f)
        {
            var delta = _input.Drag.ReadValue<Vector2>();
            
            var pos = _minimapTransform.position;
            pos.x -= delta.x * _viewDistance / Screen.height;
            pos.z -= delta.y * _viewDistance / Screen.height;
        
            var gravPos = _gravityRendererTransform.position;
            gravPos.x = pos.x;
            gravPos.z = pos.z;

            _gravityRendererTransform.position = gravPos;
            Main.transform.position = _minimapTransform.position = _minimapGravityTransform.position = pos;
        }
    }

    private void OnDisable()
    {
        // _input.ScrollWheel.performed -= OnScroll;
        
        Main.gameObject.SetActive(true);
        _minimapGravityCopyTransform.enabled = true;
        _minimapCopyTransform.enabled = true;
        _gravityRendererCopyTransform.enabled = true;
        
        Minimap.targetTexture = _minimapTexture;
        MinimapGravity.targetTexture = _minimapGravityTexture;
        GravityRenderer.material.mainTexture = _minimapGravityTexture;
        _fullscreenGravityTexture.Release();
    }
}
