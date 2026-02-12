using MyScheduler.Models;
using MyScheduler.ViewModels;
using MyScheduler.Views;
using System;
using System.Windows;

namespace MyScheduler.Services;

public class ScheduleEditorDialogService : IScheduleEditorDialogService
{
    public bool TryOpen(DateTime baseDate, ScheduleItem? existing, out ScheduleItem? result)
    {
        var vm = new ScheduleEditViewModel(baseDate, existing);
        var win = new ScheduleEditWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = vm
        };

        if (win.ShowDialog() == true && vm.Result is not null)
        {
            result = vm.Result;
            return true;
        }

        result = null;
        return false;
    }
}
