using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Paperback.Core.Interfaces;
using Paperback.Core.Models;
using VersOne.Epub;
using Wpf.Ui.Controls;

namespace Paperback.App.Views;

public partial class ReaderWindow : FluentWindow
{
    private readonly Book _book;
    private readonly ILibraryService _libraryService;
    private readonly IDatabaseService _databaseService;
    private readonly IEpubService _epubService;

    private readonly DispatcherTimer _autoHideTimer;
    private bool _isChromeVisible = true;
    private bool _isEngineReady = false;

    private int _currentPage = 1;
    private int _totalPages = 1;
    private double _progressPercent = 0.0;

    private string _currentTheme = "sepia";
    private string _currentFont = "Georgia";
    private double _currentFontSize = 18;
    private bool _isJustified = true;

    public ReaderWindow(
        Book book,
        ILibraryService libraryService,
        IDatabaseService databaseService,
        IEpubService epubService)
    {
        InitializeComponent();

        _book = book;
        _libraryService = libraryService;
        _databaseService = databaseService;
        _epubService = epubService;

        BookTitleHeader.Text = _book.Title;
        BookAuthorHeader.Text = _book.Author;

        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.8)
        };
        _autoHideTimer.Tick += OnAutoHideTimerTick;

        Loaded += async (s, e) =>
        {
            await InitializeWebViewAsync();
            await LoadTableOfContentsAsync();
            _autoHideTimer.Start();
        };

        Closed += async (s, e) =>
        {
            _autoHideTimer.Stop();
            // Save final progress
            await _libraryService.UpdateReadingProgressAsync(_book.Id, _currentPage, _totalPages, _progressPercent, null);
        };
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userDataFolder = Path.Combine(appData, "Paperback", "WebView2Data");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await ReaderWebView.EnsureCoreWebView2Async(env);

            string engineFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReaderEngine");
            if (!Directory.Exists(engineFolder))
            {
                // Fallback to project directory
                engineFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ReaderEngine"));
            }

            ReaderWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "reader.paperback.local",
                engineFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            // Also allow reading local files (for PDF and EPUB resources)
            string bookDir = Path.GetDirectoryName(_book.FilePath) ?? "";
            if (Directory.Exists(bookDir))
            {
                ReaderWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "books.paperback.local",
                    bookDir,
                    CoreWebView2HostResourceAccessKind.Allow);
            }

            ReaderWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            ReaderWebView.CoreWebView2.Navigate("https://reader.paperback.local/reader.html");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to initialize reading engine:\n{ex.Message}",
                "Engine Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json = e.WebMessageAsJson;
            var node = JsonNode.Parse(json);
            if (node == null) return;

            string type = node["type"]?.GetValue<string>() ?? "";

            switch (type)
            {
                case "ready":
                    _isEngineReady = true;
                    await SendBookToEngineAsync();
                    break;

                case "pageChanged":
                    _currentPage = node["currentPage"]?.GetValue<int>() ?? 1;
                    _totalPages = node["totalPages"]?.GetValue<int>() ?? 1;
                    _progressPercent = node["progressPercent"]?.GetValue<double>() ?? 0.0;
                    UpdatePageProgressUi();
                    // Debounced progress persistence
                    _ = _libraryService.UpdateReadingProgressAsync(_book.Id, _currentPage, _totalPages, _progressPercent, null);
                    break;

                case "bookLoaded":
                    _totalPages = node["totalPages"]?.GetValue<int>() ?? 1;
                    PageSlider.Maximum = Math.Max(_totalPages, 1);
                    UpdatePageProgressUi();
                    break;

                case "toggleUi":
                    ToggleChromeVisibility();
                    break;
            }
        }
        catch
        {
            // Ignore parsing glitches
        }
    }

    private async Task SendBookToEngineAsync()
    {
        if (!_isEngineReady || ReaderWebView.CoreWebView2 == null) return;

        if (_book.Format.Equals("PDF", StringComparison.OrdinalIgnoreCase))
        {
            string fileName = Path.GetFileName(_book.FilePath);
            string fileUrl = $"https://books.paperback.local/{Uri.EscapeDataString(fileName)}";

            var payload = new
            {
                action = "loadBook",
                book = new
                {
                    format = "PDF",
                    title = _book.Title,
                    author = _book.Author,
                    fileUrl = fileUrl,
                    startPage = Math.Max(_book.CurrentPage, 1)
                }
            };
            ReaderWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }
        else
        {
            // EPUB Book
            var chapters = await ExtractEpubChaptersAsync(_book.FilePath);
            var payload = new
            {
                action = "loadBook",
                book = new
                {
                    format = "EPUB",
                    title = _book.Title,
                    author = _book.Author,
                    startPage = Math.Max(_book.CurrentPage, 1),
                    chapters = chapters
                }
            };
            ReaderWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }
    }

    private async Task<List<object>> ExtractEpubChaptersAsync(string epubPath)
    {
        var result = new List<object>();
        try
        {
            EpubBook epubBook = await EpubReader.ReadBookAsync(epubPath);
            if (epubBook.ReadingOrder != null)
            {
                int cIdx = 1;
                foreach (var file in epubBook.ReadingOrder)
                {
                    string rawHtml = file.Content ?? "";
                    // Strip HTML tags for clean paragraph formatting in 3D reader
                    string plainText = StripHtml(rawHtml);
                    if (!string.IsNullOrWhiteSpace(plainText))
                    {
                        result.Add(new
                        {
                            title = $"Chapter {cIdx++}",
                            text = plainText
                        });
                    }
                }
            }
        }
        catch
        {
            result.Add(new
            {
                title = _book.Title,
                text = _book.Description ?? "No text content found in book."
            });
        }

        if (result.Count == 0)
        {
            result.Add(new
            {
                title = _book.Title,
                text = "Welcome to Paperback. Happy reading!"
            });
        }
        return result;
    }

    private static string StripHtml(string html)
    {
        // Replace paragraph and break tags with newlines
        string text = html.Replace("</p>", "\n\n")
                          .Replace("<p>", "")
                          .Replace("<br/>", "\n")
                          .Replace("<br>", "\n")
                          .Replace("</div>", "\n")
                          .Replace("</h1>", "\n\n")
                          .Replace("</h2>", "\n\n")
                          .Replace("</h3>", "\n\n");
        return System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", string.Empty).Trim();
    }

    private async Task LoadTableOfContentsAsync()
    {
        if (_book.Format.Equals("EPUB", StringComparison.OrdinalIgnoreCase))
        {
            var toc = await _epubService.GetTableOfContentsAsync(_book.FilePath);
            TocListBox.ItemsSource = toc;
        }
    }

    private void UpdatePageProgressUi()
    {
        PageSlider.Value = _currentPage;
        PageProgressText.Text = $"Page {_currentPage} of {_totalPages} ({Math.Round(_progressPercent)}%)";
    }

    // Auto-hiding chrome logic
    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        ShowChrome();
        _autoHideTimer.Stop();
        _autoHideTimer.Start();
    }

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        // Only hide if popups are not open
        if (TypographyPopup.Visibility != Visibility.Visible && TocDrawer.Visibility != Visibility.Visible)
        {
            HideChrome();
        }
    }

    private void ShowChrome()
    {
        if (!_isChromeVisible)
        {
            _isChromeVisible = true;
            AnimateOpacity(TopBar, 1.0);
            AnimateOpacity(BottomBar, 1.0);
        }
    }

    private void HideChrome()
    {
        if (_isChromeVisible)
        {
            _isChromeVisible = false;
            AnimateOpacity(TopBar, 0.0);
            AnimateOpacity(BottomBar, 0.0);
        }
    }

    private void ToggleChromeVisibility()
    {
        if (_isChromeVisible) HideChrome();
        else ShowChrome();
    }

    private static void AnimateOpacity(UIElement elem, double target)
    {
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        elem.BeginAnimation(OpacityProperty, anim);
    }

    // Navigation & Page Turn Actions
    private void OnPrevPageClicked(object sender, RoutedEventArgs e)
    {
        PostAction("prevPage");
    }

    private void OnNextPageClicked(object sender, RoutedEventArgs e)
    {
        PostAction("nextPage");
    }

    private void OnPageSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isEngineReady && Math.Abs(e.NewValue - _currentPage) >= 1)
        {
            int target = (int)e.NewValue;
            var payload = new { action = "jumpToPage", page = target };
            ReaderWebView.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }
    }

    private void OnTocItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TocListBox.SelectedItem is ChapterItem chapter)
        {
            var payload = new { action = "jumpToPage", page = chapter.PageNumber };
            ReaderWebView.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
            TocDrawer.Visibility = Visibility.Collapsed;
        }
    }

    private void OnBackToLibraryClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnToggleTocClicked(object sender, RoutedEventArgs e)
    {
        TypographyPopup.Visibility = Visibility.Collapsed;
        TocDrawer.Visibility = TocDrawer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnCloseTocClicked(object sender, RoutedEventArgs e)
    {
        TocDrawer.Visibility = Visibility.Collapsed;
    }

    private void OnToggleTypographyClicked(object sender, RoutedEventArgs e)
    {
        TocDrawer.Visibility = Visibility.Collapsed;
        TypographyPopup.Visibility = TypographyPopup.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnToggleBookmarkClicked(object sender, RoutedEventArgs e)
    {
        var bookmark = new Bookmark
        {
            BookId = _book.Id,
            PageIndex = _currentPage,
            Title = $"Page {_currentPage}"
        };
        await _databaseService.AddBookmarkAsync(bookmark);

        System.Windows.MessageBox.Show(
            $"Bookmark saved for Page {_currentPage}!",
            "Bookmark Added",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void OnToggleFullscreenClicked(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
    }

    // Themes
    private void OnThemeWhiteClicked(object sender, MouseButtonEventArgs e) => ApplyTheme("light");
    private void OnThemeSepiaClicked(object sender, MouseButtonEventArgs e) => ApplyTheme("sepia");
    private void OnThemeMatteClicked(object sender, MouseButtonEventArgs e) => ApplyTheme("matte");
    private void OnThemeDarkClicked(object sender, MouseButtonEventArgs e) => ApplyTheme("dark");
    private void OnThemeOledClicked(object sender, MouseButtonEventArgs e) => ApplyTheme("oled");

    private void ApplyTheme(string theme)
    {
        _currentTheme = theme;
        var payload = new { action = "setTheme", theme = theme };
        ReaderWebView.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
    }

    // Typography
    private void OnFontFamilyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontFamilyCombo?.SelectedItem is ComboBoxItem item)
        {
            _currentFont = item.Content.ToString() ?? "Georgia";
            SendTypographyUpdate();
        }
    }

    private void OnFontSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _currentFontSize = e.NewValue;
        SendTypographyUpdate();
    }

    private void OnToggleJustifyClicked(object sender, RoutedEventArgs e)
    {
        _isJustified = true;
        JustifyButton.Appearance = ControlAppearance.Secondary;
        AlignLeftButton.Appearance = ControlAppearance.Transparent;
        SendTypographyUpdate();
    }

    private void OnAlignLeftClicked(object sender, RoutedEventArgs e)
    {
        _isJustified = false;
        AlignLeftButton.Appearance = ControlAppearance.Secondary;
        JustifyButton.Appearance = ControlAppearance.Transparent;
        SendTypographyUpdate();
    }

    private void SendTypographyUpdate()
    {
        if (!_isEngineReady || ReaderWebView.CoreWebView2 == null) return;
        var payload = new
        {
            action = "setTypography",
            typography = new
            {
                fontFamily = _currentFont,
                fontSize = _currentFontSize,
                lineHeight = 1.6,
                justifyText = _isJustified
            }
        };
        ReaderWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
    }

    private void PostAction(string action)
    {
        if (!_isEngineReady || ReaderWebView.CoreWebView2 == null) return;
        var payload = new { action = action };
        ReaderWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
    }
}
