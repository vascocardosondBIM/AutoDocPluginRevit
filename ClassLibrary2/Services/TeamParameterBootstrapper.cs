using Autodesk.Revit.DB;
using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

/// <summary>
/// Cria definições no grupo «ParametrosEquipa» e associa-as como parâmetros de instância às categorias dos elementos indicados.
/// </summary>
public static class TeamParameterBootstrapper
{
    public static void EnsureBoundInstanceParameter(
        Document doc,
        string parameterName,
        TeamParameterKind kind,
        IReadOnlyList<Element> elements)
    {
        parameterName = parameterName.Trim();
        if (parameterName.Length == 0)
            throw new ArgumentException("O nome do parâmetro não pode ser vazio.", nameof(parameterName));

        if (elements.Count == 0)
            throw new ArgumentException("É necessário pelo menos um elemento para determinar as categorias.", nameof(elements));

        var specTypeId = TeamParameterKindMapping.GetSpecTypeId(kind);
        var categories = CollectDistinctCategories(doc, elements);
        if (categories.Count == 0)
            throw new InvalidOperationException(
                "Não foi possível determinar categorias válidas a partir da selecção (elementos sem categoria?).");

        var app = doc.Application;
        var defFile = OpenTeamSharedParameterFileForWrite(doc);

        var group = GetOrCreateGroup(defFile, TeamParameterConstants.DefinitionGroupName);
        var extDef = GetOrCreateExternalDefinition(group, parameterName, specTypeId);

        if (doc.ParameterBindings.Contains(extDef))
        {
            var binding = doc.ParameterBindings.get_Item(extDef);
            if (binding is not InstanceBinding instanceBinding)
            {
                throw new InvalidOperationException(
                    $"O parâmetro «{parameterName}» já existe no projecto com um tipo de binding não suportado (esperado: instância).");
            }

            var merged = MergeCategorySets(doc, instanceBinding.Categories, categories);
            if (!doc.ParameterBindings.Remove(extDef))
                throw new InvalidOperationException("Não foi possível actualizar o binding do parâmetro (Remove falhou).");

            var newBinding = app.Create.NewInstanceBinding(merged);
            if (!doc.ParameterBindings.Insert(extDef, newBinding, GroupTypeId.Data))
            {
                throw new InvalidOperationException(
                    "O Revit recusou re-associar o parâmetro às categorias actualizadas (Insert devolveu false).");
            }
        }
        else
        {
            var set = app.Create.NewCategorySet();
            foreach (var c in categories)
                set.Insert(c);

            var instanceBinding = app.Create.NewInstanceBinding(set);
            if (!doc.ParameterBindings.Insert(extDef, instanceBinding, GroupTypeId.Data))
            {
                throw new InvalidOperationException(
                    "O Revit recusou associar o parâmetro às categorias (Insert devolveu false). Verifique se o nome já existe com outro tipo de dado.");
            }
        }

        doc.Regenerate();
        SyncSharedParameterStorageFromRevitCache(doc);
    }

