using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Unity;
using GameCult.Caching;

namespace Aetheria.State;

public sealed class AetheriaRuntimeCommitApplyReport
{
    public int AppliedPlayerSettings { get; set; }
    public int AppliedLoadoutTemplates { get; set; }
    public int AppliedRunCheckpoints { get; set; }
    public string[] AppliedPaths { get; set; } = [];
}

public static class AetheriaRuntimeCommitLogApplier
{
    public static async Task<AetheriaRuntimeCommitApplyReport> ApplyPendingAsync(
        AetheriaStateNode node,
        bool deleteApplied = true)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        var pendingDirectory = node.StatePath + ".pending";
        var report = new AetheriaRuntimeCommitApplyReport();
        var applied = new List<string>();

        if (!Directory.Exists(pendingDirectory))
            return report;

        foreach (var path in Directory.EnumerateFiles(pendingDirectory, "*.cc").OrderBy(path => path, StringComparer.Ordinal))
        {
            var command = ReadCommand(path);
            switch (command.Kind)
            {
                case "player_settings":
                    await node.PutPlayerSettingsAsync(
                        ToPlayerSettings(command.PlayerSettings ?? throw MissingPayload(command), command.CreatedAtUtc))
                        .ConfigureAwait(false);
                    report.AppliedPlayerSettings++;
                    break;
                case "loadout_template":
                    var loadout = ToLoadoutTemplate(
                        command.LoadoutTemplate ?? throw MissingPayload(command),
                        command.CreatedAtUtc);
                    await node.PutLoadoutTemplateAsync(LoadoutKey(loadout.Name), loadout).ConfigureAwait(false);
                    report.AppliedLoadoutTemplates++;
                    break;
                case "run_checkpoint":
                    await ApplyRunCheckpointAsync(
                        node,
                        command.RunCheckpoint ?? throw MissingPayload(command),
                        command.CreatedAtUtc)
                        .ConfigureAwait(false);
                    report.AppliedRunCheckpoints++;
                    break;
                default:
                    throw new InvalidDataException($"Unknown Aetheria runtime commit kind '{command.Kind}' in {path}.");
            }

            applied.Add(path);
            if (deleteApplied)
                File.Delete(path);
        }

