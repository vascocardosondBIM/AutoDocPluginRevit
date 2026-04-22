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

    public string DisplayLine => $"{Name} ({DescribeKindPortuguese(Kind)})";

    private static string DescribeKindPortuguese(TeamParameterKind k) =>
        k switch
        {
            TeamParameterKind.YesNo => "Sim / Não",
            TeamParameterKind.Text => "Texto",
            TeamParameterKind.Integer => "Número inteiro",
            TeamParameterKind.DecimalNumber => "Número decimal",
            TeamParameterKind.Length => "Comprimento",
            _ => k.ToString()
        };
}
