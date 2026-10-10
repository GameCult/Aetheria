using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// tools/blender/aetheria_ships/ship_cc.py edits a ship file's HullData and ShipAuthoring records by MessagePack slot
// number and by hardpoint member position. These tests bind that file to the C# types that own the numbers: any renumbered, renamed or
// reordered member, enum value, or nested array shape fails here instead of corrupting a ship the next time Blender saves.
public sealed class ShipSchemaPinTests
{
    private static readonly string Source = File.ReadAllText(Locate("tools", "blender", "aetheria_ships", "ship_cc.py"));

    [Theory]
    [InlineData("MODEL_ASSET_SLOT", typeof(ShipAuthoring), nameof(ShipAuthoring.ModelAsset))]
    [InlineData("ANCHORS_SLOT", typeof(ShipAuthoring), nameof(ShipAuthoring.Anchors))]
    [InlineData("SCHEMATIC_LINES_SLOT", typeof(ShipAuthoring), nameof(ShipAuthoring.SchematicLines))]
    [InlineData("HULL_SHAPE_SLOT", typeof(HullData), nameof(HullData.Shape))]
    [InlineData("HULL_HARDPOINTS_SLOT", typeof(HullData), nameof(HullData.Hardpoints))]
    public void PythonSlotConstantsAreTheKeysOfTheMembersTheyNameInTheirComments(string constant, Type owner, string member)
    {
        Assert.Equal(KeyOf(owner, member), Integer(constant));
        Assert.Matches(new Regex($@"^{constant} = \d+  # \w+\.{member}\r?$", RegexOptions.Multiline), Source);
    }

    [Fact]
    public void PythonSchemaNamesAreTheDocumentsTheyNameAndTheRetiredSlotIsNotReused()
    {
        Assert.Equal(typeof(ShipAuthoring).GetCustomAttribute<CultDocumentAttribute>().SchemaName, Text("SCHEMA"));
        Assert.Equal(typeof(HullData).GetCustomAttribute<CultDocumentAttribute>().SchemaName, Text("HULL_SCHEMA"));
        Assert.Equal(1, Integer("RETIRED_HULL_SLOT"));
        // Key 1 held the embedded hull until S1. A later member must not take it, or an old file would decode as it.
        Assert.DoesNotContain(typeof(ShipAuthoring).GetMembers(BindingFlags.Public | BindingFlags.Instance),
            member => member.GetCustomAttribute<KeyAttribute>()?.IntKey == 1);
    }

    // HullData.Visual is the one binding between a hull and its visual; its slot is the wire contract.
    [Fact]
    public void HullVisualIsKey32()
    {
        Assert.Equal(32, KeyOf(typeof(HullData), nameof(HullData.Visual)));
        var taken = typeof(HullData).GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(member => member.GetCustomAttribute<KeyAttribute>()?.IntKey).Where(key => key != null).ToArray();
        Assert.Equal(taken.Length, taken.Distinct().Count());
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
    public void PythonAnchorMembersAreShipAnchorMembersAtTheirPositions()
    {
        var members = Strings("ANCHOR_MEMBERS");
        Assert.Equal(5, members.Length);
        for (var slot = 0; slot < members.Length; slot++)
            Assert.Equal(slot, KeyOf(typeof(ShipAnchor), members[slot]));
    }

    // ship_cc.py edits slots 2 to 4 of a ship and the first five slots of an anchor row, and carries every later slot (replace_visual
    // by anchor Id). The rig lives in those later slots; their numbers are the contract that carry-through keeps.
    [Fact]
    public void TheRigLivesInTheSlotsPastTheOnesPythonEdits()
    {
        Assert.Equal(Integer("SCHEMATIC_LINES_SLOT") + 1, KeyOf(typeof(ShipAuthoring), nameof(ShipAuthoring.Joints)));
        Assert.Equal(Strings("ANCHOR_MEMBERS").Length, KeyOf(typeof(ShipAnchor), nameof(ShipAnchor.Joint)));
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
        var (hull, visual) = ShipAuthoringTests.Fixture();
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ShipAuthoring).Assembly);
        object[] Body(object document) => Assert.IsType<object[]>(MessagePackSerializer.Deserialize<object>(
            MessagePackSerializer.Serialize(document.GetType(), document, options), MessagePackSerializerOptions.Standard));

        var body = Body(visual);
        Assert.Equal("mod.skiff", body[0]);
        var lines = Assert.IsType<object[]>(body[Integer("SCHEMATIC_LINES_SLOT")]);
        Assert.Equal("Hull", Assert.IsType<object[]>(Assert.Single(lines))[0]);

        var hullBody = Body(hull);
        // A schematic is [[width, height, cells]] with cells flattened x-major (index x * height + y).
        var hullShape = Assert.IsType<object[]>(Assert.IsType<object[]>(hullBody[Integer("HULL_SHAPE_SLOT")])[0]);
        Assert.Equal(new object[] { 2, 2 }, hullShape.Take(2).Select(value => (object)Convert.ToInt32(value)));
        Assert.Equal(new[] { true, false, true, false }, Assert.IsType<object[]>(hullShape[2]).Cast<bool>());

        var hardpoints = Assert.IsType<object[]>(hullBody[Integer("HULL_HARDPOINTS_SLOT")]);
        var row = Assert.IsType<object[]>(Assert.Single(hardpoints));
        var members = Strings("HARDPOINT_MEMBERS");
        Assert.True(row.Length >= members.Length);
        Assert.Equal("thruster.port", row[Array.IndexOf(members, "Transform")]);
        var footprint = Assert.IsType<object[]>(Assert.IsType<object[]>(row[Array.IndexOf(members, "Shape")])[0]);
        Assert.Equal(3, footprint.Length);
        Assert.Equal(new[] { true }, Assert.IsType<object[]>(footprint[2]).Cast<bool>());
        Assert.Equal(new[] { 1, 0 }, Assert.IsType<object[]>(row[Array.IndexOf(members, "Position")]).Select(value => Convert.ToInt32(value)));
    }

    private static int KeyOf(Type owner, string member)
    {
        var found = owner.GetMember(member, BindingFlags.Public | BindingFlags.Instance).Single();
        return Assert.IsType<int>(found.GetCustomAttribute<KeyAttribute>().IntKey);
    }

    private static int Integer(string constant) =>
        int.Parse(Regex.Match(Source, $@"^{constant} = (\d+)", RegexOptions.Multiline).Groups[1].Value);

    private static string Text(string constant)
    {
        var match = Regex.Match(Source, $@"^{constant} = ""([^""]*)""", RegexOptions.Multiline);
        Assert.True(match.Success, $"{constant} is not a string in ship_cc.py");
        return match.Groups[1].Value;
    }

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
