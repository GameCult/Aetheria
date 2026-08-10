using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;

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

    public GameObject AetherDriveUi;

    public TextMeshProUGUI EnergyLabel;
    public TextMeshProUGUI CockpitTemperatureLabel;
    public TextMeshProUGUI RadiatorTemperatureLabel;
    public TextMeshProUGUI HeatStorageTemperatureLabel;
    public TextMeshProUGUI CargoTemperatureLabel;
    public TextMeshProUGUI VisibilityLabel;
    public TextMeshProUGUI HullDurabilityLabel;
    public TextMeshProUGUI DistanceLabel;
    public TextMeshProUGUI ForwardRPMLabel;
    public TextMeshProUGUI StrafeRPMLabel;
    public TextMeshProUGUI TurnRPMLabel;

    public RectTransform SensorCooldownFill;
    public RectTransform EnergyFill;
    public RectTransform HullDurabilityFill;
    public RectTransform HeatstrokeMeterFill;
    public RectTransform HypothermiaMeterFill;
    public RectTransform HeatstrokeLimitFill;
    public RectTransform ForwardRPMFill;
    public RectTransform StrafeRPMFill;
    public RectTransform TurnRPMFill;

    private Entity _entity;
    private EquippableItem _hull;
    private Radiator[] _radiators;
    private Cockpit _cockpit;
    private Reactor _reactor;
    private Capacitor[] _capacitors;
    private HeatStorage[] _heatStorages;
    private EquippedCargoBay[] _cargoBays;
    private SchematicDisplayItem[] _schematicItems;
    private AetherDrive _aetherDrive;

    private bool _enemy;
    private Entity _player;

    public SchematicDisplayItem[] SchematicItems
    {
        get { return _schematicItems; }
    }

    public class SchematicDisplayItem
    {
        public EquippedItem Item;
        public SchematicListElement ListElement;
        public IProgressBehavior Cooldown;
        // public ItemUsage ItemUsage;
        public Weapon Weapon;
    }

    public void ShowShip(Entity entity, Entity player = null)
    {
        _enemy = player != null;
        _player = player;
        if (_schematicItems != null)
            foreach (var item in _schematicItems)
            {
                item.ListElement.GetComponent<Prototype>().ReturnToPool();
            }

        _entity = entity;
        if (!_enemy)
        {
            _cockpit = entity.GetBehavior<Cockpit>();
            _reactor = entity.GetBehavior<Reactor>();
            _capacitors = entity.GetBehaviors<Capacitor>().ToArray();
            _aetherDrive = entity.GetBehavior<AetherDrive>();
            AetherDriveUi.SetActive(_aetherDrive != null);

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
        
        _schematicItems = entity.Equipment
            .Where(x => x.Behaviors.Any(b => b.Data is WeaponData))
            .Select(x => new SchematicDisplayItem
            {
                Item = x, 
                ListElement = ListElementPrototype.Instantiate<SchematicListElement>(),
                Cooldown = _enemy ? null : (IProgressBehavior) x.Behaviors.FirstOrDefault(b=> b is IProgressBehavior),
                // ItemUsage = _enemy ? null : (ItemUsage) x.Behaviors.FirstOrDefault(b=> b is ItemUsage),
                Weapon = (Weapon) x.Behaviors.FirstOrDefault(b=>b is Weapon)
            })
            .ToArray();
        foreach (var x in _schematicItems)
        {
            if(x.Item.Data is WeaponItemData weaponItemData)
                x.ListElement.ShowWeapon(weaponItemData);
            //x.ListElement.Label.text = x.Item.EquippableItem.Name;
            if (!_enemy)
            {
                x.ListElement.InfiniteAmmoIcon.gameObject.SetActive(x.Weapon.WeaponData.AmmoType == Guid.Empty);
                x.ListElement.AmmoLabel.gameObject.SetActive(x.Weapon.WeaponData.AmmoType != Guid.Empty);
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

                if (_aetherDrive != null)
                {
                    ForwardRPMLabel.text = ActionGameManager.PlayerSettings.Format(_aetherDrive.Rpm.x);
                    ForwardRPMFill.anchorMax = new Vector2(_aetherDrive.Rpm.x / _aetherDrive.MaximumRpm, 1);
                    
                    StrafeRPMLabel.text = ActionGameManager.PlayerSettings.Format(_aetherDrive.Rpm.y);
                    StrafeRPMFill.anchorMax = new Vector2(_aetherDrive.Rpm.y / _aetherDrive.MaximumRpm, 1);
                    
                    TurnRPMLabel.text = ActionGameManager.PlayerSettings.Format(_aetherDrive.Rpm.z);
                    TurnRPMFill.anchorMax = new Vector2(_aetherDrive.Rpm.z / _aetherDrive.MaximumRpm, 1);
                }
            }
            // else
            // {
            //     DistanceLabel.text = $"{(int)length(_entity.Position - _player.Position)}";
            // }

            if(VisibilityLabel)
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
                    var itemData = _entity.ItemManager.GetData(x.Item.EquippableItem);
                    x.ListElement.HeatFill.anchorMax = new Vector2(unlerp(itemData.MinimumTemperature, itemData.MaximumTemperature, x.Item.Temperature), 0);
                    if (x.Cooldown != null)
                        x.ListElement.CooldownFill.anchorMax = new Vector2(x.Cooldown.Progress, 1);
                    x.ListElement.DurabilityLabel.text = $"{(int)(x.Item.EquippableItem.Durability / itemData.Durability * 100)}%";
                    if (x.Weapon.WeaponData.AmmoType != Guid.Empty)
                    {
                        if(x.Weapon.WeaponData.MagazineSize > 1)
                            x.ListElement.AmmoLabel.text = x.Weapon.Ammo.ToString();
                        else
                            x.ListElement.AmmoLabel.text = _entity.CountItemsInCargo(x.Weapon.WeaponData.AmmoType).ToString();
                    }
                }

                if (x.Weapon.WeaponData != null)
                {
                    if(x.ListElement.RangeLabel)
                        x.ListElement.RangeLabel.text = ((int) x.Weapon.Range).ToString();
                }
            }
        }
    }
}
