using GameCult.Aetheria.State.Verse;

public sealed class AetheriaDaemonCombatKernelSettings
{
    public static AetheriaDaemonCombatKernelSettings Default { get; } = new();

    public double DefaultHull { get; init; } = 100.0;
    public double DefaultShield { get; init; } = 35.0;
    public double DefaultHeatCapacity { get; init; } = 120.0;
    public double DefaultSignature { get; init; } = 1.0;
    public double DefaultSensorSensitivity { get; init; } = 1.0;
    public double DefaultSignatureMasking { get; init; } = 0.0;
    public double DefaultCognition { get; init; } = 1.0;
    public double DefaultFireControl { get; init; } = 1.0;
    public double SensorFalloffDistance { get; init; } = 520.0;
    public double TrackResolutionPerSecond { get; init; } = 0.46;
    public double VisibleTrackThreshold { get; init; } = 0.62;
    public double LaunchTrackThreshold { get; init; } = 0.34;
    public double TerminalSubsystemThreshold { get; init; } = 0.82;
    public double AbstractWeaponRange { get; init; } = 650.0;
    public double DefaultWeaponDamage { get; init; } = 11.0;
    public double WeaponCooldownSeconds { get; init; } = 1.0;
    public double WeaponHeat { get; init; } = 5.5;
    public double HeatDissipationPerSecond { get; init; } = 3.5;
    public double HeatSignatureScale { get; init; } = 0.85;
    public double MovementSignatureScale { get; init; } = 0.008;
    public double CognitiveLoadDecayPerSecond { get; init; } = 0.22;
    public double CognitiveLoadPerTrack { get; init; } = 0.08;
    public double SubsystemHitDamageBonus { get; init; } = 0.28;
}

public sealed class AetheriaDaemonCombatStepReport
{
    public AetheriaDaemonCombatStepReport(
        IReadOnlyList<AetheriaDaemonCombatEngagementReport> engagements,
        int resolvedContactCount)
    {
        Engagements = engagements ?? Array.Empty<AetheriaDaemonCombatEngagementReport>();
        ResolvedContactCount = resolvedContactCount;
    }

    public IReadOnlyList<AetheriaDaemonCombatEngagementReport> Engagements { get; }
    public int ResolvedContactCount { get; }
    public int ShotCount => Engagements.Count;
}

public sealed class AetheriaDaemonCombatEngagementReport
{
    public int AttackerEntityIndex { get; init; }
    public int TargetEntityIndex { get; init; }
    public double TrackConfidence { get; init; }
    public double HitQuality { get; init; }
    public double DamageApplied { get; init; }
    public bool SubsystemQualityHit { get; init; }
}

public static class AetheriaDaemonCombatKernel
{
    private const string Hull = "hull";
    private const string Shield = "shield";
    private const string Heat = "heat";
    private const string HeatCapacity = "heat-capacity";
    private const string Signature = "signature";
    private const string SignatureMasking = "signature-masking";
    private const string SensorSensitivity = "sensor-sensitivity";
    private const string Cognition = "cognition";
    private const string CognitiveLoad = "cognitive-load";
    private const string FireControl = "fire-control";
    private const string MunitionPressure = "munition-pressure";
    private const string KernelWeaponOwner = "daemon-combat-kernel";

