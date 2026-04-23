using Autodesk.Revit.ApplicationServices;

namespace AutoDocumentation.Services;

/// <summary>
/// Maps Revit's installed UI language to our embedded JSON locale (never user-chosen in the plugin UI).
/// </summary>
public static class RevitLocale
{
    public static string MapToLocaleCode(LanguageType language)
    {
        var name = Enum.GetName(typeof(LanguageType), language) ?? string.Empty;
        if (name.Contains("Portugal", StringComparison.OrdinalIgnoreCase))
            return "pt-PT";
        if (name.Equals("Portuguese", StringComparison.OrdinalIgnoreCase))
            return "pt-PT";

        return language switch
        {
            LanguageType.German => "de-DE",
            LanguageType.Spanish => "es-ES",
            LanguageType.French => "fr-FR",
            LanguageType.Brazilian_Portuguese => "pt-BR",
            LanguageType.English_USA => "en-US",
            LanguageType.English_GB => "en-US",
            _ => "en-US"
        };
    }
}
