using System;
using System.Collections;
using System.IO;
using System.Linq;
using GameCult.Caching;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using float2 = CultMath.float2;

// Batch proof that ZoneRenderer draws Zone.FloatingItems and nothing else, in a play session with no scene of its own:
// the pickup prefabs are the ones ARPG.unity's ZoneRenderer names (so the wiring is read here too), a zone is loaded
// into a fresh ZoneRenderer, and the zone's own Release, step, expiry and TryPickUp are what move and remove the
// pickups. Zone.Release adds exactly one ItemPickup, placed at the body's position; stepping the zone moves it with the
// body; expiry and TryPickUp each destroy it; an item already in the zone when the renderer loads it is drawn too.
//
//   Unity -batchmode -nographics -projectPath <project> -executeMethod FloatingItemsDrawnSmoke.Run -logFile <log>
//
// Do not pass -quit: Run enters play mode (a domain reload), the check runs there, and the Editor exits itself, 0 on
// success and 1 on any failure. Destroy only takes effect in a play session, which is why this is not an edit-mode check.
[InitializeOnLoad]
public static class FloatingItemsDrawnSmoke
{
    private const string Flag = "FloatingItemsDrawnSmoke.running";
    private const string Wiring = "FloatingItemsDrawnSmoke.wiring";
    private static IEnumerator _check;
    private static int _frames;
    private static string _work;

    static FloatingItemsDrawnSmoke()
    {
        if (SessionState.GetBool(Flag, false)) EditorApplication.playModeStateChanged += OnPlayMode;
    }

    public static void Run()
    {
        try
        {
            SessionState.SetString(Wiring, ReadWiring());
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        _work = Path.Combine(Path.GetTempPath(), $"floating-items-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_work);
        _check = Check();
        EditorApplication.update += Step;
    }

    private static void Step()
    {
        try
        {
            if (++_frames > 3000) throw new InvalidOperationException("The check did not finish in 3000 frames.");
            if (_check.MoveNext()) return;
            Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Step;
        SessionState.SetBool(Flag, false);
        try { if (Directory.Exists(_work)) Directory.Delete(_work, true); } catch (Exception) { /* a temp directory */ }
        EditorApplication.Exit(code);
    }

    private static void Require(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException(what);
    }

    private static IEnumerator Check()
    {
        var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings.asset")?.GameplaySettings
            ?? throw new InvalidOperationException("No Settings.asset.");

        // The wiring: the scene's ZoneRenderer names an ItemPickup prefab in each of the four fields, and none of those
        // prefabs keeps a script that no longer exists.
        var wired = SessionState.GetString(Wiring, "").Split('|').Select(entry => entry.Split('=')).Select(pair =>
            (Field: pair[0], Pickup: pair[1] == "" ? null : AssetDatabase.LoadAssetAtPath<GameObject>(pair[1])?.GetComponent<ItemPickup>())).ToArray();
        Require(wired.Length == 4, "The wiring was not carried into the play session.");
        foreach (var (name, pickup) in wired)
        {
            Require(pickup != null, $"ZoneRenderer.{name} is not an ItemPickup in ARPG.unity.");
            Require(pickup.ScanLabel != null && pickup.ScanLabelContainer != null, $"{name}'s prefab has no scan label.");
            Require(!pickup.GetComponentsInChildren<Component>(true).Any(c => c == null), $"{name}'s prefab has a missing script.");
        }

        var shape = new Shape(5, 5);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var cargoShape = new Shape(4, 4);
        foreach (var cell in cargoShape.AllCoordinates) cargoShape[cell] = true;
        var oneCell = new Shape(1, 1);
        oneCell[new CultMath.int2(0, 0)] = true;
        var cache = AetheriaStores.Open(Path.Combine(_work, "smoke.cc"), catalogWritable: true);
        cache.Upsert(new VerseGrammar { Revision = 1 });
        cache.Upsert(new HullData { Name = "Smoke Hub", HullType = HullType.Station, Shape = shape, Durability = 1000, Mass = 1000 });
        cache.Upsert(new CargoBayData { Name = "Smoke Crate", Shape = new Shape(), InteriorShape = cargoShape, Durability = 4 });
        cache.Upsert(new GearData
        {
            Name = "Smoke Spare", Hardpoint = HardpointType.Tool, Shape = oneCell, Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000
        });
        cache.Upsert(new SimpleCommodityData { Name = "Smoke Ore", Shape = oneCell, MaxStack = 100 });
        cache.FlushAsync().Wait();
        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 8; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), null, null);
        EquippableItem Make(string name, int lot) => new EquippableItem
        {
            Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<EquippableItemData>(name)), Durability = 1, Lot = lot
        };

        // A station with a crate takes the picked-up stack. It is not admitted: the zone holds no entity for the renderer to draw.
        var hubRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Smoke Hub"));
        var taker = new OrbitalEntity(items, zone, new EquippableItem { Data = hubRef, Durability = 1000, Lot = 1 }, default, new EntitySettings());
        Require(taker.TryEquip(Make("Smoke Crate", 2), new CultMath.int2(1, 1)), "The smoke station took no crate.");

        // An item already in the zone when the renderer loads it: drawn from the load, not only from the add.
        var early = zone.Release(Make("Smoke Spare", 3), new float2(5, 6), float2.zero);

        // The renderer's side. Inactive while it is configured, so it runs no Awake or Start of the scene's own.
        var rootObject = new GameObject("Smoke Zone Root");
        var rendererObject = new GameObject("Smoke Zone Renderer");
        rendererObject.SetActive(false);
        var renderer = rendererObject.AddComponent<ZoneRenderer>();
        renderer.ZoneRoot = rootObject.transform;
        foreach (var (name, pickup) in wired)
            typeof(ZoneRenderer).GetField(name).SetValue(renderer, pickup);
        renderer.ItemManager = items;
        renderer.SectorBrushes = new GameObject("Smoke Brushes").transform;
        renderer.SlimeGravityCamera = new GameObject("Smoke Gravity Camera").AddComponent<Camera>();
        var slimeObject = new GameObject("Smoke Slime");
        slimeObject.SetActive(false); // the real Slime needs its compute shader; LoadZone only sets its radius
        renderer.SlimeRenderer = slimeObject.AddComponent<Slime>();
        renderer.LoadZone(zone);
        yield return null;

        ItemPickup[] Pickups() => rootObject.GetComponentsInChildren<ItemPickup>(true).Where(p => p != null).ToArray();
        void At(ItemPickup pickup, FloatingItem floating, string what)
        {
            var at = floating.Body.Position;
            var want = new Vector3(at.x, zone.GetHeight(at), at.y);
            Require(Vector3.Distance(pickup.transform.position, want) < 1e-3f,
                $"{what}: the pickup is at {pickup.transform.position}, the body at {want}.");
        }

        Require(Pickups().Length == 1, $"load: {Pickups().Length} pickups for one existing floating item.");
        At(Pickups()[0], early, "load");

        // Release adds exactly one pickup, at the body's position, bound to its item.
        var ore = new SimpleCommodity { Data = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<SimpleCommodityData>("Smoke Ore")), Quantity = 7 };
        var released = zone.Release(ore, new float2(3, 4), new float2(10, 0));
        yield return null;
        Require(Pickups().Length == 2, $"release: {Pickups().Length} pickups for two floating items.");
        var drawn = Pickups().Single(p => p.Item == ore);
        At(drawn, released, "release");

