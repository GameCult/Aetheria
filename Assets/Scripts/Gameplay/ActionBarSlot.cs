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
    public ConsumableItemData Target;

    public ActionBarConsumableBinding(Entity entity, ActionBarSlot slot, ConsumableItemData target) : base(entity, slot)
    {
        Target = target;
        Slot.QuantityRemaining.gameObject.SetActive(true);
        var data = Target;
        if(!string.IsNullOrEmpty(data.Icon))
        {
            Slot.Label.gameObject.SetActive(false);
            Slot.Icon.gameObject.SetActive(true);
            Slot.Icon.texture = Resources.Load<Texture2D>(data.Icon.Substring("Assets/Resources/".Length).Split('.').First());
        }
        else Slot.Icon.gameObject.SetActive(false);
    }

    public override void Activate()
    {
        Entity.TryActivateConsumable(Target);
    }

    public override void Deactivate()
    {
    }

    public override void Update()
    {
        Slot.QuantityRemaining.text = $"{Entity.CountItemsInCargo(Target.ID)}";
        var instance = Entity.FindActiveConsumable(Target);
        if (instance == null) Slot.Fill.fillAmount = 0;
        else Slot.Fill.fillAmount = instance.RemainingDuration / instance.Data.Duration;
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
        if (!string.IsNullOrEmpty(Item.Data.ActionBarIcon))
            Slot.Icon.texture = Resources.Load<Texture2D>(Item.Data.ActionBarIcon.Substring("Assets/Resources/".Length).Split('.').First());
        else Slot.Icon.texture = ResolveIconTexture();
    }

    private Texture2D ResolveIconTexture()
    {
        var typedItem = FindTypedGearItem(Item.EquippableItem);
        if (typedItem != null)
        {
            if (Enum.TryParse<WeaponType>(typedItem.WeaponType, out var weaponType))
                return ActionGameManager.Instance.Settings.GetIcon(weaponType).texture;

            if (Enum.TryParse<HardpointType>(typedItem.HardpointType, out var hardpointType))
                return ActionGameManager.Instance.Settings.GetIcon(hardpointType).texture;
        }

        return Item.Data is WeaponItemData weaponItemData
            ? ActionGameManager.Instance.Settings.GetIcon(weaponItemData.WeaponType).texture
            : ActionGameManager.Instance.Settings.GetIcon(Item.Data.HardpointType).texture;
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
