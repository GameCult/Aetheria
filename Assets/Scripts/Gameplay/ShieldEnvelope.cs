using UnityEngine;

// Cut 2 of docs/shield-panel-cut.md (R4/R6): the one owner of "where is the shield surface, and
// which way does it face". Before this component existed, that answer was computed twice and
// independently -- FieldDriver.cs derived it from its own transform.localScale, and
// ShieldManager.cs derived it from the Shield prefab instance's transform -- with no shared
// authority and no way for the two to notice if they disagreed. This collapses both into one
// reader of a single transform. It stores nothing; every value below is derived fresh.
public class ShieldEnvelope : MonoBehaviour
{
    // R6: the envelope is derived from the existing transform, not authored as separate centre +
    // radii fields, because the transform already drives the physical collider (Shield.prefab's
    // MeshCollider / FieldShieldTest's cubesphere MeshCollider) -- authoring a second copy next to
    // it would be exactly the kind of duplicate authority this cut exists to remove.
    //
    // The one number a bare transform can't supply is the conversion from "lossyScale" to
    // "ellipsoid semi-axis length in world units", because that depends on how big the attached
    // collider mesh already is in local space. The map's draft guessed a flat 0.5 (unit-diameter
    // sphere); measuring the two meshes actually in the tree (batchmode probe, both meshes'
    // MeshCollider.sharedMesh.bounds) found neither is 0.5:
    //   - Shield.prefab's Icosphere collider: bounds.extents = (0.0095106, 0.01, 0.01) -- not a
    //     perfect cube, because a base icosahedron's vertices are not all equidistant from its own
    //     axis-aligned bounding box on every axis even though the shape is "spherical". Shield.prefab's
    //     base localScale of 100 is there to cancel the round number (100 * 0.01 = radius 1 on y/z),
    //     which is why the ship overrides (e.g. Longinus ~395,235,990) read directly as ~100x the
    //     documented "effective radii" in docs/shield-panel-cut.md's table without this factor, and
    //     why that factor must be read per-axis from the mesh rather than assumed uniform.
    //   - The FieldShieldTest cubesphere's collider: bounds.extents = (1, 1, 1) exactly, so its
    //     lossyScale (4, 3, 12) already *is* the radii with no correction -- which is why
    //     FieldDriver's pre-Cut-2 code could get away with using transform.localScale directly.
    // Reading the mesh instead of hardcoding either number is what makes one component correct for
    // both authorities without either one special-casing the other.
    Vector3 _localExtents = Vector3.one;
    bool _readExtents;

    void Awake() => ReadLocalExtents();

