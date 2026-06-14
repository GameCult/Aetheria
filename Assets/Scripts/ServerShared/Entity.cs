/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using UniRx;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using int2 = Unity.Mathematics.int2;

public abstract class Entity
{
    public Zone Zone;
    public Faction Faction;
    public EquippableItem Hull;
    public EquippedItem EquippedHull;
    
    public float3 Position;
    public float2 Direction = float2(0,1);
    public float2 Velocity;
    
    public float[,] Temperature;
    public float[,] NewTemperature;
    public bool2[,] HullConductivity;
    public float[,] ThermalMass;

    public readonly ReactiveCollection<EquippedItem> Equipment = new ReactiveCollection<EquippedItem>();
    public readonly ReactiveCollection<EquippedCargoBay> CargoBays = new ReactiveCollection<EquippedCargoBay>();
    public readonly ReactiveCollection<EquippedDockingBay> DockingBays = new ReactiveCollection<EquippedDockingBay>();
    public readonly ReactiveCollection<Entity> VisibleEntities = new ReactiveCollection<Entity>();
    public readonly ReactiveDictionary<Entity, bool> EntityHostility = new ReactiveDictionary<Entity, bool>();
    public readonly ReactiveCollection<Entity> VisibleEnemies = new ReactiveCollection<Entity>();
    public readonly ReactiveCollection<Entity> VisibleFriendlies = new ReactiveCollection<Entity>();

    public Entity Parent;
    public List<Entity> Children = new List<Entity>();
    public ReactiveProperty<Entity> Target = new ReactiveProperty<Entity>((Entity)null);

    public float3 LookDirection;
    
    public string Name;
    
    // public int Population;
    
    public readonly Dictionary<string, float> Messages = new Dictionary<string, float>();
    public readonly Dictionary<object, float> VisibilitySources = new Dictionary<object, float>();
    public readonly ReactiveDictionary<Entity, float> EntityInfoGathered = new ReactiveDictionary<Entity, float>(); 
    public readonly Dictionary<HardpointData, (float3 position, float3 direction)> HardpointTransforms = 
        new Dictionary<HardpointData, (float3 position, float3 direction)>();
    
    public (List<Weapon> weapons, List<EquippedItem> items)[] WeaponGroups;

    public List<IPopulationAssignment> PopulationAssignments = new List<IPopulationAssignment>();

    public EquippedItem[,] GearOccupancy;
    public HardpointData[,] Hardpoints;
    public float[,] Armor;
    public float[,] MaxArmor;
    
    private EquippedItem[] _orderedEquipment;
    private List<Weapon> _weapons = new List<Weapon>();
    private List<Capacitor> _capacitors = new List<Capacitor>();
    private List<Reactor> _reactors = new List<Reactor>();
    private List<Radiator> _heatsinks = new List<Radiator>();

    private List<ConsumableItemEffect> _activeConsumables = new List<ConsumableItemEffect>();
    
    protected bool _active;

    private bool _heatsinksEnabled = true;

    public bool HeatsinksEnabled
    {
        get => _heatsinksEnabled;
        set
        {
            if (value == _heatsinksEnabled) return;
            _heatsinksEnabled = value;
            foreach (var heatsink in _heatsinks)
                heatsink.Item.Enabled.Value = value;
        }
    }
    
    public EntitySettings Settings { get; }
    
    public bool OverrideShutdown { get; set; }
    
    public float TractorPower { get; set; }
    
    public bool Active
    {
        get => _active;
    }
    
    public IEnumerable<Weapon> Weapons
    {
        get => _weapons;
    }
    
    public Shield Shield { get; private set; }
    public Cockpit Cockpit { get; private set; }
    public Sensor Sensor { get; private set; }
    public float Heatstroke { get; private set; }
    public float Hypothermia { get; private set; }
    
    public float TargetRange { get; private set; }
    public float MaxTemp { get; private set; }
    public float MinTemp { get; private set; }
    public ItemManager ItemManager { get; }
    public int AssignedPopulation => PopulationAssignments.Sum(pa => pa.AssignedPopulation);
    public float Mass { get; private set; }
    public float Visibility => VisibilitySources.Values.Sum();

    public Subject<Entity> IncomingHit = new Subject<Entity>();
    public Subject<(int2 pos, float damage)> ArmorDamage = new Subject<(int2, float)>();
    public Subject<(EquippedItem item, float damage)> ItemDamage = new Subject<(EquippedItem, float)>();
    // public Subject<EquippedItem> ItemOffline = new Subject<EquippedItem>();
    // public Subject<EquippedItem> ItemOnline = new Subject<EquippedItem>();
    public Subject<float> HullDamage = new Subject<float>();
    public Subject<Entity> Docked = new Subject<Entity>();
    public Subject<Unit> HeatstrokeRisk = new Subject<Unit>();
    public Subject<Unit> HeatstrokeDeath = new Subject<Unit>();
    public Subject<Unit> HypothermiaRisk = new Subject<Unit>();
    public Subject<Unit> HypothermiaDeath = new Subject<Unit>();
    public Subject<Entity> TargetedBy = new Subject<Entity>();
    public ReactiveProperty<int> TargetedByCount = new ReactiveProperty<int>(0);
    public ReactiveProperty<SecurityLevel> CurrentSecurityLevel = new ReactiveProperty<SecurityLevel>(SecurityLevel.Open);
    public ReadOnlyReactiveProperty<bool> PresencePermitted;
    
    public UniRx.IObservable<EquippedItem> ItemDestroyed;
    public UniRx.IObservable<int2> HullArmorDepleted;
    public UniRx.IObservable<HardpointData> HardpointArmorDepleted;
    public UniRx.IObservable<Weapon> WeaponDestroyed;
    public UniRx.IObservable<CauseOfDeath> Death;

    private List<IDisposable> _subscriptions = new List<IDisposable>();
    private Dictionary<Entity, List<IDisposable>> _watchedEntitySubscriptions = new Dictionary<Entity, List<IDisposable>>();

    public virtual void Activate()
    {
        //ItemManager.Log($"Entity {Name} is activating!");
        _active = true;
        Heatstroke = 0;

        // The entity's presence is permitted when its faction owns the zone, or the current security level at its location is low enough
        PresencePermitted = new ReadOnlyReactiveProperty<bool>(CurrentSecurityLevel.Select(security =>
        {
            var factionRelationship = GetFactionRelationship(Zone.GalaxyZone.Owner);
            var presencePermitted = IsPresencePermitted(factionRelationship, security);
            return presencePermitted;
        }), initialValue: true);
        
        foreach (var item in Equipment)
        foreach (var behavior in item.Behaviors)
        {
            if(behavior is IInitializableBehavior initializableBehavior)
                initializableBehavior.Initialize();
        }
        foreach(var entity in Zone.Entities)
        {
            EntityInfoGathered[entity] = 0;
            EntityHostility[entity] = IsHostileTo(entity);
        }
        _subscriptions.Add(Zone.Entities.ObserveAdd().Subscribe(add =>
        {
            EntityInfoGathered[add.Value] = 0;
            EntityHostility[add.Value] = IsHostileTo(add.Value);
        }));
        _subscriptions.Add(Zone.Entities.ObserveRemove().Subscribe(remove =>
        {
            if (Target.Value == remove.Value) Target.Value = null;
            EntityInfoGathered.Remove(remove.Value);
            EntityHostility.Remove(remove.Value);
            VisibleEntities.Remove(remove.Value);
            VisibleEnemies.Remove(remove.Value);
            VisibleFriendlies.Remove(remove.Value);
        }));
        _subscriptions.Add(VisibleEnemies.ObserveRemove().Subscribe(remove =>
        {
            if (Target.Value == remove.Value) Target.Value = null;
        }));
        _subscriptions.Add(Target.Subscribe(entity => entity?.TargetedBy.OnNext(this)));
        _subscriptions.Add(TargetedBy.Subscribe(enemy =>
        {
            TargetedByCount.Value++;
            enemy.Target.Where(t => t != this).Take(1).Subscribe(_ => TargetedByCount.Value--);
        }));
        
        // 
        _subscriptions.Add(EntityInfoGathered.ObserveReplace().Subscribe(replace =>
        {
            if (replace.OldValue < ItemManager.GameplaySettings.TargetDetectionInfoThreshold &&
                replace.NewValue > ItemManager.GameplaySettings.TargetDetectionInfoThreshold)
            {
                VisibleEntities.Add(replace.Key);
                if(EntityHostility[replace.Key])
                    VisibleEnemies.Add(replace.Key);
                else VisibleFriendlies.Add(replace.Key);
            }
            if (replace.OldValue > ItemManager.GameplaySettings.TargetDetectionInfoThreshold &&
                replace.NewValue < ItemManager.GameplaySettings.TargetDetectionInfoThreshold)
            {
                VisibleEntities.Remove(replace.Key);
                VisibleEnemies.Remove(replace.Key);
                VisibleFriendlies.Remove(replace.Key);
            }
        }));
        
        // Create subscriptions to events occurring on any visible entity
        _subscriptions.Add(VisibleEntities.ObserveAdd()
            .Select(add=>add.Value)
            .Subscribe(entity =>
        {
            var newVisibleEntitySubscriptions = new List<IDisposable>
            {
                // Respond to changes in the target's hostility status by updating the contents of VisibleEnemies and VisibleFriendlies
                EntityHostility.ObserveReplace()
                    .Where(replace => replace.Key == entity)
                    .Select(replace => replace.NewValue)
                    .Subscribe(isHostile =>
                    {
                        (isHostile ? VisibleEnemies : VisibleFriendlies).Add(entity);
                        (isHostile ? VisibleFriendlies : VisibleEnemies).Remove(entity);
                    })
            };
            
            _watchedEntitySubscriptions[entity] = newVisibleEntitySubscriptions;
        }));
            
        // Cleanup visible entity subscriptions
        _subscriptions.Add(VisibleEntities.ObserveRemove().Subscribe(disappearingEntity =>
        {
            foreach(var subscription in _watchedEntitySubscriptions[disappearingEntity.Value]) subscription.Dispose();
            _watchedEntitySubscriptions.Remove(disappearingEntity.Value);
        }));
        
        if(WeaponGroups.All(wg=>!wg.items.Any()))
            GenerateWeaponGroups();
    }

