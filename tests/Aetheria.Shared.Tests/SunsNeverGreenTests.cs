using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Operator rulings suns-never-green and suns-never-green-edges: no generated sun colour (the body's two colour stops, its
// fog tint and its light colour) takes a hue where green dominates. The band is the Settings default, 75 to 165 degrees;
// yellow (60) and teal (170) stay allowed. Hues are recovered from the RGB the production generator wrote.
public sealed class SunsNeverGreenTests : IDisposable
{
    private const float Degree = 1f / 360f;
    // Hue recovered from a float RGB wobbles by a few millionths of a turn; the band is judged with this much margin.
    private const float Slack = 1e-3f;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-sunsgreen-" + Guid.NewGuid().ToString("N"));
    private int _runs;
    private readonly List<CultCache> _caches = new List<CultCache>();

    public SunsNeverGreenTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var cache in _caches) cache.Dispose();
        Directory.Delete(_root, true);
    }

    [Theory]
    [InlineData(76f)]
    [InlineData(80f)]
    [InlineData(120f)]
    [InlineData(160f)]
    [InlineData(164f)]
    public void HuesInsideTheDefaultBandAreRefused(float degrees)
    {
        var settings = new ZoneGenerationSettings();
        Assert.True(ColorMath.HueInBand(degrees * Degree, settings.SunHueExcludedStart, settings.SunHueExcludedEnd));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(60f)]
    [InlineData(74f)]
    [InlineData(166f)]
    [InlineData(170f)]
    [InlineData(180f)]
    [InlineData(300f)]
    public void YellowTealAndTheRestOfTheCircleAreAllowed(float degrees)
    {
        var settings = new ZoneGenerationSettings();
        Assert.False(ColorMath.HueInBand(degrees * Degree, settings.SunHueExcludedStart, settings.SunHueExcludedEnd));
    }

    // The hues below are read back from RGB the generator wrote, so the reader must invert the writer.
    [Fact]
    public void ReadingHueBackFromRgbInvertsWritingIt()
    {
        for (var degrees = 5; degrees < 360; degrees += 5)
            foreach (var value in new[] { .5f, 1f })
            {
                var rgb = ColorMath.HsvToRgb(new float3(degrees * Degree, .75f, value));
                Assert.Equal(degrees * Degree, ColorMath.RgbToHsv(rgb).x, 4);
            }
    }

    [Fact]
    public void TheDrawMapsOntoTheWholeAllowedArcAndNothingElse()
    {
        var settings = new ZoneGenerationSettings();
        var start = settings.SunHueExcludedStart;
        var end = settings.SunHueExcludedEnd;
        var low = float.MaxValue;
        var high = float.MinValue;
        for (var i = 0; i < 1000; i++)
        {
            var hue = ColorMath.HueOutsideBand(i / 1000f, start, end);
            Assert.False(ColorMath.HueInBand(hue, start, end), $"draw {i}/1000 landed in the band");
            // Unwrapped from the band's far edge, the allowed arc runs 165 up to 75 + 360.
            var along = ColorMath.HueInBand(hue, 0f, end) ? hue + 1f : hue;
            low = Math.Min(low, along);
            high = Math.Max(high, along);
        }
        Assert.InRange(low, end, end + 1f * Degree);
        Assert.InRange(high, 1f + start - 1f * Degree, 1f + start);
    }

    [Fact]
    public void GeneratedSunsKeepEveryColourOutOfTheDefaultBand()
    {
        var settings = new ZoneGenerationSettings();
        var suns = GenerateSuns(z => { }, out _);
        Assert.True(suns.Count >= 10, $"only {suns.Count} suns generated");
        foreach (var sun in suns)
            foreach (var hue in Hues(sun))
                Assert.False(ColorMath.HueInBand(hue, settings.SunHueExcludedStart + Slack, settings.SunHueExcludedEnd - Slack),
                    $"a sun colour fell in the green band (hue {hue / Degree:F1} degrees)");
    }

    [Fact]
    public void TheBandComesFromSettingsNotFromTheGenerator()
    {
        // A blue band, 200 to 300 degrees: suns avoid it and now take green freely.
        var suns = GenerateSuns(z =>
        {
            z.SunHueExcludedStart = 200f * Degree;
            z.SunHueExcludedEnd = 300f * Degree;
        }, out _);
        Assert.True(suns.Count >= 10, $"only {suns.Count} suns generated");
        var hues = suns.SelectMany(Hues).ToList();
        Assert.All(hues, hue => Assert.False(ColorMath.HueInBand(hue, 200f * Degree + Slack, 300f * Degree - Slack)));
        Assert.Contains(hues, hue => ColorMath.HueInBand(hue, 75f * Degree, 165f * Degree));
    }

    private static IEnumerable<float> Hues(SunData sun) =>
        sun.Colors.Select(c => c.xyz)
            .Concat(new[] { sun.FogTintColor, sun.LightColor })
            .Select(rgb => ColorMath.RgbToHsv(rgb).x);

    // Production path: Galaxy, ItemManager and ZoneGenerator.GenerateZone over the shipped ZoneSettings, as in OrbitsFromFieldTests.
    private List<SunData> GenerateSuns(Action<ZoneGenerationSettings> adjust, out int zones)
    {
        var suns = new List<SunData>();
        zones = 0;
        _runs++;
        for (uint seed = 1; seed <= 8; seed++)
        {
            var dir = Path.Combine(_root, $"galaxy{_runs}-{seed}");
            Directory.CreateDirectory(dir);
            var catalog = Path.Combine(dir, "Aetheria.cc");
            File.Copy(TestCatalog.Repo, catalog);
            var cache = new CultCache(TestCatalog.Registry());
            _caches.Add(cache);
            cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(catalog), AetheriaStores.CatalogTypes);
            cache.AddBackingStore(new GameCult.Caching.MessagePack.SingleFileMessagePackBackingStore(Path.Combine(dir, "run.cc")), AetheriaStores.RunTypes);
            var authored = AuthoredSettings.Load(RunStartTests.FindRepoRoot());
            var tutorial = authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings");
            var galaxy = new Galaxy(tutorial, authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings"),
                authored.Read<NameGeneratorSettings>("NameGeneratorSettings"), cache, new PlayerSettings(),
                Directory.CreateDirectory(Path.Combine(dir, "Narrative")), _ => { }, null, seed);
            var items = new ItemManager(cache, new ProvenanceLedger(), authored.Read<GameplaySettings>("GameplaySettings"), _ => { });
            var zoneSettings = authored.Read<ZoneGenerationSettings>("ZoneSettings");
            adjust(zoneSettings);
            foreach (var zone in galaxy.Zones)
            {
                zones++;
                var pack = ZoneGenerator.GenerateZone(items, zoneSettings, galaxy, zone, false, false);
                suns.AddRange(pack.Planets.Select(p => cache.Get(p)).OfType<SunData>());
            }
        }
        return suns;
    }
}
