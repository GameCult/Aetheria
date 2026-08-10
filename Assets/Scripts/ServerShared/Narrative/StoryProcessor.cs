
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ink;
using Ink.Parsed;
using MessagePack;
using Path = System.IO.Path;
using Random = Unity.Mathematics.Random;
using Story = Ink.Runtime.Story;

public class StoryProcessor : IZoneResolver, IFactionResolver
{
    private class AetheriaInkFileHandler : IFileHandler {
        private DirectoryInfo NarrativeRoot { get; }
        
        public AetheriaInkFileHandler(DirectoryInfo narrativeRoot)
        {
            NarrativeRoot = narrativeRoot;
        }

        public string ResolveInkFilename (string includeName) => Path.Combine (NarrativeRoot.FullName, includeName);

        public string LoadInkFileContents (string fullFilename) => File.ReadAllText (fullFilename);
    }

    private Dictionary<Story, string[]> _storyKnotPaths = new Dictionary<Story, string[]>();
    private Dictionary<Story, LocationStory> _placedStories = new Dictionary<Story, LocationStory>();
    private Dictionary<string, Story> _processedStories = new Dictionary<string, Story>();
    private DirectoryInfo _locationsPath;
    private DirectoryInfo _questsPath;
    private IFileHandler _inkFileHandler;
    public DirectoryInfo NarrativeDirectory { get; }
    private Random _random;
    public Galaxy Galaxy { get; }
    public PlayerSettings Settings { get; }

    private Action<string> Log { get; }
    
    public StoryProcessor(PlayerSettings settings, DirectoryInfo narrativeDirectory, Galaxy galaxy, ref Random random, Action<string> log)
    {
        NarrativeDirectory = narrativeDirectory;
        Galaxy = galaxy;
        _random = random;
        Log = log;
        Settings = settings;
        
        _inkFileHandler = new AetheriaInkFileHandler(narrativeDirectory);
        
        _locationsPath = narrativeDirectory.CreateSubdirectory("Locations");
        _questsPath = narrativeDirectory.CreateSubdirectory("Quests");
    }

    public void ProcessStories()
    {
        var locationFiles = _locationsPath.EnumerateFiles("*.ink");
        foreach(var inkFile in locationFiles)
        {
            Log($"Processing story file at {inkFile.FullName}");
            ProcessLocation(GetStory(inkFile), inkFile);
        }

        var questFiles = _questsPath.EnumerateFiles("*ink");
        foreach(var inkFile in questFiles)  ProcessQuest(GetStory(inkFile), inkFile);
    }
    
    public GalaxyZone ResolveZone(string path)
    {
        // Handle special cases: "start" and "end"
        if (path.Equals("start", StringComparison.InvariantCultureIgnoreCase))
            return Galaxy.Entrance;
        if (path.Equals("end", StringComparison.InvariantCultureIgnoreCase))
            return Galaxy.Exit;
        
        // Handle faction home reference: format = "home.<faction>"
        if(path.StartsWith("home"))
        {
            var factionString = path.Substring(5);
            var faction = ResolveFaction(factionString);
            return !Galaxy.HomeZones.ContainsKey(faction) ? null : Galaxy.HomeZones[faction];
        }

        return ResolveLocation(path)?.Zone;
    }

    public LocationStory ResolveLocation(string path)
    {
        var locationInkFile = new FileInfo(Path.Combine(_locationsPath.FullName, $"{path}.ink"));
        if (!locationInkFile.Exists) return null;

        var story = GetStory(locationInkFile);
        ProcessLocation(story, locationInkFile);
        return _placedStories.ContainsKey(story) ? _placedStories[story] : null;
    }

