using Autodesk.Revit.DB;
using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

/// <summary>
/// Remove, substitui ou alarga categorias de parâmetros de equipa no projecto.
/// </summary>
public static class TeamParameterMaintenanceService
{
    /// <summary>Instâncias nas categorias do binding do parâmetro (todas as ocorrências no projecto).</summary>
    public static IReadOnlyList<ElementId> GetElementIdsInBindingForTeamParameter(Document doc, string parameterName)
    {
        var ext = TeamParameterDefinitionResolver.TryGetBoundTeamParameter(doc, parameterName);
        if (ext is null)
            return Array.Empty<ElementId>();

        if (doc.ParameterBindings.get_Item(ext) is not InstanceBinding instanceBinding)
            return Array.Empty<ElementId>();

        return CollectElementsInCategories(doc, instanceBinding.Categories)
            .Select(e => e.Id)
            .Distinct()
            .ToList();
    }

    public static void RemoveFromProject(Document doc, string parameterName)
    {
        var ext = TeamParameterDefinitionResolver.TryGetBoundTeamParameter(doc, parameterName)
                  ?? throw new InvalidOperationException(
                      $"Não foi encontrado o parâmetro «{parameterName}» no grupo «{TeamParameterConstants.DefinitionGroupName}» ligado ao projecto.");

        var guid = ext.GUID;
        if (!doc.ParameterBindings.Remove(ext))
            throw new InvalidOperationException("O Revit recusou remover o parâmetro do projecto (Remove falhou).");

        doc.Regenerate();

        if (TeamParameterSharedFileEditor.TryRemoveDefinitionByGuid(doc, guid, out _))
            TeamParameterSharedFileReloader.ReloadDefinitionFile(doc.Application, doc);
    }

    /// <summary>
    /// Adiciona à associação as categorias dos elementos indicados (mantendo as categorias já ligadas).
    /// </summary>
    public static void MergeBindingCategoriesFromElements(
        Document doc,
        string parameterName,
        IReadOnlyList<Element> elements)
    {
        var ext = TeamParameterDefinitionResolver.TryGetBoundTeamParameter(doc, parameterName)
                  ?? throw new InvalidOperationException($"Parâmetro «{parameterName}» não encontrado no projecto.");

        if (doc.ParameterBindings.get_Item(ext) is not InstanceBinding instanceBinding)
            throw new InvalidOperationException("Só é suportado binding de instância.");

        var add = CollectDistinctCategories(doc, elements);
        if (add.Count == 0)
            throw new InvalidOperationException("Não há categorias novas a partir dos elementos indicados.");

        var app = doc.Application;
        var merged = MergeCategorySets(doc, instanceBinding.Categories, add);
        if (!doc.ParameterBindings.Remove(ext))
            throw new InvalidOperationException("Não foi possível actualizar o binding (Remove falhou).");

        var newBinding = app.Create.NewInstanceBinding(merged);
        if (!doc.ParameterBindings.Insert(ext, newBinding, GroupTypeId.Data))
            throw new InvalidOperationException("O Revit recusou re-associar o parâmetro às categorias actualizadas.");

        doc.Regenerate();
    }

