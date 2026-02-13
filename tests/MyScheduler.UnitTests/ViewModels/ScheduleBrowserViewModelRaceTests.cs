using MyScheduler.Models;
using MyScheduler.Services;
using MyScheduler.ViewModels;

namespace MyScheduler.UnitTests.ViewModels;

public class ScheduleBrowserViewModelRaceTests
{
    [Fact]
    public async Task LoadSchedulesAsync_WhenNewRequestStarts_PreviousRequestIsCanceledAndIgnored()
    {
        var service = new ControlledScheduleService(cancelOnToken: true);
        var vm = CreateViewModel(service);
        vm.Initialize(new DateTime(2026, 2, 13));

        var firstLoad = vm.LoadSchedulesAsync();
        await service.WaitForListCallCountAsync(1);
        var firstCall = service.ListCalls[0];

        var secondLoad = vm.LoadSchedulesAsync();
        await service.WaitForListCallCountAsync(2);
        var secondCall = service.ListCalls[1];

        Assert.True(firstCall.CancellationToken.IsCancellationRequested);

        secondCall.Complete(
            new List<ScheduleListItem>
            {
                new() { Id = 200, Title = "최신 요청 결과", StartAt = new DateTime(2026, 2, 13, 10, 0, 0), EndAt = new DateTime(2026, 2, 13, 11, 0, 0) }
            },
            totalCount: 1);

        await secondLoad;
        await firstLoad;

        Assert.Single(vm.PagedSchedules);
        Assert.Equal("최신 요청 결과", vm.PagedSchedules[0].Title);
        Assert.False(vm.IsLoadingList);
    }

    [Fact]
    public async Task LoadSchedulesAsync_WhenOlderResponseArrivesLate_RequestVersionPreventsOverwrite()
    {
        var service = new ControlledScheduleService(cancelOnToken: false);
        var vm = CreateViewModel(service);
        vm.Initialize(new DateTime(2026, 2, 13));

        var firstLoad = vm.LoadSchedulesAsync();
        await service.WaitForListCallCountAsync(1);
        var firstCall = service.ListCalls[0];

        var secondLoad = vm.LoadSchedulesAsync(showLoading: false);
        await service.WaitForListCallCountAsync(2);
        var secondCall = service.ListCalls[1];

        secondCall.Complete(
            new List<ScheduleListItem>
            {
                new() { Id = 2, Title = "B 응답", StartAt = new DateTime(2026, 2, 13, 9, 0, 0), EndAt = new DateTime(2026, 2, 13, 10, 0, 0) }
            },
            totalCount: 1);

        await secondLoad;

        firstCall.Complete(
            new List<ScheduleListItem>
            {
                new() { Id = 1, Title = "A 응답", StartAt = new DateTime(2026, 2, 13, 8, 0, 0), EndAt = new DateTime(2026, 2, 13, 9, 0, 0) }
            },
            totalCount: 1);

        await firstLoad;

        Assert.Single(vm.PagedSchedules);
        Assert.Equal("B 응답", vm.PagedSchedules[0].Title);
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

    private sealed class ControlledScheduleService : IScheduleService
    {
        private readonly bool _cancelOnToken;
        private readonly List<ListCall> _listCalls = new();

        public ControlledScheduleService(bool cancelOnToken)
        {
            _cancelOnToken = cancelOnToken;
        }

        public IReadOnlyList<ListCall> ListCalls => _listCalls;

        public Task<(List<ScheduleListItem> Items, int TotalCount)> GetListByDateAsync(
            DateTime date,
            string? searchText,
            string? searchScope,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var call = new ListCall(cancellationToken);
            lock (_listCalls)
            {
                _listCalls.Add(call);
            }

            if (_cancelOnToken)
            {
                cancellationToken.Register(() => call.Cancel(cancellationToken));
            }

            return call.Task;
        }

        public async Task WaitForListCallCountAsync(int expectedCount)
        {
            for (var i = 0; i < 200; i++)
            {
                lock (_listCalls)
                {
                    if (_listCalls.Count >= expectedCount)
                        return;
                }

                await Task.Delay(5);
            }

            throw new TimeoutException($"Expected list call count: {expectedCount}");
        }

        public Task<ScheduleItem?> GetByIdAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult<ScheduleItem?>(null);

        public Task<List<ScheduleListItem>> GetStartingInRangeAsync(DateTime startKst, DateTime endKst, CancellationToken cancellationToken)
            => Task.FromResult(new List<ScheduleListItem>());

        public Task<ScheduleItem> AddAsync(ScheduleItem item)
            => throw new NotSupportedException();

        public Task<ScheduleItem> UpdateAsync(ScheduleItem item)
            => throw new NotSupportedException();

        public Task DeleteAsync(int id, byte[] rowVersion)
            => throw new NotSupportedException();
    }

    private sealed class ListCall
    {
        private readonly TaskCompletionSource<(List<ScheduleListItem> Items, int TotalCount)> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ListCall(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }
        public Task<(List<ScheduleListItem> Items, int TotalCount)> Task => _tcs.Task;

        public void Complete(List<ScheduleListItem> items, int totalCount)
            => _tcs.TrySetResult((items, totalCount));

        public void Cancel(CancellationToken cancellationToken)
            => _tcs.TrySetCanceled(cancellationToken);
    }
}
