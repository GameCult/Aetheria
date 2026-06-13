/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Unity.Mathematics;

public interface INamedEntry
{
    string EntryName { get; set; }
}

public abstract class DatabaseEntry
{
    public Guid ID = Guid.NewGuid();

    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is DatabaseEntry entry) return entry.ID == ID;
        return false;
    }
}

public class DatabaseLink<T> : DatabaseLinkBase where T : ItemData
{
    public T Value => ResolveRuntimeItemCatalog<T>(LinkID);
}

public class DatabaseLinkBase
{
    public Guid LinkID;

    private static IRuntimeItemCatalogReader Catalog { get; set; }

    protected static T ResolveRuntimeItemCatalog<T>(Guid linkId) where T : ItemData
    {
        if (Catalog == null)
            throw new InvalidOperationException("Runtime item catalog resolution is not bound. Open typed Aetheria runtime state before reading item object graphs.");

        return Catalog.Get<T>(linkId);
    }

    public static void BindRuntimeItemCatalog(IRuntimeItemCatalogReader catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        Catalog = catalog;
    }
}

public interface ITintInspector
{
    float3 TintColor { get; }
}
