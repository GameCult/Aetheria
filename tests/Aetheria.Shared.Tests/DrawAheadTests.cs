using Xunit;
using float2 = CultMath.float2;

// DrawAhead draws a free body where the frame falls lead sim seconds after the last step, by the velocity that step
// moved it with. Expected values are worked from first principles: position plus launch velocity and drift, times lead.
public sealed class DrawAheadTests
{
    [Fact]
    public void ABodyIsDrawnAheadByItsTotalVelocity()
    {
        var body = new KinematicBody { Position = new float2(10f, 20f), Velocity = new float2(3f, -1f), Drift = new float2(.5f, 2f) };

        var drawn = DrawAhead.Position(body, .5f);

        Assert.Equal(10f + (3f + .5f) * .5f, drawn.x, 4);
        Assert.Equal(20f + (-1f + 2f) * .5f, drawn.y, 4);
    }

    [Fact]
    public void ABodyDrawnWithNoLeadIsWhereTheSimPutIt()
    {
        var body = new KinematicBody { Position = new float2(10f, 20f), Velocity = new float2(3f, -1f), Drift = new float2(.5f, 2f) };

        var drawn = DrawAhead.Position(body, 0f);

        Assert.Equal(10f, drawn.x, 4);
        Assert.Equal(20f, drawn.y, 4);
    }
}
