using System.Windows;
using System.Windows.Controls;
using AutoDocumentation.Models;

namespace AutoDocumentation.Views;

public partial class CreateTeamParameterDialog : Window
{
    public string ParameterName => NameTextBox.Text ?? string.Empty;

    public TeamParameterKind SelectedKind =>
        KindComboBox.SelectedItem is ComboBoxItem { Tag: TeamParameterKind k }
            ? k
            : TeamParameterKind.Text;

    public CreateTeamParameterDialog()
    {
        InitializeComponent();
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
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = ParameterName.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Indique um nome para o parâmetro.", "Novo parâmetro", MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