    void ReadLocalExtents()
    {
        if (_readExtents) return;
        _readExtents = true;

        Mesh mesh = null;
        var meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null) mesh = meshCollider.sharedMesh;
        if (mesh == null)
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null) mesh = meshFilter.sharedMesh;
        }

        // No mesh at all: nothing to correct for, so the transform's own scale is already the
        // radii (extents of 1 is a no-op multiplier). This is the compatibility case, not the
        // expected one -- every carrier in the tree today (Shield.prefab, the cubesphere) has one.
        _localExtents = mesh != null ? mesh.bounds.extents : Vector3.one;
    }

    /// <summary>World-space centre of the ellipsoid.</summary>
    public Vector3 Center => transform.position;

    /// <summary>World-space semi-axis lengths of the ellipsoid, accounting for the attached
    /// collider mesh's own local-space size (see the field comment above).</summary>
    public Vector3 Radii
    {
        get
        {
            if (!_readExtents) ReadLocalExtents(); // covers edit-mode / pre-Awake callers
            var s = transform.lossyScale;
            return new Vector3(s.x * _localExtents.x, s.y * _localExtents.y, s.z * _localExtents.z);
        }
    }

    // Local-space unit direction from the envelope's centre toward worldPoint, accounting for the
    // transform's rotation and (non-uniform) scale. This is the one primitive ProjectToSurface,
    // SurfaceDirection, and both static fallback entry points below are built from, and the only
    // place InverseTransformPoint may appear for shield-surface purposes (see the negative check
    // in docs/shield-panel-cut.md Cut 2). Static and keyed on an explicit Transform so a caller
    // with no ShieldEnvelope component can still go through this one owner instead of re-deriving
    // the maths itself.
    static Vector3 LocalUnitDirection(Transform t, Vector3 worldPoint)
    {
        var local = t.InverseTransformPoint(worldPoint);
        return local.sqrMagnitude > 1e-12f ? local.normalized : Vector3.forward;
    }

    Vector3 LocalUnitDirection(Vector3 worldPoint) => LocalUnitDirection(transform, worldPoint);

    /// <summary>
    /// Projects a world-space point outward onto the ellipsoid surface, returning a world-space
    /// point. This is the true surface projection: local direction, scale by the local-space
    /// extents, transform back through the object's rotation and scale.
    /// </summary>
    public Vector3 ProjectToSurface(Vector3 worldPoint)
    {
        if (!_readExtents) ReadLocalExtents();
        var dir = LocalUnitDirection(worldPoint);
        var localSurface = Vector3.Scale(dir, _localExtents);
        return transform.TransformPoint(localSurface);
    }

    /// <summary>
    /// The true outward normal of the ellipsoid at a world-space point already on (or near) its
    /// surface: normalize(p.x/a^2, p.y/b^2, p.z/c^2) in local space, then rotated back to world.
    /// This is deliberately NOT normalize(p) -- normalize(p) is only the correct normal for a
    /// sphere. On a non-uniform ellipsoid (Longinus is ~4:1) it is visibly wrong: it points along
    /// the radius vector rather than perpendicular to the surface, which reads as the panel tilting
    /// toward the ship's centre instead of lying flat against the hull (docs/shield-panel-cut.md
    /// Cut 2 and the Cut 4 flank-click check). FieldDriver and ShieldManager both get away with
    /// normalize(p) today only because they use it to pick a *direction*, not a *normal* -- see
    /// SurfaceDirection below, which is what they keep using.
    /// </summary>
    public Vector3 SurfaceNormal(Vector3 worldSurfacePoint)
    {
        if (!_readExtents) ReadLocalExtents();
        var local = transform.InverseTransformPoint(worldSurfacePoint);
        var a = Mathf.Max(_localExtents.x, 1e-6f);
        var b = Mathf.Max(_localExtents.y, 1e-6f);
        var c = Mathf.Max(_localExtents.z, 1e-6f);
        var gradient = new Vector3(local.x / (a * a), local.y / (b * b), local.z / (c * c));
        // A normal is a covector: under a non-uniform scale it transforms by the INVERSE of the
        // scale, then the rotation -- the opposite of how an ordinary vector (or TransformDirection,
        // which multiplies by scale) transforms. Dividing by lossyScale here and rotating with the
        // bare quaternion (no TransformDirection) is what makes this differ from normalize(p) on a
        // non-uniform ellipsoid; using TransformDirection directly was tried and measurably wrong
        // (see the batchmode probe in docs/shield-panel-cut.md Cut 2 -- it disagreed with a
        // finite-difference gradient by 5-20%, not floating-point noise).
        var s = transform.lossyScale;
        var gradientOverScale = new Vector3(
            s.x != 0 ? gradient.x / s.x : gradient.x,
            s.y != 0 ? gradient.y / s.y : gradient.y,
            s.z != 0 ? gradient.z / s.z : gradient.z);
        var worldGradient = transform.rotation * gradientOverScale;
        return worldGradient.sqrMagnitude > 1e-12f ? worldGradient.normalized : transform.forward;
    }

    /// <summary>
    /// The local-frame unit direction from the envelope's centre toward worldPoint -- exactly what
    /// FieldDriver.AddHit and ShieldManager.ShowHit computed by hand before this cut, kept as its
    /// own named method (rather than inlining InverseTransformPoint at each call site) so the
    /// negative check ("no shield-surface projection outside ShieldEnvelope.cs") stays true. This
    /// is a *direction*, not the true surface normal -- see the SurfaceNormal doc comment.
    /// </summary>
    public Vector3 SurfaceDirection(Vector3 worldPoint) => LocalUnitDirection(transform, worldPoint);

    // --- Fallback entry points for a carrier with no ShieldEnvelope component yet. ---
    //
    // Not every carrier gets the component in the same commit as this file (Q5: the operator rigs
    // prefabs and scenes by hand, on their own schedule), so both FieldDriver and ShieldManager
    // must keep working, unchanged, until their own object is rigged. "Unchanged" means byte-for-
    // byte the pre-Cut-2 formula, not a plausible-looking replacement -- a silently different
    // fallback is worse than either the old code or a hard failure. Rather than let each consumer
    // re-derive that formula by hand (recreating the exact duplicate-authority problem this cut
    // exists to remove), both fallback formulas are named, static, one-owner methods here, and
    // ShieldPanelCut2Verify.CheckLegacyFallbackFormulas pins each one against an independent
    // re-derivation of the original code.

    /// <summary>
    /// Compatibility path for a Transform with no ShieldEnvelope component: the exact direction
    /// ShieldManager.ShowHit computed by hand before this cut (normalize(InverseTransformPoint),
    /// no scale or mesh-extents factor at all -- a direction never needed one).
    /// </summary>
    public static Vector3 SurfaceDirection(Transform transform, Vector3 worldPoint)
        => LocalUnitDirection(transform, worldPoint);

    /// <summary>
    /// Compatibility path for a Transform with no ShieldEnvelope component: the exact local-frame
    /// value FieldDriver.AddHit computed by hand before this cut -- normalize(local) scaled by the
    /// transform's own localScale, deliberately NOT transformed back through rotation/translation
    /// (FieldDriver's shader consumes this in local mesh space, not world space; see the
    /// ProjectToSurface-vs-this note in FieldDriver.cs). This is a legacy, local-space formula,
    /// not a general "surface point in the world" API -- ProjectToSurface is that, and requires a
    /// real ShieldEnvelope (it needs the mesh's local extents, which no mesh means no envelope to
    /// read them from).
    /// </summary>
    public static Vector3 LegacyLocalSurfacePoint(Transform transform, Vector3 worldPoint)
        => Vector3.Scale(LocalUnitDirection(transform, worldPoint), transform.localScale);
}
