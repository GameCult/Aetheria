/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickableCollider : MonoBehaviour
{
    private static readonly List<ClickableCollider> ActiveClickables = new List<ClickableCollider>();

    private Renderer[] _renderers;

    public event Action<ClickableCollider, PointerEventData, Ray, AetheriaYmirClickableHit> OnClick;

    public static IReadOnlyList<ClickableCollider> Active => ActiveClickables;

    public Bounds ClickBounds
    {
        get
        {
            if (_renderers == null)
                RefreshYmirBoundsSource();

            if (_renderers == null || _renderers.Length == 0)
                return new Bounds(transform.position, Vector3.one * 0.5f);

            var firstRenderer = FirstLiveRenderer();
            if (firstRenderer == null)
                return new Bounds(transform.position, Vector3.one * 0.5f);

            var bounds = firstRenderer.bounds;
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null || renderer == firstRenderer)
                    continue;

                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }
    }

    public void Click(PointerEventData eventData, Ray ray, AetheriaYmirClickableHit hit) => OnClick?.Invoke(this, eventData, ray, hit);

    public void Clear()
    {
        OnClick = null;
    }

    public void RefreshYmirBoundsSource()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    private void Awake()
    {
        RefreshYmirBoundsSource();
    }

    private void OnEnable()
    {
        if (_renderers == null || _renderers.Length == 0)
            RefreshYmirBoundsSource();

        if (!ActiveClickables.Contains(this))
            ActiveClickables.Add(this);
    }

    private void OnDisable()
    {
        ActiveClickables.Remove(this);
    }

    void Start()
    {
        var proto = GetComponent<Prototype>();
        if(proto != null)
            proto.OnReturnToPool += () => OnClick = null;
    }

    private Renderer FirstLiveRenderer()
    {
        for (var i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                return _renderers[i];
        }

        return null;
    }
}
