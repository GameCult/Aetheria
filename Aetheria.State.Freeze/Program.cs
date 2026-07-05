using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.State;
using GameCult.Geometry;
using GameCult.Aetheria.State.Verse;
using MessagePack;

var options = FreezeOptions.Parse(args);
Directory.CreateDirectory(options.OutputDirectory);
Directory.CreateDirectory(Path.GetDirectoryName(options.StatePath) ?? ".");

await ExportFixtureSetAsync(
        options,
        "baseline",
        options.StatePath,
        options.OutputDirectory,
        Array.Empty<AetheriaRuntimeDaemonCommandDocument>())
    .ConfigureAwait(false);

if (options.IncludeCommandBatches)
{
    var commandStateDirectory = Path.Combine(options.OutputDirectory, ".state");
    await ExportFixtureSetAsync(
            options,
            "command-batch-pilot",
            Path.Combine(commandStateDirectory, "command-batch-pilot.cc"),
            Path.Combine(options.OutputDirectory, "command-batch-pilot"),
            BuildPilotCommands())
        .ConfigureAwait(false);

    await ExportFixtureSetAsync(
            options,
            "command-batch-interaction",
            Path.Combine(commandStateDirectory, "command-batch-interaction.cc"),
            Path.Combine(options.OutputDirectory, "command-batch-interaction"),
            BuildInteractionCommands())
        .ConfigureAwait(false);

    await ExportFixtureSetAsync(
            options,
            "command-batch-refit",
            Path.Combine(commandStateDirectory, "command-batch-refit.cc"),
            Path.Combine(options.OutputDirectory, "command-batch-refit"),
            BuildRefitCommands())
        .ConfigureAwait(false);
}

ExportAuthorityDecisionFixtures(Path.Combine(options.OutputDirectory, "authority-decisions"));
ExportYmirQueryFixtures(Path.Combine(options.OutputDirectory, "ymir-queries"));

Console.WriteLine($"Wrote Aetheria freeze fixtures to {options.OutputDirectory}");

static async Task ExportFixtureSetAsync(
    FreezeOptions options,
    string scenario,
    string statePath,
    string outputDirectory,
    IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> commands)
{
    Directory.CreateDirectory(outputDirectory);
    Directory.CreateDirectory(Path.GetDirectoryName(statePath) ?? ".");
    if (options.CleanState)
        DeleteStateFamily(statePath);

    if (commands.Count > 0)
        await SeedCommandsAsync(statePath, commands).ConfigureAwait(false);

    if (options.RunDaemon)
        await RunDaemonOnceAsync(options, statePath, scenario).ConfigureAwait(false);

    var manifest = new FreezeManifest
    {
        Scenario = scenario,
        GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
        Root = options.Root,
        StatePath = statePath,
        OutputDirectory = outputDirectory,
        RanDaemon = options.RunDaemon,
        SeededCommandCount = commands.Count
    };

    if (commands.Count > 0)
        WriteArtifact(outputDirectory, manifest, "commands.seeded", AetheriaRuntimeDaemonSchemas.Command + "[]", commands);

    await using var node = await AetheriaStateNode
        .OpenAsync(statePath, runtimeId: "aetheria-freeze", startServer: false)
        .ConfigureAwait(false);

    var frame = await node.MutableDocument<AetheriaRuntimeDaemonFrameDocument>(
            AetheriaRuntimeVerseRecordKeys.DaemonFrameLatest)
        .ReadAsync()
        .ConfigureAwait(false);
    if (frame == null)
        throw new InvalidOperationException("Freeze export cannot continue because the daemon latest frame is missing.");
    WriteArtifact(outputDirectory, manifest, "daemon-frame.latest", AetheriaRuntimeDaemonSchemas.Frame, frame);

    if (await TryReadDocumentAsync<AetheriaRuntimeDaemonHealthDocument>(node, AetheriaRuntimeVerseRecordKeys.DaemonHealth).ConfigureAwait(false) is { } health)
        WriteArtifact(outputDirectory, manifest, "daemon-health.latest", AetheriaRuntimeDaemonSchemas.Health, health);

    if (await TryReadDocumentAsync<AetheriaRuntimeDaemonProviderAdvertisementDocument>(node, AetheriaRuntimeVerseRecordKeys.DaemonProviderAdvertisement).ConfigureAwait(false) is { } provider)
        WriteArtifact(outputDirectory, manifest, "daemon-provider.latest", AetheriaRuntimeDaemonSchemas.ProviderAdvertisement, provider);

    if (await TryReadDocumentAsync<AetheriaRuntimeDaemonCommandBoundaryDocument>(node, AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary).ConfigureAwait(false) is { } commandBoundary)
        WriteArtifact(outputDirectory, manifest, "daemon-command-boundary.latest", AetheriaRuntimeDaemonSchemas.CommandBoundary, commandBoundary);

    if (await TryReadDocumentAsync<AetheriaRuntimeVerseAuthorityPolicyDocument>(node, AetheriaRuntimeVerseRecordKeys.VerseAuthorityPolicy).ConfigureAwait(false) is { } authorityPolicy)
        WriteArtifact(outputDirectory, manifest, "verse-authority-policy.latest", AetheriaRuntimeVerseAuthoritySchemas.Policy, authorityPolicy);

    if (await TryReadDocumentAsync<AetheriaRuntimeStarbridgeSessionSummaryDocument>(node, AetheriaRuntimeVerseRecordKeys.StarbridgeSessionSummary).ConfigureAwait(false) is { } starbridgeSummary)
        WriteArtifact(outputDirectory, manifest, "starbridge-session-summary.latest", AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary, starbridgeSummary);

    if (await TryReadDocumentAsync<AetheriaRuntimeDaemonSoaViewDocument>(node, AetheriaRuntimeVerseRecordKeys.DaemonSoaViewLatest).ConfigureAwait(false) is { } soaView)
        WriteArtifact(outputDirectory, manifest, "daemon-soa-view.latest", AetheriaRuntimeDaemonSchemas.SoaView, soaView);

    var viewport = new AetheriaRuntimeViewportBounds
    {
        MinX = options.ViewportMinX,
        MinY = options.ViewportMinY,
        MaxX = options.ViewportMaxX,
        MaxY = options.ViewportMaxY
    };
    WriteArtifact(outputDirectory, manifest, "document.game-viewport", AetheriaRuntimeDaemonSchemas.GameViewport, AetheriaRuntimeGameDocuments.Viewport(frame, viewport));
    WriteArtifact(outputDirectory, manifest, "document.objects-viewport", AetheriaRuntimeDaemonSchemas.ObjectsViewport, AetheriaRuntimeGameDocuments.ObjectsViewport(frame, viewport));
    WriteArtifact(outputDirectory, manifest, "document.gravity-viewport", AetheriaRuntimeDaemonSchemas.GravityViewport, AetheriaRuntimeGameDocuments.GravityViewport(frame, viewport));
    WriteArtifact(outputDirectory, manifest, "document.current-zone", AetheriaRuntimeDaemonSchemas.CurrentZone, AetheriaRuntimeGameDocuments.CurrentZone(frame));
    WriteArtifact(outputDirectory, manifest, "document.current-entity", AetheriaRuntimeDaemonSchemas.CurrentEntity, AetheriaRuntimeGameDocuments.CurrentEntity(frame));
    WriteArtifact(outputDirectory, manifest, "document.sector-map", AetheriaRuntimeDaemonSchemas.SectorMap, AetheriaRuntimeGameDocuments.SectorMap(frame));
    WriteArtifact(outputDirectory, manifest, "document.zone-render", AetheriaRuntimeDaemonSchemas.ZoneRender, AetheriaRuntimeGameDocuments.ZoneRender(frame));

    var selectedEntityIndex = ResolveSelectedEntityIndex(frame);
    WriteArtifact(outputDirectory, manifest, "document.selected-object", AetheriaRuntimeDaemonSchemas.SelectedObject, AetheriaRuntimeGameDocuments.SelectedObject(frame, selectedEntityIndex));
    WriteArtifact(outputDirectory, manifest, "document.inventory", AetheriaRuntimeDaemonSchemas.Inventory, AetheriaRuntimeGameDocuments.Inventory(frame, selectedEntityIndex));

    WriteJson(Path.Combine(outputDirectory, "manifest.json"), manifest);
}