        await node.FlushAsync().ConfigureAwait(false);
        report.AppliedPaths = applied.ToArray();
        return report;
    }

    private static AetheriaRuntimeStateCommitDocument ReadCommand(string path)
    {
        var command = AetheriaRuntimePendingCultCacheStore.ReadStateCommit(path);
        if (!string.Equals(command.Schema, AetheriaRuntimeStateCommitDocument.SchemaId, StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected Aetheria runtime commit schema '{command.Schema}' in {path}.");
        return command;
    }

    private static AetheriaPlayerSettings ToPlayerSettings(
        AetheriaRuntimePlayerSettingsCommit settings,
        string updatedAtUtc)
    {
        return new AetheriaPlayerSettings
        {
            PlayerName = settings.PlayerName ?? "",
            TutorialPassed = settings.TutorialPassed,
            StoryFileHashes = (settings.StoryFileHashes ?? Array.Empty<AetheriaRuntimeStoryFileHashCommit>())
                .Select(hash => new AetheriaStoryFileHash
                {
                    StoryPath = hash.StoryPath ?? "",
                    Hash = hash.Hash ?? ""
                })
                .ToArray(),
            Gameplay = new AetheriaPlayerGameplaySettings
            {
                TemperatureUnit = settings.TemperatureUnit ?? "",
                SignificantDigits = settings.SignificantDigits
            },
            Graphics = new AetheriaPlayerGraphicsSettings
            {
                NebulaQuality = settings.NebulaQuality ?? "",
                ShowAsteroidsInMinimap = settings.ShowAsteroidsInMinimap
            },
            Input = new AetheriaPlayerInputSettings
            {
                BindingOverrides = (settings.BindingOverrides ?? Array.Empty<AetheriaRuntimeInputBindingCommit>())
                    .Select(binding => new AetheriaInputBindingOverride
                    {
                        ActionName = binding.ActionName ?? "",
                        BindingIndex = binding.BindingIndex,
                        BindingPath = binding.BindingPath ?? ""
                    })
                    .ToArray(),
                ActionBarInputs = (settings.ActionBarInputs ?? Array.Empty<string>())
                    .Select(input => input ?? "")
                    .ToArray()
            },
            LastUpdatedAtUtc = updatedAtUtc
        };
    }

    private static AetheriaLoadoutTemplate ToLoadoutTemplate(
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

    private static AetheriaEntityLoadout ToEntityLoadout(AetheriaRuntimeEntityLoadoutCommit? entity)
    {
        entity ??= new AetheriaRuntimeEntityLoadoutCommit();
        return new AetheriaEntityLoadout
        {
            Name = entity.Name ?? "",
            Kind = entity.Kind ?? "",
            FactionKey = ReferenceKey(entity.FactionKey ?? "", "aetheria.corporation", entity.CorporationLegacyId ?? ""),
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
            Enabled = item.Enabled
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

    private static AetheriaEntityItemSlot[] ToEntityItemSlots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit>? slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            .Select(slot =>
            {
                var item = ToLoadoutItem(slot.Item);
                return new AetheriaEntityItemSlot
                {
                    Position = new AetheriaGridCoord
                    {
                        X = slot.X,
                        Y = slot.Y
                    },
                    ItemKey = item.ItemKey,
                    Quality = item.Quality,
                    Durability = item.Durability,
                    Quantity = item.Quantity,
                    Enabled = item.Enabled
                };
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

    private static async Task ApplyRunCheckpointAsync(
        AetheriaStateNode node,
        AetheriaRuntimeRunCheckpointCommit checkpoint,
        string updatedAtUtc)
    {
        var run = new AetheriaRunState
        {
            RunId = checkpoint.RunId ?? "",
            IsTutorial = checkpoint.IsTutorial,
            EntranceZoneIndex = checkpoint.EntranceZoneIndex,
            ExitZoneIndex = checkpoint.ExitZoneIndex,
            CurrentZoneIndex = checkpoint.CurrentZoneIndex,
            CurrentZoneEntityIndex = checkpoint.CurrentZoneEntityIndex,
            DiscoveredZoneIndices = ToIntArray(checkpoint.DiscoveredZoneIndices),
            ActionBarBindings = ToActionBarBindings(checkpoint.ActionBarBindings),
            FactionRelationships = ToFactionRelationships(checkpoint.FactionRelationships),
            UpdatedAtUtc = updatedAtUtc,
            GenerationSeed = checkpoint.GenerationSeed
        };

        var zoneKeys = new List<string>();
        foreach (var zone in checkpoint.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
        {
            var zoneKey = ZoneKey(run.RunId, zone.ZoneIndex);
            var entityKeys = new List<string>();
            foreach (var entity in zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                var entityKey = EntityKey(run.RunId, zone.ZoneIndex, entity.EntityIndex);
                var snapshot = ToEntitySnapshot(entity, run.RunId, zone.ZoneIndex);
                await node.PutEntitySnapshotAsync(entityKey, snapshot).ConfigureAwait(false);
                entityKeys.Add(entityKey.ToString());
            }

            await node.PutZoneStateAsync(
                zoneKey,
                new AetheriaZoneState
                {
                    Name = zone.Name ?? "",
                    Position = new AetheriaVector2
                    {
                        X = zone.PositionX,
                        Y = zone.PositionY
                    },
                    AdjacentZoneIndices = ToIntArray(zone.AdjacentZoneIndices),
                    FactionIndices = ToIntArray(zone.FactionIndices),
                    OwnerFactionIndex = zone.OwnerFactionIndex,
                    EntityKeys = entityKeys.ToArray(),
                    Orbits = ToOrbitSnapshots(zone.Orbits),
                    Bodies = ToBodySnapshots(zone.Bodies, run.RunId, zone.ZoneIndex)
                })
                .ConfigureAwait(false);
            zoneKeys.Add(zoneKey.ToString());
        }

        if (zoneKeys.Count > 0)
            run.ZoneKeys = zoneKeys.ToArray();
        await node.PutRunStateAsync(RunKey(run.RunId), run).ConfigureAwait(false);
    }

    private static AetheriaOrbitSnapshot[] ToOrbitSnapshots(
        IReadOnlyList<AetheriaRuntimeOrbitSnapshotCommit>? orbits)
    {
        return (orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>())
            .Select(orbit => new AetheriaOrbitSnapshot
            {
                OrbitId = ReferenceKey(orbit.OrbitKey ?? "", "aetheria.orbit", orbit.OrbitLegacyId ?? ""),
                ParentId = ReferenceKey(orbit.ParentOrbitKey ?? "", "aetheria.orbit", orbit.ParentLegacyId ?? ""),
                Distance = orbit.Distance,
                Phase = orbit.Phase,
                FixedPosition = new AetheriaVector2
                {
                    X = orbit.FixedPositionX,
                    Y = orbit.FixedPositionY
                }
            })
            .ToArray();
    }

    private static AetheriaBodySnapshot[] ToBodySnapshots(
        IReadOnlyList<AetheriaRuntimeBodySnapshotCommit>? bodies,
        string runId,
        int zoneIndex)
    {
        return (bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
            .Select(body => new AetheriaBodySnapshot
            {
                BodyId = ReferenceKey(body.BodyKey ?? "", "aetheria.body", body.BodyLegacyId ?? ""),
                Kind = body.Kind ?? "",
                Name = body.Name ?? "",
                OrbitId = ReferenceKey(body.OrbitKey ?? "", "aetheria.orbit", body.OrbitLegacyId ?? ""),
                Mass = body.Mass,
                Resources = ToBodyResources(body.Resources),
                BodyRadiusMultiplier = body.BodyRadiusMultiplier,
                GravityRadiusMultiplier = body.GravityRadiusMultiplier,
                GravityDepthMultiplier = body.GravityDepthMultiplier,
                GravityDepthExponent = body.GravityDepthExponent,
                Asteroids = ToAsteroidSnapshots(body.Asteroids, runId, zoneIndex),
                GasGiantVisual = ToGasGiantVisual(body.GasGiantVisual),
                SunVisual = ToSunVisual(body.SunVisual)
            })
            .ToArray();
    }

    private static AetheriaBodyResource[] ToBodyResources(
        IReadOnlyList<AetheriaRuntimeBodyResourceCommit>? resources)
    {
        return (resources ?? Array.Empty<AetheriaRuntimeBodyResourceCommit>())
            .Select(resource => new AetheriaBodyResource
            {
                ItemKey = resource.ItemKey ?? "",
                Amount = resource.Amount
            })
            .ToArray();
    }

    private static AetheriaAsteroidSnapshot[] ToAsteroidSnapshots(
        IReadOnlyList<AetheriaRuntimeAsteroidCommit>? asteroids,
        string runId,
        int zoneIndex)
    {
        return (asteroids ?? Array.Empty<AetheriaRuntimeAsteroidCommit>())
            .Select(asteroid => new AetheriaAsteroidSnapshot
            {
                Distance = asteroid.Distance,
                Phase = asteroid.Phase,
                Size = asteroid.Size,
                RotationSpeed = asteroid.RotationSpeed,
                Damage = asteroid.Damage,
                RespawnTimer = asteroid.RespawnTimer,
                MiningAccumulators = ToAsteroidMiningAccumulators(asteroid.MiningAccumulators, runId, zoneIndex)
            })
            .ToArray();
    }

    private static AetheriaAsteroidMiningAccumulatorSnapshot[] ToAsteroidMiningAccumulators(
        IReadOnlyList<AetheriaRuntimeAsteroidMiningAccumulatorCommit>? accumulators,
        string runId,
        int zoneIndex)
    {
        return (accumulators ?? Array.Empty<AetheriaRuntimeAsteroidMiningAccumulatorCommit>())
            .Where(accumulator => accumulator.MinerEntityIndex >= 0)
            .Select(accumulator => new AetheriaAsteroidMiningAccumulatorSnapshot
            {
                MinerEntityKey = EntityKey(runId, zoneIndex, accumulator.MinerEntityIndex).ToString(),
                Amount = accumulator.Amount
            })
            .ToArray();
    }

    private static AetheriaGasGiantVisualState ToGasGiantVisual(AetheriaRuntimeGasGiantVisualCommit? visual)
    {
        if (visual == null)
            return new AetheriaGasGiantVisualState();

        return new AetheriaGasGiantVisualState
        {
            FirstOffsetDomainRotationSpeed = visual.FirstOffsetDomainRotationSpeed,
            FirstOffsetRotationSpeed = visual.FirstOffsetRotationSpeed,
            SecondOffsetDomainRotationSpeed = visual.SecondOffsetDomainRotationSpeed,
            SecondOffsetRotationSpeed = visual.SecondOffsetRotationSpeed,
            AlbedoRotationSpeed = visual.AlbedoRotationSpeed,
            WaveRadiusMultiplier = visual.WaveRadiusMultiplier,
            WaveDepthMultiplier = visual.WaveDepthMultiplier,
            WaveDepthExponent = visual.WaveDepthExponent,
            WaveSpeedMultiplier = visual.WaveSpeedMultiplier,
            MaterialOverrides = (visual.MaterialOverrides ?? Array.Empty<string>()).ToArray(),
            Colors = (visual.Colors ?? Array.Empty<AetheriaRuntimeColorCommit>())
                .Select(ToColor)
                .ToArray()
        };
    }

    private static AetheriaSunVisualState ToSunVisual(AetheriaRuntimeSunVisualCommit? visual)
    {
        if (visual == null)
            return new AetheriaSunVisualState();

        return new AetheriaSunVisualState
        {
            LightColor = new AetheriaVector3
            {
                X = visual.LightColorX,
                Y = visual.LightColorY,
                Z = visual.LightColorZ
            },
            FogTintColor = new AetheriaVector3
            {
                X = visual.FogTintColorX,
                Y = visual.FogTintColorY,
                Z = visual.FogTintColorZ
            },
            LightRadiusMultiplier = visual.LightRadiusMultiplier
        };
    }

    private static AetheriaColor ToColor(AetheriaRuntimeColorCommit color)
    {
        return new AetheriaColor
        {
            X = color.X,
            Y = color.Y,
            Z = color.Z,
            W = color.W
        };
    }

    private static AetheriaEntitySnapshot ToEntitySnapshot(
        AetheriaRuntimeEntitySnapshotCommit entity,
        string runId,
        int zoneIndex)
    {
        return new AetheriaEntitySnapshot
        {
            Name = entity.Name ?? "",
            Kind = entity.Kind ?? "",
            Position = new AetheriaVector3
            {
                X = entity.PositionX,
                Y = entity.PositionY,
                Z = entity.PositionZ
            },
            Direction = new AetheriaVector2
            {
                X = entity.DirectionX,
                Y = entity.DirectionY
            },
            Velocity = new AetheriaVector2
            {
                X = entity.VelocityX,
                Y = entity.VelocityY
            },
            TargetEntityKey = entity.TargetEntityIndex >= 0
                ? EntityKey(runId, zoneIndex, entity.TargetEntityIndex).ToString()
                : "",
            IsActive = entity.IsActive,
            HeatsinksEnabled = entity.HeatsinksEnabled,
            OverrideShutdown = entity.OverrideShutdown,
            TractorPower = entity.TractorPower,
            Heatstroke = entity.Heatstroke,
            Hypothermia = entity.Hypothermia,
            FactionKey = ReferenceKey(entity.FactionKey ?? "", "aetheria.corporation", entity.CorporationLegacyId ?? ""),
            HullItemKey = entity.HullItemKey ?? "",
            Equipment = ToEntityItemSlots(entity.Equipment),
            CargoBays = ToEntityItemSlots(entity.CargoBays),
            DockingBays = ToEntityItemSlots(entity.DockingBays),
            ChildEntityKeys = (entity.ChildEntityIndices ?? Array.Empty<int>())
                .Select(childIndex => EntityKey(runId, zoneIndex, childIndex).ToString())
                .ToArray(),
            WeaponGroups = ToIntArrayArray(entity.WeaponGroups)
                .Select(group => new AetheriaWeaponGroupSnapshot { EquipmentIndices = group })
                .ToArray(),
            StatGrids = ToEntityStatGrids(entity.StatGrids),
            ActiveConsumables = ToActiveConsumables(entity.ActiveConsumables),
            BehaviorProgress = ToBehaviorProgress(entity.BehaviorProgress),
            WeaponStates = ToWeaponStates(entity.WeaponStates, runId, zoneIndex),
            BehaviorStates = ToBehaviorStates(entity.BehaviorStates),
            CargoContents = ToCargoBays(entity.CargoContents),
            DockingBayContents = ToCargoBays(entity.DockingBayContents),
            DockingBayAssignments = ToIntArray(entity.DockingBayAssignments),
            Visibility = entity.Visibility,
            VisibilitySourceCount = entity.VisibilitySourceCount,
            Contacts = ToEntityContacts(entity.Contacts, runId, zoneIndex)
        };
    }

    private static AetheriaEntityContactSnapshot[] ToEntityContacts(
        IReadOnlyList<AetheriaRuntimeEntityContactCommit>? contacts,
        string runId,
        int zoneIndex)
    {
        return (contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
            .Where(contact => contact.TargetEntityIndex >= 0)
            .Select(contact => new AetheriaEntityContactSnapshot
            {
                TargetEntityKey = EntityKey(runId, zoneIndex, contact.TargetEntityIndex).ToString(),
                InfoGathered = contact.InfoGathered,
                Hostile = contact.Hostile,
                Visible = contact.Visible
            })
            .ToArray();
    }

    private static AetheriaBehaviorStateSnapshot[] ToBehaviorStates(
        IReadOnlyList<AetheriaRuntimeBehaviorStateCommit>? states)
    {
        return (states ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
            .Select(state => new AetheriaBehaviorStateSnapshot
            {
                OwnerKind = state.OwnerKind ?? "",
                OwnerIndex = state.OwnerIndex,
                BehaviorIndex = state.BehaviorIndex,
                BehaviorKind = state.BehaviorKind ?? "",
                Pinging = state.Pinging,
                PingCooldown = state.PingCooldown,
                PingLerp = state.PingLerp,
                PingRadius = state.PingRadius,
                PingedEntityCount = state.PingedEntityCount,
                RadiatorTemperature = state.RadiatorTemperature,
                Emissivity = state.Emissivity,
                PumpedHeat = state.PumpedHeat,
                WasteHeat = state.WasteHeat,
                EnergyUsage = state.EnergyUsage,
                ReactorDraw = state.ReactorDraw,
                ReactorLoadRatio = state.ReactorLoadRatio,
                CapacitorCharge = state.CapacitorCharge,
                CapacitorCapacity = state.CapacitorCapacity,
                CapacitorEfficiency = state.CapacitorEfficiency,
                AetherDriveAxisX = state.AetherDriveAxisX,
                AetherDriveAxisY = state.AetherDriveAxisY,
                AetherDriveAxisZ = state.AetherDriveAxisZ,
                AetherDriveThrustX = state.AetherDriveThrustX,
                AetherDriveThrustY = state.AetherDriveThrustY,
                AetherDriveThrustZ = state.AetherDriveThrustZ,
                AetherDriveRpmX = state.AetherDriveRpmX,
                AetherDriveRpmY = state.AetherDriveRpmY,
                AetherDriveRpmZ = state.AetherDriveRpmZ,
                AetherDriveMaximumRpm = state.AetherDriveMaximumRpm,
                AetherDriveThrustDirectionX = state.AetherDriveThrustDirectionX,
                AetherDriveThrustDirectionY = state.AetherDriveThrustDirectionY,
                ResourceScannerTargetBodyId = state.ResourceScannerTargetBodyId ?? "",
                ResourceScannerAsteroidIndex = state.ResourceScannerAsteroidIndex,
                ResourceScannerScanTime = state.ResourceScannerScanTime,
                ResourceScannerRange = state.ResourceScannerRange,
                ResourceScannerMinimumDensity = state.ResourceScannerMinimumDensity,
                ResourceScannerScanDuration = state.ResourceScannerScanDuration,
                MiningToolAsteroidBeltId = state.MiningToolAsteroidBeltId ?? "",
                MiningToolAsteroidIndex = state.MiningToolAsteroidIndex,
                MiningToolRange = state.MiningToolRange,
                ThrusterAxis = state.ThrusterAxis,
                ThrusterThrust = state.ThrusterThrust,
                ThrusterTorque = state.ThrusterTorque,
                ShieldEfficiency = state.ShieldEfficiency,
                ShieldEnergyUsage = state.ShieldEnergyUsage,
                VelocityLimit = state.VelocityLimit,
                ThermotoggleTargetTemperature = state.ThermotoggleTargetTemperature,
                SwitchActivated = state.SwitchActivated,
                TriggerPulled = state.TriggerPulled,
                StatModifierApplied = state.StatModifierApplied,
                StatModifierExecuted = state.StatModifierExecuted,
                StatModifierTargetStatCount = state.StatModifierTargetStatCount,
                TurretControllerWeaponCount = state.TurretControllerWeaponCount,
                TurretControllerShotSpeed = state.TurretControllerShotSpeed,
                TurretControllerPredictShots = state.TurretControllerPredictShots
            })
            .ToArray();
    }

    private static AetheriaWeaponStateSnapshot[] ToWeaponStates(
        IReadOnlyList<AetheriaRuntimeWeaponStateCommit>? states,
        string runId,
        int zoneIndex)
    {
        return (states ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
            .Select(state => new AetheriaWeaponStateSnapshot
            {
                OwnerKind = state.OwnerKind ?? "",
                OwnerIndex = state.OwnerIndex,
                BehaviorIndex = state.BehaviorIndex,
                BehaviorKind = state.BehaviorKind ?? "",
                Firing = state.Firing,
                Ammo = state.Ammo,
                BurstRemaining = state.BurstRemaining,
                BurstTimer = state.BurstTimer,
                BurstInterval = state.BurstInterval,
                CooldownProgress = state.CooldownProgress,
                CoolingDown = state.CoolingDown,
                Charging = state.Charging,
                Charged = state.Charged,
                Charge = state.Charge,
                Reloading = state.Reloading,
                ReloadProgress = state.ReloadProgress,
                AmmoIntervalProgress = state.AmmoIntervalProgress,
                LockProgress = state.LockProgress,
                LockTargetEntityKey = state.LockTargetEntityIndex >= 0
                    ? EntityKey(runId, zoneIndex, state.LockTargetEntityIndex).ToString()
                    : ""
            })
            .ToArray();
    }

    private static AetheriaBehaviorProgressSnapshot[] ToBehaviorProgress(
        IReadOnlyList<AetheriaRuntimeBehaviorProgressCommit>? progress)
    {
        return (progress ?? Array.Empty<AetheriaRuntimeBehaviorProgressCommit>())
            .Select(entry => new AetheriaBehaviorProgressSnapshot
            {
                OwnerKind = entry.OwnerKind ?? "",
                OwnerIndex = entry.OwnerIndex,
                BehaviorIndex = entry.BehaviorIndex,
                BehaviorKind = entry.BehaviorKind ?? "",
                Progress = entry.Progress
            })
            .ToArray();
    }

    private static AetheriaActiveConsumableSnapshot[] ToActiveConsumables(
        IReadOnlyList<AetheriaRuntimeActiveConsumableCommit>? consumables)
    {
        return (consumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
            .Select(consumable => new AetheriaActiveConsumableSnapshot
            {
                ItemKey = consumable.ItemKey ?? "",
                Quality = consumable.Quality,
                RemainingDuration = consumable.RemainingDuration,
                Duration = consumable.Duration
            })
            .ToArray();
    }

    private static AetheriaEntityStatGrid[] ToEntityStatGrids(
        IReadOnlyList<AetheriaRuntimeEntityStatGridCommit>? grids)
    {
        return (grids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
            .Select(grid => new AetheriaEntityStatGrid
            {
                Name = grid.Name ?? "",
                Width = grid.Width,
                Height = grid.Height,
                Values = (grid.Values ?? Array.Empty<double>()).ToArray()
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

    private static AetheriaActionBarBinding[] ToActionBarBindings(
        IReadOnlyList<AetheriaRuntimeActionBarBindingCommit>? bindings)
    {
        return (bindings ?? Array.Empty<AetheriaRuntimeActionBarBindingCommit>())
            .Select(binding => new AetheriaActionBarBinding
            {
                ControlPath = binding.ControlPath ?? "",
                Kind = binding.Kind ?? "",
                TargetKey = binding.ItemKey ?? "",
                EquipmentIndex = binding.EquipmentIndex,
                BehaviorIndex = binding.BehaviorIndex,
                WeaponGroup = binding.WeaponGroup
            })
            .ToArray();
    }

    private static AetheriaFactionRelationshipState[] ToFactionRelationships(
        IReadOnlyList<AetheriaRuntimeFactionRelationshipCommit>? relationships)
    {
        return (relationships ?? Array.Empty<AetheriaRuntimeFactionRelationshipCommit>())
            .Select(relationship => new AetheriaFactionRelationshipState
            {
                FactionKey = ReferenceKey(relationship.FactionKey ?? "", "aetheria.corporation", relationship.CorporationLegacyId ?? ""),
                Relationship = relationship.Relationship ?? "",
                Standing = relationship.Standing
            })
            .ToArray();
    }

    private static CultRecordKey LoadoutKey(string name)
    {
        return new CultRecordKey($"global:aetheria.loadout_template.{StableToken(name)}.v1");
    }

    private static CultRecordKey RunKey(string runId)
    {
        return new CultRecordKey($"global:aetheria.run_state.{StableToken(runId)}.v1");
    }

    private static CultRecordKey ZoneKey(string runId, int zoneIndex)
    {
        return new CultRecordKey($"global:aetheria.run_state.{StableToken(runId)}.zone.{zoneIndex}.v1");
    }

    private static CultRecordKey EntityKey(string runId, int zoneIndex, int entityIndex)
    {
        return new CultRecordKey($"global:aetheria.run_state.{StableToken(runId)}.zone.{zoneIndex}.entity.{entityIndex}.v1");
    }

    private static string ReferenceKey(string documentName, string legacyId)
    {
        return string.IsNullOrWhiteSpace(legacyId) ? "" : $"{documentName}:legacy:{legacyId}";
    }

    private static string ReferenceKey(string typedKey, string documentName, string legacyId)
    {
        return !string.IsNullOrWhiteSpace(typedKey)
            ? typedKey
            : ReferenceKey(documentName, legacyId);
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

    private static InvalidDataException MissingPayload(AetheriaRuntimeStateCommitDocument command)
    {
        return new InvalidDataException($"Aetheria runtime commit '{command.CommandId}' has no {command.Kind} payload.");
    }
}
