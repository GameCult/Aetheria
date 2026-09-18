using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using MessagePack;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

// docs/addressables-cut.md §2.5: validates every [CultInspectorAssetGuid] string member stored in
// the catalog. Read-only: it opens the catalog through AetheriaStores.Open exactly like the game
// does, and never writes. No repair path — a failure names the record, member, GUID and path.
public static class EngineAssetCheck
{
    // One row per attributed member (docs/addressables-cut.md §2.5 point 4). A member found on a
    // stored document but missing here fails the check, so a new field can't skip validation.
    private delegate bool Contract(object owner, Type ownerType, Object asset, out string reason);

    private static readonly Dictionary<string, Contract> ConsumerContracts = new Dictionary<string, Contract>
    {
        ["Icon"] = (object owner, Type ownerType, Object asset, out string reason) => TypeContract<Texture2D>(asset, out reason),
        ["ActionBarIcon"] = (object owner, Type ownerType, Object asset, out string reason) => TypeContract<Texture2D>(asset, out reason),
        ["Schematic"] = (object owner, Type ownerType, Object asset, out string reason) => TypeContract<Texture2D>(asset, out reason),
        ["Logo"] = (object owner, Type ownerType, Object asset, out string reason) => TypeContract<Texture2D>(asset, out reason),
        ["Prefab"] = (object owner, Type ownerType, Object asset, out string reason) => ComponentContract<EntityInstance>(asset as GameObject, out reason),
        ["ParticlesPrefab"] = (object owner, Type ownerType, Object asset, out string reason) => ComponentContract<ParticleSystem>(asset as GameObject, out reason),
        ["Particles"] = (object owner, Type ownerType, Object asset, out string reason) => ComponentContract<ParticleSystem>(asset as GameObject, out reason),
        ["EffectPrefab"] = EffectPrefabContract,
    };