static void ExportAuthorityDecisionFixtures(string outputDirectory)
{
    Directory.CreateDirectory(outputDirectory);
    var fixtures = BuildAuthorityDecisionFixtures();
    var manifest = new FreezeManifest
    {
        Scenario = "authority-decisions",
        GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
        OutputDirectory = outputDirectory,
        RanDaemon = false,
        SeededCommandCount = fixtures.Cases.Length
    };
    WriteArtifact(outputDirectory, manifest, "authority-decisions", "gamecult.aetheria.freeze.authority_decisions.v1", fixtures);
    WriteJson(Path.Combine(outputDirectory, "manifest.json"), manifest);
}

static Task<T?> TryReadDocumentAsync<T>(AetheriaStateNode node, GameCult.Caching.CultRecordKey recordKey)
    where T : class
{
    return TryReadAsync(() => node.MutableDocument<T>(recordKey).ReadAsync());
}

static async Task<T?> TryReadAsync<T>(Func<Task<T?>> read)
    where T : class
{
    try
    {
        return await read().ConfigureAwait(false);
    }
    catch (KeyNotFoundException)
    {
        return null;
    }
}

static void ExportYmirQueryFixtures(string outputDirectory)
{
    Directory.CreateDirectory(outputDirectory);
    var fixtures = BuildYmirQueryFixtures();
    var manifest = new FreezeManifest
    {
        Scenario = "ymir-queries",
        GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
        OutputDirectory = outputDirectory,
        RanDaemon = false,
        SeededCommandCount = fixtures.Cases.Length
    };
    WriteArtifact(outputDirectory, manifest, "ymir-queries", "gamecult.aetheria.freeze.ymir_queries.v1", fixtures);
    WriteJson(Path.Combine(outputDirectory, "manifest.json"), manifest);
}

static async Task SeedCommandsAsync(string statePath, IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> commands)
{
    await using var node = await AetheriaStateNode
        .OpenAsync(statePath, "aetheria-freeze-command-seeder", enableDurableShardLogs: false)
        .ConfigureAwait(false);
    foreach (var command in commands)
        await node.SubmitDaemonCommandAsync(command).ConfigureAwait(false);
    await node.FlushAsync().ConfigureAwait(false);
}

static async Task RunDaemonOnceAsync(FreezeOptions options, string statePath, string scenario)
{
    var daemonProject = Path.Combine(options.Root, "Aetheria.State.Daemon", "Aetheria.State.Daemon.csproj");
    if (!File.Exists(daemonProject))
        throw new FileNotFoundException("Could not find daemon project.", daemonProject);

    var daemonArgs = string.Join(
        " ",
        new[]
        {
            "run",
            "--project",
            Quote(daemonProject),
            "--",
            "--once",
            "--root",
            Quote(options.Root),
            "--state",
            Quote(statePath),
            "--daemon-id",
            $"aetheria-freeze-{scenario}",
            "--session-id",
            scenario,
            "--client-cultmesh-port",
            "0"
        });

    var start = new ProcessStartInfo("dotnet", daemonArgs)
    {
        WorkingDirectory = options.Root,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start daemon process.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync().ConfigureAwait(false);

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"Aetheria daemon fixture run failed with exit code {process.ExitCode}.\nSTDOUT:\n{await stdout.ConfigureAwait(false)}\nSTDERR:\n{await stderr.ConfigureAwait(false)}");
    }
}

static void DeleteStateFamily(string statePath)
{
    var directory = Path.GetDirectoryName(statePath);
    var fileName = Path.GetFileName(statePath);
    if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(directory))
        return;

    foreach (var path in Directory.EnumerateFileSystemEntries(directory, fileName + "*"))
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        else
            File.Delete(path);
    }
}

static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> BuildPilotCommands()
{
    const string actor = "global:aetheria.run_state.local-rts.zone.0.entity.1.v1";
    const string target = "global:aetheria.run_state.local-rts.zone.0.entity.4.v1";
    return new[]
    {
        Command("pilot-001-set-target", AetheriaRuntimeDaemonCommandKinds.SetTarget, actor, command =>
        {
            command.TargetEntityKey = target;
        }),
        Command("pilot-002-move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector, actor, command =>
        {
            command.DirectionX = 1;
            command.DirectionY = 0.25;
            command.ScalarValue = 1;
        }),
        Command("pilot-003-look", AetheriaRuntimeDaemonCommandKinds.SetLookDirection, actor, command =>
        {
            command.DirectionX = 0.25;
            command.DirectionY = 1;
        }),
        Command("pilot-004-tractor", AetheriaRuntimeDaemonCommandKinds.SetTractorPower, actor, command =>
        {
            command.ScalarValue = 0.5;
        }),
        Command("pilot-005-sensor-ping", AetheriaRuntimeDaemonCommandKinds.SensorPing, actor),
        Command("pilot-006-toggle-shield", AetheriaRuntimeDaemonCommandKinds.ToggleShieldEnabled, actor),
        Command("pilot-007-fire-weapon-group", AetheriaRuntimeDaemonCommandKinds.FireWeaponGroup, actor, command =>
        {
            command.WeaponGroup = 0;
        })
    };
}

static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> BuildInteractionCommands()
{
    const string actor = "global:aetheria.run_state.local-rts.zone.0.entity.1.v1";
    const string station = "global:aetheria.run_state.local-rts.zone.0.entity.0.v1";
    return new[]
    {
        Command("interaction-001-dock-nearest", AetheriaRuntimeDaemonCommandKinds.DockNearest, actor),
        Command("interaction-002-dock", AetheriaRuntimeDaemonCommandKinds.Dock, actor, command =>
        {
            command.TargetEntityKey = station;
        }),
        Command("interaction-003-undock", AetheriaRuntimeDaemonCommandKinds.Undock, actor),
        Command("interaction-004-interact", AetheriaRuntimeDaemonCommandKinds.Interact, actor),
        Command("interaction-005-tow", AetheriaRuntimeDaemonCommandKinds.TowToStation, actor, command =>
        {
            command.TargetEntityKey = station;
        })
    };
}

static IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> BuildRefitCommands()
{
    const string actor = "global:aetheria.run_state.local-rts.zone.0.entity.1.v1";
    const string station = "global:aetheria.run_state.local-rts.zone.0.entity.0.v1";
    return new[]
    {
        Command("refit-001-rename", AetheriaRuntimeDaemonCommandKinds.SetEntityName, actor, command =>
        {
            command.TargetEntityKey = actor;
            command.TextValue = "Vanguard Freeze";
        }),
        Command("refit-002-transfer-cargo", AetheriaRuntimeDaemonCommandKinds.TransferCargoItem, actor, command =>
        {
            command.TargetEntityKey = actor;
            command.TextValue = "repair-parts";
            command.ScalarValue = 1;
            command.CargoTransfer.OriginEntityKey = station;
            command.CargoTransfer.OriginCargoIndex = 0;
            command.CargoTransfer.DestinationEntityKey = actor;
            command.CargoTransfer.DestinationCargoIndex = 0;
            command.CargoTransfer.SourceX = 0;
            command.CargoTransfer.SourceY = 0;
            command.CargoTransfer.DestinationX = 2;
            command.CargoTransfer.DestinationY = 0;
            command.CargoTransfer.HasDestinationPosition = true;
        }),
        Command("refit-003-trade-purchase", AetheriaRuntimeDaemonCommandKinds.TradePurchase, actor, command =>
        {
            command.TargetEntityKey = actor;
            command.EquipmentIndex = 0;
            command.PositionX = 1;
            command.PositionY = 0;
            command.ScalarValue = 25;
            command.TextValue = "reactor-fuel";
            command.TradePurchase.PurchaseKind = "cargo";
            command.TradePurchase.ItemKey = "reactor-fuel";
            command.TradePurchase.Quantity = 1;
            command.TradePurchase.UnitPrice = 25;
            command.TradePurchase.TotalPrice = 25;
            command.TradePurchase.StationEntityKey = station;
            command.TradePurchase.StationCargoIndex = 0;
            command.TradePurchase.TargetEntityKey = actor;
            command.TradePurchase.TargetCargoIndex = 0;
            command.TradePurchase.SourceX = 1;
            command.TradePurchase.SourceY = 0;
        }),
        Command("refit-004-equip-from-cargo", AetheriaRuntimeDaemonCommandKinds.EquipItem, actor, command =>
        {
            command.TargetEntityKey = actor;
            command.PositionX = 1;
            command.PositionY = 0;
            command.TextValue = "field-rations";
            command.EquipmentTransfer.SourceKind = "cargo";
            command.EquipmentTransfer.OriginEntityKey = actor;
            command.EquipmentTransfer.OriginIndex = 0;
            command.EquipmentTransfer.DestinationEntityKey = actor;
            command.EquipmentTransfer.SourceX = 1;
            command.EquipmentTransfer.SourceY = 0;
            command.EquipmentTransfer.DestinationX = 1;
            command.EquipmentTransfer.DestinationY = 0;
            command.EquipmentTransfer.HasDestinationPosition = true;
        }),
        Command("refit-005-store-equipped", AetheriaRuntimeDaemonCommandKinds.StoreItem, actor, command =>
        {
            command.TargetEntityKey = actor;
            command.TextValue = "sensor-array";
            command.StoreItem.OriginEntityKey = actor;
            command.StoreItem.SourceEquipmentIndex = 0;
            command.StoreItem.DestinationEntityKey = actor;
            command.StoreItem.DestinationCargoIndex = 0;
            command.StoreItem.DestinationX = 3;
            command.StoreItem.DestinationY = 0;
            command.StoreItem.HasDestinationPosition = true;
        }),
        Command("refit-006-set-docked-current-ship", AetheriaRuntimeDaemonCommandKinds.SetDockedCurrentShip, actor, command =>
        {
            command.TargetEntityKey = actor;
        }),
        Command("refit-007-pick-up-loot", AetheriaRuntimeDaemonCommandKinds.PickUpLoot, actor, command =>
        {
            command.TargetEntityKey = actor;
            command.TextValue = "loose-salvage";
            command.ScalarValue = 2;
            command.PositionX = -40;
            command.PositionY = 0;
            command.PositionZ = -35;
            command.LootPickup.ItemKey = "loose-salvage";
            command.LootPickup.Quantity = 2;
            command.LootPickup.PositionX = -40;
            command.LootPickup.PositionY = 0;
            command.LootPickup.PositionZ = -35;
        }),
        Command("refit-008-restore-loadout", AetheriaRuntimeDaemonCommandKinds.RestoreLoadout, actor, command =>
        {
            command.TargetEntityKey = actor;
            command.LoadoutRestore.DockedEntityKey = actor;
            command.LoadoutRestore.TemplateName = "freeze-template";
            command.LoadoutRestore.Price = 0;
        })
    };
}

