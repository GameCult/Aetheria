using Aetheria.State;
using Aetheria.State.Documents;
using GameCult.Aetheria.State.Verse;
using GameCult.Caching;

internal static class AetheriaDaemonZoneGenerator
{
    public const string RunId = "local-terminus";
    public const uint GenerationSeed = 0xA37E_2026u;

    public static async Task WritePlayableRunAsync(
        AetheriaStateNode node,
        AetheriaRuntimeCatalogSnapshot catalog,
        string now)
    {
        var runKey = new CultRecordKey($"global:aetheria.run_state.{RunId}.v1");
        var zoneKey = new CultRecordKey($"global:aetheria.zone_state.{RunId}.0.v1");
        var corporationKeys = (catalog.Corporations ?? Array.Empty<AetheriaRuntimeCorporation>())
            .Select(value => value.CorporationKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (corporationKeys.Length == 0)
        {
            throw new InvalidDataException(
                "Terminus generation requires at least one typed corporation with a stable corporation key.");
        }
        var homeZones = corporationKeys.ToDictionary(value => value, _ => 0, StringComparer.Ordinal);
        var availabilityFactions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["player"] = corporationKeys.ElementAtOrDefault(0) ?? "",
            ["raider"] = corporationKeys.ElementAtOrDefault(1) ?? corporationKeys.ElementAtOrDefault(0) ?? "",
            ["neutral"] = corporationKeys.ElementAtOrDefault(2) ?? corporationKeys.ElementAtOrDefault(0) ?? ""
        };
        var rootRandom = new CultMath.Random(GenerationSeed);
        var adjacency = new Dictionary<int, IReadOnlyList<int>> { [0] = Array.Empty<int>() };
        var loadouts = availabilityFactions.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToDictionary(
                value => value,
                _ => new AetheriaDaemonLoadoutGenerator(
                    catalog,
                    (uint)rootRandom.NextInt(1, int.MaxValue),
                    0,
                    homeZones,
                    adjacency),
                StringComparer.Ordinal);
        var entities = GenerateEntities(loadouts, availabilityFactions);
        var entityKeys = Enumerable.Range(0, entities.Length)
            .Select(index => EntityKey(0, index))
            .ToArray();

        var settings = await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
            .ReadAsync()
            .ConfigureAwait(false) ?? new AetheriaPlayerSettings();
        settings.ActiveRunKey = runKey.ToString();
        settings.PlayerName = string.IsNullOrWhiteSpace(settings.PlayerName) ? "Terminus Pilot" : settings.PlayerName;
        settings.TutorialPassed = true;
        settings.LastUpdatedAtUtc = now;
        await node.MutableDocument<AetheriaPlayerSettings>(AetheriaStateNode.PlayerSettingsKey)
            .ReplaceAsync(settings)
            .ConfigureAwait(false);

        await node.MutableDocument<AetheriaRunState>(runKey).ReplaceAsync(new AetheriaRunState
        {
            RunId = RunId,
            EntranceZoneIndex = 0,
            ExitZoneIndex = 0,
            CurrentZoneIndex = 0,
            DiscoveredZoneIndices = [0],
            ZoneKeys = [zoneKey.ToString()],
            GenerationSeed = GenerationSeed,
            CurrentEntityKey = entityKeys[1],
            UpdatedAtUtc = now
        }).ConfigureAwait(false);

        await node.MutableDocument<AetheriaZoneState>(zoneKey).ReplaceAsync(new AetheriaZoneState
        {
            Name = "Daemon Generated Terminus",
            Position = Vec2(0, 0),
            EntityKeys = entityKeys,
            FactionIndices = [0, 1, 2],
            OwnerFactionIndex = 0,
            Orbits = GenerateOrbits(),
            Bodies = GenerateBodies(),
            GravityTerrainRadius = 1900,
            GravityTerrainDepth = 7,
            GravityTerrainDepthExponent = 1.18,
            GravityTerrainBoundaryFog = 0.25,
            GravityTerrainWaveFrequency = 0.55,
            NextPickupIndex = 0,
            DroppedPickups = Array.Empty<AetheriaDroppedPickupSnapshot>()
        }).ConfigureAwait(false);

        for (var i = 0; i < entities.Length; i++)
        {
            await node.MutableDocument<AetheriaEntitySnapshot>(new CultRecordKey(entityKeys[i]))
                .ReplaceAsync(entities[i])
                .ConfigureAwait(false);
        }

        await node.FlushAsync().ConfigureAwait(false);
    }

