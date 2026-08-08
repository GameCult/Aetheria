using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;

namespace Aetheria.State;

public static class AetheriaRuntimeStateMapper
{
    public static AetheriaTradeValuePolicy ToTradeValuePolicy(
        AetheriaRuntimeTradeValueSettings settings,
        string updatedAtUtc)
    {
        return new AetheriaTradeValuePolicy
        {
            QualityPriceModifier = new AetheriaExponentialLerp
            {
                Exponent = settings.QualityPriceModifier.Exponent,
                Minimum = settings.QualityPriceModifier.Minimum,
                Maximum = settings.QualityPriceModifier.Maximum
            },
            Tiers = settings.Tiers
                .Select(tier => new AetheriaItemRarityTier
                {
                    Name = tier.Name,
                    Quality = tier.Quality,
                    Red = tier.Red,
                    Green = tier.Green,
                    Blue = tier.Blue
                })
                .ToArray(),
            UpdatedAtUtc = updatedAtUtc
        };
    }

    public static AetheriaRuntimeTradeValueSettings ToRuntimeTradeValueSettings(
        AetheriaTradeValuePolicy? policy)
    {
        if (policy == null)
            return AetheriaRuntimeTradeValueSettings.Default;

        return new AetheriaRuntimeTradeValueSettings(
            new AetheriaRuntimeExponentialLerp(
                policy.QualityPriceModifier?.Exponent ?? 1,
                policy.QualityPriceModifier?.Minimum ?? 1,
                policy.QualityPriceModifier?.Maximum ?? 1),
            (policy.Tiers ?? Array.Empty<AetheriaItemRarityTier>())
                .Select(tier => new AetheriaRuntimeItemRarityTier(
                    tier.Name,
                    tier.Quality,
                    tier.Red,
                    tier.Green,
                    tier.Blue))
                .ToArray());
    }

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

    public static AetheriaRuntimeLoadoutTemplateCommit ToRuntimeLoadoutTemplate(
        AetheriaLoadoutTemplate loadout)
    {
        if (loadout == null) throw new ArgumentNullException(nameof(loadout));
        return new AetheriaRuntimeLoadoutTemplateCommit
        {
            Name = loadout.Name,
            OwnerPlayerKey = loadout.OwnerPlayerKey,
            RootEntity = ToRuntimeEntityLoadout(loadout.RootEntity)
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

    private static AetheriaRuntimeEntityLoadoutCommit ToRuntimeEntityLoadout(AetheriaEntityLoadout entity) => new()
    {
        Name = entity.Name,
        Kind = entity.Kind,
        FactionKey = entity.FactionKey,
        Hull = ToRuntimeLoadoutItem(entity.Hull),
        Equipment = (entity.Equipment ?? []).Select(ToRuntimeItemSlot).ToArray(),
        CargoBays = (entity.CargoBays ?? []).Select(ToRuntimeItemSlot).ToArray(),
        DockingBays = (entity.DockingBays ?? []).Select(ToRuntimeItemSlot).ToArray(),
        CargoContents = (entity.CargoContents ?? []).Select(value => new AetheriaRuntimeCargoBayLoadoutCommit
        {
            Items = (value.Items ?? []).Select(ToRuntimeItemSlot).ToArray()
        }).ToArray(),
        DockingBayContents = (entity.DockingBayContents ?? []).Select(value => new AetheriaRuntimeCargoBayLoadoutCommit
        {
            Items = (value.Items ?? []).Select(ToRuntimeItemSlot).ToArray()
        }).ToArray(),
        DockingBayAssignments = (entity.DockingBayAssignments ?? []).ToArray(),
        WeaponGroups = (entity.WeaponGroups ?? []).Select(value => (IReadOnlyList<int>)(value ?? []).ToArray()).ToArray(),
        Children = (entity.Children ?? []).Select(ToRuntimeEntityLoadout).ToArray()
    };

    private static AetheriaRuntimeLoadoutItemSlotCommit ToRuntimeItemSlot(AetheriaLoadoutItemSlot slot) => new()
    {
        X = slot.Position?.X ?? 0,
        Y = slot.Position?.Y ?? 0,
        Rotation = slot.Rotation,
        Item = ToRuntimeLoadoutItem(slot.Item)
    };

    private static AetheriaRuntimeLoadoutItemCommit ToRuntimeLoadoutItem(AetheriaLoadoutItem item) => new()
    {
        ItemKey = item.ItemKey,
        Quality = item.Quality,
        Durability = item.Durability,
        Quantity = item.Quantity,
        Enabled = item.Enabled,
        OverrideShutdown = item.OverrideShutdown,
        Temperature = item.Temperature
    };

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
            OverrideShutdown = item.OverrideShutdown,
            Temperature = item.Temperature
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
                Rotation = slot.Rotation ?? "None",
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
