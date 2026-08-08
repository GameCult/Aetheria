using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;

public static class AetheriaDaemonHangarCoordinator
{
    public const string LocalPlayerKey = "player:local";
    public const string StarterShipId = "ship:local:vanguard";
    public const string StarterLoadoutName = "Vanguard One";

    public static async Task EnsureAsync(
        AetheriaStateNode node,
        AetheriaRuntimeCatalogSnapshot catalog,
        string now)
    {
        var existing = await node.MutableDocument<AetheriaHangarState>(AetheriaStateNode.HangarKey)
            .ReadAsync().ConfigureAwait(false);
        if (existing != null)
            return;

        var factionKey = (catalog.Corporations ?? [])
            .Select(value => value.CorporationKey)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? throw new InvalidDataException("Hangar creation requires one typed corporation.");
        var generator = new AetheriaDaemonLoadoutGenerator(
            catalog,
            AetheriaDaemonZoneGenerator.GenerationSeed,
            0,
            new Dictionary<string, int>(StringComparer.Ordinal) { [factionKey] = 0 },
            new Dictionary<int, IReadOnlyList<int>> { [0] = [] },
            isPrelude: true);
        var generated = generator.Build("ship", factionKey);
        var loadoutCommit = new AetheriaRuntimeLoadoutTemplateCommit
        {
            Name = StarterLoadoutName,
            OwnerPlayerKey = LocalPlayerKey,
            RootEntity = ToRuntimeLoadout(generated, factionKey)
        };
        var loadout = AetheriaRuntimeStateMapper.ToLoadoutTemplate(loadoutCommit, now);
        var loadoutKey = AetheriaRuntimeStateMapper.LoadoutKey(loadout.Name);
        var installed = generated.Equipment
            .Concat(generated.CargoBays)
            .Concat(generated.DockingBays)
            .Select(value => value.ItemKey)
            .ToHashSet(StringComparer.Ordinal);
        var inventory = catalog.EquipmentItems
            .Where(value => !string.IsNullOrWhiteSpace(value.ItemKey) && !installed.Contains(value.ItemKey))
            .OrderBy(value => value.Price)
            .ThenBy(value => value.ItemKey, StringComparer.Ordinal)
            .Take(24)
            .Select(value => new AetheriaHangarItemStack { ItemKey = value.ItemKey, Quantity = 1 })
            .ToArray();

        await node.MutableDocument<AetheriaLoadoutTemplate>(loadoutKey).ReplaceAsync(loadout).ConfigureAwait(false);
        await node.MutableDocument<AetheriaHangarState>(AetheriaStateNode.HangarKey).ReplaceAsync(new AetheriaHangarState
        {
            HangarId = "local",
            PlayerKey = LocalPlayerKey,
            Revision = 1,
            Ships =
            [
                new AetheriaHangarShip
                {
                    ShipId = StarterShipId,
                    HullItemKey = generated.HullItemKey,
                    LoadoutTemplateKey = loadoutKey.ToString(),
                    Status = AetheriaHangarShipStatuses.Available
                }
            ],
            Inventory = inventory,
            LoadoutTemplateKeys = [loadoutKey.ToString()],
            UpdatedAtUtc = now
        }).ConfigureAwait(false);
        await node.FlushAsync().ConfigureAwait(false);
    }

    public static async Task<AetheriaDeploymentReceipt> LaunchTerminusAsync(
        AetheriaStateNode node,
        AetheriaRuntimeCatalogSnapshot catalog,
        string requestId,
        string shipId,
        long expectedRevision,
        string now)
    {
        var hangar = await node.MutableDocument<AetheriaHangarState>(AetheriaStateNode.HangarKey)
            .ReadAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("The canonical Hangar document does not exist.");
        var ship = (hangar.Ships ?? []).SingleOrDefault(value => string.Equals(value.ShipId, shipId, StringComparison.Ordinal));
        var receipt = await AetheriaHangar.AdmitAsync(node, new AetheriaDeploymentRequest
        {
            RequestId = requestId,
            PlayerKey = hangar.PlayerKey,
            Mode = AetheriaGameModes.Terminus,
            ShipId = shipId,
            LoadoutTemplateKey = ship?.LoadoutTemplateKey ?? "",
            ExpectedHangarRevision = expectedRevision,
            ModePolicyId = AetheriaModePolicies.TerminusLocal
        }, now).ConfigureAwait(false);
        if (!receipt.Accepted)
            return receipt;

        await AetheriaDaemonZoneGenerator.WritePlayableRunAsync(
            node,
            catalog,
            now,
            AetheriaDaemonTerminusScenarios.Standard,
            receipt).ConfigureAwait(false);
        return receipt;
    }

