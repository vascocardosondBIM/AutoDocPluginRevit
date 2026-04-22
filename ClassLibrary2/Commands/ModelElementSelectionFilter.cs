using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace AutoDocumentation.Commands;

/// <summary>
/// Permite seleccionar elementos de modelo (exclui vistas e folhas).
/// </summary>
public sealed class ModelElementSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem is View or ViewSheet)
            return false;

        return elem.Category is not null;
    }

    public bool AllowReference(Reference reference, XYZ position) => false;
}
