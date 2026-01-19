using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Windows;
using MyScheduler.Services;
using MyScheduler.ViewModels;

namespace MyScheduler;

public partial class App : Application
{
    public static IHost AppHost { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(config =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((ctx, services) =>
                {
                    var conn = ctx.Configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("ConnectionStrings:Default 가 없습니다.");

                    // DbContext를 직접 DI로 물지 않고 필요할 때 생성
                    services.AddPooledDbContextFactory<AppDbContext>(opt =>
                        opt.UseSqlServer(conn));

                    // Service (Repository 제거)
                    services.AddSingleton<IScheduleService, ScheduleService>();

                    // UI
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            await AppHost.StartAsync();

            // DB 마이그레이션 안정화: DB 문제를 조기에 감지
            using (var scope = AppHost.Services.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var db = await factory.CreateDbContextAsync();

                var pending = await db.Database.GetPendingMigrationsAsync();
                if (pending.Any())
                {
                    await db.Database.MigrateAsync();
                }
            }

            var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = AppHost.Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            var hint =
                "가능한 원인:\n" +
                "1) SQL Server(SQLEXPRESS)가 설치되어 있지 않거나 실행 중이 아님\n" +
                "2) appsettings.json의 ConnectionStrings:Default 값이 잘못됨\n" +
                "3) DB 권한 문제(Trusted_Connection)\n\n" +
                "확인 방법:\n" +
                "- Windows 서비스에서 'SQL Server (SQLEXPRESS)' 실행 여부 확인\n" +
                "- SSMS로 localhost\\SQLEXPRESS 접속 테스트\n" +
                "- appsettings.json 연결 문자열 확인";

            MessageBox.Show(
                $"앱 초기화 중 오류가 발생했습니다.\n\n{ex.Message}\n\n{hint}",
                "초기화 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (AppHost != null)
            {
                await AppHost.StopAsync();
                AppHost.Dispose();
            }
        }
        finally
        {
            base.OnExit(e);
        }
    }
}
