using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

/// <summary>
/// Grava texto longo num parâmetro de <b>instância</b> do elemento (nome fixo do add-in).
/// Nunca grava em parâmetros de tipo (<see cref="ElementType"/>).
/// </summary>
public static class ElementDocumentationParameterWriter
{
    public static bool CanWriteText(Element element, string parameterName, out string failureReason)
    {
        failureReason = string.Empty;
        var p = ResolveInstanceStringParameter(element, parameterName);
        if (p is null)
        {
            failureReason =
                "parâmetro não encontrado nesta instância (confirme o binding à categoria do elemento como instância).";
            return false;
        }

        if (p.IsReadOnly)
        {
            failureReason = "parâmetro só de leitura.";
            return false;
        }

        if (p.StorageType != StorageType.String)
        {
            failureReason = $"o parâmetro tem de ser do tipo Texto (actual: {p.StorageType}).";
            return false;
        }

        return true;
    }

    public static bool TryWriteText(Element element, string parameterName, string value)
    {
        if (!CanWriteText(element, parameterName, out _))
            return false;

        var p = ResolveInstanceStringParameter(element, parameterName);
        if (p is null || p.IsReadOnly || p.StorageType != StorageType.String)
            return false;

        try
        {
            p.Set(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Parameter? ResolveInstanceStringParameter(Element element, string rawName)
    {
        var name = (rawName ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        foreach (Parameter candidate in element.Parameters)
        {
            if (!IsStrictlyElementInstanceParameter(element, candidate))
                continue;

            var defName = candidate.Definition?.Name;
            if (defName is null)
                continue;

            if (!string.Equals(defName, name, StringComparison.OrdinalIgnoreCase))
                continue;

            return candidate;
        }

        var lp = element.LookupParameter(name);
        return IsStrictlyElementInstanceParameter(element, lp) ? lp : null;
    }

    private static bool IsStrictlyElementInstanceParameter(Element element, Parameter? p)
    {
        if (p is null || p.Element is null)
            return false;

        if (p.Element is ElementType)
            return false;

        return p.Element.Id == element.Id;
    }
}
