using System.Text.RegularExpressions;

namespace ConnectingApps.CustomCloudLogger;

internal static class StringAnalyzer
{
    public static bool IsAlphaNumUnderscore(string str)
    {
        return Regex.IsMatch(str, @"^[a-zA-Z0-9_]+$");
    }

    public static bool IsBase64String(string str)
    {
        str = str.Trim();
        return str.Length % 4 == 0 && Regex.IsMatch(str, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
    }
}