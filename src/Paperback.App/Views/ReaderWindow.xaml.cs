using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
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

    private bool _isEngineReady = false;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private double _progressPercent = 0.0;

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

        Title = $"{_book.Title} - Paperback";
        TitleBar.Title = _book.Title;

        Loaded += async (s, e) =>
        {
            await InitializeWebViewAsync();
        };

        Closed += async (s, e) =>
        {
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
                engineFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ReaderEngine"));
            }

            ReaderWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "reader.paperback.local",
                engineFolder,
                CoreWebView2HostResourceAccessKind.Allow);

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
                    _ = _libraryService.UpdateReadingProgressAsync(_book.Id, _currentPage, _totalPages, _progressPercent, null);
                    break;

                case "closeReader":
                    Close();
                    break;

                case "toggleFullscreen":
                    ToggleFullscreen();
                    break;

                case "addBookmark":
                    int bookmarkPage = node["page"]?.GetValue<int>() ?? _currentPage;
                    await AddBookmarkAsync(bookmarkPage);
                    break;
            }
        }
        catch
        {
            // Ignore parse errors
        }
    }

    private async Task SendBookToEngineAsync()
    {
        if (!_isEngineReady || ReaderWebView.CoreWebView2 == null) return;

        List<ChapterItem>? toc = null;
        if (_book.Format.Equals("EPUB", StringComparison.OrdinalIgnoreCase))
        {
            toc = await _epubService.GetTableOfContentsAsync(_book.FilePath);
        }

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
                    startPage = Math.Max(_book.CurrentPage, 1),
                    toc = toc
                }
            };
            ReaderWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }
        else
        {
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
                    chapters = chapters,
                    toc = toc
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

    private async Task AddBookmarkAsync(int page)
    {
        var bookmark = new Bookmark
        {
            BookId = _book.Id,
            PageIndex = page,
            Title = $"Page {page}"
        };
        await _databaseService.AddBookmarkAsync(bookmark);
    }

    private void ToggleFullscreen()
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
}
