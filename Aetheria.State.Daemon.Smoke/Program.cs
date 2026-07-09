using GameCult.Aetheria.State.Verse;

var checks = new AetheriaDaemonCombatKernelSmokeChecks();
checks.Run();
Console.WriteLine("Daemon combat kernel smoke passed.");

internal sealed class AetheriaDaemonCombatKernelSmokeChecks
{
    public void Run()
    {
        DeterministicTicksProduceSameSnapshot();
        ActionableUnresolvedTrackCanFireBeforeVisibility();
        KernelWritesNativeSnapshotRows();
    }

    private static void DeterministicTicksProduceSameSnapshot()
    {
        var left = Scenario();
        var right = Scenario();
        var settings = AetheriaDaemonCombatKernelSettings.Default;

        for (var i = 0; i < 8; i++)
        {
            AetheriaDaemonCombatKernel.Step(left, null, 0.25, catalog: null, settings);
            AetheriaDaemonCombatKernel.Step(right, null, 0.25, catalog: null, settings);
        }

        RequireEqual(Snapshot(left), Snapshot(right), "combat kernel should be deterministic for the same native state");
    }

    private static void ActionableUnresolvedTrackCanFireBeforeVisibility()
    {
        var run = Scenario(
            attackerContactConfidence: 0.35,
            targetShield: 0.0);
        var settings = new AetheriaDaemonCombatKernelSettings
        {
            TrackResolutionPerSecond = 0.0,
            LaunchTrackThreshold = 0.3,
            VisibleTrackThreshold = 0.8,
            AbstractWeaponRange = 500.0,
            DefaultWeaponDamage = 10.0,
            WeaponCooldownSeconds = 1.0
        };

        var report = AetheriaDaemonCombatKernel.Step(run, null, 0.2, catalog: null, settings);
        var attacker = Entity(run, 1);
        var target = Entity(run, 2);
        var contact = attacker.Contacts.Single(contact => contact.TargetEntityIndex == 2);

        RequireEqual(1, report.ShotCount, "actionable unresolved track should permit a committed shot");
        Require(!contact.Visible, "track should remain unresolved below visibility threshold");
        Require(Stat(target, "hull") < 100.0, "shot should apply native hull damage");
    }

    private static void KernelWritesNativeSnapshotRows()
    {
        var run = Scenario();
        var report = AetheriaDaemonCombatKernel.Step(run, null, 0.5, catalog: null);
        var attacker = Entity(run, 1);

        Require(report.ResolvedContactCount > 0, "kernel should resolve contacts into native contact rows");
        Require(attacker.Contacts.Count > 0, "contacts should live on AetheriaRuntimeEntitySnapshotCommit");
        Require(attacker.WeaponStates.Any(state => state.OwnerKind == "daemon-combat-kernel"), "weapon state should be a native weapon state row");
        Require(attacker.StatGrids.Any(grid => grid.Name == "cognitive-load"), "cognition pressure should be a native stat grid row");
        Require(attacker.StatGrids.Any(grid => grid.Name == "heat-capacity"), "thermal capacity should be a native stat grid row");
    }

    private static AetheriaRuntimeRunCheckpointCommit Scenario(
        double attackerContactConfidence = 0.7,
        double targetShield = 15.0)
    {
        return new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "daemon-combat-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.1",
            Zones =
            [
                new AetheriaRuntimeZoneSnapshotCommit
                {
                    ZoneIndex = 0,
                    SimulationTimeSeconds = 0,
                    Entities =
                    [
                        new AetheriaRuntimeEntitySnapshotCommit
                        {
                            EntityIndex = 1,
                            Name = "Cold hunter",
                            Kind = "ship",
                            FactionKey = "player",
                            PositionX = 0,
                            PositionZ = 0,
                            DirectionX = 1,
                            DirectionY = 0,
                            TargetEntityIndex = 2,
                            IsActive = true,
                            Contacts =
                            [
                                new AetheriaRuntimeEntityContactCommit
                                {
                                    TargetEntityIndex = 2,
                                    InfoGathered = attackerContactConfidence,
                                    Hostile = true,
                                    Visible = attackerContactConfidence >= 0.62
                                }
                            ],
                            StatGrids =
                            [
                                Grid("hull", 100.0),
                                Grid("shield", 20.0),
                                Grid("heat", 4.0),
                                Grid("sensor-sensitivity", 1.3),
                                Grid("cognition", 1.4),
                                Grid("fire-control", 1.2),
                                Grid("signature-masking", 0.3)
                            ]
                        },
                        new AetheriaRuntimeEntitySnapshotCommit
                        {
                            EntityIndex = 2,
                            Name = "Hot brute",
                            Kind = "ship",
                            FactionKey = "raider",
                            PositionX = 180,
                            PositionZ = 0,
                            DirectionX = -1,
                            DirectionY = 0,
                            TargetEntityIndex = 1,
                            IsActive = true,
                            StatGrids =
                            [
                                Grid("hull", 100.0),
                                Grid("shield", targetShield),
                                Grid("heat", 18.0),
                                Grid("heat-capacity", 180.0),
                                Grid("signature", 1.4),
                                Grid("signature-masking", 0.05),
                                Grid("sensor-sensitivity", 0.9),
                                Grid("cognition", 0.85),
                                Grid("fire-control", 1.05)
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static AetheriaRuntimeEntitySnapshotCommit Entity(AetheriaRuntimeRunCheckpointCommit run, int index)
    {
        return run.Zones
            .SelectMany(zone => zone.Entities)
            .Single(entity => entity.EntityIndex == index);
    }

    private static AetheriaRuntimeEntityStatGridCommit Grid(string name, double value)
    {
        return new AetheriaRuntimeEntityStatGridCommit
        {
            Name = name,
            Width = 1,
            Height = 1,
            Values = [value]
        };
    }

    private static double Stat(AetheriaRuntimeEntitySnapshotCommit entity, string name)
    {
        return entity.StatGrids
            .First(grid => string.Equals(grid.Name, name, StringComparison.OrdinalIgnoreCase))
            .Values
            .First();
    }

    private static string Snapshot(AetheriaRuntimeRunCheckpointCommit run)
    {
        return string.Join(
            "|",
            run.Zones
                .SelectMany(zone => zone.Entities)
                .OrderBy(entity => entity.EntityIndex)
                .Select(entity =>
                    $"{entity.EntityIndex}:{Stat(entity, "hull"):0.000}:{Stat(entity, "shield"):0.000}:{Stat(entity, "heat"):0.000}:{Stat(entity, "cognitive-load"):0.000}:{entity.Contacts.Count}:{entity.WeaponStates.Count}"));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}. Expected {expected}; actual {actual}.");
    }
}
