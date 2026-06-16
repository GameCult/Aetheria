using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Random = Unity.Mathematics.Random;

public class NameTools : EditorWindow
{
    public int NameGeneratorMinLength = 5;
    public int NameGeneratorMaxLength = 10;
    public int NameGeneratorOrder = 4;

    private int _minWordLength = 4;
    private TextAsset _nameFile;
    private MarkovNameGenerator _nameGenerator;
    private bool _stripNumberTokens;
    private Button _generateNameButton;
    private Label _statusLabel;

    [MenuItem("Window/Aetheria/Name Tools")]
    private static void Init()
    {
        var window = GetWindow<NameTools>();
        window.titleContent = new GUIContent("Name Tools");
        window.Show();
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.paddingLeft = 12;
        root.style.paddingRight = 12;
        root.style.paddingTop = 12;
        root.style.paddingBottom = 12;
        root.style.flexDirection = FlexDirection.Column;

        var nameFileField = new ObjectField("Name File")
        {
            objectType = typeof(TextAsset),
            allowSceneObjects = false,
            value = _nameFile
        };
        nameFileField.RegisterValueChangedCallback(evt => _nameFile = evt.newValue as TextAsset);
        root.Add(nameFileField);

        root.Add(CreateIntegerField("Minimum File Word Length", _minWordLength, value => _minWordLength = value));
        root.Add(CreateIntegerField("Generated Minimum Word Length", NameGeneratorMinLength, value => NameGeneratorMinLength = value));
        root.Add(CreateIntegerField("Generated Maximum Word Length", NameGeneratorMaxLength, value => NameGeneratorMaxLength = value));
        root.Add(CreateIntegerField("Generator Order", NameGeneratorOrder, value => NameGeneratorOrder = value));

        var stripNumbersField = new Toggle("Strip Number Tokens")
        {
            value = _stripNumberTokens
        };
        stripNumbersField.RegisterValueChangedCallback(evt => _stripNumberTokens = evt.newValue);
        root.Add(stripNumbersField);

        var cleanButton = new Button(CleanNameFile)
        {
            text = "Clean Name File"
        };
        root.Add(cleanButton);

        var processButton = new Button(ProcessNameFile)
        {
            text = "Process Name File"
        };
        root.Add(processButton);

        _generateNameButton = new Button(GenerateName)
        {
            text = "Generate Name"
        };
        root.Add(_generateNameButton);

        _statusLabel = new Label();
        _statusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        _statusLabel.style.marginTop = 8;
        root.Add(_statusLabel);

        RefreshUiState();
    }

    private IntegerField CreateIntegerField(string label, int value, System.Action<int> onChanged)
    {
        var field = new IntegerField(label)
        {
            value = value
        };
        field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        return field;
    }

    private void CleanNameFile()
    {
        if (_nameFile == null)
        {
            SetStatus("Pick a name file first.");
            return;
        }

        var lines = _nameFile.text.Split('\n');
        var outputPath = Path.Combine(Application.dataPath, _nameFile.name + ".csv");
        using var outputFile = new StreamWriter(outputPath);
        var names = new HashSet<string>();
        foreach (var line in lines)
        {
            var tokens = line.Split(',', ' ');
            foreach (var token in tokens)
            {
                if (HasNonAsciiChars(token))
                    continue;

                var cleaned = new string(token
                    .Where(ch => char.IsLetter(ch) || ch == '-' || ch == '`' || ch == '\'')
                    .ToArray())
                    .Trim()
                    .Trim('`', '-');

                if (_stripNumberTokens && cleaned.Any(char.IsDigit))
                    continue;

                if (cleaned.Length < _minWordLength || !names.Add(cleaned))
                    continue;

                outputFile.WriteLine(cleaned);
            }
        }

        AssetDatabase.Refresh();
        SetStatus($"Wrote {names.Count} cleaned names to {Path.GetFileName(outputPath)}.");
    }

    private void ProcessNameFile()
    {
        if (_nameFile == null)
        {
            SetStatus("Pick a name file first.");
            return;
        }

        var names = new HashSet<string>();
        var lines = _nameFile.text.Split('\n');
        foreach (var line in lines)
        {
            foreach (var word in line.ToUpperInvariant().Split(' ', ',', '.', '"'))
            {
                if (_stripNumberTokens && word.Any(char.IsDigit))
                    continue;

                if (word.Length >= _minWordLength)
                    names.Add(word);
            }
        }

        Debug.Log($"Found {lines.Length} lines, with {names.Count} unique names!");
        var random = new Random(1337);
        _nameGenerator = new MarkovNameGenerator(ref random, names, NameGeneratorOrder, NameGeneratorMinLength, NameGeneratorMaxLength);
        SetStatus($"Processed {names.Count} unique names.");
        RefreshUiState();
    }

    private void GenerateName()
    {
        if (_nameGenerator == null)
        {
            SetStatus("Process a name file before generating names.");
            return;
        }

        var generatedName = _nameGenerator.NextName;
        Debug.Log(generatedName);
        SetStatus($"Generated: {generatedName}");
    }

    private void RefreshUiState()
    {
        _generateNameButton?.SetEnabled(_nameGenerator != null);
    }

    private void SetStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.text = message ?? "";
    }

    private static bool HasNonAsciiChars(string value)
    {
        return System.Text.Encoding.UTF8.GetByteCount(value ?? "") != (value ?? "").Length;
    }
}
