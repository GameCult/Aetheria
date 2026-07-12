using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AetheriaYmirPhysicsBridge : MonoBehaviour
{
    private static AetheriaYmirPhysicsBridge _instance;
    private readonly List<YmirSphereQueryBody> _clickableBodies = new List<YmirSphereQueryBody>();
    private readonly Dictionary<string, ClickableCollider> _clickableBodyMap =
        new Dictionary<string, ClickableCollider>(StringComparer.Ordinal);

    public bool EnableQueries = true;

    public static AetheriaYmirPhysicsBridge Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<AetheriaYmirPhysicsBridge>();
            if (_instance != null) return _instance;

            var bridge = new GameObject("Aetheria Ymir Presentation Query Bridge");
            DontDestroyOnLoad(bridge);
            _instance = bridge.AddComponent<AetheriaYmirPhysicsBridge>();
            return _instance;
        }
    }

    public bool TryCastClickables(
        IReadOnlyList<ClickableCollider> clickables,
        Ray ray,
        float distance,
        out AetheriaYmirClickableHit hit)
    {
        hit = default;
        if (!EnableQueries || clickables == null || clickables.Count == 0 || distance <= 0)
            return false;

        _clickableBodyMap.Clear();
        _clickableBodies.Clear();
        for (var index = 0; index < clickables.Count; index++)
        {
            var clickable = clickables[index];
            if (clickable == null || !clickable.isActiveAndEnabled) continue;

            var bounds = clickable.ClickBounds;
            var bodyId = $"eve.clickable.{index}";
            _clickableBodyMap[bodyId] = clickable;
            _clickableBodies.Add(new YmirSphereQueryBody
            {
                id = bodyId,
                position = ToVec3(bounds.center),
                radius = Mathf.Max(0.001f, bounds.extents.magnitude)
            });
        }

        if (_clickableBodies.Count == 0) return false;

        YmirSphereCastResult result;
        try
        {
            result = YmirPhysicsQueries.CastSphere(new YmirSphereCastRequest
            {
                origin = ToVec3(ray.origin),
                direction = ToVec3(ray.direction.normalized),
                distance = distance,
                radius = 0,
                bodies = _clickableBodies.ToArray()
            });
        }
        catch (Exception error)
        {
            Debug.LogWarning($"Ymir clickable presentation query failed: {error.Message}");
            return false;
        }

        foreach (var candidate in result?.hits ?? Array.Empty<YmirSphereCastHit>())
        {
            if (!_clickableBodyMap.TryGetValue(candidate.bodyId, out var clickable) || clickable == null)
                continue;
            hit = new AetheriaYmirClickableHit(
                clickable,
                ToVector3(candidate.point),
                ToVector3(candidate.normal).normalized,
                candidate.distance);
            return true;
        }
        return false;
    }

    private static YmirVec3 ToVec3(Vector3 value) => new YmirVec3 { x = value.x, y = value.y, z = value.z };
    private static Vector3 ToVector3(YmirVec3 value) => new Vector3(value.x, value.y, value.z);
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
