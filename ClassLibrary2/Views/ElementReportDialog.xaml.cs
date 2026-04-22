using System.Windows;

namespace AutoDocumentation.Views;

public partial class ElementReportDialog : Window
{
    public string UserNotes => UserNotesTextBox.Text ?? string.Empty;

    public ElementReportDialog()
    {
        InitializeComponent();
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
