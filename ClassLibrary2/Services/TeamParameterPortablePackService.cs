using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

/// <summary>
/// Exporta e importa um subconjunto de parâmetros «ParametrosEquipa» em JSON (mesmo formato em ambos os sentidos).
/// </summary>
public static class TeamParameterPortablePackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static bool TryBuildPackFromParameterNames(
        Document doc,
        IReadOnlyCollection<string> parameterNames,
        out TeamParameterPortablePack pack,
        out string error)
    {
        pack = new TeamParameterPortablePack { ExportedAtUtc = DateTime.UtcNow };
        error = string.Empty;

        if (parameterNames.Count == 0)
        {
            error = "Seleccione pelo menos um parâmetro na grelha.";
            return false;
        }

        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var lines = SharedParameterJsonPersistence.LoadLines(doc);
        if (!TeamParameterSharedFileEditor.TryResolveTeamGroupNumericIdFromLines(lines, out var sourceGroupId))
        {
            error =
                $"Não foi encontrado o grupo «{TeamParameterConstants.DefinitionGroupName}» no ficheiro de parâmetros do projecto.";
            return false;
        }

        pack.SourceGroupId = sourceGroupId;
        var wanted = new HashSet<string>(parameterNames.Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (!TeamParameterSharedFileEditor.TryParseParamLine(line, out var parts) || parts.Length < 6)
                continue;

            if (!TeamParameterSharedFileEditor.ParamLineGroupMatches(parts, sourceGroupId))
                continue;

            var name = parts[2].Trim();
            if (name.Length == 0 || !wanted.Contains(name))
                continue;

            var token = parts.Length > 3 ? parts[3].Trim() : "TEXT";
            if (!Guid.TryParse(parts[1].Trim().Trim('{', '}'), out var g))
            {
                error = $"Linha PARAM inválida para «{name}» (GUID).";
                return false;
            }

            if (!TeamParameterSharedFileKindMapper.TryParseDataTypeToken(token, out var kind))
                kind = TeamParameterKind.Text;

            pack.Definitions.Add(new TeamParameterPortableEntry
            {
                ParameterName = name,
                TeamParameterKind = kind,
                DefinitionGuid = g.ToString("D"),
                DataTypeToken = token,
                ParamLine = line.TrimEnd('\r', '\n')
            });
        }

        if (pack.Definitions.Count == 0)
        {
            error = "Não foi possível localizar definições no ficheiro partilhado para os nomes seleccionados.";
            return false;
        }

        if (pack.Definitions.Count != wanted.Count)
        {
            var found = pack.Definitions.Select(d => d.ParameterName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = wanted.Where(n => !found.Contains(n)).ToList();
            error = "Nem todos os parâmetros seleccionados existem no ficheiro partilhado do projecto: " +
                    string.Join(", ", missing);
            return false;
        }

        return true;
    }

    public static bool TryWritePackToPath(TeamParameterPortablePack pack, string filePath, out string error)
    {
        error = string.Empty;
        try
        {
            var json = JsonSerializer.Serialize(pack, JsonOptions);
            File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryReadPackFromPath(string filePath, out TeamParameterPortablePack? pack, out string error)
    {
        pack = null;
        error = string.Empty;
        try
        {
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            var model = JsonSerializer.Deserialize<TeamParameterPortablePack>(text, JsonOptions);
            if (model is null || model.Definitions is null || model.Definitions.Count == 0)
            {
                error = "O ficheiro não contém definições válidas.";
                return false;
            }

            if (!string.Equals(model.PackKind, "ParametrosEquipa.v1", StringComparison.OrdinalIgnoreCase))
            {
                error = "Formato de pacote não reconhecido (packKind).";
                return false;
            }

            foreach (var d in model.Definitions)
            {
                if (string.IsNullOrWhiteSpace(d.ParamLine) || string.IsNullOrWhiteSpace(d.ParameterName))
                {
                    error = "Entrada inválida: falta paramLine ou parameterName.";
                    return false;
                }
            }

            pack = model;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Junta as definições ao JSON do documento e associa-as às categorias dos elementos indicados.
    /// </summary>
    public static bool TryImportPack(
        Document doc,
        TeamParameterPortablePack pack,
        IReadOnlyList<Element> elements,
        out string summary)
    {
        var log = new StringBuilder();
        if (elements.Count == 0)
        {
            summary = "É necessária uma selecção de elementos para determinar as categorias.";
            return false;
        }

        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var lines = SharedParameterJsonPersistence.LoadLines(doc);
        EnsureTeamGroupLineExists(lines, out var targetGroupId);

        var existingGuids = CollectParamGuidsFromLines(lines);
        var existingNameToGuid = CollectParamNameToGuidFromLines(lines);
        var skipBinding = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in pack.Definitions)
        {
            if (!Guid.TryParse(entry.DefinitionGuid.Trim(), out var importGuid))
            {
                log.AppendLine($"• «{entry.ParameterName}»: GUID inválido — ignorado.");
                skipBinding.Add(entry.ParameterName);
                continue;
            }

            if (existingNameToGuid.TryGetValue(entry.ParameterName, out var otherGuid) && otherGuid != importGuid)
            {
                log.AppendLine(
                    $"• «{entry.ParameterName}»: já existe no projecto com outro GUID — conflito, ignorado.");
                skipBinding.Add(entry.ParameterName);
                continue;
            }

            if (existingGuids.Contains(importGuid))
            {
                log.AppendLine($"• «{entry.ParameterName}»: GUID já presente no ficheiro (só actualização de binding).");
                continue;
            }

            var rewritten = RewriteParamLineGroupId(entry.ParamLine, pack.SourceGroupId, targetGroupId);
            if (rewritten is null)
            {
                log.AppendLine($"• «{entry.ParameterName}»: linha PARAM inválida — ignorado.");
                skipBinding.Add(entry.ParameterName);
                continue;
            }

            lines.Add(rewritten);
            existingGuids.Add(importGuid);
            existingNameToGuid[entry.ParameterName] = importGuid;
            log.AppendLine($"• «{entry.ParameterName}»: definição acrescentada ao ficheiro do projecto.");
        }

        SharedParameterJsonPersistence.SaveLines(doc, lines);

        try
        {
            using var tx = new Transaction(doc, "Assistente de parâmetros: importar pacote");
            tx.Start();
            foreach (var entry in pack.Definitions)
            {
                if (skipBinding.Contains(entry.ParameterName))
                    continue;

                if (!Guid.TryParse(entry.DefinitionGuid.Trim(), out _))
                    continue;

                try
                {
                    TeamParameterBootstrapper.EnsureBoundInstanceParameter(doc, entry.ParameterName,
                        entry.TeamParameterKind, elements);
                }
                catch (Exception ex)
                {
                    log.AppendLine($"• «{entry.ParameterName}»: binding — {ex.Message}");
                }
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            summary = "Erro ao associar parâmetros ao projecto: " + ex.Message;
            return false;
        }

        summary = log.ToString().TrimEnd();
        return true;
    }

    private static void EnsureTeamGroupLineExists(List<string> lines, out string groupId)
    {
        if (TeamParameterSharedFileEditor.TryResolveTeamGroupNumericIdFromLines(lines, out groupId))
            return;

        var max = 0;
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (!t.StartsWith("GROUP\t", StringComparison.OrdinalIgnoreCase))
                continue;

            var p = t.Split('\t');
            if (p.Length < 2)
                continue;

            if (int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                max = Math.Max(max, id);
        }

        var next = (max + 1).ToString(CultureInfo.InvariantCulture);
        var insertAt = lines.Count;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("PARAM\t", StringComparison.OrdinalIgnoreCase))
            {
                insertAt = i;
                break;
            }
        }

        lines.Insert(insertAt, $"GROUP\t{next}\t{TeamParameterConstants.DefinitionGroupName}");
        groupId = next;
    }

    private static string? RewriteParamLineGroupId(string paramLine, string? sourceGroupId, string targetGroupId)
    {
        if (!TeamParameterSharedFileEditor.TryParseParamLine(paramLine, out var parts) || parts.Length < 6)
            return null;

        var cells = parts.ToArray();
        var replaced = false;
        for (var i = 5; i < Math.Min(cells.Length, 7); i++)
        {
            var cell = cells[i].Trim();
            if (string.IsNullOrEmpty(sourceGroupId))
            {
                if (i == 5)
                {
                    cells[i] = targetGroupId;
                    replaced = true;
                }

                continue;
            }

            if (string.Equals(cell, sourceGroupId.Trim(), StringComparison.Ordinal))
            {
                cells[i] = targetGroupId;
                replaced = true;
            }
        }

        if (!replaced && cells.Length > 5)
            cells[5] = targetGroupId;

        return string.Join("\t", cells);
    }

    private static HashSet<Guid> CollectParamGuidsFromLines(IReadOnlyList<string> lines)
    {
        var set = new HashSet<Guid>();
        foreach (var line in lines)
        {
            if (!TeamParameterSharedFileEditor.TryParseParamLine(line, out var parts) || parts.Length < 2)
                continue;

            if (Guid.TryParse(parts[1].Trim().Trim('{', '}'), out var g))
                set.Add(g);
        }

        return set;
    }

    private static Dictionary<string, Guid> CollectParamNameToGuidFromLines(IReadOnlyList<string> lines)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (!TeamParameterSharedFileEditor.TryResolveTeamGroupNumericIdFromLines(lines, out var gid))
            return map;

        foreach (var line in lines)
        {
            if (!TeamParameterSharedFileEditor.TryParseParamLine(line, out var parts) || parts.Length < 3)
                continue;

            if (!TeamParameterSharedFileEditor.ParamLineGroupMatches(parts, gid))
                continue;

            var name = parts[2].Trim();
            if (name.Length == 0)
                continue;

            if (Guid.TryParse(parts[1].Trim().Trim('{', '}'), out var g))
                map[name] = g;
        }

        return map;
    }
}
