using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AutoDocumentation.Services;
using AutoDocumentation.Views;

namespace AutoDocumentation.Commands;

/// <summary>
/// Assistente: parâmetros partilhados de equipa, relatório no elemento e edição em lote na selecção.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class ParameterAssistantCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiApp = commandData.Application;
        PluginStrings.Initialize(uiApp.Application.Language);
        var uidoc = uiApp.ActiveUIDocument;
        var doc = uidoc.Document;

        List<Element> selection;
        try
        {
            selection = CollectSelection(uidoc);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }

        if (selection.Count == 0)
        {
            message = PluginStrings.T("Cmd.ErrSelectElements");
            TaskDialog.Show(PluginStrings.T("Cmd.CmdTitle"), message);
            return Result.Failed;
        }

        var window = new TeamParametersEditorWindow(uidoc, selection, uiApp);
        _ = new WindowInteropHelper(window) { Owner = uiApp.MainWindowHandle };
        window.ShowDialog();
        return Result.Succeeded;
    }

    private static List<Element> CollectSelection(UIDocument uidoc)
    {
        var doc = uidoc.Document;
        var ids = uidoc.Selection.GetElementIds();
        if (ids.Count > 0)
        {
            return ids
                .Select(id => doc.GetElement(id))
                .OfType<Element>()
                .Where(e => e.Category is not null && e is not View && e is not ViewSheet)
                .ToList();
        }

        var refs = uidoc.Selection.PickObjects(
            ObjectType.Element,
            new ModelElementSelectionFilter(),
            PluginStrings.T("Cmd.PickPrompt"));

        var list = new List<Element>();
        var seen = new HashSet<long>();
        foreach (var r in refs)
        {
            var el = doc.GetElement(r.ElementId);
            if (el is null || el.Category is null)
                continue;
            if (!seen.Add(el.Id.Value))
                continue;
            list.Add(el);
        }

        return list;
    }
}
