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
    private readonly UIApplication _uiApp;
    private readonly List<ElementId> _elementIds;

    public ObservableCollection<TeamParameterRowModel> Rows { get; } = new();

    public TeamParametersEditorWindow(UIDocument uidoc, IReadOnlyList<Element> elements, UIApplication uiApp)
    {
        _uidoc = uidoc;
        _uiApp = uiApp;
        _elementIds = elements.Select(e => e.Id).ToList();
        InitializeComponent();
        RevitWpfAppearance.Apply(this);
        RevitWpfAppearance.AttachThemeChanged(_uiApp, this);
        ApplyLocalizedUi(elements.Count);
        DataContext = this;
        Loaded += (_, _) =>
        {
            RefreshRows();
            UpdateParameterActionButtons();
        };
    }

    private void ApplyLocalizedUi(int selectionCount)
    {
        Title = PluginStrings.T("Editor.Title");
        HeaderText.Text = PluginStrings.T("Editor.Header");
        SelectionSummaryText.Text = PluginStrings.Tf("Editor.SelectionSummary", selectionCount);
        ColParameterName.Header = PluginStrings.T("Editor.Col.ParameterName");
        ColKind.Header = PluginStrings.T("Editor.Col.DataType");
        ColValue.Header = PluginStrings.T("Editor.Col.ValueSelection");
        CreateParameterButton.Content = PluginStrings.T("Editor.Btn.NewParameter");
        ReportButton.Content = PluginStrings.T("Editor.Btn.Report");
        ExportParameterPackButton.Content = PluginStrings.T("Editor.Btn.ExportJson");
        ImportParameterPackButton.Content = PluginStrings.T("Editor.Btn.ImportJson");
        EditParameterButton.Content = PluginStrings.T("Editor.Btn.Edit");
        DeleteParameterButton.Content = PluginStrings.T("Editor.Btn.Delete");
        ApplyButton.Content = PluginStrings.T("Editor.Btn.Apply");
        CloseButton.Content = PluginStrings.T("Editor.Btn.Close");
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
            MessageBox.Show(this, PluginStrings.T("Editor.Msg.NoValidElements"), PluginStrings.T("Editor.Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
                PluginStrings.Tf("Editor.Msg.PrepareDialogFailed", ex.Message),
                PluginStrings.T("Editor.Title"),
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
                        PluginStrings.Tf("Editor.Msg.SharedFileTypeConflict", name, TeamParameterConstants.DefinitionGroupName),
                        PluginStrings.T("Editor.Title"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
        }

        try
        {
            using var tx = new Transaction(_uidoc.Document, PluginStrings.T("Tx.TxCreateParameter"));
            tx.Start();
            TeamParameterBootstrapper.EnsureBoundInstanceParameter(_uidoc.Document, name, kind, elements);
            tx.Commit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, PluginStrings.T("Editor.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        var doneMsg = dlg.IsAssociateMode
            ? PluginStrings.Tf("Editor.Msg.AssociatedDone", name)
            : PluginStrings.Tf("Editor.Msg.CreatedDone", name);
        MessageBox.Show(this, doneMsg, PluginStrings.T("Editor.Title"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportParameterPackButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = ParametersDataGrid.SelectedItems.Cast<object>().OfType<TeamParameterRowModel>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, PluginStrings.T("Export.Msg.SelectAtLeastOne"), PluginStrings.T("Export.Title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = selected.Select(r => r.ParameterName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!TeamParameterPortablePackService.TryBuildPackFromParameterNames(_uidoc.Document, names, out var pack,
                out var buildError))
        {
            MessageBox.Show(this, buildError, PluginStrings.T("Export.Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = PluginStrings.T("Export.Dialog.SaveTitle"),
            Filter = PluginStrings.T("Export.Dialog.Filter"),
            DefaultExt = ".json",
            AddExtension = true,
            FileName = PluginStrings.T("Export.Dialog.DefaultFileName")
        };

        if (dlg.ShowDialog(this) != true)
            return;

        if (!TeamParameterPortablePackService.TryWritePackToPath(pack, dlg.FileName, out var writeError))
        {
            MessageBox.Show(this, writeError, PluginStrings.T("Export.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(this,
            PluginStrings.Tf("Export.Msg.ExportWritten", pack.Definitions.Count, dlg.FileName),
            PluginStrings.T("Export.Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ImportParameterPackButton_Click(object sender, RoutedEventArgs e)
    {
        var elements = ResolveElements();
        if (elements.Count == 0)
        {
            MessageBox.Show(this, PluginStrings.T("Import.Msg.NoValidSelection"),
                PluginStrings.T("Import.Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = PluginStrings.T("Import.Dialog.OpenTitle"),
            Filter = PluginStrings.T("Export.Dialog.Filter"),
            CheckFileExists = true,
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != true)
            return;

        if (!TeamParameterPortablePackService.TryReadPackFromPath(dlg.FileName, out var pack, out var readError) ||
            pack is null)
        {
            MessageBox.Show(this, PluginStrings.Tf("Import.Msg.ReadError", readError), PluginStrings.T("Import.Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TeamParameterPortablePackService.TryImportPack(_uidoc.Document, pack, elements, out var summary))
        {
            MessageBox.Show(this, summary, PluginStrings.T("Import.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        MessageBox.Show(this,
            string.IsNullOrWhiteSpace(summary) ? PluginStrings.T("Import.Msg.Done") : summary,
            PluginStrings.T("Import.Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ReportButton_Click(object sender, RoutedEventArgs e)
    {
        var elements = ResolveElements();
        if (elements.Count == 0)
        {
            MessageBox.Show(this, PluginStrings.T("Report.Msg.NoValidElements"), PluginStrings.T("Editor.Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new ElementReportDialog { Owner = this };
        if (dlg.ShowDialog() != true)
            return;

        var options = new DocumentationRunOptions { UserAdditionalNotes = dlg.UserNotes };
        var result = new DocumentationOrchestrator().Run(_uidoc.Document, _uidoc.ActiveView, elements, options);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Message, PluginStrings.T("Report.Title"), MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(this, result.Message, PluginStrings.T("Report.Title"), MessageBoxButton.OK,
            MessageBoxImage.Information);
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
            MessageBox.Show(this, PluginStrings.T("Edit.Msg.NothingToChange"), PluginStrings.T("Edit.Title"),
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
                    PluginStrings.Tf("Edit.Msg.NewNameExistsWrongKind", dlg.NewName),
                    PluginStrings.T("Edit.Title"),
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
                    this,
                    PluginStrings.T("Edit.Msg.KindChange.Body"),
                    PluginStrings.T("Edit.Msg.KindChange.Title"),
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel);

                if (r == MessageBoxResult.Cancel)
                    return;

                if (r == MessageBoxResult.No)
                {
                    var forkName = PromptDialog.Show(this, PluginStrings.T("Edit.ForkDialog.Title"),
                        PluginStrings.T("Edit.ForkDialog.Prompt"),
                        $"{dlg.OriginalName} (sel)");
                    if (string.IsNullOrWhiteSpace(forkName))
                        return;

                    if (TeamParameterSharedFileEditor.TryGetTeamParameterDataTypeInFile(_uidoc.Document, forkName, out var forkTok) &&
                        !TeamParameterSharedFileKindMapper.TokenMatchesKind(forkTok, dlg.SelectedKind))
                    {
                        MessageBox.Show(this,
                            PluginStrings.Tf("Edit.Msg.ForkNameWrongKind", forkName),
                            PluginStrings.T("Edit.Title"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        using var tx = new Transaction(doc, PluginStrings.T("Tx.TxForkParameter"));
                        tx.Start();
                        TeamParameterMaintenanceService.ForkTeamParameterForSelection(doc, dlg.OriginalName, forkName,
                            dlg.SelectedKind, elements);
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, PluginStrings.T("Edit.Title"), MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    RefreshRows();
                    MessageBox.Show(this,
                        PluginStrings.Tf("Edit.Msg.ForkDone", forkName),
                        PluginStrings.T("Editor.Title"),
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
            using var tx = new Transaction(doc, PluginStrings.T("Tx.TxEditParameter"));
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
            MessageBox.Show(this, ex.Message, PluginStrings.T("Edit.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        MessageBox.Show(this, PluginStrings.T("Editor.Msg.ParameterUpdated"), PluginStrings.T("Editor.Title"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteParameterButton_Click(object sender, RoutedEventArgs e)
    {
        if (ParametersDataGrid.SelectedItem is not TeamParameterRowModel row)
            return;

        var r = MessageBox.Show(
            this,
            PluginStrings.Tf("Delete.Confirm", row.ParameterName),
            PluginStrings.T("Delete.Title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (r != MessageBoxResult.Yes)
            return;

        try
        {
            using var tx = new Transaction(_uidoc.Document, PluginStrings.T("Tx.TxDeleteParameter"));
            tx.Start();
            TeamParameterMaintenanceService.RemoveFromProject(_uidoc.Document, row.ParameterName);
            tx.Commit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, PluginStrings.T("Delete.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        MessageBox.Show(this, PluginStrings.T("Editor.Msg.RemovedFromProject"), PluginStrings.T("Editor.Title"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var elements = ResolveElements();
        if (elements.Count == 0)
        {
            MessageBox.Show(this, PluginStrings.T("Apply.Msg.NoValidElements"), PluginStrings.T("Editor.Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Rows.Count == 0)
        {
            MessageBox.Show(this, PluginStrings.T("Apply.Msg.NoParameters"), PluginStrings.T("Editor.Title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var errors = new StringBuilder();
        try
        {
            using var tx = new Transaction(_uidoc.Document, PluginStrings.T("Tx.TxApplyValues"));
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
                    PluginStrings.Tf("Apply.Msg.PartialErrors", errors),
                    PluginStrings.T("Editor.Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, PluginStrings.T("Editor.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RefreshRows();
        MessageBox.Show(this, PluginStrings.T("Apply.Msg.Success"), PluginStrings.T("Editor.Title"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
