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

// tools/blender/aetheria_ships/ship_cc.py holds the ship id rule and the enum member names the Blender add-on shares with
// its tools; the ship file itself is read and written only by AetherDb. These tests bind those constants to the C# rule and
// enums that own them: a changed rule, renamed or reordered enum member fails here instead of in a Blender session.
public sealed class ShipSchemaPinTests
{
    private static readonly string Source = File.ReadAllText(Locate("tools", "blender", "aetheria_ships", "ship_cc.py"));

    [Fact]
    public void RetiredShipKeyOneIsNotReused()
    {
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

    // New Ship refuses a bad ship ID before it touches anything, with ship_cc.valid_ship_id; these two constants are that
    // rule's data, so the C# rule that create and Validate share must agree with them on every character and every stem.
    [Fact]
    public void PythonShipIdConstantsAreTheRuleRequireShipIdEnforces()
    {
        var allowed = Text("SHIP_ID_CHARS");
        var reserved = Strings("RESERVED_SHIP_IDS");
        for (var code = 0; code < 256; code++)
        {
            var c = (char)code;
            Assert.Equal(allowed.Contains(c), Accepts("a" + c + "a"));
            Assert.Equal(allowed.Contains(c) && c != '.' && c != '_' && c != '-', Accepts(c + "a"));
        }
        var stems = new List<string>();
        for (var a = 'a'; a <= 'z'; a++)
            for (var b = 'a'; b <= 'z'; b++)
                for (var c = 'a'; c <= 'z'; c++)
                    stems.Add($"{a}{b}{c}");
        for (var digit = 0; digit <= 9; digit++)
            stems.AddRange(new[] { $"com{digit}", $"lpt{digit}" });
        foreach (var stem in stems)
        {
            Assert.Equal(!reserved.Contains(stem), Accepts(stem));
            Assert.Equal(!reserved.Contains(stem), Accepts(stem + ".x"));
        }
    }

    // A faction language file echoes its Faction's record key as the last element of its lang (hull_source.Language.maker); Package
    // compares it to each product's Manufacturer. The catalog owns the key, so a regenerated record fails here, not at Package.
    [Theory]
    [InlineData("zhestokost.scad", "Zhestokost")]
    [InlineData("pirate.scad", "Pirates")]
    [InlineData("rnd.scad", "Rossum & Douglas")]
    public void FactionLanguageFilesNameTheirFactionsRecordKeyInTheCatalog(string file, string faction)
    {
        var scad = File.ReadAllText(Locate("tools", "blender", "aetheria_ships", "factions", file));
        var literal = Regex.Match(scad, @"^[A-Z]+_LANG = \[[^;]*""([^""]+)""\];", RegexOptions.Multiline);
        Assert.True(literal.Success, $"{file} has no *_LANG with a maker as its last element");
        var db = AetherDb.OpenShipped(root: AetherDb.FindRoot());
        try
        {
            var record = Assert.Single(db.Cache.GetAll<Faction>().Where(f => f.Name == faction));
            Assert.Equal(db.Cache.RefOf(record).Key.ToString(), literal.Groups[1].Value);
        }
        finally { db.Cache.Dispose(); }
    }

    private static bool Accepts(string id)
    {
        try { ShipAuthoringStore.RequireShipId(id); return true; }
        catch (InvalidOperationException) { return false; }
    }

    [Fact]
    public void PythonEnumNamesAreTheEnumsInValueOrder()
    {
        Assert.Equal(EnumNames<HardpointType>(), Strings("HARDPOINT_TYPE_NAMES"));
        Assert.Equal(EnumNames<ItemRotation>(), Strings("ROTATION_NAMES"));
        Assert.Equal(EnumNames<HullType>(), Strings("HULL_TYPE_NAMES"));
    }

    // The stored number of each HardpointType: a row is stored by value, so an arm inserted mid-enum silently turns every later
    // stored row into another type. New arms are appended with the next number.
    [Fact]
    public void HardpointTypeValuesAreTheStoredNumbers()
    {
        var stored = new[]
        {
            ("Hull", 0), ("Tool", 1), ("Thermal", 2), ("Thruster", 3), ("WarpDrive", 4), ("Reactor", 5), ("Radiator", 6), ("Shield", 7),
            ("Sensors", 8), ("Energy", 9), ("Ballistic", 10), ("Launcher", 11), ("ControlModule", 12), ("Facility", 13)
        };
        Assert.Equal(stored, Enum.GetValues<HardpointType>().Select(type => (type.ToString(), (int) type)).OrderBy(pair => pair.Item2));
    }

    // GameSettings.GetIcon(HardpointType) indexes the Settings asset's ItemIcons by value: one sprite per type, none null. (GameSettings is
    // Unity code, so the asset is read as text; the loud failure of a missing entry is for the Unity editor to confirm.)
    [Fact]
    public void EveryHardpointTypeHasAnIconInTheSettingsAsset()
    {
        var text = File.ReadAllText(Path.Combine(AetherDb.FindRoot(), "Assets", "Resources", "Settings.asset"));
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var start = lines.IndexOf("  ItemIcons:") + 1;
        Assert.True(start > 0, "Settings.asset has no ItemIcons");
        var entries = lines.Skip(start).TakeWhile(line => line.StartsWith("  - ")).ToArray();
        Assert.Equal(Enum.GetValues<HardpointType>().Length, entries.Length);
        Assert.DoesNotContain(entries, entry => entry.Contains("fileID: 0}"));
    }

    private static int KeyOf(Type owner, string member)
    {
        var found = owner.GetMember(member, BindingFlags.Public | BindingFlags.Instance).Single();
        return Assert.IsType<int>(found.GetCustomAttribute<KeyAttribute>().IntKey);
    }

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
