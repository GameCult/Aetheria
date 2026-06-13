using System.Collections.Generic;

public class InputLayout
{
    public InputLayoutRow[] Rows;

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

public abstract class InputLayoutRow { }

public class InputLayoutRowSpacer : InputLayoutRow
{
    public float Height;
}

public class InputLayoutKeyRow : InputLayoutRow
{
    public InputLayoutColumn[] Columns;
}

public abstract class InputLayoutColumn
{
    public float Width;
}

public class InputLayoutColumnSpacer : InputLayoutColumn { }

public class InputLayoutKey : InputLayoutColumn { }

public class InputLayoutBindableKey : InputLayoutKey, IBindableButton
{
    public string MainLabel;
    public string AltLabel;
    public string ShortPath;

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
    public int Height;
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
