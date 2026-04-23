using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AutoDocumentation.Models;
using AutoDocumentation.Services;

namespace AutoDocumentation.Views;

public partial class CreateTeamParameterDialog : Window
{
    private readonly IReadOnlyList<AssociableTeamParameterInfo> _associable;

    public string ParameterName => NameTextBox.Text ?? string.Empty;

    public TeamParameterKind SelectedKind =>
        KindComboBox.SelectedItem is ComboBoxItem { Tag: TeamParameterKind k }
            ? k
            : TeamParameterKind.Text;

    public bool IsAssociateMode => ModeAssociateRadio.IsChecked == true;

    public AssociableTeamParameterInfo? SelectedAssociable =>
        (AssociateComboBox.SelectedItem as ComboBoxItem)?.Tag as AssociableTeamParameterInfo;

    public CreateTeamParameterDialog(IReadOnlyList<AssociableTeamParameterInfo>? associableParameters = null)
    {
        _associable = associableParameters ?? System.Array.Empty<AssociableTeamParameterInfo>();
        InitializeComponent();
        RevitWpfAppearance.Apply(this);
        ApplyLocalizedChrome();

        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = PluginStrings.T("Kind.YesNo"),
            Tag = TeamParameterKind.YesNo
        });
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = PluginStrings.T("Kind.Text"),
            Tag = TeamParameterKind.Text,
            IsSelected = true
        });
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = PluginStrings.T("Kind.Integer"),
            Tag = TeamParameterKind.Integer
        });
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = PluginStrings.T("Kind.Decimal"),
            Tag = TeamParameterKind.DecimalNumber
        });
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = PluginStrings.T("Kind.LengthVerbose"),
            Tag = TeamParameterKind.Length
        });

        foreach (var item in _associable)
        {
            AssociateComboBox.Items.Add(new ComboBoxItem
            {
                Content = item.DisplayLine,
                Tag = item
            });
        }

        if (_associable.Count == 0)
        {
            ModeAssociateRadio.IsEnabled = false;
            ModeAssociateRadio.ToolTip = PluginStrings.T("Create.Tooltip.NoAssociable");
        }
        else
            AssociateComboBox.SelectedIndex = 0;

        ModeCreateRadio.Checked += (_, _) => ApplyModeVisuals();
        ModeAssociateRadio.Checked += (_, _) => ApplyModeVisuals();
        ApplyModeVisuals();
    }

    private void ApplyLocalizedChrome()
    {
        Title = PluginStrings.T("Create.Title");
        ModeCreateRadio.Content = PluginStrings.T("Create.Mode.CreateNew");
        ModeAssociateRadio.Content = PluginStrings.T("Create.Mode.AssociateExisting");
        KindLabel.Text = PluginStrings.T("Create.Label.Kind");
        CancelButton.Content = PluginStrings.T("Common.Btn.Cancel");
        OkButton.Content = PluginStrings.T("Common.Btn.OK");
    }

    private void ApplyModeVisuals()
    {
        var associate = ModeAssociateRadio.IsChecked == true;
        NameTextBox.Visibility = associate ? Visibility.Collapsed : Visibility.Visible;
        AssociateComboBox.Visibility = associate ? Visibility.Visible : Visibility.Collapsed;
        KindLabel.Visibility = associate ? Visibility.Collapsed : Visibility.Visible;
        KindComboBox.Visibility = associate ? Visibility.Collapsed : Visibility.Visible;
        PrimaryFieldLabel.Text = associate
            ? PluginStrings.T("Create.Label.ExistingParameter")
            : PluginStrings.T("Create.Label.ParameterName");
        HelpTextBlock.Text = associate
            ? PluginStrings.T("Create.Help.Associate")
            : PluginStrings.T("Create.Help.Create");
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ModeAssociateRadio.IsChecked == true)
        {
            if (SelectedAssociable is null)
            {
                MessageBox.Show(this, PluginStrings.T("Create.Msg.SelectFromList"), PluginStrings.T("Create.Title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            var name = ParameterName.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, PluginStrings.T("Create.Msg.ProvideName"), PluginStrings.T("Create.Title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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
