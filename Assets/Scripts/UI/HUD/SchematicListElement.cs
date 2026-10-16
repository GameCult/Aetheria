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

    // Cut 2 (docs/fire-control-cut.md): marks this row as the current aim point (Entity.ResolvedAim)
    // on an enemy schematic. Unassigned in a prefab that has not been wired up in the Unity editor yet
    // (unavailable in this pass) -- SetSelected below is a no-op until then, not a null reference.
    public GameObject SelectedIndicator;

    private List<Prototype> _iconInstances = new List<Prototype>();

    // A gear row names its class; created on first use as VisibleTargetIndicator.ShowLabel creates its own (no scene or
    // prefab edit) and hidden by every other row kind, because pools are shared.
    private TextMeshProUGUI _nameLabel;

    private void ClearIcons()
    {
        foreach(var prototype in _iconInstances) prototype.ReturnToPool();
        _iconInstances.Clear();
    }

    private void SetName(string name)
    {
        var show = !string.IsNullOrEmpty(name);
        if (_nameLabel == null)
        {
            if (!show) return;
            var child = new GameObject("Name", typeof(RectTransform));
            child.transform.SetParent(transform, false);
            _nameLabel = child.AddComponent<TextMeshProUGUI>();
            _nameLabel.fontSize = 14;
            _nameLabel.alignment = TextAlignmentOptions.Left;
            _nameLabel.raycastTarget = false;
            var rect = _nameLabel.rectTransform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(40, 0);
            rect.offsetMax = Vector2.zero;
        }
        _nameLabel.gameObject.SetActive(show);
        if (show && _nameLabel.text != name) _nameLabel.text = name;
    }

    // A revealed item that is not a weapon: its hardpoint's icon and, when given, its class name. Layout-aim reuses this with
    // a null name for mount rows.
    public void ShowGear(HardpointType type, string name)
    {
        ClearIcons();
        var icon = IconPrototype.Instantiate<Image>();
        _iconInstances.Add(icon.GetComponent<Prototype>());
        icon.sprite = Settings.GetIcon(type);
        SetName(name);
    }

    public void SetSelected(bool selected)
    {
        if (SelectedIndicator != null) SelectedIndicator.SetActive(selected);
    }

    public void ShowWeapon(WeaponItemData weapon)
    {
        ClearIcons();
        SetName(null);

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
