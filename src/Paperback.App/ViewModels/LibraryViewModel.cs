using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Paperback.Core.Interfaces;
using Paperback.Core.Models;

namespace Paperback.App.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryService _libraryService;

    [ObservableProperty]
    private ObservableCollection<Book> _displayBooks = new();

    [ObservableProperty]
    private string _selectedCategory = "All Books";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedSort = "Recent";

    [ObservableProperty]
    private int _bookCount = 0;

    [ObservableProperty]
    private bool _hasBooks = false;

    [ObservableProperty]
    private bool _isLoading = false;

    public event EventHandler<Book>? RequestOpenBook;

    public LibraryViewModel(ILibraryService libraryService)
    {
        _libraryService = libraryService;
        _libraryService.Books.CollectionChanged += (s, e) => RefreshDisplay();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await RefreshDisplayAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SelectCategoryAsync(string category)
    {
        SelectedCategory = category;
        await RefreshDisplayAsync();
    }

    [RelayCommand]
    public async Task ChangeSortAsync(string sortBy)
    {
        SelectedSort = sortBy;
        await RefreshDisplayAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = RefreshDisplayAsync();
    }

    public async Task RefreshDisplayAsync()
    {
        var filtered = await _libraryService.FilterBooksAsync(SelectedCategory, SearchQuery, SelectedSort);
        
        DisplayBooks.Clear();
        foreach (var b in filtered)
        {
            DisplayBooks.Add(b);
        }

        BookCount = DisplayBooks.Count;
        HasBooks = _libraryService.Books.Count > 0;
    }

    private void RefreshDisplay()
    {
        _ = RefreshDisplayAsync();
    }

    [RelayCommand]
    public async Task ImportFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import eBooks to Paperback",
            Filter = "eBooks (*.epub;*.pdf)|*.epub;*.pdf|EPUB Files (*.epub)|*.epub|PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            IsLoading = true;
            try
            {
                foreach (var file in dialog.FileNames)
                {
                    await _libraryService.ImportBookAsync(file);
                }
            }
            finally
            {
                IsLoading = false;
                await RefreshDisplayAsync();
            }
        }
    }

    [RelayCommand]
    public async Task ImportDroppedFilesAsync(string[] files)
    {
        if (files == null || files.Length == 0) return;

        IsLoading = true;
        try
        {
            foreach (var path in files)
            {
                if (File.Exists(path))
                {
                    await _libraryService.ImportBookAsync(path);
                }
                else if (Directory.Exists(path))
                {
                    await _libraryService.ImportDirectoryAsync(path, recursive: true);
                }
            }
        }
        finally
        {
            IsLoading = false;
            await RefreshDisplayAsync();
        }
    }

    [RelayCommand]
    public void OpenBook(Book book)
    {
        if (book != null)
        {
            RequestOpenBook?.Invoke(this, book);
        }
    }

    [RelayCommand]
    public async Task MarkFinishedAsync(Book book)
    {
        if (book != null)
        {
            await _libraryService.MarkAsFinishedAsync(book.Id);
            await RefreshDisplayAsync();
        }
    }

    [RelayCommand]
    public async Task RemoveBookAsync(Book book)
    {
        if (book != null)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to remove \"{book.Title}\" from your Paperback library?",
                "Remove Book",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _libraryService.RemoveBookAsync(book.Id, deleteFileFromDisk: false);
                await RefreshDisplayAsync();
            }
        }
    }
}
