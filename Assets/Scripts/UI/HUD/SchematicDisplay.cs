using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;

public class SchematicDisplay : MonoBehaviour
{
    private static readonly HashSet<string> WeaponBehaviorKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "GuidedWeapon",
        "Launcher",
        "InstantWeapon",
        "ConstantWeapon",
        "ChargedWeapon",
        "AutoWeapon"
    };

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
    private string _clientStatePath = "";
    private AetheriaRuntimeCatalogSnapshot _catalog;
    private AetheriaRuntimePlayerSettingsDocument _playerSettings;
    private AetheriaRuntimeDaemonRenderSettings? _renderSettings;
    private AetheriaRuntimeCurrentEntityDocument _currentEntityDocument;
    private float _currentEntityDocumentReadTime = float.NegativeInfinity;
    private const float CurrentEntityHudRefreshIntervalSeconds = 0.1f;

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

    public void SetRenderSettings(AetheriaRuntimeDaemonRenderSettings renderSettings)
    {
        _renderSettings = renderSettings;
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
            .Where(HasTypedWeaponBehavior)
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
            var typedWeapon = FindTypedWeapon(x.Item.EquippableItem);
            if (typedWeapon != null)
                x.ListElement.ShowWeapon(typedWeapon);
            //x.ListElement.Label.text = x.Item.EquippableItem.Name;
            if (!_enemy)
            {
                x.ListElement.InfiniteAmmoIcon.gameObject.SetActive(!x.Weapon.UsesAmmo);
                x.ListElement.AmmoLabel.gameObject.SetActive(x.Weapon.UsesAmmo);
            }
        }
    }

    private AetheriaRuntimeCatalogItem FindTypedWeapon(ItemInstance item)
    {
        var typedItem = FindTypedItem(item);
        return typedItem != null && !string.IsNullOrWhiteSpace(typedItem.WeaponType)
            ? typedItem
            : null;
    }


    private bool HasTypedWeaponBehavior(EquippedItem item)
    {
        var typedItem = FindTypedItem(item.EquippableItem);
        return typedItem?.BehaviorKinds.Any(WeaponBehaviorKinds.Contains) == true;
    }

    void Update()
    {
        if (_entity != null)
        {
            if (!_enemy)
            {
                var hud = ResolveCurrentEntityHudStatus();
                OverrideIcon.SetActive(hud.OverrideShutdown && cos(Time.time * OverrideIconBlinkSpeed) > 0);
                ShieldIcon.SetActive(hud.ShieldActive);
                if (hud.RadiatorCount == 1)
                    RadiatorTemperatureLabel.text = FormatTemperature((float)hud.RadiatorTemperatureMinimum);
                else if (hud.RadiatorCount > 1)
                    RadiatorTemperatureLabel.text =
                        $"{FormatTemperature((float)hud.RadiatorTemperatureMinimum)}-" +
                        $"{FormatTemperature((float)hud.RadiatorTemperatureMaximum)}";
                HeatsinkBackground.color = hud.HeatsinksEnabled ? HeaderElementEnabledColor : HeaderElementDisabledColor;

                if (_heatStorages.Length == 1)
                    HeatStorageTemperatureLabel.text = FormatTemperature(_heatStorages[0].Item.Temperature);
                else if (_heatStorages.Length > 1)
                    HeatStorageTemperatureLabel.text =
                        $"{FormatTemperature(_heatStorages.Min(r => r.Item.Temperature))}-" +
                        $"{FormatTemperature(_heatStorages.Max(r => r.Item.Temperature))}";

                if (_cargoBays.Length == 1)
                    CargoTemperatureLabel.text = FormatTemperature(_cargoBays[0].Temperature);
                else if (_cargoBays.Length > 1)
                    CargoTemperatureLabel.text =
                        $"{FormatTemperature(_cargoBays.Min(r => r.Temperature))}-" +
                        $"{FormatTemperature(_cargoBays.Max(r => r.Temperature))}";

                if(_cockpit != null)
                {
                    CockpitTemperatureLabel.text = FormatTemperature(_cockpit.Item.Temperature);

                    HeatstrokeMeterFill.anchorMax = new Vector2((float)hud.Heatstroke, 1);
                    HypothermiaMeterFill.anchorMax = new Vector2((float)hud.Hypothermia, 1);
                    HeatstrokeLimitFill.anchorMax = new Vector2(
                        (float)(_renderSettings?.NormalizeThermalRisk(_cockpit.Item.Temperature) ?? 0.0),
                        1);
                }

                SensorCooldownFill.anchorMax = new Vector2((float)hud.SensorCooldown, 1);

                if (hud.CapacitorCapacity <= 0)
                {
                    EnergyFill.anchorMax = Vector2.up;
                    EnergyLabel.text = FormatValue((float)hud.ReactorDraw);
                }
                else
                {
                    var charge = hud.CapacitorCharge;
                    var maxCharge = hud.CapacitorCapacity;
                    EnergyFill.anchorMax = new Vector2((float)(charge / maxCharge), 1);
                    EnergyLabel.text = $"{((int)charge).ToString()}/{((int)maxCharge).ToString()} + ({((int)hud.ReactorDraw).ToString()})";
                }

                if (hud.AetherDriveMaximumRpm > 0)
                {
                    ForwardRPMLabel.text = FormatValue((float)hud.AetherDriveRpmX);
                    ForwardRPMFill.anchorMax = new Vector2((float)(hud.AetherDriveRpmX / hud.AetherDriveMaximumRpm), 1);
                    
                    StrafeRPMLabel.text = FormatValue((float)hud.AetherDriveRpmY);
                    StrafeRPMFill.anchorMax = new Vector2((float)(hud.AetherDriveRpmY / hud.AetherDriveMaximumRpm), 1);
                    
                    TurnRPMLabel.text = FormatValue((float)hud.AetherDriveRpmZ);
                    TurnRPMFill.anchorMax = new Vector2((float)(hud.AetherDriveRpmZ / hud.AetherDriveMaximumRpm), 1);
                }
            }
            // else
            // {
            //     DistanceLabel.text = $"{(int)length(_entity.Position - _player.Position)}";
            // }

            if (!_enemy)
            {
                var hud = ResolveCurrentEntityHudStatus();
                if(VisibilityLabel)
                    VisibilityLabel.text = ((int)hud.Visibility).ToString();

                if(HullDurabilityFill)
                {
                    var dur = (float)hud.HullDurabilityRatio;
                    HullDurabilityFill.anchorMax = new Vector2(dur, 1);
                    HullDurabilityLabel.text = $"{FormatValue(dur * 100)}%";
                }
            }
            else
            {
                if(VisibilityLabel)
                    VisibilityLabel.text = ((int)_entity.Visibility).ToString();

                if(HullDurabilityFill)
                {
                    _hull = _entity.Hull;
                    var dur = _hull.Durability / GetMaxDurability(_hull);
                    HullDurabilityFill.anchorMax = new Vector2(dur, 1);
                    HullDurabilityLabel.text = $"{FormatValue(dur * 100)}%";
                }
            }

            foreach (var x in _schematicItems)
            {
                if (!_enemy)
                {
                    var thermalRange = GetThermalRange(x.Item.EquippableItem, x.Item.Temperature);
                    x.ListElement.HeatFill.anchorMax = new Vector2(unlerp(thermalRange.minimum, thermalRange.maximum, x.Item.Temperature), 0);
                    if (x.Cooldown != null)
                        x.ListElement.CooldownFill.anchorMax = new Vector2(x.Cooldown.Progress, 1);
                    x.ListElement.DurabilityLabel.text = $"{(int)(x.Item.EquippableItem.Durability / GetMaxDurability(x.Item.EquippableItem) * 100)}%";
                    if (x.Weapon.UsesAmmo)
                    {
                        if(x.Weapon.MagazineSize > 1)
                            x.ListElement.AmmoLabel.text = x.Weapon.Ammo.ToString();
                        else
                            x.ListElement.AmmoLabel.text = _entity.CountItemsInCargo(x.Weapon.AmmoItemKey).ToString();
                    }
                }

                if (x.Weapon != null)
                {
                    if(x.ListElement.RangeLabel)
                        x.ListElement.RangeLabel.text = ((int) x.Weapon.Range).ToString();
                }
            }
        }
    }

    private (float minimum, float maximum) GetThermalRange(ItemInstance item, float currentTemperature)
    {
        var typedItem = FindTypedItem(item);
        if (typedItem != null && typedItem.MaximumTemperature > typedItem.MinimumTemperature)
            return ((float)typedItem.MinimumTemperature, (float)typedItem.MaximumTemperature);

        return (currentTemperature, currentTemperature + 1f);
    }
    private float GetMaxDurability(ItemInstance item)
    {
        var typedItem = FindTypedItem(item);
        if (typedItem != null && typedItem.Durability > 0)
            return (float)typedItem.Durability;

        return item is EquippableItem equippable ? Math.Max(equippable.Durability, 1f) : 1f;
    }

    private AetheriaRuntimeCatalogItem FindTypedItem(ItemInstance item)
    {
        return ResolveCatalog()?.FindItem(item?.ItemKey ?? "");
    }

    private AetheriaRuntimeCatalogSnapshot ResolveCatalog()
    {
        if (_catalog != null)
            return _catalog;

        try
        {
            _catalog = ResolveClient().Aetheria().LatestCatalog();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria runtime catalog for schematic display: {ex.Message}");
        }

        return _catalog;
    }

    private AetheriaRuntimePlayerSettingsDocument ResolvePlayerSettings()
    {
        if (_playerSettings != null)
            return _playerSettings;

        try
        {
            _playerSettings = ResolveClient()
                .Aetheria()
                .Settings
                .LatestPlayer();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria player settings for schematic display: {ex.Message}");
        }

        return _playerSettings;
    }

    private AetheriaRuntimeCurrentEntityHudStatus ResolveCurrentEntityHudStatus()
    {
        if (_currentEntityDocument == null ||
            Time.unscaledTime - _currentEntityDocumentReadTime >= CurrentEntityHudRefreshIntervalSeconds)
        {
            try
            {
                _currentEntityDocument = ResolveClient()
                    .Aetheria()
                    .Current
                    .Entity
                    .LatestAsync()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to read Aetheria current entity HUD status: {ex.Message}");
            }

            _currentEntityDocumentReadTime = Time.unscaledTime;
        }

        return _currentEntityDocument?.Hud ?? new AetheriaRuntimeCurrentEntityHudStatus();
    }

    private AetheriaClient ResolveClient()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
        if (!string.Equals(_clientStatePath, stateBoot.StateFilePath, StringComparison.Ordinal))
        {
            _clientStatePath = stateBoot.StateFilePath;
            ClearClientCaches();
        }

        return AetheriaUnityRuntimeClientProvider.ResolveClient(stateBoot, "unity-schematic-display");
    }

    private string FormatValue(float value)
    {
        var settings = ResolvePlayerSettings();
        var significantDigits = settings?.SignificantDigits ?? 3;
        var magnitude = value == 0.0f ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value))) + 1;
        var digits = significantDigits - magnitude;
        if (digits < 0)
            digits = 0;

        var formatted = value.ToString($"N{digits}", CultureInfo.CurrentCulture);
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var decimalSeparator = Convert.ToChar(separator);
        return formatted.Contains(separator)
            ? formatted.TrimEnd('0').TrimEnd(decimalSeparator)
            : formatted;
    }

    private string FormatTemperature(float value)
    {
        var unit = ResolvePlayerSettings()?.TemperatureUnit ?? nameof(TemperatureUnit.Celsius);
        if (string.Equals(unit, nameof(TemperatureUnit.Kelvin), StringComparison.OrdinalIgnoreCase))
            return $"{FormatValue(value)} K";
        if (string.Equals(unit, nameof(TemperatureUnit.Fahrenheit), StringComparison.OrdinalIgnoreCase))
            return $"{FormatValue(value * (9f / 5) - 459.67f)} F";

        return $"{FormatValue(value - 273.15f)} C";
    }

    private void ClearClientCaches()
    {
        _catalog = null;
        _playerSettings = null;
        _currentEntityDocument = null;
        _currentEntityDocumentReadTime = float.NegativeInfinity;
    }
}
