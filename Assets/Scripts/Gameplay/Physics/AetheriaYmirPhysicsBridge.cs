using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

public sealed class AetheriaYmirPhysicsBridge : MonoBehaviour
{
    private const string ProjectileBodyId = "aetheria.projectile";
    private const string TargetBodyPrefix = "aetheria.target.";

    private static AetheriaYmirPhysicsBridge _instance;

    public string ServiceUrl = "http://127.0.0.1:8877/simulate/step";
    public string OverlapSphereUrl = "http://127.0.0.1:8877/query/overlap-sphere";
    public string OverlapCircleUrl = "http://127.0.0.1:8877/query/overlap-circle";
    public string CastCircleUrl = "http://127.0.0.1:8877/query/cast-circle";
    public string CastSphereUrl = "http://127.0.0.1:8877/query/cast-sphere";
    public bool EnableProjectileCutover = true;
    public float ProjectileRadius = 0.1f;
    public float ProjectileMass = 1.0f;
    public float TargetRadiusPadding = 0.1f;
    public int TimeoutMilliseconds = 20;
    public AetheriaDaemonObserver DaemonObserver;

    private readonly List<YmirPhysicsBody> _projectileBodies = new List<YmirPhysicsBody>();
    private readonly List<YmirPhysicsBody> _targetBodies = new List<YmirPhysicsBody>();
    private readonly List<YmirPhysicsBody> _zoneBodies = new List<YmirPhysicsBody>();
    private readonly List<YmirPhysicsBody> _daemonBodies = new List<YmirPhysicsBody>();
    private readonly List<YmirSphereQueryBody> _clickableBodies = new List<YmirSphereQueryBody>();
    private readonly Dictionary<string, HullCollider> _hullBodyMap = new Dictionary<string, HullCollider>(StringComparer.Ordinal);
    private readonly Dictionary<string, ClickableCollider> _clickableBodyMap = new Dictionary<string, ClickableCollider>(StringComparer.Ordinal);

    public static AetheriaYmirPhysicsBridge Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindAnyObjectByType<AetheriaYmirPhysicsBridge>();
            if (_instance != null) return _instance;

