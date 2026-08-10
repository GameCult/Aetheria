using MessagePack;
using Newtonsoft.Json;

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class WwiseSoundBinding
{
    [Key(0), JsonProperty("playEvent")]
    public uint PlayEvent;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class WwiseLoopingSoundBinding : WwiseSoundBinding
{
    [Key(1), JsonProperty("stopEvent")]
    public uint StopEvent;
}

// public class WwiseParameterBinding
// {
//     [Key(0), JsonProperty("parameter")]
//     public uint Parameter;
// }