    public virtual void Deactivate()
    {
        //ItemManager.Log($"Entity {Name} is deactivating!");
        foreach(var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
        foreach(var ss in _watchedEntitySubscriptions.Values) foreach(var s in ss) s.Dispose();
        _watchedEntitySubscriptions.Clear();
        _active = false;
        EntityInfoGathered.Clear();
        VisibleEntities.Clear();
        VisibleEnemies.Clear();
        VisibleFriendlies.Clear();
    }

    public Entity(ItemManager itemManager, Zone zone, EquippableItem hull, EntitySettings settings)
    {
        Settings = settings;
        ItemManager = itemManager;
        Zone = zone;
        Hull = hull;
        var typedHull = itemManager.GetRuntimeItem(hull);
        if (typedHull == null)
            throw new InvalidOperationException($"Unable to construct entity: missing typed hull row for {hull?.Data?.ItemId}");
        Name = typedHull.Name;
        MapEntity();
        WeaponGroups = new (List<Weapon> weapons, List<EquippedItem> items)[itemManager.GameplaySettings.WeaponGroupCount];
        for(int i=0; i<itemManager.GameplaySettings.WeaponGroupCount; i++)
            WeaponGroups[i] = (new List<Weapon>(), new List<EquippedItem>());

        ItemDestroyed = ItemDamage.Where(x => x.item.EquippableItem.Durability < .01f).Select(x=>x.item);
        WeaponDestroyed = ItemDestroyed.Select(x => x.Behaviors.FirstOrDefault(b => b is Weapon) as Weapon).Where(x => x != null);
        HullArmorDepleted = ArmorDamage.Where(x => Armor[x.pos.x, x.pos.y] < .01f).Select(x => x.pos);
        Death = HullDamage.Where(_ => Hull.Durability < .01f).Select(_ => CauseOfDeath.HullDestroyed)
            .Merge(HeatstrokeDeath.Select(_ => CauseOfDeath.Heatstroke))
            .Merge(HypothermiaDeath.Select(_ => CauseOfDeath.Hypothermia))
            .Merge(ItemDestroyed.Where(i=>i.GetBehavior<Cockpit>()!=null).Select(_ => CauseOfDeath.CockpitDestroyed));

        //CurrentSecurityLevel.Value = SecurityLevel.Open;
    }

    public bool IsHostileTo(Entity other, bool recursive = false)
    {
        if (Faction == null)
            return !recursive && other.Faction != null && other.IsHostileTo(this, true);

        // TODO: Inter-faction hostility
        // When the entity faction owns the zone, they are hostile to trespassers or those hostile to them
        if (Faction.ID == Zone.GalaxyZone.Owner?.ID)
            return recursive ? !(other.PresencePermitted?.Value ?? true) : !(other.PresencePermitted?.Value ?? true)|| other.IsHostileTo(this, true);

        return !recursive && other.IsHostileTo(this, true);
    }

    // TODO: Inter-faction relationships
    public FactionRelationship GetFactionRelationship(Faction faction)
    {
        if (faction == null)
            return FactionRelationship.Neutral;
        if (this is Ship {IsPlayerShip: true})
            return Zone.Galaxy.FactionRelationships[faction];
        return faction.ID == Faction.ID ? FactionRelationship.Beloved : FactionRelationship.Neutral;
    }

    public static bool IsPresencePermitted(FactionRelationship relationship, SecurityLevel securityLevel) => 
        (int) relationship - (int) securityLevel > 0;
    public static bool IsDockingPermitted(FactionRelationship relationship, SecurityLevel securityLevel) => 
        (int) relationship - (int) securityLevel > 1;

    public void ActivateConsumable(ConsumableItem item)
    {
        _activeConsumables.Add(new ConsumableItemEffect(item, this));
    }

    public ConsumableItemEffect FindActiveConsumable(Guid itemId)
    {
        return _activeConsumables.FirstOrDefault(ac => ac.Item?.Data?.ItemId == itemId);
    }

    public bool CanActivateConsumable(AetheriaRuntimeCatalogItem item)
    {
        var itemId = GetLegacyGuid(item);
        return item != null && itemId != Guid.Empty && (item.Stackable || FindActiveConsumable(itemId) == null);
    }

    public bool TryActivateConsumable(AetheriaRuntimeCatalogItem typedItem)
    {
        if (!CanActivateConsumable(typedItem)) return false;

        var itemId = GetLegacyGuid(typedItem);
        var bay = FindItemInCargo(itemId);
        if (bay == null) return false;

        var item = (ConsumableItem)bay.ItemsOfType[itemId].First();
        ActivateConsumable(item);
        bay.Remove(item);
        return true;
    }

    private static Guid GetLegacyGuid(AetheriaRuntimeCatalogItem item)
    {
        return item != null && Guid.TryParse(item.LegacyId, out var legacyId)
            ? legacyId
            : Guid.Empty;
    }

    private void MapEntity()
    {
        var typedHull = ItemManager.GetRuntimeItem(Hull);
        if (typedHull == null)
            throw new InvalidOperationException($"Unable to map entity {Name}: missing typed hull row for {Hull?.Data?.ItemId}");

        var hullShape = GetHullShape(typedHull);
        EquippedHull = new EquippedItem(ItemManager, Hull, int2.zero, this);
        Equipment.Add(EquippedHull);
        Mass = ItemManager.GetMass(Hull);
        Temperature = new float[hullShape.Width, hullShape.Height];
        NewTemperature = new float[hullShape.Width, hullShape.Height];
        HullConductivity = new bool2[hullShape.Width, hullShape.Height];
        ThermalMass = new float[hullShape.Width, hullShape.Height];
        Armor = new float[hullShape.Width, hullShape.Height];
        MaxArmor = new float[hullShape.Width, hullShape.Height];
        Hardpoints = new HardpointData[hullShape.Width, hullShape.Height];
        foreach (var typedHardpoint in typedHull.Hardpoints)
        {
            var hardpoint = ProjectHardpoint(typedHardpoint);
            foreach (var hardpointCoord in hardpoint.Shape.Coordinates)
            {
                var hullCoord = hardpoint.Position + hardpointCoord;
                Hardpoints[hullCoord.x, hullCoord.y] = hardpoint;
            }
        }
        var cellCount = Math.Max(hullShape.Coordinates.Length, 1);
        foreach (var v in hullShape.Coordinates)
        {
            Armor[v.x, v.y] = (float)typedHull.HullArmor;
            MaxArmor[v.x, v.y] = (float)typedHull.HullArmor;
            if (Hardpoints[v.x, v.y] != null)
            {
                Armor[v.x, v.y] += Hardpoints[v.x, v.y].Armor;
                MaxArmor[v.x, v.y] += Hardpoints[v.x, v.y].Armor;
            }
            Temperature[v.x, v.y] = 280;
            ThermalMass[v.x, v.y] = (float)(typedHull.Mass * typedHull.SpecificHeat / cellCount);
        }
        GearOccupancy = new EquippedItem[hullShape.Width, hullShape.Height];
    }

    public void GenerateWeaponGroups()
    {
        foreach (var group in Weapons
            .GroupBy(w => w.Item.EquippableItem.Data.ItemId)
            .OrderBy(wg=>wg.Average(w=>w.Range))
            .Select((weapons, index) => (weapons, index)))
        {
            WeaponGroups[group.index].weapons = group.weapons.ToList();
            WeaponGroups[group.index].items = group.weapons.Select(w=>w.Item).ToList();
        }
    }

    public void AddHeat(int2 position, float heat, bool ignoreThermalMass = false)
    {
        if (ignoreThermalMass)
            Temperature[position.x, position.y] += heat;
        else
            Temperature[position.x, position.y] += heat / ThermalMass[position.x, position.y];
    }

    public int CountItemsInCargo(Guid itemDataID)
    {
        int sum = 0;
        foreach (var x in CargoBays)
        {
            if (x.ItemsOfType.ContainsKey(itemDataID))
            {
                foreach (var i in x.ItemsOfType[itemDataID]) sum += i is SimpleCommodity simpleCommodity ? simpleCommodity.Quantity : 1;
            }
        }

        return sum;
    }

    public EquippedCargoBay FindItemInCargo(Guid itemDataID)
    {
        return CargoBays.FirstOrDefault(c => c.ItemsOfType.ContainsKey(itemDataID));
    }

    public Shape UnoccupiedSpace
    {
        get
        {
            var typedHull = ItemManager.GetRuntimeItem(Hull);
            var hullShape = GetHullShape(typedHull);
            var hullInterior = GetHullInteriorShape(typedHull);
            var emptyShape = new Shape(hullShape.Width, hullShape.Height);
            foreach (var v in hullShape.Coordinates)
            {
                if (hullInterior[v] && GearOccupancy[v.x, v.y] == null && Hardpoints[v.x, v.y] == null)
                    emptyShape[v] = true;
            }

            return emptyShape;
        }
    }

    // Attempts to move a given number of items of the given type to the target Entity
    // Returns the number of items successfully transferred
    public int TryTransferItems(Entity target, Guid itemDataID, int quantity)
    {
        int quantityTransferred = 0;
        while (quantityTransferred < quantity)
        {
            EquippedCargoBay originInventory = CargoBays.FirstOrDefault(c => c.ItemsOfType.ContainsKey(itemDataID));

            if (originInventory == null) break;

            var itemInstance = originInventory.ItemsOfType[itemDataID][0];

            if (itemInstance is SimpleCommodity simpleCommodity)
            {
                var targetQuantity = min(simpleCommodity.Quantity, quantity - quantityTransferred);
                if (!target.CargoBays.Any(c => originInventory.TryTransferItem(c, simpleCommodity, targetQuantity)))
                {
                    quantityTransferred += targetQuantity - simpleCommodity.Quantity;
                    break;
                }

                quantityTransferred += targetQuantity;
            }
            else if (itemInstance is CraftedItemInstance craftedItemInstance)
            {
                if (!target.CargoBays.Any(c => originInventory.TryTransferItem(c, craftedItemInstance)))
                    break;
                
                quantityTransferred++;
            }
        }

        return quantityTransferred;
    }

    public EquippableItem TryUnequip(EquippedItem item)
    {
        // Don't allow unequipping when the entity is active
        if (_active) return null;
        
        if (item.EquippableItem == null)
        {
            ItemManager.Log("Attempted to remove equipped item with no equippable item on it! This should be impossible!");
            return null;
        }

        if (item is EquippedCargoBay cargoBay)
        {
            if(cargoBay.Cargo.Count > 0)
            {
                ItemManager.Log("Attempted to remove cargo bay that is not empty! Please check first before doing this!");
                return null;
            }

            CargoBays.Remove(cargoBay);
        }
        
        Equipment.Remove(item);
        _orderedEquipment = Equipment.OrderBy(x => x.SortPosition).ToArray();
        
        var itemShape = GetItemShape(item.EquippableItem);
        var itemCellCount = Math.Max(itemShape.Coordinates.Length, 1);
        foreach (var i in GetHullShape().Coordinates)
            if (GearOccupancy[i.x, i.y] == item)
            {
                ThermalMass[i.x, i.y] -= ItemManager.GetThermalMass(item.EquippableItem) / itemCellCount;
                GearOccupancy[i.x, i.y] = null;
            }
        Mass -= ItemManager.GetMass(item.EquippableItem);
        foreach (var b in item.Behaviors)
        {
            if (b is Weapon weapon)
                _weapons.Remove(weapon);
            if (b is Capacitor capacitor)
                _capacitors.Remove(capacitor);
            if (b is Reactor reactor)
                _reactors.Remove(reactor);
            if (b is Radiator heatsink)
                _heatsinks.Remove(heatsink);
            if (b is Shield)
                Shield = null;
            if (b is Cockpit)
                Cockpit = null;
            if (b is Sensor)
                Sensor = null;
        }

        return item.EquippableItem;
    }

    private Shape GetHullShape()
    {
        return GetHullShape(ItemManager.GetRuntimeItem(Hull));
    }

    private static Shape GetHullShape(AetheriaRuntimeCatalogItem typedHull)
    {
        return ToShape(typedHull?.ShapeWidth ?? 1, typedHull?.ShapeHeight ?? 1, typedHull?.ShapeCells);
    }

    private static Shape GetHullInteriorShape(AetheriaRuntimeCatalogItem typedHull)
    {
        return ToShape(typedHull?.InteriorShapeWidth ?? 1, typedHull?.InteriorShapeHeight ?? 1, typedHull?.InteriorShapeCells);
    }

    private Shape GetItemShape(EquippableItem item)
    {
        return GetItemShape(ItemManager.GetRuntimeItem(item));
    }

    private static Shape GetItemShape(AetheriaRuntimeCatalogItem typedItem)
    {
        return ToShape(typedItem?.ShapeWidth ?? 1, typedItem?.ShapeHeight ?? 1, typedItem?.ShapeCells);
    }

    private static AetheriaRuntimeHardpoint GetHardpointAt(AetheriaRuntimeCatalogItem typedHull, int2 hullCoord)
    {
        if (typedHull == null) return null;

        foreach (var hardpoint in typedHull.Hardpoints)
        {
            var hardpointShape = ToShape(hardpoint.ShapeWidth, hardpoint.ShapeHeight, hardpoint.ShapeCells);
            var localCoord = hullCoord - new int2(hardpoint.PositionX, hardpoint.PositionY);
            if (hardpointShape[localCoord])
                return hardpoint;
        }

        return null;
    }

    private static HardpointType GetHardpointType(AetheriaRuntimeCatalogItem typedItem, HardpointType fallback)
    {
        return typedItem != null && Enum.TryParse(typedItem.HardpointType, out HardpointType hardpointType)
            ? hardpointType
            : fallback;
    }

    private static HardpointType GetHardpointType(AetheriaRuntimeHardpoint hardpoint, HardpointType fallback)
    {
        return hardpoint != null && Enum.TryParse(hardpoint.Type, out HardpointType hardpointType)
            ? hardpointType
            : fallback;
    }

    private static ItemRotation GetRotation(AetheriaRuntimeHardpoint hardpoint)
    {
        return hardpoint != null && Enum.TryParse(hardpoint.Rotation, out ItemRotation rotation)
            ? rotation
            : ItemRotation.None;
    }

    private static HardpointData ProjectHardpoint(AetheriaRuntimeHardpoint hardpoint)
    {
        return new HardpointData
        {
            Type = GetHardpointType(hardpoint, HardpointType.Hull),
            Position = new int2(hardpoint.PositionX, hardpoint.PositionY),
            Shape = ToShape(hardpoint.ShapeWidth, hardpoint.ShapeHeight, hardpoint.ShapeCells),
            Transform = hardpoint.Transform,
            Rotation = GetRotation(hardpoint),
            Armor = (float)hardpoint.Armor
        };
    }

    private static bool IsCargoBay(AetheriaRuntimeCatalogItem typedItem)
    {
        return typedItem != null &&
               (string.Equals(typedItem.Category, AetheriaRuntimeItemCategories.CargoBay, StringComparison.Ordinal) ||
                string.Equals(typedItem.Category, AetheriaRuntimeItemCategories.DockingBay, StringComparison.Ordinal));
    }

    private static bool IsDockingBay(AetheriaRuntimeCatalogItem typedItem)
    {
        return typedItem != null && string.Equals(typedItem.Category, AetheriaRuntimeItemCategories.DockingBay, StringComparison.Ordinal);
    }

    private static Shape ToShape(int width, int height, IReadOnlyList<AetheriaRuntimeShapeCell> cells)
    {
        var shape = new Shape(Math.Max(width, 1), Math.Max(height, 1));
        if (cells == null) return shape;

        foreach (var cell in cells)
            shape[new int2(cell.X, cell.Y)] = true;

        return shape;
    }

    // Check whether the given item will fit when its origin is placed at the given coordinate
    private bool ItemFits(AetheriaRuntimeCatalogItem typedItem, AetheriaRuntimeCatalogItem typedHull, EquippableItem item, int2 hullCoord)
    {
        var hullShape = GetHullShape(typedHull);
        var itemShape = GetItemShape(typedItem);
        var itemHardpointType = GetHardpointType(typedItem, HardpointType.Tool);
        if (itemShape.Coordinates.Length == 0) return false;

        // If the given coordinate isn't even in the ship it obviously won't fit
        if (!hullShape[hullCoord]) return false;
        
        // Items without specific hardpoints on the ship can be freely rotated and placed anywhere
        if (itemHardpointType == HardpointType.Tool)
        {
            var hullInterior = GetHullInteriorShape(typedHull);
            // Check every cell of the item's shape
            foreach (var i in itemShape.Coordinates)
            {
                // If there is any gear already occupying that space, it won't fit
                // If there's a hardpoint there, it won't fit
                // Thermal items have their own layer and do not collide with gear
                var itemCoord = hullCoord + itemShape.Rotate(i, item.Rotation);
                if (!hullInterior[itemCoord] ||
                    GetHardpointAt(typedHull, itemCoord) != null ||
                    GearOccupancy[itemCoord.x, itemCoord.y] != null) 
                    return false;
            }
        }
        else
        {
            var hardpoint = GetHardpointAt(typedHull, hullCoord);
            
            // If there's no hardpoint there, it won't fit
            if (hardpoint == null) return false;

            // If the hardpoint type doesn't match the item, it won't fit
            if (GetHardpointType(hardpoint, HardpointType.Hull) != itemHardpointType) return false;
            
            // Items placed in hardpoints are automatically aligned to hardpoint rotation
            item.Rotation = GetRotation(hardpoint);

            // Inset the shapes of both item and hardpoint
            var itemShapeInset = hullShape.Inset(itemShape, hullCoord, item.Rotation);
            var hardpointShapeInset = hullShape.Inset(
                ToShape(hardpoint.ShapeWidth, hardpoint.ShapeHeight, hardpoint.ShapeCells),
                new int2(hardpoint.PositionX, hardpoint.PositionY));
            
            // Check every cell of the hardpoint shape for existing items
            foreach(var v in hardpointShapeInset.Coordinates)
                if (GearOccupancy[v.x, v.y] != null)
                    return false;
            
            // Check every cell of the item's shape
            foreach (var i in itemShapeInset.Coordinates)
            {
                // If the hardpoint does not have a matching cell, it wont fit
                if (!hardpointShapeInset[i]) return false;
            
                // If there is any gear already occupying that space, it won't fit
                if (GearOccupancy[i.x, i.y] != null) return false;
            }
        }

        return true;
    }

    // Check whether the given item will fit when its origin is placed at the given coordinate on the hull
    public bool ItemFits(EquippableItem item, int2 hullCoord)
    {
        // Don't allow equipping while deployed
        if (_active) return false;

        var typedItem = ItemManager.GetRuntimeItem(item);
        var typedHull = ItemManager.GetRuntimeItem(Hull);
        return typedItem != null && typedHull != null && ItemFits(typedItem, typedHull, item, hullCoord);
    }

    public bool TryFindSpace(EquippableItem item, out int2 hullCoord)
    {
        // Don't allow equipping while deployed
        if (_active)
        {
            hullCoord = int2.zero;
            return false;
        }
        var typedItem = ItemManager.GetRuntimeItem(item);
        var typedHull = ItemManager.GetRuntimeItem(Hull);
        if (typedItem == null || typedHull == null)
        {
            hullCoord = int2.zero;
            return false;
        }

        var itemHardpointType = GetHardpointType(typedItem, HardpointType.Tool);
        
        // Tools and thermal equipment can be installed anywhere on the ship
        // Search the whole ship for somewhere the item will fit
        if (itemHardpointType == HardpointType.Tool)
        {
            foreach (var hullCoord2 in GetHullInteriorShape(typedHull).Coordinates)
            {
                if (ItemFits(typedItem, typedHull, item, hullCoord2))
                {
                    hullCoord = hullCoord2;
                    return true;
                }
            }
        }
        
        // Everything else has to be equipped onto a hardpoint of the same type
        // Search the ship for an empty hardpoint that matches the type and shape of the item
        else
        {
            foreach (var hardpoint in typedHull.Hardpoints)
            {
                if(GetHardpointType(hardpoint, HardpointType.Hull) == itemHardpointType)
                {
                    var hardpointShape = ToShape(hardpoint.ShapeWidth, hardpoint.ShapeHeight, hardpoint.ShapeCells);
                    foreach (var hardpointCoord in hardpointShape.Coordinates)
                    {
                        var hullCoord2 = new int2(hardpoint.PositionX, hardpoint.PositionY) + hardpointCoord;
                        if (ItemFits(typedItem, typedHull, item, hullCoord2))
                        {
                            hullCoord = hullCoord2;
                            return true;
                        }
                    }
                }
            }
        }
        
        hullCoord = int2.zero;
        return false;
    }

    // Try to equip the given item anywhere it will fit, returns true when the item was successfully equipped
    public bool TryEquip(EquippableItem item) => TryFindSpace(item, out var hullCoord) && TryEquip(item, hullCoord);

    // Try to equip the given item to the given location
    public bool TryEquip(EquippableItem item, int2 hullCoord)
    {
        // Don't allow equipping while deployed
        if (_active) return false;
        
        var typedItem = ItemManager.GetRuntimeItem(item);
        var typedHull = ItemManager.GetRuntimeItem(Hull);
        if (typedItem == null || typedHull == null) return false;
        var itemHardpointType = GetHardpointType(typedItem, HardpointType.Tool);
        var itemShape = GetItemShape(typedItem);

        if (!ItemFits(typedItem, typedHull, item, hullCoord)) return false;
        
        EquippedItem equippedItem;
        if (itemHardpointType == HardpointType.Tool)
        {
            if(IsCargoBay(typedItem))
            {
                if (IsDockingBay(typedItem))
                {
                    equippedItem = new EquippedDockingBay(ItemManager, item, hullCoord, this, $"{Name} Docking Bay {DockingBays.Count + 1}");
                    DockingBays.Add((EquippedDockingBay) equippedItem);
                }
                else
                {
                    equippedItem = new EquippedCargoBay(ItemManager, item, hullCoord, this, $"{Name} Cargo Bay {CargoBays.Count + 1}");
                    CargoBays.Add((EquippedCargoBay) equippedItem);
                }
            }
            else
            {
                equippedItem = new EquippedItem(ItemManager, item, hullCoord, this);
                Equipment.Add(equippedItem);
            }
        }
        else
        {
            equippedItem = new EquippedItem(ItemManager, item, hullCoord, this);
            Equipment.Add(equippedItem);
        }
        
        foreach (var b in equippedItem.Behaviors)
        {
            if (b is Weapon weapon)
                _weapons.Add(weapon);
            if(b is Capacitor capacitor)
                _capacitors.Add(capacitor);
            if(b is Reactor reactor)
                _reactors.Add(reactor);
            if(b is Radiator heatsink)
                _heatsinks.Add(heatsink);
            if (b is Shield shield)
                Shield = shield;
            if (b is Cockpit cockpit)
                Cockpit = cockpit;
            if (b is Sensor sensor)
                Sensor = sensor;
        }

        // equippedItem.OnOnline += () => ItemOnline.OnNext(equippedItem);
        // equippedItem.OnOffline += () => ItemOffline.OnNext(equippedItem);
            
        foreach (var i in itemShape.Coordinates)
        {
            var occupiedCoord = hullCoord + itemShape.Rotate(i, item.Rotation);
            // TODO: Track thermal mass of cargo bay contents as reactive property
            ThermalMass[occupiedCoord.x, occupiedCoord.y] += ItemManager.GetThermalMass(item) / itemShape.Coordinates.Length;
            GearOccupancy[occupiedCoord.x, occupiedCoord.y] = equippedItem;
        }
                
        Mass += ItemManager.GetMass(item);
        _orderedEquipment = Equipment.OrderBy(x => x.SortPosition).ToArray();
        return true;
    }

    public EquippedDockingBay TryDock(Ship ship)
    {
        //if (!IsDockingPermitted(ship.GetFactionRelationship(Faction), CurrentSecurityLevel.Value)) return null;
        
        var bay = DockingBays.FirstOrDefault(x => x.DockedShip == null);
        if (bay != null)
        {
            bay.DockedShip = ship;
            ship.SetParent(this);
            Zone.Entities.Remove(ship);
            ship.Deactivate();
            ship.Docked.OnNext(this);
        }

        return bay;
    }

    public bool TryUndock(Ship ship)
    {
        var bay = DockingBays.FirstOrDefault(x => x.DockedShip == ship);
        if (bay == null)
        {
            ItemManager.Log($"Ship {ship.Name} attempted to undock from {Name}, but it was not docked!");
            return false;
        }

        if (bay.Cargo.Any())
            return false;

        bay.DockedShip = null;
        ship.RemoveParent();
        Zone.Entities.Add(ship);
        ship.Activate();

        return true;
    }

    public bool CanConsumeEnergy(float energy)
    {
        var capEnergy = _capacitors.Sum(cap => cap.Charge);
        int onlineReactors = _reactors.Count(reactor=>reactor.Item.Online.Value);
        return capEnergy > energy || onlineReactors > 0;
    }

    public bool TryConsumeEnergy(float energy)
    {
        if (energy < .01f) return true;
        int chargedCapacitors;
        do
        {
            chargedCapacitors = _capacitors.Count(capacitor => capacitor.Charge > .01f);
            var chargeToRemove = energy;
            foreach (var cap in _capacitors)
            {
                if(cap.Charge > 0.01f)
                {
                    var chargeRemoved = min(chargeToRemove / chargedCapacitors, cap.Charge);
                    cap.AddCharge(-chargeRemoved);
                    energy -= chargeRemoved;
                }
            }
        } while (chargedCapacitors > 0 && energy > .01f);

        if (energy < .01f) return true;

        int onlineReactors = _reactors.Count(reactor=>reactor.Item.Online.Value);
        foreach (var reactor in _reactors)
        {
            if (reactor.Item.Online.Value)
            {
                reactor.ConsumeEnergy(energy / onlineReactors);
            }
        }

        return onlineReactors > 0;
    }

    private void AddChild(Entity entity)
    {
        Mass += entity.Mass;
        Children.Add(entity);
    }

    private void RemoveChild(Entity entity)
    {
        Mass -= entity.Mass;
        Children.Remove(entity);
    }
    
    public void SetParent(Entity parent)
    {
        Parent = parent;
        parent.AddChild(this);
    }

    public void RemoveParent()
    {
        if (Parent == null)
            return;

        Parent.RemoveChild(this);
        Parent = null;
    }

    public T GetBehavior<T>() where T : Behavior
    {
        foreach (var equippedItem in Equipment)
            if(equippedItem.Behaviors != null)
                foreach (var behavior in equippedItem.Behaviors)
                    if (behavior is T b)
                        return b;
        return null;
    }

    public IEnumerable<T> GetBehaviors<T>() where T : Behavior
    {
        foreach (var equippedItem in Equipment)
            if(equippedItem.Behaviors != null)
                foreach (var behavior in equippedItem.Behaviors)
                    if (behavior is T b)
                        yield return b;
    }

    public virtual void Update(float delta)
    {
        TargetRange = Target.Value == null ? -1 : length(Position - Target.Value.Position);

        var localSecurityLevel = Zone.GetSecurityLevel(Position.xz);
        if (CurrentSecurityLevel.Value != localSecurityLevel) CurrentSecurityLevel.Value = localSecurityLevel;

        foreach (var v in VisibilitySources.Keys.ToArray())
        {
            VisibilitySources[v] = AetheriaMath.Decay(VisibilitySources[v], ItemManager.GameplaySettings.VisibilityDecay, delta);
 
            if (VisibilitySources[v] < 0.1f) VisibilitySources.Remove(v);
        }

        UpdateTemperature(delta);

        foreach (var item in _orderedEquipment) item.UpdatePerformance();

        if (_active)
        {
            foreach (var entity in Zone.Entities)
            {
                var previousHostility = EntityHostility[entity];
                var newHostility = IsHostileTo(entity);
                if (newHostility != previousHostility)
                    EntityHostility[entity] = newHostility;
            }
            
            if(Cockpit != null)
            {
                var cockpitTemp = Cockpit.Temperature;
                if (cockpitTemp > ItemManager.GameplaySettings.HeatstrokeTemperature)
                {
                    var previous = Heatstroke;
                    Heatstroke = saturate(
                        Heatstroke +
                        pow(cockpitTemp - ItemManager.GameplaySettings.HeatstrokeTemperature, ItemManager.GameplaySettings.HeatstrokeExponent) *
                        ItemManager.GameplaySettings.HeatstrokeMultiplier * delta);
                    if(previous < ItemManager.GameplaySettings.SevereHeatstrokeRiskThreshold && Heatstroke > ItemManager.GameplaySettings.SevereHeatstrokeRiskThreshold)
                        HeatstrokeRisk.OnNext(Unit.Default);
                    if(Heatstroke > .99)
                    {
                        HeatstrokeDeath.OnNext(Unit.Default);
                        Deactivate();
                    }
                }
                else
                {
                    Heatstroke = saturate(Heatstroke - ItemManager.GameplaySettings.HeatstrokeRecoverySpeed * delta);
                }

                if (cockpitTemp < ItemManager.GameplaySettings.HypothermiaTemperature)
                {
                    var previous = Hypothermia;
                    Hypothermia = saturate(
                        Hypothermia +
                        pow(ItemManager.GameplaySettings.HypothermiaTemperature - cockpitTemp, ItemManager.GameplaySettings.HypothermiaExponent) *
                        ItemManager.GameplaySettings.HypothermiaMultiplier * delta);
                    if(previous < ItemManager.GameplaySettings.SevereHeatstrokeRiskThreshold && Heatstroke > ItemManager.GameplaySettings.SevereHeatstrokeRiskThreshold)
                        HypothermiaRisk.OnNext(Unit.Default);
                    if(Hypothermia > .99)
                    {
                        HypothermiaDeath.OnNext(Unit.Default);
                        Deactivate();
                    }
                }
                else
                {
                    Hypothermia = saturate(Hypothermia - ItemManager.GameplaySettings.HypothermiaRecoverySpeed * delta);
                }
            }

            for (var i = 0; i < _activeConsumables.Count; i++)
            {
                _activeConsumables[i].Update(delta);
                if(_activeConsumables[i].RemainingDuration < 0) _activeConsumables.RemoveAt(i--);
            }

            foreach (var equippedItem in _orderedEquipment)
            {
                equippedItem.Update(delta);
            }

            foreach (var message in Messages.Keys.ToArray())
            {
                Messages[message] = Messages[message] - delta;
                if (Messages[message] < 0)
                    Messages.Remove(message);
            }
        }
        foreach(var child in Children)
            child.Update(delta);

        if (Parent != null)
        {
            Position = Parent.Position;
            Velocity = Parent.Velocity;
        }
        else Position.y = Zone.GetHeight(Position.xz) + (float)(ItemManager.GetRuntimeItem(Hull)?.HullGridOffset ?? 0);
    }

    private void UpdateTemperature(float delta)
    {
        var typedHull = ItemManager.GetRuntimeItem(Hull);
        var hullShape = GetHullShape(typedHull);
        var hullInterior = GetHullInteriorShape(typedHull);
        var hullConductivity = (float)(typedHull?.Conductivity ?? 1);
        
        MaxTemp = Single.MinValue;
        MinTemp = Single.MaxValue;
        
        //float[,] newTemp = new float[hullShape.Width,hullShape.Height];
        var radiation = 0f;
        foreach (var v in hullShape.Coordinates)
        {
            var temp = Temperature[v.x, v.y];
            var totalTemp = temp / ItemManager.GameplaySettings.HeatConductionMultiplier;
            var totalConductivity = 1f / ItemManager.GameplaySettings.HeatConductionMultiplier;
            
            if (hullShape[int2(v.x - 1, v.y)])
            {
                var conductivity = (GearOccupancy[v.x, v.y]?.Conductivity ?? 1) *
                                   (GearOccupancy[v.x - 1, v.y]?.Conductivity ?? 1) *
                                   (HullConductivity[v.x - 1, v.y].x ? hullConductivity : 1 / hullConductivity) *
                                   (ThermalMass[v.x - 1, v.y] / ThermalMass[v.x, v.y]);
                totalConductivity += conductivity;
                totalTemp += Temperature[v.x - 1, v.y] * conductivity;
            }

            if (hullShape[int2(v.x + 1, v.y)])
            {
                var conductivity = (GearOccupancy[v.x, v.y]?.Conductivity ?? 1) *
                                   (GearOccupancy[v.x + 1, v.y]?.Conductivity ?? 1) *
                                   (HullConductivity[v.x, v.y].x ? hullConductivity : 1 / hullConductivity) *
                                   (ThermalMass[v.x + 1, v.y] / ThermalMass[v.x, v.y]);
                totalConductivity += conductivity;
                totalTemp += Temperature[v.x + 1, v.y] * conductivity;
            }


            if (hullShape[int2(v.x, v.y - 1)])
            {
                var conductivity = (GearOccupancy[v.x, v.y]?.Conductivity ?? 1) *
                                   (GearOccupancy[v.x, v.y - 1]?.Conductivity ?? 1) * 
                                   (HullConductivity[v.x, v.y - 1].y ? hullConductivity : 1 / hullConductivity) *
                                   (ThermalMass[v.x, v.y - 1] / ThermalMass[v.x, v.y]);
                totalConductivity += conductivity;
                totalTemp += Temperature[v.x, v.y - 1] * conductivity;
            }


            if (hullShape[int2(v.x, v.y + 1)])
            {
                var conductivity = (GearOccupancy[v.x, v.y]?.Conductivity ?? 1) *
                                   (GearOccupancy[v.x, v.y + 1]?.Conductivity ?? 1) * 
                                   (HullConductivity[v.x, v.y].y ? hullConductivity : 1 / hullConductivity) *
                                   (ThermalMass[v.x, v.y + 1] / ThermalMass[v.x, v.y]);
                totalConductivity += conductivity;
                totalTemp += Temperature[v.x, v.y + 1] * conductivity;
            }
            
            NewTemperature[v.x, v.y] = totalTemp / totalConductivity;

            var r = 0f;
            // For all cells on the border of the entity, radiate some heat into space, increasing the visibility of the ship
            if (Parent==null && !hullInterior[v])
            {
                var rad = pow(NewTemperature[v.x, v.y], ItemManager.GameplaySettings.HeatRadiationExponent) *
                          ItemManager.GameplaySettings.HeatRadiationMultiplier;
                NewTemperature[v.x, v.y] -= rad * delta;
                r += rad;
            }

            radiation += r;
            
            if(float.IsNaN(NewTemperature[v.x, v.y]) || NewTemperature[v.x, v.y] < 0)
                ItemManager.Log("HOUSTON, WE HAVE A PROBLEM!");

            if (NewTemperature[v.x, v.y] < MinTemp)
                MinTemp = NewTemperature[v.x, v.y];
            
            if (NewTemperature[v.x, v.y] > MaxTemp)
                MaxTemp = NewTemperature[v.x, v.y];
        }

        VisibilitySources[this] = radiation;
        var swap = Temperature;
        Temperature = NewTemperature;
        NewTemperature = swap;
    }

    public void SetMessage(string message)
    {
        Messages[message] = ItemManager.GameplaySettings.MessageDuration;
    }
}

public class ConsumableItemEffect
{
    public float RemainingDuration { get; private set; }
    public Entity Entity { get; }
    public ConsumableItem Item { get; }
    public Behavior[] Behaviors { get; }
    public AetheriaRuntimeCatalogItem RuntimeItem { get; }
    public float Duration => Math.Max(_duration, float.Epsilon);

