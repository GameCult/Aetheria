using System;
using System.Collections.Generic;
using GameCult.Caching;
using GameCult.Eve.Surface;

[assembly: CultGeneratedDocumentMetadataProvider(typeof(GameCult.Aetheria.State.Verse.AetheriaRuntimeEveSurfaceDocumentMetadataProvider))]

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeEveSurfaceDocumentMetadataProvider : ICultGeneratedDocumentMetadataProvider
    {
        public IEnumerable<CultGeneratedDocumentDefinition> GetDocumentDefinitions()
        {
            yield return new CultGeneratedDocumentDefinition(
                typeof(EveSurfaceDocument),
                "gamecult.eve.surface",
                "gamecult.eve.surface.v1",
                isGlobal: false,
                nameMember: null,
                nameAccessor: null,
                serializePayload: value => AetheriaRuntimeCultCacheDocumentStore.WriteRuntimeSurfaceDocument(
                    AetheriaRuntimeSurfaceDocuments.FromPortableSurface((EveSurfaceDocument)value)),
                deserializePayload: payload => AetheriaRuntimeSurfaceDocuments.ToPortableSurface(
                    AetheriaRuntimeCultCacheDocumentStore.ReadRuntimeSurfaceDocument(payload)),
                indexAccessors: Array.Empty<CultGeneratedDocumentIndexAccessor>(),
                members: new[]
                {
                    Member("Type", 0, "string"),
                    Member("Schema", 1, "string"),
                    Member("ProviderId", 2, "string"),
                    Member("ProviderKind", 3, "string"),
                    Member("Title", 4, "string"),
                    Member("Version", 5, "int64"),
                    Member("UpdatedAtUtc", 6, "string"),
                    Member("Surface", 7, "object"),
                    Member("Commands", 8, "array")
                });
        }

        private static CultGeneratedDocumentMemberDefinition Member(
            string name,
            int slot,
            string typeName)
        {
            return new CultGeneratedDocumentMemberDefinition(
                name,
                slot,
                typeName,
                isReference: false,
                isMany: false,
                targetSchemaName: null,
                isName: false,
                indexAlias: null);
        }
    }
}
