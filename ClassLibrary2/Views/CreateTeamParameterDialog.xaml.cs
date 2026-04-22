using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AutoDocumentation.Models;

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
        InitializeComponent();
        _associable = associableParameters ?? System.Array.Empty<AssociableTeamParameterInfo>();

        // Popular combos antes de qualquer lógica de modo: o XAML dispara Checked dos rádios durante
        // InitializeComponent; se aí se alterar visibilidade do KindComboBox vazio, o WPF pode rebentar (NRE).
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = "Sim / Não",
            Tag = TeamParameterKind.YesNo
        });
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = "Texto",
            Tag = TeamParameterKind.Text,
            IsSelected = true
        });
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = "Número inteiro",
            Tag = TeamParameterKind.Integer
        });
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = "Número decimal",
            Tag = TeamParameterKind.DecimalNumber
        });
        KindComboBox.Items.Add(new ComboBoxItem
        {
            Content = "Comprimento (metros na grelha)",
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
            ModeAssociateRadio.ToolTip =
                "Não há parâmetros da equipa por associar: todos os geridos já aparecem na grelha para esta selecção.";
        }
        else
            AssociateComboBox.SelectedIndex = 0;

        ModeCreateRadio.Checked += (_, _) => ApplyModeVisuals();
        ModeAssociateRadio.Checked += (_, _) => ApplyModeVisuals();
        ApplyModeVisuals();
    }

    private void ApplyModeVisuals()
    {
        var associate = ModeAssociateRadio.IsChecked == true;
        NameTextBox.Visibility = associate ? Visibility.Collapsed : Visibility.Visible;
        AssociateComboBox.Visibility = associate ? Visibility.Visible : Visibility.Collapsed;
        KindLabel.Visibility = associate ? Visibility.Collapsed : Visibility.Visible;
        KindComboBox.Visibility = associate ? Visibility.Collapsed : Visibility.Visible;
        PrimaryFieldLabel.Text = associate ? "Parâmetro existente" : "Nome do parâmetro";
        HelpTextBlock.Text = associate
            ? "Serão acrescentadas no projecto as categorias dos elementos seleccionados a este parâmetro (instância). Os valores já existentes noutras categorias mantêm-se."
            : "O parâmetro será associado como instância às categorias dos elementos que seleccionou antes de abrir o editor. Comprimento assume metros na grelha.";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ModeAssociateRadio.IsChecked == true)
        {
            if (SelectedAssociable is null)
            {
                MessageBox.Show(this, "Seleccione um parâmetro da lista.", "Novo parâmetro", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            var name = ParameterName.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Indique um nome para o parâmetro.", "Novo parâmetro", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