    private readonly float _duration;
    private readonly BezierCurve _effectiveness;

    public ConsumableItemEffect(ConsumableItem item, Entity entity)
    {
        Item = item;
        Entity = entity;
        RuntimeItem = entity.ItemManager.GetRuntimeItem(item);
        _duration = RuntimeItem?.Duration > 0 ? (float)RuntimeItem.Duration : 0;
        _effectiveness = CreateEffectivenessCurve(RuntimeItem);
        RemainingDuration = _duration;

        Behaviors = entity.ItemManager.CreateRuntimeBehaviors(this);
    }

    public void Update(float delta)
    {
        foreach (var behavior in Behaviors)
            if(behavior is IAlwaysUpdatedBehavior alwaysUpdatedBehavior) alwaysUpdatedBehavior.Update(delta);

        foreach (var behavior in Behaviors)
        {
            if (!behavior.Execute(delta))
                break;
        }

        RemainingDuration -= delta;
    }

    public float Evaluate(PerformanceStat stat)
    {
        var duration = Math.Max(_duration, float.Epsilon);
        var effectiveness = _effectiveness.Evaluate((duration - RemainingDuration) / duration);
        var quality = pow(Item.Quality, stat.QualityExponent);

        var result = lerp(stat.Min, stat.Max, effectiveness * quality);
        
        if (float.IsNaN(result))
            return stat.Min;
        return result;
    }

