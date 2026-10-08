// Every way to set up a new run, in the order New Game lists them. Modes are what a release build lists; Development
// are the other ways the game sets up a run and Tests are arenas built to check one system each, both listed only in
// editor and development builds.
public static class Scenarios
{
    public static readonly Scenario[] Modes = { new DemoTerminus() };

    public static readonly Scenario[] Development = { new TutorialGalaxy(), new MainGalaxy() };

    public static readonly Scenario[] Tests =
    {
        new DjinniShakedown(), new StarvedReactor(), new Arcs(), new Duel(), new LauncherAngles(), new LongHaul()
    };
}