            var bridge = new GameObject("Aetheria Ymir Physics Bridge");
            DontDestroyOnLoad(bridge);
            _instance = bridge.AddComponent<AetheriaYmirPhysicsBridge>();
            return _instance;
        }
    }

    public bool TryStepProjectile(Projectile projectile, float deltaTime, out AetheriaYmirProjectileStep step)
    {
        step = default;
        if (!EnableProjectileCutover || projectile == null || deltaTime <= 0)
            return false;

        var request = BuildProjectileRequest(projectile, deltaTime);
        if (request.world.bodies.Length == 0)
            return false;

        YmirStepResult result;
        try
        {
            result = PostJson<YmirStepRequest, YmirStepResult>(ServiceUrl, request);
        }
        catch (Exception error)
        {
            Debug.LogWarning($"Ymir projectile step failed: {error.Message}");
            return false;
        }

        if (result == null || result.world == null)
            return false;

        var projectileBody = FindBody(result.world, ProjectileBodyId);
        if (projectileBody == null)
            return false;

        step.Position = new Vector3(projectileBody.position.x, projectile.transform.position.y, projectileBody.position.y);
        step.Velocity = new Vector3(projectileBody.velocity.x, projectile.Velocity.y, projectileBody.velocity.y);

        foreach (var contact in result.contacts ?? Array.Empty<YmirContactEvent>())
        {
            var otherBody = string.Equals(contact.bodyA, ProjectileBodyId, StringComparison.Ordinal)
                ? contact.bodyB
                : string.Equals(contact.bodyB, ProjectileBodyId, StringComparison.Ordinal)
                    ? contact.bodyA
                    : "";
            if (string.IsNullOrWhiteSpace(otherBody))
                continue;

            var hull = ResolveTargetHull(projectile.TargetInstance, otherBody);
            if (hull == null)
                continue;

            step.Hit = new AetheriaYmirProjectileHit(
                hull,
                new Vector3(contact.point.x, projectile.transform.position.y, contact.point.y),
                new Vector3(contact.normal.x, 0, contact.normal.y).normalized);
            break;
        }

        return true;
    }

    public bool TryOverlapDaemonBodies(
        Vector3 center,
        float radius,
        out IReadOnlyList<YmirCircleOverlapHit> hits)
    {
        hits = Array.Empty<YmirCircleOverlapHit>();
        if (!TryBuildDaemonWorld(out var world) || radius <= 0)
            return false;

        var request = new YmirCircleOverlapRequest
        {
            world = world,
            center = ToVec2(center),
            radius = radius
        };

        return TryOverlapCircle(request, out hits);
    }

    public bool TryCastDaemonBodies(
        Vector3 origin,
        Vector3 direction,
        float distance,
        float radius,
        out IReadOnlyList<YmirCircleCastHit> hits)
    {
        hits = Array.Empty<YmirCircleCastHit>();
        if (!TryBuildDaemonWorld(out var world) || distance <= 0 || radius < 0)
            return false;

        var planarDirection = new Vector3(direction.x, 0, direction.z);
        if (planarDirection.sqrMagnitude <= float.Epsilon)
            return false;

        var request = new YmirCircleCastRequest
        {
            world = world,
            origin = ToVec2(origin),
            direction = ToVec2(planarDirection.normalized),
            distance = distance,
            radius = radius
        };

        return TryCastCircle(request, "Ymir daemon body circle cast query", out hits);
    }

    public bool TryOverlapTargetHulls(
        EntityInstance target,
        Vector3 center,
        float radius,
        out IReadOnlyList<AetheriaYmirOverlapHit> hits)
    {
        hits = Array.Empty<AetheriaYmirOverlapHit>();
        if (!EnableProjectileCutover || target == null || radius <= 0)
            return false;

        var world = BuildTargetWorld(target);
        if (world.bodies.Length == 0)
            return false;

        var request = new YmirCircleOverlapRequest
        {
            center = ToVec2(center),
            radius = radius,
            world = world
        };

        if (!TryOverlapCircle(request, out var ymirHits))
            return false;

        var resolved = new List<AetheriaYmirOverlapHit>();
        foreach (var hit in ymirHits)
        {
            var hull = ResolveTargetHull(target, hit.bodyId);
            if (hull == null)
                continue;

            resolved.Add(new AetheriaYmirOverlapHit(
                hull,
                new Vector3(hit.point.x, center.y, hit.point.y),
                new Vector3(hit.normal.x, 0, hit.normal.y).normalized,
                hit.penetration,
                hit.distance));
        }

        hits = resolved;
        return true;
    }

    public bool TryOverlapSphere(
        IReadOnlyList<YmirSphereQueryBody> bodies,
        Vector3 center,
        float radius,
        out IReadOnlyList<YmirSphereOverlapHit> hits)
    {
        hits = Array.Empty<YmirSphereOverlapHit>();
        if (bodies == null || bodies.Count == 0)
            return false;

        var bodyArray = bodies as YmirSphereQueryBody[] ?? CopySphereQueryBodies(bodies);
        return TryOverlapSphere(bodyArray, center, radius, "Ymir sphere overlap query", out hits);
    }

    public bool TryCastTargetHulls(
        EntityInstance target,
        Vector3 origin,
        Vector3 direction,
        float distance,
        float radius,
        out IReadOnlyList<AetheriaYmirCastHit> hits)
    {
        hits = Array.Empty<AetheriaYmirCastHit>();
        if (!EnableProjectileCutover || target == null || distance <= 0 || direction.sqrMagnitude <= 0.000001f)
            return false;

        var world = BuildTargetWorld(target);
        if (world.bodies.Length == 0)
            return false;

        var request = new YmirCircleCastRequest
        {
            origin = ToVec2(origin),
            direction = ToVec2(direction.normalized),
            distance = distance,
            radius = Mathf.Max(0, radius),
            world = world
        };

        if (!TryCastCircle(request, "Ymir target cast query", out var ymirHits))
            return false;

        var resolved = new List<AetheriaYmirCastHit>();
        foreach (var hit in ymirHits)
        {
            var hull = ResolveTargetHull(target, hit.bodyId);
            if (hull == null)
                continue;

            resolved.Add(new AetheriaYmirCastHit(
                hull,
                ResolveShield(hull),
                new Vector3(hit.point.x, origin.y, hit.point.y),
                new Vector3(hit.normal.x, 0, hit.normal.y).normalized,
                hit.distance));
        }

        hits = resolved;
        return true;
    }

    public bool TryCastClickables(
        IReadOnlyList<ClickableCollider> clickables,
        Ray ray,
        float distance,
        out AetheriaYmirClickableHit hit)
    {
        hit = default;
        if (!EnableProjectileCutover || clickables == null || clickables.Count == 0 || distance <= 0)
            return false;

        _clickableBodyMap.Clear();
        _clickableBodies.Clear();
        for (var i = 0; i < clickables.Count; i++)
        {
            var clickable = clickables[i];
            if (clickable == null || !clickable.isActiveAndEnabled)
                continue;

            var bounds = clickable.ClickBounds;
            var bodyId = $"aetheria.clickable.{i}";
            _clickableBodyMap[bodyId] = clickable;
            _clickableBodies.Add(new YmirSphereQueryBody
            {
                id = bodyId,
                position = ToVec3(bounds.center),
                radius = Mathf.Max(0.001f, bounds.extents.magnitude)
            });
        }

        if (_clickableBodies.Count == 0)
            return false;

        var bodyArray = _clickableBodies.ToArray();
        var request = new YmirSphereCastRequest
        {
            origin = ToVec3(ray.origin),
            direction = ToVec3(ray.direction.normalized),
            distance = distance,
            radius = 0,
            bodies = bodyArray
        };

        if (!TryCastSphere(request, "Ymir clickable cast query", out var ymirHits))
            return false;

        foreach (var resultHit in ymirHits)
        {
            if (!_clickableBodyMap.TryGetValue(resultHit.bodyId, out var clickable) || clickable == null)
                continue;

            hit = new AetheriaYmirClickableHit(
                clickable,
                ToVector3(resultHit.point),
                ToVector3(resultHit.normal).normalized,
                resultHit.distance);
            return true;
        }

        return false;
    }

    public bool TryOverlapZoneHulls(
        ZoneRenderer zoneRenderer,
        Entity sourceEntity,
        Vector3 center,
        float radius,
        out IReadOnlyList<AetheriaYmirOverlapHit> hits)
    {
        hits = Array.Empty<AetheriaYmirOverlapHit>();
        if (!EnableProjectileCutover || zoneRenderer == null || radius <= 0)
            return false;

        _hullBodyMap.Clear();
        var world = BuildZoneWorld(zoneRenderer, sourceEntity, _hullBodyMap);
        if (world.bodies.Length == 0)
            return false;

        var request = new YmirCircleOverlapRequest
        {
            center = ToVec2(center),
            radius = radius,
            world = world
        };

        if (!TryOverlapCircle(request, out var ymirHits))
            return false;

        var resolved = new List<AetheriaYmirOverlapHit>();
        foreach (var hit in ymirHits)
        {
            if (!_hullBodyMap.TryGetValue(hit.bodyId ?? "", out var hull))
                continue;

            resolved.Add(new AetheriaYmirOverlapHit(
                hull,
                new Vector3(hit.point.x, center.y, hit.point.y),
                new Vector3(hit.normal.x, 0, hit.normal.y).normalized,
                hit.penetration,
                hit.distance));
        }

        hits = resolved;
        return true;
    }

    public bool TryCastZoneHulls(
        ZoneRenderer zoneRenderer,
        Entity sourceEntity,
        Vector3 origin,
        Vector3 direction,
        float distance,
        float radius,
        out IReadOnlyList<AetheriaYmirCastHit> hits)
    {
        hits = Array.Empty<AetheriaYmirCastHit>();
        if (!EnableProjectileCutover || zoneRenderer == null || distance <= 0 || direction.sqrMagnitude <= 0.000001f)
            return false;

        _hullBodyMap.Clear();
        var world = BuildZoneWorld(zoneRenderer, sourceEntity, _hullBodyMap);
        if (world.bodies.Length == 0)
            return false;

        var request = new YmirCircleCastRequest
        {
            origin = ToVec2(origin),
            direction = ToVec2(direction.normalized),
            distance = distance,
            radius = Mathf.Max(0, radius),
            world = world
        };

        if (!TryCastCircle(request, "Ymir zone cast query", out var ymirHits))
            return false;

        var resolved = new List<AetheriaYmirCastHit>();
        foreach (var hit in ymirHits)
        {
            if (!_hullBodyMap.TryGetValue(hit.bodyId ?? "", out var hull))
                continue;

            resolved.Add(new AetheriaYmirCastHit(
                hull,
                ResolveShield(hull),
                new Vector3(hit.point.x, origin.y, hit.point.y),
                new Vector3(hit.normal.x, 0, hit.normal.y).normalized,
                hit.distance));
        }

        hits = resolved;
        return true;
    }

    private YmirStepRequest BuildProjectileRequest(Projectile projectile, float deltaTime)
    {
        var transform = projectile.transform;
        _projectileBodies.Clear();
        _projectileBodies.Add(new YmirPhysicsBody
        {
            id = ProjectileBodyId,
            position = ToVec2(transform.position),
            velocity = ToVec2(projectile.Velocity),
            direction = ToVec2(transform.forward),
            angularVelocity = 0,
            torque = 0,
            momentOfInertia = 1,
            radius = Mathf.Max(ProjectileRadius, 0.001f),
            mass = Mathf.Max(ProjectileMass, 0.001f),
            isStatic = false,
            restitution = 0
        });

        var target = projectile.TargetInstance;
        if (target == null)
        {
            return new YmirStepRequest
            {
                deltaTime = deltaTime,
                world = new YmirWorld
                {
                    time = projectile.YmirWorldTime,
                    bodies = _projectileBodies.ToArray(),
                    fields = Array.Empty<YmirRadialField>()
                }
            };
        }

        _projectileBodies.AddRange(BuildTargetWorld(target).bodies);
        return new YmirStepRequest
        {
            deltaTime = deltaTime,
            world = new YmirWorld
            {
                time = projectile.YmirWorldTime,
                bodies = _projectileBodies.ToArray(),
                fields = Array.Empty<YmirRadialField>()
            }
        };
    }

    private YmirWorld BuildTargetWorld(EntityInstance target)
    {
        _targetBodies.Clear();
        for (var i = 0; i < target.HullColliders.Length; i++)
        {
            var hull = target.HullColliders[i];
            if (hull == null)
                continue;

            var bounds = hull.YmirBounds;
            _targetBodies.Add(new YmirPhysicsBody
            {
                id = TargetBodyPrefix + i,
                position = ToVec2(bounds.center),
                velocity = default,
                direction = ToVec2(hull.transform.forward),
                angularVelocity = 0,
                torque = 0,
                momentOfInertia = 1,
                radius = hull.YmirPlanarRadius + TargetRadiusPadding,
                mass = 1,
                isStatic = true,
                restitution = 0
            });
        }

        return new YmirWorld
        {
            time = 0,
            bodies = _targetBodies.ToArray(),
            fields = Array.Empty<YmirRadialField>()
        };
    }

    private YmirWorld BuildZoneWorld(
        ZoneRenderer zoneRenderer,
        Entity sourceEntity,
        Dictionary<string, HullCollider> bodyMap)
    {
        _zoneBodies.Clear();
        var entityIndex = 0;
        foreach (var entityInstance in zoneRenderer.DaemonEntityInstances.Values)
        {
            if (entityInstance == null || entityInstance.Entity == sourceEntity)
                continue;

            for (var hullIndex = 0; hullIndex < entityInstance.HullColliders.Length; hullIndex++)
            {
                var hull = entityInstance.HullColliders[hullIndex];
                if (hull == null)
                    continue;

                var bounds = hull.YmirBounds;
                var bodyId = $"aetheria.entity.{entityIndex}.hull.{hullIndex}";
                bodyMap[bodyId] = hull;
                _zoneBodies.Add(new YmirPhysicsBody
                {
                    id = bodyId,
                    position = ToVec2(bounds.center),
                    velocity = default,
                    direction = ToVec2(hull.transform.forward),
                    angularVelocity = 0,
                    torque = 0,
                    momentOfInertia = 1,
                    radius = hull.YmirPlanarRadius + TargetRadiusPadding,
                    mass = 1,
                    isStatic = true,
                    restitution = 0
                });
            }

            entityIndex++;
        }

        return new YmirWorld
        {
            time = 0,
            bodies = _zoneBodies.ToArray(),
            fields = Array.Empty<YmirRadialField>()
        };
    }

    private bool TryBuildDaemonWorld(out YmirWorld world)
    {
        world = null;
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasRenderNativeView)
            return false;

        var view = observer.LastRenderNativeView;
        if (!view.IsCreated || !view.HasPhysicsRadius)
            return false;

        _daemonBodies.Clear();
        for (var i = 0; i < view.Count; i++)
        {
            var radius = view.PhysicsBodyRadius[i];
            if (radius <= 0)
                continue;

            var inverseMass = view.HasPhysicsInverseMass ? view.PhysicsBodyInverseMass[i] : 1.0f;
            var mass = view.HasPhysicsMass && view.PhysicsBodyMass[i] > 0
                ? view.PhysicsBodyMass[i]
                : inverseMass > 0
                    ? 1.0f / inverseMass
                    : 1.0f;
            var rotation = view.HasRotation ? view.RotationRadians[i] : 0;

            _daemonBodies.Add(new YmirPhysicsBody
            {
                id = $"aetheria.daemon.body.{i}",
                position = new YmirVec2 { x = view.PositionX[i], y = view.PositionZ[i] },
                velocity = view.HasVelocity
                    ? new YmirVec2 { x = view.VelocityX[i], y = view.VelocityZ[i] }
                    : default,
                direction = new YmirVec2 { x = Mathf.Sin(rotation), y = Mathf.Cos(rotation) },
                angularVelocity = 0,
                torque = 0,
                momentOfInertia = 1,
                radius = radius,
                mass = Mathf.Max(mass, 0.001f),
                isStatic = view.HasPhysicsInverseMass && inverseMass <= 0,
                restitution = 0
            });
        }

        if (_daemonBodies.Count == 0)
            return false;

        world = new YmirWorld
        {
            time = Time.time,
            bodies = _daemonBodies.ToArray(),
            fields = Array.Empty<YmirRadialField>()
        };
        return true;
    }

    private AetheriaDaemonObserver ResolveDaemonObserver()
    {
        if (DaemonObserver != null)
            return DaemonObserver;

        DaemonObserver = FindAnyObjectByType<AetheriaDaemonObserver>();
        return DaemonObserver;
    }

    private bool TryOverlapSphere(
        YmirSphereQueryBody[] bodies,
        Vector3 center,
        float radius,
        string failureContext,
        out IReadOnlyList<YmirSphereOverlapHit> hits)
    {
        hits = Array.Empty<YmirSphereOverlapHit>();
        if (!EnableProjectileCutover || bodies == null || bodies.Length == 0 || radius <= 0)
            return false;

        try
        {
            var result = PostJson<YmirSphereOverlapRequest, YmirSphereOverlapResult>(OverlapSphereUrl, new YmirSphereOverlapRequest
            {
                center = ToVec3(center),
                radius = radius,
                bodies = bodies
            });
            hits = result.hits ?? Array.Empty<YmirSphereOverlapHit>();
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning($"{failureContext} failed: {error.Message}");
            return false;
        }
    }

    private bool TryCastSphere(
        YmirSphereCastRequest request,
        string failureContext,
        out IReadOnlyList<YmirSphereCastHit> hits)
    {
        hits = Array.Empty<YmirSphereCastHit>();
        if (!EnableProjectileCutover || request == null || request.bodies == null || request.bodies.Length == 0 || request.distance <= 0 || request.radius < 0)
            return false;

        try
        {
            var result = PostJson<YmirSphereCastRequest, YmirSphereCastResult>(CastSphereUrl, request);
            hits = result.hits ?? Array.Empty<YmirSphereCastHit>();
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning($"{failureContext} failed: {error.Message}");
            return false;
        }
    }

    private bool TryOverlapCircle(
        YmirCircleOverlapRequest request,
        out IReadOnlyList<YmirCircleOverlapHit> hits)
    {
        hits = Array.Empty<YmirCircleOverlapHit>();
        if (!EnableProjectileCutover || request == null || request.world == null || request.world.bodies == null || request.world.bodies.Length == 0 || request.radius <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(OverlapCircleUrl))
        {
            Debug.LogWarning("Ymir circle overlap query failed: daemon URL is not configured.");
            return false;
        }

        try
        {
            var result = PostJson<YmirCircleOverlapRequest, YmirCircleOverlapResult>(OverlapCircleUrl, request);
            hits = result.hits ?? Array.Empty<YmirCircleOverlapHit>();
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning($"Ymir circle overlap query failed: {error.Message}");
            return false;
        }
    }

    private bool TryCastCircle(
        YmirCircleCastRequest request,
        string failureContext,
        out IReadOnlyList<YmirCircleCastHit> hits)
    {
        hits = Array.Empty<YmirCircleCastHit>();
        if (!EnableProjectileCutover || request == null || request.world == null || request.world.bodies == null || request.world.bodies.Length == 0 || request.distance <= 0 || request.radius < 0)
            return false;

        try
        {
            var result = PostJson<YmirCircleCastRequest, YmirCircleCastResult>(CastCircleUrl, request);
            hits = result.hits ?? Array.Empty<YmirCircleCastHit>();
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning($"{failureContext} failed: {error.Message}");
            return false;
        }
    }

    private static YmirSphereQueryBody[] CopySphereQueryBodies(IReadOnlyList<YmirSphereQueryBody> bodies)
    {
        var copy = new YmirSphereQueryBody[bodies.Count];
        for (var i = 0; i < copy.Length; i++)
            copy[i] = bodies[i];
        return copy;
    }

    private TResult PostJson<TRequest, TResult>(string url, TRequest request)
        where TResult : class
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Ymir daemon URL is not configured.");

        var json = JsonUtility.ToJson(request);
        var bytes = Encoding.UTF8.GetBytes(json);
        var http = (HttpWebRequest)WebRequest.Create(url);
        http.Method = "POST";
        http.ContentType = "application/json; charset=utf-8";
        http.ContentLength = bytes.Length;
        http.Timeout = TimeoutMilliseconds;
        http.ReadWriteTimeout = TimeoutMilliseconds;

        using (var stream = http.GetRequestStream())
            stream.Write(bytes, 0, bytes.Length);

        using var response = (HttpWebResponse)http.GetResponse();
        using var reader = new StreamReader(response.GetResponseStream());
        var payload = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidDataException("Ymir daemon returned an empty response.");

        var result = JsonUtility.FromJson<TResult>(payload);
        if (result == null)
            throw new InvalidDataException("Ymir daemon returned a response that could not be parsed.");

        return result;
    }

    private static YmirPhysicsBody FindBody(YmirWorld world, string bodyId)
    {
        foreach (var body in world.bodies ?? Array.Empty<YmirPhysicsBody>())
        {
            if (string.Equals(body.id, bodyId, StringComparison.Ordinal))
                return body;
        }

        return null;
    }

    private static HullCollider ResolveTargetHull(EntityInstance target, string bodyId)
    {
        if (!bodyId.StartsWith(TargetBodyPrefix, StringComparison.Ordinal))
            return null;

        return int.TryParse(bodyId.Substring(TargetBodyPrefix.Length), out var index) &&
               index >= 0 &&
               index < target.HullColliders.Length
            ? target.HullColliders[index]
            : null;
    }

    private static ShieldManager ResolveShield(HullCollider hull)
    {
        var instance = hull != null ? hull.GetComponentInParent<EntityInstance>() : null;
        return instance != null ? instance.Shield : null;
    }

    private static YmirVec2 ToVec2(Vector3 value) => new YmirVec2 { x = value.x, y = value.z };

    private static YmirVec2 ToVec2(CultMath.float2 value) => new YmirVec2 { x = value.x, y = value.y };

    private static YmirVec3 ToVec3(Vector3 value) => new YmirVec3 { x = value.x, y = value.y, z = value.z };

    private static Vector3 ToVector3(YmirVec3 value) => new Vector3(value.x, value.y, value.z);

    private static Bounds ObjectBounds(GameObject gameObject, Vector3 fallbackCenter)
    {
        var renderers = gameObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(fallbackCenter, Vector3.one);

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }
}

