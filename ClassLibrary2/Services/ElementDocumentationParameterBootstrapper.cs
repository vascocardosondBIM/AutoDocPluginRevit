using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

/// <summary>
/// Garante que existe um parâmetro partilhado de texto (instância) com o nome indicado,
/// associado a todas as categorias necessárias para os elementos fornecidos.
/// Deve ser executado dentro de uma <see cref="Transaction"/> aberta.
/// </summary>
public static class ElementDocumentationParameterBootstrapper
{
    private const string DefinitionGroupName = "AutoDocumentation";

    public static void EnsureBoundInstanceTextParameter(Document doc, string parameterName, IReadOnlyList<Element> elements)
    {
        parameterName = parameterName.Trim();
        if (parameterName.Length == 0)
            throw new ArgumentException("O nome do parâmetro não pode ser vazio.", nameof(parameterName));

        if (elements.Count == 0)
            throw new ArgumentException("É necessário pelo menos um elemento para determinar as categorias.", nameof(elements));

        if (elements.All(e => ElementDocumentationParameterWriter.CanWriteText(e, parameterName, out _)))
            return;

        var categories = CollectDistinctCategories(doc, elements);
        if (categories.Count == 0)
            throw new InvalidOperationException(
                "Não foi possível determinar categorias válidas a partir da selecção (elementos sem categoria?).");

        var app = doc.Application;
        SharedParameterJsonPersistence.EnsureFileExists(doc);
        SharedParameterRevitTempTxt.MaterializeFromJson(doc);
        var defFile = SharedParameterFileLoader.OpenForSessionOrThrow(app, SharedParameterRevitTempTxt.GetCacheTxtPath(doc));

        var group = GetOrCreateGroup(defFile, DefinitionGroupName);
        var extDef = GetOrCreateStringExternalDefinition(group, parameterName);

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
            {
                throw new InvalidOperationException(
                    "Não foi possível actualizar o binding do parâmetro (Remove falhou).");
            }

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

        foreach (var e in elements)
        {
            if (!ElementDocumentationParameterWriter.CanWriteText(e, parameterName, out var reason))
            {
                throw new InvalidOperationException(
                    $"Após criar o parâmetro, o elemento Id {e.Id.Value} ainda não o expõe como texto editável: {reason}");
            }
        }

        SharedParameterRevitTempTxt.SyncCacheTxtIntoJson(doc);
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

    private static DefinitionGroup GetOrCreateGroup(DefinitionFile defFile, string name)
    {
        foreach (DefinitionGroup g in defFile.Groups)
        {
            if (string.Equals(g.Name, name, StringComparison.Ordinal))
                return g;
        }

        return defFile.Groups.Create(name);
    }

    private static ExternalDefinition GetOrCreateStringExternalDefinition(DefinitionGroup group, string parameterName)
    {
        foreach (Definition def in group.Definitions)
        {
            if (def is not ExternalDefinition ext)
                continue;

            if (!string.Equals(ext.Name, parameterName, StringComparison.Ordinal))
                continue;

            if (ext.GetDataType() != SpecTypeId.String.Text)
            {
                throw new InvalidOperationException(
                    $"Já existe um parâmetro partilhado chamado «{parameterName}» mas não é do tipo Texto.");
            }

            return ext;
        }

        var options = new ExternalDefinitionCreationOptions(parameterName, SpecTypeId.String.Text)
        {
            UserModifiable = true,
            Visible = true,
            Description = "Relatório gerado automaticamente pelo add-in Auto-documentação."
        };

        var created = group.Definitions.Create(options);
        return (ExternalDefinition)created;
    }
}
