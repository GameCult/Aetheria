public sealed record AetheriaDaemonRegularTopologySettings(
    int ZoneCount,
    float LinkDensity,
    int MegaCount,
    int BossCount)
{
    public static AetheriaDaemonRegularTopologySettings Fossil { get; } =
        new(196, 0.5f, 12, 3);
}

/// <summary>
/// Daemon-owned port of the fossil non-tutorial Galaxy constructor. It owns
/// regular-sector corporation selection, entrance/exit, boss chokepoints,
/// full-radius influence, discovery, and names. Zone contents consume this
/// graph and cannot revise it.
/// </summary>
public static class AetheriaDaemonRegularTopologyGenerator
{
    public static AetheriaDaemonTutorialTopology GenerateFossil(
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> availableFactions,
        uint seed,
        AetheriaDaemonRegularTopologySettings? settings = null)
    {
        settings ??= AetheriaDaemonRegularTopologySettings.Fossil;
        if (settings.ZoneCount < 3)
            throw new ArgumentOutOfRangeException(nameof(settings), "Regular topology requires at least three zones.");
        if (settings.MegaCount < 1)
            throw new ArgumentOutOfRangeException(nameof(settings), "Regular topology requires at least one corporation.");
        if (availableFactions == null || availableFactions.Count == 0)
            throw new InvalidDataException("Regular topology requires typed corporations.");

        var random = new CultMath.Random(seed);
        var factions = availableFactions
            .OrderBy(_ => random.NextFloat())
            .Take(settings.MegaCount)
            .ToArray();
        var points = AetheriaDaemonTutorialTopologyGenerator.GeneratePoints(
            settings.ZoneCount, ref random, MainCloudDensity);
        var links = AetheriaDaemonTutorialTopologyGenerator.DelaunayLinks(points);
        AetheriaDaemonTutorialTopologyGenerator.PruneLinks(
            points, links, settings.LinkDensity, MainCloudDensity);
        var distances = AetheriaDaemonTutorialTopologyGenerator.BuildDistances(points.Length, links);

        var exit = AetheriaDaemonTutorialTopologyGenerator.MaxZone(points.Length, zone =>
            Enumerable.Range(0, points.Length).Sum(other => distances[zone, other]));
        var entrance = AetheriaDaemonTutorialTopologyGenerator.MaxZone(points.Length, zone =>
            distances[exit, zone]);
        var bossZones = PlaceBosses(
            factions, settings.BossCount, entrance, exit, links, distances);
        var homes = PlaceHomes(factions, entrance, exit, bossZones, distances);

        var zoneFactions = new string[points.Length][];
        var owners = new string[points.Length];
        for (var zoneIndex = 0; zoneIndex < points.Length; zoneIndex++)
        {
            zoneFactions[zoneIndex] = factions
                .Where(faction => distances[zoneIndex, homes[faction.CorporationKey]] <= faction.InfluenceDistance)
                .Select(faction => faction.CorporationKey)
                .ToArray();
            owners[zoneIndex] = factions
                .Where(faction => distances[zoneIndex, homes[faction.CorporationKey]] <= faction.InfluenceDistance)
                .OrderBy(faction => distances[zoneIndex, homes[faction.CorporationKey]])
                .ThenBy(faction => Array.IndexOf(factions, faction))
                .Select(faction => faction.CorporationKey)
                .FirstOrDefault() ?? "";
        }

        var discovered = new[] { entrance }
            .Concat(AetheriaDaemonTutorialTopologyGenerator.Neighbors(entrance, links))
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
        var nameSeed = (uint)random.NextInt(1, int.MaxValue);
        var zoneNames = AetheriaDaemonTutorialTopologyGenerator.GenerateNames(
            factions, owners, nameSeed, order: 4);
        var zones = Enumerable.Range(0, points.Length)
            .Select(index => new AetheriaDaemonTutorialZoneTopology(
                index,
                zoneNames[index],
                points[index].X,
                points[index].Y,
                AetheriaDaemonTutorialTopologyGenerator.Neighbors(index, links).OrderBy(value => value).ToArray(),
                zoneFactions[index],
                owners[index]))
            .ToArray();
        return new AetheriaDaemonTutorialTopology(
            seed, 91.29439f, entrance, discovered, homes, zones)
        {
            ExitZoneIndex = exit,
            BossZoneByFactionKey = bossZones
        };
    }

    public static IReadOnlyList<AetheriaDaemonTutorialFactionInput> ResolveFossilFactions(
        GameCult.Aetheria.State.Verse.AetheriaRuntimeCatalogSnapshot catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        return (catalog.Corporations ?? Array.Empty<GameCult.Aetheria.State.Verse.AetheriaRuntimeCorporation>())
            .Where(corporation => corporation != null && !string.IsNullOrWhiteSpace(corporation.CorporationKey))
            .Select(corporation =>
            {
                var names = catalog.FindNameFile(corporation.GeonameFileKey);
                if (names == null || names.Names.Count == 0)
                    throw new InvalidDataException(
                        $"Regular-sector corporation '{corporation.Name}' has no typed geoname corpus.");
                return new AetheriaDaemonTutorialFactionInput(
                    corporation.CorporationKey,
                    corporation.Name ?? "",
                    corporation.ShortName ?? "",
                    corporation.InfluenceDistance)
                {
                    TrainingNames = names.Names,
                    BossHullItemKey = corporation.BossHullItemKey ?? ""
                };
            })
            .ToArray();
    }

