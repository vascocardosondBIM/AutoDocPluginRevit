using System.Windows;
using AutoDocumentation.Services;

namespace AutoDocumentation.Views;

public partial class PromptDialog : Window
{
    public string? ResultText { get; private set; }

    public PromptDialog(string title, string label, string defaultText = "")
    {
        InitializeComponent();
        RevitWpfAppearance.Apply(this);
        Title = title;
        PromptLabel.Text = label;
        InputTextBox.Text = defaultText;
        CancelButton.Content = PluginStrings.T("Common.Btn.Cancel");
        OkButton.Content = PluginStrings.T("Common.Btn.OK");
        Loaded += (_, _) => InputTextBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultText = InputTextBox.Text?.Trim() ?? string.Empty;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static string? Show(Window owner, string title, string label, string defaultText = "")
    {
        var dlg = new PromptDialog(title, label, defaultText) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.ResultText : null;
    }
}
