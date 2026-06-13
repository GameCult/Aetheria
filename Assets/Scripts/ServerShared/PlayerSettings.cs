using System;
using System.Collections.Generic;
using System.Globalization;

public class PlayerSettings
{
    public string Name = "Anonymous";
    public bool TutorialPassed;
    public Dictionary<string, string> HashedStoryFiles = new Dictionary<string, string>();
    public PlayerGameplaySettings GameplaySettings = new PlayerGameplaySettings();
    public PlayerInputSettings InputSettings = new PlayerInputSettings();
    public PlayerGraphicsSettings GraphicsSettings = new PlayerGraphicsSettings();

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

public class PlayerGameplaySettings
{
    public TemperatureUnit TemperatureUnit = TemperatureUnit.Celsius;
    public int SignificantDigits = 3;
}

public class PlayerGraphicsSettings
{
    public Quality NebulaQuality = Quality.Normal;
    public bool ShowAsteroidsInMinimap;
}

public class PlayerInputSettings
{
    public Dictionary<(string action, int binding), string> InputActionMap = new Dictionary<(string action, int binding), string>();
    public List<string> ActionBarInputs = new List<string>();
}

public enum Quality
{
    Low,
    Normal,
    High,
    Ultra
}
