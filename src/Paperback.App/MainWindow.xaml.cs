using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Paperback.App.ViewModels;
using Paperback.App.Views;
using Paperback.Core.Models;
using Wpf.Ui.Controls;

namespace Paperback.App;

public partial class MainWindow : FluentWindow
{
    public LibraryViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new LibraryViewModel(App.LibraryService);
        DataContext = ViewModel;

        ViewModel.RequestOpenBook += (s, book) => OpenReader(book);
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.BookCount) || e.PropertyName == nameof(ViewModel.HasBooks))
            {
                UpdateEmptyState();
            }
        };

        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
            UpdateEmptyState();
        };
    }

    private void UpdateEmptyState()
    {
        if (ViewModel.BookCount == 0)
        {
            EmptyStateGrid.Visibility = Visibility.Visible;
            BooksScrollViewer.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyStateGrid.Visibility = Visibility.Collapsed;
            BooksScrollViewer.Visibility = Visibility.Visible;
        }
    }

    private void SetActiveNavButton(Wpf.Ui.Controls.Button activeButton)
    {
        NavAllBooks.Appearance = ControlAppearance.Transparent;
        NavReadingNow.Appearance = ControlAppearance.Transparent;
        NavFinished.Appearance = ControlAppearance.Transparent;
        NavEpubs.Appearance = ControlAppearance.Transparent;
        NavPdfs.Appearance = ControlAppearance.Transparent;

        activeButton.Appearance = ControlAppearance.Secondary;
    }

    private async void OnNavAllBooksClicked(object sender, RoutedEventArgs e)
    {
        SetActiveNavButton(NavAllBooks);
        await ViewModel.SelectCategoryAsync("All Books");
    }

    private async void OnNavReadingNowClicked(object sender, RoutedEventArgs e)
    {
        SetActiveNavButton(NavReadingNow);
        await ViewModel.SelectCategoryAsync("Reading Now");
    }

    private async void OnNavFinishedClicked(object sender, RoutedEventArgs e)
    {
        SetActiveNavButton(NavFinished);
        await ViewModel.SelectCategoryAsync("Finished");
    }

    private async void OnNavEpubsClicked(object sender, RoutedEventArgs e)
    {
        SetActiveNavButton(NavEpubs);
        await ViewModel.SelectCategoryAsync("EPUBs");
    }

    private async void OnNavPdfsClicked(object sender, RoutedEventArgs e)
    {
        SetActiveNavButton(NavPdfs);
        await ViewModel.SelectCategoryAsync("PDFs");
    }

    private async void OnSortSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortComboBox?.SelectedItem is ComboBoxItem item && ViewModel != null)
        {
            await ViewModel.ChangeSortAsync(item.Content.ToString() ?? "Recent");
        }
    }

    private void OnBookCardOpened(object? sender, Book book)
    {
        OpenReader(book);
    }

    private async void OnBookCardFinished(object? sender, Book book)
    {
        await ViewModel.MarkFinishedAsync(book);
    }

    private async void OnBookCardRemoved(object? sender, Book book)
    {
        await ViewModel.RemoveBookAsync(book);
    }

    private void OpenReader(Book book)
    {
        if (!File.Exists(book.FilePath))
        {
            System.Windows.MessageBox.Show(
                $"Could not find the book file at:\n{book.FilePath}",
                "File Not Found",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var readerWindow = new ReaderWindow(book, App.LibraryService, App.DatabaseService, App.EpubService);
        readerWindow.Owner = this;
        readerWindow.Show();
    }

    // Drag and Drop Handling
    private void OnWindowDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnWindowDragLeave(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            await ViewModel.ImportDroppedFilesAsync(files);
        }
        e.Handled = true;
    }
}