    private static BezierCurve CreateEffectivenessCurve(AetheriaRuntimeCatalogItem item)
    {
        if (item?.EffectivenessCurveKeys != null && item.EffectivenessCurveKeys.Count > 0)
        {
            return new BezierCurve
            {
                Keys = item.EffectivenessCurveKeys
                    .Select(key => new float4(
                        (float)key.Time,
                        (float)key.Value,
                        (float)key.InTangent,
                        (float)key.OutTangent))
                    .ToArray()
            };
        }

        return new BezierCurve
        {
            Keys = new[] { new float4(0, 1, 0, 0), new float4(1, 1, 0, 0) }
        };
    }
}

public class EquippedItem
{
    public int SortPosition;
    public EquippableItem EquippableItem;
    public int2 Position;

    private bool _thermalOnline;
    private bool _durabilityOnline;
    public WwiseMetaSoundBank SoundBank;
    
    public Behavior[] Behaviors { get; }
    public Dictionary<int, BehaviorGroup> BehaviorGroups { get; }
    public float Conductivity { get; }
    public float MaxDurability { get; }
    public float ThermalResilience { get; }
    public float ThermalPerformance { get; private set; }
    public float ThermalExponent { get; }
    public float DurabilityPerformance { get; private set; }
    public float DurabilityExponent { get; }
    public float Wear { get; private set; }
    public Shape InsetShape { get; }
    public Entity Entity { get; }
    public AetheriaRuntimeCatalogItem RuntimeItem { get; }