    public static string EntityKey(int zoneIndex, int entityIndex)
    {
        return $"global:aetheria.run_state.{RunId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
    }

    private static AetheriaOrbitSnapshot[] GenerateOrbits()
    {
        var bodies = BodyPlans();
        return bodies.Select(body => new AetheriaOrbitSnapshot
        {
            OrbitKey = $"{body.Key}.orbit",
            ParentOrbitKey = body.ParentKey.Length == 0 ? "" : $"{body.ParentKey}.orbit",
            Distance = body.Distance,
            Phase = body.Phase,
            FixedPosition = Vec2(body.X, body.Z)
        }).ToArray();
    }

    private static AetheriaBodySnapshot[] GenerateBodies()
    {
        return BodyPlans().Select(body => new AetheriaBodySnapshot
        {
            BodyKey = body.Key,
            Kind = body.Kind,
            Name = body.Name,
            OrbitKey = $"{body.Key}.orbit",
            Mass = body.Mass,
            BodyRadiusMultiplier = body.BodyRadius,
            GravityRadiusMultiplier = 1,
            GravityDepthMultiplier = 1,
            GravityDepthExponent = body.DepthExponent,
            GravityInfluenceCenterX = body.X,
            GravityInfluenceCenterZ = body.Z,
            GravityInfluenceRadius = body.GravityRadius,
            GravityWellDepth = body.GravityDepth,
            GravityWaveRadius = body.WaveRadius,
            GravityWaveDepth = body.WaveDepth,
            GravityWaveSpeed = body.WaveSpeed,
            Asteroids = body.Kind == "asteroid_belt" ? Asteroids(body.Distance) : [],
            GasGiantVisual = GasGiantVisual(body),
            SunVisual = SunVisual(body)
        }).ToArray();
    }

    private static BodyPlan[] BodyPlans()
    {
        return
        [
            Body("local.sun", "", "sun", "Terminus", 0, 0, 1600, 2.8, 980, 92, 3.0, 480, 11, 1.8),
            Body("local.inner", "local.sun", "planet", "Cairn", 250, 0.45, 110, 0.78, 230, 20, 2.2, 92, 3, 1.0),
            Body("local.blue", "local.sun", "planet", "Blueglass", 440, 2.45, 260, 1.18, 360, 36, 2.55, 150, 5, 1.1),
            Body("local.blue.moon", "local.blue", "moon", "Latch", 520, 2.78, 70, 0.58, 170, 15, 2.8, 70, 2.2, 1.7),
            Body("local.ember", "local.sun", "planet", "Emberhook", 640, 3.78, 220, 1.05, 330, 31, 2.25, 128, 4.2, 1.3),
            Body("local.ember.moon", "local.ember", "moon", "Ashwake", 735, 3.45, 86, 0.62, 185, 16, 2.7, 72, 2.4, 1.45),
            Body("local.belt", "local.sun", "asteroid_belt", "Cinder Belt", 760, -0.28, 140, 1.0, 310, 24, 2.0, 122, 4, 0.8),
            Body("local.green", "local.sun", "planet", "Marrow", 930, 1.28, 180, 0.96, 300, 26, 2.35, 112, 3.6, 1.0),
            Body("local.outer", "local.sun", "gas_giant", "Vesper", 1120, 0.58, 420, 1.55, 500, 48, 2.4, 230, 7, 0.72),
            Body("local.outer.moon.a", "local.outer", "moon", "Iris", 1240, 0.88, 92, 0.66, 195, 17, 2.75, 78, 2.5, 1.2),
            Body("local.outer.moon.b", "local.outer", "moon", "Hush", 1025, 0.28, 76, 0.55, 180, 15, 2.9, 70, 2.1, 1.5),
            Body("local.deep", "local.sun", "gas_giant", "Nacre", 1420, 5.72, 340, 1.36, 440, 41, 2.55, 200, 5.8, 0.95)
        ];
    }

