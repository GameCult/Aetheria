using System.Collections.Generic;
using MessagePack;
[MessagePackObject]
public class InputLayout
{
    [Key(0)] public InputLayoutRow[] Rows;

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

[MessagePackObject,
 Union(0, typeof(InputLayoutRowSpacer)),
 Union(1, typeof(InputLayoutKeyRow))
]
public abstract class InputLayoutRow { }

[MessagePackObject]
public class InputLayoutRowSpacer : InputLayoutRow
{
    [Key(0)] public float Height;
}

[MessagePackObject]
public class InputLayoutKeyRow : InputLayoutRow
{
    [Key(0)] public InputLayoutColumn[] Columns;
}

[MessagePackObject,
 Union(0, typeof(InputLayoutColumnSpacer)),
 Union(1, typeof(InputLayoutKey)),
 Union(2, typeof(InputLayoutBindableKey)),
 Union(3, typeof(InputLayoutMultiRowKey))
]
public abstract class InputLayoutColumn
{
    [Key(0)] public float Width;
}

[MessagePackObject]
public class InputLayoutColumnSpacer : InputLayoutColumn { }

[MessagePackObject]
public class InputLayoutKey : InputLayoutColumn { }

[MessagePackObject]
public class InputLayoutBindableKey : InputLayoutKey, IBindableButton
{
    [Key(1)] public string MainLabel;
    [Key(2)] public string AltLabel;
    [Key(3)] public string ShortPath;

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
    [Key(4)] public int Height;
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