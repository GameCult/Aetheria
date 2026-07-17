using GameCult.Aetheria.State.Verse;

public sealed record AetheriaDaemonTutorialFactionInput(
    string CorporationKey,
    string Name,
    string ShortName,
    int InfluenceDistance)
{
    public IReadOnlyList<string> TrainingNames { get; init; } = Array.Empty<string>();
}

public sealed record AetheriaDaemonTutorialTopologySettings(
    int ZoneCount,
    float LinkDensity,
    string ProtagonistFaction,
    string AntagonistFaction,
    string BufferFaction,
    IReadOnlyList<string> NeutralFactions,
    string QuestFaction)
{
    public static AetheriaDaemonTutorialTopologySettings Fossil { get; } = new(
        64,
        0.5f,
        "Miss",
        "Zhe",
        "Luc",
        new[] { "Aero", "Finch" },
        "Adras");
}

public sealed record AetheriaDaemonTutorialZoneTopology(
    int ZoneIndex,
    string Name,
    float X,
    float Y,
    IReadOnlyList<int> AdjacentZoneIndices,
    IReadOnlyList<string> FactionKeys,
    string OwnerFactionKey);

public sealed record AetheriaDaemonTutorialTopology(
    uint GenerationSeed,
    float NoisePosition,
    int EntranceZoneIndex,
    IReadOnlyList<int> DiscoveredZoneIndices,
    IReadOnlyDictionary<string, int> HomeZoneByFactionKey,
    IReadOnlyList<AetheriaDaemonTutorialZoneTopology> Zones);

/// <summary>
/// Daemon-owned migration of the fossil tutorial galaxy topology. This organ
/// deliberately stops before zone contents: materialization consumes this
/// result, but cannot change its graph, faction placement, or discovery truth.
/// </summary>
public static class AetheriaDaemonTutorialTopologyGenerator
{
    private const int CandidateMultiplier = 8;

    public static AetheriaDaemonTutorialTopology GenerateFossil(
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> factions,
        uint seed,
        AetheriaDaemonTutorialTopologySettings? settings = null)
    {
        var noiseRandom = new CultMath.Random(seed ^ 0x74A7_91D3u);
        float noisePosition;
        var attempts = 0;
        do
        {
            if (++attempts > 1_000_000)
                throw new InvalidOperationException("Tutorial cloud field did not produce an admissible entrance region.");
            noisePosition = noiseRandom.NextFloat(0, 1000);
        } while (TutorialCloudDensity(0.5f, 0.5f, noisePosition) < 0.5f);

        return Generate(
            factions,
            seed,
            (x, y) => TutorialCloudDensity(x, y, noisePosition),
            settings) with
        {
            NoisePosition = noisePosition
        };
    }

