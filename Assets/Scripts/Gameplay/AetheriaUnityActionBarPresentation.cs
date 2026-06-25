using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using UnityEngine;

public sealed class AetheriaUnityActionBarPresentation
{
    private IReadOnlyList<ActionBarSlot> _slots = Array.Empty<ActionBarSlot>();
    private AetheriaRuntimeCatalogSnapshot _catalog;
    private GameSettings _settings;
    private Func<Entity> _resolveEntity = () => null;
    private Func<AetheriaClient> _resolveClient = () => null;
    private IReadOnlyList<AetheriaUnityActionBarBinding> _localBindings =
        Array.Empty<AetheriaUnityActionBarBinding>();

    public void Bind(
        IReadOnlyList<ActionBarSlot> slots,
        AetheriaRuntimeCatalogSnapshot catalog,
        GameSettings settings,
        Func<Entity> resolveEntity,
        Func<AetheriaClient> resolveClient)
    {
        _slots = slots ?? Array.Empty<ActionBarSlot>();
        _catalog = catalog;
        _settings = settings;
        _resolveEntity = resolveEntity ?? (() => null);
        _resolveClient = resolveClient ?? (() => null);
    }

    public int SlotCount => _slots?.Count ?? 0;

    public string SlotLabel(int slotIndex)
    {
        var slot = ResolveSlot(slotIndex);
        if (slot == null)
            return "";

        var controlPath = slot.ControlPath ?? "";
        if (string.IsNullOrWhiteSpace(controlPath))
            return "Action Bar";

        var slashIndex = controlPath.LastIndexOf('/');
        return slashIndex >= 0 && slashIndex < controlPath.Length - 1
            ? controlPath.Substring(slashIndex + 1)
            : controlPath;
    }

    public string BindingLabel(int slotIndex)
    {
        var slot = ResolveSlot(slotIndex);
        return slot?.Binding switch
        {
            ActionBarWeaponGroupBinding weaponGroup => $"G{weaponGroup.Group + 1}",
            ActionBarConsumableBinding consumable => consumable.Target?.Name ?? "Consumable",
            ActionBarGearBinding gear => _catalog?.FindItem(gear.Item?.EquippableItem?.ItemKey ?? "")?.Name ?? "Gear",
            _ => "Empty"
        };
    }

    public bool TryResolveControlPath(int slotIndex, out string controlPath)
    {
        controlPath = ResolveSlot(slotIndex)?.ControlPath ?? "";
        return !string.IsNullOrWhiteSpace(controlPath);
    }

    public void RequestBinding(ActionBarSlot slot, DragObject dragAction)
    {
        var entity = _resolveEntity();
        if (slot == null || dragAction == null || entity == null)
            return;

        var binding = CreateBindingCommit(slot, entity, dragAction);
        if (binding == null ||
            string.IsNullOrWhiteSpace(binding.ControlPath) ||
            string.IsNullOrWhiteSpace(binding.Kind))
        {
            return;
        }

        SetLocalBinding(binding);
        ApplyLocalBindings();
    }

    public bool RequestWeaponGroupBinding(int slotIndex, int groupIndex)
    {
        if (groupIndex < 0 || !TryResolveControlPath(slotIndex, out var controlPath))
            return false;

        SetLocalBinding(new AetheriaUnityActionBarBinding
        {
            ControlPath = controlPath,
            Kind = "weapon_group",
            ItemKey = "",
            EquipmentIndex = -1,
            BehaviorIndex = -1,
            WeaponGroup = groupIndex
        });
        ApplyLocalBindings();
        return true;
    }

    public bool ClearBinding(int slotIndex)
    {
        if (!TryResolveControlPath(slotIndex, out var controlPath))
            return false;

        _localBindings = (_localBindings ?? Array.Empty<AetheriaUnityActionBarBinding>())
            .Where(binding => !string.Equals(binding?.ControlPath ?? "", controlPath, StringComparison.Ordinal))
            .ToArray();
        ApplyLocalBindings();
        return true;
    }

    public void ApplyLocalBindings()
    {
        ApplyBindings(_resolveEntity(), _localBindings);
    }

    public void ApplyBindings(
        Entity entity,
        IReadOnlyList<AetheriaUnityActionBarBinding> bindings)
    {
        if (_slots == null || _slots.Count == 0)
            return;

        foreach (var slot in _slots)
            slot.Binding = null;

        if (entity == null || bindings == null || bindings.Count == 0)
            return;

        var slotsByControlPath = _slots
            .Where(slot => !string.IsNullOrWhiteSpace(slot?.ControlPath))
            .GroupBy(slot => slot.ControlPath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            if (binding == null ||
                string.IsNullOrWhiteSpace(binding.ControlPath) ||
                !slotsByControlPath.TryGetValue(binding.ControlPath, out var slot))
            {
                continue;
            }

            var slotBinding = CreateBinding(slot, entity, binding);
            if (slotBinding != null)
                slot.Binding = slotBinding;
        }
    }

