using System;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;

// Aetheria's stores: one home store per type, composed here and nowhere else.
public static class AetheriaStores
{
    public static readonly Type[] CatalogTypes = { typeof(ItemData), typeof(Faction), typeof(FactionProductData), typeof(PersonalityAttribute), typeof(NameFile), typeof(InputLayout), typeof(Loadout) };
    public static readonly Type[] RunTypes = { typeof(OrbitData), typeof(BodyData), typeof(SavedZone), typeof(SavedGame), typeof(ProvenanceLedger) };
    public static readonly Type[] PlayerTypes = { typeof(PlayerSettings) };

    // Attaches (hydrates) the catalog read-only unless catalogWritable, then the run and player stores when given.
    // Throws when a [CultGlobal] type routed to the catalog has no record. The one exemption is a writable catalog with
    // no records at all, a seed store being authored from nothing; a populated catalog is held to it however it opens.
    public static CultCache Open(string catalogPath, string runPath = null, string playerPath = null, bool catalogWritable = false)
    {
        var cache = new CultCache();
        try
        {
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalogPath, !catalogWritable), CatalogTypes);
            if (runPath != null) cache.AddBackingStore(new SingleFileMessagePackBackingStore(runPath), RunTypes);
            if (playerPath != null) cache.AddBackingStore(new SingleFileMessagePackBackingStore(playerPath), PlayerTypes);
            var seeding = catalogWritable && !cache.AllStoredDocuments.Any(stored =>
                CatalogTypes.Any(home => home.IsAssignableFrom(stored.Descriptor.DocumentType)));
            var missing = seeding ? null : cache.Registry.AllDescriptors.FirstOrDefault(descriptor =>
                descriptor.IsGlobal &&
                CatalogTypes.Any(home => home.IsAssignableFrom(descriptor.DocumentType)) &&
                cache.AllStoredDocuments.All(stored => stored.Descriptor != descriptor));
            if (missing != null)
                throw new InvalidOperationException($"Catalog {catalogPath} has no {missing.SchemaName} record; catalog globals are authored, never invented.");
            // R-heat (docs/stats-and-power-cut.md): every equippable design's heat response must describe a
            // coherent range before anything reads it. Fails loudly, naming the item.
            foreach (var data in cache.GetAll<EquippableItemData>())
            {
                StatValidation.ValidateHeatResponse(data);
                StatValidation.ValidateStatModifiers(data.Name, data.Behaviors);
                // Cut 6 (docs/stats-and-power-cut.md): a power request may not depend on power supply.
                StatValidation.ValidateNoPowerSupplyOnRequest(data.Name, data.Behaviors);
                // Cut 7 (docs/stats-and-power-cut.md): a stat naming a role its design lacks is refused.
                StatValidation.ValidateRoleUsage(data.Name, data.Roles, data.Behaviors);
            }
            // Cut 2 (docs/stats-and-power-cut.md): a consumable's own StatModifierData needs the same check;
            // consumables carry no heat response, so this is not folded into the loop above.
            foreach (var data in cache.GetAll<ConsumableItemData>())
            {
                StatValidation.ValidateStatModifiers(data.Name, data.Behaviors);
                StatValidation.ValidateNoPowerSupplyOnRequest(data.Name, data.Behaviors);
                StatValidation.ValidateRoleUsage(data.Name, data.Roles, data.Behaviors);
            }
            return cache;
        }
        catch
        {
            cache.Dispose();
            throw;
        }
    }
}

public static class CultRecordRefs
{
    public static bool IsSet(this CultRecordKey key) => !string.IsNullOrEmpty(key.Value);
    public static bool IsSet<T>(this CultRecordRef<T> reference) => reference.Key.IsSet();

    // The document a reference names, or null when it is unset or names nothing this cache holds.
    public static T Get<T>(this CultCache cache, CultRecordRef<T> reference) where T : class =>
        reference.IsSet() ? cache.Get<T>(reference.Key) : null;

    // A reference to a document this cache holds. The key comes from the cache, never from the document.
    public static CultRecordRef<T> RefOf<T>(this CultCache cache, T document) where T : class =>
        new CultRecordRef<T>(cache.TryGetHandle(document)?.Key ??
                             throw new InvalidOperationException($"This cache holds no such {document.GetType().Name}."));

    // F7 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): the validation body itself, pulled out of Upsert
    // below so it has exactly one owner. Every catalog write in this repository was supposed to go through
    // Upsert, but three of tools/AetherDb/Program.cs's own migration commands (Dangling, ShieldMigrate,
    // BrownoutMigrate) cannot: they repair an EXISTING record in place and must preserve its key, which Upsert's
    // own cache.UpsertAsync(document) does not support (Upsert mints or resolves a key from the document itself,
    // not from a caller-supplied one). Rather than leave those three writing raw and unvalidated -- as they did
    // before this fix -- they call this method directly, immediately before their own identity-preserving
    // batch.Upsert(document.GetType(), document, key) commit, so the same rules run either way. Takes `object`
    // (not a generic T) because that is what those three tools' own (Document, Key) change lists are typed as.
    public static void Validate(object document)
    {
        if (document is EquippableItemData data)
        {
            StatValidation.ValidateHeatResponse(data);
            StatValidation.ValidateStatModifiers(data.Name, data.Behaviors);
            StatValidation.ValidateNoPowerSupplyOnRequest(data.Name, data.Behaviors);
            StatValidation.ValidateRoleUsage(data.Name, data.Roles, data.Behaviors);
        }
        if (document is ConsumableItemData consumable)
        {
            StatValidation.ValidateStatModifiers(consumable.Name, consumable.Behaviors);
            StatValidation.ValidateNoPowerSupplyOnRequest(consumable.Name, consumable.Behaviors);
            StatValidation.ValidateRoleUsage(consumable.Name, consumable.Roles, consumable.Behaviors);
        }
    }

    // Writes a document to its home store; the cache mints its key the first time. Every catalog write in this
    // repository (tools, tests, migration scripts) goes through this one function, or through Validate above
    // directly when the caller must preserve an existing key (see Validate's own comment) -- so this is where
    // R-heat's validation closes the gap AetheriaStores.Open leaves: Open only checks what is already on disk
    // when a session starts, not what a session then writes. Validating here means an invalid heat response is
    // refused before the write ever reaches disk, not just the next time someone reopens the file.
    public static CultRecordRef<T> Upsert<T>(this CultCache cache, T document) where T : class
    {
        Validate(document);
        return new CultRecordRef<T>(cache.UpsertAsync(document).Result.Key);
    }
}
