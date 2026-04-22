using Autodesk.Revit.UI;

namespace AutoDocumentation.Views;

public sealed class TaskDialogUserMessageView : IUserMessageView
{
    public void ShowInfo(string title, string message) =>
        TaskDialog.Show(title, message, TaskDialogCommonButtons.Ok);

    public void ShowWarning(string title, string message)
    {
        var td = new TaskDialog(title)
        {
            MainContent = message,
            CommonButtons = TaskDialogCommonButtons.Ok,
            MainIcon = TaskDialogIcon.TaskDialogIconWarning
        };
        td.Show();
    }

    public void ShowError(string title, string message)
    {
        var td = new TaskDialog(title)
        {
            MainContent = message,
            CommonButtons = TaskDialogCommonButtons.Ok,
            MainIcon = TaskDialogIcon.TaskDialogIconError
        };
        td.Show();
    }
}
