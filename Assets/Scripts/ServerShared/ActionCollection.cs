/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;

public abstract class NotAnActionCollection{}

public class ActionCollection<T> : NotAnActionCollection where T : Message
{
    private List<Action<T>> _actions = new List<Action<T>>();

    public void Add(Action<T> action)
    {
        _actions.Add(action);
    }

    public void Invoke(T message)
    {
        foreach (var action in _actions)
        {
            action(message);
        }
    }

    public void Clear()
    {
        _actions.Clear();
    }
}