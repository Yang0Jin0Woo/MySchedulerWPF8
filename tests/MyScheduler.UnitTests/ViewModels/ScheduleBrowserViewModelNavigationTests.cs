using MyScheduler.Models;
using MyScheduler.Services;
using MyScheduler.ViewModels;

namespace MyScheduler.UnitTests.ViewModels;

public class ScheduleBrowserViewModelNavigationTests
{
    [Fact]
    public async Task NavigateToScheduleAsync_WhenScheduleIsOnAnotherPage_LoadsDetailThenSelectsFromTargetPage()
    {
        var date = new DateTime(2026, 2, 13);
        var target = new ScheduleItem
        {
            Id = 42,
            Title = "타깃 일정",
            StartAt = new DateTime(2026, 2, 13, 15, 0, 0),
            EndAt = new DateTime(2026, 2, 13, 16, 0, 0)
        };

        var service = new NavigationScheduleService(
            details: new Dictionary<int, ScheduleItem> { [target.Id] = target },
            pageMap: new Dictionary<int, int?> { [target.Id] = 2 },
            totalCount: 20,
            listByPage: new Dictionary<int, List<ScheduleListItem>>
            {
                [1] = new()
                {
                    new() { Id = 1, Title = "첫 일정", StartAt = date.AddHours(9), EndAt = date.AddHours(10) }
                },
                [2] = new()
                {
                    new() { Id = 42, Title = "타깃 일정", StartAt = date.AddHours(15), EndAt = date.AddHours(16) }
                }
            });
        var vm = CreateViewModel(service);
        vm.Initialize(date);

        await vm.NavigateToScheduleAsync(42);

        Assert.NotNull(vm.SelectedScheduleDetail);
        Assert.Equal(42, vm.SelectedScheduleDetail!.Id);
        Assert.Equal(target.StartAt.Date, vm.SelectedDate);
        Assert.NotNull(vm.SelectedSchedule);
        Assert.Equal(42, vm.SelectedSchedule!.Id);
        Assert.Equal(2, vm.CurrentPage);
        Assert.Contains(service.ListRequestedPages, x => x == 2);
    }

    [Fact]
    public async Task NavigateToScheduleAsync_WhenNotVisibleInCurrentListFilter_KeepsDetailAndSkipsSelection()
    {
        var date = new DateTime(2026, 2, 13);
        var target = new ScheduleItem
        {
            Id = 42,
            Title = "타깃 일정",
            StartAt = new DateTime(2026, 2, 13, 15, 0, 0),
            EndAt = new DateTime(2026, 2, 13, 16, 0, 0)
        };

        var service = new NavigationScheduleService(
            details: new Dictionary<int, ScheduleItem> { [target.Id] = target },
            pageMap: new Dictionary<int, int?> { [target.Id] = null },
            totalCount: 0,
            listByPage: new Dictionary<int, List<ScheduleListItem>>());
        var vm = CreateViewModel(service);
        vm.Initialize(date);

        await vm.NavigateToScheduleAsync(42);

        Assert.NotNull(vm.SelectedScheduleDetail);
        Assert.Equal(42, vm.SelectedScheduleDetail!.Id);
        Assert.Null(vm.SelectedSchedule);
        Assert.Empty(service.ListRequestedPages);
    }

    private static ScheduleBrowserViewModel CreateViewModel(IScheduleService scheduleService)
    {
        return new ScheduleBrowserViewModel(
            scheduleService,
            new NoOpDialogService(),
            new ScheduleListStateViewModel());
    }

    private sealed class NoOpDialogService : IDialogService
    {
        public bool Confirm(string message, string title) => true;
        public void ShowInfo(string message, string title) { }
        public void ShowWarning(string message, string title) { }
        public void ShowError(string title, string message) { }
        public string? ShowSaveCsvDialog(string defaultFileName) => null;
    }

    private sealed class NavigationScheduleService : IScheduleService
    {
        private readonly IReadOnlyDictionary<int, ScheduleItem> _details;
        private readonly IReadOnlyDictionary<int, int?> _pageMap;
        private readonly int _totalCount;
        private readonly IReadOnlyDictionary<int, List<ScheduleListItem>> _listByPage;

        public NavigationScheduleService(
            IReadOnlyDictionary<int, ScheduleItem> details,
            IReadOnlyDictionary<int, int?> pageMap,
            int totalCount,
            IReadOnlyDictionary<int, List<ScheduleListItem>> listByPage)
        {
            _details = details;
            _pageMap = pageMap;
            _totalCount = totalCount;
            _listByPage = listByPage;
        }

        public List<int> ListRequestedPages { get; } = new();

        public Task<(List<ScheduleListItem> Items, int TotalCount)> GetListByDateAsync(
            DateTime date,
            string? searchText,
            string? searchScope,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            ListRequestedPages.Add(page);
            var items = _listByPage.TryGetValue(page, out var list)
                ? list
                : new List<ScheduleListItem>();

            return Task.FromResult((items, _totalCount));
        }

        public Task<ScheduleItem?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            _details.TryGetValue(id, out var detail);
            return Task.FromResult(detail);
        }

        public Task<int?> GetPageNumberForScheduleAsync(
            int scheduleId,
            DateTime date,
            string? searchText,
            string? searchScope,
            int pageSize,
            CancellationToken cancellationToken)
        {
            _pageMap.TryGetValue(scheduleId, out var page);
            return Task.FromResult(page);
        }

        public Task<List<ScheduleListItem>> GetStartingInRangeAsync(
            DateTime startKst,
            DateTime endKst,
            CancellationToken cancellationToken)
            => Task.FromResult(new List<ScheduleListItem>());

        public Task<ScheduleItem> AddAsync(ScheduleItem item)
            => throw new NotSupportedException();

        public Task<ScheduleItem> UpdateAsync(ScheduleItem item)
            => throw new NotSupportedException();

        public Task DeleteAsync(int id, byte[] rowVersion)
            => throw new NotSupportedException();
    }
}
