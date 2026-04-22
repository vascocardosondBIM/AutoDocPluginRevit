namespace AutoDocumentation.Models;

public sealed class DocumentationRunResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    /// <summary>Elementos directamente seleccionados (relatório por instância).</summary>
    public int ElementsSelected { get; init; }

    /// <summary>Instâncias em que o relatório foi gravado no parâmetro fixo.</summary>
    public int ReportsWritten { get; init; }

    public static DocumentationRunResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static DocumentationRunResult Ok(string message, int elementsSelected, int reportsWritten) =>
        new()
        {
            Success = true,
            Message = message,
            ElementsSelected = elementsSelected,
            ReportsWritten = reportsWritten
        };
}
