using Autodesk.Revit.DB;
using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

/// <summary>
/// Grava o relatório de documentação no parâmetro de instância com nome fixo.
/// </summary>
public sealed class DocumentationOrchestrator
{
    public DocumentationRunResult Run(Document doc, View view, IReadOnlyList<Element> selection, DocumentationRunOptions options)
    {
        if (view.IsTemplate)
            return DocumentationRunResult.Fail("A vista ativa não pode ser um template.");

        if (selection.Count == 0)
            return DocumentationRunResult.Fail("Não há elementos seleccionados.");

        var reportParamName = ElementDocumentationConstants.ReportParameterName;

        var reportsWritten = 0;

        using var tx = new Transaction(doc, "Auto-documentação: relatório");
        tx.Start();

        try
        {
            ElementDocumentationParameterBootstrapper.EnsureBoundInstanceTextParameter(
                doc,
                reportParamName,
                selection);
        }
        catch (Exception ex)
        {
            tx.RollBack();
            return DocumentationRunResult.Fail(
                $"Não foi possível criar ou associar o parâmetro partilhado «{reportParamName}»: {ex.Message}");
        }

        foreach (var e in selection)
        {
            if (!ElementDocumentationParameterWriter.CanWriteText(e, reportParamName, out var reason))
            {
                tx.RollBack();
                return DocumentationRunResult.Fail(
                    $"Parâmetro «{reportParamName}»: {reason} (elemento Id {e.Id.Value}).");
            }
        }

        var notes = options.UserAdditionalNotes ?? string.Empty;
        foreach (var element in selection)
        {
            var text = ElementDocumentationReportBuilder.Build(doc, element, notes);
            if (ElementDocumentationParameterWriter.TryWriteText(element, reportParamName, text))
                reportsWritten++;
        }

        if (reportsWritten == 0)
        {
            tx.RollBack();
            return DocumentationRunResult.Fail(
                "Não foi possível gravar relatórios. Verifique a selecção e os parâmetros dos elementos.");
        }

        tx.Commit();

        var message =
            $"Elementos seleccionados: {selection.Count}. Relatórios gravados: {reportsWritten}.";
        return DocumentationRunResult.Ok(message, selection.Count, reportsWritten);
    }
}
