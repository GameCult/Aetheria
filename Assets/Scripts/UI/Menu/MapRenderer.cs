/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UI;
using static Unity.Mathematics.math;
using int2 = Unity.Mathematics.int2;

public class MapRenderer : MonoBehaviour
{
    public ActionGameManager GameManager;
    public ZoneRenderer ZoneRenderer;
    public TextMeshProUGUI Title;
    public Camera MapOverlayCamera;
    public Camera GravityCamera;
    public Camera TintCamera;
    public Camera InfluenceCamera;
    public Image OverlayDisplay;
    public Image GravityDisplay;
    public Image TintDisplay;
    public Image InfluenceDisplay;
    public float Scale;
    public float2 Position;
    public float IconSize = 1f/128;

    private RectTransform _rect;
    private RenderTexture _mapTexture;
    private RenderTexture _gravityTexture;
    private RenderTexture _tintTexture;
    private RenderTexture _influenceTexture;
    private int2 _size;
    private bool _init;
    
    void Start()
    {
        _rect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        _init = true;
        MapOverlayCamera.gameObject.SetActive(true);
        GravityCamera.gameObject.SetActive(true);
        TintCamera.gameObject.SetActive(true);
        InfluenceCamera.gameObject.SetActive(true);
        Title.text = $"Zone: {GameManager.Zone.GalaxyZone.Name}";
        
        // If hiding minimap asteroids, turn them back on for the map screen
        if (!ActionGameManager.PlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap)
            ZoneRenderer.ShowAsteroidUI = true;
    }

    private void OnDisable()
    {
        if (_mapTexture != null)
        {
            ReleaseTextures();
        }
        MapOverlayCamera.gameObject.SetActive(false);
        GravityCamera.gameObject.SetActive(false);
        TintCamera.gameObject.SetActive(false);
        InfluenceCamera.gameObject.SetActive(false);
        
        // If hiding minimap asteroids, turn them back off when leaving the map screen
        if (!ActionGameManager.PlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap)
            ZoneRenderer.ShowAsteroidUI = false;
    }

    void ReleaseTextures()
    {
        _mapTexture.Release();
        _mapTexture = null;
        _gravityTexture.Release();
        _gravityTexture = null;
        _tintTexture.Release();
        _tintTexture = null;
        _influenceTexture.Release();
        _influenceTexture = null;
    }

    void LateUpdate()
    {
        var size = int2(Screen.width, Screen.height);
        if (_init || size.x != _size.x || size.y != _size.y)
        {
            _init = false;
            _size = size;
            if (_mapTexture != null)
            {
                ReleaseTextures();
            }
            
            _mapTexture = new RenderTexture(_size.x, _size.y, 0, RenderTextureFormat.Default);
            MapOverlayCamera.targetTexture = _mapTexture;
            OverlayDisplay.material.SetTexture("_DetailTex", _mapTexture);
            
            _gravityTexture = new RenderTexture(_size.x, _size.y, 0, RenderTextureFormat.RFloat);
            GravityCamera.targetTexture = _gravityTexture;
            GravityDisplay.material.SetTexture("_DetailTex", _gravityTexture);
            
            _tintTexture = new RenderTexture(_size.x / 2, _size.y / 2, 0, RenderTextureFormat.RGB111110Float);
            TintCamera.targetTexture = _tintTexture;
            TintDisplay.material.SetTexture("_DetailTex", _tintTexture);
            
            _influenceTexture = new RenderTexture(_size.x, _size.y, 0, RenderTextureFormat.RFloat);
            InfluenceCamera.targetTexture = _influenceTexture;
            InfluenceDisplay.material.SetTexture("_DetailTex", _influenceTexture);
        }

        var pos = ((Vector2) Position).Flatland(1);
        
        MapOverlayCamera.transform.position = pos;
        MapOverlayCamera.orthographicSize = _size.y * Scale * .5f;
        
        GravityCamera.transform.position = pos;
        GravityCamera.orthographicSize = _size.y * Scale * .5f;
        GravityDisplay.material.SetFloat("_Scale", Scale / 2);
        
        TintCamera.transform.position = pos;
        TintCamera.orthographicSize = _size.y * Scale * .5f;
        
        InfluenceCamera.transform.position = pos;
        InfluenceCamera.orthographicSize = _size.y * Scale * .5f;
        
        GameManager.ZoneRenderer.SetIconSize(IconSize * Scale);
    }
}
