using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Paperback.Core.Models;

namespace Paperback.App.Views.Controls;

public partial class BookCard : UserControl
{
    public static readonly DependencyProperty BookProperty =
        DependencyProperty.Register(nameof(Book), typeof(Book), typeof(BookCard),
            new PropertyMetadata(null, OnBookChanged));

    public Book? Book
    {
        get => (Book?)GetValue(BookProperty);
        set => SetValue(BookProperty, value);
    }

    public event EventHandler<Book>? BookOpened;
    public event EventHandler<Book>? BookRemoved;
    public event EventHandler<Book>? BookFinished;

    public BookCard()
    {
        InitializeComponent();
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
    }

    private static void OnBookChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BookCard card && e.NewValue is Book book)
        {
            card.UpdateCard(book);
        }
    }

    private void UpdateCard(Book book)
    {
        TitleText.Text = book.Title;
        AuthorText.Text = book.Author;
        FormatText.Text = book.Format.ToUpperInvariant();

        // Progress text and bar
        CoverProgressBar.Value = book.ProgressPercent;
        if (book.IsFinished)
        {
            ProgressText.Text = "Finished";
            ProgressText.Opacity = 0.8;
        }
        else if (book.ProgressPercent > 0)
        {
            ProgressText.Text = $"{Math.Round(book.ProgressPercent)}% read";
            ProgressText.Opacity = 1.0;
        }
        else
        {
            ProgressText.Text = "Unread";
            ProgressText.Opacity = 0.5;
        }

        // Cover handling
        if (!string.IsNullOrEmpty(book.CoverImagePath) && File.Exists(book.CoverImagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(book.CoverImagePath, UriKind.Absolute);
                bitmap.EndInit();

                CoverImage.Source = bitmap;
                CoverImage.Visibility = Visibility.Visible;
                FallbackCover.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ShowFallbackCover(book);
            }
        }
        else
        {
            ShowFallbackCover(book);
        }
    }

    private void ShowFallbackCover(Book book)
    {
        CoverImage.Visibility = Visibility.Collapsed;
        FallbackCover.Visibility = Visibility.Visible;
        FallbackTitle.Text = book.Title;
        FallbackAuthor.Text = book.Author;

        // Give EPUB and PDF slightly different aesthetic palette tints
        if (book.Format.Equals("PDF", StringComparison.OrdinalIgnoreCase))
        {
            FallbackCover.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x40, 0x22, 0x28)); // Deep wine burgundy for PDF
        }
        else
        {
            FallbackCover.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x1F, 0x2C, 0x3E)); // Classic clothbound navy for EPUB
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        var animScale = new DoubleAnimation(1.03, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);

        var animShadow = new DoubleAnimation(26, TimeSpan.FromMilliseconds(180));
        CardShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, animShadow);
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        var animScale = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);

        var animShadow = new DoubleAnimation(18, TimeSpan.FromMilliseconds(200));
        CardShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, animShadow);
    }

    private void OnCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (Book != null)
        {
            BookOpened?.Invoke(this, Book);
        }
    }

    private void OnReadClicked(object sender, RoutedEventArgs e)
    {
        if (Book != null) BookOpened?.Invoke(this, Book);
    }

    private void OnShowInExplorerClicked(object sender, RoutedEventArgs e)
    {
        if (Book != null && File.Exists(Book.FilePath))
        {
            Process.Start("explorer.exe", $"/select,\"{Book.FilePath}\"");
        }
    }

    private void OnMarkFinishedClicked(object sender, RoutedEventArgs e)
    {
        if (Book != null) BookFinished?.Invoke(this, Book);
    }

    private void OnRemoveClicked(object sender, RoutedEventArgs e)
    {
        if (Book != null) BookRemoved?.Invoke(this, Book);
    }
}
