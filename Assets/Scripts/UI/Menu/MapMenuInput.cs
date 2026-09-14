/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections;
using System.Collections.Generic;
using CultMath;
using CultMath.UnityBridge;
using UnityEngine;
using UnityEngine.EventSystems;

public class MapMenuInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    public MapRenderer Map;
    public float ZoomSpeed;

    private Vector2 _startMousePosition;

    private Vector2 _startMapPosition;
    private RectTransform _mapRect;

    private void Start()
    {
        _mapRect = Map.GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _startMousePosition = eventData.position;
        _startMapPosition = Map.Position.ToUnity();
    }

    public void OnDrag(PointerEventData eventData)
    {
        Map.Position = (_startMapPosition - (eventData.position - _startMousePosition) * Map.Scale).ToCultMath();
    }

    public void OnScroll(PointerEventData eventData)
    {
        Vector3[] mapCorners = new Vector3[4];
        _mapRect.GetWorldCorners(mapCorners);
        var mapCenter = ((mapCorners[2] + mapCorners[0]).ToCultMath() / 2).xy;
        var oldPointerPosition = Map.Position + (eventData.position.ToCultMath() - mapCenter) * Map.Scale;
        Map.Scale *= 1 - eventData.scrollDelta.y * ZoomSpeed;
        var pointerPosition = Map.Position + (eventData.position.ToCultMath() - mapCenter) * Map.Scale;
        Map.Position += oldPointerPosition - pointerPosition;
    }
}
