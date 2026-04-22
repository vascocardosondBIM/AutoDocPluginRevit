namespace AutoDocumentation.Models;

/// <summary>Opções para substituir parâmetro (nome/tipo) no projecto.</summary>
public sealed class TeamParameterReplaceOptions
{
    /// <summary>Identificadores dos elementos seleccionados no assistente (<see cref="Autodesk.Revit.DB.ElementId.Value"/>).</summary>
    public HashSet<long>? SelectionElementIdValues { get; init; }

    /// <summary>
    /// Quando o tipo de dados muda sem mudar o nome: não repõe valores nos elementos fora da selecção
    /// (ficam vazios após a conversão de tipo).
    /// </summary>
    public bool ClearValuesOutsideSelectionOnKindChange { get; init; }
}
