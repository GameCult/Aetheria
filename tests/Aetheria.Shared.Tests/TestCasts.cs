// The two fixtures every cast-galaxy test builds from: the values the retired Settings.asset block held (Miss, Zhe, Luc,
// Aero and Finch, Adras, 64 zones, half influence), so every test galaxy is what it was. A test fixture is not new
// faction material (ruling miss-terris-belongs-to-emily-r3).
internal static class TestCasts
{
    internal static GalaxyCast Wide => new GalaxyCast
    {
        ProtagonistFaction = "Miss", AntagonistFaction = "Zhe", BufferFaction = "Luc",
        NeutralFactions = new[] { "Aero", "Finch" }, QuestFaction = "Adras", LinkDensity = .5f, ZoneCount = 64,
        InfluenceScale = .5f,
    };

    internal static SectorBackgroundSettings Background => new SectorBackgroundSettings
    {
        NoiseAmplitude = 1, NoiseOffset = .3f, NoiseGain = .7f, NoiseLacunarity = 2, NoiseFrequency = .1f,
        NoisePosition = 536.5106f, CloudExponent = 10, CloudAmplitude = .01f,
    };
}
