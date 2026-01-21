using System.Windows;
using MyScheduler.ViewModels;

namespace MyScheduler.Views;

public partial class ScheduleEditWindow : Window
{
    public ScheduleEditWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ScheduleEditViewModel vm) return;

        if (vm.SaveCommand.CanExecute(null))
            vm.SaveCommand.Execute(null);

        if (vm.ErrorMessage is null && vm.Result is not null)
        {
            DialogResult = true;
            Close();
        }
    }
}
