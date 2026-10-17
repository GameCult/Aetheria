using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
// RockMeshRadiusBuildGate runs the same check before every player build and fails the build, so a mesh with no filled radius cannot ship;
// Gate runs that gate in batch mode.
//   Unity -batchmode -nographics -projectPath <project> -executeMethod Aetheria.EditorTools.RockMeshRadius.Gate -logFile <log>
namespace Aetheria.EditorTools
{
    public static class RockMeshRadius
    {
        private const string Scene = "Assets/Scenes/ARPG.unity";
        private const float Tolerance = 1e-4f;

        public static float Measure(Mesh mesh) => Mathf.Sqrt(mesh.vertices.Max(v => v.sqrMagnitude));

        public static void Fill() => Batch(() =>
        {
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var (renderer, meshes, measured) = Load(scene);
            for (var i = 0; i < meshes.Length; i++) meshes[i].Radius = measured[i];
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The scene did not save.");
            return Summary("filled", measured);
        });

        public static void Verify() => Batch(() => Check(OpenSceneMode.Single));

        // The build gate's own entry in batch mode: exit 1 exactly when a build would fail (RockMeshRadiusBuildGate).
        public static void Gate() => Batch(() =>
        {
            try { new RockMeshRadiusBuildGate().OnPreprocessBuild(null); }
            catch (BuildFailedException failure) { throw new InvalidOperationException(failure.Message); }
            return "the build gate passes";
        });

        // Throws unless every AsteroidMeshes entry of ARPG.unity has a stored radius equal to its mesh's measured one (a zero radius never does).
        public static string Check(OpenSceneMode mode)
        {
            var scene = EditorSceneManager.OpenScene(Scene, mode);
            try
            {
                var (_, meshes, measured) = Load(scene);
                var failures = meshes.Select((m, i) => (m, i)).Where(p => !(measured[p.i] > 0f) || Mathf.Abs(p.m.Radius - measured[p.i]) > Tolerance).ToArray();
                if (failures.Length > 0)
                    throw new InvalidOperationException("InstancedMesh.Radius differs from the measured one, or is unset, on mesh (run RockMeshRadius.Fill): " + string.Join(", ",
                        failures.Select(f => f.m.Mesh.name)));
                return Summary("ok", measured);
            }
            finally
            {
                if (mode == OpenSceneMode.Additive) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static (ZoneRenderer renderer, InstancedMesh[] meshes, float[] measured) Load(UnityEngine.SceneManagement.Scene scene)
        {
            var renderer = UnityEngine.Object.FindObjectsByType<ZoneRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(r => r.gameObject.scene == scene);
            var meshes = renderer.AsteroidMeshes;
            if (meshes == null || meshes.Length == 0) throw new InvalidOperationException("ARPG.unity's ZoneRenderer has no AsteroidMeshes.");
            var bad = meshes.Where(m => m.Mesh == null).ToArray();
            if (bad.Length > 0) throw new InvalidOperationException($"{bad.Length} AsteroidMeshes entries have no mesh.");
            return (renderer, meshes, meshes.Select(m => Measure(m.Mesh)).ToArray());
        }

        private static string Summary(string what, float[] measured)
        {
            var sorted = measured.OrderBy(r => r).ToArray();
            return $"{what}: {sorted.Length} meshes, radius min {sorted[0]:F3} median {sorted[sorted.Length / 2]:F3} max {sorted[^1]:F3}";
        }

        private static void Batch(Func<string> run)
        {
            try
            {
                Debug.Log($"[RockMeshRadius] {run()}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }

    // A mesh in AsteroidMeshes with no filled radius must not ship: the build fails, naming the field.
    public class RockMeshRadiusBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            try { RockMeshRadius.Check(OpenSceneMode.Additive); }
            catch (InvalidOperationException failure) { throw new BuildFailedException(failure.Message); }
        }
    }
}
