using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

// A directional thruster's emitter mesh is the surface its exhaust is born from, along each triangle's normal, so every
// normal must face the exhaust, opposite the push (ShipModCatalog.BindExhaust). A closed box fires both ways: the operator
// saw the same thrusters firing whichever way the ship strafed. A ring thruster is a class of its own with its own rule and
// is not exercised here.
public sealed class ShipEmitterExhaustTests : IDisposable
{
    private readonly TempDirectory _directory = new TempDirectory();

    public void Dispose() => _directory.Dispose();

    private string Thruster => Path.Combine(_directory.Path, "mod.skiff", "ship.cc");

    // The fixture thruster's hardpoint Rotation is None, so its exhaust leaves toward glTF's +Z; its emitter, one quad
    // wound to face +Z, is the one-sided emitter the generator writes.
    private void Write(Action<ShipParts> tweak = null, string from = null, string to = null)
    {
        ShipFixture.WritePackage(_directory.Path, "mod.skiff", tweak: tweak);
        if (from == null) return;
        var json = ShipFixture.GlbJson(ShipFixture.Nodes);
        Assert.Contains(from, json);
        File.WriteAllBytes(Path.Combine(_directory.Path, "mod.skiff", "skiff.glb"), ShipFixture.Glb(json.Replace(from, to), ShipFixture.MeshBytes()));
    }

    private static void Reversed(ShipParts ship) => ship.Hull.Hardpoints.Single(hardpoint => hardpoint.Transform == "thruster.port").Rotation = ItemRotation.Reversed;

    [Fact]
    public void AOneSidedEmitterFacingItsExhaustIsAccepted()
    {
        Write();
        Assert.Equal("mod.skiff", ShipModCatalog.ReadPackage(Thruster).Visual.Id);
    }

