using System.IO;
using System.Text.RegularExpressions;
using Xunit;

// potential-owns-gravity (aetheria-gravity, cut grid-lowering): Assets/Shaders/Gravity.cginc lowers the potential for
// the GPU and decides nothing, so its gravityPotential must be GravityField.Potential, term for term, zero-strength
// guard and core branch included. The shader cannot be compiled here (GravityFieldParityVerify renders it, on Starfire
// only), so this test is the automated guard: each copy's expression is read from its file, reduced to the same
// spelling (float suffixes, the core radius's name, whitespace and comments), and compared. A drift in either file fails it.
public sealed class GravityShaderMirrorTests
{
    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RunStartTests.FindRepoRoot(), relative));

    // The text between `start` and the first `;` after it, comments removed and spellings reduced.
    private static string Expression(string source, string start, string then = "")
    {
        source = Regex.Replace(source, @"//[^\n]*", "");
        var from = source.IndexOf(start, System.StringComparison.Ordinal);
        Assert.True(from >= 0, $"'{start}' not found");
        var body = source.Substring(from + start.Length);
        if (then != "")
        {
            var at = body.IndexOf(then, System.StringComparison.Ordinal);
            Assert.True(at >= 0, $"'{then}' not found after '{start}'");
            body = body.Substring(at + then.Length);
        }
        body = body.Substring(0, body.IndexOf(';'));
        body = Regex.Replace(body, @"(?<=[\d.])f\b", "");
        body = body.Replace("coreRadius", "R");
        return Regex.Replace(body, @"\s+", "");
    }

    private static string CSharpLaw() =>
        Expression(Read("Assets/Scripts/ServerShared/GravityField.cs"), "Potential(float c, float coreRadius, float r) =>");

    private static string ShaderLaw() =>
        Expression(Read("Assets/Shaders/Gravity.cginc"), "float gravityPotential(float c, float R, float r)", "return");

    [Fact]
    public void TheShaderRunsTheSameLawAsGravityFieldPotential()
    {
        var law = CSharpLaw();
        // The reduction found the whole law, so equality is not two empty strings agreeing.
        Assert.Contains("log(r/R)", law);
        Assert.Contains("c==0?0:", law);
        Assert.Contains("c*r*r/(2*R*R)", law);
        Assert.Equal(law, ShaderLaw());
    }
}
