using System;
using UnityEditor;
using UnityEngine;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Batchmode probe for the shield-panel Cut 2 landing (docs/shield-panel-cut.md):
    /// ShieldEnvelope collapsing the two implicit ellipsoids (FieldDriver's transform, the Shield
    /// prefab instance's transform) into one reader. Same reason Cut 1 has one (the map's Q7):
    /// Tests.asmdef cannot see Assembly-CSharp, so this batchmode probe stands in for NUnit
    /// coverage.
    ///
    /// Cut 2 does not edit any prefab or scene (Q5: the operator rigs prefabs/scenes by hand), so
    /// this cannot literally load Longinus.prefab/Djinni.prefab and find a ShieldEnvelope on them
    /// yet. Instead it builds synthetic transforms carrying the exact override values the map's
    /// §1 table recorded for Longinus and Djinni (scale, position, rotation) plus the real
    /// Icosphere collider mesh those ships' Shield objects actually use, and runs the map's own
    /// verification rule against those: ProjectToSurface lands exactly on the ellipsoid, and
    /// SurfaceNormal matches a finite-difference gradient of the implicit surface to within 1e-3,
    /// checked on Longinus's 4:1-aspect envelope specifically (a sphere could not distinguish a
    /// correct normal from a plain normalize(p)).
    ///
    /// Run via: Unity -batchmode -projectPath &lt;repo&gt; -executeMethod
    ///          Aetheria.EditorTools.ShieldPanelCut2Verify.Run -quit -logFile &lt;path&gt;
    /// </summary>
    public static class ShieldPanelCut2Verify
    {
        const string Tag = "[ShieldPanelCut2Verify]";

        public static void Run()
        {
            bool ok = true;
            GameObject longinus = null, djinni = null, cubesphere = null;
            try
            {
                ok &= CheckIcosphereExtents();

                longinus = BuildEnvelope(
                    "LonginusShieldProbe",
                    new Vector3(395.3487f, 235.47029f, 990.37805f),
                    new Vector3(0f, 0.73f, 1.32f),
                    Quaternion.identity,
                    "Assets/Models/Icosphere.fbx");
                djinni = BuildEnvelope(
                    "DjinniShieldProbe",
                    new Vector3(857.9317f, 424.00012f, 1036.2562f),
                    new Vector3(0f, 1.75f, -1.06f),
                    new Quaternion(0.08715578f, 0f, 0f, 0.9961947f),
                    "Assets/Models/Icosphere.fbx");
                cubesphere = BuildEnvelope(
                    "FieldShieldTestProbe",
                    new Vector3(4f, 3f, 12f),
                    Vector3.zero,
                    Quaternion.identity,
                    null); // no mesh -- matches the cubesphere's collider extents of (1,1,1) exactly

                ok &= CheckEllipsoidSurface(longinus, "Longinus");
                ok &= CheckEllipsoidSurface(djinni, "Djinni");
                ok &= CheckEllipsoidSurface(cubesphere, "FieldShieldTest cubesphere");

                ok &= CheckNormalMatchesGradient(longinus, "Longinus");
                ok &= CheckNormalMatchesGradient(djinni, "Djinni");

                // The sphere-vs-ellipsoid distinction the map's whole Cut 2 rests on: on
                // Longinus's 4:1 envelope, the true normal and normalize(p) must visibly differ.
                ok &= CheckNormalDiffersFromNormalizeP(longinus, "Longinus");

                // Radii must reflect the measured mesh factor (per-axis; the Icosphere is not a
                // perfect cube of extents -- see IcosphereExtents above), not a guessed flat 0.5.
                ok &= CheckRadiiUsesMeasuredExtents(longinus, new Vector3(395.3487f, 235.47029f, 990.37805f), IcosphereExtents, "Longinus");
                ok &= CheckRadiiUsesMeasuredExtents(cubesphere, new Vector3(4f, 3f, 12f), Vector3.one, "FieldShieldTest cubesphere");
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} exception: {e}");
                ok = false;
            }
            finally
            {
                if (longinus != null) UnityEngine.Object.DestroyImmediate(longinus);
                if (djinni != null) UnityEngine.Object.DestroyImmediate(djinni);
                if (cubesphere != null) UnityEngine.Object.DestroyImmediate(cubesphere);
            }

            Debug.Log(ok ? $"{Tag} PASS" : $"{Tag} FAIL — see errors above");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // Measured once via a throwaway batchmode probe (deleted): the Icosphere collider mesh's
        // bounds.extents at import scale. Only y and z happen to land on exactly 0.01 -- x is
        // slightly smaller (a base icosahedron's vertices are not all equidistant from its own AABB
        // faces on every axis, so its bounding box is not a perfect cube even though the shape is
        // "spherical"). This is exactly why ShieldEnvelope reads the mesh's actual bounds rather
        // than assuming a round number on every axis: the real collider is not perfectly isotropic.
        static readonly Vector3 IcosphereExtents = new Vector3(0.0095106f, 0.01f, 0.01f);

        static bool CheckIcosphereExtents()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Icosphere.fbx");
            if (mesh == null)
            {
                Debug.LogError($"{Tag} Icosphere.fbx mesh not found.");
                return false;
            }
            var e = mesh.bounds.extents;
            bool ok = Vector3.Distance(e, IcosphereExtents) < 1e-4f;
            if (!ok)
                Debug.LogError($"{Tag} Icosphere.fbx bounds.extents is {e:F7}, expected {IcosphereExtents:F7} "
                    + "(measured once via batchmode; if the asset legitimately changed, update this constant "
                    + "and re-derive ShieldEnvelope's documented factor).");
            else
                Debug.Log($"{Tag} Icosphere.fbx bounds.extents={e:F7} (as measured).");
            return ok;
        }

        static GameObject BuildEnvelope(string name, Vector3 scale, Vector3 position, Quaternion rotation, string meshPath)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = scale;
            if (meshPath != null)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
            go.AddComponent<ShieldEnvelope>();
            return go;
        }

        static bool CheckEllipsoidSurface(GameObject envelopeObj, string label)
        {
            var envelope = envelopeObj.GetComponent<ShieldEnvelope>();
            var center = envelope.Center;
            var radii = envelope.Radii;
            bool ok = true;
            var rng = new System.Random(1337);
            for (int i = 0; i < 20; i++)
            {
                var probe = center + new Vector3(
                    (float)(rng.NextDouble() * 4 - 2) * radii.x,
                    (float)(rng.NextDouble() * 4 - 2) * radii.y,
                    (float)(rng.NextDouble() * 4 - 2) * radii.z);
                if (probe == center) probe += Vector3.right * 0.001f;

                var surface = envelope.ProjectToSurface(probe);
                var local = envelopeObj.transform.InverseTransformPoint(surface);
                var extents = LocalExtentsOf(envelopeObj);
                double sum = Sq(local.x / extents.x) + Sq(local.y / extents.y) + Sq(local.z / extents.z);
                if (Math.Abs(sum - 1.0) > 1e-3)
                {
                    Debug.LogError($"{Tag} {label}: ProjectToSurface point {i} is off the ellipsoid, sum(p_i/r_i)^2={sum:F6} (want 1).");
                    ok = false;
                }
            }
            if (ok) Debug.Log($"{Tag} {label}: 20/20 ProjectToSurface probes land on the ellipsoid.");
            return ok;
        }

        static bool CheckNormalMatchesGradient(GameObject envelopeObj, string label)
        {
            var envelope = envelopeObj.GetComponent<ShieldEnvelope>();
            bool ok = true;
            var rng = new System.Random(2026);
            const float h = 1e-3f;
            for (int i = 0; i < 10; i++)
            {
                var dir = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized;
                var probe = envelope.Center + envelopeObj.transform.rotation * Vector3.Scale(dir, envelope.Radii * 3f);
                var surface = envelope.ProjectToSurface(probe);
                var analytic = envelope.SurfaceNormal(surface);

                // Finite-difference gradient of F(x) = sum((local_i)/extents_i)^2 - 1 in world space.
                var extents = LocalExtentsOf(envelopeObj);
                Func<Vector3, double> f = worldP =>
                {
                    var l = envelopeObj.transform.InverseTransformPoint(worldP);
                    return Sq(l.x / extents.x) + Sq(l.y / extents.y) + Sq(l.z / extents.z);
                };
                var grad = new Vector3(
                    (float)((f(surface + Vector3.right * h) - f(surface - Vector3.right * h)) / (2 * h)),
                    (float)((f(surface + Vector3.up * h) - f(surface - Vector3.up * h)) / (2 * h)),
                    (float)((f(surface + Vector3.forward * h) - f(surface - Vector3.forward * h)) / (2 * h)));
                var fd = grad.normalized;

                var dot = Vector3.Dot(analytic, fd);
                if (dot < 1 - 1e-3f)
                {
                    Debug.LogError($"{Tag} {label}: SurfaceNormal probe {i} disagrees with the finite-difference "
                        + $"gradient, dot={dot:F6} (want ~1). analytic={analytic} fd={fd}");
                    ok = false;
                }
            }
            if (ok) Debug.Log($"{Tag} {label}: 10/10 SurfaceNormal probes match the finite-difference gradient within 1e-3.");
            return ok;
        }

        static bool CheckNormalDiffersFromNormalizeP(GameObject envelopeObj, string label)
        {
            var envelope = envelopeObj.GetComponent<ShieldEnvelope>();
            // normalize(p) and the true gradient normal coincide at every point lying exactly on a
            // principal axis (a pole) -- both directions collapse to that axis there, for any radii.
            // They diverge everywhere else, more so the more the axes differ. Longinus's radii are
            // ~3.95/2.35/9.90 (x/y/z), so a direction mixing the short x axis and the long z axis,
            // off every pole, is where the two disagree most.
            var localDir = new Vector3(0.8f, 0f, 0.6f).normalized;
            var probe = envelope.Center + envelopeObj.transform.rotation * Vector3.Scale(localDir, envelope.Radii * 3f);
            var surface = envelope.ProjectToSurface(probe);
            var trueNormal = envelope.SurfaceNormal(surface);
            var wrongNormal = (surface - envelope.Center).normalized;
            var dot = Vector3.Dot(trueNormal, wrongNormal);

            // On this envelope the two are safely coincident only at the poles; off-axis they must
            // differ by more than a token amount, or SurfaceNormal has silently degenerated into
            // normalize(p) again.
            bool ok = dot < 0.999f;
            if (!ok)
                Debug.LogError($"{Tag} {label}: SurfaceNormal equals normalize(p) (dot={dot:F6}) on a 4:1 "
                    + "ellipsoid's flank -- this is the exact wrong-answer mutation the map warns about.");
            else
                Debug.Log($"{Tag} {label}: SurfaceNormal correctly diverges from normalize(p) on the flank (dot={dot:F6}).");
            return ok;
        }

        static bool CheckRadiiUsesMeasuredExtents(GameObject envelopeObj, Vector3 lossyScale, Vector3 extentFactor, string label)
        {
            var envelope = envelopeObj.GetComponent<ShieldEnvelope>();
            var expected = Vector3.Scale(lossyScale, extentFactor);
            var actual = envelope.Radii;
            bool ok = Vector3.Distance(expected, actual) < 1e-2f;
            if (!ok)
                Debug.LogError($"{Tag} {label}: Radii={actual:F6} does not match lossyScale*measuredExtents={expected:F6}. "
                    + $"actualLossyScale={envelopeObj.transform.lossyScale:F6} localScale={envelopeObj.transform.localScale:F6}");
            else
                Debug.Log($"{Tag} {label}: Radii={actual:F6} matches lossyScale*measuredExtents.");
            return ok;
        }

        static Vector3 LocalExtentsOf(GameObject envelopeObj)
        {
            var meshCollider = envelopeObj.GetComponent<MeshCollider>();
            if (meshCollider != null && meshCollider.sharedMesh != null)
                return meshCollider.sharedMesh.bounds.extents;
            return Vector3.one;
        }

        static double Sq(double x) => x * x;
    }
}
