namespace AutoDocumentation.Models;

/// <summary>
/// Definições de parâmetros partilhados em JSON; o conteúdo efectivo são as linhas no formato de texto do Revit (PARAM/GROUP/…).
/// </summary>
public sealed class SharedParametersJsonDocument
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Linhas do ficheiro de parâmetros partilhados (tabs, ordem Revit).</summary>
    public List<string> Lines { get; set; } = new();
}
