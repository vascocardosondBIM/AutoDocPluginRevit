using System.IO;
using System.Reflection;
using System.Text.Json;

namespace AutoDocumentation.Services;

internal static class PluginStringCatalog
{
    private const string FallbackLocale = "en-US";

    public static IReadOnlyDictionary<string, string> Load(string localeCode)
    {
        var asm = Assembly.GetExecutingAssembly();
        var tried = new List<string>();

        foreach (var code in DistinctLocaleCandidates(localeCode))
        {
            var resourceName = $"AutoDocumentation.i18n.{code}.json";
            tried.Add(resourceName);
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null)
                continue;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict is not null && dict.Count > 0)
                return dict;
        }

        throw new InvalidOperationException(
            "Missing embedded i18n resources. Tried: " + string.Join(", ", tried));
    }

    private static IEnumerable<string> DistinctLocaleCandidates(string localeCode)
    {
        var trimmed = localeCode.Trim();
        if (trimmed.Length > 0)
            yield return trimmed;

        if (!string.Equals(trimmed, FallbackLocale, StringComparison.OrdinalIgnoreCase))
            yield return FallbackLocale;
    }
}
