using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;

namespace Aetheria.State;

public static class AetheriaHangar
{
    private static readonly SemaphoreSlim AdmissionGate = new(1, 1);

    public static async Task<AetheriaDeploymentReceipt> AdmitAsync(
        AetheriaStateNode node,
        AetheriaDeploymentRequest request,
        string now)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        if (request == null) throw new ArgumentNullException(nameof(request));

        await AdmissionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var pointer = node.MutableDocument<AetheriaHangarState>(AetheriaStateNode.HangarKey);
            var hangar = await pointer.ReadAsync().ConfigureAwait(false)
                ?? throw new InvalidOperationException("The canonical Hangar document does not exist.");
            var loadout = string.IsNullOrWhiteSpace(request.LoadoutTemplateKey)
                ? null
                : await node.MutableDocument<AetheriaLoadoutTemplate>(new(request.LoadoutTemplateKey))
                    .ReadAsync().ConfigureAwait(false);
            var updated = Clone(hangar);
            var receipt = Admit(updated, request, loadout, now);
            if (!receipt.Accepted)
                return receipt;

            await pointer.ReplaceAsync(updated).ConfigureAwait(false);
            await node.FlushAsync().ConfigureAwait(false);
            return receipt;
        }
        finally
        {
            AdmissionGate.Release();
        }
    }

    public static AetheriaDeploymentReceipt Admit(
        AetheriaHangarState hangar,
        AetheriaDeploymentRequest request,
        AetheriaLoadoutTemplate? loadout,
        string now)
    {
        if (hangar == null) throw new ArgumentNullException(nameof(hangar));
        if (request == null) throw new ArgumentNullException(nameof(request));

        var prior = (hangar.Deployments ?? []).FirstOrDefault(value =>
            string.Equals(value.RequestId, request.RequestId, StringComparison.Ordinal));
        if (prior != null)
            return prior;

        var rejection = Validate(hangar, request, loadout);
        if (rejection != null)
            return Receipt(hangar, request, loadout, now, false, rejection, hangar.Revision);

        var ship = hangar.Ships.Single(value =>
            string.Equals(value.ShipId, request.ShipId, StringComparison.Ordinal));
        var revision = checked(hangar.Revision + 1);
        var receipt = Receipt(hangar, request, loadout, now, true, "", revision);

        ship.Status = AetheriaHangarShipStatuses.Deployed;
        ship.ActiveDeploymentId = receipt.DeploymentId;
        ship.LoadoutTemplateKey = request.LoadoutTemplateKey;
        hangar.Revision = revision;
        hangar.UpdatedAtUtc = now;
        hangar.Deployments = (hangar.Deployments ?? []).Append(receipt).ToArray();
        return receipt;
    }

    private static string? Validate(
        AetheriaHangarState hangar,
        AetheriaDeploymentRequest request,
        AetheriaLoadoutTemplate? loadout)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId)) return "deployment request id is required";
        if (!AetheriaGameModes.IsKnown(request.Mode)) return "unknown game mode";
        if (!string.Equals(hangar.PlayerKey, request.PlayerKey, StringComparison.Ordinal)) return "player does not own hangar";
        if (request.ExpectedHangarRevision != hangar.Revision) return "hangar revision mismatch";
        if (!string.Equals(request.ModePolicyId, AetheriaModePolicies.ForMode(request.Mode), StringComparison.Ordinal))
            return "mode policy does not match game mode";

        var ships = (hangar.Ships ?? []).Where(value =>
            string.Equals(value.ShipId, request.ShipId, StringComparison.Ordinal)).ToArray();
        if (ships.Length != 1) return "deployment ship is not uniquely owned by hangar";
        if (!string.Equals(ships[0].Status, AetheriaHangarShipStatuses.Available, StringComparison.Ordinal))
            return "deployment ship is not available";
        if (!(hangar.LoadoutTemplateKeys ?? []).Contains(request.LoadoutTemplateKey, StringComparer.Ordinal))
            return "loadout template is not owned by hangar";
        if (loadout == null) return "loadout template is missing";
        if (!string.Equals(loadout.OwnerPlayerKey, request.PlayerKey, StringComparison.Ordinal))
            return "player does not own loadout template";
        if (!string.Equals(loadout.RootEntity.Hull.ItemKey, ships[0].HullItemKey, StringComparison.Ordinal))
            return "loadout hull does not match deployed ship";
        return null;
    }

    private static AetheriaDeploymentReceipt Receipt(
        AetheriaHangarState hangar,
        AetheriaDeploymentRequest request,
        AetheriaLoadoutTemplate? loadout,
        string now,
        bool accepted,
        string diagnostic,
        long revision) => new()
    {
        DeploymentId = accepted ? $"deployment:{hangar.HangarId}:{revision}" : "",
        RequestId = request.RequestId,
        Accepted = accepted,
        Diagnostic = diagnostic,
        PlayerKey = request.PlayerKey,
        Mode = request.Mode,
        ShipId = request.ShipId,
        LoadoutTemplateKey = request.LoadoutTemplateKey,
        HangarRevision = revision,
        ModePolicyId = request.ModePolicyId,
        Loadout = accepted ? Clone(loadout!.RootEntity) : new AetheriaRuntimeEntityLoadoutCommit(),
        CommittedAtUtc = now
    };

    private static AetheriaRuntimeEntityLoadoutCommit Clone(AetheriaEntityLoadout source) => new()
    {
        Name = source.Name,
        Kind = source.Kind,
        FactionKey = source.FactionKey,
        Hull = Clone(source.Hull),
        Equipment = (source.Equipment ?? []).Select(Clone).ToArray(),
        CargoBays = (source.CargoBays ?? []).Select(Clone).ToArray(),
        DockingBays = (source.DockingBays ?? []).Select(Clone).ToArray(),
        CargoContents = (source.CargoContents ?? []).Select(Clone).ToArray(),
        DockingBayContents = (source.DockingBayContents ?? []).Select(Clone).ToArray(),
        DockingBayAssignments = (source.DockingBayAssignments ?? []).ToArray(),
        WeaponGroups = (source.WeaponGroups ?? []).Select(group => (group ?? []).ToArray()).ToArray(),
        Children = (source.Children ?? []).Select(Clone).ToArray()
    };

    private static AetheriaHangarState Clone(AetheriaHangarState source) => new()
    {
        Name = source.Name,
        HangarId = source.HangarId,
        PlayerKey = source.PlayerKey,
        Revision = source.Revision,
        Ships = (source.Ships ?? []).Select(ship => new AetheriaHangarShip
        {
            ShipId = ship.ShipId,
            HullItemKey = ship.HullItemKey,
            LoadoutTemplateKey = ship.LoadoutTemplateKey,
            Status = ship.Status,
            ActiveDeploymentId = ship.ActiveDeploymentId
        }).ToArray(),
        Inventory = (source.Inventory ?? []).Select(item => new AetheriaHangarItemStack
        {
            ItemKey = item.ItemKey,
            Quantity = item.Quantity
        }).ToArray(),
        Currencies = (source.Currencies ?? []).Select(currency => new AetheriaHangarCurrency
        {
            CurrencyKey = currency.CurrencyKey,
            Quantity = currency.Quantity
        }).ToArray(),
        UnlockKeys = (source.UnlockKeys ?? []).ToArray(),
        LoadoutTemplateKeys = (source.LoadoutTemplateKeys ?? []).ToArray(),
        Deployments = (source.Deployments ?? []).ToArray(),
        UpdatedAtUtc = source.UpdatedAtUtc
    };

    private static AetheriaRuntimeLoadoutItemSlotCommit Clone(AetheriaLoadoutItemSlot source) => new()
    {
        X = source.Position.X,
        Y = source.Position.Y,
        Rotation = source.Rotation,
        Item = Clone(source.Item)
    };

    private static AetheriaRuntimeCargoBayLoadoutCommit Clone(AetheriaCargoBayLoadout source) => new()
    {
        Items = (source.Items ?? []).Select(Clone).ToArray()
    };

    private static AetheriaRuntimeLoadoutItemCommit Clone(AetheriaLoadoutItem source) => new()
    {
        ItemKey = source.ItemKey,
        Quality = source.Quality,
        Durability = source.Durability,
        Quantity = source.Quantity,
        Enabled = source.Enabled,
        OverrideShutdown = source.OverrideShutdown,
        Temperature = source.Temperature
    };
}