    private static BodyPlan Body(
        string key,
        string parentKey,
        string kind,
        string name,
        double distance,
        double phase,
        double mass,
        double bodyRadius,
        double gravityRadius,
        double gravityDepth,
        double depthExponent,
        double waveRadius,
        double waveDepth,
        double waveSpeed)
    {
        if (gravityDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(gravityDepth), "Gravity depth is an unsigned well magnitude; Ymir positive radial strength attracts.");

        return new BodyPlan(
            key,
            parentKey,
            kind,
            name,
            distance,
            phase,
            Math.Cos(phase) * distance,
            Math.Sin(phase) * distance,
            mass,
            bodyRadius,
            gravityRadius,
            gravityDepth,
            depthExponent,
            waveRadius,
            waveDepth,
            waveSpeed);
    }

    private static AetheriaEntitySnapshot[] GenerateEntities(
        IReadOnlyDictionary<string, AetheriaDaemonLoadoutGenerator> loadouts,
        IReadOnlyDictionary<string, string> availabilityFactions)
    {
        var keys = Enumerable.Range(0, 12)
            .Select(index => EntityKey(0, index))
            .ToArray();

        var entities = new[]
        {
            Entity(loadouts, availabilityFactions, "Anchor Station", "station", -50, -30, 0, 0, "player", 760, keys[6], [keys[1], keys[2], keys[3], keys[4], keys[6], keys[7], keys[11]]),
            Entity(loadouts, availabilityFactions, "Vanguard One", "ship", -40, -30, 0, 0, "player", 540, keys[6], [keys[0], keys[2], keys[4], keys[6], keys[7]]),
            Entity(loadouts, availabilityFactions, "Wing Two", "ship", 145, 125, -5, 7, "player", 450, keys[6], [keys[0], keys[1], keys[6], keys[8]]),
            Entity(loadouts, availabilityFactions, "Torch Three", "ship", -235, 210, 8, -4, "player", 500, keys[7], [keys[0], keys[1], keys[7]]),
            Entity(loadouts, availabilityFactions, "Foundry Tug", "ship", 330, -155, -2, 3, "player", 390, keys[6], [keys[0], keys[1], keys[6], keys[10]]),
            Entity(loadouts, availabilityFactions, "Derelict Relay", "station", -390, 270, 0, 0, "neutral", 160, "", [keys[0], keys[3]]),
            Entity(loadouts, availabilityFactions, "Ash Raider", "ship", 20, -30, -5, -2, "raider", 320, "", [keys[0], keys[1], keys[2], keys[4]]),
            Entity(loadouts, availabilityFactions, "Cinder Knife", "ship", 585, -115, -7, 1, "raider", 280, "", [keys[0], keys[3]]),
            Entity(loadouts, availabilityFactions, "Blackwake", "ship", 690, 300, -4, -3, "raider", 230, "", [keys[2]]),
            Entity(loadouts, availabilityFactions, "Vesper Sloop", "ship", 960, 620, -3, -5, "raider", 250, "", [keys[4]]),
            Entity(loadouts, availabilityFactions, "Survey Skiff", "ship", -610, -365, 3, 5, "neutral", 210, "", [keys[4], keys[5]]),
            Entity(loadouts, availabilityFactions, "Lagrange Beacon", "station", 105, 510, 0, 0, "neutral", 190, "", [keys[0], keys[1], keys[6]])
        };
        foreach (var index in new[] { 1, 2, 3, 4 })
            entities[index].HomeEntityKey = keys[0];
        entities[1].CargoContents =
        [
            new AetheriaCargoBayLoadout
            {
                Items = Array.Empty<AetheriaLoadoutItemSlot>()
            }
        ];
        entities[6].StatGrids =
        [
            StatGrid("hull", 1),
            StatGrid("shield", 0),
            StatGrid("heat", 0)
        ];
        var salvage = entities[6].Equipment.FirstOrDefault()
            ?? throw new InvalidOperationException("The starter raider loadout produced no canonical salvage item.");
        var salvageSelection = entities[6].LoadoutGeneration.Selections.First(selection =>
            string.Equals(selection.Role, "equipment", StringComparison.Ordinal) &&
            string.Equals(selection.ItemKey, salvage.ItemKey, StringComparison.Ordinal));
        entities[6].CargoContents =
        [
            new AetheriaCargoBayLoadout
            {
                Items =
                [
                    new AetheriaLoadoutItemSlot
                    {
                        Item = new AetheriaLoadoutItem
                        {
                            ItemKey = salvage.ItemKey,
                            Quality = salvage.Quality,
                            Durability = salvage.Durability,
                            Quantity = 1,
                            Enabled = true
                        }
                    }
                ]
            }
        ];
        entities[6].Equipment = Array.Empty<AetheriaEntityItemSlot>();
        entities[6].WeaponGroups = Array.Empty<AetheriaWeaponGroupSnapshot>();
        entities[6].LoadoutGeneration.Selections = entities[6].LoadoutGeneration.Selections
            .Where(selection => string.Equals(selection.Role, "hull", StringComparison.Ordinal))
            .Append(new AetheriaLoadoutGenerationSelection
            {
                Role = "cargo",
                ItemKey = salvageSelection.ItemKey,
                ManufacturerKey = salvageSelection.ManufacturerKey,
                Price = salvageSelection.Price,
                ManufacturerDistance = salvageSelection.ManufacturerDistance,
                Allegiance = salvageSelection.Allegiance
            })
            .ToArray();
        entities[2].AgentTaskCapabilities = ["attack", "defend", "explore"];
        entities[3].AgentTaskCapabilities = ["attack", "defend"];
        entities[4].AgentTaskCapabilities = ["mine", "haul", "tow", "explore"];
        return entities;
    }