    public ReactiveProperty<bool> ThermalOnline { get; } = new ReactiveProperty<bool>(false);
    public ReactiveProperty<bool> DurabilityOnline { get; } = new ReactiveProperty<bool>(false);
    public ReadOnlyReactiveProperty<bool> Online { get; }
    public ReactiveProperty<bool> Enabled { get; } = new ReactiveProperty<bool>(true);
    public ReadOnlyReactiveProperty<bool> Active { get; }
    public ItemManager ItemManager { get; }

    public Subject<uint> AudioEvents { get; } = new Subject<uint>();
    public Subject<(uint id, float v)> AudioParameters { get; } = new Subject<(uint id, float v)>();
    public Dictionary<uint, float> AudioParameterValues { get; } = new Dictionary<uint, float>();

    private readonly BezierCurve _thermalPerformanceCurve;
    private readonly EquippedItemAudioStatBinding[] _audioStats;
    private float oldTemperature;
    public float Temperature
    {
        get
        {
            float sum = 0;
            foreach (var x in InsetShape.Coordinates) sum += Entity.Temperature[x.x, x.y];
            return sum/InsetShape.Coordinates.Length;
        }
    }

    public void FireAudioEvent(uint eventId, bool skipVerify = false)
    {
        if(SoundBank != null && (skipVerify || SoundBank.IncludedEvents.Any(o => o.Id == eventId)))
            AudioEvents.OnNext(eventId);
    }

