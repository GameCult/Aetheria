using System;
using System.IO;
using Xunit;

// potential-owns-gravity (aetheria-gravity, cut grid-lowering): Assets/Shaders/Gravity.cginc is the output of
// GravityShader.Source(), which reads GravityField's constants and writes the one lowering of the potential, the
// band term's closed form and series, each term's reference and gravityHeight's sum. The committed file must equal it, character for
// character (line ends aside): any added statement, respelling, flipped sign, swapped argument or macro fails here.
// To change the shader, change GravityShader and run once with AETHERIA_REGENERATE_GRAVITY_CGINC=1.
public sealed class GravityShaderTests
{
    [Fact]
    public void TheCommittedShaderIsTheGeneratedOne()
    {
        var path = Path.Combine(RunStartTests.FindRepoRoot(), GravityShader.Path);
        if (Environment.GetEnvironmentVariable("AETHERIA_REGENERATE_GRAVITY_CGINC") == "1")
            File.WriteAllText(path, GravityShader.Source());
        var generated = GravityShader.Source();
        // The law was generated, not an empty template agreeing with an empty file.
        Assert.Contains("static const float GravityS[8] = { 0, 0, 0, 0, 35, -84, 70, -20 };", generated);
        Assert.Equal(generated, File.ReadAllText(path).Replace("\r\n", "\n"));
    }
}
