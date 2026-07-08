#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;

public sealed class YmirUnityRuntimeProofTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        ResetBridgeSingleton();
        Time.captureDeltaTime = 0.1f;
    }

    [TearDown]
    public void TearDown()
    {
        Time.captureDeltaTime = 0;
        ResetBridgeSingleton();
        for (var i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] != null)
                UnityEngine.Object.DestroyImmediate(_objects[i]);
        }

        _objects.Clear();
    }

    [Test]
    public void DisabledUnityCollidersDoNotPreventYmirProjectileHit()
    {
        var bridge = CreateBridge(BodySource(TargetBody(7, 1.0f, 0, 0.5f)));
        var target = CreateTarget(7, disableCollider: true);
        var projectile = CreateProjectile(target, disableCollider: true);
        projectile.Velocity = new Vector3(10, 0, 0);

        Assert.That(bridge.TryStepProjectile(projectile, 0.1f, out var step), Is.True);
        Assert.That(step.Position.x, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(step.HasHit, Is.True);
        Assert.That(step.Hit.Hull, Is.SameAs(target.Hull));
        Assert.That(projectile.GetComponent<Collider>().enabled, Is.False);
        Assert.That(target.Hull.GetComponent<Collider>().enabled, Is.False);
    }

    [Test]
    public void CollisionCallbackObserversDoNotChangeYmirHitSequence()
    {
        var body = TargetBody(7, 1.0f, 0, 0.5f);
        var target = CreateTarget(7, disableCollider: false);
        var projectile = CreateProjectile(target, disableCollider: false);
        projectile.Velocity = new Vector3(10, 0, 0);

        var observer = projectile.gameObject.AddComponent<CollisionObserverOnly>();
        var withObserver = StepHitBodyIds(BodySource(body), projectile);
        var observerCallbackCount = observer.CallbackCount;
        UnityEngine.Object.DestroyImmediate(observer);
        var withoutObserver = StepHitBodyIds(BodySource(body), projectile);

        Assert.That(observerCallbackCount, Is.EqualTo(0));
        CollectionAssert.AreEqual(withObserver, withoutObserver);
        CollectionAssert.AreEqual(new[] { "aetheria.projectile", "aetheria.daemon.entity.7" }, withObserver);
    }

    [Test]
    public void ProjectileUpdateCommitsVisibleTransformFromYmirSnapshot()
    {
        var bridge = CreateBridge(BodySource(TargetBody(7, 5.0f, 0, 0.25f)));
        var target = CreateTarget(7, disableCollider: true);
        var projectile = CreateProjectile(target, disableCollider: true);
        projectile.Velocity = new Vector3(10, 0, 0);

        var deltaTime = Time.deltaTime;
        Assert.That(bridge.TryStepProjectile(projectile, deltaTime, out var expected), Is.True);

        projectile.transform.position = Vector3.zero;
        projectile.Velocity = new Vector3(10, 0, 0);
        InvokeProjectileUpdate(projectile);

        Assert.That(projectile.transform.position.x, Is.EqualTo(expected.Position.x).Within(0.02f));
        Assert.That(projectile.transform.position.z, Is.EqualTo(expected.Position.z).Within(0.02f));
        Assert.That(projectile.Velocity.x, Is.EqualTo(expected.Velocity.x).Within(0.0001f));
    }

    [Test]
    public void ProjectileUpdateFailsClosedWhenYmirBodiesAreUnavailable()
    {
        CreateBridge(BodySource());
        var target = CreateTarget(7, disableCollider: true);
        var projectile = CreateProjectile(target, disableCollider: true);
        projectile.Velocity = new Vector3(10, 0, 0);
        projectile.Trail = projectile.gameObject.AddComponent<TrailRenderer>();
        projectile.gameObject.AddComponent<Prototype>();

        InvokeProjectileUpdate(projectile);

        Assert.That(ReadAlive(projectile), Is.False);
    }

    [Test]
    public void ReplayingCapturedYmirRequestKeepsContactOrderStable()
    {
        var request = new YmirStepRequest
        {
            deltaTime = 0.1f,
            world = new YmirWorld
            {
                time = 0,
                fields = Array.Empty<YmirRadialField>(),
                bodies = new[]
                {
                    ProjectileBody(0, 0, 10, 0),
                    TargetBody(9, 1.0f, 0, 0.5f),
                    TargetBody(7, 1.0f, 0, 0.5f)
                }
            }
        };

        var first = ContactPairs(YmirPhysicsQueries.Step(request));
        var second = ContactPairs(YmirPhysicsQueries.Step(request));

        CollectionAssert.AreEqual(first, second);
        CollectionAssert.AreEqual(
            new[]
            {
                "aetheria.projectile->aetheria.daemon.entity.9",
                "aetheria.projectile->aetheria.daemon.entity.7"
            },
            first);
    }

    private static string[] StepHitBodyIds(StaticDaemonBodySource source, Projectile projectile)
    {
        var bridge = CreateBridge(source);
        Assert.That(bridge.TryStepProjectile(projectile, 0.1f, out var step), Is.True);
        Assert.That(step.HasHit, Is.True);

        var result = YmirPhysicsQueries.Step(new YmirStepRequest
        {
            deltaTime = 0.1f,
            world = new YmirWorld
            {
                time = 0,
                fields = Array.Empty<YmirRadialField>(),
                bodies = new[] { ProjectileBody(0, 0, 10, 0), source.Bodies[0] }
            }
        });

        return result.contacts.Length == 0
            ? Array.Empty<string>()
            : new[] { result.contacts[0].bodyA, result.contacts[0].bodyB };
    }

    private static string[] ContactPairs(YmirStepResult result)
    {
        var contacts = result.contacts ?? Array.Empty<YmirContactEvent>();
        var pairs = new string[contacts.Length];
        for (var i = 0; i < contacts.Length; i++)
            pairs[i] = contacts[i].bodyA + "->" + contacts[i].bodyB;
        return pairs;
    }

    private static AetheriaYmirPhysicsBridge CreateBridge(IYmirDaemonBodySource source)
    {
        ResetBridgeSingleton();
        var gameObject = new GameObject("test-yimir-bridge");
        var bridge = gameObject.AddComponent<AetheriaYmirPhysicsBridge>();
        bridge.EnableProjectileCutover = true;
        bridge.ProjectileRadius = 0.1f;
        bridge.ProjectileMass = 1.0f;
        bridge.DaemonBodySource = source;
        _activeBridgeObject = gameObject;
        return bridge;
    }

    private static GameObject _activeBridgeObject;

    private TargetFixture CreateTarget(int daemonEntityIndex, bool disableCollider)
    {
        var targetObject = Track(new GameObject("target"));
        var hullObject = Track(new GameObject("target-hull"));
        hullObject.transform.SetParent(targetObject.transform);
        var collider = hullObject.AddComponent<SphereCollider>();
        collider.radius = 0.5f;
        collider.enabled = !disableCollider;

        var targetEntity = TestEntity.Create(daemonEntityIndex);
        var hull = hullObject.AddComponent<HullCollider>();
        hull.Entity = targetEntity;

        var instance = targetObject.AddComponent<EntityInstance>();
        instance.HullColliders = new[] { hull };
        SetEntityInstanceEntity(instance, targetEntity);
        return new TargetFixture(instance, hull);
    }

    private Projectile CreateProjectile(TargetFixture target, bool disableCollider)
    {
        var projectileObject = Track(new GameObject("projectile"));
        var collider = projectileObject.AddComponent<SphereCollider>();
        collider.radius = 0.1f;
        collider.enabled = !disableCollider;

        var projectile = projectileObject.AddComponent<Projectile>();
        projectile.SourceEntity = TestEntity.Create(1);
        projectile.TargetInstance = target.Instance;
        projectile.StartPosition = Vector3.zero;
        projectile.Range = 100;
        projectile.Gravity = 0;
        projectile.Drag = 0;
        SetAlive(projectile, true);
        return projectile;
    }

    private GameObject Track(GameObject gameObject)
    {
        _objects.Add(gameObject);
        return gameObject;
    }

    private static StaticDaemonBodySource BodySource(params YmirPhysicsBody[] bodies)
    {
        return new StaticDaemonBodySource(bodies);
    }

    private static YmirPhysicsBody ProjectileBody(float x, float z, float velocityX, float velocityZ)
    {
        return new YmirPhysicsBody
        {
            id = "aetheria.projectile",
            position = Vec2(x, z),
            velocity = Vec2(velocityX, velocityZ),
            direction = Vec2(1, 0),
            radius = 0.1f,
            mass = 1,
            momentOfInertia = 1,
            restitution = 0
        };
    }

    private static YmirPhysicsBody TargetBody(int daemonEntityIndex, float x, float z, float radius)
    {
        return new YmirPhysicsBody
        {
            id = "aetheria.daemon.entity." + daemonEntityIndex,
            position = Vec2(x, z),
            velocity = Vec2(0, 0),
            direction = Vec2(0, 1),
            radius = radius,
            mass = 1,
            momentOfInertia = 1,
            isStatic = true,
            restitution = 0
        };
    }

    private static YmirVec2 Vec2(float x, float y)
    {
        return new YmirVec2 { x = x, y = y };
    }

    private static void InvokeProjectileUpdate(Projectile projectile)
    {
        typeof(Projectile)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(projectile, Array.Empty<object>());
    }

    private static bool ReadAlive(Projectile projectile)
    {
        return (bool)typeof(Projectile)
            .GetField("_alive", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(projectile);
    }

    private static void SetAlive(Projectile projectile, bool alive)
    {
        typeof(Projectile)
            .GetField("_alive", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(projectile, alive);
    }

    private static void SetEntityInstanceEntity(EntityInstance instance, Entity entity)
    {
        typeof(EntityInstance)
            .GetProperty("Entity", BindingFlags.Instance | BindingFlags.Public)
            .GetSetMethod(true)
            .Invoke(instance, new object[] { entity });
    }

    private static void ResetBridgeSingleton()
    {
        if (_activeBridgeObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_activeBridgeObject);
            _activeBridgeObject = null;
        }

        typeof(AetheriaYmirPhysicsBridge)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, null);
    }

    private sealed class StaticDaemonBodySource : IYmirDaemonBodySource
    {
        public StaticDaemonBodySource(params YmirPhysicsBody[] bodies)
        {
            Bodies = bodies ?? Array.Empty<YmirPhysicsBody>();
        }

        public YmirPhysicsBody[] Bodies { get; }

        public bool TryAppendDaemonBodies(
            Entity sourceEntity,
            int? onlyDaemonEntityIndex,
            List<YmirPhysicsBody> bodies)
        {
            for (var i = 0; i < Bodies.Length; i++)
            {
                var body = Bodies[i];
                if (!TryParseEntityIndex(body.id, out var daemonEntityIndex))
                    continue;
                if (sourceEntity != null && sourceEntity.DaemonEntityIndex == daemonEntityIndex)
                    continue;
                if (onlyDaemonEntityIndex.HasValue && onlyDaemonEntityIndex.Value != daemonEntityIndex)
                    continue;

                bodies.Add(Clone(body));
            }

            return bodies.Count > 0;
        }

        private static bool TryParseEntityIndex(string bodyId, out int daemonEntityIndex)
        {
            const string prefix = "aetheria.daemon.entity.";
            daemonEntityIndex = -1;
            return !string.IsNullOrWhiteSpace(bodyId) &&
                   bodyId.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(bodyId.Substring(prefix.Length), out daemonEntityIndex);
        }

        private static YmirPhysicsBody Clone(YmirPhysicsBody body)
        {
            return new YmirPhysicsBody
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
    }

    private sealed class TestEntity : Entity
    {
        private TestEntity()
            : base(null, null, null, null)
        {
        }

        public static TestEntity Create(int daemonEntityIndex)
        {
            var entity = (TestEntity)FormatterServices.GetUninitializedObject(typeof(TestEntity));
            entity.DaemonEntityIndex = daemonEntityIndex;
            entity.Name = "test-entity-" + daemonEntityIndex;
            return entity;
        }
    }

    private sealed class CollisionObserverOnly : MonoBehaviour
    {
        public int CallbackCount { get; private set; }

        private void OnCollisionEnter(Collision collision)
        {
            CallbackCount++;
        }
    }

    private readonly struct TargetFixture
    {
        public TargetFixture(EntityInstance instance, HullCollider hull)
        {
            Instance = instance;
            Hull = hull;
        }

        public EntityInstance Instance { get; }
        public HullCollider Hull { get; }
    }
}
#endif
