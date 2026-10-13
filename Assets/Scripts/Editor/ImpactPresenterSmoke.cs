using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using UnityEditor;
using UnityEngine;
using static CultMath.math;

// Batch proof of ImpactPresenter's wiring, without a scene: a zone ship is bound to an EntityInstance through the game's
// own SetEntity, and impacts are published on the ship's Capabilities exactly as FireControl does. A hull absorb spawns
// one hull effect under the instance and, for the player's ship at ScreenShake 1, raises one camera impulse; ScreenShake 0
// or another ship raises none; a shield absorb spawns no hull effect. The shield ripple itself (ShieldManager.ShowHit
// needs a shield material and a rig) is looked at in the editor, not here.
//
//   Unity -batchmode -nographics -projectPath <project> -executeMethod ImpactPresenterSmoke.Run -logFile <log>
//
// Do not pass -quit: Run exits the Editor itself, 0 on success and 1 on any failure.
public static class ImpactPresenterSmoke
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Run()
    {
        var work = Path.Combine(Path.GetTempPath(), $"impact-presenter-smoke-{Guid.NewGuid():N}");
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
        var items = new ItemManager(cache, ledger, settings, _ => { });
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

        var instanceObject = new GameObject("Smoke Entity Instance");
        var instance = instanceObject.AddComponent<EntityInstance>();
        Awaken(instance);
        instance.HullColliders = new HullCollider[0];
        instance.RadiatorHardpoints = new RadiatorHardpoint[0];
        instance.ThrusterHardpoints = new ThrusterHardpoint[0];
        instance.WeaponHardpoints = new WeaponHardpoint[0];
        instance.ArticulationPoints = new ArticulationPoint[0];
        instance.SetEntity(null, ship);

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
        Expect("hull hit on another ship", 3, 1);

        manager.CurrentEntity = ship;
        ship.Capabilities.PublishAbsorb(Hit(AbsorbLayer.Shield));
        Expect("player's shield hit", 3, 2);

        Debug.Log("IMPACT_PRESENTER_SMOKE ok effects=3 impulses=2");
        return new[] { feedbackObject, effectObject, managerObject, instanceObject };
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
