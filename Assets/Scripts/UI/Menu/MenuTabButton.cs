/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuTabButton : MonoBehaviour
{
    public GameObject TabContents;
    public Button Button;
    public MenuTab Tab;
    public TextMeshProUGUI Text;
    public bool RequireDock;
}