    public static float TutorialCloudDensity(float x, float y, float noisePosition)
    {
        var point = new CultMath.float2(x + noisePosition, y + noisePosition);
        var frequency = 0.1f;
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

    public static IReadOnlyList<AetheriaDaemonTutorialFactionInput> ResolveFossilFactions(
        AetheriaRuntimeCatalogSnapshot catalog,
        AetheriaDaemonTutorialTopologySettings? settings = null)
    {
        settings ??= AetheriaDaemonTutorialTopologySettings.Fossil;
        var requested = new[]
            {
                settings.ProtagonistFaction,
                settings.AntagonistFaction,
                settings.BufferFaction
            }
            .Concat(settings.NeutralFactions)
            .Append(settings.QuestFaction)
            .ToArray();

        return requested.Select(name =>
        {
            var match = (catalog.Corporations ?? Array.Empty<AetheriaRuntimeCorporation>())
                .FirstOrDefault(value =>
                    string.Equals(value.ShortName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value.CorporationKey, name, StringComparison.OrdinalIgnoreCase));
            if (match == null || string.IsNullOrWhiteSpace(match.CorporationKey))
                throw new InvalidDataException($"Tutorial generation requires the authored faction '{name}'.");
            var nameFile = catalog.FindNameFile(match.GeonameFileKey);
            if (nameFile == null || nameFile.Names.Count == 0)
                throw new InvalidDataException($"Tutorial faction '{name}' has no typed geoname corpus.");
            return new AetheriaDaemonTutorialFactionInput(
                match.CorporationKey,
                match.Name ?? "",
                match.ShortName ?? "",
                match.InfluenceDistance)
            {
                TrainingNames = nameFile.Names
            };
        }).ToArray();
    }

    public static AetheriaDaemonTutorialTopology Generate(
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> factions,
        uint seed,
        Func<float, float, float> cloudDensity,
        AetheriaDaemonTutorialTopologySettings? settings = null)
    {
        settings ??= AetheriaDaemonTutorialTopologySettings.Fossil;
        if (settings.ZoneCount < 3)
            throw new ArgumentOutOfRangeException(nameof(settings), "Tutorial topology requires at least three zones.");
        if (cloudDensity == null)
            throw new ArgumentNullException(nameof(cloudDensity));

        var roles = ResolveRoles(factions, settings);
        var random = new CultMath.Random(seed);
        var points = GeneratePoints(settings.ZoneCount, ref random, cloudDensity);
        var links = DelaunayLinks(points);
        PruneLinks(points, links, settings.LinkDensity, cloudDensity);
        var distances = BuildDistances(points.Length, links);

        var home = PlaceFactions(points.Length, links, distances, roles);
        var influence = roles.All.ToDictionary(
            faction => faction.CorporationKey,
            faction => Math.Max(0, (faction.InfluenceDistance + 1) / 2),
            StringComparer.Ordinal);
        var zoneFactions = new string[points.Length][];
        var owners = new string[points.Length];
        for (var zoneIndex = 0; zoneIndex < points.Length; zoneIndex++)
        {
            zoneFactions[zoneIndex] = roles.All
                .Where(faction => distances[zoneIndex, home[faction.CorporationKey]] <= influence[faction.CorporationKey])
                .Select(faction => faction.CorporationKey)
                .ToArray();
            owners[zoneIndex] = roles.All
                .Where(faction => distances[zoneIndex, home[faction.CorporationKey]] <= influence[faction.CorporationKey])
                .OrderBy(faction => distances[zoneIndex, home[faction.CorporationKey]])
                .ThenBy(faction => Array.IndexOf(roles.All, faction))
                .Select(faction => faction.CorporationKey)
                .FirstOrDefault() ?? "";
        }

        var entranceCandidates = Enumerable.Range(0, points.Length)
            .Where(index => owners[index].Length == 0)
            .ToArray();
        if (entranceCandidates.Length == 0)
            throw new InvalidOperationException("Tutorial topology produced no unowned entrance zone.");
        var entrance = entranceCandidates
            .OrderBy(index => distances[index, home[roles.Protagonist.CorporationKey]])
            .ThenBy(index => index)
            .First();
        var discovered = new[] { entrance }
            .Concat(Neighbors(entrance, links))
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        var nameSeed = (uint)random.NextInt(1, int.MaxValue);
        var zoneNames = GenerateNames(roles.All, owners, nameSeed);
        var zones = Enumerable.Range(0, points.Length)
            .Select(index => new AetheriaDaemonTutorialZoneTopology(
                index,
                zoneNames[index],
                points[index].X,
                points[index].Y,
                Neighbors(index, links).OrderBy(value => value).ToArray(),
                zoneFactions[index],
                owners[index]))
            .ToArray();
        return new AetheriaDaemonTutorialTopology(seed, float.NaN, entrance, discovered, home, zones);
    }

    private static TutorialRoles ResolveRoles(
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> factions,
        AetheriaDaemonTutorialTopologySettings settings)
    {
        AetheriaDaemonTutorialFactionInput Resolve(string name)
        {
            return factions.FirstOrDefault(value =>
                       string.Equals(value.ShortName, name, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(value.CorporationKey, name, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidDataException($"Tutorial topology is missing faction '{name}'.");
        }

        var protagonist = Resolve(settings.ProtagonistFaction);
        var antagonist = Resolve(settings.AntagonistFaction);
        var buffer = Resolve(settings.BufferFaction);
        var neutrals = settings.NeutralFactions.Select(Resolve).ToArray();
        var quest = Resolve(settings.QuestFaction);
        var all = new[] { protagonist, antagonist, buffer }
            .Concat(neutrals)
            .Append(quest)
            .DistinctBy(value => value.CorporationKey, StringComparer.Ordinal)
            .ToArray();
        if (all.Length != 4 + neutrals.Length)
            throw new InvalidDataException("Tutorial faction roles must resolve to distinct corporations.");
        return new TutorialRoles(protagonist, antagonist, buffer, neutrals, quest, all);
    }

    private static string[] GenerateNames(
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> factions,
        IReadOnlyList<string> ownerKeys,
        uint seed)
    {
        var generators = new Dictionary<string, TutorialMarkovNameGenerator>(StringComparer.Ordinal);
        foreach (var faction in factions)
        {
            if (faction.TrainingNames.Count == 0)
                throw new InvalidDataException($"Tutorial faction '{faction.ShortName}' has no geoname training rows.");
            generators[faction.CorporationKey] = new TutorialMarkovNameGenerator(
                seed,
                faction.TrainingNames,
                order: 3,
                minimumLength: 5,
                maximumLength: 10);
        }

        var catalogRandom = new CultMath.Random(seed);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var output = new string[ownerKeys.Count];
        for (var zoneIndex = 0; zoneIndex < ownerKeys.Count; zoneIndex++)
        {
            var owner = ownerKeys[zoneIndex];
            if (owner.Length == 0)
            {
                string candidate;
                do candidate = $"EAC-{catalogRandom.NextInt(9999)}";
                while (!used.Add(candidate));
                output[zoneIndex] = candidate;
                continue;
            }

            output[zoneIndex] = generators[owner].Next(used);
        }
        return output;
    }

    private static Point[] GeneratePoints(
        int count,
        ref CultMath.Random random,
        Func<float, float, float> density)
    {
        var candidates = new Point[count * CandidateMultiplier];
        var accepted = 0;
        var accumulator = 0f;
        var attempts = 0;
        while (accepted < candidates.Length)
        {
            if (++attempts > candidates.Length * 1_000_000)
                throw new InvalidOperationException("Tutorial density did not admit enough topology samples.");
            var point = new Point(random.NextFloat(), random.NextFloat());
            var dx = point.X - 0.5f;
            var dy = point.Y - 0.5f;
            var envelope = Saturate((0.2f - (dx * dx + dy * dy)) * 4f);
            var sampleDensity = Saturate(density(point.X, point.Y));
            accumulator += sampleDensity * sampleDensity * envelope;
            if (accumulator <= 0.5f)
                continue;
            accumulator = 0;
            candidates[accepted++] = point;
        }

        var dMax = 2f * MathF.Sqrt((1f / count) / (2f * MathF.Sqrt(3f)));
        var dMaxSquared = dMax * dMax;
        var weights = new float[candidates.Length];
        var densities = candidates.Select(point => density(point.X, point.Y)).ToArray();
        var active = Enumerable.Repeat(true, candidates.Length).ToArray();
        for (var i = 0; i < candidates.Length; i++)
        for (var j = 0; j < candidates.Length; j++)
        {
            var distanceSquared = DistanceSquared(candidates[i], candidates[j]);
            if (distanceSquared > dMaxSquared)
                continue;
            weights[i] += Weight(distanceSquared, dMax, densities[i]);
        }

        for (var remaining = candidates.Length; remaining > count; remaining--)
        {
            var eliminated = Enumerable.Range(0, candidates.Length)
                .Where(index => active[index])
                .OrderByDescending(index => weights[index])
                .ThenBy(index => index)
                .First();
            active[eliminated] = false;
            for (var neighbor = 0; neighbor < candidates.Length; neighbor++)
            {
                if (!active[neighbor])
                    continue;
                var distanceSquared = DistanceSquared(candidates[neighbor], candidates[eliminated]);
                if (distanceSquared <= dMaxSquared)
                    weights[neighbor] -= Weight(distanceSquared, dMax, densities[neighbor]);
            }
        }

        return Enumerable.Range(0, candidates.Length)
            .Where(index => active[index])
            .Select(index => candidates[index])
            .ToArray();
    }

    private static float Weight(float distanceSquared, float dMax, float density)
    {
        var distance = MathF.Sqrt(distanceSquared);
        return MathF.Pow(1f - distance / dMax, 5f + 6f * density);
    }

    private static HashSet<Edge> DelaunayLinks(IReadOnlyList<Point> points)
    {
        var vertices = points.Concat(new[]
        {
            new Point(-16, -8),
            new Point(16, -8),
            new Point(0, 16)
        }).ToArray();
        var superA = points.Count;
        var superB = points.Count + 1;
        var superC = points.Count + 2;
        var triangles = new List<Triangle> { Orient(vertices, new Triangle(superA, superB, superC)) };

        for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            var bad = triangles.Where(triangle => ContainsInCircumcircle(vertices, triangle, vertices[pointIndex])).ToArray();
            var boundary = bad
                .SelectMany(TriangleEdges)
                .GroupBy(edge => edge)
                .Where(group => group.Count() == 1)
                .Select(group => group.Key)
                .ToArray();
            foreach (var triangle in bad)
                triangles.Remove(triangle);
            triangles.AddRange(boundary.Select(edge => Orient(vertices, new Triangle(edge.A, edge.B, pointIndex))));
        }

        return triangles
            .Where(triangle => triangle.A < points.Count && triangle.B < points.Count && triangle.C < points.Count)
            .SelectMany(TriangleEdges)
            .ToHashSet();
    }

    private static Triangle Orient(IReadOnlyList<Point> points, Triangle triangle)
    {
        return Cross(points[triangle.A], points[triangle.B], points[triangle.C]) >= 0
            ? triangle
            : new Triangle(triangle.B, triangle.A, triangle.C);
    }

    private static bool ContainsInCircumcircle(IReadOnlyList<Point> points, Triangle triangle, Point point)
    {
        var a = points[triangle.A];
        var b = points[triangle.B];
        var c = points[triangle.C];
        var ax = a.X - point.X;
        var ay = a.Y - point.Y;
        var bx = b.X - point.X;
        var by = b.Y - point.Y;
        var cx = c.X - point.X;
        var cy = c.Y - point.Y;
        var determinant = (ax * ax + ay * ay) * (bx * cy - cx * by)
                        - (bx * bx + by * by) * (ax * cy - cx * ay)
                        + (cx * cx + cy * cy) * (ax * by - bx * ay);
        return determinant > 1e-7f;
    }

    private static IEnumerable<Edge> TriangleEdges(Triangle triangle)
    {
        yield return new Edge(triangle.A, triangle.B);
        yield return new Edge(triangle.B, triangle.C);
        yield return new Edge(triangle.C, triangle.A);
    }

    private static void PruneLinks(
        IReadOnlyList<Point> points,
        HashSet<Edge> links,
        float linkDensity,
        Func<float, float, float> density)
    {
        var target = (int)MathF.Ceiling(Saturate(linkDensity) * links.Count);
        while (links.Count > target)
        {
            var degrees = Enumerable.Range(0, points.Count)
                .Select(index => links.Count(edge => edge.Contains(index)))
                .ToArray();
            var removable = links
                .Where(edge => IsConnectedWithout(points.Count, links, edge.A, edge.B, edge))
                .Select(edge => new { Edge = edge, Weight = LinkWeight(points, edge, degrees, density) })
                .OrderByDescending(value => value.Weight)
                .ThenBy(value => value.Edge.A)
                .ThenBy(value => value.Edge.B)
                .FirstOrDefault();
            if (removable == null)
                break;
            links.Remove(removable.Edge);
        }
    }

    private static float LinkWeight(
        IReadOnlyList<Point> points,
        Edge edge,
        IReadOnlyList<int> degrees,
        Func<float, float, float> density)
    {
        var a = points[edge.A];
        var b = points[edge.B];
        var midpointDensity = Saturate(density((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f));
        var densityFactor = midpointDensity <= 0 ? float.PositiveInfinity : 1f / midpointDensity;
        return densityFactor * DistanceSquared(a, b) * (degrees[edge.A] - 1) * (degrees[edge.B] - 1);
    }

    private static Dictionary<string, int> PlaceFactions(
        int zoneCount,
        HashSet<Edge> links,
        int[,] distances,
        TutorialRoles roles)
    {
        var home = new Dictionary<string, int>(StringComparer.Ordinal);
        var influence = roles.All.ToDictionary(
            faction => faction.CorporationKey,
            faction => Math.Max(0, (faction.InfluenceDistance + 1) / 2),
            StringComparer.Ordinal);

        home[roles.Protagonist.CorporationKey] = MaxZone(zoneCount, zone =>
            RegionSize(zone, influence[roles.Protagonist.CorporationKey], distances));
        home[roles.Antagonist.CorporationKey] = MaxZone(zoneCount, zone =>
            RegionSize(zone, influence[roles.Antagonist.CorporationKey], distances) *
            distances[zone, home[roles.Protagonist.CorporationKey]]);
        home[roles.Protagonist.CorporationKey] = MaxZone(zoneCount, zone =>
            RegionSize(zone, influence[roles.Protagonist.CorporationKey], distances) *
            Math.Sqrt(distances[zone, home[roles.Antagonist.CorporationKey]]));

        var minimumDifference = Enumerable.Range(0, zoneCount)
            .Min(zone => Math.Abs(
                distances[zone, home[roles.Antagonist.CorporationKey]] -
                distances[zone, home[roles.Protagonist.CorporationKey]]));
        home[roles.Buffer.CorporationKey] = MaxZone(
            Enumerable.Range(0, zoneCount).Where(zone => Math.Abs(
                distances[zone, home[roles.Antagonist.CorporationKey]] -
                distances[zone, home[roles.Protagonist.CorporationKey]]) == minimumDifference),
            zone => RegionSize(zone, influence[roles.Buffer.CorporationKey], distances));

        foreach (var neutral in roles.Neutrals)
        {
            home[neutral.CorporationKey] = MaxZone(zoneCount, zone =>
            {
                var score = (double)RegionSize(zone, influence[neutral.CorporationKey], distances);
                foreach (var occupied in home.Values)
                    score *= Math.Sqrt(distances[zone, occupied]);
                return score;
            });
        }

        var present = BuildPresence(zoneCount, roles.All.Where(value => value != roles.Quest), home, influence, distances);
        var preferredQuestZones = Enumerable.Range(0, zoneCount)
            .Where(zone => present[zone].Contains(roles.Antagonist.CorporationKey) && present[zone].Contains(roles.Buffer.CorporationKey))
            .ToArray();
        if (preferredQuestZones.Length > 0)
        {
            home[roles.Quest.CorporationKey] = MaxZone(preferredQuestZones, zone =>
                distances[zone, home[roles.Antagonist.CorporationKey]] *
                RegionSize(zone, influence[roles.Quest.CorporationKey], distances));
        }
        else
        {
            var antagonistZones = Enumerable.Range(0, zoneCount)
                .Where(zone => present[zone].Contains(roles.Antagonist.CorporationKey));
            home[roles.Quest.CorporationKey] = antagonistZones
                .OrderBy(zone => distances[zone, home[roles.Buffer.CorporationKey]])
                .ThenBy(zone => zone)
                .First();
        }

        return home;
    }

    private static HashSet<string>[] BuildPresence(
        int zoneCount,
        IEnumerable<AetheriaDaemonTutorialFactionInput> factions,
        IReadOnlyDictionary<string, int> home,
        IReadOnlyDictionary<string, int> influence,
        int[,] distances)
    {
        return Enumerable.Range(0, zoneCount)
            .Select(zone => factions
                .Where(faction => distances[zone, home[faction.CorporationKey]] <= influence[faction.CorporationKey])
                .Select(faction => faction.CorporationKey)
                .ToHashSet(StringComparer.Ordinal))
            .ToArray();
    }

    private static int[,] BuildDistances(int zoneCount, IReadOnlySet<Edge> links)
    {
        var output = new int[zoneCount, zoneCount];
        for (var source = 0; source < zoneCount; source++)
        {
            var queue = new Queue<int>();
            var visited = new bool[zoneCount];
            queue.Enqueue(source);
            visited[source] = true;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in Neighbors(current, links))
                {
                    if (visited[neighbor])
                        continue;
                    visited[neighbor] = true;
                    output[source, neighbor] = output[source, current] + 1;
                    queue.Enqueue(neighbor);
                }
            }
            if (visited.Any(value => !value))
                throw new InvalidOperationException("Tutorial topology graph is disconnected.");
        }
        return output;
    }

    private static bool IsConnectedWithout(
        int zoneCount,
        IReadOnlySet<Edge> links,
        int source,
        int target,
        Edge ignored)
    {
        var queue = new Queue<int>();
        var visited = new bool[zoneCount];
        queue.Enqueue(source);
        visited[source] = true;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in links)
            {
                if (edge == ignored || !edge.Contains(current))
                    continue;
                var neighbor = edge.Other(current);
                if (neighbor == target)
                    return true;
                if (visited[neighbor])
                    continue;
                visited[neighbor] = true;
                queue.Enqueue(neighbor);
            }
        }
        return false;
    }

