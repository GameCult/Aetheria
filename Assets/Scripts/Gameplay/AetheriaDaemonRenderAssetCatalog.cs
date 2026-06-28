using System;
using GameCult.Aetheria.State.Verse;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AetheriaDaemonRenderAssetCatalog",
    menuName = "Aetheria/Daemon Render Asset Catalog")]
public sealed class AetheriaDaemonRenderAssetCatalog : ScriptableObject
{
    [SerializeField]
    private MeshEntry[] meshes = Array.Empty<MeshEntry>();

    [SerializeField]
    private MaterialEntry[] materials = Array.Empty<MaterialEntry>();

    public bool TryResolveMesh(string key, out Mesh mesh)
    {
        mesh = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        for (var i = 0; i < meshes.Length; i++)
        {
            if (string.Equals(meshes[i].Key, key, StringComparison.Ordinal))
            {
                mesh = meshes[i].Mesh;
                return mesh != null;
            }
        }

        mesh = Resources.Load<Mesh>(NormalizeResourceKey(key));
        return mesh != null;
    }

    public bool TryResolveMesh(AetheriaRuntimeAssetRef asset, out Mesh mesh)
    {
        return TryResolveMesh(ResolveLocalKey(asset), out mesh);
    }

    public bool TryResolveMaterial(string key, out Material material)
    {
        material = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        for (var i = 0; i < materials.Length; i++)
        {
            if (string.Equals(materials[i].Key, key, StringComparison.Ordinal))
            {
                material = materials[i].Material;
                return material != null;
            }
        }

        material = Resources.Load<Material>(NormalizeResourceKey(key));
        return material != null;
    }

    public bool TryResolveMaterial(AetheriaRuntimeAssetRef asset, out Material material)
    {
        return TryResolveMaterial(ResolveLocalKey(asset), out material);
    }

    public bool TryResolveTexture(AetheriaRuntimeAssetRef asset, out Texture2D texture)
    {
        texture = null;
        var key = ResolveLocalKey(asset);
        if (string.IsNullOrWhiteSpace(key))
            return false;

        texture = Resources.Load<Texture2D>(NormalizeResourceKey(key));
        return texture != null;
    }

    private static string NormalizeResourceKey(string key)
    {
        const string resourcesPrefix = "resources://";
        if (key.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return key.Substring(resourcesPrefix.Length);
        }

        return key;
    }

    private static string ResolveLocalKey(AetheriaRuntimeAssetRef asset)
    {
        if (asset == null)
            return "";

        if (string.Equals(asset.Transport, AetheriaRuntimeAssetTransports.Resources, StringComparison.OrdinalIgnoreCase))
            return asset.Uri;

        return string.IsNullOrWhiteSpace(asset.Uri) ? asset.AssetKey : asset.Uri;
    }

    [Serializable]
    private struct MeshEntry
    {
        public string Key;
        public Mesh Mesh;
    }

    [Serializable]
    private struct MaterialEntry
    {
        public string Key;
        public Material Material;
    }
}
