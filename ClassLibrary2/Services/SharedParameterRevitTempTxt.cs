using System.IO;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

/// <summary>
/// Gera um .txt no formato Revit só em pasta temporária, a partir do JSON persistente, e sincroniza de volta para JSON
/// depois de o Revit alterar definições.
/// </summary>
public static class SharedParameterRevitTempTxt
{
    /// <summary>Caminho do ficheiro .txt em cache para este documento (não persistir como cópia do projecto).</summary>
    public static string GetCacheTxtPath(Document doc)
    {
        var jsonPath = SharedParameterPaths.GetSharedParametersJsonPath(doc);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(jsonPath));
        var hash = Convert.ToHexString(hashBytes.AsSpan(0, 8));

        var dir = Path.Combine(Path.GetTempPath(), "AutoDocumentation", "revit_sp");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, hash + ".txt");
    }

    /// <summary>Escreve o conteúdo JSON actual no .txt de cache para o Revit abrir.</summary>
    public static void MaterializeFromJson(Document doc)
    {
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var lines = SharedParameterJsonPersistence.LoadLines(doc);
        WriteLinesToTxtFile(GetCacheTxtPath(doc), lines);
    }

    /// <summary>Lê o .txt que o Revit actualizou, grava no JSON e remove o cache.</summary>
    public static void SyncCacheTxtIntoJson(Document doc)
    {
        var cache = GetCacheTxtPath(doc);
        if (!File.Exists(cache))
            return;

        try
        {
            var lines = SharedParameterJsonPersistence.ReadRawLinesFromTextFile(cache);
            SharedParameterJsonPersistence.SaveLines(doc, lines);
        }
        finally
        {
            TryDeleteQuietly(cache);
        }
    }

    private static void WriteLinesToTxtFile(string path, IReadOnlyList<string> lines)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var body = string.Join("\r\n", lines);
        if (lines.Count > 0 && !body.EndsWith("\r\n", StringComparison.Ordinal))
            body += "\r\n";

        File.WriteAllText(path, body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void TryDeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // O Revit pode manter o ficheiro bloqueado brevemente; na próxima materialização sobrescreve-se.
        }
    }
}
