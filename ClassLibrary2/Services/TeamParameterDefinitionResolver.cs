using System.IO;
using Autodesk.Revit.DB;
using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

/// <summary>
/// Localiza definições «ParametrosEquipa» no ficheiro partilhado e alinha com o mapa de bindings do documento.
/// </summary>
public static class TeamParameterDefinitionResolver
{
    public static ExternalDefinition? TryGetBoundTeamParameter(Document doc, string parameterName)
    {
        parameterName = parameterName.Trim();
        if (parameterName.Length == 0)
            return null;

        SharedParameterJsonPersistence.EnsureFileExists(doc);
        var jsonPath = SharedParameterPaths.GetSharedParametersJsonPath(doc);
        if (!File.Exists(jsonPath))
            return null;

        SharedParameterRevitTempTxt.MaterializeFromJson(doc);
        var cachePath = SharedParameterRevitTempTxt.GetCacheTxtPath(doc);
        if (!File.Exists(cachePath))
            return null;

        var app = doc.Application;
        return SharedParameterFileLoader.TryExecuteWithTemporarySharedParameterPath(app, cachePath, defFile =>
            {
                if (defFile is null)
                    return null;

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
                    return null;

                var map = doc.ParameterBindings;
                foreach (Definition def in teamGroup.Definitions)
                {
                    if (def is not ExternalDefinition fileExt)
                        continue;

                    if (!string.Equals(fileExt.Name, parameterName, StringComparison.Ordinal))
                        continue;

                    return ResolveBoundExternalDefinition(map, fileExt);
                }

                return null;
            });
    }

    /// <summary>Alinha a definição do ficheiro com a instância reconhecida pelo <see cref="BindingMap"/>.</summary>
    public static ExternalDefinition? ResolveBoundExternalDefinition(BindingMap map, ExternalDefinition fromFile)
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
}