    /// <summary>
    /// Cria um parâmetro novo (nome + tipo) ligado às categorias da selecção, copia valores do original nesses elementos e limpa o original na selecção.
    /// Os restantes elementos do projecto mantêm o parâmetro original intacto.
    /// </summary>
    public static void ForkTeamParameterForSelection(
        Document doc,
        string sourceParameterName,
        string forkParameterName,
        TeamParameterKind forkKind,
        IReadOnlyList<Element> selection)
    {
        sourceParameterName = sourceParameterName.Trim();
        forkParameterName = forkParameterName.Trim();
        if (forkParameterName.Length == 0)
            throw new ArgumentException("O nome do novo parâmetro não pode ser vazio.", nameof(forkParameterName));

        if (string.Equals(sourceParameterName, forkParameterName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Indique um nome diferente do parâmetro original.");

        if (selection.Count == 0)
            throw new ArgumentException("A selecção não pode ser vazia.", nameof(selection));

        TeamParameterBootstrapper.EnsureBoundInstanceParameter(doc, forkParameterName, forkKind, selection);

        foreach (var el in selection)
        {
            if (el.LookupParameter(sourceParameterName) is not { } src)
                continue;

            var display = src.HasValue ? TeamParameterValueFormatter.FormatForDisplay(src) : string.Empty;
            TeamParameterValueWriter.TrySetFromDisplayText(el, forkParameterName, display, out _);
            TeamParameterValueWriter.TrySetFromDisplayText(el, sourceParameterName, string.Empty, out _);
        }

        doc.Regenerate();
    }

    public static void ReplaceParameterInProject(Document doc, string oldName, string newName, TeamParameterKind newKind) =>
        ReplaceParameterInProject(doc, oldName, newName, newKind, null);

    /// <summary>
    /// Substitui o parâmetro no projecto (novo nome e/ou tipo; categorias mantidas) e tenta copiar valores por instância.
    /// Mudança de tipo com o mesmo nome actualiza o ficheiro partilhado (remove a definição antiga por GUID e recria).
    /// </summary>
    public static void ReplaceParameterInProject(
        Document doc,
        string oldName,
        string newName,
        TeamParameterKind newKind,
        TeamParameterReplaceOptions? options)
    {
        oldName = oldName.Trim();
        newName = newName.Trim();
        if (oldName.Length == 0 || newName.Length == 0)
            throw new ArgumentException("Os nomes não podem ser vazios.");

        var oldExt = TeamParameterDefinitionResolver.TryGetBoundTeamParameter(doc, oldName)
                     ?? throw new InvalidOperationException($"Parâmetro «{oldName}» não encontrado.");

        if (doc.ParameterBindings.get_Item(oldExt) is not InstanceBinding instanceBinding)
            throw new InvalidOperationException("Só é suportado binding de instância.");

        var oldKind = TeamParameterKindMapping.MapFromSpecTypeId(oldExt.GetDataType());
        var nameChanged = !string.Equals(oldName, newName, StringComparison.Ordinal);
        var kindChanged = newKind != oldKind;

        if (!nameChanged && !kindChanged)
            return;

        var oldDefinitionGuid = oldExt.GUID;

        var categoryList = new List<Category>();
        foreach (Category c in instanceBinding.Categories)
            categoryList.Add(c);

        var elements = CollectElementsInCategories(doc, instanceBinding.Categories);
        var snapshot = new Dictionary<long, string>();
        foreach (var el in elements)
        {
            var p = el.LookupParameter(oldName);
            snapshot[el.Id.Value] = p is { HasValue: true }
                ? TeamParameterValueFormatter.FormatForDisplay(p)
                : string.Empty;
        }

        if (!doc.ParameterBindings.Remove(oldExt))
            throw new InvalidOperationException("Não foi possível remover o parâmetro antigo do projecto.");

        doc.Regenerate();

        if (!TeamParameterSharedFileEditor.TryRemoveDefinitionByGuid(doc, oldDefinitionGuid, out var fileErr))
            throw new InvalidOperationException(
                $"Não foi possível actualizar o ficheiro de parâmetros partilhados (necessário para recriar «{newName}»): {fileErr}");

        TeamParameterSharedFileReloader.ReloadDefinitionFile(doc.Application, doc);

        var catSet = CategoriesToSet(doc, categoryList);
        TeamParameterBootstrapper.EnsureBoundInstanceParameter(doc, newName, newKind, catSet);

        doc.Regenerate();

        var selection = options?.SelectionElementIdValues;
        var clearOutside = kindChanged && options?.ClearValuesOutsideSelectionOnKindChange == true && selection is not null;

        foreach (var el in CollectElementsInCategories(doc, catSet))
        {
            snapshot.TryGetValue(el.Id.Value, out var display);
            display ??= string.Empty;

            if (clearOutside && selection is not null && !selection.Contains(el.Id.Value))
            {
                TeamParameterValueWriter.TrySetFromDisplayText(el, newName, string.Empty, out _);
                continue;
            }

            TeamParameterValueWriter.TrySetFromDisplayText(el, newName, display, out _);
        }
    }

    private static List<Element> CollectElementsInCategories(Document doc, CategorySet categories)
    {
        var list = new List<Element>();
        var seen = new HashSet<long>();
        foreach (Category c in categories)
        {
            foreach (var id in new FilteredElementCollector(doc).OfCategoryId(c.Id).WhereElementIsNotElementType()
                         .ToElementIds())
            {
                if (!seen.Add(id.Value))
                    continue;

                var e = doc.GetElement(id);
                if (e is not null)
                    list.Add(e);
            }
        }

        return list;
    }

    private static CategorySet CategoriesToSet(Document doc, IReadOnlyList<Category> categories)
    {
        var app = doc.Application;
        var set = app.Create.NewCategorySet();
        foreach (var c in categories)
            set.Insert(c);

        return set;
    }

    private static List<Category> CollectDistinctCategories(Document doc, IReadOnlyList<Element> elements)
    {
        var list = new List<Category>();
        var seen = new HashSet<long>();

        foreach (var e in elements)
        {
            if (e.Category is null)
                continue;

            var c = Category.GetCategory(doc, e.Category.Id);
            if (c is null)
                continue;

            if (!seen.Add(c.Id.Value))
                continue;

            list.Add(c);
        }

        return list;
    }

    private static CategorySet MergeCategorySets(Document doc, CategorySet existing, IReadOnlyList<Category> add)
    {
        var app = doc.Application;
        var result = app.Create.NewCategorySet();
        foreach (Category c in existing)
            result.Insert(c);

        foreach (var c in add)
        {
            var already = false;
            foreach (Category x in result)
            {
                if (x.Id == c.Id)
                {
                    already = true;
                    break;
                }
            }

            if (!already)
                result.Insert(c);
        }

        return result;
    }
}
