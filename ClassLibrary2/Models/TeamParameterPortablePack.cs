namespace AutoDocumentation.Models;

/// <summary>
/// Pacote portátil de definições «ParametrosEquipa» para exportar/importar entre projectos (JSON).
/// </summary>
public sealed class TeamParameterPortablePack
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Identificador de formato; não alterar entre versões compatíveis.</summary>
    public string PackKind { get; set; } = "ParametrosEquipa.v1";

    public string DefinitionGroupName { get; set; } = TeamParameterConstants.DefinitionGroupName;

    /// <summary>Id numérico do grupo no ficheiro de origem (coluna GROUP); usado ao reescrever linhas PARAM na importação.</summary>
    public string? SourceGroupId { get; set; }

    public DateTime? ExportedAtUtc { get; set; }

    public List<TeamParameterPortableEntry> Definitions { get; set; } = new();
}

/// <summary>Uma definição PARAM do grupo de equipa.</summary>
public sealed class TeamParameterPortableEntry
{
    public string ParameterName { get; set; } = string.Empty;

    public TeamParameterKind TeamParameterKind { get; set; }

    public string DefinitionGuid { get; set; } = string.Empty;

    public string DataTypeToken { get; set; } = "TEXT";

    /// <summary>Linha completa PARAM (tabs) tal como no ficheiro partilhado Revit.</summary>
    public string ParamLine { get; set; } = string.Empty;
}
