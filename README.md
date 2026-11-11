# Paperback 📖

> A beautifully designed, distraction-free eBook reader for Windows, inspired by **Apple Books**. Built natively for Windows with **.NET 8** and **Fluent 2 (WPF / WPF-UI)**, featuring authentic **3D page-turning curl animations**, a tactile book shelf with drop shadows, and dedicated **EPUB** & **PDF** reading engines.

---

## ✨ Features

- **Apple Books Aesthetic on Windows**:
  - Windows 11 **Mica backdrop** material and Fluent 2 styling.
  - Frosted glass sidebar: *All Books*, *Reading Now*, *Finished*, *EPUB Books*, and *PDF Documents*.
  - Live search in the Windows 11 TitleBar with instant filtering by title or author.
  - Right-click context menus: *Read Now*, *Show in File Explorer*, *Mark as Finished*, *Remove from Library*.

- **Tactile 3D Book Covers**:
  - Realistic left-edge spine gradient highlight simulating curved physical book bindings.
  - Multi-level soft drop shadows and format badges (`EPUB` / `PDF`).
  - Automatic cover extraction & local caching.
  - Classic clothbound cover fallback (classic navy for EPUB, deep wine burgundy for PDF) when books have no embedded art.
  - Discrete reading progress track (`ProgressBar`) displaying percent completed.
  - Hover elevation micro-interactions (1.03x scale lift with quadratic easing).

- **Physics-Based 3D Page Turn Animation**:
  - Hardware-accelerated 3D page curl physics simulation with corner peeling on mouse drag.
  - Dynamic gradient lighting, curvature fold highlights, soft drop shadow cast on adjacent pages, and reverse-side page translucency.
  - **Two-Page Spread** mode for widescreen displays with center spine crease shadow, and **Single-Page** mode for portrait or compact windows.
  - Smooth keyboard navigation (`Right Arrow` / `Space` to flip forward, `Left Arrow` to flip back).

- **Typography & Reading Themes (`Aa` Menu)**:
  - 5 refined color palettes:
    1. **Light**: `#FFFFFF` crisp white paper, charcoal text.
    2. **Sepia**: `#F8F1E5` warm parchment, `#382E2B` espresso ink.
    3. **Matte Gray**: `#E8E8EC` cool paper tone, neutral text.
    4. **Dark**: `#1C1C1E` deep slate, `#F2F2F7` warm white text.
    5. **OLED Black**: `#000000` absolute black, `#D1D1D6` soft white text.
  - Typography options: Georgia, Palatino Linotype, Segoe UI, Times New Roman, Consolas.
  - Live font size slider (13px to 30px), line height, and text alignment (Justified vs Left).

- **Distraction-Free Immersion**:
  - Auto-hiding floating chrome bars (fade out after 2.8s of inactivity, restore on mouse movement or page click).
  - Table of Contents slide-out drawer with direct chapter jumping.
  - Visual bookmark ribbon in the top-right corner.
  - Reading progress scrubber slider (`Page X of Y • Z%`).

- **Drag-and-Drop & Local-First**:
  - Drag any `.epub` or `.pdf` file directly into the Paperback window to import instantly with an animated glowing drop zone.
  - 100% offline local SQLite database stored at `%LocalAppData%\Paperback\library.db`.
  - Zero cloud dependencies, zero telemetry, zero accounts.

---

## 📥 Download & Installation

### Option 1: Download from GitHub Releases (Recommended)
1. Go to the [Releases](https://github.com) section of this repository.
2. Download the latest `Paperback-win-x64.zip`.
3. Extract the `.zip` archive to any folder (e.g. `C:\Program Files\Paperback` or your user folder).
4. Double-click `Paperback.exe` to run!
   *(No need to install .NET 8 or any other prerequisites—the app is fully self-contained).*

### Option 2: Download Artifact from GitHub Actions
For any push to `main`, GitHub Actions automatically compiles and packages a fresh build:
1. Navigate to the **Actions** tab in GitHub.
2. Click on the latest workflow run.
3. Under **Artifacts**, download `Paperback-Windows-x64.zip`.

---

## ⌨️ Keyboard Shortcuts & Controls

| Action | Control |
| :--- | :--- |
| **Next Page** | `Right Arrow`, `Space`, `Page Down`, or click right margin |
| **Previous Page** | `Left Arrow`, `Page Up`, or click left margin |
| **Interactive Page Peel** | Click and drag any page corner |
| **Toggle Distraction-Free Chrome** | Click middle region of the page |
| **Toggle Fullscreen** | Click fullscreen button in top bar or `F11` |
| **Close Reader / Back to Library** | Click "Library" back button in top bar |
| **Search Library** | Type in the TitleBar search box |

---

## 🛠️ Building From Source

### Prerequisites
- Windows 10 (1809+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Clone & Build
```powershell
# Clone the repository
git clone https://github.com/<your-username>/Paperback.git
cd Paperback

# Build solution
dotnet build Paperback.sln -c Release

# Run automated tests
dotnet test Paperback.sln

# Run Paperback
dotnet run --project src/Paperback.App/Paperback.App.csproj
```

### Produce a Standalone Single-File Release
```powershell
dotnet publish src/Paperback.App/Paperback.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
```

---

## 🏗️ Architecture

```
Paperback/
├── .github/workflows/       # GitHub Actions automated build, test & release CI/CD
├── samples/                 # Sample EPUB & PDF documents
└── src/
    ├── Paperback.Core/      # Domain models, enums & service interfaces
    ├── Paperback.Services/  # SQLite database, VersOne.Epub, PDF & Library services
    ├── Paperback.App/       # Fluent 2 WPF UI + WebView2 3D page curl engine
    └── Paperback.Tests/     # xUnit automated test suite
```

---

## 📄 License
MIT License. Built for readers who appreciate craftsmanship.
