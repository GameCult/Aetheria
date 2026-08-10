using System.Collections.Generic;
using JsonKnownTypes;using MessagePack;
using Newtonsoft.Json;

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class InputLayout
{
    [Key(0), JsonProperty("rows")] public InputLayoutRow[] Rows;

    public IEnumerable<InputLayoutBindableKey> GetBindableKeys()
    {
        foreach (var row in Rows)
        {
            if (!(row is InputLayoutKeyRow keyRow)) continue;
            
            foreach (var column in keyRow.Columns)
            {
                if (column is InputLayoutBindableKey bindableKey)
                {
                    yield return bindableKey;
                }
            }
        }
    }
}

[JsonConverter(typeof(JsonKnownTypesConverter<InputLayoutRow>)),
 MessagePackObject, 
 Union(0, typeof(InputLayoutRowSpacer)),
 Union(1, typeof(InputLayoutKeyRow))
]
public abstract class InputLayoutRow { }

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class InputLayoutRowSpacer : InputLayoutRow
{
    [Key(0), JsonProperty("height")] public float Height;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class InputLayoutKeyRow : InputLayoutRow
{
    [Key(0), JsonProperty("columns")] public InputLayoutColumn[] Columns;
}

[JsonConverter(typeof(JsonKnownTypesConverter<InputLayoutColumn>)),
 MessagePackObject, 
 Union(0, typeof(InputLayoutColumnSpacer)),
 Union(1, typeof(InputLayoutKey)),
 Union(2, typeof(InputLayoutBindableKey)),
 Union(3, typeof(InputLayoutMultiRowKey))
]
public abstract class InputLayoutColumn
{
    [Key(0), JsonProperty("width")] public float Width;
}

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class InputLayoutColumnSpacer : InputLayoutColumn { }

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class InputLayoutKey : InputLayoutColumn { }

[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class InputLayoutBindableKey : InputLayoutKey, IBindableButton
{
    [Key(1), JsonProperty("mainLabel")] public string MainLabel;
    [Key(2), JsonProperty("altLabel")] public string AltLabel;
    [Key(3), JsonProperty("path")] public string ShortPath;

    [IgnoreMember]
    public string InputSystemPath
    {
        get
        {
            return $"<Keyboard>/{ShortPath}";
        }
        set
        {
            ShortPath = value.Substring(value.LastIndexOf('/') + 1);
        }
    }
}

public class InputLayoutMultiRowKey : InputLayoutBindableKey
{
    [Key(4), JsonProperty("height")] public int Height;
}

public class InputLayoutMouseButton : IBindableButton
{
    public string Path;
    public string InputSystemPath
    {
        get => Path;
        set => Path = value;
    }
}

public interface IBindableButton
{
    public string InputSystemPath { get; set; }
}