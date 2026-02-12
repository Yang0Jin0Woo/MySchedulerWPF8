using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace MyScheduler.ViewModels;

public partial class ScheduleListStateViewModel : ObservableObject
{
    public ObservableCollection<string> SearchScopes { get; } =
        new() { "전체", "제목", "장소" };

    [ObservableProperty]
    private string selectedSearchScope;

    [ObservableProperty]
    private string searchText = "";

    private string _appliedSearchText = "";
    private string _appliedSearchScope = "전체";

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalPages = 1;

    public ObservableCollection<int> PageNumbers { get; } = new();

    public bool HasPrevPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public ScheduleListStateViewModel()
    {
        selectedSearchScope = SearchScopes.First();
    }

    public void ApplySearch()
    {
        _appliedSearchText = SearchText ?? "";
        _appliedSearchScope = SelectedSearchScope;
        MoveToFirstPage();
    }

    public void ClearSearch()
    {
        SearchText = "";
        SelectedSearchScope = SearchScopes.First();
        _appliedSearchText = SearchText;
        _appliedSearchScope = SelectedSearchScope;
        MoveToFirstPage();
    }

    public (string SearchText, string SearchScope) GetAppliedSearch()
        => (_appliedSearchText, _appliedSearchScope);

    public void MoveToFirstPage()
    {
        CurrentPage = 1;
        UpdatePageNumbers();
    }

    public bool MovePrevPage()
    {
        if (CurrentPage <= 1) return false;
        CurrentPage -= 1;
        UpdatePageNumbers();
        return true;
    }

    public bool MoveNextPage()
    {
        if (CurrentPage >= TotalPages) return false;
        CurrentPage += 1;
        UpdatePageNumbers();
        return true;
    }

    public bool GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return false;
        if (CurrentPage == page) return false;
        CurrentPage = page;
        UpdatePageNumbers();
        return true;
    }

    public void SetTotalCount(int totalCount, int pageSize)
    {
        TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;
        UpdatePageNumbers();
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(HasPrevPage));
        OnPropertyChanged(nameof(HasNextPage));
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(HasPrevPage));
        OnPropertyChanged(nameof(HasNextPage));
    }

    private void UpdatePageNumbers()
    {
        PageNumbers.Clear();

        const int windowSize = 5;
        if (TotalPages <= 0) return;

        var half = windowSize / 2;
        var start = CurrentPage - half;
        var end = CurrentPage + half;

        if (start < 1)
        {
            end += 1 - start;
            start = 1;
        }

        if (end > TotalPages)
        {
            start -= end - TotalPages;
            end = TotalPages;
        }

        if (start < 1) start = 1;

        for (var i = start; i <= end && PageNumbers.Count < windowSize; i++)
            PageNumbers.Add(i);
    }
}
