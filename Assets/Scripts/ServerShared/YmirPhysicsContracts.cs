using System;
using System.Collections.Generic;

[Serializable]
public sealed class YmirStepRequest
{
    public float deltaTime;
    public YmirWorld world;
}

[Serializable]
public sealed class YmirStepResult
{
    public YmirWorld world;
    public YmirContactEvent[] contacts;
}

[Serializable]
public sealed class YmirSphereOverlapRequest
{
    public YmirVec3 center;
    public float radius;
    public YmirSphereQueryBody[] bodies;
}

[Serializable]
public sealed class YmirSphereOverlapResult
{
    public YmirSphereOverlapHit[] hits;
}

[Serializable]
public sealed class YmirSphereOverlapHit
{
    public string bodyId;
    public YmirVec3 point;
    public YmirVec3 normal;
    public float penetration;
    public float distance;
}

[Serializable]
public sealed class YmirCircleCastRequest
{
    public YmirVec2 origin;
    public YmirVec2 direction;
    public float distance;
    public float radius;
    public YmirWorld world;
}

[Serializable]
public sealed class YmirCircleOverlapRequest
{
    public YmirVec2 center;
    public float radius;
    public YmirWorld world;
}

[Serializable]
public sealed class YmirCircleOverlapResult
{
    public YmirCircleOverlapHit[] hits;
}

[Serializable]
public sealed class YmirCircleOverlapHit
{
    public string bodyId;
    public YmirVec2 point;
    public YmirVec2 normal;
    public float penetration;
    public float distance;
}

[Serializable]
public sealed class YmirCircleCastResult
{
    public YmirCircleCastHit[] hits;
}

[Serializable]
public sealed class YmirCircleCastHit
{
    public string bodyId;
    public YmirVec2 point;
    public YmirVec2 normal;
    public float distance;
}

[Serializable]
public sealed class YmirSphereCastRequest
{
    public YmirVec3 origin;
    public YmirVec3 direction;
    public float distance;
    public float radius;
    public YmirSphereQueryBody[] bodies;
}

[Serializable]
public sealed class YmirSphereCastResult
{
    public YmirSphereCastHit[] hits;
}

[Serializable]
public sealed class YmirSphereCastHit
{
    public string bodyId;
    public YmirVec3 point;
    public YmirVec3 normal;
    public float distance;
}

[Serializable]
public sealed class YmirWorld
{
    public float time;
    public YmirPhysicsBody[] bodies;
    public YmirRadialField[] fields;
}

[Serializable]
public sealed class YmirSphereQueryWorld
{
    public YmirSphereQueryBody[] bodies;
}

[Serializable]
public sealed class YmirSphereQueryBody
{
    public string id;
    public YmirVec3 position;
    public float radius;
}

[Serializable]
public sealed class YmirPhysicsBody
{
    public string id;
    public YmirVec2 position;
    public YmirVec2 velocity;
    public YmirVec2 direction;
    public float angularVelocity;
    public float torque;
    public float momentOfInertia;
    public float radius;
    public float mass;
    public bool isStatic;
    public float restitution;
}

[Serializable]
public sealed class YmirRadialField
{
    public string id;
    public YmirVec2 position;
    public float strength;
    public float radius;
}

[Serializable]
public sealed class YmirContactEvent
{
    public string bodyA;
    public string bodyB;
    public YmirVec2 point;
    public YmirVec2 normal;
    public float penetration;
    public float relativeSpeed;
}

[Serializable]
public struct YmirVec2
{
    public float x;
    public float y;
}

[Serializable]
public struct YmirVec3
{
    public float x;
    public float y;
    public float z;
}

public static class YmirPhysicsQueries
{
    private const float Epsilon = 0.000001f;

    public static YmirStepResult Step(YmirStepRequest request)
    {
        if (request == null || request.world == null)
        {
            return new YmirStepResult
            {
                world = new YmirWorld
                {
                    time = 0,
                    bodies = Array.Empty<YmirPhysicsBody>(),
                    fields = Array.Empty<YmirRadialField>()
                },
                contacts = Array.Empty<YmirContactEvent>()
            };
        }

        return Step(request.world, request.deltaTime);
    }

