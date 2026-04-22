using System.Globalization;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace AutoDocumentation.Services;

/// <summary>
/// Gera relatório textual por instância de elemento (paredes usam o relatório detalhado existente).
/// </summary>
public static class ElementDocumentationReportBuilder
{
    private const int MaxReportLength = 16000;

    public static string Build(Document doc, Element element, string userAdditionalNotes)
    {
        if (element is Wall wall)
            return WallDocumentationReportBuilder.Build(doc, wall, userAdditionalNotes);

        if (element is Room room)
            return BuildRoomReport(doc, room, userAdditionalNotes);

        return BuildGenericElementReport(doc, element, userAdditionalNotes);
    }

    private static string BuildRoomReport(Document doc, Room room, string userAdditionalNotes)
    {
        var sb = new StringBuilder();
        var now = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);

        sb.AppendLine("=== Relatório de documentação (ambiente / Room) ===");
        sb.AppendLine($"Gerado: {now}");
        sb.AppendLine($"Id elemento: {room.Id.Value}");
        sb.AppendLine($"UniqueId: {room.UniqueId}");
        sb.AppendLine($"Nome: {room.Name}");
        sb.AppendLine();

        TryAppendParameterLine(sb, room, BuiltInParameter.ROOM_NAME, "Nome (parâmetro)");
        TryAppendParameterLine(sb, room, BuiltInParameter.ROOM_NUMBER, "Número");
        TryAppendParameterLine(sb, room, BuiltInParameter.ROOM_DEPARTMENT, "Departamento");
        TryAppendParameterLine(sb, room, BuiltInParameter.ROOM_AREA, "Área");
        TryAppendParameterLine(sb, room, BuiltInParameter.ROOM_VOLUME, "Volume");

        var level = doc.GetElement(room.LevelId) as Level;
        sb.AppendLine($"Nível associado: {level?.Name ?? "(n/d)"}");

        AppendUserNotes(sb, userAdditionalNotes);
        return Truncate(sb.ToString());
    }

    private static string BuildGenericElementReport(Document doc, Element element, string userAdditionalNotes)
    {
        var sb = new StringBuilder();
        var now = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);

        sb.AppendLine("=== Relatório de documentação (elemento) ===");
        sb.AppendLine($"Gerado: {now}");
        sb.AppendLine($"Id elemento: {element.Id.Value}");
        sb.AppendLine($"UniqueId: {element.UniqueId}");
        sb.AppendLine($"Nome: {element.Name}");
        sb.AppendLine($"Categoria: {element.Category?.Name ?? "(n/d)"}");
        sb.AppendLine();

        if (doc.GetElement(element.GetTypeId()) is ElementType et)
            sb.AppendLine($"Tipo: {et.Name}");

        if (element is FamilyInstance fi)
        {
            var fam = fi.Symbol?.Family?.Name ?? fi.Name;
            var sym = fi.Symbol?.Name ?? "(símbolo)";
            sb.AppendLine($"Família: {fam}");
            sb.AppendLine($"Tipo de família: {sym}");
        }

        if (element.LevelId is { } lid && lid != ElementId.InvalidElementId)
        {
            var lvl = doc.GetElement(lid) as Level;
            sb.AppendLine($"Nível: {lvl?.Name ?? lid.Value.ToString(CultureInfo.CurrentCulture)}");
        }

        AppendLocationSummary(doc, element, sb);
        AppendBoundingBoxSummary(doc, element, sb);

        AppendUserNotes(sb, userAdditionalNotes);
        return Truncate(sb.ToString());
    }

    private static void AppendLocationSummary(Document doc, Element element, StringBuilder sb)
    {
        switch (element.Location)
        {
            case LocationCurve lc when lc.Curve is Line line:
                sb.AppendLine($"Comprimento (curva): {FormatLength(doc, line.Length)}");
                break;
            case LocationCurve lc:
                try
                {
                    var len = lc.Curve.ApproximateLength;
                    sb.AppendLine($"Comprimento aproximado (curva): {FormatLength(doc, len)}");
                }
                catch
                {
                    sb.AppendLine("Localização: curva (comprimento n/d).");
                }

                break;
            case LocationPoint lp:
                sb.AppendLine(
                    $"Ponto (coord. internas, pés): X={lp.Point.X.ToString("F3", CultureInfo.InvariantCulture)}, Y={lp.Point.Y.ToString("F3", CultureInfo.InvariantCulture)}, Z={lp.Point.Z.ToString("F3", CultureInfo.InvariantCulture)}");
                break;
            default:
                sb.AppendLine("Localização: (n/d ou não aplicável).");
                break;
        }
    }

    private static void AppendBoundingBoxSummary(Document doc, Element element, StringBuilder sb)
    {
        var bb = element.get_BoundingBox(null);
        if (bb is null)
        {
            sb.AppendLine("Caixa delimitadora: (n/d).");
            return;
        }

        var dx = Math.Abs(bb.Max.X - bb.Min.X);
        var dy = Math.Abs(bb.Max.Y - bb.Min.Y);
        var dz = Math.Abs(bb.Max.Z - bb.Min.Z);
        sb.AppendLine(
            $"Caixa delimitadora (L×P×A): {FormatLength(doc, dx)} × {FormatLength(doc, dy)} × {FormatLength(doc, dz)}");
    }

    private static void AppendUserNotes(StringBuilder sb, string userAdditionalNotes)
    {
        var notes = userAdditionalNotes.Trim();
        if (notes.Length == 0)
            return;

        sb.AppendLine();
        sb.AppendLine("--- Notas do utilizador ---");
        sb.AppendLine(notes);
    }

    private static string Truncate(string text)
    {
        if (text.Length <= MaxReportLength)
            return text;

        return text.Substring(0, MaxReportLength - 20) + "\n… (truncado)";
    }

    private static void TryAppendParameterLine(StringBuilder sb, Element element, BuiltInParameter bip, string label)
    {
        var p = element.get_Parameter(bip);
        if (p is null || !p.HasValue)
            return;

        var s = FormatParameterValue(p);
        if (string.IsNullOrWhiteSpace(s))
            return;

        sb.AppendLine($"{label}: {s}");
    }

    private static string FormatParameterValue(Parameter p)
    {
        return p.StorageType switch
        {
            StorageType.String => p.AsString() ?? string.Empty,
            StorageType.Double => p.AsValueString() ?? p.AsDouble().ToString(CultureInfo.InvariantCulture),
            StorageType.Integer => p.AsInteger().ToString(CultureInfo.CurrentCulture),
            StorageType.ElementId => p.AsElementId() is { } id && id != ElementId.InvalidElementId
                ? id.Value.ToString(CultureInfo.CurrentCulture)
                : string.Empty,
            _ => string.Empty
        };
    }

    private static string FormatLength(Document doc, double internalFeet)
    {
        try
        {
            var m = UnitUtils.ConvertFromInternalUnits(internalFeet, UnitTypeId.Meters);
            return $"{m.ToString("F3", CultureInfo.CurrentCulture)} m";
        }
        catch
        {
            return $"{internalFeet.ToString("F3", CultureInfo.CurrentCulture)} (int.)";
        }
    }
}
