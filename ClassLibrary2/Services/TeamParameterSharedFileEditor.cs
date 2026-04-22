using System.IO;
using System.Text;
using AutoDocumentation.Models;
using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

/// <summary>
/// Operações sobre as linhas de parâmetros partilhados (grupo «ParametrosEquipa»); persistência em JSON.
/// </summary>
public static class TeamParameterSharedFileEditor
{
    /// <summary>Nome → token de tipo de dados (TEXT, INTEGER, …) para PARAM do grupo «ParametrosEquipa» no JSON.</summary>
    public static IReadOnlyDictionary<string, string> GetTeamGroupParameterDataTypeTokensByName(Document doc)
    {
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var lines = SharedParameterJsonPersistence.LoadLines(doc);
        return GetTeamGroupParameterDataTypeTokensByNameFromLines(lines);
    }

    internal static IReadOnlyDictionary<string, string> GetTeamGroupParameterDataTypeTokensByNameFromLines(
        IReadOnlyList<string> lines)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!TryResolveTeamGroupNumericIdFromLines(lines, out var groupId))
            return dict;

        foreach (var line in lines)
        {
            if (!TryParseParamLine(line, out var parts) || parts.Length < 6)
                continue;

            if (!ParamLineGroupMatches(parts, groupId))
                continue;

            var name = parts[2].Trim();
            if (name.Length == 0)
                continue;

            dict[name] = parts[3].Trim();
        }

        return dict;
    }

    /// <summary>GUIDs das definições PARAM do grupo «ParametrosEquipa».</summary>
    public static bool TryGetGuidsForTeamParameterParameters(Document doc, out HashSet<Guid> guids)
    {
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        return TryGetGuidsForTeamParameterParametersFromLines(SharedParameterJsonPersistence.LoadLines(doc), out guids);
    }

    public static bool TryGetGuidsForTeamParameterParametersFromLines(IReadOnlyList<string> lines, out HashSet<Guid> guids)
    {
        guids = new HashSet<Guid>();
        if (lines.Count == 0)
            return false;

        try
        {
            if (!TryResolveTeamGroupNumericIdFromLines(lines, out var groupId))
                return false;

            foreach (var line in lines)
            {
                if (!TryParseParamLine(line, out var parts) || parts.Length < 6)
                    continue;

                if (!ParamLineGroupMatches(parts, groupId))
                    continue;

                if (Guid.TryParse(parts[1].Trim().Trim('{', '}'), out var g))
                    guids.Add(g);
            }

            return guids.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Remove a linha PARAM com o GUID indicado (qualquer grupo) do JSON do documento.</summary>
    public static bool TryRemoveDefinitionByGuid(Document doc, Guid definitionGuid, out string error)
    {
        error = string.Empty;
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var lines = SharedParameterJsonPersistence.LoadLines(doc);
        if (!TryRemoveDefinitionByGuidFromLines(lines, definitionGuid, out var kept, out error))
            return false;

        SharedParameterJsonPersistence.SaveLines(doc, kept);
        return true;
    }

    private static bool TryRemoveDefinitionByGuidFromLines(
        List<string> lines,
        Guid definitionGuid,
        out List<string> kept,
        out string error)
    {
        kept = new List<string>(lines.Count);
        error = string.Empty;
        var guidForms = GuidMatchForms(definitionGuid);
        var removed = false;
        foreach (var line in lines)
        {
            if (IsParamLineForGuid(line, guidForms))
            {
                removed = true;
                continue;
            }

            kept.Add(line);
        }

        if (!removed)
        {
            error = "Não foi encontrada a definição no ficheiro partilhado (GUID).";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Se existir no grupo «ParametrosEquipa», devolve o tipo de dados tal como aparece no ficheiro (TEXT, NUMBER, …).
    /// </summary>
    public static bool TryGetTeamParameterDataTypeInFile(Document doc, string parameterName, out string dataTypeToken)
    {
        dataTypeToken = string.Empty;
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var lines = SharedParameterJsonPersistence.LoadLines(doc);
        return TryGetTeamParameterDataTypeFromLines(lines, parameterName, out dataTypeToken, out _);
    }

    private static bool TryGetTeamParameterDataTypeFromLines(
        IReadOnlyList<string> lines,
        string parameterName,
        out string dataTypeToken,
        out string error)
    {
        dataTypeToken = string.Empty;
        error = string.Empty;
        if (!TryResolveTeamGroupNumericIdFromLines(lines, out var groupId))
        {
            error = $"Grupo «{TeamParameterConstants.DefinitionGroupName}» não encontrado no ficheiro.";
            return false;
        }

        foreach (var line in lines)
        {
            if (!TryParseParamLine(line, out var parts))
                continue;

            if (parts.Length < 6)
                continue;

            if (!ParamLineGroupMatches(parts, groupId))
                continue;

            if (!string.Equals(parts[2].Trim(), parameterName.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            dataTypeToken = parts[3].Trim();
            return true;
        }

        return false;
    }

    internal static bool TryResolveTeamGroupNumericIdFromLines(IReadOnlyList<string> lines, out string groupId)
    {
        groupId = string.Empty;
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (!t.StartsWith("GROUP\t", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = t.Split('\t');
            if (parts.Length < 3)
                continue;

            if (!string.Equals(parts[2].Trim(), TeamParameterConstants.DefinitionGroupName, StringComparison.Ordinal))
                continue;

            groupId = parts[1].Trim();
            return true;
        }

        return false;
    }

    internal static bool ParamLineGroupMatches(string[] parts, string groupId)
    {
        if (parts.Length > 5 && string.Equals(parts[5].Trim(), groupId, StringComparison.Ordinal))
            return true;
        if (parts.Length > 6 && string.Equals(parts[6].Trim(), groupId, StringComparison.Ordinal))
            return true;
        return false;
    }

    internal static bool TryParseParamLine(string line, out string[] parts)
    {
        parts = Array.Empty<string>();
        var t = line.Trim();
        if (!t.StartsWith("PARAM\t", StringComparison.OrdinalIgnoreCase))
            return false;

        parts = t.Split('\t');
        return parts.Length > 0;
    }

    private static bool IsParamLineForGuid(string line, string[] guidForms)
    {
        if (!TryParseParamLine(line, out var parts) || parts.Length < 2)
            return false;

        var cell = parts[1].Trim();
        foreach (var g in guidForms)
        {
            if (string.Equals(cell, g, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string[] GuidMatchForms(Guid g)
    {
        return
        [
            g.ToString("D"),
            g.ToString("D").ToUpperInvariant(),
            g.ToString("B"),
            g.ToString("B").ToUpperInvariant(),
            g.ToString("P"),
            g.ToString("P").ToUpperInvariant()
        ];
    }
}