    public static YmirStepResult Step(YmirWorld world, float deltaTime)
    {
        var bodies = CopyBodies(world?.bodies);
        var fields = CopyFields(world?.fields);
        var steppedWorld = new YmirWorld
        {
            time = world == null ? 0 : world.time + Math.Max(0, deltaTime),
            bodies = bodies,
            fields = fields
        };

        if (world == null || bodies.Length == 0 || deltaTime <= 0)
        {
            return new YmirStepResult
            {
                world = steppedWorld,
                contacts = Array.Empty<YmirContactEvent>()
            };
        }

        ApplyFields(bodies, fields, deltaTime);
        IntegrateBodies(bodies, deltaTime);
        var contacts = ResolveContacts(bodies);
        return new YmirStepResult
        {
            world = steppedWorld,
            contacts = contacts
        };
    }

    public static YmirSphereOverlapResult OverlapSphere(YmirSphereOverlapRequest request)
    {
        return new YmirSphereOverlapResult
        {
            hits = request == null
                ? Array.Empty<YmirSphereOverlapHit>()
                : OverlapSphere(request.bodies, request.center, request.radius)
        };
    }

    public static YmirSphereOverlapHit[] OverlapSphere(
        IReadOnlyList<YmirSphereQueryBody> bodies,
        YmirVec3 center,
        float radius)
    {
        var hits = new List<YmirSphereOverlapHit>();
        OverlapSphere(bodies, center, radius, hits);
        return hits.Count == 0 ? Array.Empty<YmirSphereOverlapHit>() : hits.ToArray();
    }

    public static int OverlapSphere(
        IReadOnlyList<YmirSphereQueryBody> bodies,
        YmirVec3 center,
        float radius,
        List<YmirSphereOverlapHit> hits)
    {
        if (hits == null)
            throw new ArgumentNullException(nameof(hits));

        hits.Clear();
        if (bodies == null || bodies.Count == 0 || radius < 0)
            return 0;

        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body == null || body.radius < 0)
                continue;

            var delta = Subtract(body.position, center);
            var centerDistanceSquared = LengthSquared(delta);
            var combinedRadius = radius + body.radius;
            if (centerDistanceSquared > combinedRadius * combinedRadius)
                continue;

            var centerDistance = (float)Math.Sqrt(centerDistanceSquared);
            var normal = centerDistance > Epsilon
                ? Scale(delta, 1.0f / centerDistance)
                : new YmirVec3 { x = 1, y = 0, z = 0 };

