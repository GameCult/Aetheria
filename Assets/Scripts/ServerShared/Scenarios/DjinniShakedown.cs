using float2 = CultMath.float2;

public sealed class DjinniShakedown : Scenario
{
    public override string Name => "Djinni Shakedown";
    public override string Brief => "A fitted Djinni in empty space. Verify: its thrusters move and turn it; with the hull " +
                                    "offline and thrusters firing, its speed does not run away.";
    public override uint Seed => 101;
    public override bool Ambient => false;

    public override Galaxy Generate(GalaxyStage stage) => stage.Prelude();

    public override void Stage(ScenarioStage stage) =>
        stage.Player(stage.Generated("Djinni"), float2.zero);
}
