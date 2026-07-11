using Aetheria.State.Daemon;
using GameCult.Aetheria.State.Verse;

var checks = new AetheriaDaemonYmirSmokeChecks();
checks.Run();
Console.WriteLine("Daemon Ymir projectile smoke passed.");

internal sealed class AetheriaDaemonYmirSmokeChecks
{
    public void Run()
    {
        YmirMovesProjectileAndReportsStableContact();
        DaemonSimulationAppliesYmirHit();
        MissingPhysicsOwnerCannotAdvanceProjectiles();
    }

    private static void YmirMovesProjectileAndReportsStableContact()
    {
        var (run, zone, target) = Scenario();
        var step = new AetheriaYmirProjectilePhysics().Step(zone, zone.Entities, 0.1);

        RequireEqual("ymir.core", new AetheriaYmirProjectilePhysics().AuthorityId, "adapter must identify its owner");
        RequireEqual(0, step.Projectiles.Count, "contacted projectile must not survive");
        RequireEqual(1, step.Hits.Count, "Ymir must report one projectile contact");
        RequireEqual(target.EntityIndex, step.Hits[0].TargetEntityIndex, "contact must resolve the daemon entity");
        RequireEqual("aetheria.projectile.smoke-projectile", step.Hits[0].ProjectileBodyId, "projectile body id must be stable");
        RequireEqual("aetheria.daemon.entity.2", step.Hits[0].TargetBodyId, "entity body id must be stable");
    }

    private static void DaemonSimulationAppliesYmirHit()
    {
        var (run, _, target) = Scenario();

        AetheriaRuntimeDaemonSimulation.Step(
            run,
            new AetheriaRuntimeDaemonIntentState(),
            0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            new AetheriaYmirProjectilePhysics());

        RequireEqual(88.0, Stat(target, "hull"), "Aetheria must interpret the Ymir contact as damage");
        RequireEqual(0, run.Zones[0].Projectiles.Count, "spent projectile must leave daemon state");
    }

    private static void MissingPhysicsOwnerCannotAdvanceProjectiles()
    {
        var (run, _, _) = Scenario();
        try
        {
            AetheriaRuntimeDaemonSimulation.Step(
                run,
                new AetheriaRuntimeDaemonIntentState(),
                0.1,
                AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
                AetheriaRuntimeProjectilePhysicsUnavailable.Instance);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("daemon advanced a projectile without an authoritative physics owner");
    }

    private static (AetheriaRuntimeRunCheckpointCommit Run, AetheriaRuntimeZoneSnapshotCommit Zone, AetheriaRuntimeEntitySnapshotCommit Target) Scenario()
    {
        var source = Entity(1, 0, "player");
        var target = Entity(2, 30, "raider");
        var zone = new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = 0,
            Entities = [source, target],
            Projectiles =
            [
                new AetheriaRuntimeProjectileCommit
                {
                    ProjectileId = "smoke-projectile",
                    SourceEntityIndex = 1,
                    TargetEntityIndex = 2,
                    PositionX = 0,
                    PositionZ = 0,
                    VelocityX = 100,
                    VelocityY = 0,
                    Radius = 1,
                    Damage = 12,
                    LifetimeSeconds = 5,
                    Active = true
                }
            ]
        };
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "ymir-projectile-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.1",
            Zones = [zone]
        };
        return (run, zone, target);
    }

    private static AetheriaRuntimeEntitySnapshotCommit Entity(int index, double x, string faction) => new()
    {
        EntityIndex = index,
        Kind = "ship",
        FactionKey = faction,
        PositionX = x,
        PositionZ = 0,
        TargetEntityIndex = -1,
        IsActive = true,
        StatGrids = [Grid("hull", 100), Grid("shield", 0), Grid("heat", 0)]
    };

    private static AetheriaRuntimeEntityStatGridCommit Grid(string name, double value) => new()
    {
        Name = name,
        Width = 1,
        Height = 1,
        Values = [value]
    };

    private static double Stat(AetheriaRuntimeEntitySnapshotCommit entity, string name) =>
        entity.StatGrids.Single(grid => string.Equals(grid.Name, name, StringComparison.OrdinalIgnoreCase)).Values[0];

    private static void RequireEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}. Expected {expected}; actual {actual}.");
    }
}
