namespace Onyx;

internal static class OnyxText
{
    private static string _langRaw;
    private static bool _langRu;

    internal static bool IsRussian
    {
        get
        {
            string v = OnyxConfig.Language != null ? OnyxConfig.Language.Value : "en";
            if (v != _langRaw)
            {
                _langRaw = v;
                _langRu = !string.IsNullOrEmpty(v) && v.Trim().ToLowerInvariant() == "ru";
            }
            return _langRu;
        }
    }

    internal static string T(string ru, string en) => IsRussian ? ru : en;

    internal static string LangName => IsRussian ? "Русский" : "English";

    internal static void Toggle()
    {
        if (OnyxConfig.Language != null) OnyxConfig.Language.Value = IsRussian ? "en" : "ru";
    }
}