    public void FireAudioEvent(WwiseSoundBinding soundBinding)
    {
        FireAudioEvent(soundBinding.PlayEvent);
    }

    public void PlaySound(WwiseLoopingSoundBinding soundBinding)
    {
        FireAudioEvent(soundBinding.PlayEvent);
    }

    public void StopSound(WwiseLoopingSoundBinding soundBinding)
    {
        FireAudioEvent(soundBinding.StopEvent);
    }

    public void FireAudioEvent(WeaponAudioEvent weaponAudioEvent)
    {
        if (SoundBank == null) return;
        var eventObject = SoundBank.GetEvent(weaponAudioEvent);
        if (eventObject == null)
        {
            ItemManager.Log($"Attempted to trigger {Enum.GetName(typeof(WeaponAudioEvent), weaponAudioEvent)} weapon audio event, but the soundbank doesn't support it!");
            return;
        }
        FireAudioEvent(eventObject.Id, true);
    }

    public void FireAudioEvent(ChargedWeaponAudioEvent weaponAudioEvent)
    {
        if (SoundBank == null) return;
        var eventObject = SoundBank.GetEvent(weaponAudioEvent);
        if (eventObject == null)
        {
            ItemManager.Log($"Attempted to trigger {Enum.GetName(typeof(ChargedWeaponAudioEvent), weaponAudioEvent)} weapon audio event, but the soundbank doesn't support it!");
            return;
        }
        FireAudioEvent(eventObject.Id, true);
    }

    public void SetAudioParameter(uint id, float v, bool skipVerify = false)
    {
        if (SoundBank != null && (skipVerify || SoundBank.GameParameters.Any(o => o.Id == id)))
        {
            AudioParameterValues[id] = v;
            AudioParameters.OnNext((id, v));
        }
    }

    // public void SetAudioParameter(WwiseParameterBinding binding)
    // {
    //     FireAudioEvent(binding.Parameter);
    // }

    public void SetAudioParameter(SpecialAudioParameter p, float v)
    {
        if (SoundBank == null) return;
        var metaObject = SoundBank.GetParameter(p);
        if (metaObject == null)
        {
            ItemManager.Log($"Attempted to set {Enum.GetName(typeof(ChargedWeaponAudioEvent), p)} audio parameter, but the soundbank doesn't support it!");
            return;
        }
        SetAudioParameter(metaObject.Id, v, true);
    }

    public EquippedItem(ItemManager itemManager, EquippableItem item, int2 position, Entity entity)
    {
        ItemManager = itemManager;
        Entity = entity;
        EquippableItem = item;
        Position = position;
        RuntimeItem = ItemManager.GetRuntimeItem(item);
        Conductivity = RuntimeItem != null ? (float)RuntimeItem.Conductivity : 0;
        MaxDurability = RuntimeItem?.Durability > 0 ? (float)RuntimeItem.Durability : Math.Max(item.Durability, 1f);
        ThermalResilience = RuntimeItem?.ThermalResilience > 0 ? (float)RuntimeItem.ThermalResilience : 1;
        _thermalPerformanceCurve = CreateThermalPerformanceCurve(RuntimeItem);
        _audioStats = CreateAudioStatBindings(RuntimeItem);
        ThermalExponent = lerp(
            ItemManager.GameplaySettings.ThermalQualityMin,
            ItemManager.GameplaySettings.ThermalQualityMax,
            pow(item.Quality, ItemManager.GameplaySettings.ThermalQualityExponent));
        DurabilityExponent = lerp(
            ItemManager.GameplaySettings.DurabilityQualityMin,
            ItemManager.GameplaySettings.DurabilityQualityMax,
            pow(item.Quality, ItemManager.GameplaySettings.DurabilityQualityExponent));
        var hullShape = ItemManager.GetRuntimeShape(entity.Hull);
        var itemShape = ItemManager.GetRuntimeShape(item);
        InsetShape = hullShape.Inset(itemShape, position, item.Rotation);
        if (Entity.Temperature != null) oldTemperature = Temperature;

        Online = new ReadOnlyReactiveProperty<bool>(ThermalOnline
            .CombineLatest(DurabilityOnline, (thermal, durability) => thermal && durability).DistinctUntilChanged());
        Active = new ReadOnlyReactiveProperty<bool>(Enabled
            .CombineLatest(Online, (enabled, online) => enabled && online).DistinctUntilChanged());
        

        Behaviors = ItemManager.CreateRuntimeBehaviors(this);

        BehaviorGroups = Behaviors
            .GroupBy(b => b.Data.Group)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => new BehaviorGroup
            {
                Behaviors = g.ToArray()
            });

        foreach (var behavior in Behaviors)
        {
            if (behavior is IOrderedBehavior orderedBehavior)
                SortPosition = orderedBehavior.Order;
            if(behavior is IPopulationAssignment populationAssignment)
                entity.PopulationAssignments.Add(populationAssignment);
        }
    }

