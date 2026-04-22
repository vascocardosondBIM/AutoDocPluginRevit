using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoDocumentation.Models;
using AutoDocumentation.Services;

namespace AutoDocumentation.Views;

public partial class TeamParametersEditorWindow : Window
{
    private readonly UIDocument _uidoc;
    private readonly List<ElementId> _elementIds;

    public ObservableCollection<TeamParameterRowModel> Rows { get; } = new();

    public TeamParametersEditorWindow(UIDocument uidoc, IReadOnlyList<Element> elements)
    {
        _uidoc = uidoc;
        _elementIds = elements.Select(e => e.Id).ToList();
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) =>
        {
            SelectionSummaryText.Text =
                $"{elements.Count} elemento(s) seleccionado(s). " +
                "Só aparecem parâmetros criados ou associados com «Novo parâmetro» (ficheiro partilhado por projecto, grupo «ParametrosEquipa»), ligados ao projecto e presentes em todas as instâncias seleccionadas. " +
                "O grupo «Dados» na paleta de propriedades é outro conceito (agrupamento visual no Revit), não este.";
            RefreshRows();
            UpdateParameterActionButtons();
        };
    }

    private void ParametersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateParameterActionButtons();

    private void UpdateParameterActionButtons()
    {
        var count = ParametersDataGrid.SelectedItems.Cast<object>().OfType<TeamParameterRowModel>().Count();
        EditParameterButton.IsEnabled = count == 1;
        DeleteParameterButton.IsEnabled = count == 1;
        ExportParameterPackButton.IsEnabled = count >= 1;
    }

    private List<Element> ResolveElements()
    {
        var doc = _uidoc.Document;
        return _elementIds
            .Select(id => doc.GetElement(id))
            .OfType<Element>()
            .ToList();
    }

    private void RefreshRows()
    {
        Rows.Clear();
        var elements = ResolveElements();
        foreach (var row in TeamParameterDiscoveryService.GetCommonEditableRows(_uidoc.Document, elements))
            Rows.Add(row);
        UpdateParameterActionButtons();
    }

    private void CreateParameterButton_Click(object sender, RoutedEventArgs e)
    {
        var elements = ResolveElements();
        if (elements.Count == 0)
        {
            MessageBox.Show(this, "Não há elementos válidos na selecção.", "Assistente de parâmetros", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        IReadOnlyList<AssociableTeamParameterInfo> associable;
        CreateTeamParameterDialog dlg;
        try
        {
            associable = TeamParameterDiscoveryService.GetAssociableTeamParameters(_uidoc.Document, elements);
            dlg = new CreateTeamParameterDialog(associable) { Owner = this };
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Não foi possível preparar o diálogo de parâmetros.\n\n" + ex.Message,
                "Assistente de parâmetros",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        if (dlg.ShowDialog() != true)
            return;

        string name;
        TeamParameterKind kind;
        if (dlg.IsAssociateMode && dlg.SelectedAssociable is { } link)
        {
            name = link.Name;
            kind = link.Kind;
        }
        else
        {
            name = dlg.ParameterName.Trim();
            kind = dlg.SelectedKind;

            if (TeamParameterSharedFileEditor.TryGetTeamParameterDataTypeInFile(_uidoc.Document, name, out var existingTok))
            {
                if (!TeamParameterSharedFileKindMapper.TokenMatchesKind(existingTok, kind))
                {
                    MessageBox.Show(
                        this,
                        $"Já existe no ficheiro partilhado (grupo «{TeamParameterConstants.DefinitionGroupName}») um parâmetro chamado «{name}» com outro tipo de dados. " +
                        "Use outro nome ou remova a definição antiga no editor de parâmetros partilhados do Revit.",
                        "Assistente de parâmetros",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
        }

        try
        {
            using var tx = new Transaction(_uidoc.Document, "Assistente de parâmetros: criar parâmetro");
            tx.Start();
            TeamParameterBootstrapper.EnsureBoundInstanceParameter(_uidoc.Document, name, kind, elements);
            tx.Commit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Assistente de parâmetros", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        var doneMsg = dlg.IsAssociateMode
            ? $"O parâmetro «{name}» foi associado às categorias dos elementos seleccionados (categorias acrescentadas ao binding no projecto)."
            : $"O parâmetro «{name}» foi criado ou actualizado (mesmo nome e tipo: categorias fundidas) e associado às categorias dos elementos seleccionados.";
        MessageBox.Show(this, doneMsg, "Assistente de parâmetros", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportParameterPackButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = ParametersDataGrid.SelectedItems.Cast<object>().OfType<TeamParameterRowModel>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Seleccione pelo menos um parâmetro na grelha para exportar.", "Exportar JSON",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = selected.Select(r => r.ParameterName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!TeamParameterPortablePackService.TryBuildPackFromParameterNames(_uidoc.Document, names, out var pack,
                out var buildError))
        {
            MessageBox.Show(this, buildError, "Exportar JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Guardar pacote de parâmetros",
            Filter = "JSON (*.json)|*.json|Todos os ficheiros (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = "ParametrosEquipa.json"
        };

        if (dlg.ShowDialog(this) != true)
            return;

        if (!TeamParameterPortablePackService.TryWritePackToPath(pack, dlg.FileName, out var writeError))
        {
            MessageBox.Show(this, writeError, "Exportar JSON", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(this,
            $"Foram exportadas {pack.Definitions.Count} definição(ões) para:\n{dlg.FileName}",
            "Exportar JSON",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ImportParameterPackButton_Click(object sender, RoutedEventArgs e)
    {
        var elements = ResolveElements();
        if (elements.Count == 0)
        {
            MessageBox.Show(this, "Não há elementos válidos na selecção — não é possível determinar categorias para o binding.",
                "Importar JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = "Carregar pacote de parâmetros",
            Filter = "JSON (*.json)|*.json|Todos os ficheiros (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != true)
            return;

        if (!TeamParameterPortablePackService.TryReadPackFromPath(dlg.FileName, out var pack, out var readError) ||
            pack is null)
        {
            MessageBox.Show(this, readError, "Importar JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TeamParameterPortablePackService.TryImportPack(_uidoc.Document, pack, elements, out var summary))
        {
            MessageBox.Show(this, summary, "Importar JSON", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        MessageBox.Show(this,
            string.IsNullOrWhiteSpace(summary) ? "Importação concluída." : summary,
            "Importar JSON",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ReportButton_Click(object sender, RoutedEventArgs e)
    {
        var elements = ResolveElements();
        if (elements.Count == 0)
        {
            MessageBox.Show(this, "Não há elementos válidos na selecção.", "Assistente de parâmetros", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var dlg = new ElementReportDialog { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        var options = new DocumentationRunOptions { UserAdditionalNotes = dlg.UserNotes };
        var result = new DocumentationOrchestrator().Run(_uidoc.Document, _uidoc.ActiveView, elements, options);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Message, "Relatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(this, result.Message, "Relatório", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void EditParameterButton_Click(object sender, RoutedEventArgs e)
    {
        if (ParametersDataGrid.SelectedItem is not TeamParameterRowModel row)
            return;

        var doc = _uidoc.Document;
        var elements = ResolveElements();
        var dlg = new EditTeamParameterDialog(row.ParameterName, row.Kind, elements.Count) { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        var nameOrKindChanged = !string.Equals(dlg.OriginalName, dlg.NewName, StringComparison.Ordinal) ||
                                dlg.SelectedKind != row.Kind;
        var needsMerge = dlg.ExtendCategoriesFromEditorSelection && elements.Count > 0;
        if (!nameOrKindChanged && !needsMerge)
        {
            MessageBox.Show(this, "Nada a alterar: indique um nome novo, outro tipo de dados ou marque + categorias.",
                "Editar parâmetro",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.Equals(dlg.OriginalName, dlg.NewName, StringComparison.Ordinal) &&
            TeamParameterSharedFileEditor.TryGetTeamParameterDataTypeInFile(_uidoc.Document, dlg.NewName, out var newNameTok))
        {
            if (!TeamParameterSharedFileKindMapper.TokenMatchesKind(newNameTok, dlg.SelectedKind))
            {
                MessageBox.Show(
                    this,
                    $"O nome «{dlg.NewName}» já existe no ficheiro partilhado com outro tipo de dados. Escolha outro nome.",
                    "Editar parâmetro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        TeamParameterReplaceOptions? replaceOpts = null;
        var kindChangedSameName = dlg.SelectedKind != row.Kind &&
                                  string.Equals(dlg.OriginalName, dlg.NewName, StringComparison.Ordinal);
        if (kindChangedSameName && elements.Count > 0)
        {
            var affected = TeamParameterMaintenanceService.GetElementIdsInBindingForTeamParameter(doc, dlg.OriginalName);
            var selSet = elements.Select(x => x.Id.Value).ToHashSet();
            var allWithinSelection = affected.Count == 0 || affected.All(id => selSet.Contains(id.Value));

            if (affected.Count > 0 && !allWithinSelection)
            {
                var r = MessageBox.Show(
                    "Este parâmetro está associado a instâncias nas categorias ligadas que não estão na selecção actual do assistente.\n\n" +
                    "Sim — alterar o tipo em todo o projecto: os valores nas instâncias fora da selecção serão limpos.\n" +
                    "Não — criar um parâmetro novo só para os elementos seleccionados (será pedido um nome); os outros elementos mantêm o parâmetro original.\n" +
                    "Cancelar — não fazer alterações.",
                    "Alterar tipo de dados",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel);

                if (r == MessageBoxResult.Cancel)
                    return;

                if (r == MessageBoxResult.No)
                {
                    var forkName = PromptDialog.Show(this, "Novo parâmetro na selecção",
                        "Nome do novo parâmetro (só os elementos seleccionados recebem cópia e limpeza do original):",
                        $"{dlg.OriginalName} (sel)");
                    if (string.IsNullOrWhiteSpace(forkName))
                        return;

                    if (TeamParameterSharedFileEditor.TryGetTeamParameterDataTypeInFile(_uidoc.Document, forkName, out var forkTok) &&
                        !TeamParameterSharedFileKindMapper.TokenMatchesKind(forkTok, dlg.SelectedKind))
                    {
                        MessageBox.Show(this,
                            $"O nome «{forkName}» já existe no ficheiro partilhado com outro tipo de dados.",
                            "Editar parâmetro",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        using var tx = new Transaction(doc, "Assistente de parâmetros: fork parâmetro");
                        tx.Start();
                        TeamParameterMaintenanceService.ForkTeamParameterForSelection(doc, dlg.OriginalName, forkName,
                            dlg.SelectedKind, elements);
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "Editar parâmetro", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    RefreshRows();
                    MessageBox.Show(this,
                        $"Foi criado «{forkName}» na selecção; os valores foram copiados e o parâmetro original foi limpo nesses elementos.",
                        "Assistente de parâmetros",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                replaceOpts = new TeamParameterReplaceOptions
                {
                    SelectionElementIdValues = selSet,
                    ClearValuesOutsideSelectionOnKindChange = true
                };
            }
        }

        try
        {
            using var tx = new Transaction(doc, "Assistente de parâmetros: editar parâmetro");
            tx.Start();

            var nameAfter = row.ParameterName;
            if (nameOrKindChanged)
            {
                TeamParameterMaintenanceService.ReplaceParameterInProject(doc, dlg.OriginalName, dlg.NewName,
                    dlg.SelectedKind, replaceOpts);
                nameAfter = dlg.NewName;
            }

            if (needsMerge)
                TeamParameterMaintenanceService.MergeBindingCategoriesFromElements(doc, nameAfter, elements);

            tx.Commit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Editar parâmetro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        MessageBox.Show(this, "Parâmetro actualizado.", "Assistente de parâmetros", MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void DeleteParameterButton_Click(object sender, RoutedEventArgs e)
    {
        if (ParametersDataGrid.SelectedItem is not TeamParameterRowModel row)
            return;

        var r = MessageBox.Show(
            this,
            $"Remover o parâmetro «{row.ParameterName}» do projecto? Os valores nas instâncias deixarão de estar disponíveis por este parâmetro.",
            "Eliminar parâmetro",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (r != MessageBoxResult.Yes)
            return;

        try
        {
            using var tx = new Transaction(_uidoc.Document, "Assistente de parâmetros: eliminar parâmetro");
            tx.Start();
            TeamParameterMaintenanceService.RemoveFromProject(_uidoc.Document, row.ParameterName);
            tx.Commit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Eliminar parâmetro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        MessageBox.Show(this, "Parâmetro removido do projecto.", "Assistente de parâmetros", MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var elements = ResolveElements();
        if (elements.Count == 0)
        {
            MessageBox.Show(this, "Não há elementos válidos.", "Assistente de parâmetros", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (Rows.Count == 0)
        {
            MessageBox.Show(
                this,
                "Não há parâmetros para aplicar. Crie primeiro um parâmetro ou seleccione elementos que partilhem os mesmos parâmetros no projecto.",
                "Assistente de parâmetros",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var errors = new StringBuilder();
        try
        {
            using var tx = new Transaction(_uidoc.Document, "Assistente de parâmetros: aplicar valores");
            tx.Start();

            foreach (var row in Rows)
            {
                foreach (var el in elements)
                {
                    if (!TeamParameterValueWriter.TrySetFromDisplayText(el, row.ParameterName, row.ValueText, out var err))
                        errors.AppendLine($"[{el.Id.Value}] «{row.ParameterName}»: {err}");
                }
            }

            if (errors.Length > 0)
            {
                tx.RollBack();
                MessageBox.Show(
                    this,
                    "Não foi possível aplicar todos os valores:\n\n" + errors,
                    "Assistente de parâmetros",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Assistente de parâmetros", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        MessageBox.Show(this, "Valores aplicados com sucesso.", "Assistente de parâmetros", MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
