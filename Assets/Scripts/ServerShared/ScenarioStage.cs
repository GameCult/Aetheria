using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The run half of a scenario's vocabulary (docs/scenarios-cut.md, R.4). A scenario's Stage builds its fits and places
// them here; nothing joins the arena until Stage returns with no failure, and then the stage admits it all at once.
// Every verb names what it could not do instead of throwing, so one bad name in a script lists every bad name.
// Designs, hulls and products are named as the catalog names them.
// ZoneStage is the placement vocabulary and the admission of what it placed; Zone.Admit stays the only admission.
// RunStart builds a ScenarioStage (a ZoneStage that also stages the player and authors stance); ZoneStage.Summon builds a
// ZoneStage for a running zone. A ZoneStage has no Player verb and no stance, so a summon placing a player or writing who
// fights whom is unrepresentable: a summon's stance is the faction rule's and its grudges'.
public class ZoneStage
{
    private readonly ItemManager _items;
    private readonly Func<CraftedItemData, FactionProductData> _choose;
    private protected readonly List<string> _failures = new List<string>();
    private readonly List<Placement> _placed = new List<Placement>();
    private int _placements;
    // The entities this stage built from a fit (a packed fit is a saved ship, whose cargo is its own) and those whose cargo the scenario
    // authored with Cargo: the rest are stocked with the crates their weapons draw when staging ends (StockUnauthoredAmmunition).
    private readonly HashSet<Entity> _fitted = new HashSet<Entity>();
    private readonly HashSet<Entity> _cargoAuthored = new HashSet<Entity>();

    internal ZoneStage(ItemManager items, Zone arena)
    {
        _items = items;
        Arena = arena;
        // A design named without a product is chosen by the generator's ChooseProduct. The stage outfits with no faction,
        // so every maker is reachable and the run's seed draws.
        _choose = new LoadoutGenerator(ref items.Random, items, arena.Galaxy, arena.GalaxyZone, null, 2).ChooseProduct;
    }

    public Galaxy Galaxy => Arena.Galaxy;
    public Zone Arena { get; }

    internal IReadOnlyList<string> Failures => _failures;

    // Staging's last step: a fitted entity whose cargo the scenario did not author gets the crates its weapons draw, like every other
    // newly built armed entity (Loadouts.StockAmmunition). Draws nothing, so it moves no item roll.
    private protected void StockUnauthoredAmmunition()
    {
        foreach (var entity in _fitted.Where(entity => !_cargoAuthored.Contains(entity)))
            Loadouts.StockAmmunition(_items, entity);
    }
    internal IReadOnlyList<Placement> Placed => _placed;

    internal sealed class Placement
    {
        public Entity Entity;
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
        if (preset == null)
        {
            _failures.Add($"preset {name}: no such preset");
            return null;
        }
        // A faction's boss is the zone generator's to place (boss-hulls-exclusive): Loadouts.BossOf owns which loadout it is.
        if (_items.ItemData.GetAll<Faction>().Any(faction => Loadouts.BossOf(_items.ItemData, faction, out _)?.Name == preset.Name))
        {
            _failures.Add($"preset {name}: a faction's boss loadout is placed only by the zone generator");
            return null;
        }
        return new ScenarioFit(preset);
    }

    // Anything else in the arena. A piloted ship gets the zone's pilot; an unpiloted entity has none and goes nowhere on its
    // own unless it is placed moving: velocity is the planar velocity it starts with, and the sim carries it from there. A
    // turret hull stays where it is put. It writes no stance: who fights whom is the faction rule's (stance is ScenarioStage's).
    public Entity Place(ScenarioFit fit, float2 at, float2 facing = default, bool piloted = false, float2 velocity = default)
    {
        var where = $"entity {_placements++}";
        var entity = Build(fit, where, at, facing);
        if (entity == null) return null;
        entity.Velocity = velocity;
        if (piloted && !(entity is Ship)) _failures.Add($"{where}: {entity.Name} is not a ship hull, so it cannot be piloted");
        _placed.Add(new Placement { Entity = entity, Piloted = piloted });
        return entity;
    }

