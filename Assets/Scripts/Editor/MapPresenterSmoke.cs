using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using UnityEditor;
using UnityEngine;
using CultMath;
using CultMath.UnityBridge;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using float4 = CultMath.float4;

// Batch proof of MapPresenter's contract on a real Zone, without a scene: a sun with a very deep well, a belt, and ships
// registered through EntityInstance.SetEntity as the game does. Planet icons draw at the map plane inside both map
// cameras' clip range however deep the well, a rolled and pitched hull's icon turns by yaw alone, every kind registers one
// draw and leaves no icon child behind, a belt is one instanced draw of every asteroid, and an entity's fade reaches its
// icon the frame it is set. Not reached: prefab, mod ship, station and turret instances (they register through the same
// call), and the rendered pixels (a GPU capture).
//
//   Unity -batchmode -projectPath <project> -executeMethod MapPresenterSmoke.Run -logFile <log>
//
// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
public static class MapPresenterSmoke
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const int Asteroids = 24;

    public static void Run()
    {
        var work = Path.Combine(Path.GetTempPath(), $"map-presenter-smoke-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(work);
            Check(work);
            Debug.Log("[MapPresenterSmoke] ok");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException(what);
    }

    private static void Awaken(Component component) => component.GetType().GetMethod("Awake", Any)?.Invoke(component, null);

    private static MeshRenderer NewIcon(Transform parent)
    {
        var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
        icon.layer = LayerMask.NameToLayer("Minimap");
        icon.transform.SetParent(parent, false);
        var renderer = icon.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(1, 1, 1, .8f) };
        return renderer;
    }

    private static void Check(string work)
    {
        var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset")
            ?? throw new InvalidOperationException("No Settings.asset.");
        var planetSettings = new PlanetSettings
        {
            BodyRadius = new ExponentialCurve(),
            WellStrength = new ExponentialCurve { Multiplier = 1f, Exponent = 1f },
            RocheLimit = new ExponentialCurve { Constant = 2f },
            AsteroidSize = new ExponentialLerp { Minimum = 2f, Maximum = 9f, Exponent = 1.7f },
            AsteroidHitpoints = new ExponentialLerp { Minimum = 10f, Maximum = 300f, Exponent = 2f },
            AsteroidRespawnTime = new ExponentialLerp { Minimum = 5f, Maximum = 60f, Exponent = 1f }
        };
        var cache = AetheriaStores.Open(Path.Combine(work, "c.cc"), Path.Combine(work, "r.cc"), catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        var shape = new Shape(5, 4);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        cache.Upsert(new HullData { Name = "Smoke Hull", HullType = HullType.Ship, Shape = shape, Durability = 1000, Mass = 1000, Armor = 0 });
        var sunOrbit = cache.Upsert(new OrbitData()).Key;
        var sunKey = cache.Upsert(new PlanetData { Name = "Sun", Orbit = new CultRecordRef<OrbitData>(sunOrbit), Mass = 1e6f }).Key;
        var rootOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(sunOrbit), Distance = 4000f, Phase = .17f }).Key;
        var beltOrbit = cache.Upsert(new OrbitData { Parent = new CultRecordRef<OrbitData>(rootOrbit), Distance = 900f, Phase = .42f }).Key;
        var asteroids = Enumerable.Range(0, Asteroids).Select(i => new Asteroid
            { Distance = 120f + i * 11.3f, Phase = frac(i * .618f), Size = frac(i * .37f + .1f), RotationSpeed = .15f + i * .037f }).ToArray();
        var beltKey = cache.Upsert(new AsteroidBeltData { Orbit = new CultRecordRef<OrbitData>(beltOrbit), Asteroids = asteroids }).Key;
        var pack = new ZonePack
        {
            Orbits = { new CultRecordRef<OrbitData>(sunOrbit), new CultRecordRef<OrbitData>(rootOrbit), new CultRecordRef<OrbitData>(beltOrbit) },
            Planets = { new CultRecordRef<BodyData>(sunKey), new CultRecordRef<BodyData>(beltKey) },
            Radius = 5000f, Mass = 10000f, Time = 0
        };
        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 8; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings.GameplaySettings, _ => { });
        var zone = new Zone(items, planetSettings, pack, new GalaxyZone { Name = "Smoke", Owner = null }, null);
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Smoke Hull"));
        var ship = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 }, new EntitySettings());
        var other = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 2 }, new EntitySettings());
        ship.Position = float3(300, 0, 200);
        other.Position = float3(-300, 0, 100);
        zone.Admit(ship, piloted: false);
        zone.Admit(other, piloted: false);

        // A hull rolled 30 and pitched 20 degrees, heading 40 degrees from +z: Direction is the heading, Rotation the full pose.
        var heading = Quaternion.Euler(0, 40, 0);
        var forward = heading * Quaternion.Euler(-20, 0, 0) * Vector3.forward;
        var pose = Quaternion.LookRotation(forward, Quaternion.AngleAxis(30, forward) * Vector3.up);
        ship.Direction = float2(Mathf.Sin(40 * Mathf.Deg2Rad), Mathf.Cos(40 * Mathf.Deg2Rad));
        typeof(Ship).GetProperty("Rotation", Any).GetSetMethod(true).Invoke(ship, new object[] { new CultMath.quaternion(pose.x, pose.y, pose.z, pose.w) });

        var map = new MapPresenter(new Material[0], AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/MapAsteroid.mat"), 4, 4, 3f);
        var roots = new GameObject("Smoke Roots").transform;
        EntityInstance Bound(Entity entity)
        {
            var body = new GameObject(entity.Name);
            body.transform.SetParent(roots);
            var instance = body.AddComponent<EntityInstance>();
            Awaken(instance);
            instance.HullColliders = new HullCollider[0];
            instance.RadiatorHardpoints = new RadiatorHardpoint[0];
            instance.ThrusterHardpoints = new ThrusterHardpoint[0];
            instance.WeaponHardpoints = new WeaponHardpoint[0];
            instance.ArticulationPoints = new ArticulationPoint[0];
            instance.MapIcon = NewIcon(body.transform);
            var zoneRenderer = new GameObject("Smoke Zone Renderer").AddComponent<ZoneRenderer>();
            zoneRenderer.Settings = settings;
            zoneRenderer.MapGravityMaterials = new Material[0];
            Awaken(zoneRenderer);
            typeof(ZoneRenderer).GetProperty("Map").GetSetMethod(true).Invoke(zoneRenderer, new object[] { map });
            instance.SetEntity(zoneRenderer, entity);
            return instance;
        }
        var shipInstance = Bound(ship);
        var otherInstance = Bound(other);
        var sunRoot = new GameObject("Smoke Sun").transform;
        sunRoot.SetParent(roots);
        map.RegisterPlanet(sunKey, NewIcon(sunRoot), sunRoot);
        var belt = new float4[Asteroids];
        map.RegisterBelt(beltKey, belt);
        zone.EvaluateBelt(beltKey, belt, DrawAhead.Time(zone, 0));
        map.BeltsOnMinimap = true;
        map.SetFade(ship, 1f);
        map.SetFade(other, 1f);
        map.SetMinimapScale(2f);
        map.Draw(zone, 0);
        Checks(zone, map, ship, other, shipInstance, otherInstance, sunKey, beltKey, belt, roots);
    }

    private static void Checks(Zone zone, MapPresenter map, Ship ship, Ship other, EntityInstance shipInstance,
        EntityInstance otherInstance, CultRecordKey sunKey, CultRecordKey beltKey, float4[] belt, Transform roots)
    {
        // Deep planet: the sun's field is deeper than 2000 below the plane, and its icon sits on the map plane inside both
        // map cameras' clip range (minimap at y 10, map screen at y 1; near 0.3, far 1000, looking down).
        var well = zone.GetHeight(DrawAhead.Orbit(zone, zone.PlanetInstances[sunKey].BodyData.Orbit.Key, 0), 0);
        Require(well < -2000f, $"The fixture sun's well is {well}, not below -2000.");
        Require(map.TryDrawn(sunKey, out var sunIcon), "The sun registered no icon.");
        var y = sunIcon.GetPosition().y;
        Require(y < 1f - .3f && y > 10f - 1000f && Mathf.Abs(y - MapPresenter.PlaneY) < .5f, $"The sun's icon is drawn at y {y}, not on the map plane.");

        // Tilted hull: the icon turns by yaw 40 about up, times its authored rotation (identity here), with no roll or pitch.
        Require(map.TryDrawn(ship, out var shipIcon, out _), "The ship registered no icon.");
        var want = Quaternion.Euler(0, 40, 0);
        Require(Quaternion.Angle(shipIcon.rotation, want) < .1f, $"The icon is rotated {shipIcon.rotation.eulerAngles}, not yaw 40 alone.");
        Require(Vector3.Angle(shipIcon.rotation * Vector3.up, Vector3.up) < .1f, "The icon's up axis tilted with the hull.");
        Require(Mathf.Abs(shipIcon.GetPosition().y - (MapPresenter.PlaneY + MapPresenter.ShipOffset)) < 1e-4f, "The ship's icon left the map plane.");

        // Every kind registers once: three icons drew, and no icon child remains under any registered transform.
        Require(map.IconCount == 3 && map.IconDraws == 3, $"{map.IconCount} icons, {map.IconDraws} drawn; expected 3 and 3.");
        Require(roots.GetComponentsInChildren<MeshRenderer>(true).Length == 0, "An icon child remains under an entity or planet transform.");

        // A belt is one instanced draw of every asteroid, and its buffer holds this frame's floats.
        Require(map.BeltDraws == 1 && map.LastBeltInstances == belt.Length, $"{map.BeltDraws} belt draws of {map.LastBeltInstances} instances; expected 1 of {belt.Length}.");
        var read = new float4[belt.Length];
        map.ReadBelt(beltKey, read);
        for (var i = 0; i < belt.Length; i++)
            Require(read[i].x == belt[i].x && read[i].y == belt[i].y && read[i].z == belt[i].z && read[i].w == belt[i].w, $"Belt buffer differs from the frame's floats at {i}.");

        // The setting still gates belts on the minimap, and the map screen always shows them.
        map.BeltsOnMinimap = false;
        map.Draw(zone, 0);
        Require(map.BeltDraws == 0, "A belt drew on the minimap with the setting off.");
        map.OpenMapScreen(true);
        map.Draw(zone, 0);
        Require(map.BeltDraws == 1, "The map screen did not draw the belt with the setting off.");
        map.OpenMapScreen(false);
        map.BeltsOnMinimap = true;

        // Fade: half faded reaches the icon's colour alpha on the next draw; faded out draws nothing.
        map.SetFade(ship, .5f);
        map.Draw(zone, 0);
        Require(map.TryDrawn(ship, out _, out var alpha) && Mathf.Abs(alpha - .4f) < 1e-3f, $"Icon alpha {alpha}, expected 0.8 * 0.5.");
        map.SetFade(ship, 0f);
        map.Draw(zone, 0);
        Require(map.IconDraws == 2, $"{map.IconDraws} icons drew with one fully faded out; expected 2.");
    }
}