    public ActionBarBinding CreateBinding(ActionBarSlot slot, Entity entity, DragObject dragAction)
    {
        var binding = CreateBindingCommit(slot, entity, dragAction);
        return binding == null ? null : CreateBinding(slot, entity, binding);
    }

    private AetheriaUnityActionBarBinding CreateBindingCommit(
        ActionBarSlot slot,
        Entity entity,
        DragObject dragAction)
    {
        if (slot == null || entity == null || dragAction == null)
            return null;

        switch (dragAction)
        {
            case EquippedItemDragObject equippedItemDragAction:
                var equippedItem = equippedItemDragAction.EquippedItem;
                var trigger = equippedItem?.GetBehavior<IActivatedBehavior>();
                var equipmentIndex = equippedItem == null ? -1 : entity.Equipment.IndexOf(equippedItem);
                var behaviorIndex = equippedItem?.Behaviors == null ? -1 : Array.IndexOf(equippedItem.Behaviors, trigger);
                return trigger == null || equipmentIndex < 0 || behaviorIndex < 0
                    ? null
                    : new AetheriaUnityActionBarBinding
                    {
                        ControlPath = slot.ControlPath ?? "",
                        Kind = "gear",
                        ItemKey = equippedItem.EquippableItem?.ItemKey ?? "",
                        EquipmentIndex = equipmentIndex,
                        BehaviorIndex = behaviorIndex
                    };
            case ItemInstanceDragObject itemInstanceDragAction:
                var consumable = FindConsumable(itemInstanceDragAction.Item);
                return consumable == null
                    ? null
                    : new AetheriaUnityActionBarBinding
                    {
                        ControlPath = slot.ControlPath ?? "",
                        Kind = "consumable",
                        ItemKey = consumable.ItemKey ?? ""
                    };
            default:
                return null;
        }
    }

    private void SetLocalBinding(AetheriaUnityActionBarBinding binding)
    {
        if (binding == null || string.IsNullOrWhiteSpace(binding.ControlPath))
            return;

        _localBindings = (_localBindings ?? Array.Empty<AetheriaUnityActionBarBinding>())
            .Where(existing => !string.Equals(existing?.ControlPath ?? "", binding.ControlPath, StringComparison.Ordinal))
            .Concat(new[] { binding })
            .ToArray();
    }

    private ActionBarBinding CreateBinding(
        ActionBarSlot slot,
        Entity entity,
        AetheriaUnityActionBarBinding binding)
    {
        if (slot == null || entity == null || binding == null)
            return null;

        var client = _resolveClient();

        switch (binding.Kind)
        {
            case "consumable":
                var consumable = _catalog?.FindItem(binding.ItemKey ?? "");
                return consumable != null &&
                       string.Equals(consumable.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal)
                    ? new ActionBarConsumableBinding(entity, slot, client, _settings, consumable)
                    : null;
            case "gear":
                if (binding.EquipmentIndex < 0 || binding.EquipmentIndex >= entity.Equipment.Count)
                    return null;

                var equippedItem = entity.Equipment[binding.EquipmentIndex];
                var behaviors = equippedItem?.Behaviors;
                if (equippedItem == null ||
                    !string.Equals(equippedItem.EquippableItem?.ItemKey ?? "", binding.ItemKey ?? "", StringComparison.Ordinal) ||
                    behaviors == null ||
                    binding.BehaviorIndex < 0 ||
                    binding.BehaviorIndex >= behaviors.Length)
                {
                    return null;
                }

                if (!(behaviors[binding.BehaviorIndex] is IActivatedBehavior activatedBehavior))
                    return null;

                return new ActionBarGearBinding(entity, slot, client, _settings, equippedItem, activatedBehavior);
            case "weapon_group":
                return entity.WeaponGroups != null &&
                       binding.WeaponGroup >= 0 &&
                       binding.WeaponGroup < entity.WeaponGroups.Length
                    ? new ActionBarWeaponGroupBinding(entity, slot, client, _settings, binding.WeaponGroup)
                    : null;
            default:
                return null;
        }
    }

    private AetheriaRuntimeCatalogItem FindConsumable(ItemInstance item)
    {
        var typedItem = _catalog?.FindItem(item?.ItemKey ?? "");
        return typedItem != null &&
               string.Equals(typedItem.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal)
            ? typedItem
            : null;
    }

    private ActionBarSlot ResolveSlot(int slotIndex)
    {
        return _slots != null &&
               slotIndex >= 0 &&
               slotIndex < _slots.Count
            ? _slots[slotIndex]
            : null;
    }
}
