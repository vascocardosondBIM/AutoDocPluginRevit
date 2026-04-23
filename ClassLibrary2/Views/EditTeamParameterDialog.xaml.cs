using System.Windows;
using System.Windows.Controls;
using AutoDocumentation.Models;
using AutoDocumentation.Services;

namespace AutoDocumentation.Views;

public partial class EditTeamParameterDialog : Window
{
    public string OriginalName { get; }

    public string NewName => NewNameTextBox.Text?.Trim() ?? string.Empty;

    public TeamParameterKind SelectedKind =>
        KindComboBox.SelectedItem is ComboBoxItem { Tag: TeamParameterKind k }
            ? k
            : TeamParameterKind.Text;

    public bool ExtendCategoriesFromEditorSelection => ExtendCategoriesCheckBox.IsChecked == true;

    public TeamParameterKind InitialKind { get; }

    public EditTeamParameterDialog(string currentParameterName, TeamParameterKind currentKind, int editorSelectionCount)
    {
        OriginalName = currentParameterName;
        InitialKind = currentKind;
        InitializeComponent();
        RevitWpfAppearance.Apply(this);

        Title = PluginStrings.T("Edit.Title");
        NameLabel.Text = PluginStrings.T("Edit.Label.Name");
        KindLabel.Text = PluginStrings.T("Edit.Label.Kind");
        ExtendCategoriesCheckBox.Content = PluginStrings.T("Edit.Check.ExtendCategories");
        ExtendCategoriesCheckBox.ToolTip = PluginStrings.T("Edit.Tooltip.ExtendCategories");
        FooterHelpText.Text = PluginStrings.T("Edit.Help.Footer");
        CancelButton.Content = PluginStrings.T("Common.Btn.Cancel");
        OkButton.Content = PluginStrings.T("Common.Btn.OK");

        NewNameTextBox.Text = currentParameterName;
        PopulateKindCombo(currentKind);

        IntroText.Text = PluginStrings.Tf("Edit.Intro", currentParameterName, editorSelectionCount);

        ExtendCategoriesCheckBox.IsEnabled = editorSelectionCount > 0;
    }

    private void PopulateKindCombo(TeamParameterKind select)
    {
        KindComboBox.Items.Clear();
        void Add(string label, TeamParameterKind kind, bool selected)
        {
            KindComboBox.Items.Add(new ComboBoxItem { Content = label, Tag = kind, IsSelected = selected });
        }

        Add(PluginStrings.T("Kind.YesNo"), TeamParameterKind.YesNo, select == TeamParameterKind.YesNo);
        Add(PluginStrings.T("Kind.Text"), TeamParameterKind.Text, select == TeamParameterKind.Text);
        Add(PluginStrings.T("Kind.Integer"), TeamParameterKind.Integer, select == TeamParameterKind.Integer);
        Add(PluginStrings.T("Kind.Decimal"), TeamParameterKind.DecimalNumber, select == TeamParameterKind.DecimalNumber);
        Add(PluginStrings.T("Kind.Length"), TeamParameterKind.Length, select == TeamParameterKind.Length);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (NewName.Length == 0)
        {
            MessageBox.Show(this, PluginStrings.T("Edit.Msg.NameEmpty"), PluginStrings.T("Edit.Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
