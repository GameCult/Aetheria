using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// tools/blender/aetheria_ships/ship_cc.py edits a ShipAuthoring record by MessagePack slot number and by hardpoint
// member position. These tests bind that file to the C# types that own the numbers: any renumbered, renamed or
// reordered member, enum value, or nested array shape fails here instead of corrupting a ship the next time Blender saves.
public sealed class ShipSchemaPinTests
{
    private static readonly string Source = File.ReadAllText(Locate("tools", "blender", "aetheria_ships", "ship_cc.py"));

    [Theory]
    [InlineData("SCHEMATIC_LINES_SLOT", typeof(ShipAuthoring), nameof(ShipAuthoring.SchematicLines))]
    [InlineData("HULL_SLOT", typeof(ShipAuthoring), nameof(ShipAuthoring.Hull))]
    [InlineData("HULL_SHAPE_SLOT", typeof(HullData), nameof(HullData.Shape))]
    [InlineData("HULL_HARDPOINTS_SLOT", typeof(HullData), nameof(HullData.Hardpoints))]
    public void PythonSlotConstantsAreTheKeysOfTheMembersTheyNameInTheirComments(string constant, Type owner, string member)
    {
        Assert.Equal(KeyOf(owner, member), Integer(constant));
        Assert.Matches(new Regex($@"^{constant} = \d+  # \w+\.{member}\r?$", RegexOptions.Multiline), Source);
    }

    [Fact]
    public void PythonHardpointMembersAreHardpointDataMembersAtTheirOwnPositions()
    {
        var members = Strings("HARDPOINT_MEMBERS");
        Assert.Equal(7, members.Length);
        for (var slot = 0; slot < members.Length; slot++)
            Assert.Equal(slot, KeyOf(typeof(HardpointData), members[slot]));
    }

    [Fact]
    public void PythonEnumNamesAreTheEnumsInValueOrder()
    {
        Assert.Equal(EnumNames<HardpointType>(), Strings("HARDPOINT_TYPE_NAMES"));
        Assert.Equal(EnumNames<ItemRotation>(), Strings("ROTATION_NAMES"));
    }

    [Fact]
    public void SerializedShipHasTheNestedArrayShapesPythonIndexes()
    {
        var ship = ShipAuthoringTests.Fixture();
        var bytes = MessagePackSerializer.Serialize(ship, CultDocumentMessagePackSerialization.OptionsFor(typeof(ShipAuthoring).Assembly));
        var body = Assert.IsType<object[]>(MessagePackSerializer.Deserialize<object>(bytes, MessagePackSerializerOptions.Standard));

        Assert.Equal("mod.skiff", body[0]);
        var hull = Assert.IsType<object[]>(body[Integer("HULL_SLOT")]);
        // A schematic is [[width, height, cells]] with cells flattened x-major (index x * height + y).
        var hullShape = Assert.IsType<object[]>(Assert.IsType<object[]>(hull[Integer("HULL_SHAPE_SLOT")])[0]);
        Assert.Equal(new object[] { 2, 2 }, hullShape.Take(2).Select(value => (object)Convert.ToInt32(value)));
        Assert.Equal(new[] { true, false, true, false }, Assert.IsType<object[]>(hullShape[2]).Cast<bool>());

        var hardpoints = Assert.IsType<object[]>(hull[Integer("HULL_HARDPOINTS_SLOT")]);
        var row = Assert.IsType<object[]>(Assert.Single(hardpoints));
        var members = Strings("HARDPOINT_MEMBERS");
        Assert.True(row.Length >= members.Length);
        Assert.Equal("thruster.port", row[Array.IndexOf(members, "Transform")]);
        var footprint = Assert.IsType<object[]>(Assert.IsType<object[]>(row[Array.IndexOf(members, "Shape")])[0]);
        Assert.Equal(3, footprint.Length);
        Assert.Equal(new[] { true }, Assert.IsType<object[]>(footprint[2]).Cast<bool>());
        Assert.Equal(new[] { 1, 0 }, Assert.IsType<object[]>(row[Array.IndexOf(members, "Position")]).Select(value => Convert.ToInt32(value)));

        var lines = Assert.IsType<object[]>(body[Integer("SCHEMATIC_LINES_SLOT")]);
        Assert.Equal("Hull", Assert.IsType<object[]>(Assert.Single(lines))[0]);
    }

    private static int KeyOf(Type owner, string member)
    {
        var found = owner.GetMember(member, BindingFlags.Public | BindingFlags.Instance).Single();
        return Assert.IsType<int>(found.GetCustomAttribute<KeyAttribute>().IntKey);
    }

    private static int Integer(string constant) =>
        int.Parse(Regex.Match(Source, $@"^{constant} = (\d+)", RegexOptions.Multiline).Groups[1].Value);

    private static string[] Strings(string constant)
    {
        var block = Regex.Match(Source, $@"^{constant} = \((.*?)\)\r?$", RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.True(block.Success, $"{constant} is not a tuple in ship_cc.py");
        return Regex.Matches(block.Groups[1].Value, "\"([^\"]*)\"").Select(match => match.Groups[1].Value).ToArray();
    }

    private static string[] EnumNames<T>() where T : struct, Enum
    {
        var values = Enum.GetValues<T>().OrderBy(value => Convert.ToInt32(value)).ToArray();
        Assert.Equal(Enumerable.Range(0, values.Length), values.Select(value => Convert.ToInt32(value)));
        return values.Select(value => value.ToString()).ToArray();
    }

    private static string Locate(params string[] relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relative).ToArray());
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Repository file not found above the test binary: " + Path.Combine(relative));
    }
}