            hits.Add(new YmirSphereOverlapHit
            {
                bodyId = body.id,
                point = Subtract(body.position, Scale(normal, body.radius)),
                normal = normal,
                penetration = combinedRadius - centerDistance,
                distance = Math.Max(0, centerDistance - body.radius)
            });
        }

        hits.Sort((left, right) => CompareHitOrder(left.distance, left.bodyId, right.distance, right.bodyId));
        return hits.Count;
    }

    public static YmirSphereCastResult CastSphere(YmirSphereCastRequest request)
    {
        return new YmirSphereCastResult
        {
            hits = request == null
                ? Array.Empty<YmirSphereCastHit>()
                : CastSphere(request.bodies, request.origin, request.direction, request.distance, request.radius)
        };
    }

    public static YmirCircleCastResult CastCircle(YmirCircleCastRequest request)
    {
        return new YmirCircleCastResult
        {
            hits = request == null || request.world == null
                ? Array.Empty<YmirCircleCastHit>()
                : CastCircle(request.world.bodies, request.origin, request.direction, request.distance, request.radius)
        };
    }

    public static YmirCircleOverlapResult OverlapCircle(YmirCircleOverlapRequest request)
    {
        return new YmirCircleOverlapResult
        {
            hits = request == null || request.world == null
                ? Array.Empty<YmirCircleOverlapHit>()
                : OverlapCircle(request.world.bodies, request.center, request.radius)
        };
    }

    public static YmirCircleOverlapHit[] OverlapCircle(
        IReadOnlyList<YmirPhysicsBody> bodies,
        YmirVec2 center,
        float radius)
    {
        var hits = new List<YmirCircleOverlapHit>();
        OverlapCircle(bodies, center, radius, hits);
        return hits.Count == 0 ? Array.Empty<YmirCircleOverlapHit>() : hits.ToArray();
    }

    public static int OverlapCircle(
        IReadOnlyList<YmirPhysicsBody> bodies,
        YmirVec2 center,
        float radius,
        List<YmirCircleOverlapHit> hits)
    {
        if (hits == null)
            throw new ArgumentNullException(nameof(hits));

        hits.Clear();
        if (bodies == null || bodies.Count == 0 || radius < 0)
            return 0;

        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body == null || body.radius < 0)
                continue;

            var delta = Subtract(body.position, center);
            var centerDistanceSquared = LengthSquared(delta);
            var combinedRadius = radius + body.radius;
            if (centerDistanceSquared > combinedRadius * combinedRadius)
                continue;

            var centerDistance = (float)Math.Sqrt(centerDistanceSquared);
            var normal = centerDistance > Epsilon
                ? Scale(delta, 1.0f / centerDistance)
                : new YmirVec2 { x = 1, y = 0 };

            hits.Add(new YmirCircleOverlapHit
            {
                bodyId = body.id,
                point = Subtract(body.position, Scale(normal, body.radius)),
                normal = normal,
                penetration = combinedRadius - centerDistance,
                distance = Math.Max(0, centerDistance - body.radius)
            });
        }

        hits.Sort((left, right) => CompareHitOrder(left.distance, left.bodyId, right.distance, right.bodyId));
        return hits.Count;
    }

    public static YmirCircleCastHit[] CastCircle(
        IReadOnlyList<YmirPhysicsBody> bodies,
        YmirVec2 origin,
        YmirVec2 direction,
        float distance,
        float radius)
    {
        var hits = new List<YmirCircleCastHit>();
        CastCircle(bodies, origin, direction, distance, radius, hits);
        return hits.Count == 0 ? Array.Empty<YmirCircleCastHit>() : hits.ToArray();
    }

    public static int CastCircle(
        IReadOnlyList<YmirPhysicsBody> bodies,
        YmirVec2 origin,
        YmirVec2 direction,
        float distance,
        float radius,
        List<YmirCircleCastHit> hits)
    {
        if (hits == null)
            throw new ArgumentNullException(nameof(hits));

        hits.Clear();
        var directionLengthSquared = LengthSquared(direction);
        if (bodies == null || bodies.Count == 0 || distance < 0 || radius < 0 || directionLengthSquared <= Epsilon)
            return 0;

        var directionLength = (float)Math.Sqrt(directionLengthSquared);
        var rayDirection = Scale(direction, 1.0f / directionLength);
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body == null || body.radius < 0)
                continue;

            var toOrigin = Subtract(origin, body.position);
            var combinedRadius = radius + body.radius;
            var c = LengthSquared(toOrigin) - combinedRadius * combinedRadius;
            float hitDistance;
            if (c <= 0)
            {
                hitDistance = 0;
            }
            else
            {
                var b = Dot(toOrigin, rayDirection);
                if (b > 0)
                    continue;

                var discriminant = b * b - c;
                if (discriminant < 0)
                    continue;

                hitDistance = -b - (float)Math.Sqrt(discriminant);
                if (hitDistance < 0 || hitDistance > distance)
                    continue;
            }

            var castCenter = Add(origin, Scale(rayDirection, hitDistance));
            var delta = Subtract(body.position, castCenter);
            var centerDistanceSquared = LengthSquared(delta);
            var normal = centerDistanceSquared > Epsilon
                ? Scale(delta, 1.0f / (float)Math.Sqrt(centerDistanceSquared))
                : Scale(rayDirection, -1);

            hits.Add(new YmirCircleCastHit
            {
                bodyId = body.id,
                point = Subtract(body.position, Scale(normal, body.radius)),
                normal = normal,
                distance = hitDistance
            });
        }

        hits.Sort((left, right) => CompareHitOrder(left.distance, left.bodyId, right.distance, right.bodyId));
        return hits.Count;
    }

    public static YmirSphereCastHit[] CastSphere(
        IReadOnlyList<YmirSphereQueryBody> bodies,
        YmirVec3 origin,
        YmirVec3 direction,
        float distance,
        float radius)
    {
        var hits = new List<YmirSphereCastHit>();
        CastSphere(bodies, origin, direction, distance, radius, hits);
        return hits.Count == 0 ? Array.Empty<YmirSphereCastHit>() : hits.ToArray();
    }

    public static int CastSphere(
        IReadOnlyList<YmirSphereQueryBody> bodies,
        YmirVec3 origin,
        YmirVec3 direction,
        float distance,
        float radius,
        List<YmirSphereCastHit> hits)
    {
        if (hits == null)
            throw new ArgumentNullException(nameof(hits));

        hits.Clear();
        var directionLengthSquared = LengthSquared(direction);
        if (bodies == null || bodies.Count == 0 || distance < 0 || radius < 0 || directionLengthSquared <= Epsilon)
            return 0;

        var directionLength = (float)Math.Sqrt(directionLengthSquared);
        var rayDirection = Scale(direction, 1.0f / directionLength);
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body == null || body.radius < 0)
                continue;

            var toOrigin = Subtract(origin, body.position);
            var combinedRadius = radius + body.radius;
            var c = LengthSquared(toOrigin) - combinedRadius * combinedRadius;
            float hitDistance;
            if (c <= 0)
            {
                hitDistance = 0;
            }
            else
            {
                var b = Dot(toOrigin, rayDirection);
                if (b > 0)
                    continue;

                var discriminant = b * b - c;
                if (discriminant < 0)
                    continue;

                hitDistance = -b - (float)Math.Sqrt(discriminant);
                if (hitDistance < 0 || hitDistance > distance)
                    continue;
            }

            var castCenter = Add(origin, Scale(rayDirection, hitDistance));
            var delta = Subtract(body.position, castCenter);
            var centerDistanceSquared = LengthSquared(delta);
            var normal = centerDistanceSquared > Epsilon
                ? Scale(delta, 1.0f / (float)Math.Sqrt(centerDistanceSquared))
                : Scale(rayDirection, -1);

            hits.Add(new YmirSphereCastHit
            {
                bodyId = body.id,
                point = Subtract(body.position, Scale(normal, body.radius)),
                normal = normal,
                distance = hitDistance
            });
        }

        hits.Sort((left, right) => CompareHitOrder(left.distance, left.bodyId, right.distance, right.bodyId));
        return hits.Count;
    }

    private static int CompareHitOrder(float leftDistance, string leftBodyId, float rightDistance, string rightBodyId)
    {
        var distanceOrder = leftDistance.CompareTo(rightDistance);
        return distanceOrder != 0
            ? distanceOrder
            : string.CompareOrdinal(leftBodyId, rightBodyId);
    }

    private static void ApplyFields(IReadOnlyList<YmirPhysicsBody> bodies, IReadOnlyList<YmirRadialField> fields, float deltaTime)
    {
        if (fields == null || fields.Count == 0)
            return;

        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            var body = bodies[bodyIndex];
            if (body == null || body.isStatic)
                continue;

            for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
            {
                var field = fields[fieldIndex];
                if (field == null || field.radius <= 0 || Math.Abs(field.strength) <= Epsilon)
                    continue;

                var offset = Subtract(body.position, field.position);
                var distanceSquared = LengthSquared(offset);
                var radiusSquared = field.radius * field.radius;
                if (distanceSquared <= Epsilon || distanceSquared > radiusSquared)
                    continue;

                var distance = (float)Math.Sqrt(distanceSquared);
                var falloff = 1.0f - Math.Min(1.0f, distance / field.radius);
                var acceleration = Scale(offset, field.strength * falloff / distance);
                body.velocity = Add(body.velocity, Scale(acceleration, deltaTime));
            }
        }
    }

    private static void IntegrateBodies(IReadOnlyList<YmirPhysicsBody> bodies, float deltaTime)
    {
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body == null || body.isStatic)
                continue;

            if (body.momentOfInertia > Epsilon)
                body.angularVelocity += body.torque / body.momentOfInertia * deltaTime;

            body.position = Add(body.position, Scale(body.velocity, deltaTime));
            if (Math.Abs(body.angularVelocity) > Epsilon)
                body.direction = Rotate(body.direction, body.angularVelocity * deltaTime);
        }
    }

    private static YmirContactEvent[] ResolveContacts(IReadOnlyList<YmirPhysicsBody> bodies)
    {
        var contacts = new List<YmirContactEvent>();
        for (var leftIndex = 0; leftIndex < bodies.Count; leftIndex++)
        {
            var left = bodies[leftIndex];
            if (left == null || left.radius < 0)
                continue;

            for (var rightIndex = leftIndex + 1; rightIndex < bodies.Count; rightIndex++)
            {
                var right = bodies[rightIndex];
                if (right == null || right.radius < 0 || left.isStatic && right.isStatic)
                    continue;

                var delta = Subtract(right.position, left.position);
                var distanceSquared = LengthSquared(delta);
                var combinedRadius = left.radius + right.radius;
                if (distanceSquared > combinedRadius * combinedRadius)
                    continue;

                var distance = (float)Math.Sqrt(distanceSquared);
                var normal = distance > Epsilon
                    ? Scale(delta, 1.0f / distance)
                    : new YmirVec2 { x = 1, y = 0 };
                var penetration = combinedRadius - distance;
                var relativeVelocity = Subtract(left.velocity, right.velocity);

                contacts.Add(new YmirContactEvent
                {
                    bodyA = left.id,
                    bodyB = right.id,
                    point = Add(left.position, Scale(normal, Math.Max(0, left.radius - penetration * 0.5f))),
                    normal = normal,
                    penetration = penetration,
                    relativeSpeed = Math.Max(0, Dot(relativeVelocity, normal))
                });

                SeparateBodies(left, right, normal, penetration);
                ResolveVelocity(left, right, normal);
            }
        }

        return contacts.ToArray();
    }

    private static void SeparateBodies(YmirPhysicsBody left, YmirPhysicsBody right, YmirVec2 normal, float penetration)
    {
        if (penetration <= 0)
            return;

        if (left.isStatic)
        {
            right.position = Add(right.position, Scale(normal, penetration));
            return;
        }

        if (right.isStatic)
        {
            left.position = Subtract(left.position, Scale(normal, penetration));
            return;
        }

        var leftInverseMass = InverseMass(left);
        var rightInverseMass = InverseMass(right);
        var inverseMassTotal = leftInverseMass + rightInverseMass;
        if (inverseMassTotal <= Epsilon)
            return;

        var correction = Scale(normal, penetration / inverseMassTotal);
        left.position = Subtract(left.position, Scale(correction, leftInverseMass));
        right.position = Add(right.position, Scale(correction, rightInverseMass));
    }

    private static void ResolveVelocity(YmirPhysicsBody left, YmirPhysicsBody right, YmirVec2 normal)
    {
        var leftInverseMass = InverseMass(left);
        var rightInverseMass = InverseMass(right);
        var inverseMassTotal = leftInverseMass + rightInverseMass;
        if (inverseMassTotal <= Epsilon)
            return;

        var relativeVelocity = Subtract(right.velocity, left.velocity);
        var velocityAlongNormal = Dot(relativeVelocity, normal);
        if (velocityAlongNormal > 0)
            return;

        var restitution = Math.Max(0, Math.Min(1, Math.Max(left.restitution, right.restitution)));
        var impulseMagnitude = -(1 + restitution) * velocityAlongNormal / inverseMassTotal;
        var impulse = Scale(normal, impulseMagnitude);
        if (!left.isStatic)
            left.velocity = Subtract(left.velocity, Scale(impulse, leftInverseMass));
        if (!right.isStatic)
            right.velocity = Add(right.velocity, Scale(impulse, rightInverseMass));
    }

    private static float InverseMass(YmirPhysicsBody body)
    {
        return body == null || body.isStatic || body.mass <= Epsilon ? 0 : 1.0f / body.mass;
    }

    private static YmirPhysicsBody[] CopyBodies(IReadOnlyList<YmirPhysicsBody> bodies)
    {
        if (bodies == null || bodies.Count == 0)
            return Array.Empty<YmirPhysicsBody>();

        var copy = new YmirPhysicsBody[bodies.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            var body = bodies[i];
            copy[i] = body == null
                ? null
                : new YmirPhysicsBody
                {
                    id = body.id,
                    position = body.position,
                    velocity = body.velocity,
                    direction = body.direction,
                    angularVelocity = body.angularVelocity,
                    torque = body.torque,
                    momentOfInertia = body.momentOfInertia,
                    radius = body.radius,
                    mass = body.mass,
                    isStatic = body.isStatic,
                    restitution = body.restitution
                };
        }

        return copy;
    }

    private static YmirRadialField[] CopyFields(IReadOnlyList<YmirRadialField> fields)
    {
        if (fields == null || fields.Count == 0)
            return Array.Empty<YmirRadialField>();

        var copy = new YmirRadialField[fields.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            var field = fields[i];
            copy[i] = field == null
                ? null
                : new YmirRadialField
                {
                    id = field.id,
                    position = field.position,
                    strength = field.strength,
                    radius = field.radius
                };
        }

        return copy;
    }

    private static YmirVec3 Subtract(YmirVec3 left, YmirVec3 right)
    {
        return new YmirVec3
        {
            x = left.x - right.x,
            y = left.y - right.y,
            z = left.z - right.z
        };
    }

    private static YmirVec2 Subtract(YmirVec2 left, YmirVec2 right)
    {
        return new YmirVec2
        {
            x = left.x - right.x,
            y = left.y - right.y
        };
    }

    private static YmirVec3 Add(YmirVec3 left, YmirVec3 right)
    {
        return new YmirVec3
        {
            x = left.x + right.x,
            y = left.y + right.y,
            z = left.z + right.z
        };
    }

    private static YmirVec2 Add(YmirVec2 left, YmirVec2 right)
    {
        return new YmirVec2
        {
            x = left.x + right.x,
            y = left.y + right.y
        };
    }

    private static YmirVec3 Scale(YmirVec3 value, float scale)
    {
        return new YmirVec3
        {
            x = value.x * scale,
            y = value.y * scale,
            z = value.z * scale
        };
    }

    private static YmirVec2 Scale(YmirVec2 value, float scale)
    {
        return new YmirVec2
        {
            x = value.x * scale,
            y = value.y * scale
        };
    }

    private static YmirVec2 Rotate(YmirVec2 value, float radians)
    {
        var sine = (float)Math.Sin(radians);
        var cosine = (float)Math.Cos(radians);
        return new YmirVec2
        {
            x = value.x * cosine - value.y * sine,
            y = value.x * sine + value.y * cosine
        };
    }

    private static float LengthSquared(YmirVec3 value)
    {
        return value.x * value.x + value.y * value.y + value.z * value.z;
    }

    private static float LengthSquared(YmirVec2 value)
    {
        return value.x * value.x + value.y * value.y;
    }

    private static float Dot(YmirVec3 left, YmirVec3 right)
    {
        return left.x * right.x + left.y * right.y + left.z * right.z;
    }

    private static float Dot(YmirVec2 left, YmirVec2 right)
    {
        return left.x * right.x + left.y * right.y;
    }
}
