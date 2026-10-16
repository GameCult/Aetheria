using System.Collections.Generic;
using CultMath;
using CultMath.UnityBridge;
using GameCult.Caching;
using UnityEngine;
using UnityEngine.Rendering;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using float4 = CultMath.float4;
using quaternion = CultMath.quaternion;

// The one owner of map-space projection (ruling map-icons-placed-in-2d): where every map icon and belt asteroid is
// drawn, its yaw, its size, its fade, and the contour's depth range. Icons are not world objects: each is a mesh,
// a material instance and an authored rotation read once from the icon child at registration (the child is then
// destroyed), drawn every frame on the Minimap layer at the map plane from sim state through DrawAhead. Roll and
// pitch never reach an icon, and field depth never decides whether one is seen. Both map cameras (the minimap's
// and the map screen's) see these same draws; only the size input differs.
public class MapPresenter
{
    // The map plane. Every map camera is orthographic and looks down from above it, so this height is inside every
    // clip range; the offsets only order overlapping icons (planets under stations under ships, belts under all).
    public const float PlaneY = 0f;
    public const float BeltOffset = -.1f, PlanetOffset = 0f, OrbitalOffset = .1f, ShipOffset = .2f;

    // The one place the heading of an icon is decided (question contact-icon-heading, yaw-for-all): every icon turns
    // with its entity's yaw. Another answer changes only this function.
    public static float IconYaw(Entity entity, quaternion drawnRotation)
    {
        var forward = drawnRotation.ToUnity() * Vector3.forward;
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    private sealed class Icon
    {
        public Mesh Mesh;
        public Material Material;
        public Color Base;
        public Quaternion Authored;
        public float Offset;
        public Entity Entity;
        public CultRecordKey Planet;
        public float Fade;
        public Matrix4x4 Drawn;
    }

    private readonly List<Icon> _icons = new List<Icon>();
    private readonly Dictionary<Entity, Icon> _entityIcons = new Dictionary<Entity, Icon>();
    private readonly Material[] _gravityMaterials;
    private readonly int _layer = LayerMask.NameToLayer("Minimap");
    private float _minimapScale = 1, _mapScreenScale;
    private bool _mapScreenOpen;
    private readonly Material _asteroidMaterial;
    private readonly Vector4 _spriteSheet;
    private readonly float _asteroidSize;

    public MapPresenter(Material[] gravityMaterials, Material asteroidMaterial, int spriteColumns, int spriteRows,
        float asteroidSize)
    {
        _gravityMaterials = gravityMaterials;
        _asteroidMaterial = asteroidMaterial;
        _spriteSheet = new Vector4(spriteColumns, spriteRows, 0, 0);
        _asteroidSize = asteroidSize;
    }

    // The icon size: the map screen's scale while it is open, else the minimap's zoom.
    public float IconSize => _mapScreenOpen ? _mapScreenScale : _minimapScale;
    public void SetMinimapScale(float scale) => _minimapScale = scale;
    public void OpenMapScreen(bool open) => _mapScreenOpen = open;
    public void SetMapScreenScale(float scale) => _mapScreenScale = scale;

    // Destroy in play mode, DestroyImmediate where the editor smokes run without one.
    private static void Discard(Object doomed)
    {
        if (Application.isPlaying) Object.Destroy(doomed);
        else Object.DestroyImmediate(doomed);
    }

    public int IconCount => _icons.Count;
    // What the last Draw issued, for the smoke.
    public int IconDraws { get; private set; }
    public int BeltDraws { get; private set; }
    public int LastBeltInstances { get; private set; }
    public bool TryDrawn(CultRecordKey planet, out Matrix4x4 drawn)
    {
        drawn = default;
        var icon = _icons.Find(candidate => candidate.Entity == null && candidate.Planet.Equals(planet));
        if (icon == null) return false;
        drawn = icon.Drawn;
        return true;
    }

    // What a belt's buffer holds on the GPU side, read back.
    public void ReadBelt(CultRecordKey key, float4[] into) => _belts[key].Buffer.GetData(into);

    public bool TryDrawn(Entity entity, out Matrix4x4 drawn, out float alpha)
    {
        drawn = default;
        alpha = 0;
        if (!_entityIcons.TryGetValue(entity, out var icon)) return false;
        drawn = icon.Drawn;
        alpha = icon.Material.GetColor("_Color").a;
        return true;
    }

    // Reads the icon child once (mesh, material, rotation relative to its root) and destroys it: from here on no
    // world transform carries the icon.
    private Icon Take(MeshRenderer renderer, Transform root, float offset)
    {
        var icon = new Icon
        {
            Mesh = renderer.GetComponent<MeshFilter>().sharedMesh,
            Material = Object.Instantiate(renderer.sharedMaterial),
            Authored = Quaternion.Inverse(root.rotation) * renderer.transform.rotation,
            Offset = offset,
            Fade = 1,
        };
        icon.Base = icon.Material.HasProperty("_Color") ? icon.Material.GetColor("_Color") : Color.white;
        Discard(renderer.gameObject);
        _icons.Add(icon);
        return icon;
    }

    public void RegisterEntity(Entity entity, MeshRenderer renderer, Transform root)
    {
        var icon = Take(renderer, root, entity is Ship ? ShipOffset : OrbitalOffset);
        icon.Entity = entity;
        icon.Fade = 0;
        _entityIcons[entity] = icon;
    }

    public void RegisterPlanet(CultRecordKey key, MeshRenderer renderer, Transform root) =>
        Take(renderer, root, PlanetOffset).Planet = key;

    // The entity's fade, applied to its icon the same frame the hull's materials take it.
    public void SetFade(Entity entity, float fade)
    {
        if (_entityIcons.TryGetValue(entity, out var icon)) icon.Fade = fade;
    }

    public void Unregister(Entity entity)
    {
        if (!_entityIcons.Remove(entity, out var icon)) return;
        _icons.Remove(icon);
        Discard(icon.Material);
    }

    private sealed class Belt
    {
        public float4[] Source;
        public ComputeBuffer Buffer;
        public MaterialPropertyBlock Block = new MaterialPropertyBlock();
    }

    private readonly Dictionary<CultRecordKey, Belt> _belts = new Dictionary<CultRecordKey, Belt>();
    private static readonly int BeltProperty = Shader.PropertyToID("_Belt");
    private static readonly int SheetProperty = Shader.PropertyToID("_Sheet");
    private static readonly int PlaneProperty = Shader.PropertyToID("_PlaneY");
    private static readonly int SizeProperty = Shader.PropertyToID("_AsteroidSize");

    // A belt's map sprites: the 3D belt's per-frame float4s (x, y planar, z spin, w size), uploaded as they are.
    public void RegisterBelt(CultRecordKey key, float4[] source) =>
        _belts[key] = new Belt { Source = source, Buffer = new ComputeBuffer(source.Length, 16) };

    // One instanced draw per belt: six vertices per asteroid, expanded to a quad on the map plane by the shader.
    private void DrawBelt(Belt belt)
    {
        belt.Buffer.SetData(belt.Source);
        belt.Block.SetBuffer(BeltProperty, belt.Buffer);
        belt.Block.SetVector(SheetProperty, _spriteSheet);
        belt.Block.SetFloat(PlaneProperty, PlaneY + BeltOffset);
        belt.Block.SetFloat(SizeProperty, _asteroidSize);
        var parameters = new RenderParams(_asteroidMaterial)
        {
            layer = _layer, matProps = belt.Block, shadowCastingMode = ShadowCastingMode.Off,
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e6f),
        };
        Graphics.RenderPrimitives(parameters, MeshTopology.Triangles, 6, belt.Source.Length);
        BeltDraws++;
        LastBeltInstances = belt.Source.Length;
    }