    public Faction ResolveFaction(string name)
    {
        return Galaxy.Factions.FirstOrDefault(f => f.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));
    }

    Story GetStory(FileInfo inkFile)
    {
        var fileName = Path.GetFileNameWithoutExtension(inkFile.FullName);
        
        // If the story has already been read / compiled, return it directly
        if (_processedStories.ContainsKey(fileName)) return _processedStories[fileName];
        
        // var compiledFileName = Path.Combine(inkFile.Directory.FullName, $"{fileName}.json");
        //
        // // Calculate a hash of the ink file
        // var hash = File.ReadAllBytes(inkFile.FullName).GetHashSHA1();
        //
        // if (File.Exists(compiledFileName) && // If a compiled version of the ink file exists
        //     Settings.HashedStoryFiles.ContainsKey(fileName) && // And its hash has been saved
        //     hash == Settings.HashedStoryFiles[fileName]) // And the saved hash matches the current hash
        // {
        //     // Load the story from the existing compiled JSON representation
        //     return new Story(File.ReadAllText(compiledFileName));
        // }
        //
        // Settings.HashedStoryFiles[fileName] = hash;
        var compiler = new Compiler(File.ReadAllText(inkFile.FullName), new Compiler.Options
        {
            countAllVisits = true,
            fileHandler = _inkFileHandler
        });
        var story = compiler.Compile();
        var knots = compiler.parsedStory.FindAll<Knot>().Select(k=>k.runtimePath.ToString()).ToArray();
        _storyKnotPaths[story] = knots;
        //File.WriteAllText(compiledFileName, story.ToJson());
        _processedStories[fileName] = story;
        return story;
    }

    public void ProcessQuest(Story story, FileInfo inkFile)
    {
        var fileName = Path.GetFileNameWithoutExtension(inkFile.FullName);
        var quest = new GalaxyQuest();
        quest.Story = story;
        foreach (var knot in _storyKnotPaths[story])
        {
            var tags = story.TagsForContentAtPath(knot);
            if(tags==null) continue;
            var contentTags = GetContentTags(tags);
            if (contentTags.ContainsKey("location"))
            {
                var locationName = contentTags["location"].First();
                var locationStory = ResolveLocation(locationName);
                if (tags.Contains("required") && locationStory == null)
                {
                    Log($"Quest \"{fileName}\" unable to find required location \"{locationName}\" for knot \"{knot}\"");
                    return;
                }
                quest.KnotLocations[knot] = locationStory;
            }
        }

        foreach (var x in quest.KnotLocations)
        {
            if (!x.Value.KnotQuests.ContainsKey(x.Key)) x.Value.KnotQuests[x.Key] = new List<GalaxyQuest>();
            x.Value.KnotQuests[x.Key].Add(quest);
        }
    }
    
    public void ProcessLocation(Story story, FileInfo inkFile)
    {
        if (_placedStories.ContainsKey(story)) return; // Don't place already placed stories, idiot!
        _placedStories[story] = null; // Avoids potential for infinite loops when evaluating constraints
        var fileName = Path.GetFileNameWithoutExtension(inkFile.FullName);

        var contentTags = GetContentTags(story.globalTags);

        var constraints = new List<ZoneConstraint>();
        if(contentTags.ContainsKey("constraint"))
            foreach (var constraintString in contentTags["constraint"])
            {
                var args = constraintString.Split(' ');
                var flip = args[0] == "not";
                if (flip) args = args.Skip(1).ToArray();
                var constraintName = args[0];
                args = args.Skip(1).ToArray();
                ZoneConstraint constraint = constraintName switch
                {
                    "DistanceFrom" => new DistanceConstraint(args, this) { Flip = flip },
                    "FactionPresent" => new FactionPresenceConstraint(args, this) { Flip = flip },
                    "FactionOwner" => new FactionOwnerConstraint(args, this) { Flip = flip },
                    _ => null
                };
                if(constraint!=null) constraints.Add(constraint);
            }

        ZoneSelector selector;
        if (contentTags.ContainsKey("select"))
        {
            var selectorString = contentTags["select"].First();
            var args = selectorString.Split(' ');
            var flip = args[0] == "not";
            if (flip) args = args.Skip(1).ToArray();
            var selectorName = args[0];
            args = args.Skip(1).ToArray();
            selector = selectorName switch
            {
                "DistanceFrom" => new DistanceSelector(args, this) { Flip = flip },
                _ => new RandomSelector(ref _random)
            };
        }
        else
            selector = new RandomSelector(ref _random);
        
        var zoneCandidates = new List<GalaxyZone>();
        zoneCandidates.AddRange(Galaxy.Zones.Where(z=>!z.NamedZone && constraints.All(c=>c.Test(z))));
        
        var zone = selector.SelectZone(zoneCandidates);

        var location = new LocationStory
        {
            Zone = zone,
            FileName = fileName,
            Name = contentTags.ContainsKey("name") ? contentTags["name"].First() : fileName,
            Faction = contentTags.ContainsKey("faction") ? ResolveFaction(contentTags["faction"].First()) : zone.Owner,
            Security = contentTags.ContainsKey("security")
                ? (SecurityLevel) Enum.Parse(typeof(SecurityLevel), contentTags["security"].First(), true)
                : SecurityLevel.Open,
            Story = story,
            Type = contentTags.ContainsKey("type")
                ? (LocationType) Enum.Parse(typeof(LocationType), contentTags["type"].First(), true)
                : LocationType.Station,
            Turrets = contentTags.ContainsKey("turrets") ? int.Parse(contentTags["turrets"].First()) : 0
        };
        zone.Locations.Add(location);

        if (contentTags.ContainsKey("namezone"))
        {
            zone.Name = contentTags["namezone"].First();
        }

        _placedStories[story] = location;
    }

    // Parse all tags of the format X:Y into a dictionary with a list of every Y for each X
    public static Dictionary<string, List<string>> GetContentTags(List<string> tags)
    {
        var contentTags = new Dictionary<string, List<string>>();
        foreach (var tag in tags)
        {
            if (tag.IndexOf(':') == -1) continue;
            
            var tokens = tag.Split(':');
            var leftSide = tokens[0].Trim().ToLowerInvariant();
            var rightSide = tokens[1].Trim();
            if (!contentTags.ContainsKey(leftSide)) contentTags[leftSide] = new List<string>();
            contentTags[leftSide].Add(rightSide);
        }

        return contentTags;
    }
}