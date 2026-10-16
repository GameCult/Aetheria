using GameCult.Caching;
using CultMath;
using UnityEngine;
using static CultMath.math;
using float2 = CultMath.float2;
using float4 = CultMath.float4;
using Random = UnityEngine.Random;

// The base revision's per-frame belt map path, copied from ZoneRenderer.cs at da6fe033 (class AsteroidBeltUI) so that
// MapBeltPerf can measure it beside MapPresenter in one job. Only the lead it read from ActionGameManager.Clock is
// fixed at zero. Not used by the game.
public class MapBeltPerfBase
{
    private Vector3[] _vertices;
    private Vector3[] _normals;
    private Vector2[] _uvs;
    private int[] _indices;
    private CultRecordKey _orbitParent;
    private Mesh _mesh;
    private float _size;
    private Zone _zone;
    private AsteroidBelt _belt;
    private float _scale;

    public MapBeltPerfBase(Zone zone, AsteroidBelt belt, int spritesheetWidth, int spritesheetHeight, float scale)
    {
        _belt = belt;
        _zone = zone;
        var orbit = zone.Orbits[belt.Data.Orbit.Key];
        _orbitParent = orbit.Data.Parent.Key;
        _vertices = new Vector3[_belt.Data.Asteroids.Length * 4];
        _normals = new Vector3[_belt.Data.Asteroids.Length * 4];
        _uvs = new Vector2[_belt.Data.Asteroids.Length * 4];
        _indices = new int[_belt.Data.Asteroids.Length * 6];
        _scale = scale;

        var maxDist = 0f;
        var spriteSize = float2(1f / spritesheetWidth, 1f / spritesheetHeight);
        // vertex order: bottom left, top left, top right, bottom right
        for (var i = 0; i < belt.Data.Asteroids.Length; i++)
        {
            if (belt.Data.Asteroids[i].Distance > maxDist)
                maxDist = belt.Data.Asteroids[i].Distance;
            var spriteX = Random.Range(0, spritesheetWidth);
            var spriteY = Random.Range(0, spritesheetHeight);

            _uvs[i * 4] = new Vector2(spriteX * spriteSize.x, spriteY * spriteSize.y);
            _uvs[i * 4 + 1] = new Vector2(spriteX * spriteSize.x, spriteY * spriteSize.y + spriteSize.y);
            _uvs[i * 4 + 2] = new Vector2(spriteX * spriteSize.x + spriteSize.x, spriteY * spriteSize.y + spriteSize.y);
            _uvs[i * 4 + 3] = new Vector2(spriteX * spriteSize.x + spriteSize.x, spriteY * spriteSize.y);

            _indices[i * 6] = i * 4;
            _indices[i * 6 + 1] = i * 4 + 1;
            _indices[i * 6 + 2] = i * 4 + 3;
            _indices[i * 6 + 3] = i * 4 + 3;
            _indices[i * 6 + 4] = i * 4 + 1;
            _indices[i * 6 + 5] = i * 4 + 2;
        }

        for (var i = 0; i < _normals.Length; i++) _normals[i] = -Vector3.forward;

        _mesh = new Mesh();
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.triangles = _indices;
        _mesh.normals = _normals;
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * maxDist);
        _size = maxDist;
    }

    public void Update(float4[] transforms, float height)
    {
        var parentPosition = DrawAhead.Orbit(_zone, _orbitParent, 0f);
        for (var i = 0; i < transforms.Length; i++)
        {
            var transform = transforms[i];
            var rotation = Quaternion.Euler(90, transform.z, 0);
            var position = new Vector3(transform.x, height, transform.y);
            _vertices[i * 4] = rotation * new Vector3(-transform.w * _scale, -transform.w * _scale, 0) + position;
            _vertices[i * 4 + 1] = rotation * new Vector3(-transform.w * _scale, transform.w * _scale, 0) + position;
            _vertices[i * 4 + 2] = rotation * new Vector3(transform.w * _scale, transform.w * _scale, 0) + position;
            _vertices[i * 4 + 3] = rotation * new Vector3(transform.w * _scale, -transform.w * _scale, 0) + position;
        }

        _mesh.bounds = new Bounds(new Vector3(parentPosition.x, 0, parentPosition.y), Vector3.one * (_size * 2));
        _mesh.vertices = _vertices;
    }
}
