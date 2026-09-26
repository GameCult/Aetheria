using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using Object = UnityEngine.Object;

// A mod package's visual assembly. It does not decide hull cells or hardpoint roles.
public static class ShipModVisual
{
    public sealed class Instance
    {
        public GameObject Root;
        public IReadOnlyDictionary<string, Transform> Anchors;
        public Mesh LineMesh;
        public Material LineMaterial;

        public void Destroy()
        {
            DestroyObject(Root);
            DestroyObject(LineMesh);
            DestroyObject(LineMaterial);
        }
    }

    private sealed class Nodes : GameObjectInstantiator
    {
        public Nodes(IGltfReadable gltf, Transform parent) : base(gltf, parent) { }
        public Transform Get(uint index) => m_Nodes.TryGetValue(index, out var node) ? node.transform : null;
    }

    public static async Task<Instance> LoadAsync(string shipPath, Transform parent)
    {
        var package = ShipModCatalog.ReadPackage(shipPath);
        var ship = package.Ship;

        var root = new GameObject("mod-ship:" + ship.Id);
        root.SetActive(false);
        root.transform.SetParent(parent, false);
        var result = new Instance { Root = root };
        try
        {
            // glTFast's default defer agent creates a DontDestroyOnLoad object, which is
            // unavailable in edit-mode previews. Play mode keeps its frame-budgeted agent.
            var gltf = new GltfImport(deferAgent: Application.isPlaying ? null : new UninterruptedDeferAgent());
            if (!await gltf.LoadFile(package.ModelPath))
                throw new InvalidOperationException($"{ship.Id}: GLB import failed.");
            var nodes = new Nodes(gltf, root.transform);
            if (!await gltf.InstantiateMainSceneAsync(nodes))
                throw new InvalidOperationException($"{ship.Id}: GLB scene instantiation failed.");
            result.Anchors = ship.Anchors.ToDictionary(anchor => anchor.Id, anchor =>
                nodes.Get(package.NodeIndices[anchor.ModelNodeId]) ??
                throw new InvalidOperationException($"{ship.Id}: GLB scene omitted node {anchor.ModelNodeId}."),
                StringComparer.Ordinal);
            result.LineMesh = BuildLineMesh(ship.SchematicLines);
            if (result.LineMesh != null)
            {
                var lines = new GameObject("Schematic Lines");
                lines.transform.SetParent(root.transform, false);
                lines.AddComponent<MeshFilter>().sharedMesh = result.LineMesh;
                var shader = Shader.Find("Sprites/Default") ??
                    throw new InvalidOperationException("Sprites/Default shader is unavailable for ship lines.");
                result.LineMaterial = new Material(shader) { name = "Ship Lines" };
                lines.AddComponent<MeshRenderer>().sharedMaterial = result.LineMaterial;
            }
            root.SetActive(true);
            return result;
        }
        catch
        {
            result.Destroy();
            throw;
        }
    }

    private static void DestroyObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    // Blender Z-up to Unity Y-up through Blender's glTF export and glTFast's handedness conversion.
    // Segment topology preserves every authored point; display width is currently one screen pixel.
    public static Mesh BuildLineMesh(IReadOnlyList<ShipPolyline> lines)
    {
        if (lines == null || lines.Count == 0) return null;
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var segments = new List<int>();
        foreach (var line in lines)
        {
            var count = line.Points.Length / 3;
            var start = vertices.Count;
            for (var i = 0; i < count; i++)
            {
                var offset = i * 3;
                vertices.Add(new Vector3(-line.Points[offset], line.Points[offset + 2], -line.Points[offset + 1]));
                var rgba = line.Color;
                colors.Add(new Color(rgba?[0] ?? 1, rgba?[1] ?? 1, rgba?[2] ?? 1,
                    (rgba?[3] ?? 1) * (line.Opacities?[i] ?? 1)));
                if (i > 0) { segments.Add(start + i - 1); segments.Add(start + i); }
            }
            if (line.Cyclic && count > 2) { segments.Add(start + count - 1); segments.Add(start); }
        }
        var mesh = new Mesh { name = "Captured Ship Lines", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetIndices(segments, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
