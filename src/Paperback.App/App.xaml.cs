using System;
using System.IO;
using System.Windows;
using Paperback.Core.Interfaces;
using Paperback.Services.Database;
using Paperback.Services.Epub;
using Paperback.Services.Library;
using Paperback.Services.Pdf;
using Wpf.Ui.Appearance;

namespace Paperback.App;

public partial class App : Application
{
    public static IDatabaseService DatabaseService { get; private set; } = null!;
    public static IEpubService EpubService { get; private set; } = null!;
    public static IPdfService PdfService { get; private set; } = null!;
    public static ILibraryService LibraryService { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Synchronize with Windows system theme (Dark or Light)
        ApplicationThemeManager.ApplySystemTheme();

        // Initialize Services
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string folder = Path.Combine(appData, "Paperback");
        Directory.CreateDirectory(folder);

        DatabaseService = new SqliteDatabaseService(Path.Combine(folder, "library.db"));
        EpubService = new VersOneEpubService();
        PdfService = new WindowsPdfService();
        LibraryService = new LibraryService(DatabaseService, EpubService, PdfService, Path.Combine(folder, "covers"));

        await LibraryService.InitializeAsync();

        // On first run, import bundled samples if library is empty
        if (LibraryService.Books.Count == 0)
        {
            string samplesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples");
            if (!Directory.Exists(samplesDir))
            {
                samplesDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "samples"));
            }

            if (Directory.Exists(samplesDir))
            {
                await LibraryService.ImportDirectoryAsync(samplesDir, recursive: false);
            }
        }
    }
}
