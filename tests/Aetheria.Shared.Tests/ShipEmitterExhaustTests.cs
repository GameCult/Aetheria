using System;
using System.IO;
using System.Linq;
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
}
