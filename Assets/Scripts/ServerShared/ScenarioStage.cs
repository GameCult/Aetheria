using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The run half of a scenario's vocabulary (docs/scenarios-cut.md, R.4). A scenario's Stage builds its fits and places
// them here; nothing joins the arena until Stage returns with no failure, and then RunStart admits it all at once.
// Every verb names what it could not do instead of throwing, so one bad name in a script lists every bad name.
// Designs, hulls and products are named as the catalog names them. Only RunStart builds a stage.
public sealed class ScenarioStage
{
    private readonly ItemManager _items;
    private readonly Func<CraftedItemData, FactionProductData> _choose;
    private readonly List<string> _failures = new List<string>();
    private readonly List<Placement> _placed = new List<Placement>();
    private int _placements;

    internal ScenarioStage(ItemManager items, Zone arena, string startingHull, TutorialGenerationSettings tutorialGenerationSettings)
    {
        _items = items;
        Arena = arena;
        StartingHull = startingHull;
        TutorialGenerationSettings = tutorialGenerationSettings;
        Credits = items.GameplaySettings.StartingCredits;
        // A design named without a product is chosen by the generator's ChooseProduct. The stage outfits with no faction,
        // so every maker is reachable and the run's seed draws.
        _choose = new LoadoutGenerator(ref items.Random, items, arena.Galaxy, arena.GalaxyZone, null, 2).ChooseProduct;
    }

    public Galaxy Galaxy => Arena.Galaxy;
    public Zone Arena { get; }
    public string StartingHull { get; }
    public TutorialGenerationSettings TutorialGenerationSettings { get; }

    // What the run starts with: the gameplay settings' starting credits unless the scenario sets another amount.
    public int Credits { get; set; }

    internal IReadOnlyList<string> Failures => _failures;
    internal bool PlayerPlaced { get; private set; }
    internal Ship PlayerShip { get; private set; }
    internal IReadOnlyList<Placement> Placed => _placed;

    internal sealed class Placement
    {
        public Entity Entity;
        public ScenarioStance Stance;
        public bool Piloted;
        public AgentTask Task;
        // What the pilot already knows at the start: an entity its sensors hold a track of (Approach). Seeded through the
        // sensors' own state at admission, so they may lose it honestly.
        public Entity Knows;
    }

    // A hull fitted slot by slot: each slot a design or a product, the hull cell it goes in, and its rotation. A name is
    // a design (open: the stage chooses its product) or a product (pinned: that brand).
    public ScenarioFit Fit(string hull, params (string design, int2 cell, ItemRotation rotation)[] slots) =>
        FitAt(hull, null, slots);

    // The same fit with every lot it mints (hull, slots, unbranded) at the stated quality, drawing nothing. A fit a scenario's
    // checks rest on states its quality, so the check does not ride the item stream.
    public ScenarioFit Fit(string hull, float quality, params (string design, int2 cell, ItemRotation rotation)[] slots) =>
        FitAt(hull, quality, slots);

    private ScenarioFit FitAt(string hull, float? quality, (string design, int2 cell, ItemRotation rotation)[] slots)
    {
        var where = $"fit {hull}";
        var (hullData, hullProduct) = NamedOrProduct<HullData>(hull, where);
        var loadout = new Loadout { Name = hull, WeaponGroups = new int[0][] };
        if (hullData != null) loadout.Hull = _items.ItemData.RefOf(hullData);
        if (hullProduct != null) loadout.HullProduct = _items.ItemData.RefOf(hullProduct);
        foreach (var (design, cell, rotation) in slots)
        {
            var (data, product) = NamedOrProduct<EquippableItemData>(design, where);
            if (data == null) continue;
            loadout.Slots.Add(new LoadoutSlot
            {
                Design = _items.ItemData.RefOf(data), Position = cell, Rotation = rotation,
                Product = product == null ? default : _items.ItemData.RefOf(product)
            });
        }
        return hullData != null && loadout.Slots.Count == slots.Length ? new ScenarioFit(loadout, quality) : null;
    }

    // A hull with nothing fitted.
    public ScenarioFit Bare(string hull) => Fit(hull);

    // A fit generated as the game generates its ships, for the faction when one is given, from the run's item draws,
    // which RunStart seeds from the galaxy's seed. An empty hull means any ship hull.
    public ScenarioFit Generated(string hull, Faction faction = null)
    {
        var generator = new LoadoutGenerator(ref _items.Random, _items, Galaxy, Arena.GalaxyZone, faction, 2);
        var pack = generator.GenerateShipLoadout(data => string.IsNullOrEmpty(hull) || data.Name == hull);
        if (pack != null) return new ScenarioFit(pack);
        _failures.Add($"generated: no ship hull named {hull}");
        return null;
    }

    // A loadout preset from the catalog, as capturepreset or Studio wrote it.
    public ScenarioFit Preset(string name)
    {
        var preset = _items.ItemData.GetAll<Loadout>().FirstOrDefault(loadout => loadout.Name == name);
        if (preset != null) return new ScenarioFit(preset);
        _failures.Add($"preset {name}: no such preset");
        return null;
    }

