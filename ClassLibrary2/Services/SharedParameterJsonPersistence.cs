using System.IO;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

/// <summary>
/// Grava e lê o armazenamento persistente em JSON; o Revit continua a usar um .txt gerado em cache temporário.
/// </summary>
public static class SharedParameterJsonPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private const string LegacySidecarTxtSuffix = ".AutoDocumentation.shared_parameters.txt";

    public static void EnsureFileExists(Document doc)
    {
        var jsonPath = SharedParameterPaths.GetSharedParametersJsonPath(doc);
        var dir = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(jsonPath))
            return;

        if (TryMigrateFromLegacyTxtSidecar(doc, jsonPath))
            return;

        if (TryMigrateFromLegacyCentralTxt(doc, jsonPath))
            return;

        SaveDocument(doc, CreateMinimalDocument());
    }

    private static bool TryMigrateFromLegacyCentralTxt(Document doc, string jsonPath)
    {
        if (!doc.IsWorkshared)
            return false;

        Guid g;
        try
        {
            g = doc.WorksharingCentralGUID;
        }
        catch
        {
            return false;
        }

        if (g == Guid.Empty)
            return false;

        var dir = Path.GetDirectoryName(jsonPath);
        if (string.IsNullOrEmpty(dir))
            return false;

        var legacy = Path.Combine(dir, g.ToString("N") + ".shared_parameters.txt");
        if (!File.Exists(legacy))
            return false;

        try
        {
            var lines = ReadRawLinesFromTextFile(legacy);
            if (lines.Count == 0)
                return false;

            SaveDocument(doc, new SharedParametersJsonDocument { Lines = lines });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static SharedParametersJsonDocument LoadDocument(Document doc)
    {
        EnsureFileExists(doc);
        var jsonPath = SharedParameterPaths.GetSharedParametersJsonPath(doc);
        string text;
        try
        {
            text = File.ReadAllText(jsonPath, Encoding.UTF8);
        }
        catch
        {
            return CreateMinimalDocument();
        }

        try
        {
            var model = JsonSerializer.Deserialize<SharedParametersJsonDocument>(text, JsonOptions)
                        ?? CreateMinimalDocument();
            model.Lines ??= new List<string>();
            return model;
        }
        catch (JsonException)
        {
            TryQuarantineCorruptJson(jsonPath);
            var fresh = CreateMinimalDocument();
            try
            {
                SaveDocument(doc, fresh);
            }
            catch
            {
                /* ignorar: próxima operação tentará de novo */
            }

            return fresh;
        }
    }

    private static void TryQuarantineCorruptJson(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
                return;

            var bak = jsonPath + ".invalid." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".bak";
            File.Move(jsonPath, bak, overwrite: false);
        }
        catch
        {
            /* ignorar */
        }
    }

    public static List<string> LoadLines(Document doc)
    {
        var model = LoadDocument(doc);
        return model.Lines.Count > 0 ? new List<string>(model.Lines) : new List<string>(CreateMinimalDocument().Lines);
    }

    public static void SaveDocument(Document doc, SharedParametersJsonDocument data)
    {
        var jsonPath = SharedParameterPaths.GetSharedParametersJsonPath(doc);
        var dir = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(jsonPath, json, Encoding.UTF8);
    }

    public static void SaveLines(Document doc, IReadOnlyList<string> lines)
    {
        var model = new SharedParametersJsonDocument { Lines = lines.ToList() };
        SaveDocument(doc, model);
    }

    public static SharedParametersJsonDocument CreateMinimalDocument() =>
        new()
        {
            Lines =
            [
                "# Revit Shared Parameter File",
                "*META\tVERSION\tMINVERSION",
                "META\t2\t1"
            ]
        };

    private static bool TryMigrateFromLegacyTxtSidecar(Document doc, string jsonPath)
    {
        if (!TryGetLegacyTxtSidecarPath(doc, out var legacyTxt) || !File.Exists(legacyTxt))
            return false;

        try
        {
            var lines = ReadRawLinesFromTextFile(legacyTxt);
            if (lines.Count == 0)
                return false;

            SaveDocument(doc, new SharedParametersJsonDocument { Lines = lines });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetLegacyTxtSidecarPath(Document doc, out string path)
    {
        path = string.Empty;
        var pathName = doc.PathName?.Trim() ?? string.Empty;
        if (pathName.Length == 0)
            return false;

        try
        {
            if (!Path.IsPathRooted(pathName))
                return false;

            var directory = Path.GetDirectoryName(pathName);
            if (string.IsNullOrEmpty(directory))
                return false;

            var baseName = Path.GetFileNameWithoutExtension(pathName);
            foreach (var c in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(c, '_');

            baseName = baseName.Trim();
            if (baseName.Length == 0)
                baseName = "projeto";

            path = Path.Combine(directory, baseName + LegacySidecarTxtSuffix);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static List<string> ReadRawLinesFromTextFile(string path)
    {
        using var sr = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        var text = sr.ReadToEnd();
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n', StringSplitOptions.None).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }
}
