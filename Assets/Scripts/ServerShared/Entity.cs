/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using UniRx;
using CultMath;
using static CultMath.math;
using int2 = CultMath.int2;

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

    // Runtime-only IFF overrides for testing/manual marking and grudges; not persisted in packs.
    // When set for another entity, it decides IsHostileTo outright, ahead of the derived faction rules.
    // Reactive so a change can update EntityHostility and dependent grudges immediately, not on the next tick.
    private readonly ReactiveDictionary<Entity, bool> _iffOverrides = new ReactiveDictionary<Entity, bool>();

    public Entity Parent;
    public List<Entity> Children = new List<Entity>();
    public ReactiveProperty<Entity> Target = new ReactiveProperty<Entity>((Entity)null);

    // Cut 2 (docs/fire-control-cut.md): the stored aim-point slot. Written only by TrySelectTargetItem
    // below, and nulled whenever Target changes (subscribed in Activate). Do not read this field directly
    // outside Entity -- read ResolvedTargetItem, which re-validates on every call, so decay (or the item
    // leaving the target) drops the aim point the moment it is no longer earned, with no loop that has to
    // notice and clear it.
    public ReactiveProperty<EquippedItem> TargetItem = new ReactiveProperty<EquippedItem>((EquippedItem)null);

    public float3 LookDirection;
    
    public string Name;
    
    // public int Population;
    // public Dictionary<Guid, float> Personality = new Dictionary<Guid, float>();
    
    public readonly Dictionary<string, float> Messages = new Dictionary<string, float>();
    public readonly Dictionary<object, float> VisibilitySources = new Dictionary<object, float>();
    public readonly ReactiveDictionary<Entity, float> EntityInfoGathered = new ReactiveDictionary<Entity, float>();

    public (List<Weapon> weapons, List<EquippedItem> items)[] WeaponGroups;

    public List<IPopulationAssignment> PopulationAssignments = new List<IPopulationAssignment>();

    // Cut 2 (docs/stats-and-power-cut.md): the sole owner of every resolved (item, stat) value for this entity.
    // Constructed with the entity and reachable only from it -- nothing on the catalog side ever points back --
    // so it is collected with the entity instead of surviving it for the life of the process (§0.3, §1.1).
    public readonly StatResolver Resolver = new StatResolver();

    // Cut 3 (docs/stats-and-power-cut.md): the sole owner of every power grant for this entity, same lifecycle
    // reasoning as Resolver above (§1.2).
    public readonly PowerBus PowerBus;

    public EquippedItem[,] GearOccupancy;
    public HardpointData[,] Hardpoints;
    public float[,] Armor;
    public float[,] MaxArmor;
    
    private EquippedItem[] _orderedEquipment;
    private List<Weapon> _weapons = new List<Weapon>();
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
    
    public HullData HullData { get; }
    
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
    private Dictionary<Entity, List<IDisposable>> _grudgeSubscriptions = new Dictionary<Entity, List<IDisposable>>();

    // Grudges are a non-player behavior only: a player's own stance is always the operator's explicit call.
    private bool IsPlayerControlled => this is Ship {IsPlayerShip: true};

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
            if (!IsPlayerControlled) WatchForGrudge(entity);
        }
        _subscriptions.Add(Zone.Entities.ObserveAdd().Subscribe(add =>
        {
            EntityInfoGathered[add.Value] = 0;
            EntityHostility[add.Value] = IsHostileTo(add.Value);
            if (!IsPlayerControlled) WatchForGrudge(add.Value);
        }));
        _subscriptions.Add(Zone.Entities.ObserveRemove().Subscribe(remove =>
        {
            if (Target.Value == remove.Value) Target.Value = null;
            EntityInfoGathered.Remove(remove.Value);
            EntityHostility.Remove(remove.Value);
            VisibleEntities.Remove(remove.Value);
            VisibleEnemies.Remove(remove.Value);
            VisibleFriendlies.Remove(remove.Value);
            _iffOverrides.Remove(remove.Value);
            if (_grudgeSubscriptions.TryGetValue(remove.Value, out var grudgeSubs))
            {
                foreach (var s in grudgeSubs) s.Dispose();
                _grudgeSubscriptions.Remove(remove.Value);
            }
        }));

        // An override changing updates this entity's hostility toward that entity immediately,
        // rather than waiting for the next per-tick derived-hostility refresh.
        void RefreshHostilityFromOverride(Entity other)
        {
            if (EntityHostility.ContainsKey(other))
                EntityHostility[other] = IsHostileTo(other);
        }
        _subscriptions.Add(_iffOverrides.ObserveAdd().Subscribe(add => RefreshHostilityFromOverride(add.Key)));
        _subscriptions.Add(_iffOverrides.ObserveReplace().Subscribe(replace => RefreshHostilityFromOverride(replace.Key)));
        _subscriptions.Add(_iffOverrides.ObserveRemove().Subscribe(remove => RefreshHostilityFromOverride(remove.Key)));
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

        // Cut 2 (docs/fire-control-cut.md): a Target change nulls the aim point. Fires immediately on
        // subscribe with whatever Target already holds, which is harmless -- TargetItem starts null anyway.
        _subscriptions.Add(Target.Subscribe(_ => TargetItem.Value = null));

        // 9.3 (docs/fire-control-cut.md, Soul C3): a Target surviving Deactivate can point at an entity
        // this zone removed while inactive -- Deactivate disposes the Zone.Entities.ObserveRemove
        // subscription that is the only thing that nulls a stale Target, and EntityInfoGathered was just
        // rebuilt above from the zone's *current* membership, not from whatever Target still remembers.
        // Reconcile here, now that both the fresh EntityInfoGathered and the Target-change subscription
        // above are live (so this write also clears TargetItem through it): an active entity's Target
        // must always be an entity it has info on. Activate owns this reconciliation rather than
        // Deactivate nulling Target outright, because nulling on every dock would throw away a target
        // that is still alive and simply never changed while docked -- the common case, and not the one
        // that crashed.
        if (Target.Value != null && !EntityInfoGathered.ContainsKey(Target.Value))
            Target.Value = null;

        if(WeaponGroups.All(wg=>!wg.items.Any()))
            GenerateWeaponGroups();
    }

    // Cut 2 (docs/fire-control-cut.md): the only writer of TargetItem. Accepts null (clearing the aim
    // point) or an item belonging to the current Target that FireControl.IsRevealed resolves true for this
    // entity as observer. Returns whether the write took effect; a rejected non-null selection leaves
    // TargetItem unchanged.
    public bool TrySelectTargetItem(EquippedItem item)
    {
        if (item == null)
        {
            TargetItem.Value = null;
            return true;
        }

        if (Target.Value == null || item.Entity != Target.Value || !FireControl.IsRevealed(this, item))
            return false;

        TargetItem.Value = item;
        return true;
    }

    // The read path for TargetItem (see the field's own comment): null once the aimed item is no longer
    // revealed to this entity, or no longer belongs to the current Target, even though nothing wrote
    // TargetItem.Value at the moment that became true. Re-checked on every call; never cached.
    public EquippedItem ResolvedTargetItem =>
        TargetItem.Value != null && Target.Value == TargetItem.Value.Entity && FireControl.IsRevealed(this, TargetItem.Value)
            ? TargetItem.Value
            : null;

    // Cut 12.3 (docs/fire-control-cut.md): "armour absorbs first" -- the per-cell armour phase, split out of
    // what used to be Absorb's own first half (moved verbatim from DamageSchematic's per-cell body, Cut 3) so
    // Cut 12.3's fix batch (proportional multi-lane item absorption, below) can call the armour phase once per
    // cell -- always local to one lane, since armour never spans cells -- while deferring the item phase to
    // ItemAbsorb's own pool for EVERY occupied cell, not only ones shared with another lane: a lane that turns
    // out to be an item's only contributor still pools through a list of one (ItemAbsorb's own degenerate
    // case), never a separate solo path. ArmorDamage fires only for incoming > 0.
    public float ArmorAbsorb(int2 cell, float damage)
    {
        var d = damage;

        if (d > 0f)
        {
            var prevArmor = Armor[cell.x, cell.y];
            Armor[cell.x, cell.y] = max(prevArmor - d, 0);
            ArmorDamage.OnNext((cell, d));
            d = max(d - prevArmor, 0);
        }

        return d;
    }

    // Cut 12.3 fix batch (one apply path, operator ruling 2026-09-25 "do not expect items taking up multiple
    // cells to be an exception, this should be one code path"): the ONE function that decides an item's own
    // absorption, whatever the item's shape or however many lanes of one shot reach it. `incoming` holds every
    // contributing lane's own post-armour remainder for this resolve, one lane's worth included -- a
    // single-lane item is this rule's own degenerate case, not a separate function or a separate threshold.
    // The existing .1f threshold is decided once, on the POOLED total, never per contribution (a pooled path
    // that gated each contribution separately would let two shares under .1f each slip past an item that a
    // single .08f solo hit would already have stopped at). Absorbed durability is split back across `incoming`
    // in place, pro-rata to what each lane brought -- the divide below never sees total <= 0.1f, so it never
    // sees zero (the guard above already returned). ItemDamage fires once per contributing lane, reporting its
    // own INCOMING share -- not the post-clamp amount, the same convention ArmorAbsorb already uses -- and only
    // when that lane's own share is itself > 0, so a zero-deposit contributor (a lane whose armour ate its
    // whole share before reaching this item) never fires a phantom event. `item` is never null here: both
    // callers (`FireControl.ApplyPooled`'s `Resolve`, one lane's worth of contributions included, and
    // `FireControl.Detonate`'s own per-entity item-pool resolution, one covered cell's worth included) already
    // guarantee it -- a pool is only ever opened under a non-null `GearOccupancy` cell.
    public void ItemAbsorb(EquippedItem item, Span<float> incoming)
    {
        var total = 0f;
        for (var i = 0; i < incoming.Length; i++) total += incoming[i];
        if (total <= 0.1f) return;

        var before = item.EquippableItem.Durability;
        var absorbed = min(total, before);
        item.EquippableItem.Durability = max(before - absorbed, 0f);
        var fraction = absorbed / total;
        for (var i = 0; i < incoming.Length; i++)
        {
            if (incoming[i] > 0f) ItemDamage.OnNext((item, incoming[i]));
            incoming[i] -= incoming[i] * fraction;
        }
    }

    // Cut 12.3: moved verbatim from DamageSchematic's own tail (Cut 3) -- the >.1f threshold and the one
    // HullDamage event are unchanged. FireControl.Apply calls this once per resolved hit, with the summed
    // remainder every lane's own walk left over (Q12-3 = A: a lane's remainder goes to the hull wherever the
    // lane ends -- penetration exhausted, a gap, or the far side).
    public void DamageHull(float damage)
    {
        if (damage > .1f)
        {
            Hull.Durability -= damage;
            HullDamage.OnNext(damage);
        }
    }

    // Cut 12.1 (docs/fire-control-cut.md): the one schematic-frame owner. Maps a world-planar vector into this
    // entity's own schematic frame -- x = starboard, y = bow -- so FireControl.Apply's lane walk and
    // FireControl.Detonate's per-entity point conversion both read the same transform instead of each carrying
    // their own copy of forward/right.
    public float2 ToSchematic(float2 worldPlanar)
    {
        var forward = normalize(Direction);
        var right = float2(forward.y, -forward.x);
        return float2(dot(worldPlanar, right), dot(worldPlanar, forward));
    }

    // Cut 12.4(b) (docs/fire-control-cut.md, "Area, per entity"): the world<->schematic POINT owners, built on
    // ToSchematic's frame -- the entity's own position is the schematic's centre of mass. FireControl.Detonate
    // uses ToSchematicPoint to find where a blast sits in each candidate's own schematic (the host of a contact
    // or delayed blast included: it is treated like any other entity in the radius, its cells found again
    // here, not carried over from the commit). ToWorldPoint is the inverse, used once, to convert a contact or
    // delayed fuse point (found on the committed lane, in the host's schematic frame at commit) into the one
    // world point Detonate's single input shape takes -- the round trip through the host's own pose at arrival
    // is what fixes the host's damage to the commit (R4) while every bystander is judged live.
    public float2 ToSchematicPoint(float2 worldPlanar)
    {
        var hullData = ItemManager.GetData(Hull) as HullData;
        var cellSize = ItemManager.GameplaySettings.SchematicCellSize;
        return ToSchematic(worldPlanar - Position.xz) / cellSize + hullData.Shape.CenterOfMass;
    }

    public float2 ToWorldPoint(float2 schematicPoint)
    {
        var hullData = ItemManager.GetData(Hull) as HullData;
        var cellSize = ItemManager.GameplaySettings.SchematicCellSize;
        var local = (schematicPoint - hullData.Shape.CenterOfMass) * cellSize;
        var forward = normalize(Direction);
        var right = float2(forward.y, -forward.x);
        return Position.xz + local.x * right + local.y * forward;
    }

    // Another entity's stance toward THIS one, as far as this entity can perceive it: unknown (null)
    // until this entity detects them, mirroring the existing detection model (VisibleEntities, driven
    // by EntityInfoGathered crossing TargetDetectionInfoThreshold). Detected but not yet hostility-rated
    // by the other entity also reads as unknown.
    public bool? PerceivedStanceOf(Entity other) =>
        VisibleEntities.Contains(other) && other.EntityHostility.TryGetValue(this, out var hostile)
            ? hostile
            : (bool?) null;

    // Non-player entities hold a grudge: once another entity's stance toward THIS one turns hostile
    // (its own override or its derived rule) WHILE this entity detects it, this entity sets a sticky
    // hostile override on it back, event-driven off that entity's own EntityHostility changes and off
    // this entity's own detection (VisibleEntities) picking up an already-hostile entity. It never
    // forgives on its own; only a future utility-evaluation pass is meant to lift a grudge. Leaving the
    // zone clears overrides (see the Zone.Entities removal subscription above), so a grudge is also
    // cleared then -- a known gap, flagged as a follow-up rather than solved here.
    private void WatchForGrudge(Entity other)
    {
        void CheckGrudge(bool otherIsHostileToThis)
        {
            if (otherIsHostileToThis && VisibleEntities.Contains(other) && !_iffOverrides.ContainsKey(other))
                SetIff(other, true);
        }

        var subscriptions = new List<IDisposable>
        {
            other.EntityHostility.ObserveAdd()
                .Where(add => add.Key == this)
                .Subscribe(add => CheckGrudge(add.Value)),
            other.EntityHostility.ObserveReplace()
                .Where(replace => replace.Key == this)
                .Subscribe(replace => CheckGrudge(replace.NewValue)),
            // Detecting an entity whose stance toward this one is already hostile grudges it immediately.
            VisibleEntities.ObserveAdd()
                .Where(add => add.Value == other)
                .Subscribe(_ =>
                {
                    if (other.EntityHostility.TryGetValue(this, out var hostile))
                        CheckGrudge(hostile);
                })
        };
        _grudgeSubscriptions[other] = subscriptions;

        if (other.EntityHostility.TryGetValue(this, out var currentlyHostile))
            CheckGrudge(currentlyHostile);
    }

    public virtual void Deactivate()
    {
        //ItemManager.Log($"Entity {Name} is deactivating!");
        foreach(var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
        foreach(var ss in _watchedEntitySubscriptions.Values) foreach(var s in ss) s.Dispose();
        _watchedEntitySubscriptions.Clear();
        foreach(var ss in _grudgeSubscriptions.Values) foreach(var s in ss) s.Dispose();
        _grudgeSubscriptions.Clear();
        _active = false;
        EntityInfoGathered.Clear();
        VisibleEntities.Clear();
        VisibleEnemies.Clear();
        VisibleFriendlies.Clear();
    }

    public Entity(ItemManager itemManager, Zone zone, EquippableItem hull, EntitySettings settings)
    {
        Settings = MessagePackSerializer.Deserialize<EntitySettings>(MessagePackSerializer.Serialize(settings));
        ItemManager = itemManager;
        Zone = zone;
        Hull = hull;
        HullData = itemManager.GetData(hull) as HullData;
        Name = HullData.Name;
        PowerBus = new PowerBus(this);
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

    // Sets or clears a runtime-only IFF override deciding this entity's hostility toward another.
    // Pass null to clear the override and restore the derived (faction-based) rule.
    public void SetIff(Entity other, bool? hostile)
    {
        if (hostile.HasValue) _iffOverrides[other] = hostile.Value;
        else _iffOverrides.Remove(other);
    }

    public bool IsHostileTo(Entity other, bool recursive = false)
    {
        // An override decides only the stance of the entity that holds it. The recursive=true calls
        // below are the derived rule asking "is the other entity hostile to me" purely to compute ITS
        // OWN reciprocal stance; skipping the override check there stops one entity's override (e.g. a
        // player going neutral) from leaking into another entity's derived hostility.
        if (!recursive && _iffOverrides.TryGetValue(other, out var overrideHostile))
            return overrideHostile;

        if (Faction == null)
            return !recursive && other.Faction != null && other.IsHostileTo(this, true);

        // TODO: Inter-faction hostility
        // When the entity faction owns the zone, they are hostile to trespassers or those hostile to them
        if (Faction == Zone.GalaxyZone.Owner)
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
        return faction == Faction ? FactionRelationship.Beloved : FactionRelationship.Neutral;
    }

    public static bool IsPresencePermitted(FactionRelationship relationship, SecurityLevel securityLevel) => 
        (int) relationship - (int) securityLevel > 0;
    public static bool IsDockingPermitted(FactionRelationship relationship, SecurityLevel securityLevel) => 
        (int) relationship - (int) securityLevel > 1;

    public void ActivateConsumable(ConsumableItem item)
    {
        _activeConsumables.Add(new ConsumableItemEffect(item, this));
    }

    public ConsumableItemEffect FindActiveConsumable(ConsumableItemData data)
    {
        return _activeConsumables.FirstOrDefault(ac => ac.Data == data);
    }

    public bool CanActivateConsumable(ConsumableItemData data)
    {
        return data.Stackable || FindActiveConsumable(data) == null;
    }

    public bool TryActivateConsumable(ConsumableItemData data)
    {
        if (!CanActivateConsumable(data)) return false;
        
        var key = ItemManager.ItemData.RefOf<ItemData>(data).Key;
        var bay = FindItemInCargo(key);
        if (bay == null) return false;
        
        var item = (ConsumableItem) bay.ItemsOfType[key].First();
        ActivateConsumable(item);
        bay.Remove(item);
        return true;
    }

    private void MapEntity()
    {
        var hullData = ItemManager.GetData(Hull) as HullData;
        EquippedHull = new EquippedItem(ItemManager, Hull, int2.zero, this);
        Equipment.Add(EquippedHull);
        Mass = hullData.Mass;
        Temperature = new float[hullData.Shape.Width, hullData.Shape.Height];
        NewTemperature = new float[hullData.Shape.Width, hullData.Shape.Height];
        HullConductivity = new bool2[hullData.Shape.Width,hullData.Shape.Height];
        ThermalMass = new float[hullData.Shape.Width, hullData.Shape.Height];
        Armor = new float[hullData.Shape.Width, hullData.Shape.Height];
        MaxArmor = new float[hullData.Shape.Width, hullData.Shape.Height];
        Hardpoints = new HardpointData[hullData.Shape.Width, hullData.Shape.Height];
        foreach (var hardpoint in hullData.Hardpoints)
        {
            foreach (var hardpointCoord in hardpoint.Shape.Coordinates)
            {
                var hullCoord = hardpoint.Position + hardpointCoord;
                Hardpoints[hullCoord.x, hullCoord.y] = hardpoint;
            }
        }
        var cellCount = hullData.Shape.Coordinates.Length;
        foreach (var v in hullData.Shape.Coordinates)
        {
            Armor[v.x, v.y] = hullData.Armor;
            MaxArmor[v.x, v.y] = hullData.Armor;
            if (Hardpoints[v.x, v.y] != null)
            {
                Armor[v.x, v.y] += Hardpoints[v.x, v.y].Armor;
                MaxArmor[v.x, v.y] += Hardpoints[v.x, v.y].Armor;
            }
            Temperature[v.x, v.y] = 280;
            ThermalMass[v.x, v.y] = hullData.Mass * hullData.SpecificHeat / cellCount;
        }
        GearOccupancy = new EquippedItem[hullData.Shape.Width, hullData.Shape.Height];
    }

    public void GenerateWeaponGroups()
    {
        foreach (var group in Weapons
            .GroupBy(w => w.Item.EquippableItem.Data.Key)
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

    public int CountItemsInCargo(CultRecordKey itemDataID)
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

    public EquippedCargoBay FindItemInCargo(CultRecordKey itemDataID)
    {
        return CargoBays.FirstOrDefault(c => c.ItemsOfType.ContainsKey(itemDataID));
    }

    public Shape UnoccupiedSpace
    {
        get
        {
            var emptyShape = new Shape(HullData.Shape.Width, HullData.Shape.Height);
            foreach (var v in HullData.Shape.Coordinates)
            {
                // Empty hardpoint cells count as free: general gear may use them until hardpoint gear claims them
                if (HullData.InteriorCells[v] && GearOccupancy[v.x, v.y] == null)
                    emptyShape[v] = true;
            }

            return emptyShape;
        }
    }

    // Attempts to move a given number of items of the given type to the target Entity
    // Returns the number of items successfully transferred
    public int TryTransferItems(Entity target, CultRecordKey itemDataID, int quantity)
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
        _orderedEquipment = Equipment.ToArray();
        
        var hullData = ItemManager.GetData(Hull) as HullData;
        var itemData = ItemManager.GetData(item.EquippableItem);
        foreach (var i in hullData.Shape.Coordinates)
            if (GearOccupancy[i.x, i.y] == item)
            {
                ThermalMass[i.x, i.y] -= ItemManager.GetThermalMass(item.EquippableItem) / itemData.Shape.Coordinates.Length;
                GearOccupancy[i.x, i.y] = null;
            }
        Mass -= itemData.Mass;
        foreach (var b in item.Behaviors)
        {
            if (b is Weapon weapon)
            {
                _weapons.Remove(weapon);
                foreach (var group in WeaponGroups) { group.items.Remove(item); group.weapons.Remove(weapon); }
            }
            if (b is Radiator heatsink)
                _heatsinks.Remove(heatsink);
            if (b is Shield)
                Shield = null;
            if (b is Cockpit)
                Cockpit = null;
            if (b is Sensor)
                Sensor = null;
        }

        // Cut 2 Gate 1 fix (docs/stats-and-power-cut.md): the resolver's §0b lifecycle promise -- a resolver
        // entry is "destroyed at unequip" -- was never wired up. Without this, every unequipped item stayed
        // reachable from the resolver's dictionaries for the entity's whole remaining life.
        Resolver.Forget(item);

        return item.EquippableItem;
    }

    // Check whether the given item will fit when its origin is placed at the given coordinate
    private bool ItemFits(EquippableItemData itemData, HullData hullData, EquippableItem item, int2 hullCoord)
    {
        // If the given coordinate isn't even in the ship it obviously won't fit
        if (!hullData.Shape[hullCoord]) return false;
        
        // Items without specific hardpoints on the ship can be freely rotated and placed anywhere
        if (itemData.HardpointType == HardpointType.Tool)
        {
            // Check every cell of the item's shape
            foreach (var i in itemData.Shape.Coordinates)
            {
                // An interior cell is usable when no gear occupies it. An empty hardpoint cell counts as usable:
                // general gear may take it until hardpoint gear claims it, which is the same rule
                // UnoccupiedSpace reports, so what generation is offered and what equipping accepts agree.
                var itemCoord = hullCoord + itemData.Shape.Rotate(i, item.Rotation);
                if (!hullData.InteriorCells[itemCoord] || GearOccupancy[itemCoord.x, itemCoord.y] != null)
                    return false;
            }
        }
        else
        {
            var hardpoint = Hardpoints[hullCoord.x, hullCoord.y];
            
            // If there's no hardpoint there, it won't fit
            if (hardpoint == null) return false;

            // If the hardpoint type doesn't match the item, it won't fit
            if (hardpoint.Type != itemData.HardpointType) return false;
            
            // Items placed in hardpoints are automatically aligned to hardpoint rotation
            item.Rotation = hardpoint.Rotation;

            // Inset the shapes of both item and hardpoint
            var itemShapeInset = hullData.Shape.Inset(itemData.Shape, hullCoord, item.Rotation);
            var hardpointShapeInset = hullData.Shape.Inset(hardpoint.Shape, hardpoint.Position);
            
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

        var itemData = ItemManager.GetData(item);
        var hullData = ItemManager.GetData(Hull) as HullData;
        return ItemFits(itemData, hullData, item, hullCoord);
    }

    public bool TryFindSpace(EquippableItem item, out int2 hullCoord)
    {
        // Don't allow equipping while deployed
        if (_active)
        {
            hullCoord = int2.zero;
            return false;
        }
        var itemData = ItemManager.GetData(item);
        var hullData = ItemManager.GetData(Hull) as HullData;
        
        // Tools and thermal equipment can be installed anywhere on the ship
        // Search the whole ship for somewhere the item will fit
        if (itemData.HardpointType == HardpointType.Tool)
        {
            foreach (var hullCoord2 in hullData.InteriorCells.Coordinates)
            {
                if (ItemFits(itemData, hullData, item, hullCoord2))
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
            foreach (var hardpoint in hullData.Hardpoints)
            {
                if(hardpoint.Type == itemData.HardpointType)
                {
                    foreach (var hardpointCoord in hardpoint.Shape.Coordinates)
                    {
                        var hullCoord2 = hardpoint.Position + hardpointCoord;
                        if (ItemFits(itemData, hullData, item, hullCoord2))
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
        
        var itemData = ItemManager.GetData(item);
        var hullData = ItemManager.GetData(Hull) as HullData;

        if (!ItemFits(itemData, hullData, item, hullCoord)) return false;
        
        EquippedItem equippedItem;
        if (itemData.HardpointType == HardpointType.Tool)
        {
            if(itemData is CargoBayData)
            {
                if (itemData is DockingBayData)
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
            
        foreach (var i in itemData.Shape.Coordinates)
        {
            var occupiedCoord = hullCoord + itemData.Shape.Rotate(i, item.Rotation);
            // TODO: Track thermal mass of cargo bay contents as reactive property
            ThermalMass[occupiedCoord.x, occupiedCoord.y] += ItemManager.GetThermalMass(item) / itemData.Shape.Coordinates.Length;
            GearOccupancy[occupiedCoord.x, occupiedCoord.y] = equippedItem;
        }
                
        Mass += itemData.Mass;
        _orderedEquipment = Equipment.ToArray();
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

    // Cut 4 (docs/stats-and-power-cut.md, Cut 4): CanSpendCapacitorCharge/TrySpendCapacitorCharge died here with
    // their last caller. Cut 3 named them a temporary exception for the four instant draws (a burst, a shot, a
    // ping, a hit taken); all four now spend from their own InputCapacitor instead (InstantWeapon, Sensor,
    // Shield), fed continuously by PowerBus like every other consumer. The entity's shared bus capacitors are
    // now touched only by PowerBus itself (§0b: "the bus fills; the owning behaviour spends").

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

    public IEnumerable<T> GetBehaviorData<T>() where T : BehaviorData
    {
        foreach (var equippedItem in Equipment)
            foreach (var behavior in equippedItem.Behaviors)
                if (behavior.Data is T b)
                    yield return b;
    }

    public virtual void Update(float delta)
    {
        if (!_active) return;

        var hullData = ItemManager.GetData(Hull) as HullData;

        TargetRange = Target.Value == null ? -1 : length(Position - Target.Value.Position);

        var localSecurityLevel = Zone.GetSecurityLevel(Position.xz);
        if (CurrentSecurityLevel.Value != localSecurityLevel) CurrentSecurityLevel.Value = localSecurityLevel;

        foreach (var v in VisibilitySources.Keys.ToArray())
        {
            VisibilitySources[v] = decay(VisibilitySources[v], ItemManager.GameplaySettings.VisibilityDecay, delta);
 
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
                if (_activeConsumables[i].RemainingDuration < 0)
                {
                    // Cut 2 Gate 1 fix (docs/stats-and-power-cut.md): an expired consumable dropped out of this
                    // list without ever telling the resolver, leaving its generation/cache/modifier entries
                    // reachable (keyed by this ConsumableItemEffect instance) for the rest of the process.
                    Resolver.Forget(_activeConsumables[i]);
                    _activeConsumables.RemoveAt(i--);
                }
            }

            // Cut 3 (docs/stats-and-power-cut.md §1.2): stepped once per tick, before any equipped item's
            // Behaviors execute, so every IPowerConsumer's grant is decided before it acts on it.
            PowerBus.Step(delta);

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
        else Position.y = Zone.GetHeight(Position.xz) + hullData.GridOffset;
    }

    private void UpdateTemperature(float delta)
    {
        var hullData = ItemManager.GetData(Hull) as HullData;
        
        MaxTemp = Single.MinValue;
        MinTemp = Single.MaxValue;
        
        //float[,] newTemp = new float[hullData.Shape.Width,hullData.Shape.Height];
        var radiation = 0f;
        foreach (var v in hullData.Shape.Coordinates)
        {
            var temp = Temperature[v.x, v.y];
            var totalTemp = temp / ItemManager.GameplaySettings.HeatConductionMultiplier;
            var totalConductivity = 1f / ItemManager.GameplaySettings.HeatConductionMultiplier;
            
            if (hullData.Shape[int2(v.x - 1, v.y)])
            {
                var conductivity = (GearOccupancy[v.x, v.y]?.Conductivity ?? 1) *
                                   (GearOccupancy[v.x - 1, v.y]?.Conductivity ?? 1) *
                                   (HullConductivity[v.x - 1, v.y].x ? hullData.Conductivity : 1 / hullData.Conductivity) *
                                   (ThermalMass[v.x - 1, v.y] / ThermalMass[v.x, v.y]);
                totalConductivity += conductivity;
                totalTemp += Temperature[v.x - 1, v.y] * conductivity;
            }

            if (hullData.Shape[int2(v.x + 1, v.y)])
            {
                var conductivity = (GearOccupancy[v.x, v.y]?.Conductivity ?? 1) *
                                   (GearOccupancy[v.x + 1, v.y]?.Conductivity ?? 1) *
                                   (HullConductivity[v.x, v.y].x ? hullData.Conductivity : 1 / hullData.Conductivity) *
                                   (ThermalMass[v.x + 1, v.y] / ThermalMass[v.x, v.y]);
                totalConductivity += conductivity;
                totalTemp += Temperature[v.x + 1, v.y] * conductivity;
            }


            if (hullData.Shape[int2(v.x, v.y - 1)])
            {
                var conductivity = (GearOccupancy[v.x, v.y]?.Conductivity ?? 1) *
                                   (GearOccupancy[v.x, v.y - 1]?.Conductivity ?? 1) * 
                                   (HullConductivity[v.x, v.y - 1].y ? hullData.Conductivity : 1 / hullData.Conductivity) *
                                   (ThermalMass[v.x, v.y - 1] / ThermalMass[v.x, v.y]);
                totalConductivity += conductivity;
                totalTemp += Temperature[v.x, v.y - 1] * conductivity;
            }


            if (hullData.Shape[int2(v.x, v.y + 1)])
            {
                var conductivity = (GearOccupancy[v.x, v.y]?.Conductivity ?? 1) *
                                   (GearOccupancy[v.x, v.y + 1]?.Conductivity ?? 1) * 
                                   (HullConductivity[v.x, v.y].y ? hullData.Conductivity : 1 / hullData.Conductivity) *
                                   (ThermalMass[v.x, v.y + 1] / ThermalMass[v.x, v.y]);
                totalConductivity += conductivity;
                totalTemp += Temperature[v.x, v.y + 1] * conductivity;
            }
            
            NewTemperature[v.x, v.y] = totalTemp / totalConductivity;

            var r = 0f;
            // For all cells on the border of the entity, radiate some heat into space, increasing the visibility of the ship
            if (Parent==null && !hullData.InteriorCells[v])
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

// The consumable case: no durability, no modifiers (there is no entity's worth of equipment to modify against),
// and progress-through-duration substitutes for heat rather than being exponentiated by the stat's heat field.
// That substitution is the disagreement the map names, kept exactly as it behaved before the collapse.
public class ConsumableItemEffect : IStatContext
{
    public float RemainingDuration { get; private set; }
    public Entity Entity { get; }
    public ConsumableItem Item { get; }
    public ConsumableItemData Data { get; }
    public Lot Lot { get; }
    public Behavior[] Behaviors { get; }

    public ConsumableItemEffect(ConsumableItem item, Entity entity)
    {
        Item = item;
        Entity = entity;
        Data = (ConsumableItemData) entity.ItemManager.GetData(item);
        Lot = entity.ItemManager.GetLot(item);
        RemainingDuration = Data.Duration;

        Behaviors = Data.Behaviors
            .Select(bd => bd.CreateInstance(this))
            .ToArray();
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
        // Progress moves every tick; a stat with a ConsumableProgress term must recompute against it, the same
        // way EquippedItem signals Heat and Durability from UpdatePerformance below.
        Entity.Resolver.InvalidateSource(this, StatSource.ConsumableProgress);
    }

    // Cut 2 (docs/stats-and-power-cut.md): the resolver owns the value; this is the caller's read of it, keyed by
    // this effect instance (never by Entity, so two active consumables never share an entry).
    public float Evaluate(PerformanceStat stat) => Entity.Resolver.Resolve(this, stat, this);

    // Cut 1 (docs/stats-and-power-cut.md): progress-through-duration is now its own StatSource
    // (ConsumableProgress) rather than a hard-coded override of "heat" that applied to every stat regardless of
    // its declared terms. A consumable stat that wants this must declare a ConsumableProgress term; one that
    // does not gets the identity (1), same as any other context reading a term it has no source for.
    public float HeatFactor(float exponent) => 1f;
    public float DurabilityFactor(float exponent) => 1f;
    public float ConsumableProgressFactor(float exponent) =>
        pow(Data.Effectiveness.Evaluate((Data.Duration - RemainingDuration) / Data.Duration), exponent);
    public float PowerSupplyFactor(float exponent) => 1f;
    // No modifiers: there is no entity's worth of equipment to modify against (comment above, unchanged by Cut 2).
    public float ScaleModifier(PerformanceStat stat) => 1f;
    public float ConstantModifier(PerformanceStat stat) => 0f;
}

// The equipped case: the only one with heat, live durability, and modifiers, because it is the only one with an
// entity and a running simulation behind it.
public class EquippedItem : IStatContext
{
    // SortPosition (Cut 3-era, only ever set by Reactor.Order) is deleted by Cut 5 (docs/stats-and-power-cut.md
    // §1.3): "EquippedItem.SortPosition must no longer influence who is fed" -- the tiered allocation pass
    // replaces it, and IOrderedBehavior/Reactor.Order go with it (§7 O3).
    public EquippableItem EquippableItem;
    public int2 Position;

    private bool _thermalOnline;
    private bool _durabilityOnline;
    public WwiseMetaSoundBank SoundBank;
    
    public Behavior[] Behaviors { get; }
    public Dictionary<int, BehaviorGroup> BehaviorGroups { get; }
    public float Conductivity { get; }
    public float ThermalPerformance { get; private set; }
    public float ThermalExponent { get; }
    public float DurabilityPerformance { get; private set; }
    public float DurabilityExponent { get; }
    public float Wear { get; private set; }
    public Shape InsetShape { get; }
    public Entity Entity { get; }
    public EquippableItemData Data { get; }
    public Lot Lot { get; }

    public ReactiveProperty<bool> ThermalOnline { get; } = new ReactiveProperty<bool>(false);
    public ReactiveProperty<bool> DurabilityOnline { get; } = new ReactiveProperty<bool>(false);
    public ReadOnlyReactiveProperty<bool> Online { get; }
    public ReactiveProperty<bool> Enabled { get; } = new ReactiveProperty<bool>(true);
    public ReadOnlyReactiveProperty<bool> Active { get; }
    public ItemManager ItemManager { get; }

    public Subject<uint> AudioEvents { get; } = new Subject<uint>();
    public Subject<(uint id, float v)> AudioParameters { get; } = new Subject<(uint id, float v)>();
    public Dictionary<uint, float> AudioParameterValues { get; } = new Dictionary<uint, float>();

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
        Data = ItemManager.GetData(item);
        Entity = entity;
        EquippableItem = item;
        Lot = ItemManager.GetLot(item);
        Position = position;
        Conductivity = Data.Conductivity;
        ThermalExponent = lerp(
            ItemManager.GameplaySettings.ThermalQualityMin,
            ItemManager.GameplaySettings.ThermalQualityMax,
            pow(Lot.Quality, ItemManager.GameplaySettings.ThermalQualityExponent));
        DurabilityExponent = lerp(
            ItemManager.GameplaySettings.DurabilityQualityMin,
            ItemManager.GameplaySettings.DurabilityQualityMax,
            pow(Lot.Quality, ItemManager.GameplaySettings.DurabilityQualityExponent));
        var hullData = itemManager.GetData(entity.Hull);
        InsetShape = hullData.Shape.Inset(Data.Shape, position, item.Rotation);
        if (Entity.Temperature != null) oldTemperature = Temperature;

        Online = new ReadOnlyReactiveProperty<bool>(ThermalOnline
            .CombineLatest(DurabilityOnline, (thermal, durability) => thermal && durability).DistinctUntilChanged());
        Active = new ReadOnlyReactiveProperty<bool>(Enabled
            .CombineLatest(Online, (enabled, online) => enabled && online).DistinctUntilChanged());
        

        Behaviors = Data.Behaviors
            .Select(bd => bd.CreateInstance(this))
            .ToArray();

        BehaviorGroups = Behaviors
            .GroupBy(b => b.Data.Group)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => new BehaviorGroup
            {
                Behaviors = g.ToArray()
            });

        foreach (var behavior in Behaviors)
        {
            if(behavior is IPopulationAssignment populationAssignment)
                entity.PopulationAssignments.Add(populationAssignment);
        }

        // Cut 5 (docs/stats-and-power-cut.md §1.3, Q5 "defaulted per behaviour kind"): seed the stored tier
        // exactly once, the first time this unit is ever equipped. A unit the player has already assigned a
        // tier to (or one seeded on an earlier equip) keeps it -- this never runs again for that unit.
        if (EquippableItem.PowerTier == PowerTiers.Unassigned)
        {
            var consumerTiers = Behaviors.OfType<IPowerConsumer>().Select(c => c.DefaultPowerTier).ToArray();
            if (consumerTiers.Length > 0)
                EquippableItem.PowerTier = consumerTiers.Min();
        }

        // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19, superseding F6): Cut
        // 6's static half of this check -- refusing any PowerRequestFields stat whose own Terms named
        // PowerSupply -- assumed PowerRequest read those stats the same way Execute does. It does not any more:
        // EvaluateNominalPower (below, and Behavior.EvaluateNominalPower) is what every PowerRequest/RefreshReserve/
        // RefreshInputCapacitor implementation now calls for a request-field stat, and it pins that stat's own
        // PowerSupplyFactor to 1 regardless of what Terms the stat declares -- so a direct PowerSupply term on a
        // request field can no longer make the request depend on its own answer, and the six shipped records this
        // ruling exists to make legal again (RadiatorData.PumpedHeat, AetherDriveData.Torque) are not an authoring
        // error to refuse. What is still refused: a request field fed a PowerSupply-tainted value through a
        // *modifier chain* (StatModifier.ValidateNoPowerSupplyChain, StatModifier.cs) -- EvaluateNominalPower
        // forwards ScaleModifier/ConstantModifier to the item's real, non-nominal resolver entries (same as
        // ConditionRatio's NominalContext already did), so a modifier chain that reaches a power-tainted magnitude
        // stat still corrupts the nominal read too. That dynamic check is unchanged and still runs from
        // Initialize below via StatModifier -- it is the only surviving half of Cut 6's rule.
    }

    // Cut 2 (docs/stats-and-power-cut.md): the resolver owns the value; this is the caller's read of it, keyed by
    // this EquippedItem instance, so two ships equipping the same design never share a resolved value.
    public float Evaluate(PerformanceStat stat) => Entity.Resolver.Resolve(this, stat, this);

    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19): what this item wants at
    // full power supply, not what it is currently managing -- the read every PowerRequest/RefreshReserve/
    // RefreshInputCapacitor implementation uses for a stat named in StatValidation.PowerRequestFields, so a stat
    // that is both what a request asks for and what it also produces (RadiatorData.PumpedHeat, AetherDriveData.
    // Torque) stops being circular at the root instead of being forbidden outright. Only PowerSupply is pinned:
    // Heat and Durability are NOT, because they are not the term a request would be circular through -- this
    // tick's PowerBus.Step (which calls PowerRequest) always runs before this tick's grant exists, so nothing
    // here could depend on an answer that has not been computed yet, and a hot or worn item honestly asking for
    // less power is real degradation the ruling never objected to, only "the request depends on its own answer."
    // Bypasses the resolver's (owner, stat) cache the same way ConditionRatio's NominalContext below does and for
    // the same reason: the cache key is (this, stat) alone, so resolving a second, different context under that
    // key would clobber whatever Execute's own real Evaluate(stat) call cached for this same tick.
    public float EvaluateNominalPower(PerformanceStat stat) => stat.Evaluate(new PowerRequestContext(this));

    // Real-condition view of this same item for EvaluateNominalPower above: everything but PowerSupply is
    // forwarded to the real item, including ScaleModifier/ConstantModifier -- deliberately NOT pinned, so a
    // modifier chain that reaches a power-tainted magnitude stat still taints a nominal read through them too.
    // That is exactly the residual case StatModifier.ValidateNoPowerSupplyChain still refuses: nominalizing this
    // item's own direct Terms cannot undo a value baked in by another item's modifier.
    private readonly struct PowerRequestContext : IStatContext
    {
        private readonly EquippedItem _item;
        public PowerRequestContext(EquippedItem item) => _item = item;
        public Lot Lot => _item.Lot;
        public float HeatFactor(float exponent) => _item.HeatFactor(exponent);
        public float DurabilityFactor(float exponent) => _item.DurabilityFactor(exponent);
        public float ConsumableProgressFactor(float exponent) => _item.ConsumableProgressFactor(exponent);
        public float PowerSupplyFactor(float exponent) => 1f;
        public float ScaleModifier(PerformanceStat stat) => _item.ScaleModifier(stat);
        public float ConstantModifier(PerformanceStat stat) => _item.ConstantModifier(stat);
    }

    public float HeatFactor(float exponent) => pow(ThermalPerformance, ThermalExponent * exponent);
    public float DurabilityFactor(float exponent) => pow(DurabilityPerformance, DurabilityExponent * exponent);
    public float ConsumableProgressFactor(float exponent) => 1f;
    // Cut 6 (docs/stats-and-power-cut.md), applied per F1 (operator ruling 2026-09-19): PerformanceStat.Evaluate
    // multiplies this straight onto the resolved value rather than blending it into the Min/Max interpolation, so
    // a stat with a PowerSupply term is exactly unchanged at full supply (pow(1, exponent) == 1 regardless of
    // exponent) and exactly zero at zero supply (pow(0, exponent) == 0 for any exponent > 0), no matter what Min
    // is -- "a continuous consumer that degrades instead of stopping" down to genuinely nothing. PowerBus writes
    // PowerSupply below; the resolver invalidates this source once per tick from the same write (PowerBus.cs
    // AllocateTiers), so this read is always this tick's own grant, never a stale one.
    public float PowerSupplyFactor(float exponent) => pow(PowerSupply, exponent);

    // Cut 3 (docs/stats-and-power-cut.md §1.2): PowerBus's grant ratio for this item, in [0,1], from whichever
    // tick last ran its Step. Display-only and, as of Cut 6, stat-source-only: the only other reader is the
    // resolver, through PowerSupplyFactor above, on behalf of whichever IPowerConsumer behaviour declared a
    // PowerSupply term. Forbidden writers: nothing but PowerBus sets this. Defaults to 1 (fully supplied) for an
    // item that draws no power, or before the bus has run once.
    public float PowerSupply { get; internal set; } = 1f;

    // Cut 2: these used to read the catalog stat's own per-entity dictionary (the leak, §0.3). A modifier now
    // attaches to this entity's resolver, keyed by (this item, stat); reading it here is unchanged.
    public float ScaleModifier(PerformanceStat stat) => Entity.Resolver.ScaleModifier(this, stat);
    public float ConstantModifier(PerformanceStat stat) => Entity.Resolver.ConstantModifier(this, stat);

    // Cut 8 (operator ask 2026-09-19): "multiply [emission] by actual performance so a broken thruster emits a
    // puny flame." One ratio per (item, stat): this item's actually-resolved value against what the same item,
    // same lot, same quality, same modifier stack would produce with every degradable term at its identity --
    // full durability, optimal temperature, full power supply. Quality and lot are deliberately NOT part of
    // "perfect": NominalContext forwards Lot/ScaleModifier/ConstantModifier unchanged and only pins Heat,
    // Durability, PowerSupply and ConsumableProgress to 1, so a cheap item's honestly-lower ceiling still reads
    // 1 at full health -- it is not reported as damaged for being cheap.
    //
    // Cost: Evaluate(stat) is the same resolver-cached read every other caller already pays for (§ above), so it
    // is a dictionary hit here unless something already invalidated it this tick. The nominal side cannot reuse
    // that cache -- the resolver's cache key is (owner, stat) alone, and owner is `this`, so resolving a second,
    // different context under the same key would clobber the real cached value -- so it calls stat.Evaluate
    // directly against NominalContext, bypassing the cache entirely. That is one pass over the stat's own Terms
    // list (Thrust/Torque declare at most three: Quality, Heat or Durability, PowerSupply), each a pow() and a
    // multiply, i.e. a handful of flops with no allocation (NominalContext is a readonly struct). Cheap enough to
    // call once per thruster per frame, which is all presentation (ShipInstance.Update) does with it.
    // No epsilon guard on a zero/near-zero nominal: math.min/max (CultMath's own DXIL-lowering semantics,
    // math.cs) resolve a NaN operand to the OTHER operand, not to NaN, so saturate(0f/0f) -- the one way nominal
    // degenerates to exactly 0 (a stat authored with Max <= 0 and no ScaleModifier/ConstantModifier moving it off
    // that) -- is a real, tested 0f (ConditionIsZeroNotNaNWhenTheNominalValueItselfDegeneratesToZero), not NaN. A
    // manual guard here would duplicate that behaviour rather than add any.
    public float ConditionRatio(PerformanceStat stat) => saturate(Evaluate(stat) / stat.Evaluate(new NominalContext(this)));

    // Perfect-conditions view of this same item for ConditionRatio above. Everything but the three degradable
    // factors is forwarded to the real item so a modifier stack or a cheap lot's lower Quality term still shapes
    // "perfect" the same way it shapes "actual" -- only Heat/Durability/PowerSupply/ConsumableProgress are
    // pinned to their identity (1), since those are exactly the terms condition is supposed to measure.
    private readonly struct NominalContext : IStatContext
    {
        private readonly EquippedItem _item;
        public NominalContext(EquippedItem item) => _item = item;
        public Lot Lot => _item.Lot;
        public float HeatFactor(float exponent) => 1f;
        public float DurabilityFactor(float exponent) => 1f;
        public float ConsumableProgressFactor(float exponent) => 1f;
        public float PowerSupplyFactor(float exponent) => 1f;
        public float ScaleModifier(PerformanceStat stat) => _item.ScaleModifier(stat);
        public float ConstantModifier(PerformanceStat stat) => _item.ConstantModifier(stat);
    }

    public void AddHeat(float heat, bool ignoreThermalMass = false)
    {
        foreach(var hullCoord in InsetShape.Coordinates)
            Entity.AddHeat(hullCoord, heat / InsetShape.Coordinates.Length, ignoreThermalMass);
    }

    public void UpdatePerformance()
    {
        var temp = Temperature;
        ThermalPerformance = Data.Performance(temp);
        Entity.Resolver.InvalidateSource(this, StatSource.Heat);
        var deltaTemp = math.abs(temp - oldTemperature);
        DurabilityPerformance = EquippableItem.Durability / Data.Durability;
        Entity.Resolver.InvalidateSource(this, StatSource.Durability);
        var performanceThreshold = Entity.Settings.ShutdownPerformance;
        Wear = (1 - pow(ThermalPerformance,
                (1 - pow(Lot.Quality, ItemManager.GameplaySettings.QualityWearExponent)) *
                ItemManager.GameplaySettings.ThermalWearExponent) +
                deltaTemp * ItemManager.GameplaySettings.DeltaTempWearExponent
            ) * Data.Durability / Data.ThermalResilience;
        ThermalOnline.Value = ThermalPerformance > performanceThreshold || Entity.OverrideShutdown && EquippableItem.OverrideShutdown;
        DurabilityOnline.Value = EquippableItem.Durability > .01f;
        oldTemperature = temp;
    }

    public void Update(float delta)
    {
        foreach (var audioStat in Data.AudioStats)
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
}

public class EquippedCargoBay : EquippedItem
{
    public readonly ReactiveDictionary<ItemInstance, int2> Cargo = new ReactiveDictionary<ItemInstance, int2>();

    public readonly ItemInstance[,] Occupancy;

    public readonly Dictionary<CultRecordKey, List<ItemInstance>> ItemsOfType = new Dictionary<CultRecordKey, List<ItemInstance>>();

    public new readonly CargoBayData Data;
    
    public float Mass { get; private set; }
    public float ThermalMass { get; private set; }
    public string Name { get; }

    public Shape UnoccupiedSpace
    {
        get
        {
            var unoccupied = new Shape(Data.InteriorShape.Width, Data.InteriorShape.Height);
            foreach (var v in unoccupied.AllCoordinates)
                unoccupied[v] = Occupancy[v.x, v.y] == null;
            return unoccupied;
        }
    }

    public EquippedCargoBay(ItemManager itemManager, EquippableItem item, int2 position, Entity entity, string name) : base(itemManager, item, position, entity)
    {
        Data = ItemManager.GetData(EquippableItem) as CargoBayData;
        Name = name;

        Mass = Data.Mass;
        ThermalMass = Data.Mass * Data.SpecificHeat;
        
        Occupancy = new ItemInstance[Data.InteriorShape.Width,Data.InteriorShape.Height];
    }

    // Check whether the given item will fit when its origin is placed at the given coordinate
    public bool ItemFits(ItemInstance item, int2 cargoCoord)
    {
        var itemData = ItemManager.GetData(item);
        // Check every cell of the item's shape
        foreach (var i in itemData.Shape.Coordinates)
        {
            // If there is an item already occupying that space, it won't fit
            var itemCargoCoord = cargoCoord + itemData.Shape.Rotate(i, item.Rotation);
            if (!Data.InteriorShape[itemCargoCoord] || (Occupancy[itemCargoCoord.x, itemCargoCoord.y] != null && Occupancy[itemCargoCoord.x, itemCargoCoord.y] != item)) return false;
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
        var itemData = ItemManager.GetData(item);
        var remainingQuantity = item.Quantity;
        
        // For simple commodities, search for existing item stacks to add to
        foreach (var cargoItem in Cargo.Keys)
        {
            if (!item.Data.Key.Equals(cargoItem.Data.Key)) continue;
            
            var cargoCommodity = (SimpleCommodity) cargoItem;
            if (cargoCommodity.Quantity >= itemData.MaxStack) continue;
            
            // Subtract remaining space in existing stack from remaining quantity
            remainingQuantity -= min(itemData.MaxStack - cargoCommodity.Quantity, remainingQuantity);
            positions.Add(Cargo[cargoItem]);
            
            // If we've moved all of the items into existing stacks, no need to search for empty space!
            if (remainingQuantity == 0) return true;
        }
        
        // TODO: Try alternate item rotations / use Shape.FitsWithin
        // Search all the space in the cargo bay for an empty space where the item fits
        foreach (var cargoCoord in Data.InteriorShape.Coordinates)
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
        foreach (var cargoCoord in Data.InteriorShape.Coordinates)
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
        var itemData = ItemManager.GetData(item);
        if (ItemFits(item, cargoCoord))
        {
            foreach (var p in itemData.Shape.Coordinates)
            {
                var pos = cargoCoord + itemData.Shape.Rotate(p, item.Rotation);
                Occupancy[pos.x, pos.y] = item;
            }
            Cargo[item] = cargoCoord;
            
            if(!ItemsOfType.ContainsKey(item.Data.Key))
                ItemsOfType[item.Data.Key] = new List<ItemInstance>();
            ItemsOfType[item.Data.Key].Add(item);
        }
        else if (Occupancy[cargoCoord.x, cargoCoord.y] is SimpleCommodity cargoCommodity && cargoCommodity.Data.Key.Equals(item.Data.Key))
        {
            if (cargoCommodity.Quantity + item.Quantity <= itemData.MaxStack)
            {
                cargoCommodity.Quantity += item.Quantity;
            }
            else
            {
                var quantityTransferred = itemData.MaxStack - cargoCommodity.Quantity;
                item.Quantity -= quantityTransferred;
                cargoCommodity.Quantity = itemData.MaxStack;
                
                Mass += itemData.Mass * quantityTransferred;
                ThermalMass += itemData.Mass * itemData.SpecificHeat * quantityTransferred;
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
        
        var itemData = ItemManager.GetData(item);
        foreach (var p in itemData.Shape.Coordinates)
        {
            var pos = cargoCoord + itemData.Shape.Rotate(p, item.Rotation);
            Occupancy[pos.x, pos.y] = item;
        }
        Cargo[item] = cargoCoord;
        
        if(!ItemsOfType.ContainsKey(item.Data.Key))
            ItemsOfType[item.Data.Key] = new List<ItemInstance>();
        ItemsOfType[item.Data.Key].Add(item);
        
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
        var itemData = ItemManager.GetData(item);
        if(quantity >= item.Quantity)
        {
            foreach(var v in Data.InteriorShape.Coordinates)
                if (Occupancy[v.x, v.y] == item)
                    Occupancy[v.x, v.y] = null;

            Cargo.Remove(item);
            ItemsOfType[item.Data.Key].Remove(item);
            if (!ItemsOfType[item.Data.Key].Any())
                ItemsOfType.Remove(item.Data.Key);

            Mass -= ItemManager.GetMass(item);
            ThermalMass -= ItemManager.GetThermalMass(item);

            return item;
        }

        item.Quantity -= quantity;
        Mass -= itemData.Mass * quantity;
        ThermalMass -= itemData.Mass * itemData.SpecificHeat * quantity;
        return new SimpleCommodity{Data = item.Data, Quantity = quantity, Rotation = item.Rotation};
    }

    public void Remove(CraftedItemInstance item)
    {
        if (!Cargo.ContainsKey(item))
        {
            ItemManager.Log("Attempted to remove item from a cargo bay that it wasn't even in! Something went wrong here!");
            return;
        }
        var itemData = ItemManager.GetData(item);
        foreach(var v in Data.InteriorShape.Coordinates)
            if (Occupancy[v.x, v.y] == item)
                Occupancy[v.x, v.y] = null;
        
        Cargo.Remove(item);
        ItemsOfType[item.Data.Key].Remove(item);
        if (!ItemsOfType[item.Data.Key].Any())
            ItemsOfType.Remove(item.Data.Key);
        
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
    public int2 MaxSize => _data.MaxSize;
    private DockingBayData _data;
    public EquippedDockingBay(ItemManager itemManager, EquippableItem item, int2 position, Entity entity, string name) : base(itemManager, item, position, entity, name)
    {
        _data = ItemManager.GetData(EquippableItem) as DockingBayData;
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