    // A piloted ship that carries a standing haul order (ruling demo-zenith-ore-depot): its task is derived from the order at
    // admission (HaulRun.TaskOf), so the order must exist by then; ScenarioStage writes its orders before it admits. A fit that is not
    // a ship is Place's failure.
    public Ship Hauler(ScenarioFit fit, string orderId, float2 at)
    {
        var ship = Place(fit, at, piloted: true) as Ship;
        if (ship != null) ship.Haul = new CultRecordRef<HaulOrderData>(HaulOrderData.KeyOf(orderId));
        return ship;
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
        // The one production handle minted from truth: the stage states the pilot's knowledge of its anchor (ruling
        // stage-states-pilot-knowledge), and RunStart seeds the matching sensor track. The view still answers about the
        // anchor only while the pilot's sensors hold it.
        placement.Task = new FollowTask { Anchor = PilotView.Handle(target), Standoff = 0f };
        placement.Knows = target;
    }

    // Items in the entity's first cargo bay, one per name. A name is an equippable or consumable design (open: the stage
    // chooses its product) or a product of one (pinned), or a commodity (one unit: the crates an AmmoType weapon reloads
    // from); a name that is none of these is a failure.
    public void Cargo(Entity entity, params string[] names)
    {
        if (entity == null) return;
        _cargoAuthored.Add(entity);
        var where = $"cargo of {entity.Name}";
        var bay = entity.CargoBays.FirstOrDefault();
        foreach (var name in names)
        {
            var commodity = _items.ItemData.GetAll<SimpleCommodityData>().FirstOrDefault(data => data.Name == name);
            if (commodity != null)
            {
                if (bay == null || !bay.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(_items.ItemData.RefOf<ItemData>(commodity).Key), Quantity = 1 }))
                    _failures.Add($"{where}: no room for {name}");
                continue;
            }
            var (data, product) = NamedOrProduct<CraftedItemData>(name, where, found => found is EquippableItemData || found is ConsumableItemData);
            if (data == null) continue;
            var build = Loadouts.Resolve(_items, _items.ItemData.RefOf(data),
                product == null ? default : _items.ItemData.RefOf(product), _choose, where, _failures);
            if (build == null) continue;
            if (bay == null || !bay.TryStore(build()))
                _failures.Add($"{where}: no room for {name}");
        }
    }

    // Admits what was placed: Zone.Admit with a pilot for each piloted one. It writes no stance. A start state includes what a pilot knows (ruling stage-states-pilot-knowledge): its anchor is tracked
    // from the first step, by the sensors' own path (info gathered past the detection threshold), so they can lose it
    // like any track. The one owner of admitting placements.
    private protected void AdmitPlaced()
    {
        foreach (var placed in _placed)
        {
            Arena.Admit(placed.Entity, placed.Piloted, placed.Task);
        }
        foreach (var placed in _placed)
            if (placed.Knows != null && placed.Piloted)
                placed.Entity.EntityInfoGathered[placed.Knows] = (_items.GameplaySettings.TargetDetectionInfoThreshold + 1f) / 2f;
    }

    // Places ships into a running zone through the admission a run start uses: the script places with the stage's verbs,
    // and the entities join with a pilot when piloted and the knowledge the script stated. A summon writes no stance: the
    // faction rule decides who fights whom, and a grudge (Entity.Provoke) turns the summoned faction on whoever attacks it.
    // Returns the placed entities in order, or null with every failure listed and the zone and the item stream and lot ledger unchanged.
    // It draws from the live item stream and does not reseed (a reseed would give every summon the same fits).
    public static IReadOnlyList<Entity> Summon(ItemManager items, Zone zone, Action<ZoneStage> script, List<string> failures)
    {
        var mark = items.Mark();
        var stage = new ZoneStage(items, zone);
        script(stage);
        failures.AddRange(stage._failures);
        if (stage._failures.Count > 0)
        {
            items.Rewind(mark); // a refusal draws and mints nothing: the fits it built are discarded with it
            return null;
        }
        stage.StockUnauthoredAmmunition();
        stage.AdmitPlaced();
        return stage._placed.Select(placed => placed.Entity).ToList();
    }

    private protected Entity Build(ScenarioFit fit, string where, float2 at, float2 facing)
    {
        if (fit == null) return null; // the verb that made it named why

        Entity entity;
        if (fit.Pack != null) entity = EntitySerializer.Unpack(_items, Arena, fit.Pack);
        else
        {
            var failures = new List<string>();
            entity = Loadouts.Materialize(_items, Arena, fit.Loadout, _choose, failures, fit.Quality, stockAmmunition: false);
            if (entity != null) _fitted.Add(entity);
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

// The run-start stage: a ZoneStage that also stages the player, the credits the run starts with and the hull it
// starts in, and the one place a script authors stance: a Hostile placement is hostile both ways with the player, written
// once at admission. Neutral writes nothing (the faction rule and grudges decide). Only RunStart builds one.
public sealed class ScenarioStage : ZoneStage
{
    private readonly ItemManager _runItems;
    private readonly List<(string id, HaulOrderData order)> _haulOrders = new List<(string, HaulOrderData)>();

    internal ScenarioStage(ItemManager items, Zone arena, string startingHull) : base(items, arena)
    {
        _runItems = items;
        StartingHull = startingHull;
        Credits = items.GameplaySettings.StartingCredits;
    }

    public string StartingHull { get; }

    // What the run starts with: the gameplay settings' starting credits unless the scenario sets another amount.
    public int Credits { get; set; }

    private readonly List<Entity> _hostile = new List<Entity>();

    // Anything else in the arena, as ZoneStage.Place, with a stance toward the player: Hostile holds both ways from the start.
    public new Entity Place(ScenarioFit fit, float2 at, float2 facing = default, ScenarioStance stance = ScenarioStance.Neutral, bool piloted = false,
        float2 velocity = default)
    {
        var entity = base.Place(fit, at, facing, piloted, velocity);
        if (entity != null && stance == ScenarioStance.Hostile) _hostile.Add(entity);
        return entity;
    }

    internal bool PlayerPlaced { get; private set; }
    internal Ship PlayerShip { get; private set; }

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

    // The arena's one station of a kind (what it is for, never its hull); a failure naming the kind when there is none or several.
    public OrbitalEntity Station(string kindId)
    {
        var kind = StationKindData.KeyOf(kindId);
        var found = Arena.Entities.OfType<OrbitalEntity>().Where(entity => entity.Kind.IsSet() && entity.Kind.Key.Equals(kind)).Take(2).ToArray();
        if (found.Length == 1) return found[0];
        _failures.Add(found.Length == 0 ? $"station {kindId}: the arena holds no station of that kind" : $"station {kindId}: the arena holds more than one station of that kind");
        return null;
    }

    // A standing haul order between two of the arena's stations, by their orbits (ruling transit-depots-buy-for-onward-haul).
    // Queued here and written only in Admit, so a refused stage writes nothing.
    public void HaulOrder(string id, OrbitalEntity from, OrbitalEntity to, IEnumerable<CultRecordRef<ItemData>> goods, string commitment)
    {
        if (from == null || to == null) return; // the verb that found them named why
        if (!from.OrbitData.IsSet() || !to.OrbitData.IsSet())
        {
            _failures.Add($"haul order {id}: a station with no orbit record cannot be a haul order's end");
            return;
        }
        _haulOrders.Add((id, new HaulOrderData
        {
            From = new CultRecordRef<OrbitData>(from.OrbitData), To = new CultRecordRef<OrbitData>(to.OrbitData),
            Goods = goods.ToList(), Commitment = commitment
        }));
    }

    // Run start's admission: stock unauthored ammunition, the player, then everything placed. Nothing at admission or
    // activation reads cargo (ammunition is read when a weapon fires), so stocking is not ordered by admission; it keeps
    // the run start's order.
    internal void Admit()
    {
        if (_haulOrders.Count > 0)
            _runItems.ItemData.Commit(batch =>
            {
                foreach (var (id, order) in _haulOrders) batch.Upsert(typeof(HaulOrderData), order, HaulOrderData.KeyOf(id));
            });
        StockUnauthoredAmmunition();
        PlayerShip.IsPlayerShip = true;
        Arena.Admit(PlayerShip, piloted: false);
        foreach (var entity in _hostile)
        {
            entity.SetIff(PlayerShip, true);
            PlayerShip.SetIff(entity, true);
        }
        AdmitPlaced();
    }
}

// What a scenario places: a fit built from named designs or a preset, or a ship generated as the game generates them.
// Only a ZoneStage makes one.
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
