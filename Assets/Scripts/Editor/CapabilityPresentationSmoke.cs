using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using UnityEditor;
using UnityEngine;
using CultMath;
using static CultMath.math;

// Batch proof of the capability presenters' wiring, without a scene: a zone ship is bound to an EntityInstance through the
// game's own SetEntity, and impacts and grabs are published on the ship's Capabilities exactly as the simulation does. A hull absorb spawns
// one hull effect under the instance and, for the player's ship at ScreenShake 1, raises one camera impulse; ScreenShake 0
// or another ship raises none; a shield absorb spawns no hull effect. The shield ripple itself (ShieldManager.ShowHit
// needs a shield material and a rig) is looked at in the editor, not here. Grabs: a Tractor Beam prefab under the instance
// is bound by the same SetEntity and presents a grab from events alone (emission at Power and the aim at the held point
// from Started to Completed or Cancelled, nothing otherwise, nothing for another entity's grab).
//
//   Unity -batchmode -nographics -projectPath <project> -executeMethod CapabilityPresentationSmoke.Run -logFile <log>
//
// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
public static class CapabilityPresentationSmoke
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Run()
    {
        var work = Path.Combine(Path.GetTempPath(), $"capability-presentation-smoke-{Guid.NewGuid():N}");
        GameObject[] objects = null;
        try
        {
            Directory.CreateDirectory(work);
            objects = Check(work);
            Clean(objects, work);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Clean(objects, work);
            EditorApplication.Exit(1);
        }
    }

    // Edit mode runs no Awake or Start; the smoke runs the ones the game's scene would have.
    private static void Awaken(Component component) => component.GetType().GetMethod("Awake", Any)?.Invoke(component, null);

    private static GameObject[] Check(string work)
    {
        var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset")?.GameplaySettings
            ?? throw new InvalidOperationException("No Settings.asset.");

        var shape = new Shape(5, 4);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hullData = new HullData
        {
            Name = "Smoke Hull", HullType = HullType.Ship, Shape = shape, Durability = 1000, Mass = 1000, Armor = 0
        };
        var cache = AetheriaStores.Open(Path.Combine(work, "smoke.cc"), catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        cache.Upsert(hullData);
        cache.FlushAsync().Wait();
        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 8; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(1u, cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Smoke", Owner = null }, null);
        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Smoke Hull"));
        var ship = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 }, new EntitySettings());
        var other = new Ship(items, zone, new EquippableItem { Data = hullRef, Durability = 1000, Lot = 2 }, new EntitySettings());
        zone.Admit(ship, piloted: false);
        zone.Admit(other, piloted: false);

        // The scene's side: the tuning object with a pooled effect, and the game manager that names the player's ship.
        var feedbackObject = new GameObject("Smoke Impact Feedback");
        var feedback = feedbackObject.AddComponent<ImpactFeedback>();
        Awaken(feedback);
        var effectObject = new GameObject("Smoke Hull Effect");
        feedback.HullEffect = effectObject.AddComponent<Prototype>();
        var managerObject = new GameObject("Smoke Game Manager");
        var manager = managerObject.AddComponent<ActionGameManager>();
        typeof(ActionGameManager).GetProperty("Instance", Any).SetValue(null, manager);
        manager.CurrentEntity = ship;

        var beamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Content/Prefabs/Tractor Beam.prefab")
            ?? throw new InvalidOperationException("No Tractor Beam prefab.");
        // An entity's instance as the game builds it: the beam is a child before SetEntity on both ship paths, so the
        // binder finds it as the game does.
        // The scene's map: an entity registers its icon with the ZoneRenderer's presenter, so each instance carries a quad.
        var mapRenderer = new GameObject("Smoke Zone Renderer").AddComponent<ZoneRenderer>();
        mapRenderer.Settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset");
        mapRenderer.MapGravityMaterials = new Material[0];
        Awaken(mapRenderer);
        (GameObject Object, EntityInstance Instance, TractorBeam Beam) Bound(string name, Entity entity)
        {
            var go = new GameObject(name);
            var bound = go.AddComponent<EntityInstance>();
            Awaken(bound);
            bound.HullColliders = new HullCollider[0];
            bound.RadiatorHardpoints = new RadiatorHardpoint[0];
            bound.ThrusterHardpoints = new ThrusterHardpoint[0];
            bound.WeaponHardpoints = new WeaponHardpoint[0];
            bound.ArticulationPoints = new ArticulationPoint[0];
            var tractor = ((GameObject)PrefabUtility.InstantiatePrefab(beamPrefab, go.transform)).GetComponent<TractorBeam>();
            tractor.Power = .8f;
            tractor.Direction = Vector3.forward;
            var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            icon.transform.SetParent(go.transform);
            bound.MapIcon = icon.GetComponent<MeshRenderer>();
            bound.MapIcon.sharedMaterial = new Material(Shader.Find("Unlit/Color"));
            bound.SetEntity(mapRenderer, entity);
            return (go, bound, tractor);
        }
        var (instanceObject, instance, beam) = Bound("Smoke Entity Instance", ship);
        var (otherObject, _, otherBeam) = Bound("Smoke Other Instance", other);

        int Effects() => instance.GetComponentsInChildren<Prototype>(true).Count(p => p != null && !p.isOriginalPrototype);
        void Expect(string what, int effects, int kicks)
        {
            if (Effects() != effects || feedback.KicksRaised != kicks)
                throw new InvalidOperationException($"{what}: {Effects()} hull effects (want {effects}), {feedback.KicksRaised} impulses (want {kicks}).");
        }
        AbsorbEvent Hit(AbsorbLayer layer) => new AbsorbEvent(float3(1f, 0f, 2f), float3(0f, 0f, 1f), 100f, DamageType.Kinetic, layer);

        ActionGameManager.PlayerSettings.GraphicsSettings.ScreenShake = 1f;
        ship.Capabilities.PublishAbsorb(Hit(AbsorbLayer.Hull));
        Expect("player's hull hit", 1, 1);
        var effect = instance.GetComponentsInChildren<Prototype>(true).Single(p => !p.isOriginalPrototype);
        var drawn = DrawAhead.Carried(ship, float3(1f, 0f, 2f), ActionGameManager.Clock.Lead);
        if (abs(effect.transform.position.x - drawn.x) > 1e-3f || abs(effect.transform.position.z - drawn.z) > 1e-3f)
            throw new InvalidOperationException("The hull effect is not at the drawn impact point.");

        ActionGameManager.PlayerSettings.GraphicsSettings.ScreenShake = 0f;
        ship.Capabilities.PublishAbsorb(Hit(AbsorbLayer.Hull));
        Expect("hull hit at ScreenShake 0", 2, 1);

        ActionGameManager.PlayerSettings.GraphicsSettings.ScreenShake = 1f;
        manager.CurrentEntity = other;
        ship.Capabilities.PublishAbsorb(Hit(AbsorbLayer.Hull));
        // An untracked ship is not drawn at (ActionGameManager.DrawsAt, since contact-hud): no hull effect, no impulse.
        Expect("hull hit on an untracked ship", 2, 1);

        // Once the player's instruments track it, the same hit is drawn.
        other.VisibleEntities.Add(ship);
        ship.Capabilities.PublishAbsorb(Hit(AbsorbLayer.Hull));
        Expect("hull hit on a tracked ship", 3, 1);
        other.VisibleEntities.Remove(ship);

        manager.CurrentEntity = ship;
        ship.Capabilities.PublishAbsorb(Hit(AbsorbLayer.Shield));
        Expect("player's shield hit", 3, 2);

        // Grabs: the beam presents the simulation's events and nothing else.
        float Emission() => beam.ParticleSystem.emission.rateOverTimeMultiplier;
        void ExpectBeam(string what, float emission, bool towardHeld)
        {
            beam.Update();
            var forward = beam.transform.forward;
            var aimed = towardHeld ? forward.x > .999f : forward.z > .999f;
            if (abs(Emission() - emission) > 1e-4f || !aimed)
                throw new InvalidOperationException($"{what}: emission {Emission()} (want {emission}), forward {forward} (want {(towardHeld ? "+x" : "+z")}).");
        }
        GrabEvent Grab(GrabPhase phase, float3 at) => new GrabEvent(new FloatingBodyId(7), phase, at, 1f);

        ExpectBeam("idle beam", 0f, false);
        ship.Capabilities.PublishGrab(Grab(GrabPhase.Started, float3(20f, 0f, 0f)));
        ExpectBeam("grab started", .8f, true);
        foreach (var phase in new[] { GrabPhase.Extend, GrabPhase.Envelop, GrabPhase.Pull })
        {
            ship.Capabilities.PublishGrab(Grab(phase, float3(20f, 0f, 0f)));
            ExpectBeam($"grab {phase}", .8f, true);
        }
        ship.Capabilities.PublishGrab(Grab(GrabPhase.Completed, float3(20f, 0f, 0f)));
        ExpectBeam("grab completed", 0f, false);

        // A grab cancelled after its item vanished carries no position: the beam goes dark and never turns toward it.
        ship.Capabilities.PublishGrab(Grab(GrabPhase.Started, float3(20f, 0f, 0f)));
        ExpectBeam("second grab started", .8f, true);
        ship.Capabilities.PublishGrab(Grab(GrabPhase.Cancelled, float3(0f, 0f, 0f)));
        ExpectBeam("grab cancelled", 0f, false);

        // Aim is on the ground plane: a beam above the held point does not pitch toward it.
        var beamHome = beam.transform.position;
        beam.transform.position = beamHome + new Vector3(0f, 10f, 0f);
        ship.Capabilities.PublishGrab(Grab(GrabPhase.Started, float3(20f, 7f, 0f)));
        ExpectBeam("grab from above", .8f, true);
        ship.Capabilities.PublishGrab(Grab(GrabPhase.Completed, float3(20f, 7f, 0f)));
        beam.transform.position = beamHome;

        // A grab held at the beam's own position has no direction to take: the beam keeps the aim it had.
        beam.Direction = Vector3.right;
        var atBeam = float3(beam.transform.position.x, 0f, beam.transform.position.z);
        ship.Capabilities.PublishGrab(Grab(GrabPhase.Started, atBeam));
        ExpectBeam("grab at the beam", .8f, true);
        ship.Capabilities.PublishGrab(Grab(GrabPhase.Completed, atBeam));
        beam.Direction = Vector3.forward;
        ExpectBeam("after the grab at the beam", 0f, false);

        // The beam belongs to the ship's entity: another entity's grab, with its own beam bound, moves only that beam.
        other.Capabilities.PublishGrab(Grab(GrabPhase.Started, float3(20f, 0f, 0f)));
        ExpectBeam("another entity's grab", 0f, false);
        otherBeam.Update();
        if (abs(otherBeam.ParticleSystem.emission.rateOverTimeMultiplier - .8f) > 1e-4f)
            throw new InvalidOperationException("The other entity's own beam did not present its grab.");

        Debug.Log("CAPABILITY_PRESENTATION_SMOKE ok effects=3 impulses=2 grabs=ok");
        return new[] { feedbackObject, effectObject, managerObject, instanceObject, otherObject };
    }

    private static void Clean(GameObject[] objects, string work)
    {
        if (objects != null)
            foreach (var go in objects)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        try { if (Directory.Exists(work)) Directory.Delete(work, true); }
        catch (Exception error) { Debug.LogWarning($"Impact presenter smoke left {work}: {error.Message}"); }
    }
}
