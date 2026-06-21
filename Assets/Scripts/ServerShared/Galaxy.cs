using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataStructures.ViliWonka.Heap;
using GameCult.Aetheria.State.Verse;
using Ink.Runtime;
using MIConvexHull;
using JM.LinqFaster;
using UniRx;
using float2 = Unity.Mathematics.float2;
using Random = Unity.Mathematics.Random;

public class Galaxy
{
    public Dictionary<Faction, GalaxyZone> HomeZones = new Dictionary<Faction, GalaxyZone>();
    public Dictionary<Faction, GalaxyZone> BossZones = new Dictionary<Faction, GalaxyZone>();
    public HashSet<GalaxyZone> DiscoveredZones = new HashSet<GalaxyZone>();
    
    public SectorBackgroundSettings Background { get; }
    public NameGeneratorSettings NameGeneratorSettings { get; }
    public Faction[] Factions { get; }
    public GalaxyZone[] Zones { get; }
    public GalaxyZone Entrance { get; }
    public GalaxyZone Exit { get; }
    public uint GenerationSeed { get; }
    public Dictionary<Faction, FactionRelationship> FactionRelationships { get; } = new Dictionary<Faction, FactionRelationship>();
    private Action<string> Log { get; }
    public bool IsPrelude { get; }
    
    private HashSet<string> _containedFactionKeys;
    private GalaxyZone[] _exitPath;
    private Dictionary<Faction, MarkovNameGenerator> _nameGenerators = new Dictionary<Faction, MarkovNameGenerator>();
    private readonly AetheriaRuntimeCatalogSnapshot _runtimeCatalog;
    private readonly Faction[] _allFactions;

    public GalaxyZone[] ExitPath
    {
        get
        {
            if(Entrance!=null && Exit != null)
                return _exitPath ??= FindPath(Entrance, Exit);
            return null;
        }
    }

    public Galaxy(
        SectorGenerationSettings settings, 
        SectorBackgroundSettings background, 
        NameGeneratorSettings nameGeneratorSettings, 
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Action<string> log,
        Action<string> progressCallback = null,
        uint seed = 0)
    {
        _runtimeCatalog = runtimeCatalog ?? throw new InvalidOperationException("Galaxy generation requires the typed Aetheria runtime catalog.");
        _allFactions = ProjectFactions(_runtimeCatalog);
        IsPrelude = false;
        Background = background;
        Log = log;
        GenerationSeed = seed == 0 ? (uint) (DateTime.Now.Ticks % uint.MaxValue) : seed;
        var random = new Random(GenerationSeed);
        Factions = _allFactions.OrderBy(x => random.NextFloat()).Take(settings.MegaCount).ToArray();
        foreach (var f in Factions) FactionRelationships[f] = FactionRelationship.Neutral;

        Zones = GenerateZones(settings.ZoneCount, ref random, progressCallback);

        GenerateLinks(settings.LinkDensity, progressCallback);

        CalculateDistanceMatrix(progressCallback);

        // Exit is the most isolated zone (highest total distance to all other zones)
        Exit = Zones.MaxBy(z => z.Isolation);
        
        // Entrance is the zone furthest from the exit
        Entrance = Zones.MaxBy(z => Exit.Distance[z]);
        
        DiscoveredZones.Add(Entrance);
        foreach(var z in Entrance.AdjacentZones) DiscoveredZones.Add(z);
        
        PlaceFactionsMain(settings.BossCount, progressCallback);

        CalculateFactionInfluence(progressCallback);

        var nameRandom = new CultMath.Random((uint)random.NextInt(1, int.MaxValue));
        GenerateNames(nameGeneratorSettings, ref nameRandom, progressCallback);

        progressCallback?.Invoke("Done!");
        if(progressCallback!=null) Thread.Sleep(500); // Inserting Delay to make it seem like it's doing more work lmao
    }

    public static Galaxy ProjectObservedDaemonRun(
        AetheriaRuntimeRunCheckpointCommit run,
        SectorBackgroundSettings background,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Action<string> log)
    {
        return new Galaxy(run, background, runtimeCatalog, log);
    }

