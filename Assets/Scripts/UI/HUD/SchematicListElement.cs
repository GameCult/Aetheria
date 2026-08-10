using System.Collections;
using System.Collections.Generic;
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

    public void ShowWeapon(WeaponItemData weapon)
    {
        foreach(var prototype in _iconInstances) prototype.ReturnToPool();
        _iconInstances.Clear();

        var caliberIcon = IconPrototype.Instantiate<Image>();
        _iconInstances.Add(caliberIcon.GetComponent<Prototype>());
        caliberIcon.sprite = Settings.GetIcon(weapon.WeaponCaliber);

        var rangeIcon = IconPrototype.Instantiate<Image>();
        _iconInstances.Add(rangeIcon.GetComponent<Prototype>());
        rangeIcon.sprite = Settings.GetIcon(weapon.WeaponRange);

        var typeIcon = IconPrototype.Instantiate<Image>();
        _iconInstances.Add(typeIcon.GetComponent<Prototype>());
        typeIcon.sprite = Settings.GetIcon(weapon.WeaponType);

        foreach (var sprite in Settings.GetIcons(weapon.WeaponFireTypes))
        {
            var fireTypeIcon = IconPrototype.Instantiate<Image>();
            _iconInstances.Add(fireTypeIcon.GetComponent<Prototype>());
            fireTypeIcon.sprite = sprite;
        }

        foreach (var sprite in Settings.GetIcons(weapon.WeaponModifiers))
        {
            var modifierIcon = IconPrototype.Instantiate<Image>();
            _iconInstances.Add(modifierIcon.GetComponent<Prototype>());
            modifierIcon.sprite = sprite;
        }
    }
}