    public float Evaluate(PerformanceStat stat)
    {
        var heat = pow(ThermalPerformance, ThermalExponent * stat.HeatExponentMultiplier);
        var durability = pow(DurabilityPerformance, DurabilityExponent * stat.DurabilityExponentMultiplier);
        var quality = pow(EquippableItem.Quality, stat.QualityExponent);

        var scaleModifier = 1.0f;
        var scaleModifiers = stat.GetScaleModifiers(Entity).Values;
        foreach (var value in scaleModifiers) scaleModifier *= value;

        float constantModifier = 0;
        foreach (var value in stat.GetConstantModifiers(Entity).Values) constantModifier += value;

        var result = lerp(stat.Min, stat.Max, durability * quality * heat) * scaleModifier + constantModifier;
        if (float.IsNaN(result))
            return stat.Min;
        return result;
    }

    public void AddHeat(float heat, bool ignoreThermalMass = false)
    {
        foreach(var hullCoord in InsetShape.Coordinates)
            Entity.AddHeat(hullCoord, heat / InsetShape.Coordinates.Length, ignoreThermalMass);
    }

    public void UpdatePerformance()
    {        
        var temp = Temperature;
        ThermalPerformance = EvaluateThermalPerformance(temp);
        var deltaTemp = math.abs(temp - oldTemperature);
        DurabilityPerformance = EquippableItem.Durability / MaxDurability;
        var performanceThreshold = Entity.Settings.ShutdownPerformance;
        Wear = (1 - pow(ThermalPerformance,
                (1 - pow(EquippableItem.Quality, ItemManager.GameplaySettings.QualityWearExponent)) *
                ItemManager.GameplaySettings.ThermalWearExponent) +
                deltaTemp * ItemManager.GameplaySettings.DeltaTempWearExponent            
            ) * MaxDurability / ThermalResilience;
        ThermalOnline.Value = ThermalPerformance > performanceThreshold || Entity.OverrideShutdown && EquippableItem.OverrideShutdown;
        DurabilityOnline.Value = EquippableItem.Durability > .01f;
        oldTemperature = temp;
    }

    private float EvaluateThermalPerformance(float temperature)
    {
        if (_thermalPerformanceCurve == null ||
            RuntimeItem == null ||
            RuntimeItem.MaximumTemperature <= RuntimeItem.MinimumTemperature)
        {
            return 1;
        }

        var t = unlerp((float)RuntimeItem.MinimumTemperature, (float)RuntimeItem.MaximumTemperature, temperature);
        return saturate(_thermalPerformanceCurve.Evaluate(t));
    }

    private static BezierCurve CreateThermalPerformanceCurve(AetheriaRuntimeCatalogItem item)
    {
        if (item?.ThermalPerformanceCurveKeys == null || item.ThermalPerformanceCurveKeys.Count == 0)
        {
            return null;
        }

        return new BezierCurve
        {
            Keys = item.ThermalPerformanceCurveKeys
                .Select(key => new float4((float)key.Time, (float)key.Value, (float)key.InTangent, (float)key.OutTangent))
                .ToArray()
        };
    }

    public void Update(float delta)
    {
        foreach (var audioStat in _audioStats)
        {
            SetAudioParameter(audioStat.Parameter, Evaluate(audioStat.Stat));
        }
        
        if (Active.Value)
        {
            foreach (var group in BehaviorGroups.Values)
            {
                foreach (var behavior in group.Behaviors)
                {
                    if (!behavior.Execute(delta))
                        break;
                }
            }
        }
        
        foreach (var behavior in Behaviors)
            if(behavior is IAlwaysUpdatedBehavior alwaysUpdatedBehavior) alwaysUpdatedBehavior.Update(delta);
    }

    public T GetBehavior<T>() where T : class
    {
        foreach (var behavior in Behaviors)
            if (behavior is T b)
                return b;
        return null;
    }

    private static EquippedItemAudioStatBinding[] CreateAudioStatBindings(AetheriaRuntimeCatalogItem item)
    {
        if (item?.AudioStats != null)
        {
            return item.AudioStats
                .Select(audioStat => new EquippedItemAudioStatBinding(
                    audioStat.Parameter,
                    CreatePerformanceStat(audioStat.Stat)))
                .ToArray();
        }

        return Array.Empty<EquippedItemAudioStatBinding>();
    }

    private static PerformanceStat CreatePerformanceStat(AetheriaRuntimePerformanceStat stat)
    {
        return new PerformanceStat
        {
            Min = (float)stat.Min,
            Max = (float)stat.Max,
            HeatExponentMultiplier = (float)stat.HeatExponentMultiplier,
            DurabilityExponentMultiplier = (float)stat.DurabilityExponentMultiplier,
            QualityExponent = (float)stat.QualityExponent
        };
    }

    private readonly struct EquippedItemAudioStatBinding
    {
        public EquippedItemAudioStatBinding(uint parameter, PerformanceStat stat)
        {
            Parameter = parameter;
            Stat = stat;
        }

        public uint Parameter { get; }
        public PerformanceStat Stat { get; }
    }
}

public class EquippedCargoBay : EquippedItem
{
    public readonly ReactiveDictionary<ItemInstance, int2> Cargo = new ReactiveDictionary<ItemInstance, int2>();

    public readonly ItemInstance[,] Occupancy;

    public readonly Dictionary<Guid, List<ItemInstance>> ItemsOfType = new Dictionary<Guid, List<ItemInstance>>();

    public Shape InteriorShape { get; }

    public float Mass { get; private set; }
    public float ThermalMass { get; private set; }
    public string Name { get; }

    public Shape UnoccupiedSpace
    {
        get
        {
            var unoccupied = new Shape(InteriorShape.Width, InteriorShape.Height);
            foreach (var v in unoccupied.AllCoordinates)
                unoccupied[v] = Occupancy[v.x, v.y] == null;
            return unoccupied;
        }
    }

    public EquippedCargoBay(ItemManager itemManager, EquippableItem item, int2 position, Entity entity, string name) : base(itemManager, item, position, entity)
    {
        var typedCargoBay = ItemManager.GetRuntimeItem(EquippableItem);
        InteriorShape = ToShape(
            typedCargoBay?.InteriorShapeWidth ?? 1,
            typedCargoBay?.InteriorShapeHeight ?? 1,
            typedCargoBay?.InteriorShapeCells);
        Name = name;

        Mass = ItemManager.GetMass(EquippableItem);
        ThermalMass = ItemManager.GetThermalMass(EquippableItem);
        
        Occupancy = new ItemInstance[InteriorShape.Width, InteriorShape.Height];
    }

    // Check whether the given item will fit when its origin is placed at the given coordinate
    public bool ItemFits(ItemInstance item, int2 cargoCoord)
    {
        var itemShape = GetItemShape(item);
        // Check every cell of the item's shape
        foreach (var i in itemShape.Coordinates)
        {
            // If there is an item already occupying that space, it won't fit
            var itemCargoCoord = cargoCoord + itemShape.Rotate(i, item.Rotation);
            if (!InteriorShape[itemCargoCoord] || (Occupancy[itemCargoCoord.x, itemCargoCoord.y] != null && Occupancy[itemCargoCoord.x, itemCargoCoord.y] != item)) return false;
        }

        return true;
    }
    
    public bool TryFindSpace(ItemInstance item)
    {
        if (item is SimpleCommodity simpleCommodity)
            return TryFindSpace(simpleCommodity, out _);
        if (item is CraftedItemInstance craftedItem)
            return TryFindSpace(craftedItem, out _);
        return false;
    }

    // Tries to find a place to put the given items in the inventory
    // Will attempt to fill existing item stacks first
    // Returns true only when ALL of the items have places to go
    public bool TryFindSpace(SimpleCommodity item, out List<int2> positions)
    {
        positions = new List<int2>();
        var maxStack = GetMaxStack(item);
        var remainingQuantity = item.Quantity;
        
        // For simple commodities, search for existing item stacks to add to
        foreach (var cargoItem in Cargo.Keys)
        {
            if (item.Data != cargoItem.Data) continue;
            
            var cargoCommodity = (SimpleCommodity) cargoItem;
            if (cargoCommodity.Quantity >= maxStack) continue;
            
            // Subtract remaining space in existing stack from remaining quantity
            remainingQuantity -= min(maxStack - cargoCommodity.Quantity, remainingQuantity);
            positions.Add(Cargo[cargoItem]);
            
            // If we've moved all of the items into existing stacks, no need to search for empty space!
            if (remainingQuantity == 0) return true;
        }
        
        // TODO: Try alternate item rotations / use Shape.FitsWithin
        // Search all the space in the cargo bay for an empty space where the item fits
        foreach (var cargoCoord in InteriorShape.Coordinates)
        {
            if (ItemFits(item, cargoCoord))
            {
                positions.Add(cargoCoord);
                return true;
            }
        }

        return false;
    }

    // Searches the cargo bay for a position where the item will fit, returns true when found
    public bool TryFindSpace(CraftedItemInstance item, out int2 position)
    {
        // Search all the space in the cargo bay for an empty space where the item fits
        foreach (var cargoCoord in InteriorShape.Coordinates)
        {
            if (ItemFits(item, cargoCoord))
            {
                position = cargoCoord;
                return true;
            }
        }

        position = int2.zero;
        return false;
    }
    