static AuthorityDecisionFixtureSet BuildAuthorityDecisionFixtures()
{
    const string verseId = "aetheria.freeze";
    const string host = "aetheria-daemon";
    const string unity = "raven-unity";
    const string rts = "starfire-rts";
    const string actor = "global:aetheria.run_state.local-rts.zone.0.entity.1.v1";
    const string raider = "global:aetheria.run_state.local-rts.zone.0.entity.4.v1";
    var validLease = new AetheriaRuntimeAuthorityLeaseDocument
    {
        LeaseId = "lease-rts-vanguard-movement",
        VerseId = verseId,
        RuntimeId = rts,
        SubjectPrefix = actor,
        ClaimKinds = new[] { AetheriaRuntimeClaimKinds.Movement },
        ValidFromUtc = "2026-01-01T00:00:00.0000000Z",
        ExpiresAtUtc = "2100-01-01T00:00:00.0000000Z",
        Scope = "nearby-interest"
    };

    var cases = new[]
    {
        AuthorityCase(
            "trusted-coop-allows-rts-movement",
            AetheriaRuntimeVerseAuthorityPolicyDocument.TrustedCoop(verseId, host),
            Array.Empty<AetheriaRuntimeAuthorityLeaseDocument>(),
            AuthorityCommand("auth-001-rts-move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector, actor, rts),
            host),
        AuthorityCase(
            "host-authoritative-allows-host",
            HostPolicy(verseId, host),
            Array.Empty<AetheriaRuntimeAuthorityLeaseDocument>(),
            AuthorityCommand("auth-002-host-move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector, actor, host),
            host),
        AuthorityCase(
            "host-authoritative-rejects-rts",
            HostPolicy(verseId, host),
            Array.Empty<AetheriaRuntimeAuthorityLeaseDocument>(),
            AuthorityCommand("auth-003-rts-move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector, actor, rts),
            host),
        AuthorityCase(
            "delegated-runtime-allows-rts-targeting",
            DelegatedPolicy(verseId, host, actor, AetheriaRuntimeClaimKinds.Targeting, rts),
            Array.Empty<AetheriaRuntimeAuthorityLeaseDocument>(),
            AuthorityCommand("auth-004-rts-target", AetheriaRuntimeDaemonCommandKinds.SetTarget, actor, rts, command => command.TargetEntityKey = raider),
            host),
        AuthorityCase(
            "delegated-runtime-rejects-unity-targeting",
            DelegatedPolicy(verseId, host, actor, AetheriaRuntimeClaimKinds.Targeting, rts),
            Array.Empty<AetheriaRuntimeAuthorityLeaseDocument>(),
            AuthorityCommand("auth-005-unity-target", AetheriaRuntimeDaemonCommandKinds.SetTarget, actor, unity, command => command.TargetEntityKey = raider),
            host),
        AuthorityCase(
            "interest-lease-allows-rts-movement",
            LeasePolicy(verseId, host, actor, AetheriaRuntimeClaimKinds.Movement),
            new[] { validLease },
            AuthorityCommand("auth-006-rts-lease-move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector, actor, rts),
            host),
        AuthorityCase(
            "interest-lease-rejects-rts-without-lease",
            LeasePolicy(verseId, host, actor, AetheriaRuntimeClaimKinds.Movement),
            Array.Empty<AetheriaRuntimeAuthorityLeaseDocument>(),
            AuthorityCommand("auth-007-rts-no-lease-move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector, actor, rts),
            host),
        AuthorityCase(
            "owning-runtime-is-not-implemented",
            OwningPolicy(verseId, host, actor),
            Array.Empty<AetheriaRuntimeAuthorityLeaseDocument>(),
            AuthorityCommand("auth-008-owning-mode-move", AetheriaRuntimeDaemonCommandKinds.SetMoveVector, actor, unity),
            host)
    };

    return new AuthorityDecisionFixtureSet
    {
        Schema = "gamecult.aetheria.freeze.authority_decisions.v1",
        GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
        Cases = cases
    };
}

static YmirQueryFixtureSet BuildYmirQueryFixtures()
{
    var stepBody = YmirBody("ship", 1, 2, 1);
    stepBody.velocity = YmirVec2Of(3, -4);
    var stepRequest = new YmirStepRequest
    {
        deltaTime = 0.5f,
        world = YmirWorldOf(stepBody)
    };

    var radialBody = YmirBody("ship", 5, 0, 1);
    var radialRequest = new YmirStepRequest
    {
        deltaTime = 1.0f,
        world = YmirWorldOf(radialBody)
    };
    radialRequest.world.fields = new[]
    {
        new YmirRadialField
        {
            id = "push",
            position = YmirVec2Of(0, 0),
            strength = 10,
            radius = 10
        }
    };

    var mover = YmirBody("mover", 0, 0, 1);
    mover.velocity = YmirVec2Of(2, 0);
    mover.mass = 1;
    var wall = YmirBody("wall", 1.5f, 0, 1);
    wall.isStatic = true;
    var contactRequest = new YmirStepRequest
    {
        deltaTime = 0.1f,
        world = YmirWorldOf(mover, wall)
    };

    var overlapSphereRequest = new YmirSphereOverlapRequest
    {
        center = YmirVec3Of(0, 0, 0),
        radius = 3,
        bodies = new[]
        {
            YmirSphereBody("far", 4, 0, 0, 2),
            YmirSphereBody("near", 1, 0, 0, 0.25f),
            YmirSphereBody("miss", 10, 0, 0, 1)
        }
    };

    var overlapCircleRequest = new YmirCircleOverlapRequest
    {
        center = YmirVec2Of(0, 0),
        radius = 3,
        world = YmirWorldOf(
            YmirBody("far", 4, 0, 2),
            YmirBody("near", 1, 0, 0.25f),
            YmirBody("miss", 10, 0, 1))
    };

    var castSphereRequest = new YmirSphereCastRequest
    {
        origin = YmirVec3Of(0, 0, 0),
        direction = YmirVec3Of(2, 0, 0),
        distance = 10,
        radius = 1,
        bodies = new[] { YmirSphereBody("target", 4, 0, 0, 1) }
    };

    var castCircleRequest = new YmirCircleCastRequest
    {
        origin = YmirVec2Of(0, 0),
        direction = YmirVec2Of(1, 0),
        distance = 10,
        radius = 1,
        world = YmirWorldOf(
            YmirBody("far", 8, 0, 1),
            YmirBody("near", 4, 0, 1),
            YmirBody("miss", 4, 4, 1))
    };

    var invalidCastCircleRequest = new YmirCircleCastRequest
    {
        origin = YmirVec2Of(0, 0),
        direction = YmirVec2Of(0, 0),
        distance = 1,
        radius = 1,
        world = YmirWorldOf(YmirBody("target", 0, 0, 1))
    };

    return new YmirQueryFixtureSet
    {
        Schema = "gamecult.aetheria.freeze.ymir_queries.v1",
        GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
        Cases = new[]
        {
            YmirStepCase("step-integrates-dynamic-body", "Dynamic bodies integrate velocity without mutating the input world.", stepRequest, YmirPhysicsQueries.Step(stepRequest)),
            YmirStepCase("step-applies-radial-field", "Radial fields apply linear falloff acceleration before integration.", radialRequest, YmirPhysicsQueries.Step(radialRequest)),
            YmirStepCase("step-reports-contact-and-separates", "Dynamic/static body overlap produces a contact and separates the dynamic body.", contactRequest, YmirPhysicsQueries.Step(contactRequest)),
            YmirSphereOverlapCase("overlap-sphere-sorts-surface-distance", "Sphere overlap hits sort by surface distance, then id.", overlapSphereRequest, YmirPhysicsQueries.OverlapSphere(overlapSphereRequest)),
            YmirCircleOverlapCase("overlap-circle-sorts-surface-distance", "Circle overlap hits sort by surface distance, then id.", overlapCircleRequest, YmirPhysicsQueries.OverlapCircle(overlapCircleRequest)),
            YmirSphereCastCase("cast-sphere-impact-point-normal", "Sphere casts report impact distance, point, and normal.", castSphereRequest, YmirPhysicsQueries.CastSphere(castSphereRequest)),
            YmirCircleCastCase("cast-circle-sorts-impact-distance", "Circle cast hits sort by impact distance, then id.", castCircleRequest, YmirPhysicsQueries.CastCircle(castCircleRequest)),
            YmirCircleCastCase("cast-circle-invalid-direction-skips", "Invalid cast direction returns no hits.", invalidCastCircleRequest, YmirPhysicsQueries.CastCircle(invalidCastCircleRequest))
        }
    };
}

static YmirQueryFixtureCase YmirStepCase(
    string caseId,
    string intent,
    YmirStepRequest request,
    YmirStepResult result)
{
    return new YmirQueryFixtureCase
    {
        CaseId = caseId,
        QueryKind = "step",
        Intent = intent,
        Request = new YmirQueryFixturePayload
        {
            DeltaTime = request.deltaTime,
            World = ToFixtureWorld(request.world)
        },
        Result = new YmirQueryFixturePayload
        {
            World = ToFixtureWorld(result.world),
            Contacts = ToFixtureContacts(result.contacts)
        }
    };
}

static YmirQueryFixtureCase YmirSphereOverlapCase(
    string caseId,
    string intent,
    YmirSphereOverlapRequest request,
    YmirSphereOverlapResult result)
{
    return new YmirQueryFixtureCase
    {
        CaseId = caseId,
        QueryKind = "overlap-sphere",
        Intent = intent,
        Request = new YmirQueryFixturePayload
        {
            Center3 = ToFixtureVec3(request.center),
            Radius = request.radius,
            SphereBodies = ToFixtureSphereBodies(request.bodies)
        },
        Result = new YmirQueryFixturePayload
        {
            SphereOverlapHits = ToFixtureSphereOverlapHits(result.hits)
        }
    };
}

static YmirQueryFixtureCase YmirCircleOverlapCase(
    string caseId,
    string intent,
    YmirCircleOverlapRequest request,
    YmirCircleOverlapResult result)
{
    return new YmirQueryFixtureCase
    {
        CaseId = caseId,
        QueryKind = "overlap-circle",
        Intent = intent,
        Request = new YmirQueryFixturePayload
        {
            Center2 = ToFixtureVec2(request.center),
            Radius = request.radius,
            World = ToFixtureWorld(request.world)
        },
        Result = new YmirQueryFixturePayload
        {
            CircleOverlapHits = ToFixtureCircleOverlapHits(result.hits)
        }
    };
}

static YmirQueryFixtureCase YmirSphereCastCase(
    string caseId,
    string intent,
    YmirSphereCastRequest request,
    YmirSphereCastResult result)
{
    return new YmirQueryFixtureCase
    {
        CaseId = caseId,
        QueryKind = "cast-sphere",
        Intent = intent,
        Request = new YmirQueryFixturePayload
        {
            Origin3 = ToFixtureVec3(request.origin),
            Direction3 = ToFixtureVec3(request.direction),
            Distance = request.distance,
            Radius = request.radius,
            SphereBodies = ToFixtureSphereBodies(request.bodies)
        },
        Result = new YmirQueryFixturePayload
        {
            SphereCastHits = ToFixtureSphereCastHits(result.hits)
        }
    };
}

static YmirQueryFixtureCase YmirCircleCastCase(
    string caseId,
    string intent,
    YmirCircleCastRequest request,
    YmirCircleCastResult result)
{
    return new YmirQueryFixtureCase
    {
        CaseId = caseId,
        QueryKind = "cast-circle",
        Intent = intent,
        Request = new YmirQueryFixturePayload
        {
            Origin2 = ToFixtureVec2(request.origin),
            Direction2 = ToFixtureVec2(request.direction),
            Distance = request.distance,
            Radius = request.radius,
            World = ToFixtureWorld(request.world)
        },
        Result = new YmirQueryFixturePayload
        {
            CircleCastHits = ToFixtureCircleCastHits(result.hits)
        }
    };
}

static YmirFixtureWorld ToFixtureWorld(YmirWorld world)
{
    return new YmirFixtureWorld
    {
        Time = world?.time ?? 0,
        Bodies = ToFixtureBodies(world?.bodies),
        Fields = ToFixtureFields(world?.fields)
    };
}

static YmirFixtureBody[] ToFixtureBodies(IReadOnlyList<YmirPhysicsBody>? bodies)
{
    return (bodies ?? Array.Empty<YmirPhysicsBody>())
        .Select(body => new YmirFixtureBody
        {
            Id = body?.id ?? "",
            Position = ToFixtureVec2(body?.position ?? default),
            Velocity = ToFixtureVec2(body?.velocity ?? default),
            Direction = ToFixtureVec2(body?.direction ?? default),
            AngularVelocity = body?.angularVelocity ?? 0,
            Torque = body?.torque ?? 0,
            MomentOfInertia = body?.momentOfInertia ?? 0,
            Radius = body?.radius ?? 0,
            Mass = body?.mass ?? 0,
            IsStatic = body?.isStatic ?? false,
            Restitution = body?.restitution ?? 0
        })
        .ToArray();
}

static YmirFixtureSphereBody[] ToFixtureSphereBodies(IReadOnlyList<YmirSphereQueryBody>? bodies)
{
    return (bodies ?? Array.Empty<YmirSphereQueryBody>())
        .Select(body => new YmirFixtureSphereBody
        {
            Id = body?.id ?? "",
            Position = ToFixtureVec3(body?.position ?? default),
            Radius = body?.radius ?? 0
        })
        .ToArray();
}

static YmirFixtureField[] ToFixtureFields(IReadOnlyList<YmirRadialField>? fields)
{
    return (fields ?? Array.Empty<YmirRadialField>())
        .Select(field => new YmirFixtureField
        {
            Id = field?.id ?? "",
            Position = ToFixtureVec2(field?.position ?? default),
            Strength = field?.strength ?? 0,
            Radius = field?.radius ?? 0
        })
        .ToArray();
}

static YmirFixtureContact[] ToFixtureContacts(IReadOnlyList<YmirContactEvent>? contacts)
{
    return (contacts ?? Array.Empty<YmirContactEvent>())
        .Select(contact => new YmirFixtureContact
        {
            BodyA = contact?.bodyA ?? "",
            BodyB = contact?.bodyB ?? "",
            Point = ToFixtureVec2(contact?.point ?? default),
            Normal = ToFixtureVec2(contact?.normal ?? default),
            Penetration = contact?.penetration ?? 0,
            RelativeSpeed = contact?.relativeSpeed ?? 0
        })
        .ToArray();
}

static YmirFixtureCircleOverlapHit[] ToFixtureCircleOverlapHits(IReadOnlyList<YmirCircleOverlapHit>? hits)
{
    return (hits ?? Array.Empty<YmirCircleOverlapHit>())
        .Select(hit => new YmirFixtureCircleOverlapHit
        {
            BodyId = hit?.bodyId ?? "",
            Point = ToFixtureVec2(hit?.point ?? default),
            Normal = ToFixtureVec2(hit?.normal ?? default),
            Penetration = hit?.penetration ?? 0,
            Distance = hit?.distance ?? 0
        })
        .ToArray();
}

static YmirFixtureSphereOverlapHit[] ToFixtureSphereOverlapHits(IReadOnlyList<YmirSphereOverlapHit>? hits)
{
    return (hits ?? Array.Empty<YmirSphereOverlapHit>())
        .Select(hit => new YmirFixtureSphereOverlapHit
        {
            BodyId = hit?.bodyId ?? "",
            Point = ToFixtureVec3(hit?.point ?? default),
            Normal = ToFixtureVec3(hit?.normal ?? default),
            Penetration = hit?.penetration ?? 0,
            Distance = hit?.distance ?? 0
        })
        .ToArray();
}

static YmirFixtureCircleCastHit[] ToFixtureCircleCastHits(IReadOnlyList<YmirCircleCastHit>? hits)
{
    return (hits ?? Array.Empty<YmirCircleCastHit>())
        .Select(hit => new YmirFixtureCircleCastHit
        {
            BodyId = hit?.bodyId ?? "",
            Point = ToFixtureVec2(hit?.point ?? default),
            Normal = ToFixtureVec2(hit?.normal ?? default),
            Distance = hit?.distance ?? 0
        })
        .ToArray();
}

static YmirFixtureSphereCastHit[] ToFixtureSphereCastHits(IReadOnlyList<YmirSphereCastHit>? hits)
{
    return (hits ?? Array.Empty<YmirSphereCastHit>())
        .Select(hit => new YmirFixtureSphereCastHit
        {
            BodyId = hit?.bodyId ?? "",
            Point = ToFixtureVec3(hit?.point ?? default),
            Normal = ToFixtureVec3(hit?.normal ?? default),
            Distance = hit?.distance ?? 0
        })
        .ToArray();
}

static CultVec2 ToFixtureVec2(YmirVec2 value)
{
    return new CultVec2(value.x, value.y);
}

static CultVec3 ToFixtureVec3(YmirVec3 value)
{
    return new CultVec3(value.x, value.y, value.z);
}

static YmirWorld YmirWorldOf(params YmirPhysicsBody[] bodies)
{
    return new YmirWorld
    {
        time = 0,
        bodies = bodies,
        fields = Array.Empty<YmirRadialField>()
    };
}

static YmirPhysicsBody YmirBody(string id, float x, float y, float radius)
{
    return new YmirPhysicsBody
    {
        id = id,
        position = YmirVec2Of(x, y),
        direction = YmirVec2Of(1, 0),
        radius = radius,
        mass = 1,
        momentOfInertia = 1
    };
}

static YmirSphereQueryBody YmirSphereBody(string id, float x, float y, float z, float radius)
{
    return new YmirSphereQueryBody
    {
        id = id,
        position = YmirVec3Of(x, y, z),
        radius = radius
    };
}

static YmirVec2 YmirVec2Of(float x, float y)
{
    return new YmirVec2 { x = x, y = y };
}

static YmirVec3 YmirVec3Of(float x, float y, float z)
{
    return new YmirVec3 { x = x, y = y, z = z };
}

static AuthorityDecisionCase AuthorityCase(
    string caseId,
    AetheriaRuntimeVerseAuthorityPolicyDocument policy,
    IReadOnlyList<AetheriaRuntimeAuthorityLeaseDocument> leases,
    AetheriaRuntimeDaemonCommandDocument command,
    string localRuntimeId)
{
    var decision = AetheriaRuntimeAuthorityRouter.Authorize(command, policy, leases, localRuntimeId);
    return new AuthorityDecisionCase
    {
        CaseId = caseId,
        LocalRuntimeId = localRuntimeId,
        Policy = policy,
        Leases = leases.ToArray(),
        Command = command,
        Decision = new AuthorityDecisionSnapshot
        {
            Authorized = decision.Authorized,
            Reason = decision.Reason,
            Mode = decision.Mode,
            SubjectKey = decision.SubjectKey,
            ClaimKind = decision.ClaimKind,
            AuthorRuntimeId = decision.AuthorRuntimeId,
            RuleId = decision.RuleId
        }
    };
}

static AetheriaRuntimeVerseAuthorityPolicyDocument HostPolicy(string verseId, string hostRuntimeId)
{
    return new AetheriaRuntimeVerseAuthorityPolicyDocument
    {
        VerseId = verseId,
        PolicyId = "aetheria.freeze.host-authoritative.v1",
        HostRuntimeId = hostRuntimeId,
        DefaultMode = AetheriaRuntimeAuthorityModes.HostAuthoritative,
        DeploymentMode = AetheriaRuntimeVerseDeploymentModes.DedicatedDaemon,
        UpdatedAtUtc = "2026-01-01T00:00:00.0000000Z",
        Rules = new[]
        {
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "freeze.host.default",
                SubjectPrefix = "*",
                ClaimKinds = new[] { AetheriaRuntimeClaimKinds.Any },
                Mode = AetheriaRuntimeAuthorityModes.HostAuthoritative
            }
        }
    };
}

static AetheriaRuntimeVerseAuthorityPolicyDocument DelegatedPolicy(
    string verseId,
    string hostRuntimeId,
    string subjectPrefix,
    string claimKind,
    string runtimeId)
{
    return new AetheriaRuntimeVerseAuthorityPolicyDocument
    {
        VerseId = verseId,
        PolicyId = "aetheria.freeze.delegated.v1",
        HostRuntimeId = hostRuntimeId,
        DefaultMode = AetheriaRuntimeAuthorityModes.HostAuthoritative,
        DeploymentMode = AetheriaRuntimeVerseDeploymentModes.DistributedTrusted,
        UpdatedAtUtc = "2026-01-01T00:00:00.0000000Z",
        Rules = new[]
        {
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "freeze.delegated.subject-claim",
                SubjectPrefix = subjectPrefix,
                ClaimKinds = new[] { claimKind },
                Mode = AetheriaRuntimeAuthorityModes.DelegatedRuntime,
                RuntimeIds = new[] { runtimeId },
                Priority = 100
            },
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "freeze.host.default",
                SubjectPrefix = "*",
                ClaimKinds = new[] { AetheriaRuntimeClaimKinds.Any },
                Mode = AetheriaRuntimeAuthorityModes.HostAuthoritative
            }
        }
    };
}

static AetheriaRuntimeVerseAuthorityPolicyDocument LeasePolicy(
    string verseId,
    string hostRuntimeId,
    string subjectPrefix,
    string claimKind)
{
    return new AetheriaRuntimeVerseAuthorityPolicyDocument
    {
        VerseId = verseId,
        PolicyId = "aetheria.freeze.interest-lease.v1",
        HostRuntimeId = hostRuntimeId,
        DefaultMode = AetheriaRuntimeAuthorityModes.HostAuthoritative,
        DeploymentMode = AetheriaRuntimeVerseDeploymentModes.DistributedTrusted,
        UpdatedAtUtc = "2026-01-01T00:00:00.0000000Z",
        Rules = new[]
        {
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "freeze.lease.subject-claim",
                SubjectPrefix = subjectPrefix,
                ClaimKinds = new[] { claimKind },
                Mode = AetheriaRuntimeAuthorityModes.InterestLease,
                LeaseScope = "nearby-interest",
                Priority = 100
            },
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "freeze.host.default",
                SubjectPrefix = "*",
                ClaimKinds = new[] { AetheriaRuntimeClaimKinds.Any },
                Mode = AetheriaRuntimeAuthorityModes.HostAuthoritative
            }
        }
    };
}

