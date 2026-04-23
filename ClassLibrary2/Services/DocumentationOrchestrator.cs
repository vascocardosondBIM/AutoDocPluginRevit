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
            return DocumentationRunResult.Fail(PluginStrings.T("Doc.Err.ViewTemplate"));

        if (selection.Count == 0)
            return DocumentationRunResult.Fail(PluginStrings.T("Doc.Err.NoSelection"));

        var reportParamName = ElementDocumentationConstants.ReportParameterName;

        var reportsWritten = 0;

        using var tx = new Transaction(doc, PluginStrings.T("Tx.AutoDocReport"));
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
                PluginStrings.Tf("Doc.Err.CreateParamFail", reportParamName, ex.Message));
        }

        foreach (var e in selection)
        {
            if (!ElementDocumentationParameterWriter.CanWriteText(e, reportParamName, out var reason))
            {
                tx.RollBack();
                return DocumentationRunResult.Fail(
                    PluginStrings.Tf("Doc.Err.CannotWrite", reportParamName, reason, e.Id.Value));
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
            return DocumentationRunResult.Fail(PluginStrings.T("Doc.Err.NoReportsWritten"));
        }

        tx.Commit();

        var message = PluginStrings.Tf("Doc.Ok.Summary", selection.Count, reportsWritten);
        return DocumentationRunResult.Ok(message, selection.Count, reportsWritten);
    }
}