    public bool TryStore(ItemInstance item)
    {
        if (item is SimpleCommodity simpleCommodity)
            return TryStore(simpleCommodity);
        if (item is CraftedItemInstance craftedItem)
            return TryStore(craftedItem);
        return false;
    }

    public bool TryStore(ItemInstance item, int2 cargoCoord)
    {
        if (item is SimpleCommodity simpleCommodity)
            return TryStore(simpleCommodity, cargoCoord);
        if (item is CraftedItemInstance craftedItem)
            return TryStore(craftedItem, cargoCoord);
        return false;
    }

    // Attempts to store all of the given item anywhere in the inventory
    // Will attempt to fill existing item stacks first
    // Returns true only when ALL of the items are successfully stored
    public bool TryStore(SimpleCommodity item)
    {
        TryFindSpace(item, out var positions);
        foreach (var position in positions)
        {
            if (TryStore(item, position)) return true;
        }

        return false;
    }

    // Try to store the given commodity at the given position
    // If there's a stack at the given position it will be added to
    // Returns true only when ALL of the items are successfully stored
    public bool TryStore(SimpleCommodity item, int2 cargoCoord)
    {
        var itemShape = GetItemShape(item);
        var maxStack = GetMaxStack(item);
        if (ItemFits(item, cargoCoord))
        {
            foreach (var p in itemShape.Coordinates)
            {
                var pos = cargoCoord + itemShape.Rotate(p, item.Rotation);
                Occupancy[pos.x, pos.y] = item;
            }
            Cargo[item] = cargoCoord;
            
            if(!ItemsOfType.ContainsKey(item.Data.ItemId))
                ItemsOfType[item.Data.ItemId] = new List<ItemInstance>();
            ItemsOfType[item.Data.ItemId].Add(item);
        }
        else if (Occupancy[cargoCoord.x, cargoCoord.y] is SimpleCommodity cargoCommodity && cargoCommodity.Data == item.Data)
        {
            if (cargoCommodity.Quantity + item.Quantity <= maxStack)
            {
                cargoCommodity.Quantity += item.Quantity;
            }
            else
            {
                var quantityTransferred = maxStack - cargoCommodity.Quantity;
                item.Quantity -= quantityTransferred;
                cargoCommodity.Quantity = maxStack;
                
                Mass += GetUnitMass(item) * quantityTransferred;
                ThermalMass += GetUnitThermalMass(item) * quantityTransferred;
                return false;
            }
        }
        else return false;
        
        Mass += ItemManager.GetMass(item);
        ThermalMass += ItemManager.GetThermalMass(item);
        return true;
    }

    // Try to store the given item anywhere it will fit, returns true when the item was successfully stored
    public bool TryStore(CraftedItemInstance item) => TryFindSpace(item, out var position) && TryStore(item, position);

    // Try to store the given item at the given position, returns true when the item was successfully stored
    public bool TryStore(CraftedItemInstance item, int2 cargoCoord)
    {
        if (!ItemFits(item, cargoCoord)) return false;
        
        var itemShape = GetItemShape(item);
        foreach (var p in itemShape.Coordinates)
        {
            var pos = cargoCoord + itemShape.Rotate(p, item.Rotation);
            Occupancy[pos.x, pos.y] = item;
        }
        Cargo[item] = cargoCoord;
        
        if(!ItemsOfType.ContainsKey(item.Data.ItemId))
            ItemsOfType[item.Data.ItemId] = new List<ItemInstance>();
        ItemsOfType[item.Data.ItemId].Add(item);
        
        Mass += ItemManager.GetMass(item);
        ThermalMass += ItemManager.GetThermalMass(item);

        return true;
    }

    public SimpleCommodity Remove(SimpleCommodity item, int quantity)
    {
        if (!Cargo.ContainsKey(item))
        {
            ItemManager.Log("Attempted to remove item from a cargo bay that it wasn't even in! Something went wrong here!");
            return null;
        }
        if(quantity >= item.Quantity)
        {
            foreach(var v in InteriorShape.Coordinates)
                if (Occupancy[v.x, v.y] == item)
                    Occupancy[v.x, v.y] = null;

            Cargo.Remove(item);
            ItemsOfType[item.Data.ItemId].Remove(item);
            if (!ItemsOfType[item.Data.ItemId].Any())
                ItemsOfType.Remove(item.Data.ItemId);

            Mass -= ItemManager.GetMass(item);
            ThermalMass -= ItemManager.GetThermalMass(item);

            return item;
        }

        item.Quantity -= quantity;
        Mass -= GetUnitMass(item) * quantity;
        ThermalMass -= GetUnitThermalMass(item) * quantity;
        return new SimpleCommodity{Data = item.Data, Quantity = quantity, Rotation = item.Rotation};
    }

    public void Remove(CraftedItemInstance item)
    {
        if (!Cargo.ContainsKey(item))
        {
            ItemManager.Log("Attempted to remove item from a cargo bay that it wasn't even in! Something went wrong here!");
            return;
        }
        foreach(var v in InteriorShape.Coordinates)
            if (Occupancy[v.x, v.y] == item)
                Occupancy[v.x, v.y] = null;
        
        Cargo.Remove(item);
        ItemsOfType[item.Data.ItemId].Remove(item);
        if (!ItemsOfType[item.Data.ItemId].Any())
            ItemsOfType.Remove(item.Data.ItemId);
        
        Mass -= ItemManager.GetMass(item);
        ThermalMass -= ItemManager.GetThermalMass(item);
    }

    public void Remove(ItemInstance item)
    {
        if (item is SimpleCommodity simpleCommodity)
            Remove(simpleCommodity, simpleCommodity.Quantity);
        if (item is CraftedItemInstance craftedItem)
            Remove(craftedItem);
    }

    private Shape GetItemShape(ItemInstance item)
    {
        var typedItem = ItemManager.GetRuntimeItem(item);
        return ToShape(typedItem?.ShapeWidth ?? 1, typedItem?.ShapeHeight ?? 1, typedItem?.ShapeCells);
    }

    private int GetMaxStack(SimpleCommodity item)
    {
        var typedItem = ItemManager.GetRuntimeItem(item);
        return typedItem != null && typedItem.MaxStack > 0 ? typedItem.MaxStack : 1;
    }

    private float GetUnitMass(ItemInstance item)
    {
        var typedItem = ItemManager.GetRuntimeItem(item);
        return typedItem != null ? (float)typedItem.Mass : 0f;
    }

    private float GetUnitThermalMass(ItemInstance item)
    {
        var typedItem = ItemManager.GetRuntimeItem(item);
        return typedItem != null ? (float)(typedItem.Mass * typedItem.SpecificHeat) : 0f;
    }

    private static Shape ToShape(int width, int height, IReadOnlyList<AetheriaRuntimeShapeCell> cells)
    {
        var shape = new Shape(Math.Max(width, 1), Math.Max(height, 1));
        if (cells == null) return shape;

        foreach (var cell in cells)
            shape[new int2(cell.X, cell.Y)] = true;

        return shape;
    }
    
    public bool TryTransferItem(EquippedCargoBay target, SimpleCommodity item, int quantity)
    {
        if (!Cargo.ContainsKey(item))
        {
            ItemManager.Log("Attempted to remove item from a cargo bay that it wasn't even in! Something went wrong here!");
            return false;
        }

        var oldPos = Cargo[item];
        var newItem = Remove(item, quantity);
        
        if (target.TryStore(item)) return true;
        
        // Failed to transfer full quantity, move the remaining items back to their old slot
        TryStore(newItem, oldPos);
        return false;
    }
    
    public bool TryTransferItem(EquippedCargoBay target, CraftedItemInstance item)
    {
        if (!Cargo.ContainsKey(item))
        {
            ItemManager.Log("Attempted to remove item from a cargo bay that it wasn't even in! Something went wrong here!");
            return false;
        }
        
        if (!target.TryStore(item)) return false;
        Remove(item);
        return true;
    }
}

public class EquippedDockingBay : EquippedCargoBay
{
    public Ship DockedShip;
    public int2 MaxSize { get; }

    public EquippedDockingBay(ItemManager itemManager, EquippableItem item, int2 position, Entity entity, string name) : base(itemManager, item, position, entity, name)
    {
        var typedDockingBay = ItemManager.GetRuntimeItem(EquippableItem);
        MaxSize = typedDockingBay != null
            ? new int2(typedDockingBay.DockingMaxSizeX, typedDockingBay.DockingMaxSizeY)
            : int2.zero;
    }
}

public class BehaviorGroup
{
    public Behavior[] Behaviors;

    public T GetBehavior<T>() where T : Behavior
    {
        foreach (var b in Behaviors)
        {
            if (!(b is T s)) continue;
            return s;
        }

        return null;
    }
    
    public T GetExposed<T>() where T : Behavior, IInteractiveBehavior
    {
        foreach (var b in Behaviors)
        {
            if (!(b is T s) || !((IInteractiveBehavior)b).Exposed) continue;
            return s;
        }

        return null;
    }
    
    //public IAnalogBehavior Axis;
}