static AetheriaRuntimeVerseAuthorityPolicyDocument OwningPolicy(string verseId, string hostRuntimeId, string subjectPrefix)
{
    return new AetheriaRuntimeVerseAuthorityPolicyDocument
    {
        VerseId = verseId,
        PolicyId = "aetheria.freeze.owning-runtime.v1",
        HostRuntimeId = hostRuntimeId,
        DefaultMode = AetheriaRuntimeAuthorityModes.HostAuthoritative,
        DeploymentMode = AetheriaRuntimeVerseDeploymentModes.DistributedTrusted,
        UpdatedAtUtc = "2026-01-01T00:00:00.0000000Z",
        Rules = new[]
        {
            new AetheriaRuntimeAuthorityRule
            {
                RuleId = "freeze.owning.subject",
                SubjectPrefix = subjectPrefix,
                ClaimKinds = new[] { AetheriaRuntimeClaimKinds.Movement },
                Mode = AetheriaRuntimeAuthorityModes.OwningRuntime,
                Priority = 100
            }
        }
    };
}

static AetheriaRuntimeDaemonCommandDocument AuthorityCommand(
    string commandId,
    AetheriaRuntimeDaemonCommandKinds kind,
    string actor,
    string authorRuntimeId,
    Action<AetheriaRuntimeDaemonCommandDocument>? configure = null)
{
    var command = Command(commandId, kind, actor, configure);
    command.AuthorRuntimeId = authorRuntimeId;
    command.ClientId = authorRuntimeId;
    return command;
}

