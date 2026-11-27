/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.IO;
using System.Linq;
using GameCult.Caching;

// catalog resave [apply] (aetheria-living-world follow-up aetherdb-catalog-resave): re-saves every record the catalog holds
// under an older schema id through the current schema, so a schema change (an appended field, a restamped id) never needs a
// hand restamp of GameData/Aetheria.cc. The cache loads each record through the game's own types, so a record reads whole
// and an appended field takes its initializer; the write puts it under the current id. A record already under the current
// id is not written, so a second apply writes nothing. Dry run unless apply.
public static class CatalogResaveCommand
{
    public static int Run(CultCache catalog, bool apply, TextWriter output)
    {
        var stale = catalog.AllStoredDocuments.Where(stored => stored.StoredSchemaId != stored.Descriptor.SchemaId).ToArray();
        foreach (var group in stale.GroupBy(stored => stored.Descriptor.SchemaName).OrderBy(group => group.Key, System.StringComparer.Ordinal))
            output.WriteLine($"{group.Key}: {group.Count()} record(s) under an older schema id");
        output.WriteLine($"{stale.Length} record(s) would be re-saved");
        if (!apply || stale.Length == 0) return 0;
        catalog.Commit(batch =>
        {
            foreach (var stored in stale) batch.Upsert(stored.Descriptor.DocumentType, stored.Document, stored.Key);
        });
        catalog.FlushAsync().GetAwaiter().GetResult();
        output.WriteLine($"{stale.Length} record(s) re-saved");
        return 0;
    }
}
