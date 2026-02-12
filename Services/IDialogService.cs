namespace MyScheduler.Services;

public interface IDialogService
{
    bool Confirm(string message, string title);
    void ShowInfo(string message, string title);
    void ShowWarning(string message, string title);
    void ShowError(string title, string message);
    string? ShowSaveCsvDialog(string defaultFileName);
}
