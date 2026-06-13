using System;
using System.Collections.Generic;
using GameCult.Aetheria.State.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SchematicListElement : MonoBehaviour
{
    public GameSettings Settings;
    public Prototype IconPrototype;
    public TextMeshProUGUI DurabilityLabel;
    public TextMeshProUGUI RangeLabel;
    public TextMeshProUGUI AmmoLabel;
    public Image InfiniteAmmoIcon;
    public RectTransform CooldownFill;
    public RectTransform HeatFill;

    private List<Prototype> _iconInstances = new List<Prototype>();

    public void ShowWeapon(AetheriaRuntimeCatalogItem weapon)
    {
        if (TryParseWeaponFacets(
                weapon,
                out var caliber,
                out var range,
                out var type,
                out var fireTypes,
                out var modifiers))
            ShowWeapon(caliber, range, type, fireTypes, modifiers);
    }

    public void ShowWeapon(WeaponItemData weapon)
    {
        ShowWeapon(
            weapon.WeaponCaliber,
            weapon.WeaponRange,
            weapon.WeaponType,
            weapon.WeaponFireTypes,
            weapon.WeaponModifiers);
    }

    private void ShowWeapon(
        WeaponCaliber caliber,
        WeaponRange range,
        WeaponType type,
        WeaponFireType fireTypes,
        WeaponModifiers modifiers)
    {
        foreach (var prototype in _iconInstances) prototype.ReturnToPool();
        _iconInstances.Clear();

        var caliberIcon = IconPrototype.Instantiate<Image>();
        _iconInstances.Add(caliberIcon.GetComponent<Prototype>());
        caliberIcon.sprite = Settings.GetIcon(caliber);

        var rangeIcon = IconPrototype.Instantiate<Image>();
        _iconInstances.Add(rangeIcon.GetComponent<Prototype>());
        rangeIcon.sprite = Settings.GetIcon(range);

        var typeIcon = IconPrototype.Instantiate<Image>();
        _iconInstances.Add(typeIcon.GetComponent<Prototype>());
        typeIcon.sprite = Settings.GetIcon(type);

        foreach (var sprite in Settings.GetIcons(fireTypes))
        {
            var fireTypeIcon = IconPrototype.Instantiate<Image>();
            _iconInstances.Add(fireTypeIcon.GetComponent<Prototype>());
            fireTypeIcon.sprite = sprite;
        }

        foreach (var sprite in Settings.GetIcons(modifiers))
        {
            var modifierIcon = IconPrototype.Instantiate<Image>();
            _iconInstances.Add(modifierIcon.GetComponent<Prototype>());
            modifierIcon.sprite = sprite;
        }
    }

    private static bool TryParseWeaponFacets(
        AetheriaRuntimeCatalogItem weapon,
        out WeaponCaliber caliber,
        out WeaponRange range,
        out WeaponType type,
        out WeaponFireType fireTypes,
        out WeaponModifiers modifiers)
    {
        return
            Enum.TryParse(weapon.WeaponCaliber, out caliber) &&
            Enum.TryParse(weapon.WeaponRange, out range) &&
            Enum.TryParse(weapon.WeaponType, out type) &&
            Enum.TryParse(weapon.WeaponFireTypes, out fireTypes) &&
            Enum.TryParse(weapon.WeaponModifiers, out modifiers);
    }
}
