using CultMath;

public class Wormhole
{
    public float2 Position;
    public GalaxyZone Target;
    // The run's exit gate: it leads nowhere (Target is null) and ends the run when RunGoal.ExitOpen.
    public bool Exit;
}