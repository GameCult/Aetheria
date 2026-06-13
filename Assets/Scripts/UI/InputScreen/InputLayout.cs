using System.Collections.Generic;

public class InputLayout
{
    public InputLayoutRow[] Rows;

    public static InputLayout CreateAnsi104()
    {
        return new InputLayout
        {
            Rows = new InputLayoutRow[]
            {
                Row(
                    Key("Esc"), Spacer(1), Key("F1"), Key("F2"), Key("F3"), Key("F4"), Spacer(.5f),
                    Key("F5"), Key("F6"), Key("F7"), Key("F8"), Spacer(.5f),
                    Key("F9"), Key("F10"), Key("F11"), Key("F12"), Spacer(.25f),
                    Key(""), Key(""), Key("")),
                RowSpacer(.5f),
                Row(
                    Key("~\n`"), Key("!\n1"), Key("@\n2"), Key("#\n3"), Key("$\n4"), Key("%\n5"), Key("^\n6"),
                    Key("&\n7"), Key("*\n8"), Key("(\n9"), Key(")\n0"), Key("_\n-"), Key("+\n="), Key("Backspace", 2),
                    Spacer(.25f), Key("Insert"), Key("Home"), Key("PgUp"), Spacer(.25f), Key(""), Key("/"), Key("*"), Key("-")),
                Row(
                    Key("Tab", 1.5f), Key("Q"), Key("W"), Key("E"), Key("R"), Key("T"), Key("Y"), Key("U"), Key("I"), Key("O"), Key("P"),
                    Key("{\n["), Key("}\n]"), Key("|\n\\", 1.5f), Spacer(.25f),
                    Key("Delete"), Key("End"), Key("PgDn"), Spacer(.25f), Key("Home\n7"), Key("Up\n8"), Key("PgUp\n9"), Key("+", 1, 2)),
                Row(
                    Key("Caps Lock", 1.75f), Key("A"), Key("S"), Key("D"), Key("F"), Key("G"), Key("H"), Key("J"), Key("K"), Key("L"),
                    Key(":\n;"), Key("\"\n'"), Key("Enter", 2.25f), Spacer(3.5f), Key("Left\n4"), Key("5"), Key("Right\n6")),
                Row(
                    Key("Shift", 2.25f), Key("Z"), Key("X"), Key("C"), Key("V"), Key("B"), Key("N"), Key("M"),
                    Key("<\n,"), Key(">\n."), Key("?\n/"), Key("Shift", 2.75f), Spacer(1.25f), Key("Up"), Spacer(1.25f),
                    Key("End\n1"), Key("Down\n2"), Key("PgDn\n3"), Key("Enter", 1, 2)),
                Row(
                    Key("Ctrl", 1.25f), Key("", 1.25f), Key("Alt", 1.25f), Key("Spacebar", 6.25f), Key("Alt", 1.25f),
                    Key("", 1.25f), Key("", 1.25f), Key("Ctrl", 1.25f), Spacer(.25f),
                    Key("Left"), Key("Down"), Key("Right"), Spacer(.25f), Key("Ins\n0", 2), Key("Del\n."))
            }
        };
    }

    private static InputLayoutRowSpacer RowSpacer(float height)
    {
        return new InputLayoutRowSpacer {Height = height};
    }

    private static InputLayoutKeyRow Row(params InputLayoutColumn[] columns)
    {
        return new InputLayoutKeyRow {Columns = columns};
    }

    private static InputLayoutColumnSpacer Spacer(float width)
    {
        return new InputLayoutColumnSpacer {Width = width};
    }

    private static InputLayoutKey Key(string label, float width = 1f, int height = 1)
    {
        InputLayoutKey key;
        var trimmedLabel = label.Trim();
        if (!string.IsNullOrEmpty(trimmedLabel))
        {
            var labels = trimmedLabel.Split('\n');
            key = height != 1 ? new InputLayoutMultiRowKey {Height = height} : new InputLayoutBindableKey();
            ((InputLayoutBindableKey) key).MainLabel = labels.Length == 2 ? labels[1] : trimmedLabel;
            ((InputLayoutBindableKey) key).AltLabel = labels.Length == 2 ? labels[0] : "";
        }
        else
        {
            key = new InputLayoutKey();
        }

        key.Width = width;
        return key;
    }

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