    private Galaxy(
        AetheriaRuntimeRunCheckpointCommit run,
        SectorBackgroundSettings background,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        Action<string> log)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));

        _runtimeCatalog = runtimeCatalog ?? throw new InvalidOperationException("Daemon-observed galaxy requires the typed Aetheria runtime catalog.");
        _allFactions = ProjectFactions(_runtimeCatalog);
        IsPrelude = run.IsTutorial;
        Background = background;
        Log = log;
        GenerationSeed = run.GenerationSeed;
        NameGeneratorSettings = null;
        Factions = ResolveDaemonFactions(run);
        foreach (var faction in Factions)
            FactionRelationships[faction] = ResolveDaemonFactionRelationship(run, faction);

        Zones = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            .Where(zone => zone != null && zone.ZoneIndex >= 0)
            .OrderBy(zone => zone.ZoneIndex)
            .Select(zone => new GalaxyZone
            {
                Name = string.IsNullOrWhiteSpace(zone.Name) ? $"Daemon Zone {zone.ZoneIndex}" : zone.Name,
                Position = new float2((float)zone.PositionX, (float)zone.PositionY),
                Factions = ResolveDaemonZoneFactions(zone),
                Owner = ResolveDaemonFactionByIndex(zone.OwnerFactionIndex),
                NamedZone = !string.IsNullOrWhiteSpace(zone.Name)
            })
            .ToArray();

        var zonesByIndex = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            .Where(zone => zone != null && zone.ZoneIndex >= 0)
            .OrderBy(zone => zone.ZoneIndex)
            .Select((zone, ordinal) => new { zone.ZoneIndex, Zone = Zones[ordinal] })
            .ToDictionary(pair => pair.ZoneIndex, pair => pair.Zone);

        foreach (var source in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
        {
            if (source == null || !zonesByIndex.TryGetValue(source.ZoneIndex, out var sourceZone))
                continue;

            foreach (var adjacentIndex in source.AdjacentZoneIndices ?? Array.Empty<int>())
            {
                if (zonesByIndex.TryGetValue(adjacentIndex, out var adjacentZone) &&
                    !sourceZone.AdjacentZones.Contains(adjacentZone))
                {
                    sourceZone.AdjacentZones.Add(adjacentZone);
                }
            }
        }

        Entrance = ResolveDaemonZone(run.EntranceZoneIndex) ?? ResolveDaemonZone(run.CurrentZoneIndex) ?? Zones.FirstOrDefault();
        Exit = ResolveDaemonZone(run.ExitZoneIndex);
        foreach (var zone in run.DiscoveredZoneIndices ?? Array.Empty<int>())
        {
            var discovered = ResolveDaemonZone(zone);
            if (discovered != null)
                DiscoveredZones.Add(discovered);
        }
        if (DiscoveredZones.Count == 0 && Entrance != null)
            DiscoveredZones.Add(Entrance);

        CalculateDistanceMatrix();

        GalaxyZone ResolveDaemonZone(int zoneIndex)
        {
            return zonesByIndex.TryGetValue(zoneIndex, out var zone) ? zone : null;
        }
    }

    private Faction[] ResolveDaemonFactions(AetheriaRuntimeRunCheckpointCommit run)
    {
        var factionKeys = new HashSet<string>(
            (run.FactionRelationships ?? Array.Empty<AetheriaRuntimeFactionRelationshipCommit>())
                .Select(relationship => relationship?.FactionKey)
                .Where(key => !string.IsNullOrWhiteSpace(key)),
            StringComparer.OrdinalIgnoreCase);

        var factionIndices = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            .Where(zone => zone != null)
            .SelectMany(zone => (zone.FactionIndices ?? Array.Empty<int>()).Concat(new[] { zone.OwnerFactionIndex }))
            .Where(index => index >= 0)
            .Distinct()
            .ToArray();

        foreach (var index in factionIndices)
        {
            if (index >= 0 && index < _allFactions.Length)
                factionKeys.Add(_allFactions[index].FactionKey);
        }

        var factions = _allFactions
            .Where(faction => factionKeys.Contains(faction.FactionKey))
            .ToArray();
        return factions.Length == 0 ? _allFactions : factions;
    }

    private Faction[] ResolveDaemonZoneFactions(AetheriaRuntimeZoneSnapshotCommit zone)
    {
        return (zone.FactionIndices ?? Array.Empty<int>())
            .Select(ResolveDaemonFactionByIndex)
            .Where(faction => faction != null)
            .ToArray();
    }

    private Faction ResolveDaemonFactionByIndex(int index)
    {
        return index >= 0 && index < _allFactions.Length ? _allFactions[index] : null;
    }

    private FactionRelationship ResolveDaemonFactionRelationship(
        AetheriaRuntimeRunCheckpointCommit run,
        Faction faction)
    {
        var relationship = (run.FactionRelationships ?? Array.Empty<AetheriaRuntimeFactionRelationshipCommit>())
            .FirstOrDefault(candidate => string.Equals(candidate?.FactionKey ?? "", faction?.FactionKey ?? "", StringComparison.OrdinalIgnoreCase));
        return Enum.TryParse<FactionRelationship>(relationship?.Relationship ?? "", out var parsed)
            ? parsed
            : FactionRelationship.Neutral;
    }
    
    public Faction ResolveFaction(string name)
    {
        var faction = _allFactions.FirstOrDefault(f => f.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));
        if (faction == null)
        {
            throw new InvalidOperationException($"Typed catalog has no faction matching '{name}'.");
        }

        return faction;
    }

    public Galaxy(
        TutorialGenerationSettings settings,
        SectorBackgroundSettings background,
        NameGeneratorSettings nameGeneratorSettings,
        AetheriaRuntimeCatalogSnapshot runtimeCatalog,
        RuntimePlayerSettings playerSettings, 
        DirectoryInfo narrativeDirectory,
        Action<string> log,
        Action<string> progressCallback = null,
        uint seed = 0)
    {
        _runtimeCatalog = runtimeCatalog ?? throw new InvalidOperationException("Galaxy generation requires the typed Aetheria runtime catalog.");
        _allFactions = ProjectFactions(_runtimeCatalog);
        IsPrelude = true;

        Background = background;
        Log = log;
        GenerationSeed = seed == 0 ? (uint) (DateTime.Now.Ticks % uint.MaxValue) : seed;
        var random = new Random(GenerationSeed);
        
        var factions = new List<Faction>();

        var protagonistFaction = ResolveFaction(settings.ProtagonistFaction);
        factions.Add(protagonistFaction);
        
        var antagonistFaction = ResolveFaction(settings.AntagonistFaction);
        factions.Add(antagonistFaction);
        
        var bufferFaction = ResolveFaction(settings.BufferFaction);
        factions.Add(bufferFaction);
        
        var questFaction = ResolveFaction(settings.QuestFaction);
        factions.Add(questFaction);
        
        var neutralFactions = settings.NeutralFactions
            .Select(ResolveFaction)
            .ToArray();
        factions.AddRange(neutralFactions);
        
        Factions = factions.ToArray();
        foreach (var faction in Factions)
        {
            FactionRelationships[faction] = FactionRelationship.Neutral;
            faction.InfluenceDistance = (faction.InfluenceDistance + 1) / 2;
        }

        Zones = GenerateZones(settings.ZoneCount, ref random, progressCallback);

        GenerateLinks(settings.LinkDensity, progressCallback);

        CalculateDistanceMatrix(progressCallback);

        HomeZones[protagonistFaction] = Zones
            .MaxBy(z => ConnectedRegion(z, protagonistFaction.InfluenceDistance).Count);

        HomeZones[antagonistFaction] = Zones
            .MaxBy(z => ConnectedRegion(z, antagonistFaction.InfluenceDistance).Count * z.Distance[HomeZones[protagonistFaction]]);

        HomeZones[protagonistFaction] = Zones
            .MaxBy(z => ConnectedRegion(z, protagonistFaction.InfluenceDistance).Count * MathF.Sqrt(z.Distance[HomeZones[antagonistFaction]]));
        
        // var antagonistRegion = ConnectedRegion(HomeZones[antagonistFaction], antagonistFaction.InfluenceDistance);
        // var protagonistRegion = ConnectedRegion(HomeZones[protagonistFaction], protagonistFaction.InfluenceDistance);

        // Place the buffer faction in a zone where it has equal distance to the pro/antagonist HQs and where it can control the most territory
        var bufferDistance = Zones.Min(z => Math.Abs(z.Distance[HomeZones[antagonistFaction]] - z.Distance[HomeZones[protagonistFaction]]));
        var potentialBufferZones = Zones
            .Where(z => Math.Abs(z.Distance[HomeZones[antagonistFaction]] - z.Distance[HomeZones[protagonistFaction]]) == bufferDistance);
        HomeZones[bufferFaction] = potentialBufferZones.MaxBy(z => ConnectedRegion(z, bufferFaction.InfluenceDistance).Count);
        
        // Place neutral headquarters away from existing factions while also maximizing territory
        foreach (var faction in neutralFactions)
        {
            HomeZones[faction] = Zones.MaxBy(z =>
                ConnectedRegion(z, faction.InfluenceDistance).Count *
                HomeZones.Values.Aggregate(1f, (i, os) => i * MathF.Sqrt(os.Distance[z])));
        }
        
        CalculateFactionInfluence(progressCallback);

        var potentialQuestZones = Zones
            .Where(z => z.Factions.Contains(antagonistFaction) && z.Factions.Contains(bufferFaction));
        if (potentialQuestZones.Any())
            HomeZones[questFaction] = potentialQuestZones
                .MaxBy(z => z.Distance[HomeZones[antagonistFaction]] * ConnectedRegion(z, questFaction.InfluenceDistance).Count);
        else 
            HomeZones[questFaction] = Zones
                .Where(z => z.Factions.Contains(antagonistFaction))
                .MinBy(z=>z.Distance[HomeZones[bufferFaction]]);
        
        CalculateFactionInfluence(progressCallback);

        Entrance = Zones.Where(z => z.Owner == null).MinBy(z => z.Distance[HomeZones[protagonistFaction]]);
        
        DiscoveredZones.Add(Entrance);
        foreach(var z in Entrance.AdjacentZones) DiscoveredZones.Add(z);

        CalculateFactionInfluence(progressCallback);

        var nameRandom = new CultMath.Random((uint)random.NextInt(1, int.MaxValue));
        GenerateNames(nameGeneratorSettings, ref nameRandom, progressCallback);
        
        // progressCallback?.Invoke("Weaving Narrative");
        // var processor = new StoryProcessor(playerSettings, narrativeDirectory, this, ref random, Log);
        // processor.ProcessStories();

        progressCallback?.Invoke("Done!");
        if(progressCallback!=null) Thread.Sleep(500); // Inserting Delay to make it seem like it's doing more work lmao
    }

    private GalaxyZone[] GenerateZones(int zoneCount, ref Random random, Action<string> progressCallback = null)
    {
        var outputSamples = WeightedSampleElimination.GeneratePoints(zoneCount,
            ref random,
            Background.CloudDensity,
            v => (.2f - LengthSquared(v - new float2(.5f, .5f))) * 4,
            progressCallback);
        return outputSamples.Select(v => new GalaxyZone {Position = v}).ToArray();
    }

    private static Faction[] ProjectFactions(AetheriaRuntimeCatalogSnapshot runtimeCatalog)
    {
        var corporations = runtimeCatalog.Corporations.ToArray();
        var factions = corporations
            .Select(ProjectFaction)
            .ToArray();
        if (factions.Length == 0)
        {
            throw new InvalidOperationException("Typed catalog has no factions for galaxy generation.");
        }

        var factionKeys = new HashSet<string>(
            factions
                .Select(faction => faction.FactionKey)
                .Where(key => !string.IsNullOrWhiteSpace(key)),
            StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < corporations.Length; index++)
        {
            foreach (var allegiance in corporations[index].Allegiances)
            {
                if (!string.IsNullOrWhiteSpace(allegiance.CorporationKey) &&
                    factionKeys.Contains(allegiance.CorporationKey))
                {
                    factions[index].AllegianceByKey[allegiance.CorporationKey] = (float) allegiance.Weight;
                }
            }
        }

        return factions;
    }

    private static Faction ProjectFaction(AetheriaRuntimeCorporation corporation)
    {
        if (string.IsNullOrWhiteSpace(corporation.CorporationKey))
        {
            throw new InvalidOperationException($"Typed catalog corporation {corporation.Name} has no corporation key.");
        }

        return new Faction
        {
            FactionKey = corporation.CorporationKey,
            Name = corporation.Name,
            ShortName = corporation.ShortName,
            Description = corporation.Description,
            GeonameFileKey = corporation.GeonameFileKey,
            BossHullItemKey = corporation.BossHullItemKey,
            InfluenceDistance = corporation.InfluenceDistance,
        };
    }

    private void PlaceFactionsMain(int bossCount, Action<string> progressCallback = null)
    {
        progressCallback?.Invoke("Finding Chokepoints");
        if(progressCallback!=null) Thread.Sleep(500); // Inserting Delay to make it seem like it's doing more work lmao
        
        // Find all zones on the exit path where removing that zone would disconnect the entrance from the exit
        // Disregard "corridor" zones with only two adjacent zones
        var chokePoints = ExitPath
            .Where(z => z.AdjacentZones.Count > 2 && !ConnectedRegion(Entrance, z).Contains(Exit));

        progressCallback?.Invoke("Placing Factions");
        if (progressCallback != null) Thread.Sleep(500); // Inserting Delay to make it seem like it's doing more work lmao

        // Choose some megas to have bosses placed based on whether a boss hull is assigned
        var bossMegas = Factions
            .Where(m => !string.IsNullOrWhiteSpace(m.BossHullItemKey))
            .Take(bossCount)
            .ToArray();

        // Place boss zones along the critical path as far apart from each other as possible
        foreach (var mega in bossMegas)
        {
            BossZones[mega] = chokePoints.MaxBy(z =>
                Exit.Distance[z] * Entrance.Distance[z] *
                BossZones.Values.Aggregate(1, (i, os) => i * os.Distance[z]));
        }

        // Place boss mega headquarters such that their sphere of influence encompasses their boss zone
        // While occupying as much territory as possible
        foreach (var mega in bossMegas)
        {
            HomeZones[mega] = ConnectedRegion(BossZones[mega], mega.InfluenceDistance)
                .MaxBy(z =>
                    ConnectedRegion(z, mega.InfluenceDistance).Count *
                    HomeZones.Values.Aggregate(1f, (i, os) => i * MathF.Sqrt(os.Distance[z])));
        }

        // Place remaining headquarters away from existing megas while also maximizing territory
        foreach (var mega in Factions.Where(m => !bossMegas.Contains(m)))
        {
            HomeZones[mega] = Zones.MaxBy(z =>
                MathF.Pow(ConnectedRegion(z, mega.InfluenceDistance).Count, HomeZones.Count) *
                Exit.Distance[z] * Entrance.Distance[z] *
                HomeZones.Values.Aggregate(1f, (i, os) => i * MathF.Sqrt(os.Distance[z])) *
                BossZones.Values.Aggregate(1f, (i, os) => i * MathF.Sqrt(os.Distance[z])));
        }
    }

    private void CalculateFactionInfluence(Action<string> progressCallback = null)
    {
        progressCallback?.Invoke("Calculating Faction Influence");
        if (progressCallback != null) Thread.Sleep(500); // Inserting Delay to make it seem like it's doing more work lmao

        // Assign faction presence
        foreach (var zone in Zones)
        {
            // Factions are present in all zones within their sphere of influence
            zone.Factions = Factions
                .Where(f => HomeZones.ContainsKey(f))
                .Where(f => zone.Distance[HomeZones[f]] <= f.InfluenceDistance)
                .ToArray();

            // Owner of a zone is the faction with the nearest headquarters
            var nearestFaction = Factions
                .Where(f => HomeZones.ContainsKey(f))
                .MinBy(f => (float)zone.Distance[HomeZones[f]]);
            if (zone.Distance[HomeZones[nearestFaction]] <= nearestFaction.InfluenceDistance)
                zone.Owner = nearestFaction;
        }
    }

    private void GenerateNames(NameGeneratorSettings nameGeneratorSettings,
        ref CultMath.Random random,
        Action<string> progressCallback = null)
    {
        for (var i = 0; i < Factions.Length; i++)
        {
            progressCallback?.Invoke($"Feeding Markov Chains: {i + 1} / {Factions.Length}");
            //if(progressCallback!=null) Thread.Sleep(250); // Inserting Delay to make it seem like it's doing more work lmao
            var faction = Factions[i];
            var nameFile = _runtimeCatalog.FindNameFile(faction.GeonameFileKey);
            if (nameFile == null || nameFile.Names.Count == 0)
            {
                throw new InvalidOperationException($"Typed catalog has no name file for faction {faction.Name} ({faction.GeonameFileKey}).");
            }

            _nameGenerators[faction] = new MarkovNameGenerator(ref random, nameFile.Names, nameGeneratorSettings);
        }

        // Generate zone name using the owner's name generator, otherwise assign catalogue ID
        foreach (var zone in Zones)
        {
            if (zone.Owner != null)
            {
                zone.Name = _nameGenerators[zone.Owner].NextName.Trim();
            }
            else
            {
                zone.Name = $"EAC-{random.NextInt(9999).ToString()}";
            }
        }
    }

    private void GenerateLinks(float linkDensity, Action<string> progressCallback = null)
    {
        progressCallback?.Invoke("Triangulating Zone Positions");
        if (progressCallback != null) Thread.Sleep(500); // Inserting Delay to make it seem like it's doing more work lmao

        // Create a delaunay triangulation to connect adjacent sectors
        var triangulation = DelaunayTriangulation<Vertex2<GalaxyZone>, Cell2<GalaxyZone>>
            .Create(Zones.Select(z => new Vertex2<GalaxyZone>(z.Position, z)).ToList(), 1e-7f);
        var links = new HashSet<(GalaxyZone, GalaxyZone)>();
        foreach (var cell in triangulation.Cells)
        {
            if (!links.Contains((cell.Vertices[0].StoredObject, cell.Vertices[1].StoredObject)) &&
                !links.Contains((cell.Vertices[1].StoredObject, cell.Vertices[0].StoredObject)))
                links.Add((cell.Vertices[0].StoredObject, cell.Vertices[1].StoredObject));
            if (!links.Contains((cell.Vertices[1].StoredObject, cell.Vertices[2].StoredObject)) &&
                !links.Contains((cell.Vertices[2].StoredObject, cell.Vertices[1].StoredObject)))
                links.Add((cell.Vertices[1].StoredObject, cell.Vertices[2].StoredObject));
            if (!links.Contains((cell.Vertices[0].StoredObject, cell.Vertices[2].StoredObject)) &&
                !links.Contains((cell.Vertices[2].StoredObject, cell.Vertices[0].StoredObject)))
                links.Add((cell.Vertices[0].StoredObject, cell.Vertices[2].StoredObject));
        }

        progressCallback?.Invoke("Eliminating Zone Links");
        if (progressCallback != null) Thread.Sleep(500); // Inserting Delay to make it seem like it's doing more work lmao
        // foreach (var link in links.ToArray())
        // {
        //     foreach (var zone in Zones)
        //     {
        //         if(zone != link.Item1 && zone != link.Item2)
        //             if (AetheriaMath.FindDistanceToSegment(zone.Position, link.Item1.Position, link.Item2.Position, out _) < minLineSeparation)
        //                 links.Remove(link);
        //     }
        // }

        foreach (var link in links)
        {
            link.Item1.AdjacentZones.Add(link.Item2);
            link.Item2.AdjacentZones.Add(link.Item1);
        }

        float LinkWeight((GalaxyZone, GalaxyZone) link)
        {
            return 1 / Saturate(Background.CloudDensity((link.Item1.Position + link.Item2.Position) / 2)) *
                   LengthSquared(link.Item1.Position - link.Item2.Position) *
                   (link.Item1.AdjacentZones.Count - 1) * (link.Item2.AdjacentZones.Count - 1);
        }

        var heap = new MaxHeap<(GalaxyZone, GalaxyZone)>(links.Count);
        foreach (var link in links) heap.PushObj(link, LinkWeight(link));
        while (heap.Count > linkDensity * links.Count)
        {
            var link = heap.PopObj();
            if (ConnectedRegion(link.Item1, link.Item1, link.Item2).Contains(link.Item2))
            {
                link.Item1.AdjacentZones.Remove(link.Item2);
                link.Item2.AdjacentZones.Remove(link.Item1);
                foreach (var secondary in link.Item1.AdjacentZones)
                {
                    heap.SetValue((link.Item1, secondary), LinkWeight((link.Item1, secondary)));
                    heap.SetValue((secondary, link.Item1), LinkWeight((secondary, link.Item1)));
                }

                foreach (var secondary in link.Item2.AdjacentZones)
                {
                    heap.SetValue((link.Item2, secondary), LinkWeight((link.Item2, secondary)));
                    heap.SetValue((secondary, link.Item2), LinkWeight((secondary, link.Item2)));
                }
            }
        }
    }

    // Cache distance matrix and calculate isolation for every zone (used extensively for placing stuff)
    private void CalculateDistanceMatrix(Action<string> progressCallback = null)
    {
        progressCallback?.Invoke("Calculating Distance Matrix");
        if(progressCallback!=null) Thread.Sleep(500); // Inserting Delay to make it seem like it's doing more work lmao
        foreach (var zone in Zones)
        {
            zone.Distance = ConnectedRegionDistance(zone);
            zone.Isolation = zone.Distance.Sum(x => x.Value);
        }
    }

    public bool ContainsFaction(string factionKey)
    {
        _containedFactionKeys ??= new HashSet<string>(
            Factions
                .Select(f => f.FactionKey)
                .Where(key => !string.IsNullOrWhiteSpace(key)),
            StringComparer.OrdinalIgnoreCase);
        return !string.IsNullOrWhiteSpace(factionKey) && _containedFactionKeys.Contains(factionKey);
    }

    public Faction ResolveFactionByKey(string factionKey)
    {
        if (string.IsNullOrWhiteSpace(factionKey))
        {
            return null;
        }

        var faction = Factions.FirstOrDefault(f => string.Equals(f.FactionKey, factionKey, StringComparison.OrdinalIgnoreCase));
        if (faction != null)
        {
            return faction;
        }

        return _allFactions.FirstOrDefault(f => string.Equals(f.FactionKey, factionKey, StringComparison.OrdinalIgnoreCase));
    }

    class DijkstraVertex
    {
        public float Cost;
        public DijkstraVertex Parent;
        public GalaxyZone Zone;
    }
    
    public Dictionary<GalaxyZone, int> ConnectedRegionDistance(GalaxyZone v)
    {
        var members = new Dictionary<GalaxyZone, int> {{v, 0}};
        int cost = 1;
        while (true)
        {
            var lastCount = members.Count;
            // For each member, add all vertices that are connected to it but are not already a member
            // Also, if there is a zone being ignored, do not traverse across it
            foreach (var member in members.Keys.ToArray())
            foreach (var adjacentZone in member.AdjacentZones)
            {
                if(!members.ContainsKey(adjacentZone))
                    members.Add(adjacentZone, cost);
            }
            // If we have stopped finding neighbors, stop traversing
            if (members.Count == lastCount)
                return members;
            cost++;
        }
    }

    public HashSet<GalaxyZone> ConnectedRegion(
        GalaxyZone v,
        GalaxyZone ignoreLinkSource,
        GalaxyZone ignoreLinkTarget,
        int maxDistance = int.MaxValue)
    {
        return ConnectedRegion(v, x => x.Item1 != ignoreLinkSource || x.Item2 != ignoreLinkTarget, maxDistance);
    }

    public HashSet<GalaxyZone> ConnectedRegion(
        GalaxyZone v,
        GalaxyZone ignoreZone,
        int maxDistance = int.MaxValue)
    {
        return ConnectedRegion(v, x => x.Item2 != ignoreZone, maxDistance);
    }

    public HashSet<GalaxyZone> ConnectedRegion(
        GalaxyZone v,
        int maxDistance = int.MaxValue)
    {
        return ConnectedRegion(v, x => true, maxDistance);
    }
    
    public HashSet<GalaxyZone> ConnectedRegion(
        GalaxyZone v, 
        Predicate<(GalaxyZone source, GalaxyZone target)> linkFilter, 
        int maxDistance = int.MaxValue)
    {
        var visited = new HashSet<GalaxyZone> {v};
        int cost = 1;
        while (true)
        {
            var lastCount = visited.Count;
            // For each member, add all nodes that are connected to it but have not been visited
            // Also, if there is a zone being ignored, do not traverse across it
            foreach (var zone in visited.ToArray())
            foreach (var adjacentZone in zone.AdjacentZones)
            {
                if(!visited.Contains(adjacentZone) && linkFilter((zone, adjacentZone)))
                    visited.Add(adjacentZone);
            }
            // If we have stopped finding neighbors, stop traversing
            if (visited.Count == lastCount || cost == maxDistance)
                return visited;
            cost++;
        }
    }
    
    public GalaxyZone[] FindPath(GalaxyZone source, GalaxyZone target, bool bestFirst = false)
    {
        MinHeap<DijkstraVertex> unsearchedNodes = new MinHeap<DijkstraVertex>();
        unsearchedNodes.PushObj(new DijkstraVertex{Zone = source}, 0);
        var searched = new HashSet<GalaxyZone>();
        while (true)
        {
            if(unsearchedNodes.Count == 0) return null;  // No nodes left unsearched
            var s = unsearchedNodes.PopObj(); // Lowest cost unsearched node
            if (s.Zone == target) // We found the path
            {
                Stack<DijkstraVertex> path = new Stack<DijkstraVertex>(); // Since we start at the end, use a LIFO collection
                path.Push(s);
                while(path.Peek().Parent!=null) // Keep pushing until we reach the start, which has no parent
                    path.Push(path.Peek().Parent);
                return path.Select(dv => dv.Zone).ToArray();
            }
            // For each adjacent star (filter already visited stars unless heuristic is in use)
            IEnumerable<GalaxyZone> zonesToSearch = s.Zone.AdjacentZones;
            if (!bestFirst) zonesToSearch = zonesToSearch.Where(z => !searched.Contains(z));
            foreach (var dijkstraStar in zonesToSearch
                    // Cost is parent cost plus distance squared
                    .Select(zone => new DijkstraVertex {Parent = s, Zone = zone, Cost = s.Cost + LengthSquared(s.Zone.Position - zone.Position)}))
                // Add new member to list, sorted by cost plus optional heuristic 
                unsearchedNodes.PushObj(dijkstraStar, bestFirst ? dijkstraStar.Cost + LengthSquared(dijkstraStar.Zone.Position - target.Position) : dijkstraStar.Cost);
            searched.Add(s.Zone);
        }
    }

    private static float LengthSquared(float2 value)
    {
        return value.x * value.x + value.y * value.y;
    }

    private static float Saturate(float value)
    {
        return value < 0 ? 0 : value > 1 ? 1 : value;
    }
}

public class GalaxyZone
{
    public string Name;
    public float2 Position;
    public List<GalaxyZone> AdjacentZones = new List<GalaxyZone>();
    public int Isolation;
    public Dictionary<GalaxyZone, int> Distance;
    public Faction[] Factions;
    public Faction Owner;
    public bool NamedZone;
    public List<LocationStory> Locations = new List<LocationStory>();
}

public class GalaxyQuest
{
    public Story Story;
    public Dictionary<string, LocationStory> KnotLocations = new Dictionary<string, LocationStory>();
}

public class LocationStory
{
    public GalaxyZone Zone;
    public string FileName;
    public string Name;
    public Story Story;
    public Faction Faction;
    public SecurityLevel Security;
    public LocationType Type;
    public int Turrets;
    public Dictionary<string, List<GalaxyQuest>> KnotQuests = new Dictionary<string, List<GalaxyQuest>>();
}
