using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The radius of each rock mesh ZoneRenderer draws (ruling rock-meshes-by-size): the farthest vertex from the mesh origin, which bounds the
// mesh's top under any rotation. DrawAhead.RockScale divides the gameplay radius by it, so the drawn rock is the gameplay rock. It is stored on
// InstancedMesh.Radius in ARPG.unity, because a build cannot read vertices of a mesh without Read/Write import.
//
//   Unity -batchmode -nographics -projectPath <project> -executeMethod Aetheria.EditorTools.RockMeshRadius.Fill   -logFile <log>
//   Unity -batchmode -nographics -projectPath <project> -executeMethod Aetheria.EditorTools.RockMeshRadius.Verify -logFile <log>
//
// Fill measures every AsteroidMeshes entry and saves the scene; Verify fails (exit 1) when any stored radius is not the measured one.
namespace Aetheria.EditorTools
{
    public static class RockMeshRadius
    {
        private const string Scene = "Assets/Scenes/ARPG.unity";
        private const float Tolerance = 1e-4f;

        public static float Measure(Mesh mesh) => Mathf.Sqrt(mesh.vertices.Max(v => v.sqrMagnitude));

        public static void Fill() => Run(fill: true);

        public static void Verify() => Run(fill: false);

        private static void Run(bool fill)
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                var renderer = UnityEngine.Object.FindObjectsByType<ZoneRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(r => r.gameObject.scene == scene);
                var meshes = renderer.AsteroidMeshes;
                if (meshes == null || meshes.Length == 0) throw new InvalidOperationException("ARPG.unity's ZoneRenderer has no AsteroidMeshes.");
                var bad = meshes.Where(m => m.Mesh == null).ToArray();
                if (bad.Length > 0) throw new InvalidOperationException($"{bad.Length} AsteroidMeshes entries have no mesh.");
                var measured = meshes.Select(m => Measure(m.Mesh)).ToArray();
                var failures = meshes.Select((m, i) => (m, i)).Where(p => !(measured[p.i] > 0f) || Mathf.Abs(p.m.Radius - measured[p.i]) > Tolerance).ToArray();
                if (fill)
                {
                    for (var i = 0; i < meshes.Length; i++) meshes[i].Radius = measured[i];
                    EditorUtility.SetDirty(renderer);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The scene did not save.");
                }
                else if (failures.Length > 0)
                    throw new InvalidOperationException("Stored radius differs from measured: " + string.Join(", ",
                        failures.Select(f => $"{f.m.Mesh.name} stored {f.m.Radius} measured {measured[f.i]}")));
                var sorted = measured.OrderBy(r => r).ToArray();
                Debug.Log($"[RockMeshRadius] {(fill ? "filled" : "ok")}: {sorted.Length} meshes, radius min {sorted[0]:F3} median {sorted[sorted.Length / 2]:F3} max {sorted[^1]:F3}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