    // Every registered icon and belt for this frame, from sim state at the drawn time; and the contour's depth range,
    // the deepest drawn planet's field depth (the map gravity materials' normalisation, not a camera bound).
    public void Draw(Zone zone, float lead)
    {
        var size = IconSize;
        IconDraws = BeltDraws = 0;
        var maxDepth = 0f;
        foreach (var icon in _icons)
        {
            float2 planar;
            Quaternion yaw = Quaternion.identity;
            if (icon.Entity != null)
            {
                var drawn = DrawAhead.Position(icon.Entity, lead);
                planar = drawn.xz;
                var rotation = icon.Entity is Ship ship ? DrawAhead.Rotation(ship, lead) : quaternion.identity;
                yaw = Quaternion.Euler(0, IconYaw(icon.Entity, rotation), 0);
            }
            else
            {
                var body = zone.PlanetInstances[icon.Planet];
                planar = DrawAhead.Orbit(zone, body.BodyData.Orbit.Key, lead);
                maxDepth = Mathf.Max(maxDepth, -DrawAhead.Height(zone, planar, lead));
            }

            if (icon.Fade <= .01f) continue;
            if (icon.Material.HasProperty("_Color"))
                icon.Material.SetColor("_Color", new Color(icon.Base.r, icon.Base.g, icon.Base.b, icon.Base.a * icon.Fade));
            var parameters = new RenderParams(icon.Material) { layer = _layer, shadowCastingMode = ShadowCastingMode.Off };
            icon.Drawn = Matrix4x4.TRS(new Vector3(planar.x, PlaneY + icon.Offset, planar.y), yaw * icon.Authored,
                Vector3.one * size);
            Graphics.RenderMesh(parameters, icon.Mesh, 0, icon.Drawn);
            IconDraws++;
        }

        var depthRange = Mathf.Max(maxDepth, 1f);
        foreach (var material in _gravityMaterials)
        {
            material.SetFloat("_StartDepth", 0f);
            material.SetFloat("_DepthRange", depthRange);
        }

        foreach (var belt in _belts.Values) DrawBelt(belt);
    }

    public void Clear()
    {
        foreach (var icon in _icons) Discard(icon.Material);
        _icons.Clear();
        _entityIcons.Clear();
        foreach (var belt in _belts.Values) belt.Buffer.Release();
        _belts.Clear();
    }
}