    private static AetheriaEntitySnapshot Entity(
        IReadOnlyDictionary<string, AetheriaDaemonLoadoutGenerator> loadouts,
        IReadOnlyDictionary<string, string> availabilityFactions,
        string name,
        string kind,
        double x,
        double z,
        double vx,
        double vy,
        string faction,
        double visibility,
        string target,
        string[] contactKeys)
    {
        var availabilityFaction = availabilityFactions[faction];
        var loadout = loadouts[availabilityFaction].Build(kind, availabilityFaction);
        return new AetheriaEntitySnapshot
        {
            Name = name,
            Kind = kind,
            Position = Vec3(x, 0, z),
            Direction = Vec2(vx == 0 && vy == 0 ? 0 : vx, vx == 0 && vy == 0 ? 1 : vy),
            LookDirection = Vec2(vx == 0 && vy == 0 ? 0 : vx, vx == 0 && vy == 0 ? 1 : vy),
            Velocity = Vec2(vx, vy),
            FactionKey = faction,
            HullItemKey = loadout.HullItemKey,
            LoadoutGeneration = loadout.Receipt,
            IsActive = true,
            HeatsinksEnabled = true,
            TractorPower = 0,
            Visibility = visibility,
            VisibilitySourceCount = 1,
            TargetEntityKey = target,
            Contacts = contactKeys.Select(key => new AetheriaEntityContactSnapshot
            {
                TargetEntityKey = key,
                InfoGathered = 1,
                Hostile = string.Equals(faction, "player", StringComparison.OrdinalIgnoreCase) && key.Contains(".entity.6.", StringComparison.Ordinal)
            }).ToArray(),
            StatGrids =
            [
                StatGrid("hull", string.Equals(kind, "station", StringComparison.OrdinalIgnoreCase) ? 420 : string.Equals(faction, "raider", StringComparison.OrdinalIgnoreCase) ? 85 : 130),
                StatGrid("shield", string.Equals(kind, "station", StringComparison.OrdinalIgnoreCase) ? 130 : 50),
                StatGrid("heat", 0)
            ],
            Equipment = loadout.Equipment,
            WeaponGroups = loadout.WeaponGroups
                .Select(indices => new AetheriaWeaponGroupSnapshot { EquipmentIndices = indices })
                .ToArray(),
            CargoContents =
            [
                new AetheriaCargoBayLoadout
                {
                    Items = loadout.Cargo
                }
            ]
        };
    }