    // The player's ship. A run has exactly one, and it is a ship hull.
    public Ship Player(ScenarioFit fit, float2 at, float2 facing = default)
    {
        if (PlayerPlaced)
        {
            _failures.Add("player: the scenario places a second player");
            return null;
        }
        PlayerPlaced = true;
        var entity = Build(fit, "player", at, facing);
        if (entity == null) return null;
        if (entity is Ship ship) return PlayerShip = ship;
        _failures.Add($"player: {entity.Name} is not a ship hull");
        return null;
    }

    // Anything else in the arena. A stance holds both ways between it and the player. A piloted ship gets the zone's
    // pilot; an unpiloted entity has none and goes nowhere on its own unless it is placed moving: velocity is the planar
    // velocity it starts with, and the sim carries it from there. A turret hull stays where it is put.
    public Entity Place(ScenarioFit fit, float2 at, float2 facing = default, ScenarioStance stance = ScenarioStance.Neutral, bool piloted = false,
        float2 velocity = default)
    {
        var where = $"entity {_placements++}";
        var entity = Build(fit, where, at, facing);
        if (entity == null) return null;
        entity.Velocity = velocity;
        if (piloted && !(entity is Ship)) _failures.Add($"{where}: {entity.Name} is not a ship hull, so it cannot be piloted");
        _placed.Add(new Placement { Entity = entity, Stance = stance, Piloted = piloted });
        return entity;
    }

    // The task a piloted entity starts on: it closes on the target and holds there (the flight's follow task, standoff
    // nothing), in place of the patrol circuit the zone would draw for it. The pilot starts knowing it (RunStart seeds a sensor track). A check that rests on where a pilot goes states
    // it here, so the pilot's course is the scenario's and not a draw. An entity that is not placed and piloted, or a
    // target that is not placed, is a failure.
    public void Approach(Entity entity, Entity target)
    {
        var placement = _placed.FirstOrDefault(p => ReferenceEquals(p.Entity, entity));
        if (placement == null || !placement.Piloted) { _failures.Add($"approach: {entity?.Name ?? "no entity"} is not a placed, piloted ship"); return; }
        if (target == null) { _failures.Add("approach: no target to close on"); return; }
        placement.Task = new FollowTask { Anchor = PilotView.Handle(target), Standoff = 0f };
        placement.Knows = target;
    }

    // Items in the entity's first cargo bay, one per name. A name is an equippable or consumable design (open: the stage
    // chooses its product) or a product of one (pinned); a name that is neither is a failure.
    public void Cargo(Entity entity, params string[] names)
    {
        if (entity == null) return;
        var where = $"cargo of {entity.Name}";
        var bay = entity.CargoBays.FirstOrDefault();
        foreach (var name in names)
        {
            var (data, product) = NamedOrProduct<CraftedItemData>(name, where, found => found is EquippableItemData || found is ConsumableItemData);
            if (data == null) continue;
            var build = Loadouts.Resolve(_items, _items.ItemData.RefOf(data),
                product == null ? default : _items.ItemData.RefOf(product), _choose, where, _failures);
            if (build == null) continue;
            if (bay == null || !bay.TryStore(build()))
                _failures.Add($"{where}: no room for {name}");
        }
    }

    private Entity Build(ScenarioFit fit, string where, float2 at, float2 facing)
    {
        if (fit == null) return null; // the verb that made it named why

        Entity entity;
        if (fit.Pack != null) entity = EntitySerializer.Unpack(_items, Arena, fit.Pack);
        else
        {
            var failures = new List<string>();
            entity = Loadouts.Materialize(_items, Arena, fit.Loadout, _choose, failures, fit.Quality);
            _failures.AddRange(failures.Select(failure => $"{where} ({fit.Loadout.Name}): {failure}"));
            if (entity == null) return null;
        }

        entity.Position = float3(at.x, 0, at.y);
        if (lengthsq(facing) > 0) entity.Direction = normalize(facing);
        return entity;
    }

    // A name is a design, or else exactly one product (names are unique) whose design is a T passing among: the design
    // and that product. Neither is Named's failure.
    private (T design, FactionProductData product) NamedOrProduct<T>(string name, string where, Func<T, bool> among = null) where T : CraftedItemData
    {
        var cache = _items.ItemData;
        var products = cache.GetAll<FactionProductData>().Where(p => p.Name == name && cache.Get(p.Design) is T design && (among == null || among(design))).Take(2).ToArray();
        if (products.Length == 1 && !cache.GetAll<T>().Any(data => data.Name == name && (among == null || among(data))))
            return ((T) cache.Get(products[0].Design), products[0]);
        return (Named(name, where, among), null);
    }

    private T Named<T>(string name, string where, Func<T, bool> among = null) where T : CraftedItemData
    {
        var named = _items.ItemData.GetAll<T>().Where(data => data.Name == name && (among == null || among(data))).Take(2).ToArray();
        if (named.Length == 1) return named[0];
        _failures.Add(named.Length == 0 ? $"{where}: no design named {name}" : $"{where}: more than one design is named {name}");
        return null;
    }
}

// What a scenario places: a fit built from named designs or a preset, or a ship generated as the game generates them.
// Only ScenarioStage makes one.
public sealed class ScenarioFit
{
    internal readonly Loadout Loadout;
    internal readonly EntityPack Pack;
    internal readonly float? Quality;

    internal ScenarioFit(Loadout loadout, float? quality = null)
    {
        Loadout = loadout;
        Quality = quality;
    }

    internal ScenarioFit(EntityPack pack) => Pack = pack;
}