    public static async Task<bool> CanContinueTerminusAsync(
        AetheriaStateNode node,
        string shipId,
        string deploymentId)
    {
        var hangar = await node.MutableDocument<AetheriaHangarState>(AetheriaStateNode.HangarKey)
            .ReadAsync().ConfigureAwait(false);
        var ship = (hangar?.Ships ?? []).SingleOrDefault(value =>
            string.Equals(value.ShipId, shipId, StringComparison.Ordinal) &&
            string.Equals(value.ActiveDeploymentId, deploymentId, StringComparison.Ordinal) &&
            string.Equals(value.Status, AetheriaHangarShipStatuses.Deployed, StringComparison.Ordinal));
        if (ship == null)
            return false;
        var deployment = (hangar!.Deployments ?? []).SingleOrDefault(value =>
            value.Accepted &&
            string.Equals(value.DeploymentId, deploymentId, StringComparison.Ordinal) &&
            string.Equals(value.Mode, AetheriaGameModes.Terminus, StringComparison.Ordinal));
        if (deployment == null)
            return false;
        var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
            .ReadAsync().ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(settings?.ActiveRunKey);
    }

    private static AetheriaRuntimeEntityLoadoutCommit ToRuntimeLoadout(
        AetheriaDaemonLoadout source,
        string factionKey) => new()
    {
        Name = StarterLoadoutName,
        Kind = "ship",
        FactionKey = factionKey,
        Hull = new AetheriaRuntimeLoadoutItemCommit
        {
            ItemKey = source.HullItemKey,
            Quality = 1,
            Durability = 1,
            Quantity = 1,
            Enabled = true
        },
        Equipment = source.Equipment.Select(ToRuntimeSlot).ToArray(),
        CargoBays = source.CargoBays.Select(ToRuntimeSlot).ToArray(),
        DockingBays = source.DockingBays.Select(ToRuntimeSlot).ToArray(),
        CargoContents = source.CargoBays.Select((_, index) => new AetheriaRuntimeCargoBayLoadoutCommit
        {
            Items = index == 0 ? source.Cargo.Select(ToRuntimeSlot).ToArray() : []
        }).ToArray(),
        DockingBayContents = source.DockingBays.Select(_ => new AetheriaRuntimeCargoBayLoadoutCommit()).ToArray(),
        DockingBayAssignments = source.DockingBays.Select(_ => -1).ToArray(),
        WeaponGroups = source.WeaponGroups.Select(group => (IReadOnlyList<int>)group.ToArray()).ToArray()
    };

    private static AetheriaRuntimeLoadoutItemSlotCommit ToRuntimeSlot(AetheriaEntityItemSlot slot) => new()
    {
        X = slot.Position?.X ?? 0,
        Y = slot.Position?.Y ?? 0,
        Rotation = slot.Rotation,
        Item = new AetheriaRuntimeLoadoutItemCommit
        {
            ItemKey = slot.ItemKey,
            Quality = slot.Quality,
            Durability = slot.Durability,
            Quantity = slot.Quantity,
            Enabled = slot.Enabled,
            OverrideShutdown = slot.OverrideShutdown,
            Temperature = slot.Temperature
        }
    };

    private static AetheriaRuntimeLoadoutItemSlotCommit ToRuntimeSlot(AetheriaLoadoutItemSlot slot) => new()
    {
        X = slot.Position?.X ?? 0,
        Y = slot.Position?.Y ?? 0,
        Rotation = slot.Rotation,
        Item = new AetheriaRuntimeLoadoutItemCommit
        {
            ItemKey = slot.Item?.ItemKey ?? "",
            Quality = slot.Item?.Quality ?? 1,
            Durability = slot.Item?.Durability ?? 1,
            Quantity = slot.Item?.Quantity ?? 1,
            Enabled = slot.Item?.Enabled ?? true,
            OverrideShutdown = slot.Item?.OverrideShutdown ?? false,
            Temperature = slot.Item?.Temperature ?? 0
        }
    };
}
