/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickRaycaster : MonoBehaviour
{
    public LayerMask Layers;
    public event Action<PointerEventData> OnClickMiss;
    public Camera RayCamera;
    private ClickCatcher _clickCatcher;
    
    void Start()
    {
        _clickCatcher = GetComponent<ClickCatcher>();
        _clickCatcher.OnDown.Subscribe(pointer =>
        {
            var ray = RayCamera.ScreenPointToRay(pointer.position);
            var clickables = new List<ClickableCollider>();
            foreach (var clickable in ClickableCollider.Active)
            {
                if (clickable == null || ((1 << clickable.gameObject.layer) & Layers.value) == 0)
                    continue;

                clickables.Add(clickable);
            }

            if (AetheriaYmirPhysicsBridge.Instance.TryCastClickables(clickables, ray, 1000, out var hit))
                hit.Clickable.Click(pointer, ray, hit);
            else
                OnClickMiss?.Invoke(pointer);
        });
    }
}
