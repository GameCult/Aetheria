using System;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using TMPro;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public class ActionBarSlot : MonoBehaviour
{
    public TextMeshProUGUI Label;
    public RawImage Icon;
    public TextMeshProUGUI InputLabel;
    public Image InputIcon;
    public TextMeshProUGUI QuantityRemaining;
    public Image Fill;
    public ObservablePointerEnterTrigger PointerEnterTrigger;
    public ObservablePointerExitTrigger PointerExitTrigger;
    private ActionBarBinding binding;
    public string ControlPath { get; set; }

    public ActionBarBinding Binding
    {
        get => binding;
        set
        {
            if (ReferenceEquals(binding, value))
                return;

            binding = value;
            if (binding == null)
            {
                Fill.fillAmount = 0;
                Label.gameObject.SetActive(false);
                QuantityRemaining.gameObject.SetActive(false);
                Icon.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        Binding = null;
    }

    private void Update()
    {
        Binding?.Update();
    }

}

public abstract class ActionBarBinding
{
    public Entity Entity { get; }
    private readonly Func<AetheriaControl> _resolveControl;
    protected ActionBarSlot Slot { get; }
    protected GameSettings Settings { get; }
    public abstract void Activate();
    public abstract void Deactivate();
    public abstract void Update();

    protected ActionBarBinding(
        Entity entity,
        ActionBarSlot slot,
        Func<AetheriaControl> resolveControl,
        GameSettings settings)
    {
        Entity = entity;
        Slot = slot;
        _resolveControl = resolveControl ?? (() => null);
        Settings = settings;
    }

    protected bool TrySubmit(Action<AetheriaControl> submit, string label)
    {
        if (submit == null)
            return false;

        try
        {
            var control = _resolveControl();
            if (control == null)
                return false;

            submit(control);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon action-bar {label} operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    protected AetheriaRuntimeCatalogItem FindCatalogItem(ItemInstance item)
    {
        try
        {
            return AetheriaUnityRuntimeClientProvider
                .CurrentCatalog()
                .FindItem(item, x => x.ItemKey);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria runtime catalog for action-bar binding: {ex.Message}");
            return null;
        }
    }
}

public class ActionBarConsumableBinding : ActionBarBinding
{
    public AetheriaRuntimeCatalogItem Target { get; }

    public ActionBarConsumableBinding(
        Entity entity,
        ActionBarSlot slot,
        Func<AetheriaControl> resolveControl,
        GameSettings settings,
        AetheriaRuntimeCatalogItem target) : base(entity, slot, resolveControl, settings)
    {
        Target = target;
        Slot.QuantityRemaining.gameObject.SetActive(true);
        Slot.Label.gameObject.SetActive(false);
        Slot.Icon.gameObject.SetActive(false);
    }

    public override void Activate()
    {
        TrySubmit(
            operations => operations.ActivateConsumable(TargetItemKey),
            "consumable");
    }

    public override void Deactivate()
    {
    }

    public override void Update()
    {
        Slot.QuantityRemaining.text = $"{Entity.CountItemsInCargo(TargetItemKey)}";
        if (string.IsNullOrWhiteSpace(TargetItemKey))
        {
            Slot.Fill.fillAmount = 0;
            return;
        }

        var instance = Entity.FindActiveConsumable(TargetItemKey);
        if (instance == null) Slot.Fill.fillAmount = 0;
        else Slot.Fill.fillAmount = instance.RemainingDuration / instance.Duration;
    }

    public string TargetItemKey => Target?.ItemKey ?? "";

}

public class ActionBarGearBinding : ActionBarBinding
{
    public EquippedItem Item { get; }
    public IActivatedBehavior Behavior { get; }

    public bool Active;

    public int EquipmentIndex => Entity?.Equipment?.IndexOf(Item) ?? -1;

    public int BehaviorIndex => Item?.Behaviors == null ? -1 : Array.IndexOf(Item.Behaviors, Behavior);

    public string TargetItemKey => Item?.EquippableItem?.ItemKey ?? "";

    public ActionBarGearBinding(
        Entity entity,
        ActionBarSlot slot,
        Func<AetheriaControl> resolveControl,
        GameSettings settings,
        EquippedItem item,
        IActivatedBehavior behavior) : base(entity, slot, resolveControl, settings)
    {
        Item = item;
        Behavior = behavior;
        Slot.QuantityRemaining.gameObject.SetActive(false);
        Slot.Icon.gameObject.SetActive(true);
        Slot.Label.gameObject.SetActive(false);
        Slot.Icon.texture = ResolveIconTexture();
    }

    private Texture2D ResolveIconTexture()
    {
        var typedItem = FindCatalogItem(Item.EquippableItem);
        if (typedItem != null)
        {
            var actionBarIcon = LoadActionBarIcon(typedItem.ActionBarIcon);
            if (actionBarIcon != null)
                return actionBarIcon;

            if (Enum.TryParse<WeaponType>(typedItem.WeaponType, out var weaponType))
                return Settings.GetIcon(weaponType).texture;

            if (Enum.TryParse<HardpointType>(typedItem.HardpointType, out var hardpointType))
                return Settings.GetIcon(hardpointType).texture;
        }

        return Settings.GetIcon(HardpointType.Tool).texture;
    }

    private static Texture2D LoadActionBarIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        const string resourcesPrefix = "Assets/Resources/";
        var resourcePath = path.StartsWith(resourcesPrefix, StringComparison.Ordinal)
            ? path.Substring(resourcesPrefix.Length)
            : path;

        resourcePath = resourcePath.Split('.').First();
        return Resources.Load<Texture2D>(resourcePath);
    }

    public override void Activate()
    {
        Active = true;
        TrySubmit(
            operations => operations.SetBehaviorActive(EquipmentIndex, BehaviorIndex, true),
            "behavior activation");
    }

    public override void Deactivate()
    {
        Active = false;
        TrySubmit(
            operations => operations.SetBehaviorActive(EquipmentIndex, BehaviorIndex, false),
            "behavior activation");
    }

    public override void Update()
    {
        Slot.Fill.fillAmount = Active ? 1 : 0;
    }
}

public class ActionBarWeaponGroupBinding : ActionBarBinding
{
    public int Group;

    public ActionBarWeaponGroupBinding(
        Entity entity,
        ActionBarSlot slot,
        Func<AetheriaControl> resolveControl,
        GameSettings settings,
        int group) : base(entity, slot, resolveControl, settings)
    {
        Group = group;
        slot.Label.gameObject.SetActive(true);
        slot.Label.text = $"G{Group+1}";
        Slot.Icon.gameObject.SetActive(false);
    }

    public override void Activate()
    {
        TrySubmit(
            operations => operations.SetWeaponGroupActive(Group, true),
            "weapon-group");
    }

    public override void Deactivate()
    {
        TrySubmit(
            operations => operations.SetWeaponGroupActive(Group, false),
            "weapon-group");
    }

    public override void Update()
    {

    }
}