        // Stepping the zone moves the sim body; the pickup follows it on its next update, with nothing of its own.
        var before = drawn.transform.position;
        zone.Update(.5f);
        yield return null;
        Require(released.Body.Position.x > 3.5f, "the sim body did not drift.");
        At(drawn, released, "step");
        Require(drawn.transform.position != before, "the pickup did not move with the body.");

        // Pickup destroys exactly the picked-up item's pickup.
        Require(zone.TryPickUp(taker, released.Id), "the station refused a one-cell stack.");
        yield return null;
        yield return null;
        Require(drawn == null, "pickup: the pickup is still there.");
        Require(Pickups().Length == 1 && Pickups()[0].Item == early.Item, "pickup: the wrong pickup went.");

        // Expiry destroys the pickup of an item nobody took.
        zone.Update(settings.FloatingItemLifetime + 1f);
        yield return null;
        yield return null;
        Require(zone.FloatingItems.Count == 0, "expiry: the item did not expire.");
        Require(Pickups().Length == 0, $"expiry: {Pickups().Length} pickups left.");

        // A later release is drawn again, and clearing the renderer takes what is left.
        zone.Release(Make("Smoke Spare", 4), new float2(1, 1), float2.zero);
        yield return null;
        Require(Pickups().Length == 1, "re-release: not drawn.");
        renderer.ClearZone();
        yield return null;
        yield return null;
        Require(Pickups().Length == 0, "clear: pickups left after ClearZone.");
        Debug.Log("FLOATING_ITEMS_SMOKE ok");
    }

    // What ARPG.unity's ZoneRenderer names in each pickup field, read from the saved scene before the play session (a
    // scene cannot be opened inside one) and carried across the domain reload as the prefabs' asset paths.
    private static string ReadWiring()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ARPG.unity", OpenSceneMode.Additive);
        try
        {
            var found = UnityEngine.Object.FindObjectsByType<ZoneRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.gameObject.scene == scene).ToArray();
            Require(found.Length == 1, $"ARPG.unity has {found.Length} ZoneRenderers.");
            var wired = found[0];
            return string.Join("|", new[]
            {
                ("SimpleCommodityPickup", wired.SimpleCommodityPickup), ("CompoundCommodityPickup", wired.CompoundCommodityPickup),
                ("GearPickup", wired.GearPickup), ("WeaponPickup", wired.WeaponPickup)
            }.Select(f => f.Item1 + "=" + (f.Item2 == null ? "" : AssetDatabase.GetAssetPath(f.Item2))));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
