using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CultMath;
using UnityEngine.Serialization;
using static CultMath.math;

public class SchematicDisplay : MonoBehaviour
{
    public GameSettings Settings;
    public Prototype ListElementPrototype;

    public Color HeaderElementEnabledColor;
    public Color HeaderElementDisabledColor;

    public GameObject OverrideIcon;
    public float OverrideIconBlinkSpeed;
    public GameObject ShieldIcon;
    public Image HeatsinkBackground;

    public TextMeshProUGUI EnergyLabel;
    public TextMeshProUGUI CockpitTemperatureLabel;
    public TextMeshProUGUI RadiatorTemperatureLabel;
    public TextMeshProUGUI HeatStorageTemperatureLabel;
    public TextMeshProUGUI CargoTemperatureLabel;
    public TextMeshProUGUI VisibilityLabel;
    public TextMeshProUGUI HullDurabilityLabel;
    public TextMeshProUGUI DistanceLabel;

    public RectTransform SensorCooldownFill;
    public RectTransform EnergyFill;
    public RectTransform HullDurabilityFill;
    public RectTransform HeatstrokeMeterFill;
    public RectTransform HypothermiaMeterFill;
    public RectTransform HeatstrokeLimitFill;

    private Entity _entity;
    private EquippableItem _hull;
    private Radiator[] _radiators;
    private Cockpit _cockpit;
    private Reactor _reactor;
    private Capacitor[] _capacitors;
    private HeatStorage[] _heatStorages;
    private EquippedCargoBay[] _cargoBays;
    private SchematicDisplayItem[] _schematicItems;

    private bool _enemy;
    private Entity _player;

    public SchematicDisplayItem[] SchematicItems
    {
        get { return _schematicItems; }
    }

    public class SchematicDisplayItem
    {
        // The row Entity.SchematicRows derived: which item, which class and which range it shows.
        public SchematicRow Row;
        public SchematicListElement ListElement;
        public IProgressBehavior Cooldown;
        // public ItemUsage ItemUsage;
        public Weapon Weapon;
    }

    public void ShowShip(Entity entity, Entity player = null)
    {
        _enemy = player != null;
        _player = player;
        ReturnRows();

        _entity = entity;
        if (VisibilityLabel)
            VisibilityLabel.gameObject.SetActive(!_enemy);
        if (!_enemy)
        {
            _cockpit = entity.GetBehavior<Cockpit>();
            _reactor = entity.GetBehavior<Reactor>();
            _capacitors = entity.GetBehaviors<Capacitor>().ToArray();

            _radiators = entity.GetBehaviors<Radiator>().ToArray();
            if (_radiators.Length == 0)
                RadiatorTemperatureLabel.text = "N/A";

            _heatStorages = entity.GetBehaviors<HeatStorage>().ToArray();
            if (_heatStorages.Length == 0)
                HeatStorageTemperatureLabel.text = "N/A";

            _cargoBays = entity.CargoBays.ToArray();
            if (_cargoBays.Length == 0)
                CargoTemperatureLabel.text = "N/A";
        }
        
        BuildRows(Rows());
    }

    // Which rows, which class and which range label are Entity.SchematicRows' decision (aetheria-ew, classify-from-belief);
    // this display only renders them.
    private IEnumerable<SchematicRow> Rows() => (_player ?? _entity).SchematicRows(_entity);

    private void ReturnRows()
    {
        if (_schematicItems == null) return;
        foreach (var item in _schematicItems)
            item.ListElement.GetComponent<Prototype>().ReturnToPool();
        _schematicItems = null;
    }

    private void BuildRows(IEnumerable<SchematicRow> rows)
    {
        _schematicItems = rows
            .Select(x => new SchematicDisplayItem
            {
                Row = x,
                ListElement = ListElementPrototype.Instantiate<SchematicListElement>(),
                Cooldown = _enemy ? null : (IProgressBehavior) x.Item.Behaviors.FirstOrDefault(b=> b is IProgressBehavior),
                // ItemUsage = _enemy ? null : (ItemUsage) x.Behaviors.FirstOrDefault(b=> b is ItemUsage),
                Weapon = _enemy ? null : (Weapon) x.Item.Behaviors.FirstOrDefault(b=>b is Weapon)
            })
            .ToArray();
        foreach (var x in _schematicItems)
        {
            if (x.Row.Mount != null)
                x.ListElement.ShowGear(x.Row.Mount.Type, null);
            else if(x.Row.Claimed is WeaponItemData weaponItemData)
                x.ListElement.ShowWeapon(weaponItemData);
            else
                x.ListElement.ShowGear(x.Row.Claimed.HardpointType, x.Row.Claimed.Name);
            //x.ListElement.Label.text = x.Item.EquippableItem.Name;
            if (!_enemy)
            {
                x.ListElement.InfiniteAmmoIcon.gameObject.SetActive(!x.Weapon.WeaponData.AmmoType.IsSet());
                x.ListElement.AmmoLabel.gameObject.SetActive(x.Weapon.WeaponData.AmmoType.IsSet());
            }
        }
    }

