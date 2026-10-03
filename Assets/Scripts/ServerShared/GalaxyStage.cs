using System;
using System.IO;
using GameCult.Caching;
using MessagePack;
using static CultMath.math;
using Random = CultMath.Random;

// The galaxy half of a scenario's vocabulary (docs/scenarios-cut.md, R.4): the two kinds of galaxy the game generates,
// at the scenario's seed. The menu hands it the generation inputs; RunStart.Generate sets the seed and hands it to
// Scenario.Generate. The background's noise position derives from the same seed, so a fixed seed fixes the whole
// galaxy, and a clock seed makes every launch a new one.
public sealed class GalaxyStage
{
    private readonly SectorGenerationSettings _sector;
    private readonly SectorBackgroundSettings _sectorBackground;
    private readonly TutorialGenerationSettings _prelude;
    private readonly SectorBackgroundSettings _preludeBackground;
    private readonly NameGeneratorSettings _names;
    private readonly CultCache _cache;
    private readonly PlayerSettings _playerSettings;
    private readonly DirectoryInfo _narrativeDirectory;
    private readonly Action<string> _log;
    private readonly Action<string> _progress;
    private readonly Func<uint> _clock;

    public GalaxyStage(
        SectorGenerationSettings sector,
        SectorBackgroundSettings sectorBackground,
        TutorialGenerationSettings prelude,
        SectorBackgroundSettings preludeBackground,
        NameGeneratorSettings names,
        CultCache cache,
        PlayerSettings playerSettings,
        DirectoryInfo narrativeDirectory,
        Action<string> log,
        Action<string> progress = null,
        Func<uint> clock = null)
    {
        _sector = sector;
        _sectorBackground = sectorBackground;
        _prelude = prelude;
        _preludeBackground = preludeBackground;
        _names = names;
        _cache = cache;
        _playerSettings = playerSettings;
        _narrativeDirectory = narrativeDirectory;
        _log = log;
        _progress = progress;
        _clock = clock ?? (() => (uint) (DateTime.Now.Ticks % uint.MaxValue));
    }

    // The seed this generation runs at, never zero: the scenario's, or the clock's when the scenario's is zero.
    public uint Seed { get; private set; }

    internal void SeedFrom(Scenario scenario)
    {
        var clock = _clock();
        Seed = scenario.Seed != 0 ? scenario.Seed : clock != 0 ? clock : 1;
    }

    // A main galaxy: the sector settings, factions drawn from the catalog, an exit to reach.
    public Galaxy Main()
    {
        var background = Copy(_sectorBackground);
        var random = new Random(Seed);
        background.NoisePosition = random.NextFloat() * 1000;
        return new Galaxy(_sector, background, _names, _cache, _log, _progress, Seed);
    }

    // A prelude galaxy: the tutorial settings and their fixed factions, centred in dense cloud.
    public Galaxy Prelude()
    {
        var background = Copy(_preludeBackground);
        var random = new Random(Seed);
        var iteration = 1;
        do
        {
            background.NoisePosition = random.NextFloat() * 1000;
            _progress?.Invoke($"Finding Galaxy Position: iteration {iteration++}");
        } while (background.CloudDensity(float2(.5f)) < .5f);
        return new Galaxy(_prelude, background, _names, _cache, _playerSettings, _narrativeDirectory, _log, _progress, Seed);
    }

    private static SectorBackgroundSettings Copy(SectorBackgroundSettings settings) =>
        MessagePackSerializer.Deserialize<SectorBackgroundSettings>(MessagePackSerializer.Serialize(settings));
}