    public static float MainCloudDensity(float x, float y)
    {
        var point = new CultMath.float2(x + 91.29439f, y + 91.29439f);
        var frequency = 1f;
        var amplitude = 0.5f;
        var sum = 0f;
        for (var octave = 0; octave < 10; octave++)
        {
            var noise = MathF.Abs(CultMath.math.simplex_noise(point * frequency));
            sum += (octave < 4 ? 1f - noise : noise) * amplitude;
            frequency *= 2f;
            amplitude *= 0.7f;
        }
        return MathF.Pow(sum + 0.3f, 10f) * 0.01f;
    }

    private static Dictionary<string, int> PlaceBosses(
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> factions,
        int bossCount,
        int entrance,
        int exit,
        IReadOnlySet<AetheriaDaemonTutorialTopologyGenerator.Edge> links,
        int[,] distances)
    {
        var path = ShortestPath(entrance, exit, links);
        var chokepoints = path
            .Where(zone => AetheriaDaemonTutorialTopologyGenerator.Neighbors(zone, links).Count() > 2)
            .Where(zone => !ConnectedWithoutZone(entrance, exit, zone, links, distances.GetLength(0)))
            .ToArray();
        var bossZones = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var faction in factions
            .Where(value => !string.IsNullOrWhiteSpace(value.BossHullItemKey))
            .Take(Math.Max(0, bossCount)))
        {
            var candidates = chokepoints.Length == 0 ? path : chokepoints;
            bossZones[faction.CorporationKey] = AetheriaDaemonTutorialTopologyGenerator.MaxZone(candidates, zone =>
            {
                var score = (double)distances[exit, zone] * distances[entrance, zone];
                foreach (var occupied in bossZones.Values)
                    score *= distances[occupied, zone];
                return score;
            });
        }
        return bossZones;
    }

    private static Dictionary<string, int> PlaceHomes(
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> factions,
        int entrance,
        int exit,
        IReadOnlyDictionary<string, int> bossZones,
        int[,] distances)
    {
        var homes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var faction in factions.Where(value => bossZones.ContainsKey(value.CorporationKey)))
        {
            var candidates = Enumerable.Range(0, distances.GetLength(0))
                .Where(zone => distances[zone, bossZones[faction.CorporationKey]] <= faction.InfluenceDistance);
            homes[faction.CorporationKey] = AetheriaDaemonTutorialTopologyGenerator.MaxZone(candidates, zone =>
            {
                var score = (double)AetheriaDaemonTutorialTopologyGenerator.RegionSize(
                    zone, faction.InfluenceDistance, distances);
                foreach (var occupied in homes.Values)
                    score *= Math.Sqrt(distances[occupied, zone]);
                return score;
            });
        }
        foreach (var faction in factions.Where(value => !homes.ContainsKey(value.CorporationKey)))
        {
            homes[faction.CorporationKey] = AetheriaDaemonTutorialTopologyGenerator.MaxZone(
                distances.GetLength(0), zone =>
                {
                    var score = Math.Pow(
                        AetheriaDaemonTutorialTopologyGenerator.RegionSize(
                            zone, faction.InfluenceDistance, distances), homes.Count);
                    score *= distances[exit, zone] * distances[entrance, zone];
                    foreach (var occupied in homes.Values)
                        score *= Math.Sqrt(distances[occupied, zone]);
                    foreach (var boss in bossZones.Values)
                        score *= Math.Sqrt(distances[boss, zone]);
                    return score;
                });
        }
        return homes;
    }

    private static int[] ShortestPath(
        int source,
        int target,
        IReadOnlySet<AetheriaDaemonTutorialTopologyGenerator.Edge> links)
    {
        var queue = new Queue<int>();
        var parents = new Dictionary<int, int>();
        queue.Enqueue(source);
        parents[source] = -1;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target) break;
            foreach (var neighbor in AetheriaDaemonTutorialTopologyGenerator.Neighbors(current, links).OrderBy(value => value))
            {
                if (parents.ContainsKey(neighbor)) continue;
                parents[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }
        var path = new List<int>();
        for (var cursor = target; cursor >= 0; cursor = parents[cursor])
            path.Add(cursor);
        path.Reverse();
        return path.ToArray();
    }

    private static bool ConnectedWithoutZone(
        int source,
        int target,
        int ignored,
        IReadOnlySet<AetheriaDaemonTutorialTopologyGenerator.Edge> links,
        int zoneCount)
    {
        if (source == ignored || target == ignored) return false;
        var queue = new Queue<int>();
        var visited = new bool[zoneCount];
        queue.Enqueue(source);
        visited[source] = true;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in AetheriaDaemonTutorialTopologyGenerator.Neighbors(current, links))
            {
                if (neighbor == ignored || visited[neighbor]) continue;
                if (neighbor == target) return true;
                visited[neighbor] = true;
                queue.Enqueue(neighbor);
            }
        }
        return false;
    }
}
