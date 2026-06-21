using System.Collections.Generic;
using NUnit.Framework;

public sealed class YmirPhysicsQueryTests
{
    [Test]
    public void StepIntegratesDynamicBodiesWithoutMutatingInputWorld()
    {
        var body = PhysicsBody("ship", 1, 2, 1);
        body.velocity = Vec2(3, -4);
        var world = World(body);

        var result = YmirPhysicsQueries.Step(world, 0.5f);

        Assert.That(result.world.time, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(result.world.bodies[0].position.x, Is.EqualTo(2.5f).Within(0.0001f));
        Assert.That(result.world.bodies[0].position.y, Is.EqualTo(0.0f).Within(0.0001f));
        Assert.That(world.bodies[0].position.x, Is.EqualTo(1.0f).Within(0.0001f));
        Assert.That(world.bodies[0].position.y, Is.EqualTo(2.0f).Within(0.0001f));
    }

    [Test]
    public void StepAppliesRadialFieldsWithLinearFalloff()
    {
        var body = PhysicsBody("ship", 5, 0, 1);
        var world = World(body);
        world.fields = new[]
        {
            new YmirRadialField
            {
                id = "push",
                position = Vec2(0, 0),
                strength = 10,
                radius = 10
            }
        };

        var result = YmirPhysicsQueries.Step(world, 1.0f);

        Assert.That(result.world.bodies[0].velocity.x, Is.EqualTo(5.0f).Within(0.0001f));
        Assert.That(result.world.bodies[0].position.x, Is.EqualTo(10.0f).Within(0.0001f));
    }

    [Test]
    public void StepReportsContactsAndSeparatesDynamicBodyFromStaticBody()
    {
        var mover = PhysicsBody("mover", 0, 0, 1);
        mover.velocity = Vec2(2, 0);
        mover.mass = 1;
        var wall = PhysicsBody("wall", 1.5f, 0, 1);
        wall.isStatic = true;
        var world = World(mover, wall);

        var result = YmirPhysicsQueries.Step(world, 0.1f);

        Assert.That(result.contacts, Has.Length.EqualTo(1));
        Assert.AreEqual("mover", result.contacts[0].bodyA);
        Assert.AreEqual("wall", result.contacts[0].bodyB);
        Assert.That(result.contacts[0].normal.x, Is.EqualTo(1.0f).Within(0.0001f));
        Assert.That(result.contacts[0].penetration, Is.GreaterThan(0));
        Assert.That(result.world.bodies[0].position.x, Is.EqualTo(-0.5f).Within(0.0001f));
    }

    [Test]
    public void StepSkipsInvalidInput()
    {
        var result = YmirPhysicsQueries.Step((YmirStepRequest)null);

        Assert.That(result.world.bodies, Is.Empty);
        Assert.That(result.contacts, Is.Empty);
        Assert.That(YmirPhysicsQueries.Step(World(PhysicsBody("ship", 1, 0, 1)), -1).contacts, Is.Empty);
    }

    [Test]
    public void OverlapSphereReturnsHitsSortedBySurfaceDistance()
    {
        var hits = YmirPhysicsQueries.OverlapSphere(
            new[]
            {
                Body("far", 4, 0, 0, 2),
                Body("near", 1, 0, 0, 0.25f),
                Body("miss", 10, 0, 0, 1)
            },
            Vec3(0, 0, 0),
            3);

        Assert.That(hits, Has.Length.EqualTo(2));
        Assert.AreEqual("near", hits[0].bodyId);
        Assert.AreEqual("far", hits[1].bodyId);
        Assert.That(hits[0].distance, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(hits[1].distance, Is.EqualTo(2.0f).Within(0.0001f));
    }

    [Test]
    public void OverlapSphereBreaksEqualDistanceTiesByBodyId()
    {
        var hits = YmirPhysicsQueries.OverlapSphere(
            new[]
            {
                Body("b", -2, 0, 0, 1),
                Body("a", 2, 0, 0, 1)
            },
            Vec3(0, 0, 0),
            3);

        Assert.That(hits, Has.Length.EqualTo(2));
        Assert.AreEqual("a", hits[0].bodyId);
        Assert.AreEqual("b", hits[1].bodyId);
    }

    [Test]
    public void OverlapSphereReportsPenetrationAndContactPoint()
    {
        var hits = YmirPhysicsQueries.OverlapSphere(
            new[] { Body("target", 2, 0, 0, 1) },
            Vec3(0, 0, 0),
            3);

        Assert.That(hits, Has.Length.EqualTo(1));
        Assert.That(hits[0].penetration, Is.EqualTo(2.0f).Within(0.0001f));
        Assert.That(hits[0].normal.x, Is.EqualTo(1.0f).Within(0.0001f));
        Assert.That(hits[0].point.x, Is.EqualTo(1.0f).Within(0.0001f));
    }

    [Test]
    public void OverlapSphereCanReuseCallerOwnedHitBuffer()
    {
        var hits = new List<YmirSphereOverlapHit> { new YmirSphereOverlapHit { bodyId = "stale" } };

        var count = YmirPhysicsQueries.OverlapSphere(
            new[]
            {
                Body("b", -2, 0, 0, 1),
                Body("a", 2, 0, 0, 1)
            },
            Vec3(0, 0, 0),
            3,
            hits);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(hits, Has.Count.EqualTo(2));
        Assert.AreEqual("a", hits[0].bodyId);
        Assert.AreEqual("b", hits[1].bodyId);
    }

    [Test]
    public void OverlapSphereSkipsInvalidInput()
    {
        Assert.That(YmirPhysicsQueries.OverlapSphere(null, Vec3(0, 0, 0), 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.OverlapSphere(new[] { Body("bad", 0, 0, 0, -1) }, Vec3(0, 0, 0), 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.OverlapSphere(new[] { Body("target", 0, 0, 0, 1) }, Vec3(0, 0, 0), -1), Is.Empty);
    }

    [Test]
    public void OverlapCircleReturnsHitsSortedBySurfaceDistance()
    {
        var hits = YmirPhysicsQueries.OverlapCircle(
            new[]
            {
                PhysicsBody("far", 4, 0, 2),
                PhysicsBody("near", 1, 0, 0.25f),
                PhysicsBody("miss", 10, 0, 1)
            },
            Vec2(0, 0),
            3);

        Assert.That(hits, Has.Length.EqualTo(2));
        Assert.AreEqual("near", hits[0].bodyId);
        Assert.AreEqual("far", hits[1].bodyId);
        Assert.That(hits[0].distance, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(hits[1].distance, Is.EqualTo(2.0f).Within(0.0001f));
    }

    [Test]
    public void OverlapCircleBreaksEqualDistanceTiesByBodyId()
    {
        var hits = YmirPhysicsQueries.OverlapCircle(
            new[]
            {
                PhysicsBody("b", -2, 0, 1),
                PhysicsBody("a", 2, 0, 1)
            },
            Vec2(0, 0),
            3);

        Assert.That(hits, Has.Length.EqualTo(2));
        Assert.AreEqual("a", hits[0].bodyId);
        Assert.AreEqual("b", hits[1].bodyId);
    }

    [Test]
    public void OverlapCircleReportsPenetrationAndContactPoint()
    {
        var hits = YmirPhysicsQueries.OverlapCircle(
            new[] { PhysicsBody("target", 2, 0, 1) },
            Vec2(0, 0),
            3);

        Assert.That(hits, Has.Length.EqualTo(1));
        Assert.That(hits[0].penetration, Is.EqualTo(2.0f).Within(0.0001f));
        Assert.That(hits[0].normal.x, Is.EqualTo(1.0f).Within(0.0001f));
        Assert.That(hits[0].point.x, Is.EqualTo(1.0f).Within(0.0001f));
    }

    [Test]
    public void OverlapCircleCanReuseCallerOwnedHitBuffer()
    {
        var hits = new List<YmirCircleOverlapHit> { new YmirCircleOverlapHit { bodyId = "stale" } };

        var count = YmirPhysicsQueries.OverlapCircle(
            new[]
            {
                PhysicsBody("b", -2, 0, 1),
                PhysicsBody("a", 2, 0, 1)
            },
            Vec2(0, 0),
            3,
            hits);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(hits, Has.Count.EqualTo(2));
        Assert.AreEqual("a", hits[0].bodyId);
        Assert.AreEqual("b", hits[1].bodyId);
    }

    [Test]
    public void OverlapCircleSkipsInvalidInput()
    {
        Assert.That(YmirPhysicsQueries.OverlapCircle(null, Vec2(0, 0), 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.OverlapCircle(new[] { PhysicsBody("bad", 0, 0, -1) }, Vec2(0, 0), 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.OverlapCircle(new[] { PhysicsBody("target", 0, 0, 1) }, Vec2(0, 0), -1), Is.Empty);
    }

    [Test]
    public void CastSphereReturnsHitsSortedByImpactDistance()
    {
        var hits = YmirPhysicsQueries.CastSphere(
            new[]
            {
                Body("far", 8, 0, 0, 1),
                Body("near", 4, 0, 0, 1),
                Body("miss", 4, 4, 0, 1)
            },
            Vec3(0, 0, 0),
            Vec3(1, 0, 0),
            10,
            1);

        Assert.That(hits, Has.Length.EqualTo(2));
        Assert.AreEqual("near", hits[0].bodyId);
        Assert.AreEqual("far", hits[1].bodyId);
        Assert.That(hits[0].distance, Is.EqualTo(2.0f).Within(0.0001f));
        Assert.That(hits[1].distance, Is.EqualTo(6.0f).Within(0.0001f));
    }

    [Test]
    public void CastSphereBreaksEqualDistanceTiesByBodyId()
    {
        var hits = YmirPhysicsQueries.CastSphere(
            new[]
            {
                Body("b", 4, 1, 0, 1),
                Body("a", 4, -1, 0, 1)
            },
            Vec3(0, 0, 0),
            Vec3(1, 0, 0),
            10,
            0);

        Assert.That(hits, Has.Length.EqualTo(2));
        Assert.AreEqual("a", hits[0].bodyId);
        Assert.AreEqual("b", hits[1].bodyId);
    }

    [Test]
    public void CastSphereReportsImpactPointAndNormal()
    {
        var hits = YmirPhysicsQueries.CastSphere(
            new[] { Body("target", 4, 0, 0, 1) },
            Vec3(0, 0, 0),
            Vec3(2, 0, 0),
            10,
            1);

        Assert.That(hits, Has.Length.EqualTo(1));
        Assert.That(hits[0].distance, Is.EqualTo(2.0f).Within(0.0001f));
        Assert.That(hits[0].normal.x, Is.EqualTo(1.0f).Within(0.0001f));
        Assert.That(hits[0].point.x, Is.EqualTo(3.0f).Within(0.0001f));
    }

    [Test]
    public void CastSphereReportsInitialOverlapAtZeroDistance()
    {
        var hits = YmirPhysicsQueries.CastSphere(
            new[] { Body("inside", 0.5f, 0, 0, 1) },
            Vec3(0, 0, 0),
            Vec3(1, 0, 0),
            10,
            1);

        Assert.That(hits, Has.Length.EqualTo(1));
        Assert.That(hits[0].distance, Is.EqualTo(0.0f).Within(0.0001f));
    }

    [Test]
    public void CastSphereCanReuseCallerOwnedHitBuffer()
    {
        var hits = new List<YmirSphereCastHit> { new YmirSphereCastHit { bodyId = "stale" } };

        var count = YmirPhysicsQueries.CastSphere(
            new[]
            {
                Body("far", 8, 0, 0, 1),
                Body("near", 4, 0, 0, 1)
            },
            Vec3(0, 0, 0),
            Vec3(1, 0, 0),
            10,
            1,
            hits);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(hits, Has.Count.EqualTo(2));
        Assert.AreEqual("near", hits[0].bodyId);
        Assert.AreEqual("far", hits[1].bodyId);
    }

    [Test]
    public void CastSphereSkipsInvalidInput()
    {
        Assert.That(YmirPhysicsQueries.CastSphere(null, Vec3(0, 0, 0), Vec3(1, 0, 0), 1, 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.CastSphere(new[] { Body("target", 0, 0, 0, 1) }, Vec3(0, 0, 0), Vec3(0, 0, 0), 1, 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.CastSphere(new[] { Body("target", 0, 0, 0, 1) }, Vec3(0, 0, 0), Vec3(1, 0, 0), -1, 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.CastSphere(new[] { Body("bad", 0, 0, 0, -1) }, Vec3(0, 0, 0), Vec3(1, 0, 0), 1, 1), Is.Empty);
    }

    [Test]
    public void CastCircleReturnsHitsSortedByImpactDistance()
    {
        var hits = YmirPhysicsQueries.CastCircle(
            new[]
            {
                PhysicsBody("far", 8, 0, 1),
                PhysicsBody("near", 4, 0, 1),
                PhysicsBody("miss", 4, 4, 1)
            },
            Vec2(0, 0),
            Vec2(1, 0),
            10,
            1);

        Assert.That(hits, Has.Length.EqualTo(2));
        Assert.AreEqual("near", hits[0].bodyId);
        Assert.AreEqual("far", hits[1].bodyId);
        Assert.That(hits[0].distance, Is.EqualTo(2.0f).Within(0.0001f));
        Assert.That(hits[1].distance, Is.EqualTo(6.0f).Within(0.0001f));
    }

    [Test]
    public void CastCircleBreaksEqualDistanceTiesByBodyId()
    {
        var hits = YmirPhysicsQueries.CastCircle(
            new[]
            {
                PhysicsBody("b", 4, 1, 1),
                PhysicsBody("a", 4, -1, 1)
            },
            Vec2(0, 0),
            Vec2(1, 0),
            10,
            0);

        Assert.That(hits, Has.Length.EqualTo(2));
        Assert.AreEqual("a", hits[0].bodyId);
        Assert.AreEqual("b", hits[1].bodyId);
    }

    [Test]
    public void CastCircleReportsImpactPointAndNormal()
    {
        var hits = YmirPhysicsQueries.CastCircle(
            new[] { PhysicsBody("target", 4, 0, 1) },
            Vec2(0, 0),
            Vec2(2, 0),
            10,
            1);

        Assert.That(hits, Has.Length.EqualTo(1));
        Assert.That(hits[0].distance, Is.EqualTo(2.0f).Within(0.0001f));
        Assert.That(hits[0].normal.x, Is.EqualTo(1.0f).Within(0.0001f));
        Assert.That(hits[0].point.x, Is.EqualTo(3.0f).Within(0.0001f));
    }

    [Test]
    public void CastCircleReportsInitialOverlapAtZeroDistance()
    {
        var hits = YmirPhysicsQueries.CastCircle(
            new[] { PhysicsBody("inside", 0.5f, 0, 1) },
            Vec2(0, 0),
            Vec2(1, 0),
            10,
            1);

        Assert.That(hits, Has.Length.EqualTo(1));
        Assert.That(hits[0].distance, Is.EqualTo(0.0f).Within(0.0001f));
    }

    [Test]
    public void CastCircleCanReuseCallerOwnedHitBuffer()
    {
        var hits = new List<YmirCircleCastHit> { new YmirCircleCastHit { bodyId = "stale" } };

        var count = YmirPhysicsQueries.CastCircle(
            new[]
            {
                PhysicsBody("far", 8, 0, 1),
                PhysicsBody("near", 4, 0, 1)
            },
            Vec2(0, 0),
            Vec2(1, 0),
            10,
            1,
            hits);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(hits, Has.Count.EqualTo(2));
        Assert.AreEqual("near", hits[0].bodyId);
        Assert.AreEqual("far", hits[1].bodyId);
    }

    [Test]
    public void CastCircleSkipsInvalidInput()
    {
        Assert.That(YmirPhysicsQueries.CastCircle(null, Vec2(0, 0), Vec2(1, 0), 1, 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.CastCircle(new[] { PhysicsBody("target", 0, 0, 1) }, Vec2(0, 0), Vec2(0, 0), 1, 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.CastCircle(new[] { PhysicsBody("target", 0, 0, 1) }, Vec2(0, 0), Vec2(1, 0), -1, 1), Is.Empty);
        Assert.That(YmirPhysicsQueries.CastCircle(new[] { PhysicsBody("bad", 0, 0, -1) }, Vec2(0, 0), Vec2(1, 0), 1, 1), Is.Empty);
    }

    private static YmirSphereQueryBody Body(string id, float x, float y, float z, float radius)
    {
        return new YmirSphereQueryBody
        {
            id = id,
            position = Vec3(x, y, z),
            radius = radius
        };
    }

    private static YmirPhysicsBody PhysicsBody(string id, float x, float y, float radius)
    {
        return new YmirPhysicsBody
        {
            id = id,
            position = Vec2(x, y),
            direction = Vec2(1, 0),
            radius = radius,
            mass = 1,
            momentOfInertia = 1
        };
    }

    private static YmirWorld World(params YmirPhysicsBody[] bodies)
    {
        return new YmirWorld
        {
            time = 0,
            bodies = bodies,
            fields = new YmirRadialField[0]
        };
    }

    private static YmirVec3 Vec3(float x, float y, float z)
    {
        return new YmirVec3 { x = x, y = y, z = z };
    }

    private static YmirVec2 Vec2(float x, float y)
    {
        return new YmirVec2 { x = x, y = y };
    }
}
