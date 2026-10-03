// Every way to set up a new run, in the order New Game lists them. Modes are the game; Tests are arenas built to check
// one system each, listed only in editor and development builds.
public static class Scenarios
{
    public static readonly Scenario[] Modes = { new TutorialGalaxy(), new MainGalaxy() };

    public static readonly Scenario[] Tests =
    {
        new DjinniShakedown(), new StarvedReactor(), new Arcs(), new Duel(), new LauncherAngles(), new LongHaul()
    };
}
