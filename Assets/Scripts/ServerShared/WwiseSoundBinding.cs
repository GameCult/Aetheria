using MessagePack;
[MessagePackObject]
public class WwiseSoundBinding
{
    [Key(0)]
    public uint PlayEvent;
}

[MessagePackObject]
public class WwiseLoopingSoundBinding : WwiseSoundBinding
{
    [Key(1)]
    public uint StopEvent;
}

// public class WwiseParameterBinding
// {
//     [Key(0)]
//     public uint Parameter;
// }