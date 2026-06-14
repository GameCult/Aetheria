using System;
using System.Linq;
using GameCult.Aetheria.State.Unity;
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

    public ActionBarBinding Binding
    {
        get => binding;
        set
        {
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

    private void Update()
    {
        Binding?.Update();
    }

}

public abstract class ActionBarBinding
{
    public Entity Entity { get; }
    protected ActionBarSlot Slot { get; }
    public abstract void Activate();
    public abstract void Deactivate();
    public abstract void Update();
    public ActionBarBinding(Entity entity, ActionBarSlot slot)
    {
        Entity = entity;
        Slot = slot;
    }
}

public class ActionBarConsumableBinding : ActionBarBinding
{
    public AetheriaRuntimeCatalogItem Target { get; }
    private readonly Func<ConsumableItemData> _resolveLegacyTarget;

    public ActionBarConsumableBinding(
        Entity entity,
        ActionBarSlot slot,
        AetheriaRuntimeCatalogItem target,
        Func<ConsumableItemData> resolveLegacyTarget) : base(entity, slot)
    {
        Target = target;
        _resolveLegacyTarget = resolveLegacyTarget;
        Slot.QuantityRemaining.gameObject.SetActive(true);
        Slot.Label.gameObject.SetActive(false);
        Slot.Icon.gameObject.SetActive(false);
    }

    public override void Activate()
    {
        var legacyTarget = ResolveLegacyTarget();
        if (legacyTarget != null)
            Entity.TryActivateConsumable(legacyTarget);
    }

    public override void Deactivate()
    {
    }

    public override void Update()
    {
        Slot.QuantityRemaining.text = $"{Entity.CountItemsInCargo(TargetLegacyId)}";
        var legacyTarget = ResolveLegacyTarget();
        if (legacyTarget == null)
        {
            Slot.Fill.fillAmount = 0;
            return;
        }

        var instance = Entity.FindActiveConsumable(legacyTarget);
        if (instance == null) Slot.Fill.fillAmount = 0;
        else Slot.Fill.fillAmount = instance.RemainingDuration / instance.Data.Duration;
    }

    private Guid TargetLegacyId =>
        Guid.TryParse(Target.LegacyId, out var legacyId) ? legacyId : Guid.Empty;

    private ConsumableItemData ResolveLegacyTarget()
    {
        return _resolveLegacyTarget?.Invoke();
    }
}

public class ActionBarGearBinding : ActionBarBinding
{
    public EquippedItem Item { get; }
    public IActivatedBehavior Behavior { get; }

    public bool Active;

    public ActionBarGearBinding(Entity entity, ActionBarSlot slot, EquippedItem item, IActivatedBehavior behavior) : base(entity, slot)
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
        var typedItem = FindTypedGearItem(Item.EquippableItem);
        if (typedItem != null)
        {
            var actionBarIcon = LoadActionBarIcon(typedItem.ActionBarIcon);
            if (actionBarIcon != null)
                return actionBarIcon;

            if (Enum.TryParse<WeaponType>(typedItem.WeaponType, out var weaponType))
                return ActionGameManager.Instance.Settings.GetIcon(weaponType).texture;

            if (Enum.TryParse<HardpointType>(typedItem.HardpointType, out var hardpointType))
                return ActionGameManager.Instance.Settings.GetIcon(hardpointType).texture;
        }

        return ActionGameManager.Instance.Settings.GetIcon(HardpointType.Tool).texture;
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

    private static AetheriaRuntimeCatalogItem FindTypedGearItem(ItemInstance item)
    {
        var itemId = item?.Data?.ItemId ?? Guid.Empty;
        return itemId == Guid.Empty
            ? null
            : ActionGameManager.RuntimeCatalog?.FindItemByLegacyId(itemId.ToString("D"));
    }

    public override void Activate()
    {
        Active = true;
        Behavior.Activate();
    }

    public override void Deactivate()
    {
        Active = false;
        Behavior.Deactivate();
    }

    public override void Update()
    {
        Slot.Fill.fillAmount = Active ? 1 : 0;
    }
}

public class ActionBarWeaponGroupBinding : ActionBarBinding
{
    public int Group;

    public ActionBarWeaponGroupBinding(Entity entity, ActionBarSlot slot, int group) : base(entity, slot)
    {
        Group = group;
        slot.Label.gameObject.SetActive(true);
        slot.Label.text = $"G{Group+1}";
        Slot.Icon.gameObject.SetActive(false);
    }

    public override void Activate()
    {
        foreach (var weapon in Entity.WeaponGroups[Group].weapons)
        {
            weapon.Activate();
        }
    }

    public override void Deactivate()
    {
        foreach (var weapon in Entity.WeaponGroups[Group].weapons)
        {
            weapon.Deactivate();
        }
    }

    public override void Update()
    {

    }
}
