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
        ThermalCellsUseFossilConductionAndRadiation();
        MultipleActorsUseTheSameMovementLever();
        AgentClaimsAndCompletesExploreTaskThroughCommands();
        SchedulerAssignsHighestPriorityCompatibleTask();
        AgentCompletesAttackTaskThroughTargetFireAndYmir();
    }

    private static void AgentCompletesAttackTaskThroughTargetFireAndYmir()
    {
        var agent = Entity(0, 0, "player");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Attack];
        agent.WeaponGroups = [new[] { 0 }];
        var target = Entity(1, 105, "raider");
        target.Kind = "station";
        target.StatGrids = [Grid("hull", 24), Grid("shield", 0), Grid("heat", 0)];
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-attack-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [agent, target] }]
        };
        var issue = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.IssueAgentTask,
            "commander-smoke",
            "starbridge-smoke",
            0,
            "");
        issue.CommandId = "issue-attack";
        issue.AgentTask = new AetheriaRuntimeAgentTaskCommand
        {
            TaskId = "attack-raider",
            CorporationKey = "player",
            TaskType = AetheriaRuntimeAgentTaskTypes.Attack,
            Priority = 100,
            ZoneIndex = 0,
            TargetEntityIndex = 1,
            CompletionRadius = 25,
            WeaponGroup = 0
        };
        var sawTargetCommand = false;
        var sawFireCommand = false;
        for (var frame = 0; frame < 60; frame++)
        {
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), "aetheria-agent-attack-smoke.cc"),
                run,
                new AetheriaRuntimeDaemonTickOptions
                {
                    FrameId = frame,
                    FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frame * 0.1,
                    ObservedCommands = frame == 0 ? [issue] : Array.Empty<AetheriaRuntimeDaemonCommandDocument>(),
                    ProjectilePhysics = new AetheriaYmirProjectilePhysics(),
                    BuildPublications = false
                });
            sawTargetCommand |= tick.OperationResult.AppliedCommandIds.Any(id => id.EndsWith(":target", StringComparison.Ordinal));
            sawFireCommand |= tick.OperationResult.AppliedCommandIds.Any(id => id.EndsWith(":fire", StringComparison.Ordinal));
            if (string.Equals(run.AgentTasks.Single().Status, AetheriaRuntimeAgentTaskStatuses.Completed, StringComparison.Ordinal))
                break;
        }

        Require(sawTargetCommand, "attack agent must target through the shared target command");
        Require(sawFireCommand, "attack agent must fire through the shared weapon-group command");
        Require(!target.IsActive,
            $"attack task must end through daemon damage after Ymir projectile contacts; hull={Stat(target, "hull"):0.###} " +
            $"agent={agent.PositionX:0.###},{agent.PositionZ:0.###} target={target.PositionX:0.###},{target.PositionZ:0.###} " +
            $"projectiles={run.Zones[0].Projectiles.Count} task={run.AgentTasks.Single().Status}");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Completed, run.AgentTasks.Single().Status,
            "attack task must complete when its target dies");
    }

    private static void SchedulerAssignsHighestPriorityCompatibleTask()
    {
        var agent = Entity(0, 0, "workers");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var low = AgentTask("low", 1);
        var high = AgentTask("high", 99);
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-priority-smoke",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [agent] }],
            AgentTasks = [low, high]
        };

        AetheriaRuntimeAgentScheduler.AssignAndPlan(run, 4);

        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Assigned, high.Status,
            "highest priority compatible task must claim the available agent");
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Queued, low.Status,
            "lower priority task must remain queued when no controller remains");
        RequireEqual("high", agent.AssignedAgentTaskId, "agent assignment must point at the selected task");
    }

    private static AetheriaRuntimeAgentTaskCommit AgentTask(string id, int priority) => new()
    {
        TaskId = id,
        CorporationKey = "workers",
        TaskType = AetheriaRuntimeAgentTaskTypes.Explore,
        Priority = priority,
        ZoneIndex = 0,
        TargetPositionX = 100,
        CompletionRadius = 5
    };

    private static void AgentClaimsAndCompletesExploreTaskThroughCommands()
    {
        var agent = Entity(0, 0, "workers");
        agent.AgentTaskCapabilities = [AetheriaRuntimeAgentTaskTypes.Explore];
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "agent-task-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [agent] }]
        };
        var issue = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.IssueAgentTask,
            "commander-smoke",
            "starbridge-smoke",
            0,
            "");
        issue.CommandId = "issue-explore";
        issue.AgentTask = new AetheriaRuntimeAgentTaskCommand
        {
            TaskId = "explore-east",
            CorporationKey = "workers",
            TaskType = AetheriaRuntimeAgentTaskTypes.Explore,
            Priority = 50,
            ZoneIndex = 0,
            TargetPositionX = 50,
            TargetPositionZ = 0,
            CompletionRadius = 5
        };
        var completed = false;
        for (var frame = 0; frame <= 20; frame++)
        {
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                Path.Combine(Path.GetTempPath(), "aetheria-agent-task-smoke.cc"),
                run,
                new AetheriaRuntimeDaemonTickOptions
                {
                    FrameId = frame,
                    FixedDeltaSeconds = 0.1,
                    SimulationTimeSeconds = frame * 0.1,
                    ObservedCommands = frame == 0 ? [issue] : Array.Empty<AetheriaRuntimeDaemonCommandDocument>(),
                    ProjectilePhysics = AetheriaRuntimeProjectilePhysicsUnavailable.Instance,
                    BuildPublications = false
                });
            if (frame == 0)
            {
                Require(tick.OperationResult.AppliedCommandIds.Contains("issue-explore"),
                    "commander task command must enter the normal tick reducer");
                Require(tick.OperationResult.AppliedCommandIds.Any(id => id.Contains(":explore-east:0:move", StringComparison.Ordinal)),
                    "agent movement must return through the same command receipts");
            }
            completed = string.Equals(run.AgentTasks.Single().Status, AetheriaRuntimeAgentTaskStatuses.Completed, StringComparison.Ordinal);
            if (completed)
                break;
        }

        Require(completed, "agent must complete an explore task without direct position mutation");
        Require(string.IsNullOrWhiteSpace(agent.AssignedAgentTaskId), "completed task must release the agent");
        Require(agent.PositionX >= 45, "agent must reach the task through repeated movement commands");

        var frameDocument = new AetheriaRuntimeDaemonFrameDocument { FrameId = 21, Run = run };
        var commander = AetheriaRuntimeDaemonGameSurfaceBuilder.BuildCommander(
            frameDocument,
            new AetheriaRuntimeDaemonHealthDocument(),
            AetheriaRuntimeDaemonCommandBoundaryDocument.Create("agent-task-smoke"));
        var taskNode = Flatten(commander.Surface.Root)
            .Single(component => string.Equals(component.Id, "aetheria.starbridge.commander.tasks.explore_east", StringComparison.Ordinal));
        RequireEqual(AetheriaRuntimeAgentTaskStatuses.Completed, taskNode.Props["status"],
            "commander Eve surface must publish authoritative task status");
        Require(commander.Commands.Any(command => string.Equals(command.Command, "aetheria.daemon.issue_agent_task", StringComparison.Ordinal)),
            "commander Eve surface must advertise task issue command");
    }

    private static void MultipleActorsUseTheSameMovementLever()
    {
        var player = Entity(0, 0, "player");
        var agent = Entity(1, 0, "worker");
        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "shared-lever-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.0",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [player, agent] }]
        };
        var commands = new[]
        {
            MovementCommand("player-move", "zone.0.entity.0", 1, 0),
            MovementCommand("agent-move", "zone.0.entity.1", 0, 1)
        };
        var operation = AetheriaRuntimeDaemonOperations.Execute(run, commands);

        RequireEqual(2, operation.Intents.Movements.Count, "movement intent must retain one lever position per actor");
        AetheriaRuntimeDaemonSimulation.Step(
            run,
            operation.Intents,
            0.1,
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault,
            AetheriaRuntimeProjectilePhysicsUnavailable.Instance);
        Require(player.VelocityX > 0 && Math.Abs(player.VelocityY) < 0.001,
            "player command must drive its actor through the shared movement lever");
        Require(agent.VelocityY > 0 && Math.Abs(agent.VelocityX) < 0.001,
            "agent command must drive its actor through the shared movement lever");
    }

    private static AetheriaRuntimeDaemonCommandDocument MovementCommand(
        string commandId,
        string actor,
        double x,
        double y)
    {
        var command = AetheriaRuntimeDaemonCommandDocument.Create(
            AetheriaRuntimeDaemonCommandKinds.SetMoveVector,
            "shared-control-smoke",
            "shared-control-session",
            0,
            actor);
        command.CommandId = commandId;
        command.DirectionX = x;
        command.DirectionY = y;
        command.ScalarValue = 1;
        return command;
    }

    private static void ThermalCellsUseFossilConductionAndRadiation()
    {
        var entity = Entity(7, 0, "player");
        entity.StatGrids =
        [
            Grid(AetheriaRuntimeThermalSimulation.TemperatureGrid, 2, 1, 300, 280),
            Grid(AetheriaRuntimeThermalSimulation.ThermalMassGrid, 2, 1, 1, 1),
            Grid(AetheriaRuntimeThermalSimulation.ConductivityGrid, 2, 1, 1, 1)
        ];

        AetheriaRuntimeThermalSimulation.AddHeat(entity, 20);
        RequireNear(310, GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 0), 0.000001,
            "heat energy must be divided across cells and thermal mass");
        RequireNear(290, GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 1), 0.000001,
            "heat energy must be divided across cells and thermal mass");

        AetheriaRuntimeThermalSimulation.Step(entity, 0.1);
        var expectedHot = (310 / 0.01 + 290) / 101;
        expectedHot -= Math.Pow(expectedHot, 3) * 0.00000001 * 0.1;
        var expectedCool = (290 / 0.01 + 310) / 101;
        expectedCool -= Math.Pow(expectedCool, 3) * 0.00000001 * 0.1;
        RequireNear(expectedHot, GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 0), 0.000001,
            "hot cell must follow fossil conduction and radiation");
        RequireNear(expectedCool, GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 1), 0.000001,
            "cool cell must follow fossil conduction and radiation");
        RequireNear((expectedHot + expectedCool) / 2, Stat(entity, "heat"), 0.000001,
            "legacy heat scalar must be derived from cell temperature");

        var run = new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = "thermal-projection-smoke",
            CurrentZoneIndex = 0,
            CurrentEntityKey = "zone.0.entity.7",
            Zones = [new AetheriaRuntimeZoneSnapshotCommit { ZoneIndex = 0, Entities = [entity] }]
        };
        var document = AetheriaRuntimeGameDocuments.CurrentEntity(new AetheriaRuntimeDaemonFrameDocument { Run = run });
        RequireNear(Stat(entity, "heat"), document.Hud.MeanTemperature, 0.000001,
            "Eve current-entity state must publish mean temperature");
        RequireNear(GridValue(entity, AetheriaRuntimeThermalSimulation.TemperatureGrid, 0), document.Hud.MaximumTemperature, 0.000001,
            "Eve current-entity state must publish maximum temperature");
        Require(document.Hud.ThermalVisibility > 0, "Eve current-entity state must publish thermal visibility");
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

    private static AetheriaRuntimeEntityStatGridCommit Grid(string name, int width, int height, params double[] values) => new()
    {
        Name = name,
        Width = width,
        Height = height,
        Values = values
    };

    private static double GridValue(AetheriaRuntimeEntitySnapshotCommit entity, string name, int index) =>
        entity.StatGrids.Single(grid => string.Equals(grid.Name, name, StringComparison.OrdinalIgnoreCase)).Values[index];

    private static IEnumerable<AetheriaRuntimeSurfaceComponent> Flatten(AetheriaRuntimeSurfaceComponent root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var descendant in Flatten(child))
            yield return descendant;
    }

    private static double Stat(AetheriaRuntimeEntitySnapshotCommit entity, string name) =>
        entity.StatGrids.Single(grid => string.Equals(grid.Name, name, StringComparison.OrdinalIgnoreCase)).Values[0];

    private static void RequireEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}. Expected {expected}; actual {actual}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireNear(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message}. Expected {expected}; actual {actual}.");
    }
}
