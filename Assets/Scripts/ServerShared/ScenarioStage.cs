using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The run half of a scenario's vocabulary (docs/scenarios-cut.md, R.4). A scenario's Stage builds its fits and places
// them here; nothing joins the arena until Stage returns with no failure, and then RunStart admits it all at once.
// Every verb names what it could not do instead of throwing, so one bad name in a script lists every bad name.
// Designs and hulls are named as the catalog names them. Only RunStart builds a stage.
public sealed class ScenarioStage
{
    private readonly ItemManager _items;
    private readonly Predicate<FactionProductData> _available;
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
        // A fit names designs, not manufacturers, so what the galaxy offers decides which product builds each one.
        _available = new LoadoutGenerator(ref items.Random, items, arena.Galaxy, arena.GalaxyZone, null, 2).IsAvailable;
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
    }

    // A hull fitted slot by slot: each slot a design, the hull cell it goes in, and its rotation.
    public ScenarioFit Fit(string hull, params (string design, int2 cell, ItemRotation rotation)[] slots)
    {
        var where = $"fit {hull}";
        var hullData = Named<HullData>(hull, where);
        var loadout = new Loadout { Name = hull, WeaponGroups = new int[0][] };
        if (hullData != null) loadout.Hull = _items.ItemData.RefOf(hullData);
        foreach (var (design, cell, rotation) in slots)
        {
            var data = Named<EquippableItemData>(design, where);
            if (data == null) continue;
            loadout.Slots.Add(new LoadoutSlot { Design = _items.ItemData.RefOf(data), Position = cell, Rotation = rotation });
        }
        return hullData != null && loadout.Slots.Count == slots.Length ? new ScenarioFit(loadout) : null;
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
    // pilot; an unpiloted entity has none and goes nowhere on its own. A turret hull stays where it is put.
    public Entity Place(ScenarioFit fit, float2 at, float2 facing = default, ScenarioStance stance = ScenarioStance.Neutral, bool piloted = false)
    {
        var where = $"entity {_placements++}";
        var entity = Build(fit, where, at, facing);
        if (entity == null) return null;
        if (piloted && !(entity is Ship)) _failures.Add($"{where}: {entity.Name} is not a ship hull, so it cannot be piloted");
        _placed.Add(new Placement { Entity = entity, Stance = stance, Piloted = piloted });
        return entity;
    }

    // Items in the entity's first cargo bay, one per design named.
    public void Cargo(Entity entity, params string[] designs)
    {
        if (entity == null) return;
        var where = $"cargo of {entity.Name}";
        var bay = entity.CargoBays.FirstOrDefault();
        foreach (var design in designs)
        {
            var data = Named<EquippableItemData>(design, where);
            if (data == null) continue;
            var build = Loadouts.Resolve(_items, _items.ItemData.RefOf(data), _available, where, _failures);
            if (build != null && (bay == null || !bay.TryStore(build())))
                _failures.Add($"{where}: no room for {design}");
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
            entity = Loadouts.Materialize(_items, Arena, fit.Loadout, _available, failures);
            _failures.AddRange(failures.Select(failure => $"{where} ({fit.Loadout.Name}): {failure}"));
            if (entity == null) return null;
        }

        entity.Position = float3(at.x, 0, at.y);
        if (lengthsq(facing) > 0) entity.Direction = normalize(facing);
        return entity;
    }

    private T Named<T>(string name, string where) where T : EquippableItemData
    {
        var named = _items.ItemData.GetAll<T>().Where(data => data.Name == name).Take(2).ToArray();
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

    internal ScenarioFit(Loadout loadout) => Loadout = loadout;
    internal ScenarioFit(EntityPack pack) => Pack = pack;
}