    void Update()
    {
        if (_entity != null)
        {
            if (!_enemy)
            {
                OverrideIcon.SetActive(_entity.OverrideShutdown && cos(Time.time * OverrideIconBlinkSpeed) > 0);
                ShieldIcon.SetActive(_entity.Shield!=null && _entity.Shield.Item.Active.Value);
                if (_radiators.Length == 1)
                    RadiatorTemperatureLabel.text = $"{((int)(_radiators[0].RadiatorTemperature - 273.15f)).ToString()}°C";
                else if (_radiators.Length > 1)
                    RadiatorTemperatureLabel.text =
                        $"{((int)(_radiators.Min(r => r.RadiatorTemperature) - 273.15f)).ToString()}-" +
                        $"{((int)(_radiators.Max(r => r.RadiatorTemperature) - 273.15f)).ToString()}°C";
                HeatsinkBackground.color = _entity.HeatsinksEnabled ? HeaderElementEnabledColor : HeaderElementDisabledColor;

                if (_heatStorages.Length == 1)
                    HeatStorageTemperatureLabel.text = $"{((int)(_heatStorages[0].Item.Temperature - 273.15f)).ToString()}°C";
                else if (_heatStorages.Length > 1)
                    HeatStorageTemperatureLabel.text =
                        $"{((int)(_heatStorages.Min(r => r.Item.Temperature) - 273.15f)).ToString()}-" +
                        $"{((int)(_heatStorages.Max(r => r.Item.Temperature) - 273.15f)).ToString()}°C";

                if (_cargoBays.Length == 1)
                    CargoTemperatureLabel.text = $"{((int)(_cargoBays[0].Temperature - 273.15f)).ToString()}°C";
                else if (_cargoBays.Length > 1)
                    CargoTemperatureLabel.text =
                        $"{((int)(_cargoBays.Min(r => r.Temperature) - 273.15f)).ToString()}-" +
                        $"{((int)(_cargoBays.Max(r => r.Temperature) - 273.15f)).ToString()}°C";

                if(_cockpit != null)
                {
                    CockpitTemperatureLabel.text = $"{((int) (_cockpit.Item.Temperature - 273.15f)).ToString()}°C";

                    HeatstrokeMeterFill.anchorMax = new Vector2(_entity.Heatstroke, 1);
                    HypothermiaMeterFill.anchorMax = new Vector2(_entity.Hypothermia, 1);
                    HeatstrokeLimitFill.anchorMax = new Vector2(unlerp(Settings.GameplaySettings.HypothermiaTemperature, Settings.GameplaySettings.HeatstrokeTemperature, _cockpit.Item.Temperature), 1);
                }

                SensorCooldownFill.anchorMax = new Vector2(_entity.Sensor?.Cooldown ?? 0, 1);

                if (_capacitors.Length == 0)
                {
                    EnergyFill.anchorMax = Vector2.up;
                    EnergyLabel.text = ActionGameManager.PlayerSettings.Format(_reactor.Draw);
                }
                else
                {
                    var charge = _capacitors.Sum(x => x.Charge);
                    var maxCharge = _capacitors.Sum(x => x.Capacity);
                    EnergyFill.anchorMax = new Vector2(charge / maxCharge, 1);
                    EnergyLabel.text = $"{((int)charge).ToString()}/{((int)maxCharge).ToString()} + ({((int)_reactor.Draw).ToString()})";
                }
            }
            // else
            // {
            //     DistanceLabel.text = $"{(int)length(_entity.Position - _player.Position)}";
            // }

            // A reveal gained or ended after selection changes the rows with no event to hook: rebuild when the
            // revealed set differs from the rows' items.
            if (_enemy && !Rows().Select(r => r.Key).SequenceEqual(_schematicItems.Select(x => x.Row.Key)))
            {
                ReturnRows();
                BuildRows(Rows());
            }

            // Cut 2 (docs/fire-control-cut.md): marks whichever row is the player's current aim point.
            // Re-checked every frame (not just on ShowShip) because ResolvedAim can drop on its own
            // as info decays, with no event to hook.
            if (_enemy && _schematicItems != null)
                foreach (var x in _schematicItems)
                    x.ListElement.SetSelected(x.Row.Marks(_player.ResolvedAim));

            if(VisibilityLabel && !_enemy)
                VisibilityLabel.text = ((int)_entity.Visibility).ToString();

            if(HullDurabilityFill)
            {
                _hull = _entity.Hull;
                var hullData = _entity.ItemManager.GetData(_hull);
                var dur = _hull.Durability / hullData.Durability;
                HullDurabilityFill.anchorMax = new Vector2(dur, 1);
                HullDurabilityLabel.text = $"{ActionGameManager.PlayerSettings.Format(dur * 100)}%";
            }

            foreach (var x in _schematicItems)
            {
                if (!_enemy)
                {
                    var itemData = _entity.ItemManager.GetData(x.Row.Item.EquippableItem);
                    x.ListElement.HeatFill.anchorMax = new Vector2(unlerp(itemData.MinimumTemperature, itemData.MaximumTemperature, x.Row.Item.Temperature), 0);
                    if (x.Cooldown != null)
                        x.ListElement.CooldownFill.anchorMax = new Vector2(x.Cooldown.Progress, 1);
                    x.ListElement.DurabilityLabel.text = $"{(int)(x.Row.Item.EquippableItem.Durability / itemData.Durability * 100)}%";
                    if (x.Weapon.WeaponData.AmmoType.IsSet())
                    {
                        if(x.Weapon.WeaponData.MagazineSize > 1)
                            x.ListElement.AmmoLabel.text = x.Weapon.Ammo.ToString();
                        else
                            x.ListElement.AmmoLabel.text = _entity.CountItemsInCargo(x.Weapon.WeaponData.AmmoType.Key).ToString();
                    }
                }

                if (x.ListElement.RangeLabel)
                {
                    var range = x.Row.Range;
                    x.ListElement.RangeLabel.gameObject.SetActive(range.HasValue);
                    if (range.HasValue) x.ListElement.RangeLabel.text = ((int) range.Value).ToString();
                }
            }
        }
    }
}
