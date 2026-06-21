/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using cfloat3 = CultMath.float3;

public interface INamedEntry
{
    string EntryName { get; set; }
}

public class AetheriaRuntimeItemReference
{
    public string ItemKey;

    public AetheriaRuntimeItemReference()
    {
    }

    public AetheriaRuntimeItemReference(string itemKey)
    {
        ItemKey = itemKey;
    }

}

public interface ITintInspector
{
    cfloat3 TintColor { get; }
}
