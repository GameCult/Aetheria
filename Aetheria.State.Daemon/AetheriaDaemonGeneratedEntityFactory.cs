using Aetheria.State.Documents;

internal static class AetheriaDaemonGeneratedEntityFactory
{
    public static AetheriaEntitySnapshot Create(
        string name,
        string kind,
        string factionKey,
        AetheriaDaemonLoadout loadout,
        double x = 0,
        double z = 0,
        string orbitKey = "",
        int securityLevel = 0,
        double securityRadius = 0)
    {
        var orbital = IsOrbital(kind);
        return new AetheriaEntitySnapshot
        {
            Name = name,
            Kind = kind,
            Position = new AetheriaVector3 { X = x, Z = z },
            Direction = new AetheriaVector2 { Y = 1 },
            LookDirection = new AetheriaVector2 { Y = 1 },
            Velocity = new AetheriaVector2(),
            FactionKey = factionKey,
            HullItemKey = loadout.HullItemKey,
            LoadoutGeneration = loadout.Receipt,
            OrbitKey = orbitKey,
            SecurityLevel = securityLevel,
            SecurityRadius = securityRadius,
            IsActive = true,
            HeatsinksEnabled = true,
            Visibility = orbital ? 760 : 420,
            VisibilitySourceCount = 1,
            StatGrids =
            [
                StatGrid("hull", orbital ? 420 : 130),
                StatGrid("shield", orbital ? 130 : 50),
                StatGrid("heat", 0)
            ],
            Equipment = loadout.Equipment,
            CargoBays = loadout.CargoBays,
            DockingBays = loadout.DockingBays,
            WeaponGroups = loadout.WeaponGroups
                .Select(indices => new AetheriaWeaponGroupSnapshot { EquipmentIndices = indices })
                .ToArray(),
            CargoContents = loadout.CargoBays
                .Select((_, index) => new AetheriaCargoBayLoadout
                {
                    Items = index == 0 ? loadout.Cargo : []
                })
                .ToArray(),
            DockingBayContents = loadout.DockingBays
                .Select(_ => new AetheriaCargoBayLoadout())
                .ToArray(),
            DockingBayAssignments = loadout.DockingBays.Select(_ => -1).ToArray()
        };
    }

    private static bool IsOrbital(string kind) =>
        string.Equals(kind, "station", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "turret", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "orbital", StringComparison.OrdinalIgnoreCase);

    private static AetheriaEntityStatGrid StatGrid(string name, double value) => new()
    {
        Name = name,
        Width = 1,
        Height = 1,
        Values = [value]
    };
}