    private static AetheriaGasGiantVisualState GasGiantVisual(BodyPlan body)
    {
        if (body.Kind != "gas_giant" && body.Kind != "sun")
            return new AetheriaGasGiantVisualState();

        return new AetheriaGasGiantVisualState
        {
            FirstOffsetDomainRotationSpeed = body.Kind == "sun" ? 5 : 0,
            FirstOffsetRotationSpeed = 5,
            SecondOffsetRotationSpeed = 10,
            SecondOffsetDomainRotationSpeed = -25,
            AlbedoRotationSpeed = -3,
            WaveRadiusMultiplier = 1,
            WaveDepthMultiplier = 1,
            Colors =
            [
                Color(0.85, 0.32, 0.18, 0),
                Color(0.18, 0.54, 0.92, 0.5),
                Color(0.92, 0.78, 0.38, 1)
            ]
        };
    }

    private static AetheriaSunVisualState SunVisual(BodyPlan body)
    {
        return body.Kind == "sun"
            ? new AetheriaSunVisualState
            {
                LightColor = Vec3(1, 0.86, 0.58),
                FogTintColor = Vec3(0.32, 0.52, 1),
                LightRadiusMultiplier = 1.8
            }
            : new AetheriaSunVisualState();
    }

    private static AetheriaAsteroidSnapshot[] Asteroids(double orbitDistance)
    {
        return Enumerable.Range(0, 18)
            .Select(index =>
            {
                var phase = index * Math.Tau / 18.0;
                return new AetheriaAsteroidSnapshot
                {
                    Distance = orbitDistance + Math.Sin(phase * 3.0) * 42.0,
                    Phase = phase,
                    Size = 0.65 + (index % 5) * 0.24,
                    RotationSpeed = (index % 2 == 0 ? 1 : -1) * (0.04 + index * 0.006)
                };
            })
            .ToArray();
    }

    private static AetheriaEntityStatGrid StatGrid(string name, double value)
    {
        return new AetheriaEntityStatGrid
        {
            Name = name,
            Width = 1,
            Height = 1,
            Values = [value]
        };
    }

    private static AetheriaColor Color(double x, double y, double z, double w)
    {
        return new AetheriaColor { X = x, Y = y, Z = z, W = w };
    }

    private static AetheriaVector2 Vec2(double x, double y)
    {
        return new AetheriaVector2 { X = x, Y = y };
    }

    private static AetheriaVector3 Vec3(double x, double y, double z)
    {
        return new AetheriaVector3 { X = x, Y = y, Z = z };
    }

    private sealed record BodyPlan(
        string Key,
        string ParentKey,
        string Kind,
        string Name,
        double Distance,
        double Phase,
        double X,
        double Z,
        double Mass,
        double BodyRadius,
        double GravityRadius,
        double GravityDepth,
        double DepthExponent,
        double WaveRadius,
        double WaveDepth,
        double WaveSpeed);

}