    /// <summary>
    /// Liga o parâmetro a um conjunto de categorias já conhecido (ex.: renomear mantendo as mesmas categorias).
    /// </summary>
    public static void EnsureBoundInstanceParameter(
        Document doc,
        string parameterName,
        TeamParameterKind kind,
        CategorySet categorySet)
    {
        parameterName = parameterName.Trim();
        if (parameterName.Length == 0)
            throw new ArgumentException("O nome do parâmetro não pode ser vazio.", nameof(parameterName));

        if (!CategorySetHasAny(categorySet))
            throw new ArgumentException("É necessário pelo menos uma categoria.", nameof(categorySet));

        var specTypeId = TeamParameterKindMapping.GetSpecTypeId(kind);

        var app = doc.Application;
        var defFile = OpenTeamSharedParameterFileForWrite(doc);

        var group = GetOrCreateGroup(defFile, TeamParameterConstants.DefinitionGroupName);
        var extDef = GetOrCreateExternalDefinition(group, parameterName, specTypeId);

        var setCopy = CloneCategorySet(doc, categorySet);

        if (doc.ParameterBindings.Contains(extDef))
        {
            var binding = doc.ParameterBindings.get_Item(extDef);
            if (binding is not InstanceBinding instanceBinding)
            {
                throw new InvalidOperationException(
                    $"O parâmetro «{parameterName}» já existe no projecto com um tipo de binding não suportado (esperado: instância).");
            }

            var merged = MergeCategorySetsFromCategorySets(doc, instanceBinding.Categories, setCopy);
            if (!doc.ParameterBindings.Remove(extDef))
                throw new InvalidOperationException("Não foi possível actualizar o binding do parâmetro (Remove falhou).");

            var newBinding = app.Create.NewInstanceBinding(merged);
            if (!doc.ParameterBindings.Insert(extDef, newBinding, GroupTypeId.Data))
            {
                throw new InvalidOperationException(
                    "O Revit recusou re-associar o parâmetro às categorias actualizadas (Insert devolveu false).");
            }
        }
        else
        {
            var instanceBinding = app.Create.NewInstanceBinding(setCopy);
            if (!doc.ParameterBindings.Insert(extDef, instanceBinding, GroupTypeId.Data))
            {
                throw new InvalidOperationException(
                    "O Revit recusou associar o parâmetro às categorias (Insert devolveu false). Verifique se o nome já existe com outro tipo de dado.");
            }
        }

        doc.Regenerate();
        SyncSharedParameterStorageFromRevitCache(doc);
    }

    private static bool CategorySetHasAny(CategorySet set)
    {
        foreach (Category _ in set)
            return true;

        return false;
    }

    private static CategorySet CloneCategorySet(Document doc, CategorySet source)
    {
        var app = doc.Application;
        var result = app.Create.NewCategorySet();
        foreach (Category c in source)
            result.Insert(c);

        return result;
    }

    private static CategorySet MergeCategorySetsFromCategorySets(Document doc, CategorySet existing, CategorySet add)
    {
        var app = doc.Application;
        var result = app.Create.NewCategorySet();
        foreach (Category c in existing)
            result.Insert(c);

        foreach (Category c in add)
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

    private static DefinitionFile OpenTeamSharedParameterFileForWrite(Document doc)
    {
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        SharedParameterRevitTempTxt.MaterializeFromJson(doc);
        var cache = SharedParameterRevitTempTxt.GetCacheTxtPath(doc);
        return SharedParameterFileLoader.OpenForSessionOrThrow(doc.Application, cache);
    }

    private static void SyncSharedParameterStorageFromRevitCache(Document doc) =>
        SharedParameterRevitTempTxt.SyncCacheTxtIntoJson(doc);

    private static DefinitionGroup GetOrCreateGroup(DefinitionFile defFile, string name)
    {
        foreach (DefinitionGroup g in defFile.Groups)
        {
            if (string.Equals(g.Name, name, StringComparison.Ordinal))
                return g;
        }

        return defFile.Groups.Create(name);
    }

    private static ExternalDefinition GetOrCreateExternalDefinition(
        DefinitionGroup group,
        string parameterName,
        ForgeTypeId specTypeId)
    {
        foreach (Definition def in group.Definitions)
        {
            if (def is not ExternalDefinition ext)
                continue;

            if (!string.Equals(ext.Name, parameterName, StringComparison.Ordinal))
                continue;

            if (ext.GetDataType() != specTypeId)
            {
                throw new InvalidOperationException(
                    $"Já existe um parâmetro partilhado chamado «{parameterName}» com outro tipo de dado.");
            }

            return ext;
        }

        var options = new ExternalDefinitionCreationOptions(parameterName, specTypeId)
        {
            UserModifiable = true,
            Visible = true,
            HideWhenNoValue = true,
            Description = "Parâmetro de equipa criado pelo add-in Auto-documentação."
        };

        var created = group.Definitions.Create(options);
        return (ExternalDefinition)created;
    }
}
