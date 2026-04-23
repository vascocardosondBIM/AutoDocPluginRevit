using Autodesk.Revit.ApplicationServices;

namespace AutoDocumentation.Services;

/// <summary>
/// Localized UI strings loaded from embedded JSON, keyed by Revit's <see cref="LanguageType"/>.
/// </summary>
public static class PluginStrings
{
    private static IReadOnlyDictionary<string, string>? _map;

    public static void Initialize(LanguageType language)
    {
        var locale = RevitLocale.MapToLocaleCode(language);
        _map = PluginStringCatalog.Load(locale);
    }

    public static string T(string key)
    {
        if (_map is not null && _map.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;
        return key;
    }

    public static string Tf(string key, params object?[] args) =>
        string.Format(T(key), args);
}