public struct AetheriaYmirProjectileStep
{
    public Vector3 Position;
    public Vector3 Velocity;
    public AetheriaYmirProjectileHit Hit;
    public bool HasHit => Hit.Hull != null;
}

public readonly struct AetheriaYmirProjectileHit
{
    public AetheriaYmirProjectileHit(HullCollider hull, Vector3 point, Vector3 normal)
    {
        Hull = hull;
        Point = point;
        Normal = normal;
    }

    public HullCollider Hull { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
}

public readonly struct AetheriaYmirOverlapHit
{
    public AetheriaYmirOverlapHit(HullCollider hull, Vector3 point, Vector3 normal, float penetration, float distance)
    {
        Hull = hull;
        Point = point;
        Normal = normal;
        Penetration = penetration;
        Distance = distance;
    }

    public HullCollider Hull { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
    public float Penetration { get; }
    public float Distance { get; }
}

public readonly struct AetheriaYmirCastHit
{
    public AetheriaYmirCastHit(HullCollider hull, ShieldManager shield, Vector3 point, Vector3 normal, float distance)
    {
        Hull = hull;
        Shield = shield;
        Point = point;
        Normal = normal;
        Distance = distance;
    }

    public HullCollider Hull { get; }
    public ShieldManager Shield { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
    public float Distance { get; }
}

public readonly struct AetheriaYmirClickableHit
{
    public AetheriaYmirClickableHit(ClickableCollider clickable, Vector3 point, Vector3 normal, float distance)
    {
        Clickable = clickable;
        Point = point;
        Normal = normal;
        Distance = distance;
    }

    public ClickableCollider Clickable { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
    public float Distance { get; }
}
