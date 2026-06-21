using Aetheria.State.Documents;
using GameCult.Aetheria.State.Unity;
using GameCult.Caching;

namespace Aetheria.State;

public static class AetheriaRuntimeStateMapper
{
    public static AetheriaLoadoutTemplate ToLoadoutTemplate(
        AetheriaRuntimeLoadoutTemplateCommit loadout,
        string updatedAtUtc)
    {
        return new AetheriaLoadoutTemplate
        {
            Name = loadout.Name ?? "",
            OwnerPlayerKey = loadout.OwnerPlayerKey ?? "",
            RootEntity = ToEntityLoadout(loadout.RootEntity),
            CreatedAtUtc = updatedAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    public static CultRecordKey LoadoutKey(string name)
    {
        return new CultRecordKey($"global:aetheria.loadout_template.{StableToken(name)}.v1");
    }

    private static AetheriaEntityLoadout ToEntityLoadout(AetheriaRuntimeEntityLoadoutCommit? entity)
    {
        entity ??= new AetheriaRuntimeEntityLoadoutCommit();
        return new AetheriaEntityLoadout
        {
            Name = entity.Name ?? "",
            Kind = entity.Kind ?? "",
            FactionKey = entity.FactionKey ?? "",
            Hull = ToLoadoutItem(entity.Hull),
            Equipment = ToItemSlots(entity.Equipment),
            CargoBays = ToItemSlots(entity.CargoBays),
            DockingBays = ToItemSlots(entity.DockingBays),
            CargoContents = ToCargoBays(entity.CargoContents),
            DockingBayContents = ToCargoBays(entity.DockingBayContents),
            DockingBayAssignments = ToIntArray(entity.DockingBayAssignments),
            WeaponGroups = ToIntArrayArray(entity.WeaponGroups),
            Children = (entity.Children ?? Array.Empty<AetheriaRuntimeEntityLoadoutCommit>())
                .Select(ToEntityLoadout)
                .ToArray()
        };
    }

    private static AetheriaLoadoutItem ToLoadoutItem(AetheriaRuntimeLoadoutItemCommit? item)
    {
        item ??= new AetheriaRuntimeLoadoutItemCommit();
        return new AetheriaLoadoutItem
        {
            ItemKey = item.ItemKey ?? "",
            Quality = item.Quality,
            Durability = item.Durability,
            Quantity = item.Quantity,
            Enabled = item.Enabled,
            OverrideShutdown = item.OverrideShutdown
        };
    }

    private static AetheriaLoadoutItemSlot[] ToItemSlots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            .Select(slot => new AetheriaLoadoutItemSlot
            {
                Position = new AetheriaGridCoord
                {
                    X = slot.X,
                    Y = slot.Y
                },
                Item = ToLoadoutItem(slot.Item)
            })
            .ToArray();
    }

    private static AetheriaCargoBayLoadout[] ToCargoBays(
        IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit>? cargoBays)
    {
        return (cargoBays ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
            .Select(bay => new AetheriaCargoBayLoadout
            {
                Items = ToItemSlots(bay.Items)
            })
            .ToArray();
    }

    private static int[] ToIntArray(IReadOnlyList<int>? values)
    {
        return (values ?? Array.Empty<int>()).ToArray();
    }

    private static int[][] ToIntArrayArray(IReadOnlyList<IReadOnlyList<int>>? values)
    {
        return (values ?? Array.Empty<IReadOnlyList<int>>())
            .Select(ToIntArray)
            .ToArray();
    }

    private static string StableToken(string value)
    {
        var chars = (string.IsNullOrWhiteSpace(value) ? "unnamed" : value.Trim().ToLowerInvariant())
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var token = new string(chars).Trim('-');
        while (token.Contains("--", StringComparison.Ordinal))
            token = token.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(token) ? "unnamed" : token;
    }
}
