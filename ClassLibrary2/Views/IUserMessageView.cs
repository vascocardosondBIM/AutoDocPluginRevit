namespace AutoDocumentation.Views;

/// <summary>
/// Abstração simples para feedback ao utilizador (TaskDialog, etc.).
/// </summary>
public interface IUserMessageView
{
    void ShowInfo(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
}
