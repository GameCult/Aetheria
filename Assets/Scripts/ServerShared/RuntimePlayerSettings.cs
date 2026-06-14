using System;
using System.Collections.Generic;
using System.Globalization;

public class RuntimePlayerSettings
{
    public string Name = "Anonymous";
    public bool TutorialPassed;
    public Dictionary<string, string> HashedStoryFiles = new Dictionary<string, string>();
    public RuntimePlayerGameplaySettings GameplaySettings = new RuntimePlayerGameplaySettings();
    public RuntimePlayerInputSettings InputSettings = new RuntimePlayerInputSettings();
    public RuntimePlayerGraphicsSettings GraphicsSettings = new RuntimePlayerGraphicsSettings();

    public string FormatTemperature(float t)
    {
        return GameplaySettings.TemperatureUnit switch
        {
            TemperatureUnit.Kelvin => $"{Format(t)}°K",
            TemperatureUnit.Celsius => $"{Format(t - 273.15f)}°C",
            TemperatureUnit.Fahrenheit => $"{Format(t * (9f / 5) - 459.67f)}°F",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public float ParseTemperature(string s)
    {
        var t = float.Parse(s);
        return GameplaySettings.TemperatureUnit switch
        {
            TemperatureUnit.Kelvin => t,
            TemperatureUnit.Celsius => t + 273.15f,
            TemperatureUnit.Fahrenheit => (t - 32) * (5f / 9) + 273.15f,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public string Format(float d)
    {
        var magnitude = d == 0.0f ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(d))) + 1;
        var digits = GameplaySettings.SignificantDigits;
        digits -= magnitude;
        if (digits < 0)
            digits = 0;
        var strdec = d.ToString($"N{digits}");
        var dec = Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        return strdec.Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator) ? strdec.TrimEnd('0').TrimEnd(dec) : strdec;
    }
}

public class RuntimePlayerGameplaySettings
{
    public TemperatureUnit TemperatureUnit = TemperatureUnit.Celsius;
    public int SignificantDigits = 3;
}

public class RuntimePlayerGraphicsSettings
{
    public Quality NebulaQuality = Quality.Normal;
    public bool ShowAsteroidsInMinimap;
}

public class RuntimePlayerInputSettings
{
    public Dictionary<(string action, int binding), string> InputActionMap = new Dictionary<(string action, int binding), string>();
    public List<string> ActionBarInputs = new List<string>();

    public void SetBindingOverride(string action, int binding, string inputSystemPath)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action name is required.", nameof(action));
        if (binding < 0)
            throw new ArgumentOutOfRangeException(nameof(binding));
        if (string.IsNullOrWhiteSpace(inputSystemPath))
            throw new ArgumentException("Input path is required.", nameof(inputSystemPath));

        InputActionMap[(action, binding)] = inputSystemPath;
    }

    public void SetActionBarInputEnabled(string inputSystemPath, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(inputSystemPath))
            throw new ArgumentException("Input path is required.", nameof(inputSystemPath));

        var index = ActionBarInputs.IndexOf(inputSystemPath);
        if (enabled)
        {
            if (index < 0)
                ActionBarInputs.Add(inputSystemPath);
        }
        else if (index >= 0)
        {
            ActionBarInputs.RemoveAt(index);
        }
    }
}

public enum Quality
{
    Low,
    Normal,
    High,
    Ultra
}
