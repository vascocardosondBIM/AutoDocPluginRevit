using System.Globalization;
using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

public static class TeamParameterValueFormatter
{
    public static string FormatForDisplay(Parameter p)
    {
        if (!p.HasValue)
        {
            if (p.StorageType == StorageType.String)
                return p.AsString() ?? string.Empty;

            return string.Empty;
        }

        if (p.StorageType == StorageType.String)
            return p.AsString() ?? string.Empty;

        if (p.StorageType == StorageType.Integer)
        {
            if (p.Definition?.GetDataType() == SpecTypeId.Boolean.YesNo)
                return p.AsInteger() != 0 ? "Sim" : "Não";

            return p.AsInteger().ToString(CultureInfo.CurrentCulture);
        }

        if (p.StorageType == StorageType.Double)
        {
            var d = p.AsDouble();
            return p.Definition?.GetDataType() == SpecTypeId.Length
                ? UnitUtils.ConvertFromInternalUnits(d, UnitTypeId.Meters).ToString("0.###", CultureInfo.CurrentCulture)
                : d.ToString("0.###", CultureInfo.CurrentCulture);
        }

        return string.Empty;
    }

    /// <summary>
    /// Valor comum em texto; <c>null</c> se os elementos tiverem valores diferentes (ou parâmetro em falta).
    /// </summary>
    public static string? GetCommonValueHint(IReadOnlyList<Element> elements, string parameterName, StorageType storageType)
    {
        string? first = null;
        var started = false;
        foreach (var el in elements)
        {
            var p = el.LookupParameter(parameterName);
            if (p is null)
                return null;

            var s = FormatForDisplay(p);
            if (!started)
            {
                first = s;
                started = true;
            }
            else if (!ValuesEqual(storageType, first, s))
                return null;
        }

        return first;
    }

    private static bool ValuesEqual(StorageType storageType, string? a, string? b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return true;

        if (storageType == StorageType.Double && TryParseDoubleLoose(a, out var da) && TryParseDoubleLoose(b, out var db))
            return Math.Abs(da - db) < 1e-9;

        return false;
    }

    private static bool TryParseDoubleLoose(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
               || double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
