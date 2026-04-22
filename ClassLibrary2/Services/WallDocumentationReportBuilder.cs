using System.Globalization;
using System.Text;
using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

/// <summary>
/// Gera o relatório textual determinístico por instância de parede (para gravar num parâmetro de texto custom).
/// </summary>
public static class WallDocumentationReportBuilder
{
    private const int MaxReportLength = 16000;

    public static string Build(Document doc, Wall wall, string userAdditionalNotes)
    {
        var sb = new StringBuilder();
        var now = DateTime.Now.ToString("g", CultureInfo.CurrentCulture);

        sb.AppendLine("=== Relatório de documentação (instância de parede) ===");
        sb.AppendLine($"Gerado: {now}");
        sb.AppendLine($"Id elemento: {wall.Id.Value}");
        sb.AppendLine($"UniqueId: {wall.UniqueId}");
        sb.AppendLine($"Nome instância: {wall.Name}");
        sb.AppendLine();

        var wt = doc.GetElement(wall.GetTypeId()) as WallType;
        sb.AppendLine($"Tipo de parede: {wt?.Name ?? "(n/d)"}");

        var level = doc.GetElement(wall.LevelId) as Level;
        sb.AppendLine($"Nível base: {level?.Name ?? "(n/d)"}");

        TryAppendParameterLine(sb, wall, BuiltInParameter.WALL_BASE_OFFSET, "Deslocamento base");
        TryAppendParameterLine(sb, wall, BuiltInParameter.WALL_HEIGHT_TYPE, "Tipo de altura");
        TryAppendParameterLine(sb, wall, BuiltInParameter.WALL_USER_HEIGHT_PARAM, "Altura (parâmetro utilizador)");
        TryAppendParameterLine(sb, wall, BuiltInParameter.WALL_TOP_OFFSET, "Deslocamento do topo");

        var lengthFt = wall.Location is LocationCurve lc && lc.Curve is Line line
            ? line.Length
            : (double?)null;

        if (lengthFt is { } len)
            sb.AppendLine($"Comprimento (eixo): {FormatLength(doc, len)}");

        try
        {
            var w = wall.Width;
            sb.AppendLine($"Espessura (largura): {FormatLength(doc, w)}");
        }
        catch
        {
            sb.AppendLine("Espessura (largura): (n/d)");
        }

        sb.AppendLine();
        sb.AppendLine("--- Inserções / elementos integrados ---");
        AppendHostedElements(doc, wall, sb);

        TeamParameterDiscoveryService.AppendTeamParameterReportSection(doc, wall, sb);

        var notes = userAdditionalNotes.Trim();
        if (notes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- Notas do utilizador ---");
            sb.AppendLine(notes);
        }

        var text = sb.ToString();
        if (text.Length > MaxReportLength)
            text = text.Substring(0, MaxReportLength - 20) + "\n… (truncado)";

        return text;
    }

    private static void AppendHostedElements(Document doc, Wall wall, StringBuilder sb)
    {
        var seen = new HashSet<long>();

        ICollection<ElementId>? insertIds = null;
        try
        {
            insertIds = wall.FindInserts(true, true, true, true);
        }
        catch
        {
            insertIds = Array.Empty<ElementId>();
        }

        foreach (var id in insertIds)
        {
            if (!seen.Add(id.Value))
                continue;

            AppendElementLine(doc, sb, id);
        }

        foreach (var fi in new FilteredElementCollector(doc)
                     .OfClass(typeof(FamilyInstance))
                     .Cast<FamilyInstance>()
                     .Where(f => f.Host?.Id == wall.Id)
                     .OrderBy(f => f.Category?.Name)
                     .ThenBy(f => f.Name))
        {
            if (!seen.Add(fi.Id.Value))
                continue;

            AppendFamilyInstanceLine(sb, fi);
        }

        if (seen.Count == 0)
            sb.AppendLine("(Nenhum elemento integrado detectado.)");
    }

    private static void AppendElementLine(Document doc, StringBuilder sb, ElementId id)
    {
        var el = doc.GetElement(id);
        if (el is FamilyInstance fi)
        {
            AppendFamilyInstanceLine(sb, fi);
            return;
        }

        if (el is null)
        {
            sb.AppendLine($"- Id {id.Value}: (elemento não encontrado)");
            return;
        }

        var cat = el.Category?.Name ?? el.GetType().Name;
        sb.AppendLine($"- [{cat}] {el.Name} | Id: {el.Id.Value}");
    }

    private static void AppendFamilyInstanceLine(StringBuilder sb, FamilyInstance fi)
    {
        var cat = fi.Category?.Name ?? "Sem categoria";
        var fam = fi.Symbol?.Family?.Name ?? fi.Name;
        var typ = fi.Symbol?.Name ?? "(tipo)";
        var mark = fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
        var markPart = string.IsNullOrWhiteSpace(mark) ? "" : $" | Marca: {mark}";

        sb.AppendLine($"- [{cat}] {fam} — {typ}{markPart} | Id: {fi.Id.Value}");
    }

    private static void TryAppendParameterLine(StringBuilder sb, Wall wall, BuiltInParameter bip, string label)
    {
        var p = wall.get_Parameter(bip);
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