    public static AetheriaDaemonCombatStepReport Step(
        AetheriaRuntimeRunCheckpointCommit? run,
        AetheriaRuntimeDaemonIntentState? intents,
        double deltaSeconds,
        AetheriaRuntimeCatalogSnapshot? catalog,
        AetheriaDaemonCombatKernelSettings? settings = null)
    {
        if (run == null || deltaSeconds <= 0 || !double.IsFinite(deltaSeconds))
            return new AetheriaDaemonCombatStepReport(Array.Empty<AetheriaDaemonCombatEngagementReport>(), 0);

        settings ??= AetheriaDaemonCombatKernelSettings.Default;
        var engagements = new List<AetheriaDaemonCombatEngagementReport>();
        var resolvedContacts = 0;
        foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
        {
            if (zone == null)
                continue;

            var entities = (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
                .Where(entity => entity != null)
                .OrderBy(entity => entity.EntityIndex)
                .ToArray();
            if (entities.Length <= 1)
                continue;

            foreach (var entity in entities)
                EnsureCombatStats(entity, catalog, settings);

            StepContactResolution(entities, deltaSeconds, settings, ref resolvedContacts);
            StepWeapons(entities, deltaSeconds, settings, engagements);
            StepThermalAndCognition(entities, deltaSeconds, settings);
        }

        return new AetheriaDaemonCombatStepReport(engagements, resolvedContacts);
    }

    private static void StepContactResolution(
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
        double deltaSeconds,
        AetheriaDaemonCombatKernelSettings settings,
        ref int resolvedContacts)
    {
        foreach (var observer in entities)
        {
            if (!IsAlive(observer))
                continue;

            var contacts = new List<AetheriaRuntimeEntityContactCommit>();
            var existing = (observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                .Where(contact => contact != null)
                .GroupBy(contact => contact.TargetEntityIndex)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var target in entities)
            {
                if (target.EntityIndex == observer.EntityIndex || !IsAlive(target))
                    continue;

                existing.TryGetValue(target.EntityIndex, out var previous);
                var confidence = previous?.InfoGathered ?? 0.0;
                var gain = ResolveTrackGain(observer, target, deltaSeconds, settings);
                confidence = Clamp01(confidence + gain);
                var visible = confidence >= settings.VisibleTrackThreshold;
                if (visible)
                    resolvedContacts++;

                contacts.Add(new AetheriaRuntimeEntityContactCommit
                {
                    TargetEntityIndex = target.EntityIndex,
                    InfoGathered = confidence,
                    Hostile = Hostile(observer, target),
                    Visible = visible
                });
            }

            observer.Contacts = contacts
                .OrderBy(contact => contact.TargetEntityIndex)
                .ToArray();
            observer.Visibility = contacts.Count == 0 ? 0.0 : contacts.Max(contact => contact.InfoGathered);
            observer.VisibilitySourceCount = contacts.Count(contact => contact.Visible);
            SetStat(observer, CognitiveLoad, ClampNonNegative(
                GetStat(observer, CognitiveLoad) +
                contacts.Count * settings.CognitiveLoadPerTrack * deltaSeconds));
        }
    }

    private static void StepWeapons(
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
        double deltaSeconds,
        AetheriaDaemonCombatKernelSettings settings,
        List<AetheriaDaemonCombatEngagementReport> engagements)
    {
        var byIndex = entities.ToDictionary(entity => entity.EntityIndex);
        foreach (var attacker in entities)
        {
            var weaponState = EnsureKernelWeaponState(attacker, settings);
            weaponState.Firing = false;
            weaponState.CooldownProgress = Math.Max(0, weaponState.CooldownProgress - deltaSeconds);
            weaponState.CoolingDown = weaponState.CooldownProgress > 0;

            if (!IsAlive(attacker) ||
                attacker.TargetEntityIndex < 0 ||
                !byIndex.TryGetValue(attacker.TargetEntityIndex, out var target) ||
                !IsAlive(target) ||
                !Hostile(attacker, target) ||
                Distance(attacker, target) > settings.AbstractWeaponRange)
            {
                continue;
            }

            var confidence = ContactConfidence(attacker, target.EntityIndex);
            if (confidence < settings.LaunchTrackThreshold || weaponState.CooldownProgress > 0)
                continue;

            var hitQuality = ResolveHitQuality(attacker, target, confidence);
            var subsystemHit = confidence >= settings.TerminalSubsystemThreshold &&
                EffectiveCognition(attacker) > 1.15;
            var damage = settings.DefaultWeaponDamage * hitQuality *
                (subsystemHit ? 1.0 + settings.SubsystemHitDamageBonus : 1.0);

            ApplyDamage(target, damage);
            SetStat(attacker, Heat, GetStat(attacker, Heat) + settings.WeaponHeat);
            SetStat(attacker, MunitionPressure, GetStat(attacker, MunitionPressure) + confidence * 0.1);
            weaponState.Firing = true;
            weaponState.CoolingDown = true;
            weaponState.CooldownProgress = settings.WeaponCooldownSeconds;
            weaponState.LockProgress = confidence;
            weaponState.LockTargetEntityIndex = target.EntityIndex;
            engagements.Add(new AetheriaDaemonCombatEngagementReport
            {
                AttackerEntityIndex = attacker.EntityIndex,
                TargetEntityIndex = target.EntityIndex,
                TrackConfidence = confidence,
                HitQuality = hitQuality,
                DamageApplied = damage,
                SubsystemQualityHit = subsystemHit
            });
        }
    }

    private static void StepThermalAndCognition(
        IReadOnlyList<AetheriaRuntimeEntitySnapshotCommit> entities,
        double deltaSeconds,
        AetheriaDaemonCombatKernelSettings settings)
    {
        foreach (var entity in entities)
        {
            SetStat(entity, Heat, Math.Max(0, GetStat(entity, Heat) - settings.HeatDissipationPerSecond * deltaSeconds));
            SetStat(entity, CognitiveLoad, Math.Max(0, GetStat(entity, CognitiveLoad) - settings.CognitiveLoadDecayPerSecond * deltaSeconds));
            entity.IsActive = IsAlive(entity);
            if (!entity.IsActive)
            {
                entity.VelocityX = 0;
                entity.VelocityY = 0;
                entity.TargetEntityIndex = -1;
            }
        }
    }

    private static void EnsureCombatStats(
        AetheriaRuntimeEntitySnapshotCommit entity,
        AetheriaRuntimeCatalogSnapshot? catalog,
        AetheriaDaemonCombatKernelSettings settings)
    {
        var hullItem = catalog?.FindItem(entity.HullItemKey ?? "");
        EnsureStat(entity, Hull, ResolveHull(entity, hullItem, settings));
        EnsureStat(entity, Shield, settings.DefaultShield);
        EnsureStat(entity, Heat, 0.0);
        EnsureStat(entity, HeatCapacity, ResolveHeatCapacity(entity, catalog, hullItem, settings));
        EnsureStat(entity, Signature, settings.DefaultSignature);
        EnsureStat(entity, SignatureMasking, settings.DefaultSignatureMasking);
        EnsureStat(entity, SensorSensitivity, settings.DefaultSensorSensitivity);
        EnsureStat(entity, Cognition, settings.DefaultCognition);
        EnsureStat(entity, CognitiveLoad, 0.0);
        EnsureStat(entity, FireControl, settings.DefaultFireControl);
        EnsureStat(entity, MunitionPressure, 0.0);
        if (!entity.IsActive && GetStat(entity, Hull) > 0)
            entity.IsActive = true;
    }

    private static double ResolveTrackGain(
        AetheriaRuntimeEntitySnapshotCommit observer,
        AetheriaRuntimeEntitySnapshotCommit target,
        double deltaSeconds,
        AetheriaDaemonCombatKernelSettings settings)
    {
        var distanceFactor = 1.0 + Distance(observer, target) / Math.Max(1.0, settings.SensorFalloffDistance);
        distanceFactor *= distanceFactor;
        var masking = 1.0 + Math.Max(0.0, GetStat(target, SignatureMasking));
        var sensor = Math.Max(0.05, GetStat(observer, SensorSensitivity));
        var cognition = Math.Max(0.05, EffectiveCognition(observer));
        var signature = ResolveSignature(target, settings);
        return deltaSeconds * settings.TrackResolutionPerSecond * sensor * cognition * signature / (distanceFactor * masking);
    }

    private static double ResolveSignature(
        AetheriaRuntimeEntitySnapshotCommit entity,
        AetheriaDaemonCombatKernelSettings settings)
    {
        var capacity = Math.Max(1.0, GetStat(entity, HeatCapacity));
        var heatRatio = GetStat(entity, Heat) / capacity;
        var speed = Math.Sqrt(entity.VelocityX * entity.VelocityX + entity.VelocityY * entity.VelocityY);
        return Math.Max(
            0.05,
            GetStat(entity, Signature) +
            heatRatio * settings.HeatSignatureScale +
            speed * settings.MovementSignatureScale);
    }

    private static double ResolveHitQuality(
        AetheriaRuntimeEntitySnapshotCommit attacker,
        AetheriaRuntimeEntitySnapshotCommit target,
        double confidence)
    {
        var fireControl = Math.Max(0.05, GetStat(attacker, FireControl));
        var cognition = Math.Max(0.05, EffectiveCognition(attacker));
        var masking = Math.Max(0.0, GetStat(target, SignatureMasking));
        var speed = Math.Sqrt(target.VelocityX * target.VelocityX + target.VelocityY * target.VelocityY);
        var targetDifficulty = 1.0 + masking * 0.35 + speed * 0.003;
        var quality = (0.45 + confidence * 0.75) * fireControl * (0.75 + cognition * 0.25) / targetDifficulty;
        return Clamp(quality, 0.08, 1.75);
    }

    private static double ResolveHull(
        AetheriaRuntimeEntitySnapshotCommit entity,
        AetheriaRuntimeCatalogItem? hullItem,
        AetheriaDaemonCombatKernelSettings settings)
    {
        if (hullItem?.HullArmor > 0)
            return hullItem.HullArmor;
        if (hullItem?.Durability > 0)
            return hullItem.Durability;
        return string.Equals(entity.Kind, "station", StringComparison.OrdinalIgnoreCase)
            ? settings.DefaultHull * 3.0
            : settings.DefaultHull;
    }

    private static double ResolveHeatCapacity(
        AetheriaRuntimeEntitySnapshotCommit entity,
        AetheriaRuntimeCatalogSnapshot? catalog,
        AetheriaRuntimeCatalogItem? hullItem,
        AetheriaDaemonCombatKernelSettings settings)
    {
        var heatCapacity = CapacityFromItem(hullItem);
        foreach (var slot in entity.Equipment ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            heatCapacity += CapacityFromItem(catalog?.FindItem(slot?.Item?.ItemKey ?? ""));
        return heatCapacity > 0 ? heatCapacity : settings.DefaultHeatCapacity;
    }

    private static double CapacityFromItem(AetheriaRuntimeCatalogItem? item)
    {
        if (item == null)
            return 0.0;
        return Math.Max(0.0, item.Mass) * Math.Max(0.0, item.SpecificHeat);
    }

    private static AetheriaRuntimeWeaponStateCommit EnsureKernelWeaponState(
        AetheriaRuntimeEntitySnapshotCommit entity,
        AetheriaDaemonCombatKernelSettings settings)
    {
        var states = (entity.WeaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>()).ToList();
        var state = states.FirstOrDefault(candidate =>
            candidate != null &&
            string.Equals(candidate.OwnerKind, KernelWeaponOwner, StringComparison.Ordinal) &&
            candidate.OwnerIndex == entity.EntityIndex);
        if (state != null)
            return state;

        state = new AetheriaRuntimeWeaponStateCommit
        {
            OwnerKind = KernelWeaponOwner,
            OwnerIndex = entity.EntityIndex,
            BehaviorIndex = 0,
            BehaviorKind = "AbstractCombatKernel",
            Ammo = -1,
            BurstInterval = settings.WeaponCooldownSeconds,
            LockTargetEntityIndex = -1
        };
        states.Add(state);
        entity.WeaponStates = states.ToArray();
        return state;
    }

    private static double EffectiveCognition(AetheriaRuntimeEntitySnapshotCommit entity)
    {
        return GetStat(entity, Cognition) / (1.0 + Math.Max(0.0, GetStat(entity, CognitiveLoad)));
    }

    private static double ContactConfidence(AetheriaRuntimeEntitySnapshotCommit observer, int targetEntityIndex)
    {
        return (observer.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
            .FirstOrDefault(contact => contact.TargetEntityIndex == targetEntityIndex)
            ?.InfoGathered ?? 0.0;
    }

    private static bool Hostile(
        AetheriaRuntimeEntitySnapshotCommit left,
        AetheriaRuntimeEntitySnapshotCommit right)
    {
        if (string.IsNullOrWhiteSpace(left.FactionKey) || string.IsNullOrWhiteSpace(right.FactionKey))
            return false;
        return !string.Equals(left.FactionKey, right.FactionKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAlive(AetheriaRuntimeEntitySnapshotCommit entity)
    {
        return entity.IsActive && GetStat(entity, Hull) > 0;
    }

    private static void ApplyDamage(AetheriaRuntimeEntitySnapshotCommit target, double damage)
    {
        var shield = GetStat(target, Shield);
        var shieldDamage = Math.Min(shield, damage);
        if (shieldDamage > 0)
        {
            SetStat(target, Shield, shield - shieldDamage);
            damage -= shieldDamage;
        }

        if (damage > 0)
            SetStat(target, Hull, Math.Max(0, GetStat(target, Hull) - damage));
    }

    private static void EnsureStat(AetheriaRuntimeEntitySnapshotCommit entity, string name, double value)
    {
        if (!HasStat(entity, name))
            SetStat(entity, name, value);
    }

    private static bool HasStat(AetheriaRuntimeEntitySnapshotCommit entity, string name)
    {
        return (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
            .Any(grid => string.Equals(grid?.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static double GetStat(AetheriaRuntimeEntitySnapshotCommit entity, string name)
    {
        var grid = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
            .FirstOrDefault(candidate => string.Equals(candidate?.Name, name, StringComparison.OrdinalIgnoreCase));
        if (grid?.Values == null || grid.Values.Count == 0)
            return 0.0;
        return grid.Values.Average();
    }

    private static void SetStat(AetheriaRuntimeEntitySnapshotCommit entity, string name, double value)
    {
        var grids = (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>()).ToList();
        var index = grids.FindIndex(candidate => string.Equals(candidate?.Name, name, StringComparison.OrdinalIgnoreCase));
        var grid = new AetheriaRuntimeEntityStatGridCommit
        {
            Name = name,
            Width = 1,
            Height = 1,
            Values = new[] { double.IsFinite(value) ? value : 0.0 }
        };
        if (index >= 0)
            grids[index] = grid;
        else
            grids.Add(grid);
        entity.StatGrids = grids;
    }

    private static double Distance(AetheriaRuntimeEntitySnapshotCommit left, AetheriaRuntimeEntitySnapshotCommit right)
    {
        var dx = right.PositionX - left.PositionX;
        var dy = right.PositionZ - left.PositionZ;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Clamp01(double value)
    {
        return Clamp(value, 0.0, 1.0);
    }

    private static double ClampNonNegative(double value)
    {
        return double.IsFinite(value) && value > 0 ? value : 0.0;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (!double.IsFinite(value))
            return min;
        if (value < min)
            return min;
        return value > max ? max : value;
    }
}
