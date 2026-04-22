using System;
using System.Windows;
using System.Windows.Controls;
using AutoDocumentation.Models;

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
        NewNameTextBox.Text = currentParameterName;
        PopulateKindCombo(currentKind);

        IntroText.Text =
            $"A editar «{currentParameterName}» ({editorSelectionCount} elemento(s) no assistente).";

        ExtendCategoriesCheckBox.IsEnabled = editorSelectionCount > 0;
    }

    private void PopulateKindCombo(TeamParameterKind select)
    {
        KindComboBox.Items.Clear();
        void Add(string label, TeamParameterKind kind, bool selected)
        {
            KindComboBox.Items.Add(new ComboBoxItem { Content = label, Tag = kind, IsSelected = selected });
        }

        Add("Sim / Não", TeamParameterKind.YesNo, select == TeamParameterKind.YesNo);
        Add("Texto", TeamParameterKind.Text, select == TeamParameterKind.Text);
        Add("Número inteiro", TeamParameterKind.Integer, select == TeamParameterKind.Integer);
        Add("Número decimal", TeamParameterKind.DecimalNumber, select == TeamParameterKind.DecimalNumber);
        Add("Comprimento (m na grelha)", TeamParameterKind.Length, select == TeamParameterKind.Length);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (NewName.Length == 0)
        {
            MessageBox.Show(this, "O nome do parâmetro não pode ser vazio.", "Editar parâmetro", MessageBoxButton.OK,
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
