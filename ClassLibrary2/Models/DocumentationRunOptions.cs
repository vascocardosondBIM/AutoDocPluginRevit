namespace AutoDocumentation.Models;

/// <summary>
/// Opções do automatizador de documentação definidas na UI.
/// </summary>
public sealed class DocumentationRunOptions
{
    /// <summary>Texto livre acrescentado ao relatório de cada elemento.</summary>
    public string UserAdditionalNotes { get; init; } = string.Empty;
}
