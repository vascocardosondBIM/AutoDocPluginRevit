using System.IO;
using Autodesk.Revit.DB;
using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

/// <summary>
/// Encontra parâmetros de instância do grupo «ParametrosEquipa» ligados ao projecto e presentes em todos os elementos da selecção.
/// </summary>
public static class TeamParameterDiscoveryService
{
    /// <summary>
    /// Parâmetros «ParametrosEquipa» que ainda não estão disponíveis em todos os elementos seleccionados
    /// (só no ficheiro, ou no projecto mas sem categorias da selecção / sem instância em algum elemento).
    /// </summary>
    public static IReadOnlyList<AssociableTeamParameterInfo> GetAssociableTeamParameters(
        Document doc,
        IReadOnlyList<Element> elements)
    {
        if (elements.Count == 0)
            return Array.Empty<AssociableTeamParameterInfo>();

        try
        {
            SharedParameterJsonPersistence.EnsureFileExists(doc);
            var fromFile = TeamParameterSharedFileEditor.GetTeamGroupParameterDataTypeTokensByName(doc);
            var managed = GetManagedInstanceBoundParameterNames(doc);
            var allNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in managed)
                allNames.Add(n);
            foreach (var n in fromFile.Keys)
                allNames.Add(n);

            var result = new List<AssociableTeamParameterInfo>();
            foreach (var name in allNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                if (TryDescribeCommonParameter(elements, name, out _, out _, out _))
                    continue;

                var kind = ResolveTeamParameterKindForName(doc, name, fromFile);
                result.Add(new AssociableTeamParameterInfo(name, kind));
            }

            return result;
        }
        catch
        {
            return Array.Empty<AssociableTeamParameterInfo>();
        }
    }

    private static TeamParameterKind ResolveTeamParameterKindForName(
        Document doc,
        string name,
        IReadOnlyDictionary<string, string> fromFileTokens)
    {
        var ext = TeamParameterDefinitionResolver.TryGetBoundTeamParameter(doc, name);
        if (ext is not null)
            return TeamParameterKindMapping.MapFromSpecTypeId(ext.GetDataType());

        if (fromFileTokens.TryGetValue(name, out var tok) &&
            TeamParameterSharedFileKindMapper.TryParseDataTypeToken(tok, out var k))
            return k;

        return TeamParameterKind.Text;
    }

    public static IReadOnlyList<TeamParameterRowModel> GetCommonEditableRows(Document doc, IReadOnlyList<Element> elements)
    {
        if (elements.Count == 0)
            return Array.Empty<TeamParameterRowModel>();

        var managedNames = GetManagedInstanceBoundParameterNames(doc);
        if (managedNames.Count == 0)
            return Array.Empty<TeamParameterRowModel>();

        var rows = new List<TeamParameterRowModel>();
        foreach (var name in managedNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryDescribeCommonParameter(elements, name, out var kind, out var storage, out var commonHint))
                continue;

            rows.Add(new TeamParameterRowModel(name, kind, storage, commonHint));
        }

        return rows;
    }

    /// <summary>
    /// O Revit nem sempre preenche <see cref="ExternalDefinition.OwnerGroup"/> nas chaves devolvidas por
    /// <see cref="BindingMap"/>, pelo que o filtro por nome de grupo falhava e a lista ficava vazia.
    /// Enumeramos o ficheiro de parâmetros partilhados (grupo «ParametrosEquipa») e verificamos o binding no documento.
    /// </summary>
    private static HashSet<string> GetManagedInstanceBoundParameterNames(Document doc)
    {
        var combined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in TryCollectNamesFromSharedParameterFile(doc))
            combined.Add(n);
        foreach (var n in CollectNamesFromBindingsWithOwnerGroupFallback(doc))
            combined.Add(n);
        foreach (var n in CollectNamesFromBindingsMatchingTeamFileGuids(doc))
            combined.Add(n);
        return combined;
    }

    /// <summary>
    /// Quando o ficheiro partilhado deixa de abrir via API, ainda conseguimos listar parâmetros cujo GUID está no ficheiro «ParametrosEquipa».
    /// </summary>
    private static HashSet<string> CollectNamesFromBindingsMatchingTeamFileGuids(Document doc)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var jsonPath = SharedParameterPaths.GetSharedParametersJsonPath(doc);
        if (!File.Exists(jsonPath))
            return result;

        if (!TeamParameterSharedFileEditor.TryGetGuidsForTeamParameterParameters(doc, out var teamGuids) ||
            teamGuids.Count == 0)
            return result;

        var map = doc.ParameterBindings;
        var it = map.ForwardIterator();
        it.Reset();
        while (it.MoveNext())
        {
            if (it.Key is not ExternalDefinition ext)
                continue;

            if (map.get_Item(ext) is not InstanceBinding)
                continue;

            if (!teamGuids.Contains(ext.GUID))
                continue;

            result.Add(ext.Name);
        }

        return result;
    }

    private static HashSet<string> TryCollectNamesFromSharedParameterFile(Document doc)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var jsonPath = SharedParameterPaths.GetSharedParametersJsonPath(doc);
        if (!File.Exists(jsonPath))
            return result;

        SharedParameterRevitTempTxt.MaterializeFromJson(doc);
        var cachePath = SharedParameterRevitTempTxt.GetCacheTxtPath(doc);
        if (!File.Exists(cachePath))
            return result;

        var map = doc.ParameterBindings;
        var app = doc.Application;
        try
        {
            SharedParameterFileLoader.TryExecuteWithTemporarySharedParameterPath(app, cachePath, defFile =>
            {
                if (defFile is null)
                    return;

                DefinitionGroup? teamGroup = null;
                foreach (DefinitionGroup g in defFile.Groups)
                {
                    if (string.Equals(g.Name, TeamParameterConstants.DefinitionGroupName, StringComparison.Ordinal))
                    {
                        teamGroup = g;
                        break;
                    }
                }

                if (teamGroup is null)
                    return;

                foreach (Definition def in teamGroup.Definitions)
                {
                    if (def is not ExternalDefinition fileExt)
                        continue;

                    var bound = ResolveBoundExternalDefinition(map, fileExt);
                    if (bound is null)
                        continue;

                    if (map.get_Item(bound) is not InstanceBinding)
                        continue;

                    result.Add(bound.Name);
                }
            });
        }
        catch
        {
            // Não limpar: manter entradas já recolhidas; o conjunto final junta também bindings e GUIDs do ficheiro.
        }

        return result;
    }

    /// <summary>
    /// O mapa de bindings pode não reconhecer a mesma instância de <see cref="ExternalDefinition"/> que veio do ficheiro;
    /// alinhamos por GUID (e, em último caso, por nome).
    /// </summary>
    private static ExternalDefinition? ResolveBoundExternalDefinition(BindingMap map, ExternalDefinition fromFile)
    {
        if (map.Contains(fromFile))
            return fromFile;

        var targetGuid = fromFile.GUID;
        var targetName = fromFile.Name;
        var it = map.ForwardIterator();
        it.Reset();
        while (it.MoveNext())
        {
            if (it.Key is not ExternalDefinition docExt)
                continue;

            if (docExt.GUID == targetGuid)
                return docExt;

            if (string.Equals(docExt.Name, targetName, StringComparison.Ordinal))
                return docExt;
        }

        return null;
    }

    private static HashSet<string> CollectNamesFromBindingsWithOwnerGroupFallback(Document doc)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var map = doc.ParameterBindings;
        var it = map.ForwardIterator();
        it.Reset();
        while (it.MoveNext())
        {
            if (it.Key is not ExternalDefinition ext)
                continue;

            if (!string.Equals(ext.OwnerGroup?.Name, TeamParameterConstants.DefinitionGroupName, StringComparison.Ordinal))
                continue;

            if (map.get_Item(ext) is not InstanceBinding)
                continue;

            result.Add(ext.Name);
        }

        return result;
    }

    private static bool TryDescribeCommonParameter(
        IReadOnlyList<Element> elements,
        string parameterName,
        out TeamParameterKind kind,
        out StorageType storageType,
        out string? commonHint)
    {
        kind = TeamParameterKind.Text;
        storageType = StorageType.String;
        commonHint = null;

        Parameter? first = null;
        foreach (var el in elements)
        {
            var p = ResolveInstanceParameter(el, parameterName);
            if (p is null || p.IsReadOnly)
                return false;

            if (first is null)
            {
                first = p;
                var spec = p.Definition?.GetDataType();
                if (spec is not null)
                    kind = TeamParameterKindMapping.MapFromSpecTypeId(spec);
                storageType = p.StorageType;
            }
            else
            {
                if (p.StorageType != first.StorageType)
                    return false;

                if (p.Definition?.GetDataType() != first.Definition?.GetDataType())
                    return false;
            }
        }

        if (first is null)
            return false;

        commonHint = TeamParameterValueFormatter.GetCommonValueHint(elements, parameterName, first.StorageType);
        return true;
    }

    private static Parameter? ResolveInstanceParameter(Element element, string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return null;

        foreach (Parameter candidate in element.Parameters)
        {
            if (!IsStrictlyElementInstanceParameter(element, candidate))
                continue;

            var defName = candidate.Definition?.Name;
            if (defName is null)
                continue;

            if (!string.Equals(defName, trimmed, StringComparison.OrdinalIgnoreCase))
                continue;

            return candidate;
        }

        var lp = element.LookupParameter(trimmed);
        return IsStrictlyElementInstanceParameter(element, lp) ? lp : null;
    }

    private static bool IsStrictlyElementInstanceParameter(Element element, Parameter? p)
    {
        if (p is null || p.Element is null)
            return false;

        if (p.Element is ElementType)
            return false;

        return p.Element.Id == element.Id;
    }
}
