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

    [SerializeField]
    private TextureEntry[] textures = Array.Empty<TextureEntry>();

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

        return false;
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

        return false;
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
        {
            return false;
        }

        for (var i = 0; i < textures.Length; i++)
        {
            if (string.Equals(textures[i].Key, key, StringComparison.Ordinal))
            {
                texture = textures[i].Texture;
                return texture != null;
            }
        }

        return false;
    }

    private static string ResolveLocalKey(AetheriaRuntimeAssetRef asset)
    {
        if (asset == null)
            return "";

        return string.IsNullOrWhiteSpace(asset.AssetKey) ? asset.Uri ?? "" : asset.AssetKey;
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

    [Serializable]
    private struct TextureEntry
    {
        public string Key;
        public Texture2D Texture;
    }
}
