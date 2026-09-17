using CultMath;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;
using static CultMath.math;

// Pins the fix for the zero-sensitivity regression: ActionGameManager.Sensitivity (a Unity-serialized float2) was
// deleted because CultMath's float2 isn't Unity-serializable, and the setting moved into the CultCache
// PlayerInputSettings global. A player store written before this field existed has no key 2 for Sensitivity; it must
// deserialize with the look-sensitivity default, not zero, or mouse look goes dead again.
public sealed class PlayerSettingsSensitivityTests
{
    private static readonly MessagePackSerializerOptions Options =
        CultDocumentMessagePackSerialization.OptionsFor(typeof(PlayerInputSettings).Assembly);

    [Fact]
    public void MissingSensitivityKeyDeserializesToDefault()
    {
        // Simulate a pre-existing store: a PlayerInputSettings array holding only keys 0 and 1, so key 2
        // (Sensitivity) was never written.
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        MessagePackSerializer.Serialize(ref writer, new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<int, string>>(), Options);
        MessagePackSerializer.Serialize(ref writer, new System.Collections.Generic.List<string>(), Options);
        writer.Flush();

        var settings = MessagePackSerializer.Deserialize<PlayerInputSettings>(buffer.WrittenMemory, Options);

        Assert.Equal(float2(-0.001f, 0.001f), settings.Sensitivity);
    }

    [Fact]
    public void NonDefaultSensitivityRoundTrips()
    {
        var settings = new PlayerInputSettings { Sensitivity = float2(0.5f, -0.25f) };

        var bytes = MessagePackSerializer.Serialize(settings, Options);
        var result = MessagePackSerializer.Deserialize<PlayerInputSettings>(bytes, Options);

        Assert.Equal(float2(0.5f, -0.25f), result.Sensitivity);
    }
}
