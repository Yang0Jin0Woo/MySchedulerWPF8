using Microsoft.Win32;
using System.Windows;

namespace MyScheduler.Services;

public class DialogService : IDialogService
{
    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public string? ShowSaveCsvDialog(string defaultFileName)
    {
        var dlg = new SaveFileDialog
        {
            Title = "CSV로 내보내기",
            Filter = "CSV 파일 (*.csv)|*.csv",
            FileName = defaultFileName,
            AddExtension = true,
            DefaultExt = ".csv"
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
