/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;

public sealed class AetheriaUnityActionBarBindingAdapter
{
    private readonly AetheriaUnityActionBarPresentation _presentation;

    public AetheriaUnityActionBarBindingAdapter(
        AetheriaUnityActionBarPresentation presentation)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
    }

    public void ApplyBindings(IReadOnlyList<AetheriaUnityActionBarBinding> bindings)
    {
        _presentation.ApplyLocalBindings();
    }
}