    // Menu entry for interactive use; -executeMethod EngineAssetCheck.Run for batchmode/CI, exit 1 on failure.
    [MenuItem("Aetheria/Validate Engine Asset References")]
    public static void Run()
    {
        var failures = RunCheck(out var checkedCount);
        foreach (var failure in failures) Debug.LogError(failure);
        Debug.Log(failures.Count == 0
            ? $"EngineAssetCheck: {checkedCount} stored reference(s) OK."
            : $"EngineAssetCheck: {failures.Count} failure(s) of {checkedCount} checked.");

        if (Application.isBatchMode)
            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    // Returns one failure line per bad stored reference; empty when everything resolves. Exposed for the
    // mutation tests (tests/mutation_tests.py) driving this same method through a batchmode -executeMethod.
    public static List<string> RunCheck(out int checkedCount)
    {
        var failures = new List<string>();
        checkedCount = 0;

        var root = Directory.GetParent(Application.dataPath)!.FullName;
        var catalogPath = Path.Combine(root, "GameData", "Aetheria.cc");
        var cache = AetheriaStores.Open(catalogPath);
        try
        {
            foreach (var stored in cache.AllStoredDocuments.ToArray())
            {
                var docType = stored.Descriptor.DocumentType;
                var document = stored.Document;
                var name = NameOf(document, docType);
                var seen = new HashSet<object>();

                foreach (var (owner, field) in WalkAssetGuidFields(document, seen))
                {
                    var key = (string)field.GetValue(owner);
                    if (string.IsNullOrEmpty(key)) continue;
                    checkedCount++;

                    var label = $"{docType.Name} \"{name}\" .{field.Name} = \"{key}\"";

                    if (!CultAssetGuidKey.TryParse(key, out var guid, out var subAssetName))
                    {
                        failures.Add($"{label}: not a valid Addressables key (guid or guid[subAsset])");
                        continue;
                    }
                    if (guid.Length != 32 || !guid.All(IsLowerHex))
                    {
                        failures.Add($"{label}: GUID \"{guid}\" is not 32 lowercase hex characters");
                        continue;
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !File.Exists(Path.Combine(root, path)))
                    {
                        failures.Add($"{label}: GUID {guid} names no existing file");
                        continue;
                    }

                    var entry = AddressableAssetSettingsDefaultObject.Settings?.FindAssetEntry(guid, true);
                    if (entry == null)
                    {
                        failures.Add($"{label}: {path} ({guid}) is not addressable");
                        continue;
                    }

                    if (!ConsumerContracts.TryGetValue(field.Name, out var contract))
                    {
                        failures.Add($"{label}: no consumer contract registered for member \"{field.Name}\"; add one to EngineAssetCheck.ConsumerContracts");
                        continue;
                    }

                    Object asset = string.IsNullOrEmpty(subAssetName)
                        ? AssetDatabase.LoadAssetAtPath<Object>(path)
                        : AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(a => a.name == subAssetName);
                    if (asset == null)
                    {
                        failures.Add($"{label}: {(string.IsNullOrEmpty(subAssetName) ? path : path + "[" + subAssetName + "]")} did not load");
                        continue;
                    }

                    if (!contract(owner, owner.GetType(), asset, out var reason))
                        failures.Add($"{label}: {reason}");
                }
            }

            return failures;
        }
        finally
        {
            cache.Dispose();
        }
    }

    private static bool TypeContract<T>(Object asset, out string reason) where T : Object
    {
        if (!(asset is T)) { reason = $"asset is a {asset.GetType().Name}, not a {typeof(T).Name}"; return false; }
        reason = null;
        return true;
    }

    private static bool ComponentContract<T>(GameObject gameObject, out string reason) where T : Component
    {
        if (gameObject == null) { reason = "asset is not a GameObject"; return false; }
        if (gameObject.GetComponent<T>() == null) { reason = $"GameObject has no {typeof(T).Name}"; return false; }
        reason = null;
        return true;
    }

    private static bool EffectPrefabContract(object owner, Type ownerType, Object asset, out string reason)
    {
        var gameObject = asset as GameObject;
        if (typeof(InstantWeaponData).IsAssignableFrom(ownerType)) return ComponentContract<InstantWeaponEffectManager>(gameObject, out reason);
        if (typeof(ConstantWeaponData).IsAssignableFrom(ownerType)) return ComponentContract<ConstantWeaponEffectManager>(gameObject, out reason);
        reason = $"unrecognised WeaponData subtype {ownerType.Name} for EffectPrefab";
        return false;
    }

    private static bool IsLowerHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');

    private static string NameOf(object document, Type docType)
    {
        var nameField = docType.GetField("Name", BindingFlags.Public | BindingFlags.Instance);
        return nameField?.GetValue(document) as string ?? "(unnamed)";
    }

    // Same shape as AetherDb's DanglingRefs / the migration's WalkAssetPathFields: plain fields, list
    // elements, and nested [MessagePackObject]/[Union] values (a HullData's Behaviors list, say).
    private static IEnumerable<(object Owner, FieldInfo Field)> WalkAssetGuidFields(object value, HashSet<object> seen)
    {
        if (value == null || !seen.Add(value)) yield break;
        foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(string))
            {
                if (field.GetCustomAttribute<CultInspectorAssetGuidAttribute>() != null)
                    yield return (value, field);
                continue;
            }

            var member = field.GetValue(value);
            if (member == null) continue;

            if (member is IDictionary dictionary)
            {
                foreach (var key in dictionary.Keys)
                {
                    foreach (var nested in WalkAssetGuidFields(key, seen)) yield return nested;
                    foreach (var nested in WalkAssetGuidFields(dictionary[key], seen)) yield return nested;
                }
            }
            else if (member is IList list && !(member is Array { Rank: > 1 }))
            {
                foreach (var element in list)
                    if (element != null && IsSerializedObject(element.GetType()))
                        foreach (var nested in WalkAssetGuidFields(element, seen)) yield return nested;
            }
            else if (IsSerializedObject(member.GetType()))
            {
                foreach (var nested in WalkAssetGuidFields(member, seen)) yield return nested;
            }
        }
    }

    private static bool IsSerializedObject(Type type)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            if (t.IsDefined(typeof(MessagePackObjectAttribute), false) || t.IsDefined(typeof(UnionAttribute), false))
                return true;
        return false;
    }
}
