<div align="center">

<br />

# Ogma Library

**Your personal PDF library — beautifully managed, intelligently advised.**

<br />

[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS-1A5C38?style=for-the-badge&logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0id2hpdGUiIGQ9Ik0wIDBoMTJ2MTJIMHoiLz48cGF0aCBmaWxsPSJ3aGl0ZSIgZD0iTTEzIDBoMTF2MTJIMTMiLz48cGF0aCBmaWxsPSJ3aGl0ZSIgZD0iTTAgMTNoMTJ2MTFIMCJ6Ii8+PHBhdGggZmlsbD0id2hpdGUiIGQ9Ik0xMyAxM2gxMXYxMUgxMyIvPjwvc3ZnPg==)](https://github.com/peterbamuhigire/ogma-library)
[![Framework](https://img.shields.io/badge/.NET%2010%20%2F%20Avalonia%20UI-512BD4?style=for-the-badge)](https://avaloniaui.net/)
[![Language](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/license-MIT-C4922A?style=for-the-badge)](LICENSE)
[![Status](https://img.shields.io/badge/status-In%20Development-orange?style=for-the-badge)](https://github.com/peterbamuhigire/ogma-library/projects)

<br />

*Named after **Ogma** — the Celtic Irish deity who invented the Ogham alphabet,*
*the first writing system native to the British Isles.*
*Your collection, carved into stone.*

<br />

---

</div>

Ogma Library turns a folder full of PDF files into a living, curated personal library. It scans your collection, enriches every book's metadata from global book databases, generates cover thumbnails, and presents your library in a stunning **3D virtual bookshelf**. Its defining feature: describe what you want to read in plain language and the built-in **AI reading advisor** finds the best matching books from your own collection — and explains exactly why each one fits.

> **Status:** Active development. Core scanning, metadata, and reader are being built in Phase 1–3. The 3D bookshelf and AI advisor follow in Phase 4–5. See the [Roadmap](#roadmap) for current progress.

---

## Table of contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Architecture](#architecture)
- [Project structure](#project-structure)
- [Getting started](#getting-started)
- [Network drive support](#network-drive-support)
- [Database portability](#database-portability)
- [AI reading advisor](#ai-reading-advisor)
- [Metadata enrichment](#metadata-enrichment)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [Third-party acknowledgements](#third-party-acknowledgements)
- [About](#about)
- [License](#license)

---

## Features

### 📚 Library management
- Scan any local folder or network drive (Windows + macOS) as your **library root**
- Automatic incremental rescans — only processes files that have changed
- **Portable database:** move `ogma.db` to any machine, point it at the same books, and everything — metadata, covers, reading progress, bookmarks, shelves — loads instantly with no rescanning
- Supports local drives, NAS shares, mapped drives, UNC paths (`\\server\share`), and macOS SMB/AFP mounts (`/Volumes/...`)
- Graceful **offline mode** when a network drive is unavailable — full catalogue still browsable

### 🔍 Metadata enrichment
- Automatic **ISBN detection** from PDF text content, XMP metadata, and filenames
- One-click enrichment from **Open Library** (free, no key), **Google Books** (free with API key), **ISBNdb** (premium), and **WorldCat**
- **Write metadata back into the PDF file** — title, authors, publisher, categories written to XMP and DocInfo via PdfSharp. Non-destructive: original file backed up before any modification
- Fields stored: title, subtitle, authors, publisher, year, edition, language, page count, description, ISBN-10/13, categories, tags, series, cover source, and external database IDs

### 🏛 Views
| View | Description |
|---|---|
| **3D Bookshelf** | Photorealistic book spines on wooden shelves rendered with Three.js. Hover to tilt forward, click to inspect, double-click to read. Four shelf themes. |
| **Grid** | Cover cards in a resizable masonry layout — the natural way to browse a visual collection. |
| **List** | Sortable, filterable DataGrid with all metadata columns. Best for finding specific books and bulk operations. |
| **Directory** | Split-pane view of your folder tree and the books inside each folder. |

### 📖 PDF reader
Built on **PDFium** — the same rendering engine as Google Chrome.

- All zoom modes: fit to width, fit to page, actual size, custom percentage
- Single page, two-page spread (book mode), and continuous scroll layouts
- **Full-screen mode** — all UI hidden, minimal toolbar on mouse approach
- **In-document text search** — all matches highlighted across all pages
- **Bookmarks** — create, label, and jump to bookmarks stored in the database
- **Highlights and annotations** — 5 highlight colours, text notes pinned to any position. Stored in the database, never modifying the PDF file
- **Reading progress** — last page saved on every turn, restored on every reopen
- **Cumulative reading time** tracked per book
- Night mode and sepia mode

### 🤖 AI reading advisor
- Describe what you want to read in plain language — the AI analyses your entire library and returns **12 best-matching books** with a personalised explanation for each
- Supports **OpenAI**, **Anthropic Claude**, **DeepSeek**, and **Ollama** (fully local — no data leaves your machine)
- Two-pass strategy for large libraries: efficient even with 2,000+ books
- API keys stored in the **OS credential manager** (Windows Credential Manager / macOS Keychain) — never in config files or the database
- Query history saved: re-run previous queries instantly
- Optional **reading plan**: suggests an order to read the recommended books

### 🗂 Organisation
- **Virtual shelves** — named collections independent of folder structure
- Built-in system shelves: Favorites, Currently Reading, Finished
- **Tags** — free-form user labels per book
- **Star ratings** (0–5)
- **Reading statistics** — books per month chart, pages per day, reading streak, genre breakdown

---

## Screenshots

> *Coming with the first public release — Phase 4.*

---

## Architecture

| Component | Technology |
|---|---|
| UI framework | [Avalonia UI 11](https://avaloniaui.net/) (.NET 10) — native Windows and macOS from one codebase |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) |
| 3D bookshelf | [Three.js](https://threejs.org/) in an embedded WebView2 (Windows) / WKWebView (macOS) |
| PDF rendering | [PDFium](https://pdfium.googlesource.com/pdfium/) via Pdfium.Net.SDK |
| PDF metadata write | [PdfSharp](https://www.pdfsharp.net/) |
| PDF parsing / extraction | [PdfPig](https://uglytoad.github.io/PdfPig/) |
| Database | SQLite via [Entity Framework Core 10](https://learn.microsoft.com/en-us/ef/core/) |
| Image processing | [SkiaSharp](https://github.com/mono/SkiaSharp) |
| HTTP / external APIs | `System.Net.Http.HttpClient` + `System.Text.Json` |
| Credential storage | OS credential manager via `Microsoft.Extensions.SecretManagement` |
| Auto-update | [Velopack](https://velopack.io/) (direct download) · MSIX (Microsoft Store) |

---

## Project structure

```
OgmaLibrary.sln
│
├── OgmaLibrary.App/                 ← Avalonia UI project
│   ├── Views/                       ← All application screens (AXAML)
│   ├── ViewModels/                  ← CommunityToolkit.Mvvm ViewModels
│   ├── Controls/                    ← PdfiumView, WebView3DControl, CoverCard, StarRating
│   └── Assets/
│       ├── bookshelf.html           ← Self-contained Three.js bookshelf scene
│       ├── Fonts/                   ← DM Sans, JetBrains Mono
│       └── Themes/                  ← Light.axaml, Dark.axaml
│
├── OgmaLibrary.Core/                ← Business logic (no Avalonia dependency)
│   ├── Models/                      ← Book, ReadingProgress, Bookmark, Annotation, Shelf
│   ├── Services/
│   │   ├── LibraryScannerService    ← File walk, relative path, SHA256 hash
│   │   ├── ThumbnailService         ← PDFium → SkiaSharp → WebP
│   │   ├── SpineTextureService      ← Cover + title → Three.js spine texture
│   │   ├── MetadataService          ← In-PDF XMP and DocInfo extraction
│   │   ├── IsbnLookupService        ← Google Books, Open Library, ISBNdb
│   │   ├── PdfMetadataWriterService ← PdfSharp write-back to PDF file
│   │   ├── AiAdvisorService         ← Prompt builder and provider routing
│   │   ├── ReadingProgressService   ← Last page, %, time tracking
│   │   └── CredentialService        ← OS credential store abstraction
│   └── Data/
│       ├── OgmaDbContext.cs         ← EF Core DbContext
│       ├── Migrations/
│       └── Repositories/
│
└── OgmaLibrary.Tests/               ← xUnit tests for Core services
```

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10) or later
- Windows 10 version 1903+ **or** macOS 12 Monterey+
- (Optional) A [Google Books API key](https://developers.google.com/books/docs/v1/using#APIKey) for metadata enrichment
- (Optional) An API key for your preferred AI provider — [OpenAI](https://platform.openai.com/), [Anthropic](https://console.anthropic.com/), or [DeepSeek](https://platform.deepseek.com/)

### Build from source

```bash
git clone https://github.com/peterbamuhigire/ogma-library.git
cd ogma-library
dotnet restore
dotnet build
dotnet run --project OgmaLibrary.App
```

### First run

1. On first launch, click **Set library root** and select the folder (or network path) containing your PDF books
2. Ogma scans the folder and generates cover thumbnails — this runs once; subsequent scans are incremental and fast
3. Browse your library using the view toggle in the toolbar: **3D Bookshelf**, **Grid**, **List**, or **Directory**
4. Click any book to open the inspector panel — cover, all metadata, reading progress, quick actions
5. Click **Read Now** to open the book in the built-in PDF reader

### Set up metadata enrichment

1. Go to **Settings → Metadata**
2. Add a Google Books API key (free — [get one here](https://developers.google.com/books/docs/v1/using#APIKey))
3. Back in the library, right-click any book → **Fetch metadata**, or select multiple books → **Bulk enrich**

### Set up the AI advisor

1. Go to **Settings → AI Advisor**
2. Choose your provider (OpenAI, Anthropic, DeepSeek, or Ollama for fully local)
3. Paste your API key — it is stored in the OS credential manager, never in a file
4. Open the **Ogma AI** panel from the sidebar and start describing what you want to read

---

## Network drive support

Ogma Library works with PDF libraries stored on a network-attached storage device (NAS) or any shared folder on your local network.

### Windows

Set your library root to a UNC path or a mapped drive letter:

```
\\192.168.1.100\books\
\\NAS-NAME\library\
Z:\Books\
```

### macOS

Mount the SMB share first in Finder (`⌘K → smb://192.168.1.100/books`), then set the library root to the mount point:

```
/Volumes/Books/
/Volumes/NAS/Library/
```

### How it works

- The **PDF files** live on the network drive
- The **database** (`ogma.db`) and **thumbnails** (`.ogma/thumbnails/`) live in a sidecar folder on the network drive alongside the books — any machine that mounts the same share has instant access to all metadata and covers without rescanning
- If the network drive is **offline** when Ogma starts, the app enters offline mode: your full catalogue is browsable from a local cache, PDF reading resumes automatically when the drive reconnects
- A background monitor checks for reconnection every 30 seconds and triggers an incremental scan automatically

> **Multiple machines, same user:** place `ogma.db` on the network drive so reading progress stays consistent between your desktop and laptop. See the full network architecture specification in `docs/network-drive-support.md`.

---

## Database portability

Everything Ogma knows about your library lives in a single, portable SQLite file.

```
~/Documents/OgmaLibrary/
├── ogma.db                  ← all metadata, reading progress, bookmarks, shelves
└── .ogma/
    └── thumbnails/          ← WebP cover thumbnails, one per book
```

**To move your library to another machine:**

1. Copy `ogma.db` and the `.ogma/` sidecar folder
2. Copy your book files (or point to the same network share)
3. Install Ogma Library on the new machine
4. Set the library root to where the books are
5. Everything loads instantly — no rescanning

**How matching works:**

Books are matched to their database records by **relative path** within the library root. If the directory structure is preserved when copying, every book matches immediately. If files were reorganised, Ogma falls back to matching by **SHA256 content hash** — reading progress is never lost even if you renamed a folder.

---

## AI reading advisor

The AI advisor is the feature that separates Ogma Library from every other PDF manager.

### Example queries

```
I want to understand how the human brain forms and changes habits

Something practical about building and scaling a software startup — people, product, and growth

Deep technical content on distributed systems, consensus algorithms, and fault tolerance

A gripping biography — someone who changed the course of history through sheer force of will

I have a job interview at a tech company next week, help me prepare fast
```

The AI reads your entire library catalogue — titles, authors, categories, descriptions — and returns the **12 books that best match your intent**, with a specific, personalised explanation for each recommendation. It never recommends a book that is not in your library.

### Supported providers

| Provider | Models | Data sent |
|---|---|---|
| OpenAI | `gpt-4o`, `gpt-4o-mini` | Book titles, authors, categories to OpenAI API |
| Anthropic | `claude-sonnet-4-6` | Book titles, authors, categories to Anthropic API |
| DeepSeek | `deepseek-chat` | Book titles, authors, categories to DeepSeek API |
| **Ollama (local)** | `llama3.1:8b`, `mistral:7b`, any installed model | **Nothing — runs entirely on your machine** |

> **Privacy:** The AI advisor sends only book metadata (titles, authors, categories). Your PDF file contents never leave your machine regardless of which provider you use.

All API keys are stored in the OS credential manager. They are never written to config files, the database, or log files.

---

## Metadata enrichment

Most PDF books have poor or missing metadata. A scanned book may have `Title = "Untitled"` and `Author = ""`. Ogma fixes this.

### ISBN detection

Ogma searches for the book's ISBN automatically in:

1. The PDF's XMP metadata and DocInfo dictionary
2. The text content of the first 10 pages (where copyright pages typically appear)
3. The filename itself

### Online databases

| Database | Key required | Best for |
|---|---|---|
| **Open Library** | None — always free | Starting point for all books. No rate limits. |
| **Google Books** | Free Google API key | Mainstream and technical books. Richer descriptions. |
| **ISBNdb** | Paid subscription | Older, academic, and obscure titles. Most comprehensive. |
| **WorldCat** | Free OCLC key | Academic and library-catalogued books. |

**Strategy:** Ogma tries Open Library first, then Google Books. If your library has ISBNdb or WorldCat keys configured, they are used as fallbacks for books neither free database covers.

### Write back to PDF

After enriching from an online database, Ogma can write the metadata into the actual PDF file — title, authors, publisher, categories — via PdfSharp. This makes the files properly tagged in any other application.

- Original file is backed up as `filename.pdf.ogma.bak` before modification
- Modification is non-destructive and reversible
- Works on individual books or as a bulk operation on selected books

---

## Roadmap

| Phase | Timeline | Deliverables | Status |
|---|---|---|---|
| **1 — Foundation** | Weeks 1–3 | Avalonia shell, EF Core database, library scanner, thumbnail generation, basic list view, portable database verified | 🔨 In progress |
| **2 — Metadata** | Weeks 4–6 | ISBN detection, Open Library + Google Books, bulk enrichment, PdfSharp write-back, inspector panel | ⏳ Planned |
| **3 — Reader** | Weeks 7–9 | PDFium reader, all zoom modes, full-screen, reading progress, bookmarks, highlights, annotations | ⏳ Planned |
| **4 — 3D Bookshelf** | Weeks 10–12 | Three.js scene, spine textures, hover/click animations, four shelf themes, view toggle | ⏳ Planned |
| **5 — AI Advisor** | Weeks 13–14 | All four AI providers, two-pass prompting, 12-book result grid, reading plan, query history | ⏳ Planned |
| **6 — Polish + Launch** | Weeks 15–16 | Virtual shelves, reading stats, MSIX packaging, notarized macOS DMG | ⏳ Planned |

**Post-launch:**
- EPUB and CBZ support
- Full-text search index (search inside all books)
- Shared library across multiple machines with Litestream sync
- Multi-user shared catalogue (NAS + per-user local databases)
- In-app SMB share mounting (macOS)
- Goodreads CSV import

---

## Contributing

Ogma Library is developed by [Peter Bamuhigire](https://techguypeter.com) under [Chwezi Core Systems](https://chwezicore.com). Contributions, issues, and feature suggestions are welcome.

**To contribute:**

1. Fork the repository
2. Create a feature branch: `git checkout -b feat/your-feature`
3. Commit with a descriptive message: `git commit -m 'feat: add reading streak chart to stats view'`
4. Push: `git push origin feat/your-feature`
5. Open a pull request against `main`

**Code standards:**
- C# latest, .NET 10, nullable reference types enabled
- `async`/`await` throughout — no blocking calls on the UI thread
- No raw SQL — use EF Core repositories in `OgmaLibrary.Core`
- Business logic in `OgmaLibrary.Core` only — no Avalonia dependencies in Core
- New services should have corresponding xUnit tests in `OgmaLibrary.Tests`

---

## Third-party acknowledgements

Ogma Library is built on the shoulders of excellent open-source projects:

| Project | License | Purpose |
|---|---|---|
| [Avalonia UI](https://avaloniaui.net/) | MIT | Cross-platform .NET UI framework |
| [PDFium](https://pdfium.googlesource.com/pdfium/) | BSD 3-Clause | PDF rendering engine |
| [Three.js](https://threejs.org/) | MIT | 3D graphics for the bookshelf scene |
| [PdfPig](https://uglytoad.github.io/PdfPig/) | Apache 2.0 | In-PDF text and metadata extraction |
| [PdfSharp](https://www.pdfsharp.net/) | MIT | PDF metadata write-back |
| [QRCoder](https://github.com/codebude/QRCoder) | MIT | LAN Host QR join-code generation |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT | Thumbnail and spine texture generation |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | MVVM framework |
| [Entity Framework Core](https://github.com/dotnet/efcore) | MIT | SQLite ORM |
| [Velopack](https://velopack.io/) | MIT | Cross-platform installer and auto-updater |

Metadata sourced from [Open Library](https://openlibrary.org/) (Internet Archive) and [Google Books](https://developers.google.com/books).

---

## About

> *Ogma (also Ogmios in Gaulish tradition) is the Irish deity of eloquence, literacy, and the written word, from the Ulster Cycle — the great body of pre-Christian Irish mythology. He is credited with inventing the Ogham alphabet: an ancient script cut as strokes and notches along the edges of standing stones across Ireland, Scotland, Wales, and the Isle of Man. Thousands of Ogham stones survive to this day — a permanent, carved record that has outlasted kingdoms, languages, and civilisations.*
>
> *Your library deserves the same permanence.*

---

<div align="center">

Built by **[Peter Bamuhigire](https://techguypeter.com)**

[Chwezi Core Systems](https://chwezicore.com) · Uganda 🇺🇬

<br />

[MIT License](LICENSE) · [Report a bug](https://github.com/peterbamuhigire/ogma-library/issues) · [Request a feature](https://github.com/peterbamuhigire/ogma-library/discussions)

</div>