    [Fact]
    public void AClosedBoxEmitterIsRefusedNamingItsAnchor()
    {
        Write(from: @"""name"":""thruster-port"",""mesh"":1", to: @"""name"":""thruster-port"",""mesh"":0");
        var message = Assert.Throws<InvalidOperationException>(() => ShipModCatalog.ReadPackage(Thruster)).Message;
        Assert.Contains("thruster emitter thruster.port", message);
        Assert.Contains("one-sided", message);
    }

    [Fact]
    public void AnEmitterFacingTheWrongWayIsRefused()
    {
        // The same quad toward +Z under a thruster that now faces aft: its exhaust would leave along the push.
        Write(Reversed);
        Assert.Contains("thruster emitter thruster.port", Assert.Throws<InvalidOperationException>(() => ShipModCatalog.ReadPackage(Thruster)).Message);
    }

    [Fact]
    public void TheNodesTurnDecidesWhichWayTheEmitterFaces()
    {
        // Turned half a circle about Y, the quad's +Z becomes -Z: the exhaust of an aft-facing thruster.
        Write(Reversed, from: @"""name"":""thruster-port"",""mesh"":1", to: @"""name"":""thruster-port"",""mesh"":1,""rotation"":[0,1,0,0]");
        Assert.Equal("mod.skiff", ShipModCatalog.ReadPackage(Thruster).Visual.Id);
    }

    [Fact]
    public void TheShippedAndFixturePackagesPass()
    {
        var root = RunStartTests.FindRepoRoot();
        foreach (var ship in new[] { Path.Combine("GameData", "Mods", "aetheria.nakovalnya"), Path.Combine("tests", "fixtures", "mods", "aetheria.wren") })
            Assert.NotNull(ShipModCatalog.ReadPackage(Path.Combine(root, ship, "ship.cc")));
    }

    // The fixture package whose thruster node carries a mesh of the test's own (a third mesh appended to the BIN chunk), with
    // optional vertex normals, an edit to the node, and an edit to the primitive. The thruster's Rotation is None: the exhaust
    // leaves toward +Z and the push is -Z.
    private string WriteCustom(float[] positions, ushort[] indices, float[] normals = null, Action<JObject> node = null, Action<JObject> primitive = null)
    {
        ShipFixture.WritePackage(_directory.Path, "mod.skiff");
        var json = JObject.Parse(ShipFixture.GlbJson(ShipFixture.Nodes));
        var bin = ShipFixture.MeshBytes().ToList();
        int View(byte[] bytes)
        {
            while (bin.Count % 4 != 0) bin.Add(0);
            ((JArray)json["bufferViews"]).Add(new JObject { ["buffer"] = 0, ["byteOffset"] = bin.Count, ["byteLength"] = bytes.Length });
            bin.AddRange(bytes);
            return ((JArray)json["bufferViews"]).Count - 1;
        }
        int Accessor(int view, int component, int count, string type)
        {
            ((JArray)json["accessors"]).Add(new JObject { ["bufferView"] = view, ["componentType"] = component, ["count"] = count, ["type"] = type });
            return ((JArray)json["accessors"]).Count - 1;
        }
        var attributes = new JObject { ["POSITION"] = Accessor(View(positions.SelectMany(BitConverter.GetBytes).ToArray()), 5126, positions.Length / 3, "VEC3") };
        if (normals != null) attributes["NORMAL"] = Accessor(View(normals.SelectMany(BitConverter.GetBytes).ToArray()), 5126, normals.Length / 3, "VEC3");
        var made = new JObject { ["attributes"] = attributes, ["indices"] = Accessor(View(indices.SelectMany(BitConverter.GetBytes).ToArray()), 5123, indices.Length, "SCALAR") };
        primitive?.Invoke(made);
        ((JArray)json["meshes"]).Add(new JObject { ["primitives"] = new JArray(made) });
        var thruster = json["nodes"].Single(n => (string)n["name"] == "thruster-port");
        thruster["mesh"] = ((JArray)json["meshes"]).Count - 1;
        node?.Invoke((JObject)thruster);
        json["buffers"][0]["byteLength"] = bin.Count + (4 - bin.Count % 4) % 4;
        File.WriteAllBytes(Path.Combine(_directory.Path, "mod.skiff", "skiff.glb"), ShipFixture.Glb(json.ToString(Newtonsoft.Json.Formatting.None), bin.ToArray()));
        return Thruster;
    }

    private static readonly ushort[] Quad = { 0, 1, 2, 0, 2, 3 };

    // A unit quad with counter-clockwise normal n (in the XZ plane's span: n = (sin a, 0, cos a)), a measured from +Z.
    private static float[] Plane(double degrees)
    {
        var (s, c) = (Math.Sin(degrees * Math.PI / 180), Math.Cos(degrees * Math.PI / 180));
        var t = new[] { (float)c, 0f, (float)-s };
        return new[] { 0f, 0f, 0f, t[0], 0f, t[2], t[0], 1f, t[2], 0f, 1f, 0f };
    }

    private static float[] Repeat(float x, float y, float z) => Enumerable.Repeat(new[] { x, y, z }, 4).SelectMany(v => v).ToArray();

    private bool Accepts(string ship)
    {
        try { ShipModCatalog.ReadPackage(ship); return true; }
        catch (InvalidOperationException error) when (error.Message.Contains("thruster emitter thruster.port")) { return false; }
    }

    [Theory]
    [InlineData(0, true), InlineData(60, true), InlineData(-60, true), InlineData(89, true), InlineData(-89, true),
     InlineData(91, false), InlineData(-91, false), InlineData(120, false), InlineData(180, false)]
    public void ATiltedEmitterIsJudgedByItsDotWithThePush(double degrees, bool accepted) =>
        Assert.Equal(accepted, Accepts(WriteCustom(Plane(degrees), Quad)));

    [Fact]
    public void VertexNormalsThatContradictTheWindingAreRefused()
    {
        // Unity's Mesh Renderer shape emits along the vertex normals, so a face wound toward the exhaust is not enough.
        Assert.True(Accepts(WriteCustom(Plane(0), Quad, Repeat(0, 0, 1))));
        Assert.False(Accepts(WriteCustom(Plane(0), Quad, Repeat(0, 0, -1))));
        Assert.False(Accepts(WriteCustom(Plane(0), Quad, Repeat(1, 0, 0))));
    }

    [Fact]
    public void AMirroredNodeFlipsTheWindingAndIsJudgedByWhatItDrawsNotWhatItIsWound()
    {
        // Scaled -1 in X the quad's winding reverses in the world, so glTF's front face is the other one.
        Action<JObject> mirror = node => node["scale"] = new JArray(-1, 1, 1);
        Assert.True(Accepts(WriteCustom(Plane(0), Quad, node: mirror)));
        Assert.False(Accepts(WriteCustom(Plane(180), Quad, node: mirror)));
    }

    [Fact]
    public void VertexNormalsFollowTheInverseTransposeUnderNonUniformScale()
    {
        // A quarter turn's eighth about Y over a stretch of 4 in X: the plane with local normal (1,0,1) keeps a normal toward
        // +Z (0.75 of it), where transforming the normal like a position would send it toward -Z.
        Action<JObject> stretched = node => { node["scale"] = new JArray(4, 1, 1); node["rotation"] = new JArray(0, 0.3826834f, 0, 0.9238795f); };
        var tangent = (float)(1 / Math.Sqrt(2));
        var positions = new[] { 0f, 0f, 0f, tangent, 0f, -tangent, tangent, 1f, -tangent, 0f, 1f, 0f };
        Assert.True(Accepts(WriteCustom(positions, Quad, Repeat(tangent, 0, tangent), stretched)));
    }

    [Fact]
    public void OnlyExtensionsThatHideGeometryAreRefused()
    {
        Assert.True(Accepts(WriteCustom(Plane(0), Quad, primitive: p => p["extensions"] = JObject.Parse(@"{""KHR_materials_variants"":{""mappings"":[]}}"))));
        var compressed = WriteCustom(Plane(0), Quad, primitive: p => p["extensions"] = JObject.Parse(@"{""KHR_draco_mesh_compression"":{""bufferView"":0,""attributes"":{""POSITION"":0}}}"));
        Assert.Contains("compressed by KHR_draco_mesh_compression", Assert.Throws<InvalidOperationException>(() => ShipModCatalog.ReadPackage(compressed)).Message);
    }

    [Fact]
    public void ASidewaysEmitterWhoseNormalIsPerpendicularToThePushIsRefused() =>
        // Normal exactly +X, dot with the push exactly 0: the rule is non-negative, so it fires along the axis it is not meant to.
        Assert.False(Accepts(WriteCustom(new[] { 0f, 0f, 0f, 0f, 1f, 0f, 0f, 1f, 1f, 0f, 0f, 1f }, Quad)));

    [Theory]
    [InlineData("KHR_draco_mesh_compression"), InlineData("EXT_meshopt_compression")]
    public void AnAccessorWhoseBufferViewIsCompressedIsRefused(string extension)
    {
        // The compression sits on the bufferView, not the primitive: the accessor's bytes are not positions.
        var path = WriteCustom(Plane(0), Quad);
        var glb = Path.Combine(_directory.Path, "mod.skiff", "skiff.glb");
        var bytes = File.ReadAllBytes(glb);
        var length = (int)BitConverter.ToUInt32(bytes, 12);
        var json = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, length));
        var views = (JArray)json["bufferViews"];
        var positions = (int)json["accessors"][(int)json["meshes"].Last["primitives"][0]["attributes"]["POSITION"]]["bufferView"];
        views[positions]["extensions"] = JObject.Parse("{\"" + extension + "\":{}}");
        var bin = bytes.Skip(28 + length).ToArray();
        File.WriteAllBytes(glb, ShipFixture.Glb(json.ToString(Newtonsoft.Json.Formatting.None), bin));
        Assert.Contains("compressed by " + extension, Assert.Throws<InvalidOperationException>(() => ShipModCatalog.ReadPackage(path)).Message);
    }
}
