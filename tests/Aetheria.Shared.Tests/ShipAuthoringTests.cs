using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CultMath;
using Xunit;

public sealed class ShipAuthoringTests
{
    [Fact]
    public async Task StandaloneShipRoundTripsWithoutTouchingTheCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "aetheria-ship-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "skiff.cc");
        try
        {
            var ship = Fixture();
            ShipAuthoringStore.Validate(ship);
            using (var cache = ShipAuthoringStore.Open(path, writable: true))
            {
                cache.Upsert(ship);
                await cache.FlushAsync();
            }
            var reopened = ShipAuthoringStore.Read(path);
            Assert.Equal("mod.skiff", reopened.Id);
            Assert.Equal("Skiff", reopened.Hull.Name);
            Assert.True(reopened.Hull.Shape.Cells[1, 0]);
            Assert.Equal("thruster.port", reopened.Hull.Hardpoints[0].Transform);
            Assert.Equal("skiff.glb", reopened.ModelAsset);
            Assert.Equal(2, reopened.SchematicLines[0].Points.Length / 3);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void InvalidSchematicOrDanglingMountCannotBePublished()
    {
        var ship = Fixture();
        ship.Hull.Shape.Cells[1, 0] = false;
        Assert.Contains("outside the hull schematic", Assert.Throws<InvalidOperationException>(() =>
            ShipAuthoringStore.Validate(ship)).Message);

        ship = Fixture();
        ship.Anchors.RemoveAt(ship.Anchors.Count - 1);
        Assert.Contains("has no model anchor", Assert.Throws<InvalidOperationException>(() =>
            ShipAuthoringStore.Validate(ship)).Message);

        ship = Fixture();
        ship.SchematicLines[0].Points[0] = float.NaN;
        Assert.Contains("finite XYZ", Assert.Throws<InvalidOperationException>(() =>
            ShipAuthoringStore.Validate(ship)).Message);

        ship = Fixture();
        ShipAuthoringStore.Validate(ship);
        ship.Hull.Shape.Cells[1, 0] = false;
        Assert.Contains("outside the hull schematic", Assert.Throws<InvalidOperationException>(() =>
            ShipAuthoringStore.Validate(ship)).Message);

        ship = Fixture();
        ship.Hull.Hardpoints.Add(new HardpointData
        {
            Type = HardpointType.Thruster,
            Position = new int2(1, 0),
            Shape = new Shape(),
            Transform = "thruster.starboard"
        });
        ship.Anchors.Add(new ShipAnchor { Id = "thruster.starboard", Role = "thruster-emitter", ModelNodeId = "thruster-starboard" });
        Assert.Contains("overlaps another hardpoint", Assert.Throws<InvalidOperationException>(() =>
            ShipAuthoringStore.Validate(ship)).Message);
    }

    private static ShipAuthoring Fixture()
    {
        var shape = new Shape(2, 2);
        shape.Cells[0, 0] = true;
        shape.Cells[1, 0] = true;
        return new ShipAuthoring
        {
            Id = "mod.skiff",
            ModelAsset = "skiff.glb",
            Hull = new HullData
            {
                Name = "Skiff",
                Shape = shape,
                Hardpoints = new List<HardpointData>
                {
                    new HardpointData
                    {
                        Type = HardpointType.Thruster,
                        Position = new int2(1, 0),
                        Shape = new Shape(),
                        Transform = "thruster.port"
                    }
                }
            },
            Anchors = new List<ShipAnchor>
            {
                new ShipAnchor { Id = "map", Role = "map-icon", ModelNodeId = "map" },
                new ShipAnchor { Id = "collider", Role = "hull-collider", ModelNodeId = "collider" },
                new ShipAnchor { Id = "shield", Role = "shield", ModelNodeId = "shield" },
                new ShipAnchor { Id = "tractor", Role = "tractor", ModelNodeId = "tractor" },
                new ShipAnchor { Id = "thruster.port", Role = "thruster-emitter", ModelNodeId = "thruster-port" }
            },
            SchematicLines = new List<ShipPolyline>
            {
                new ShipPolyline
                {
                    Layer = "Hull",
                    Material = "White",
                    Points = new[] { 0f, 0f, 0f, 1f, 0f, 0f },
                    Radii = new[] { .01f, .01f },
                    Opacities = new[] { 1f, 1f },
                    Color = new[] { 1f, 1f, 1f, 1f }
                }
            }
        };
    }
}
