using AutoDocumentation.Services;

namespace AutoDocumentation.Models;

/// <summary>
/// Parâmetro do grupo «ParametrosEquipa» que pode ser ligado às categorias da selecção actual.
/// </summary>
public sealed class AssociableTeamParameterInfo
{
    public AssociableTeamParameterInfo(string name, TeamParameterKind kind)
    {
        Name = name;
        Kind = kind;
    }

    public string Name { get; }

    public TeamParameterKind Kind { get; }

    public string DisplayLine => $"{Name} ({DescribeKind(Kind)})";

    private static string DescribeKind(TeamParameterKind k) =>
        k switch
        {
            TeamParameterKind.YesNo => PluginStrings.T("Kind.YesNo"),
            TeamParameterKind.Text => PluginStrings.T("Kind.Text"),
            TeamParameterKind.Integer => PluginStrings.T("Kind.Integer"),
            TeamParameterKind.DecimalNumber => PluginStrings.T("Kind.Decimal"),
            TeamParameterKind.Length => PluginStrings.T("Kind.LengthShort"),
            _ => k.ToString()
        };
}