static AetheriaRuntimeDaemonCommandDocument Command(
    string commandId,
    AetheriaRuntimeDaemonCommandKinds kind,
    string actor,
    Action<AetheriaRuntimeDaemonCommandDocument>? configure = null)
{
    var command = AetheriaRuntimeDaemonCommandDocument.Create(
        kind,
        "aetheria-freeze-fixture",
        "freeze",
        observedFrameId: -1,
        actorEntityKey: actor);
    command.CommandId = commandId;
    command.IssuedAtUtc = "2026-01-01T00:00:00.0000000Z";
    configure?.Invoke(command);
    return command;
}

static int ResolveSelectedEntityIndex(AetheriaRuntimeDaemonFrameDocument frame)
{
    var contextRun = frame.Run ?? new AetheriaRuntimeRunCheckpointCommit();
    var current = contextRun.CurrentEntityKey ?? "";
    var marker = ".entity.";
    var markerIndex = current.LastIndexOf(marker, StringComparison.Ordinal);
    if (markerIndex >= 0)
    {
        var start = markerIndex + marker.Length;
        var end = current.IndexOf('.', start);
        var token = end < 0 ? current[start..] : current[start..end];
        if (int.TryParse(token, out var parsed))
            return parsed;
    }

    var currentZone = (contextRun.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
        .FirstOrDefault(zone => zone != null && zone.ZoneIndex == contextRun.CurrentZoneIndex) ??
        (contextRun.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>()).FirstOrDefault();
    return (currentZone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
        .FirstOrDefault()?.EntityIndex ?? -1;
}

static void WriteArtifact<T>(string outputDirectory, FreezeManifest manifest, string name, string schemaId, T document)
{
    var jsonFile = name + ".json";
    var msgpackFile = name + ".msgpack";
    WriteJson(Path.Combine(outputDirectory, jsonFile), document);
    File.WriteAllBytes(Path.Combine(outputDirectory, msgpackFile), MessagePackSerializer.Serialize(document));
    manifest.Artifacts.Add(new FreezeArtifact
    {
        Name = name,
        SchemaId = schemaId,
        Json = jsonFile,
        MessagePack = msgpackFile,
        DocumentType = typeof(T).FullName ?? typeof(T).Name
    });
}

static void WriteJson<T>(string path, T value)
{
    var json = JsonSerializer.Serialize(value, FreezeJson.Options);
    File.WriteAllText(path, json);
}

static string Quote(string value)
{
    return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

internal sealed class FreezeManifest
{
    public string Scenario { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string Root { get; set; } = "";
    public string StatePath { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public bool RanDaemon { get; set; }
    public int SeededCommandCount { get; set; }
    public List<FreezeArtifact> Artifacts { get; } = new();
}

internal sealed class FreezeArtifact
{
    public string Name { get; set; } = "";
    public string SchemaId { get; set; } = "";
    public string DocumentType { get; set; } = "";
    public string Json { get; set; } = "";
    public string MessagePack { get; set; } = "";
}

[MessagePackObject]
internal sealed class YmirQueryFixtureSet
{
    [Key(0)] public string Schema { get; set; } = "";
    [Key(1)] public string GeneratedAtUtc { get; set; } = "";
    [Key(2)] public YmirQueryFixtureCase[] Cases { get; set; } = Array.Empty<YmirQueryFixtureCase>();
}

[MessagePackObject]
internal sealed class YmirQueryFixtureCase
{
    [Key(0)] public string CaseId { get; set; } = "";
    [Key(1)] public string QueryKind { get; set; } = "";
    [Key(2)] public string Intent { get; set; } = "";
    [Key(3)] public YmirQueryFixturePayload Request { get; set; } = new();
    [Key(4)] public YmirQueryFixturePayload Result { get; set; } = new();
}

[MessagePackObject]
internal sealed class YmirQueryFixturePayload
{
    [Key(0)] public float? DeltaTime { get; set; }
    [Key(1)] public float? Radius { get; set; }
    [Key(2)] public float? Distance { get; set; }
    [Key(3)] public CultVec2? Center2 { get; set; }
    [Key(4)] public CultVec3? Center3 { get; set; }
    [Key(5)] public CultVec2? Origin2 { get; set; }
    [Key(6)] public CultVec3? Origin3 { get; set; }
    [Key(7)] public CultVec2? Direction2 { get; set; }
    [Key(8)] public CultVec3? Direction3 { get; set; }
    [Key(9)] public YmirFixtureWorld? World { get; set; }
    [Key(10)] public YmirFixtureSphereBody[] SphereBodies { get; set; } = Array.Empty<YmirFixtureSphereBody>();
    [Key(11)] public YmirFixtureCircleOverlapHit[] CircleOverlapHits { get; set; } = Array.Empty<YmirFixtureCircleOverlapHit>();
    [Key(12)] public YmirFixtureSphereOverlapHit[] SphereOverlapHits { get; set; } = Array.Empty<YmirFixtureSphereOverlapHit>();
    [Key(13)] public YmirFixtureCircleCastHit[] CircleCastHits { get; set; } = Array.Empty<YmirFixtureCircleCastHit>();
    [Key(14)] public YmirFixtureSphereCastHit[] SphereCastHits { get; set; } = Array.Empty<YmirFixtureSphereCastHit>();
    [Key(15)] public YmirFixtureContact[] Contacts { get; set; } = Array.Empty<YmirFixtureContact>();
}

[MessagePackObject]
internal sealed class YmirFixtureWorld
{
    [Key(0)] public float Time { get; set; }
    [Key(1)] public YmirFixtureBody[] Bodies { get; set; } = Array.Empty<YmirFixtureBody>();
    [Key(2)] public YmirFixtureField[] Fields { get; set; } = Array.Empty<YmirFixtureField>();
}

[MessagePackObject]
internal sealed class YmirFixtureBody
{
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public CultVec2 Position { get; set; }
    [Key(2)] public CultVec2 Velocity { get; set; }
    [Key(3)] public CultVec2 Direction { get; set; }
    [Key(4)] public float AngularVelocity { get; set; }
    [Key(5)] public float Torque { get; set; }
    [Key(6)] public float MomentOfInertia { get; set; }
    [Key(7)] public float Radius { get; set; }
    [Key(8)] public float Mass { get; set; }
    [Key(9)] public bool IsStatic { get; set; }
    [Key(10)] public float Restitution { get; set; }
}

[MessagePackObject]
internal sealed class YmirFixtureSphereBody
{
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public CultVec3 Position { get; set; }
    [Key(2)] public float Radius { get; set; }
}

[MessagePackObject]
internal sealed class YmirFixtureField
{
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public CultVec2 Position { get; set; }
    [Key(2)] public float Strength { get; set; }
    [Key(3)] public float Radius { get; set; }
}

[MessagePackObject]
internal sealed class YmirFixtureContact
{
    [Key(0)] public string BodyA { get; set; } = "";
    [Key(1)] public string BodyB { get; set; } = "";
    [Key(2)] public CultVec2 Point { get; set; }
    [Key(3)] public CultVec2 Normal { get; set; }
    [Key(4)] public float Penetration { get; set; }
    [Key(5)] public float RelativeSpeed { get; set; }
}

[MessagePackObject]
internal sealed class YmirFixtureCircleOverlapHit
{
    [Key(0)] public string BodyId { get; set; } = "";
    [Key(1)] public CultVec2 Point { get; set; }
    [Key(2)] public CultVec2 Normal { get; set; }
    [Key(3)] public float Penetration { get; set; }
    [Key(4)] public float Distance { get; set; }
}

[MessagePackObject]
internal sealed class YmirFixtureSphereOverlapHit
{
    [Key(0)] public string BodyId { get; set; } = "";
    [Key(1)] public CultVec3 Point { get; set; }
    [Key(2)] public CultVec3 Normal { get; set; }
    [Key(3)] public float Penetration { get; set; }
    [Key(4)] public float Distance { get; set; }
}

[MessagePackObject]
internal sealed class YmirFixtureCircleCastHit
{
    [Key(0)] public string BodyId { get; set; } = "";
    [Key(1)] public CultVec2 Point { get; set; }
    [Key(2)] public CultVec2 Normal { get; set; }
    [Key(3)] public float Distance { get; set; }
}

[MessagePackObject]
internal sealed class YmirFixtureSphereCastHit
{
    [Key(0)] public string BodyId { get; set; } = "";
    [Key(1)] public CultVec3 Point { get; set; }
    [Key(2)] public CultVec3 Normal { get; set; }
    [Key(3)] public float Distance { get; set; }
}

[MessagePackObject]
internal sealed class AuthorityDecisionFixtureSet
{
    [Key(0)] public string Schema { get; set; } = "";
    [Key(1)] public string GeneratedAtUtc { get; set; } = "";
    [Key(2)] public AuthorityDecisionCase[] Cases { get; set; } = Array.Empty<AuthorityDecisionCase>();
}

[MessagePackObject]
internal sealed class AuthorityDecisionCase
{
    [Key(0)] public string CaseId { get; set; } = "";
    [Key(1)] public string LocalRuntimeId { get; set; } = "";
    [Key(2)] public AetheriaRuntimeVerseAuthorityPolicyDocument Policy { get; set; } = new();
    [Key(3)] public AetheriaRuntimeAuthorityLeaseDocument[] Leases { get; set; } = Array.Empty<AetheriaRuntimeAuthorityLeaseDocument>();
    [Key(4)] public AetheriaRuntimeDaemonCommandDocument Command { get; set; } = new();
    [Key(5)] public AuthorityDecisionSnapshot Decision { get; set; } = new();
}

[MessagePackObject]
internal sealed class AuthorityDecisionSnapshot
{
    [Key(0)] public bool Authorized { get; set; }
    [Key(1)] public string Reason { get; set; } = "";
    [Key(2)] public string Mode { get; set; } = "";
    [Key(3)] public string SubjectKey { get; set; } = "";
    [Key(4)] public string ClaimKind { get; set; } = "";
    [Key(5)] public string AuthorRuntimeId { get; set; } = "";
    [Key(6)] public string RuleId { get; set; } = "";
}

internal static class FreezeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };
}

internal sealed class FreezeOptions
{
    public string Root { get; init; } = "";
    public string StatePath { get; init; } = "";
    public string OutputDirectory { get; init; } = "";
    public bool RunDaemon { get; init; } = true;
    public bool CleanState { get; init; } = true;
    public bool IncludeCommandBatches { get; init; } = true;
    public double ViewportMinX { get; init; } = -1000;
    public double ViewportMinY { get; init; } = -1000;
    public double ViewportMaxX { get; init; } = 1000;
    public double ViewportMaxY { get; init; } = 1000;

    public static FreezeOptions Parse(IReadOnlyList<string> args)
    {
        var root = FullPath(ReadOption(args, "--root"), Directory.GetCurrentDirectory());
        var defaultState = Path.Combine(root, "obj", "freeze", "aetheria-freeze-state.cc");
        var defaultOutput = Path.Combine(root, "Aetheria.State", "fixtures", "rust-rebuild-freeze");
        return new FreezeOptions
        {
            Root = root,
            StatePath = FullPath(ReadOption(args, "--state"), defaultState),
            OutputDirectory = FullPath(ReadOption(args, "--out"), defaultOutput),
            RunDaemon = !HasFlag(args, "--no-run-daemon"),
            CleanState = !HasFlag(args, "--no-clean-state"),
            IncludeCommandBatches = !HasFlag(args, "--no-command-batches"),
            ViewportMinX = ReadDouble(args, "--viewport-min-x") ?? -1000,
            ViewportMinY = ReadDouble(args, "--viewport-min-y") ?? -1000,
            ViewportMaxX = ReadDouble(args, "--viewport-max-x") ?? 1000,
            ViewportMaxY = ReadDouble(args, "--viewport-max-y") ?? 1000
        };
    }

    private static string FullPath(string value, string fallback)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(value) ? fallback : value);
    }

    private static string ReadOption(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return "";
    }

    private static double? ReadDouble(IReadOnlyList<string> args, string name)
    {
        return double.TryParse(ReadOption(args, name), out var value) ? value : null;
    }

    private static bool HasFlag(IReadOnlyList<string> args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }
}
