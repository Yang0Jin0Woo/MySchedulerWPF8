using Microsoft.EntityFrameworkCore;
using MyScheduler.Data;
using System.Windows;

namespace MyScheduler;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        using var db = new AppDbContext();
        db.Database.Migrate(); // 테이블/마이그레이션 자동 적용
        base.OnStartup(e);
    }
}
