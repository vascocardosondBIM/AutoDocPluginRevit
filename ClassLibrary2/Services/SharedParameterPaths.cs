using System.IO;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

/// <summary>
/// Resolve o ficheiro JSON persistente por documento (definições de parâmetros partilhados em formato linhas Revit).
/// </summary>
public static class SharedParameterPaths
{
    private const string JsonSidecarSuffix = ".AutoDocumentation.parameters.json";

    private static readonly ConditionalWeakTable<Document, string> UnsavedDocumentPaths = new();

    public static string GetSharedParametersJsonPath(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        if (doc.IsWorkshared)
        {
            try
            {
                var centralGuid = doc.WorksharingCentralGUID;
                if (centralGuid != Guid.Empty)
                {
                    var dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "AutoDocumentation",
                        "por_modelo_central");
                    Directory.CreateDirectory(dir);
                    return Path.Combine(dir, centralGuid.ToString("N") + ".parameters.json");
                }
            }
            catch
            {
                // Continuar com sidecar ou rascunho.
            }
        }

        if (TryGetSidecarPathNextToProject(doc, out var sidecar))
        {
            TryMigrateScratchJsonToSidecar(doc, sidecar);
            return sidecar;
        }

        return UnsavedDocumentPaths.GetValue(doc, CreateScratchJsonPath);
    }

    private static void TryMigrateScratchJsonToSidecar(Document doc, string sidecarJsonPath)
    {
        if (File.Exists(sidecarJsonPath))
            return;

        if (!UnsavedDocumentPaths.TryGetValue(doc, out var scratchPath) || string.IsNullOrEmpty(scratchPath))
            return;

        if (!File.Exists(scratchPath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(sidecarJsonPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(scratchPath, sidecarJsonPath, overwrite: false);
        }
        catch
        {
            // EnsureFileExists criará JSON mínimo no sidecar.
        }
    }

    private static bool TryGetSidecarPathNextToProject(Document doc, out string path)
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
            var safe = SanitizeFileName(baseName);
            if (safe.Length == 0)
                safe = "projeto";

            path = Path.Combine(directory, safe + JsonSidecarSuffix);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CreateScratchJsonPath(Document doc)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoDocumentation",
            "rascunhos");
        Directory.CreateDirectory(root);

        var title = SanitizeFileName(doc.Title?.Trim() ?? "sem_titulo");
        if (title.Length == 0)
            title = "sem_titulo";

        return Path.Combine(root, $"{title}_{Guid.NewGuid():N}.parameters.json");
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name.Trim();
    }
}
