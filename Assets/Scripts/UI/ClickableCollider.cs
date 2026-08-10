/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickableCollider : MonoBehaviour
{
    public event Action<ClickableCollider, PointerEventData, Ray, RaycastHit> OnClick;

    public void Click(PointerEventData eventData, Ray ray, RaycastHit raycastHit) => OnClick?.Invoke(this, eventData, ray, raycastHit);

    public void Clear()
    {
        OnClick = null;
    }

    void Start()
    {
        var proto = GetComponent<Prototype>();
        if(proto != null)
            proto.OnReturnToPool += () => OnClick = null;
    }
}
