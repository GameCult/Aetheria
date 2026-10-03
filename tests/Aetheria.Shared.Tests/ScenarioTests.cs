/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Buffers;
using System.Collections.Generic;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// Scenarios S2 (docs/scenarios-cut.md, R): every new run is a scenario. The fixture is RunStartTests': the shipped
// catalog, the authored settings and a prelude galaxy at a fixed seed.
public sealed partial class RunStartTests
{
    // Whether a run is the prelude is the galaxy's fact (Galaxy.IsPrelude); a save records it with no second owner.
    [Fact]
    public void SaveReadsPrelude()
    {
        var arena = Arena(null);
        var (prelude, _) = RunSave.Capture(_cache, _galaxy, arena, null, new SavedActionBarBinding[0]);
        Assert.True(prelude.IsTutorial);

        var (main, _) = RunSave.Capture(_cache, MainGalaxy(), arena, null, new SavedActionBarBinding[0]);
        Assert.False(main.IsTutorial);
    }

    // PlayerSettings Key 2 (TutorialPassed) is retired: a player.cc written with it still loads, every other field intact.
    [Fact]
    public void OldPlayerSettingsLoad()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(PlayerSettings).Assembly);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(7);
        writer.Write("Pilot");
        writer.WriteNil();                         // Key 1, retired before this cut
        writer.Write(true);                        // Key 2, TutorialPassed
        MessagePackSerializer.Serialize(ref writer, new Dictionary<string, string> { ["story"] = "hash" }, options);
        MessagePackSerializer.Serialize(ref writer, new PlayerGameplaySettings { TemperatureUnit = TemperatureUnit.Kelvin, SignificantDigits = 5 }, options);
        MessagePackSerializer.Serialize(ref writer, new PlayerInputSettings { ActionBarInputs = { "<Keyboard>/1" } }, options);
        MessagePackSerializer.Serialize(ref writer, new PlayerGraphicsSettings { NebulaQuality = Quality.High, ShowAsteroidsInMinimap = true }, options);
        writer.Flush();

        var settings = MessagePackSerializer.Deserialize<PlayerSettings>(buffer.WrittenMemory, options);

        Assert.Equal("Pilot", settings.Name);
        Assert.Equal("hash", settings.HashedStoryFiles["story"]);
        Assert.Equal((TemperatureUnit.Kelvin, 5), (settings.GameplaySettings.TemperatureUnit, settings.GameplaySettings.SignificantDigits));
        Assert.Equal("<Keyboard>/1", Assert.Single(settings.InputSettings.ActionBarInputs));
        Assert.Equal((Quality.High, true), (settings.GraphicsSettings.NebulaQuality, settings.GraphicsSettings.ShowAsteroidsInMinimap));
    }
}
