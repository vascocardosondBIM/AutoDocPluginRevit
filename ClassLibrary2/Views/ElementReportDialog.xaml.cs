using System.Windows;
using AutoDocumentation.Services;

namespace AutoDocumentation.Views;

public partial class ElementReportDialog : Window
{
    public string UserNotes => UserNotesTextBox.Text ?? string.Empty;

    public ElementReportDialog()
    {
        InitializeComponent();
        RevitWpfAppearance.Apply(this);
        Title = PluginStrings.T("ReportDlg.Title");
        DescriptionText.Text = PluginStrings.T("ReportDlg.Body");
        NotesLabel.Text = PluginStrings.T("ReportDlg.NotesLabel");
        CancelButton.Content = PluginStrings.T("Common.Btn.Cancel");
        SaveButton.Content = PluginStrings.T("Common.Btn.Save");
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