    private static IEnumerable<int> Neighbors(int zone, IEnumerable<Edge> links)
    {
        return links.Where(edge => edge.Contains(zone)).Select(edge => edge.Other(zone));
    }

    private static int RegionSize(int zone, int maxDistance, int[,] distances)
    {
        return Enumerable.Range(0, distances.GetLength(0)).Count(other => distances[zone, other] <= maxDistance);
    }

    private static int MaxZone(int zoneCount, Func<int, double> score) =>
        MaxZone(Enumerable.Range(0, zoneCount), score);

    private static int MaxZone(IEnumerable<int> zones, Func<int, double> score) =>
        zones.Select(zone => new { Zone = zone, Score = score(zone) })
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Zone)
            .First().Zone;

    private static float DistanceSquared(Point a, Point b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return x * x + y * y;
    }

    private static float Cross(Point a, Point b, Point c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static float Saturate(float value) => value < 0 ? 0 : value > 1 ? 1 : value;

    private readonly record struct Point(float X, float Y);
    private readonly record struct Triangle(int A, int B, int C);
    private readonly record struct Edge
    {
        public Edge(int a, int b)
        {
            A = Math.Min(a, b);
            B = Math.Max(a, b);
        }

        public int A { get; }
        public int B { get; }
        public bool Contains(int value) => A == value || B == value;
        public int Other(int value) => A == value ? B : B == value ? A : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private sealed record TutorialRoles(
        AetheriaDaemonTutorialFactionInput Protagonist,
        AetheriaDaemonTutorialFactionInput Antagonist,
        AetheriaDaemonTutorialFactionInput Buffer,
        IReadOnlyList<AetheriaDaemonTutorialFactionInput> Neutrals,
        AetheriaDaemonTutorialFactionInput Quest,
        AetheriaDaemonTutorialFactionInput[] All);

    private sealed class TutorialMarkovNameGenerator
    {
        private readonly Dictionary<string, List<char>> _chains = new(StringComparer.Ordinal);
        private readonly List<string> _samples = [];
        private readonly int _order;
        private readonly int _minimumLength;
        private readonly int _maximumLength;
        private CultMath.Random _random;

        public TutorialMarkovNameGenerator(
            uint seed,
            IEnumerable<string> sampleNames,
            int order,
            int minimumLength,
            int maximumLength)
        {
            _random = new CultMath.Random(seed);
            _order = Math.Max(1, order);
            _minimumLength = Math.Max(1, minimumLength);
            _maximumLength = maximumLength;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in sampleNames)
            foreach (var word in (line ?? "").ToUpperInvariant().Split(' ', ',', '.', '"'))
                if (word.Length >= _minimumLength) names.Add(word);

            foreach (var line in names)
            foreach (var word in line.Split(' ', '\'', '(', ')'))
            {
                var lower = word.Trim().ToLowerInvariant();
                if (lower.Length >= _order + 1) _samples.Add(lower + "|");
            }
            foreach (var word in _samples)
            for (var letter = 0; letter < word.Length - _order; letter++)
            {
                var token = word.Substring(letter, _order);
                if (!_chains.TryGetValue(token, out var row))
                    _chains[token] = row = [];
                row.Add(word[letter + _order]);
            }
            if (_samples.Count == 0)
                throw new InvalidDataException("Tutorial geoname corpus contains no usable Markov samples.");
        }

        public string Next(ISet<string> used)
        {
            for (var attempt = 0; attempt < 1_000_000; attempt++)
            {
                var sample = _samples[_random.NextInt(0, _samples.Count)];
                var value = sample.Substring(0, _order);
                while (_chains.TryGetValue(value.Substring(value.Length - _order, _order), out var row))
                {
                    var next = row[_random.NextInt(0, row.Count)];
                    if (next == '|') break;
                    value += next;
                }
                value = char.ToUpperInvariant(value[0]) + value.Substring(1);
                if (value.Length >= _minimumLength && value.Length <= _maximumLength && used.Add(value))
                    return value;
            }
            throw new InvalidOperationException("Tutorial Markov corpus could not produce another unique zone name.");
        }
    }
}
