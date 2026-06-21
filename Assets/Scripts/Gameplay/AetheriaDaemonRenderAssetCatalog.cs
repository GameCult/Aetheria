using System;
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

    private static string NormalizeResourceKey(string key)
    {
        const string resourcesPrefix = "resources://";
        if (key.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return key.Substring(resourcesPrefix.Length);
        }

        return key;
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
