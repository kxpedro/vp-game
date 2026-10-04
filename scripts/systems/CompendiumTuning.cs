using System.Collections.Generic;

public static class CompendiumTuning
{
    private static readonly Dictionary<string, double> NumberOverrides = new();

    public static void SetNumber(string key, double value)
    {
        NumberOverrides[key] = value;
    }

    public static double GetNumber(string key, double originalValue)
    {
        return NumberOverrides.TryGetValue(key, out var value) ? value : originalValue;
    }

    public static float GetFloat(string key, float originalValue)
    {
        return (float)GetNumber(key, originalValue);
    }

    public static int GetInt(string key, int originalValue)
    {
        return System.Math.Max(0, (int)System.Math.Round(GetNumber(key, originalValue)));
    }

    public static void Reset()
    {
        NumberOverrides.Clear();
    }
}
