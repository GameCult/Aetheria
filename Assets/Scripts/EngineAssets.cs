using System;
using System.Collections.Generic;
using GameCult.Caching;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

// Turns a CultCache-stored Addressables key (bare GUID, or "guid[subAssetName]" for a sub-asset;
// see CultAssetGuidKey and docs/addressables-cut.md Ruling S) into a loaded asset. Replaces
// UnityHelpers.LoadAsset, which stripped an "Assets/Resources/" path prefix; there is no path to
// strip any more; the catalog names the asset by its stable .meta GUID and Addressables resolves
// it, in editor play mode and in a built player alike. Owns one Addressables handle per (key,
// type) for the process lifetime; nothing here releases a handle, which is the lifetime
// Resources.Load had. All call sites are synchronous construction paths that Instantiate at once,
// so loading stays synchronous (WaitForCompletion) rather than reordering entity construction.
public static class EngineAssets
{
    private static readonly Dictionary<(string Key, Type Type), Object> Cache = new Dictionary<(string, Type), Object>();

    public static T Load<T>(string key) where T : Object
    {
        if (string.IsNullOrEmpty(key)) return null;

        var cacheKey = (key, typeof(T));
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached as T;

        T asset;
        if (typeof(Component).IsAssignableFrom(typeof(T)))
        {
            // Addressables cannot load a component directly: load the GameObject and take it from there.
            var handle = Addressables.LoadAssetAsync<GameObject>(key);
            var gameObject = handle.WaitForCompletion();
            asset = gameObject == null ? null : gameObject.GetComponent<T>();
        }
        else
        {
            var handle = Addressables.LoadAssetAsync<T>(key);
            asset = handle.WaitForCompletion();
        }

        if (asset == null)
        {
            Debug.LogError($"EngineAssets: failed to load {typeof(T).Name} for Addressables key \"{key}\"");
            return null;
        }

        Cache[cacheKey] = asset;
        return asset;
    }
}
