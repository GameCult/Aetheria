using float2 = CultMath.float2;

public sealed class DjinniShakedown : Scenario
{
    public override string Name => "Djinni Shakedown";
    public override string Id => "djinni-shakedown";
    public override string Brief => "A fitted Djinni in empty space, facing +z, with a bare hull dead ahead and one abeam. Verify: its thrusters " +
                                    "move and turn it; with the hull offline and thrusters firing, its speed does not run away. Its " +
                                    "articulated ballistic barrel points at the hull ahead when it bears, and rests at the arc edge while you " +
                                    "aim at the hull abeam.";
    public override uint Seed => 101;
    public override bool Ambient => false;

    // Where the targets sit: the ballistic mount faces +z, so Ahead is inside its arc and Abeam outside it.
    public static readonly float2 Ahead = new float2(0, 400), Abeam = new float2(400, 0);

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage)
    {
        stage.Player(stage.Generated("Djinni"), float2.zero);
        stage.Place(stage.Bare("Longinus"), Ahead);
        stage.Place(stage.Bare("Longinus"), Abeam);
    }
}
