namespace Onyx;

// Простая двуязычка меню: ru/en. T("русский", "english") — по текущему языку.
internal static class OnyxText
{
    internal static bool IsRussian
    {
        get
        {
            string v = OnyxConfig.Language != null ? OnyxConfig.Language.Value : "en";
            return !string.IsNullOrEmpty(v) && v.Trim().ToLowerInvariant() == "ru";
        }
    }

    internal static string T(string ru, string en) => IsRussian ? ru : en;

    internal static string LangName => IsRussian ? "Русский" : "English";

    internal static void Toggle()
    {
        if (OnyxConfig.Language != null) OnyxConfig.Language.Value = IsRussian ? "en" : "ru";
    }
}
