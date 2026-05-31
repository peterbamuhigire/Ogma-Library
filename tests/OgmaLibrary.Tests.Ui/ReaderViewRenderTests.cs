using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.Views.Reader;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Headless UI tests for the production reader surface.
/// </summary>
public sealed class ReaderViewRenderTests
{
    private static string ArtifactsDir
    {
        get
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "screenshots");
            Directory.CreateDirectory(dir);
            return Path.GetFullPath(dir);
        }
    }

    /// <summary>
    /// Renders the production reader view with Phase 09 bookmark/layer/memory panels.
    /// </summary>
    [AvaloniaFact]
    public void ReaderView_RendersPhase09Panels_CapturesScreenshot()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");

        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        var window = new Window
        {
            Title = "Ogma Library - Reader",
            Width = 800,
            Height = 600,
            Content = new ReaderView { DataContext = viewModel },
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        string screenshotPath = Path.Combine(ArtifactsDir, "reader-en.png");
        frame!.Save(screenshotPath);

        string devGuideDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "docs", "developer-guide", "images");
        Directory.CreateDirectory(devGuideDir);
        File.Copy(screenshotPath, Path.Combine(devGuideDir, "reader-en.png"), overwrite: true);

        Assert.True(frame.Size.Width > 100, "Rendered frame width should be > 100px");
        Assert.True(frame.Size.Height > 100, "Rendered frame height should be > 100px");
        Assert.True(viewModel.IsOpen);
        Assert.NotEmpty(viewModel.Bookmarks);
        Assert.NotEmpty(viewModel.Layers);
        Assert.NotEmpty(viewModel.Annotations);
        Assert.NotEmpty(viewModel.AnnotationOverlays);
    }

    /// <summary>
    /// Verifies French localization strings for the reader are non-empty and non-sentinel.
    /// </summary>
    [AvaloniaFact]
    public void Reader_FrenchLocalization_NoMissingStrings()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("fr");

        string[] readerKeys =
        [
            "Reader.Navigation.FirstPage",
            "Reader.Navigation.PreviousPage",
            "Reader.Navigation.NextPage",
            "Reader.Navigation.LastPage",
            "Reader.Zoom.FitWidth",
            "Reader.Zoom.FitPage",
            "Reader.Display.SinglePage",
            "Reader.Display.TwoPage",
            "Reader.Display.Continuous",
            "Reader.FullScreen.Enter",
            "Reader.FullScreen.Exit",
            "Reader.Search.Placeholder",
            "Reader.Search.NoResults",
            "Reader.Panel.Title",
            "Annotation.Panel",
            "Annotation.Highlight.Color",
            "Annotation.Highlight.LayerColor",
            "Annotation.SelectionQuoteFormat",
            "Annotation.Delete",
            "Annotation.Deleted",
            "Annotation.Delete.Confirm",
            "Annotation.Delete.Cancel",
            "Annotation.Delete.Confirmation",
            "Annotation.Note.Edit",
            "Annotation.Note.Editor",
            "Annotation.Note.Saved",
            "Bookmark.Panel",
            "Bookmark.Panel.AccessibleFormat",
            "Bookmark.Renamed",
            "Bookmark.Removed",
            "Layer.Panel",
            "Layer.Filter",
            "Layer.Filter.AllVisible",
            "Layer.Merge",
            "Layer.Delete",
            "Layer.AtLeastOne",
            "Layer.Renamed",
            "Layer.Deleted",
            "Layer.Merged",
            "Citation.Capture",
            "Citation.Copy",
            "Citation.Export",
            "Citation.Copied",
            "Citation.Exported",
            "Citation.PageFormat",
            "Citation.UnknownTitle",
            "Citation.UnknownAuthor",
            "Citation.NoSelection",
            "ReadingMemory.Open",
            "ReadingMemory.Save",
            "ReadingMemory.Saved",
            "ReadingMemory.InvalidDisposition",
            "Icon.ic_close.Label",
            "Icon.ic_annotation_highlight.Label",
            "Icon.ic_annotation_highlight_color.Label",
            "Icon.ic_annotation_note.Label",
            "Icon.ic_annotation_note_anchor.Label",
            "Icon.ic_annotation_delete.Label",
            "Icon.ic_annotation_panel.Label",
            "Icon.ic_bookmark_add.Label",
            "Icon.ic_bookmark_remove.Label",
            "Icon.ic_bookmark_panel.Label",
            "Icon.ic_bookmark_item.Label",
            "Icon.ic_bookmark_rename.Label",
            "Icon.ic_layer_panel.Label",
            "Icon.ic_layer_add.Label",
            "Icon.ic_layer_visible.Label",
            "Icon.ic_layer_hidden.Label",
            "Icon.ic_layer_merge.Label",
            "Icon.ic_layer_delete.Label",
            "Icon.ic_citation_capture.Label",
            "Icon.ic_citation_copy.Label",
            "Icon.ic_citation_export.Label",
            "Icon.ic_reading_memory.Label",
            "Icon.ic_reading_memory_disposition.Label",
        ];

        foreach (string key in readerKeys)
        {
            string value = localization[key];
            Assert.False(
                value.StartsWith('\u27E6'),
                $"Missing French translation for key '{key}'");
        }
    }

    [AvaloniaFact]
    public void ReaderViewModel_CreateHighlight_PersistsAndBuildsOverlay()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Single(annotationService.CreatedAnnotations);
        Assert.Single(viewModel.Annotations);
        Assert.Single(viewModel.AnnotationOverlays);
        Assert.Equal("book-001", annotationService.CreatedAnnotations[0].BookId);
        Assert.Equal(0, annotationService.CreatedAnnotations[0].Regions[0].PageIndex);
    }

    [AvaloniaFact]
    public void ReaderViewModel_CreateHighlight_UsesLayerColorUntilSwatchOverrideSelected()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.AddLayerAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel
            .SetLayerVisibilityAsync(viewModel.Layers[0], isVisible: false, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal("#88AA77", annotationService.CreatedAnnotations[0].HighlightColor);
        Assert.Equal("#88AA77", viewModel.AnnotationOverlays[0].Color);
        Assert.Equal("#88AA77", viewModel.HighlightColorOptions[0].Color);
        Assert.True(viewModel.HighlightColorOptions[0].IsSelected);

        HighlightColorOption clayOverride = Assert.Single(
            viewModel.HighlightColorOptions,
            option => option.Color == "#C7795A" && !option.IsLayerDefault);
        viewModel.SelectHighlightColor(clayOverride);
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal("#C7795A", annotationService.CreatedAnnotations[1].HighlightColor);
        Assert.Equal("#88AA77", viewModel.Layers.Single(layer => layer.IsVisible).Color);
        Assert.Contains(viewModel.HighlightColorOptions, option =>
            option.Color == "#C7795A" && option.IsSelected);
    }

    [AvaloniaFact]
    public void ReaderViewModel_SelectionHighlight_UsesDraggedRegionAndClearsSelection()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.BeginSelection(100, 200);
        viewModel.UpdateSelection(220, 260);
        viewModel.CompleteSelection();

        Assert.True(viewModel.HasSelection);
        viewModel.CreateHighlightFromSelectionAsync(CancellationToken.None).GetAwaiter().GetResult();

        AnnotationV2 annotation = Assert.Single(annotationService.CreatedAnnotations);
        AnnotationRegion region = Assert.Single(annotation.Regions);
        Assert.Equal(100.0 / viewModel.PageSurfaceWidth, region.NormLeft, precision: 4);
        Assert.Equal(200.0 / viewModel.PageSurfaceHeight, region.NormTop, precision: 4);
        Assert.Equal(120.0 / viewModel.PageSurfaceWidth, region.NormWidth, precision: 4);
        Assert.Equal(60.0 / viewModel.PageSurfaceHeight, region.NormHeight, precision: 4);
        Assert.Equal("Selection on page 1", annotation.QuoteText);
        Assert.False(viewModel.HasSelection);
        Assert.Single(viewModel.AnnotationOverlays);
    }

    [AvaloniaFact]
    public void ReaderViewModel_RotatedSelectionHighlight_StoresUnrotatedRegion()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService
            {
                ZoomMode = ZoomMode.Fixed,
                ZoomPercent = 150,
                PageRotationDegrees = 90,
            },
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        viewModel.BeginSelection(1008, 108);
        viewModel.UpdateSelection(1152, 432);
        viewModel.CompleteSelection();
        viewModel.CreateHighlightFromSelectionAsync(CancellationToken.None).GetAwaiter().GetResult();

        AnnotationV2 annotation = Assert.Single(annotationService.CreatedAnnotations);
        AnnotationRegion region = Assert.Single(annotation.Regions);
        Assert.Equal(0.1, region.NormLeft, precision: 4);
        Assert.Equal(0.2, region.NormTop, precision: 4);
        Assert.Equal(0.3, region.NormWidth, precision: 4);
        Assert.Equal(0.1, region.NormHeight, precision: 4);
        Assert.Single(viewModel.AnnotationOverlays);
    }

    [AvaloniaFact]
    public void ReaderViewModel_SelectionCitation_CapturesSelectionText()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.BeginSelection(40, 45);
        viewModel.UpdateSelection(160, 95);
        viewModel.CompleteSelection();

        viewModel.CaptureCitationFromSelectionAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(viewModel.HasCitationCard);
        Assert.Equal("Selection on page 1", viewModel.CitationCard?.SelectedText);
        Assert.False(viewModel.HasSelection);
    }

    [AvaloniaFact]
    public void ReaderViewModel_SelectionCitation_UsesTextLayerWordsWhenAvailable()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization,
            new FakeTextLayerService(new TextLayer(
                0,
                [
                    new TextWord("Actual", 0.10, 0.10, 0.18, 0.16),
                    new TextWord("selected", 0.19, 0.10, 0.30, 0.16),
                    new TextWord("outside", 0.75, 0.75, 0.90, 0.82),
                ],
                ExtractionQuality.Full)));

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.BeginSelection(72, 96);
        viewModel.UpdateSelection(230, 180);
        viewModel.CompleteSelection();

        viewModel.CaptureCitationFromSelectionAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(viewModel.HasCitationCard);
        Assert.Equal("Actual selected", viewModel.CitationCard?.SelectedText);
        Assert.False(viewModel.HasSelection);
    }

    [AvaloniaFact]
    public void ReaderViewModel_SelectionTooSmall_ClearsSelection()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.BeginSelection(40, 45);
        viewModel.UpdateSelection(42, 47);
        viewModel.CompleteSelection();

        Assert.False(viewModel.HasSelection);
    }

    [AvaloniaFact]
    public void ReaderView_PageSurfaceDrag_OpensSelectionActionMenuWithFocusableActions()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        Window window = ShowReaderWindow(viewModel);
        try
        {
            var view = Assert.IsType<ReaderView>(window.Content);
            var pageSurface = Assert.IsType<Border>(view.FindControl<Border>("PageSurface"));

            Point start = pageSurface.TranslatePoint(new Point(120, 140), window)
                ?? throw new InvalidOperationException("Could not resolve selection start point.");
            Point end = pageSurface.TranslatePoint(new Point(260, 220), window)
                ?? throw new InvalidOperationException("Could not resolve selection end point.");

            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(end, RawInputModifiers.None);
            window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.HasSelection);

            var actionMenu = Assert.IsType<Border>(view.FindControl<Border>("SelectionActionMenu"));
            Assert.True(actionMenu.IsVisible);
            List<Button> actionButtons = actionMenu.GetVisualDescendants().OfType<Button>().ToList();

            Assert.Equal(3, actionButtons.Count);
            Assert.Contains(actionButtons, button => GetAutomationName(button) == viewModel.CreateHighlightLabel);
            Assert.Contains(actionButtons, button => GetAutomationName(button) == viewModel.CreateNoteLabel);
            Assert.Contains(actionButtons, button => GetAutomationName(button) == viewModel.CaptureCitationLabel);

            foreach (Button button in actionButtons)
            {
                button.Focus();
                Dispatcher.UIThread.RunJobs();
                Assert.True(button.IsFocused, $"Selection action '{GetAutomationName(button)}' should accept keyboard focus.");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderViewModel_OverlayUsesSessionRotationAndFixedZoom()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var sessionService = new FakeReaderSessionService
        {
            ZoomMode = ZoomMode.Fixed,
            ZoomPercent = 150,
            PageRotationDegrees = 90,
        };
        var annotationService = new FakeAnnotationService();
        annotationService.CreatedAnnotations.Add(new AnnotationV2
        {
            Id = "annotation-rotated-zoomed",
            BookId = "book-001",
            LayerId = null,
            Kind = AnnotationKind.Highlight,
            Regions = [new AnnotationRegion(0, 0.1, 0.2, 0.3, 0.1)],
            HighlightColor = "#FFCC66",
            QuoteText = "Rotated zoomed highlight",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });

        var viewModel = new ReaderViewModel(
            sessionService,
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        AnnotationOverlayItem overlay = Assert.Single(viewModel.AnnotationOverlays);
        Assert.Equal(90, viewModel.PageRotationDegrees);
        Assert.Equal(1.5, viewModel.OverlayZoomFactor, precision: 3);
        Assert.Equal(1440, viewModel.PageSurfaceWidth, precision: 3);
        Assert.Equal(1080, viewModel.PageSurfaceHeight, precision: 3);
        Assert.Equal(1008, overlay.X, precision: 3);
        Assert.Equal(108, overlay.Y, precision: 3);
        Assert.Equal(144, overlay.Width, precision: 3);
        Assert.Equal(324, overlay.Height, precision: 3);
    }

    [AvaloniaFact]
    public void ReaderViewModel_AnnotationOverlayAccessibleLabel_IncludesPageAndLayer()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        annotationService.CreatedAnnotations.Add(new AnnotationV2
        {
            Id = "annotation-accessible-label",
            BookId = "book-001",
            LayerId = null,
            Kind = AnnotationKind.Highlight,
            Regions = [new AnnotationRegion(0, 0.1, 0.2, 0.3, 0.1)],
            HighlightColor = "#FFCC66",
            QuoteText = "Accessible highlight",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        AnnotationOverlayItem overlay = Assert.Single(viewModel.AnnotationOverlays);
        Assert.Contains("Highlight", overlay.AccessibleLabel, StringComparison.Ordinal);
        Assert.Contains("Key arguments", overlay.AccessibleLabel, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 10", overlay.AccessibleLabel, StringComparison.Ordinal);

        Window window = ShowReaderWindow(viewModel);
        try
        {
            Assert.Contains(
                window.GetVisualDescendants().OfType<Border>(),
                border => GetAutomationName(border) == overlay.AccessibleLabel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderViewModel_AnnotationOverlayColors_MeetContrastGate()
    {
        string[] colors = ["#FFCC66", "#88AA77", "#C7795A", "#8E5A8A"];

        foreach (string color in colors)
        {
            var overlay = new AnnotationOverlayItem(
                "annotation-contrast",
                0,
                0,
                120,
                24,
                color,
                "Highlight - Key arguments - Page 1 of 10",
                "Note anchor - Key arguments - Page 1 of 10",
                IsNote: false);

            string composited = CompositeOverWhite(overlay.OverlayColor, alpha: 0.8);
            double contrast = ContrastRatio(composited, "#FFFFFF");

            Assert.True(
                contrast >= 3.0,
                $"{color} overlay display color {overlay.OverlayColor} composites to {composited} at {contrast:F2}:1.");
        }
    }

    [AvaloniaFact]
    public void ReaderViewModel_BookmarkPanelAccessibleLabel_IncludesCount()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal("Bookmarks (1)", viewModel.BookmarkPanelAccessibleLabel);
        BookmarkListItem bookmark = Assert.Single(viewModel.Bookmarks);
        Assert.Equal("Important page, page 3", bookmark.AccessibleLabel);
    }

    [AvaloniaFact]
    public void ReaderViewModel_PageTurnP95_With100AnnotationsPerPage_Under100ms()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        for (int page = 0; page < 10; page++)
        {
            for (int index = 0; index < 100; index++)
            {
                annotationService.CreatedAnnotations.Add(new AnnotationV2
                {
                    Id = $"annotation-page-{page}-{index}",
                    BookId = "book-001",
                    LayerId = null,
                    Kind = AnnotationKind.Highlight,
                    Regions =
                    [
                        new AnnotationRegion(
                            page,
                            0.04 + (index % 10) * 0.08,
                            0.05 + (index / 10) * 0.08,
                            0.05,
                            0.025),
                    ],
                    HighlightColor = "#FFCC66",
                    QuoteText = $"Annotation {index}",
                    CreatedUtc = DateTimeOffset.UtcNow,
                    ModifiedUtc = DateTimeOffset.UtcNow,
                });
            }
        }

        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        var durations = new List<TimeSpan>(capacity: 20);

        for (int iteration = 0; iteration < 20; iteration++)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            if (iteration % 2 == 0)
            {
                viewModel.GoNextAsync().GetAwaiter().GetResult();
            }
            else
            {
                viewModel.GoPreviousAsync().GetAwaiter().GetResult();
            }

            durations.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started));
            Assert.Equal(100, viewModel.AnnotationOverlays.Count);
        }

        TimeSpan p95 = Percentile95(durations);

        Assert.True(
            p95 <= TimeSpan.FromMilliseconds(100),
            $"Reader page turn with 100 annotation overlays should stay under 100 ms P95; actual {p95.TotalMilliseconds:F3} ms.");
    }

    [AvaloniaFact]
    public void ReaderView_Phase09ControlsExposeActionSpecificAutomationNames()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);
        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.SelectedCitationText = "Action-specific citation passage";
        viewModel.CaptureCitationAsync(CancellationToken.None).GetAwaiter().GetResult();
        Window window = ShowReaderWindow(viewModel);

        try
        {
            var tabControl = Assert.Single(window.GetVisualDescendants().OfType<TabControl>());

            tabControl.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(
                window.GetVisualDescendants().OfType<ListBox>(),
                list => GetAutomationName(list) == "Bookmarks (1)");

            tabControl.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(
                window.GetVisualDescendants().OfType<CheckBox>(),
                box => GetAutomationName(box) == "Layer visible");
            Assert.Contains(
                window.GetVisualDescendants().OfType<Button>(),
                button => GetAutomationName(button) == "Delete layer");
            viewModel
                .SetLayerVisibilityAsync(viewModel.Layers[0], isVisible: false, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(
                window.GetVisualDescendants().OfType<CheckBox>(),
                box => GetAutomationName(box) == "Layer hidden");

            tabControl.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(
                window.GetVisualDescendants().OfType<TextBox>(),
                textBox => GetAutomationName(textBox) == "Why I opened this");
            Assert.Contains(
                window.GetVisualDescendants().OfType<TextBox>(),
                textBox => GetAutomationName(textBox) == "Disposition");

            Assert.Contains(
                window.GetVisualDescendants().OfType<Button>(),
                button => GetAutomationName(button) == "Copy citation");
            Assert.Contains(
                window.GetVisualDescendants().OfType<Button>(),
                button => GetAutomationName(button) == "Export citation");
            Assert.NotEqual(viewModel.CopyCitationLabel, viewModel.ExportCitationLabel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderView_Phase09InteractiveControls_AcceptKeyboardFocusAndNames()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateNoteAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel.OpenNoteEditor(viewModel.Annotations[0]);
        viewModel.SelectedCitationText = "Accessible citation passage";
        viewModel.CaptureCitationAsync(CancellationToken.None).GetAwaiter().GetResult();

        Window window = ShowReaderWindow(viewModel);
        try
        {
            AssertFocusableControl<Button>(window, "Highlight");
            AssertFocusableControl<Button>(window, "Add note");
            AssertFocusableControl<Button>(window, "Add bookmark");
            AssertFocusableControl<Button>(window, "Capture citation");
            AssertFocusableControl<Button>(window, "Copy citation");
            AssertFocusableControl<Button>(window, "Export citation");
            AssertFocusableControl<Button>(window, "Close");
            AssertFocusableControl<Button>(window, "Edit note");
            AssertFocusableControl<Button>(window, "Delete annotation");
            AssertFocusableControl<TextBox>(window, "Note");

            var tabControl = Assert.Single(window.GetVisualDescendants().OfType<TabControl>());

            tabControl.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            AssertFocusableControl<ListBox>(window, "Bookmarks (1)");
            AssertFocusableControl<Button>(window, "Important page, page 3");
            AssertFocusableControl<TextBox>(window, "Rename bookmark");
            AssertFocusableControl<Button>(window, "Remove bookmark");

            tabControl.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            AssertFocusableControl<Button>(window, "New layer");
            AssertFocusableControl<ComboBox>(window, "Show annotations from");
            AssertFocusableControl<TextBox>(window, "Key arguments");
            AssertFocusableControl<CheckBox>(window, "Layer visible");
            AssertFocusableControl<Button>(window, "Merge layer");
            AssertFocusableControl<Button>(window, "Delete layer");

            tabControl.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            AssertFocusableControl<TextBox>(window, "Why I opened this");
            AssertFocusableControl<TextBox>(window, "Key insight");
            AssertFocusableControl<TextBox>(window, "Open questions");
            AssertFocusableControl<TextBox>(window, "Disposition");
            AssertFocusableControl<Button>(window, "Save memory");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderViewModel_OverlayUsesUnzoomedSurfaceForFitWidth()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var sessionService = new FakeReaderSessionService
        {
            ZoomMode = ZoomMode.FitWidth,
            ZoomPercent = 150,
        };
        var annotationService = new FakeAnnotationService();
        annotationService.CreatedAnnotations.Add(new AnnotationV2
        {
            Id = "annotation-fit-width",
            BookId = "book-001",
            LayerId = null,
            Kind = AnnotationKind.Highlight,
            Regions = [new AnnotationRegion(0, 0.1, 0.2, 0.3, 0.1)],
            HighlightColor = "#FFCC66",
            QuoteText = "Fit width highlight",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });

        var viewModel = new ReaderViewModel(
            sessionService,
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        AnnotationOverlayItem overlay = Assert.Single(viewModel.AnnotationOverlays);
        Assert.Equal(1.0, viewModel.OverlayZoomFactor, precision: 3);
        Assert.Equal(720, viewModel.PageSurfaceWidth, precision: 3);
        Assert.Equal(960, viewModel.PageSurfaceHeight, precision: 3);
        Assert.Equal(72, overlay.X, precision: 3);
        Assert.Equal(192, overlay.Y, precision: 3);
        Assert.Equal(216, overlay.Width, precision: 3);
        Assert.Equal(96, overlay.Height, precision: 3);
    }

    [AvaloniaFact]
    public void ReaderViewModel_DeleteAnnotation_RemovesAnnotationAndOverlay()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();

        viewModel
            .DeleteAnnotationAsync(viewModel.Annotations[0], CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Empty(annotationService.CreatedAnnotations);
        Assert.Empty(viewModel.Annotations);
        Assert.Empty(viewModel.AnnotationOverlays);
        Assert.Equal("Annotation deleted", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_RequestDeleteAnnotation_RequiresConfirmationBeforeDelete()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();

        viewModel.RequestDeleteAnnotation(viewModel.Annotations[0]);

        Assert.True(viewModel.HasPendingDeleteAnnotation);
        Assert.Single(annotationService.CreatedAnnotations);
        Assert.Equal(0, annotationService.DeleteCallCount);
        Assert.Equal("Delete this Highlight?", viewModel.DeleteAnnotationConfirmationText);

        viewModel.ConfirmDeleteAnnotationAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.False(viewModel.HasPendingDeleteAnnotation);
        Assert.Equal(1, annotationService.DeleteCallCount);
        Assert.Empty(annotationService.CreatedAnnotations);
        Assert.Empty(viewModel.Annotations);
        Assert.Empty(viewModel.AnnotationOverlays);
        Assert.Equal("Annotation deleted", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_CancelDeleteAnnotation_KeepsAnnotation()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();

        viewModel.RequestDeleteAnnotation(viewModel.Annotations[0]);
        viewModel.CancelDeleteAnnotation();

        Assert.False(viewModel.HasPendingDeleteAnnotation);
        Assert.Equal(0, annotationService.DeleteCallCount);
        Assert.Single(annotationService.CreatedAnnotations);
        Assert.Single(viewModel.Annotations);
    }

    [AvaloniaFact]
    public void ReaderViewModel_RefreshAnnotations_ClearsStalePendingDelete()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel.RequestDeleteAnnotation(viewModel.Annotations[0]);

        viewModel.GoNextAsync().GetAwaiter().GetResult();

        Assert.False(viewModel.HasPendingDeleteAnnotation);
    }

    [AvaloniaFact]
    public void ReaderViewModel_EditNote_SavesUpdatedTextAndClosesEditor()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateNoteAsync(CancellationToken.None).GetAwaiter().GetResult();

        AnnotationListItem note = Assert.Single(viewModel.Annotations);
        Assert.True(note.IsNote);
        viewModel.OpenNoteEditor(note);
        viewModel.EditingNoteText = "Updated note body";
        viewModel.SaveOpenNoteAsync(CancellationToken.None).GetAwaiter().GetResult();

        AnnotationV2 updated = Assert.Single(annotationService.UpdatedAnnotations);
        Assert.Equal("Updated note body", updated.NoteText);
        Assert.False(viewModel.IsNoteEditorOpen);
        Assert.Equal("Updated note body", viewModel.Annotations[0].Preview);
        Assert.Equal("Note saved", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_NoteOverlay_ExposesAnchorMarker()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateNoteAsync(CancellationToken.None).GetAwaiter().GetResult();

        AnnotationOverlayItem overlay = Assert.Single(viewModel.AnnotationOverlays);

        Assert.True(overlay.IsNote);
        Assert.Contains("Note", overlay.AccessibleLabel, StringComparison.Ordinal);
        Assert.Contains("Note anchor", overlay.NoteAnchorAccessibleLabel, StringComparison.Ordinal);
        Assert.Contains("Key arguments", overlay.NoteAnchorAccessibleLabel, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 10", overlay.NoteAnchorAccessibleLabel, StringComparison.Ordinal);
        Assert.True(overlay.NoteAnchorMargin.Left >= overlay.Margin.Left);
        Assert.True(overlay.NoteAnchorMargin.Top >= 0);

        Window window = ShowReaderWindow(viewModel);
        try
        {
            Assert.Contains(
                window.GetVisualDescendants().OfType<Border>(),
                border => GetAutomationName(border) == overlay.NoteAnchorAccessibleLabel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderView_NoteEditor_TextBindingUpdatesViewModel()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateNoteAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel.OpenNoteEditor(viewModel.Annotations[0]);

        var view = new ReaderView { DataContext = viewModel };
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TextBox editor = Assert.IsType<TextBox>(view.FindControl<TextBox>("NoteEditorTextBox"));
        editor.Text = "Typed through XAML";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Typed through XAML", viewModel.EditingNoteText);
    }

    [AvaloniaFact]
    public void ReaderView_NoteEditorEscape_ClosesEditorWithoutNavigating()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateNoteAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel.OpenNoteEditor(viewModel.Annotations[0]);

        Window window = ShowReaderWindow(viewModel);
        try
        {
            var view = Assert.IsType<ReaderView>(window.Content);
            TextBox editor = Assert.IsType<TextBox>(view.FindControl<TextBox>("NoteEditorTextBox"));
            editor.Focus();
            Dispatcher.UIThread.RunJobs();

            int pageBeforeEscape = viewModel.CurrentPageIndex;
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsNoteEditorOpen);
            Assert.Null(viewModel.EditingNoteText);
            Assert.Equal(pageBeforeEscape, viewModel.CurrentPageIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderView_PseudolocalePhase09Panels_RendersWithoutOversizedTextBounds()
    {
        var localization = new PseudoLocalizationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateNoteAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel.OpenedBecause = "[!! Research goal with expanded pseudolocale text !!]";
        viewModel.KeyInsight = "[!! Expanded insight text used to exercise reader memory layout !!]";
        viewModel.OpenQuestions = "[!! Which follow-up questions still remain after reading? !!]";
        Dispatcher.UIThread.RunJobs();

        var window = new Window
        {
            Title = "Ogma Library - Pseudolocale Reader",
            Width = 1100,
            Height = 760,
            Content = new ReaderView { DataContext = viewModel },
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string screenshotPath = Path.Combine(ArtifactsDir, "reader-qps-ploc.png");
        frame!.Save(screenshotPath);

        var textBlocks = window.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.NotEmpty(textBlocks);
        Assert.All(textBlocks, textBlock =>
        {
            Control? parent = textBlock.Parent as Control;
            if (parent is null ||
                textBlock.Bounds.Width <= 0 ||
                parent.Bounds.Width <= 0 ||
                parent is ScrollViewer)
            {
                return;
            }

            Assert.True(
                textBlock.Bounds.Width <= parent.Bounds.Width + 2,
                $"Text '{textBlock.Text}' exceeds parent width {parent.Bounds.Width:F1}px with {textBlock.Bounds.Width:F1}px.");
        });
    }

    [AvaloniaFact]
    public void ReaderViewModel_DeleteOpenNote_ClosesEditor()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateNoteAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel.OpenNoteEditor(viewModel.Annotations[0]);

        viewModel
            .DeleteAnnotationAsync(viewModel.Annotations[0], CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.False(viewModel.IsNoteEditorOpen);
        Assert.Null(viewModel.EditingNoteText);
    }

    [AvaloniaFact]
    public void ReaderViewModel_HidingLayer_FiltersAnnotationsAndOverlays()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();
        annotationService.CreatedAnnotations.Add(new AnnotationV2
        {
            Id = "annotation-orphan",
            BookId = "book-001",
            LayerId = null,
            Kind = AnnotationKind.Highlight,
            Regions = [new AnnotationRegion(0, 0.52, 0.26, 0.24, 0.05)],
            HighlightColor = "#88AA77",
            QuoteText = "Orphaned highlight",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });

        Assert.Single(viewModel.Layers);
        Assert.Single(viewModel.Annotations);
        Assert.Single(viewModel.AnnotationOverlays);

        viewModel
            .SetLayerVisibilityAsync(viewModel.Layers[0], isVisible: false, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.False(viewModel.Layers[0].IsVisible);
        Assert.Empty(viewModel.Annotations);
        Assert.Empty(viewModel.AnnotationOverlays);

        viewModel
            .SetLayerVisibilityAsync(viewModel.Layers[0], isVisible: true, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.True(viewModel.Layers[0].IsVisible);
        Assert.Equal(2, viewModel.Annotations.Count);
        Assert.Equal(2, viewModel.AnnotationOverlays.Count);
    }

    [AvaloniaFact]
    public void ReaderViewModel_LayerFilter_ShowsOnlySelectedLayerAnnotationsAndOverlays()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();
        LayerListItem firstLayer = Assert.Single(viewModel.Layers);
        viewModel.AddLayerAsync(CancellationToken.None).GetAwaiter().GetResult();

        LayerListItem secondLayer = viewModel.Layers.Single(layer => layer.Id == "layer-2");
        annotationService.CreatedAnnotations.Add(new AnnotationV2
        {
            Id = "annotation-layer-2",
            BookId = "book-001",
            LayerId = secondLayer.Id,
            Kind = AnnotationKind.Highlight,
            Regions = [new AnnotationRegion(0, 0.52, 0.26, 0.24, 0.05)],
            HighlightColor = secondLayer.Color,
            QuoteText = "Second layer highlight",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });
        annotationService.CreatedAnnotations.Add(new AnnotationV2
        {
            Id = "annotation-legacy-default",
            BookId = "book-001",
            LayerId = null,
            Kind = AnnotationKind.Highlight,
            Regions = [new AnnotationRegion(0, 0.18, 0.38, 0.24, 0.05)],
            HighlightColor = firstLayer.Color,
            QuoteText = "Legacy default-layer highlight",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        });

        LayerFilterOption secondLayerFilter = Assert.Single(
            viewModel.LayerFilterOptions,
            option => option.Id == secondLayer.Id);

        viewModel
            .SelectLayerFilterAsync(secondLayerFilter, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        AnnotationListItem filtered = Assert.Single(viewModel.Annotations);
        AnnotationOverlayItem overlay = Assert.Single(viewModel.AnnotationOverlays);
        Assert.Equal(secondLayer.Id, filtered.LayerId);
        Assert.Equal("annotation-layer-2", overlay.AnnotationId);

        LayerFilterOption firstLayerFilter = Assert.Single(
            viewModel.LayerFilterOptions,
            option => option.Id == firstLayer.Id);

        viewModel
            .SelectLayerFilterAsync(firstLayerFilter, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Equal(2, viewModel.Annotations.Count);
        Assert.Contains(viewModel.Annotations, annotation => annotation.LayerId == firstLayer.Id);
        Assert.Contains(viewModel.Annotations, annotation => annotation.LayerId is null);

        viewModel
            .SelectLayerFilterAsync(
                viewModel.LayerFilterOptions.Single(option => option.IsAllVisible),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Equal(3, viewModel.Annotations.Count);
        Assert.Equal(3, viewModel.AnnotationOverlays.Count);
    }

    [AvaloniaFact]
    public void ReaderViewModel_RenameLayer_UpdatesLayerAndStatus()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        viewModel
            .RenameLayerAsync(viewModel.Layers[0], "Questions", CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Equal("Questions", viewModel.Layers[0].Name);
        Assert.Equal("Layer renamed", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_DeleteLayer_RemovesLayerWhenMoreThanOneExists()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.AddLayerAsync(CancellationToken.None).GetAwaiter().GetResult();

        viewModel
            .DeleteLayerAsync(viewModel.Layers[1], CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Single(viewModel.Layers);
        Assert.Equal("Layer deleted", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_DeleteLayer_ReportsLastLayerConstraint()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        viewModel
            .DeleteLayerAsync(viewModel.Layers[0], CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Single(viewModel.Layers);
        Assert.Equal("At least one layer is required", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_MergeLayer_MergesIntoFirstAvailableLayer()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var annotationService = new FakeAnnotationService();
        var layerService = new FakeLayerService(annotationService);
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            annotationService,
            new FakeBookmarkService(),
            layerService,
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.AddLayerAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel.AddLayerAsync(CancellationToken.None).GetAwaiter().GetResult();
        viewModel
            .SetLayerVisibilityAsync(viewModel.Layers[0], isVisible: false, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        viewModel.CreateHighlightAsync(CancellationToken.None).GetAwaiter().GetResult();

        viewModel
            .MergeLayerIntoFirstAvailableAsync(
                viewModel.Layers.Single(layer => layer.Id == "layer-2"),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Equal(2, viewModel.Layers.Count);
        Assert.DoesNotContain(viewModel.Layers, layer => layer.Id == "layer-2");
        Assert.Equal("layer-3", annotationService.CreatedAnnotations[0].LayerId);
        Assert.Single(viewModel.Annotations);
        Assert.Single(viewModel.AnnotationOverlays);
        Assert.Equal("Layer merged", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_MergeLayer_ReportsLastLayerConstraintWhenNoTargetExists()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        viewModel
            .MergeLayerIntoFirstAvailableAsync(viewModel.Layers[0], CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Single(viewModel.Layers);
        Assert.Equal("At least one layer is required", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_CaptureCitation_BuildsCardAndPlainText()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.SelectedCitationText = "A useful passage";
        viewModel.CaptureCitationAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(viewModel.HasCitationCard);
        Assert.NotNull(viewModel.CitationCard);
        Assert.Equal("A useful passage", viewModel.CitationCard.SelectedText);
        Assert.Equal("Test Title", viewModel.CitationCard.Title);
        Assert.Equal("Test Author", viewModel.CitationCard.Author);
        Assert.Contains("A useful passage", viewModel.CitationPlainText, StringComparison.Ordinal);
        Assert.Contains("p.1", viewModel.CitationPlainText, StringComparison.Ordinal);

        viewModel.CloseCitationCard();

        Assert.False(viewModel.HasCitationCard);
        Assert.Empty(viewModel.CitationPlainText);
    }

    [AvaloniaFact]
    public void ReaderViewModel_CaptureCitation_RequiresSelectedText()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.CaptureCitationAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.False(viewModel.HasCitationCard);
        Assert.Equal("Select text before capturing a citation", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_ExportCitation_UsesCapturedDomainCard()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var citationService = new FakeCitationService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            citationService,
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.SelectedCitationText = "Export this passage";
        viewModel.CaptureCitationAsync(CancellationToken.None).GetAwaiter().GetResult();

        string? path = viewModel.ExportCitationAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(Path.Combine(Path.GetTempPath(), "citation.txt"), path);
        Assert.NotNull(citationService.ExportedCard);
        Assert.Equal("Export this passage", citationService.ExportedCard.SelectedText);
        Assert.Equal("Citation exported", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_ToggleBookmark_AddsAndRemovesCurrentPageBookmark()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        Assert.False(viewModel.IsCurrentPageBookmarked);
        Assert.Single(viewModel.Bookmarks);

        viewModel.ToggleBookmarkAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(viewModel.IsCurrentPageBookmarked);
        Assert.Equal(2, viewModel.Bookmarks.Count);
        Assert.Contains(viewModel.Bookmarks, bookmark => bookmark.PageIndex == 0);
        Assert.Equal("Bookmark saved", viewModel.StatusMessage);

        viewModel.ToggleBookmarkAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.False(viewModel.IsCurrentPageBookmarked);
        Assert.Single(viewModel.Bookmarks);
        Assert.DoesNotContain(viewModel.Bookmarks, bookmark => bookmark.PageIndex == 0);
        Assert.Equal("Bookmark removed", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_OpenBookmarkPanel_SelectsBookmarkTab()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenBookmarkPanel();

        Assert.Equal(1, viewModel.SelectedSidebarTabIndex);
    }

    [AvaloniaFact]
    public void ReaderView_CtrlB_TogglesCurrentPageBookmark()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);
        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        Window window = ShowReaderWindow(viewModel);
        try
        {
            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsCurrentPageBookmarked);
            Assert.Contains(viewModel.Bookmarks, bookmark => bookmark.PageIndex == 0);

            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsCurrentPageBookmarked);
            Assert.DoesNotContain(viewModel.Bookmarks, bookmark => bookmark.PageIndex == 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderView_CtrlShiftB_OpensBookmarkPanel()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);
        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.SelectedSidebarTabIndex = 0;

        Window window = ShowReaderWindow(viewModel);
        try
        {
            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.Control | RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, viewModel.SelectedSidebarTabIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderView_BookmarkPanelKeyboard_ArrowSelectsAndEnterNavigates()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);
        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        Window window = ShowReaderWindow(viewModel);
        try
        {
            var view = Assert.IsType<ReaderView>(window.Content);
            var tabControl = Assert.Single(window.GetVisualDescendants().OfType<TabControl>());
            tabControl.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            var bookmarkList = Assert.Single(
                window.GetVisualDescendants().OfType<ListBox>(),
                list => GetAutomationName(list) == "Bookmarks (1)");

            bookmarkList.Focus();
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, bookmarkList.SelectedIndex);
            Assert.Equal(0, viewModel.CurrentPageIndex);

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, viewModel.CurrentPageIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderView_BookmarkContextFlyout_RenameFocusesEditorAndDeleteRemovesBookmark()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);
        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        Window window = ShowReaderWindow(viewModel);
        try
        {
            var view = Assert.IsType<ReaderView>(window.Content);
            var tabControl = Assert.Single(window.GetVisualDescendants().OfType<TabControl>());
            tabControl.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            BookmarkListItem bookmark = Assert.Single(viewModel.Bookmarks);
            var bookmarkRow = window.GetVisualDescendants()
                .OfType<Grid>()
                .First(grid =>
                    ReferenceEquals(grid.DataContext, bookmark) &&
                    grid.ContextFlyout is MenuFlyout);

            var flyout = Assert.IsType<MenuFlyout>(bookmarkRow.ContextFlyout);
            List<MenuItem> menuItems = flyout.Items.OfType<MenuItem>().ToList();
            MenuItem renameItem = Assert.Single(menuItems, item => item.Header?.ToString() == "Rename bookmark");
            MenuItem deleteItem = Assert.Single(menuItems, item => item.Header?.ToString() == "Remove bookmark");

            flyout.ShowAt(bookmarkRow);
            Dispatcher.UIThread.RunJobs();

            InvokeReaderViewHandler(view, "RenameBookmarkMenuItem_Click", renameItem);

            TextBox editor = window.GetVisualDescendants()
                .OfType<TextBox>()
                .First(textBox => ReferenceEquals(textBox.DataContext, bookmark));
            Assert.True(editor.IsFocused);

            InvokeReaderViewHandler(view, "DeleteBookmarkMenuItem_Click", deleteItem);

            Assert.Empty(viewModel.Bookmarks);
            Assert.Equal("Bookmark removed", viewModel.StatusMessage);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderView_CtrlShiftC_CapturesSelectedCitation()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);
        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();
        viewModel.SelectedCitationText = "Shortcut citation passage";

        Window window = ShowReaderWindow(viewModel);
        try
        {
            window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control | RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.HasCitationCard);
            Assert.Equal("Shortcut citation passage", viewModel.CitationCard?.SelectedText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReaderViewModel_RenameBookmark_UpdatesLabelAndStatus()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        viewModel
            .RenameBookmarkAsync(viewModel.Bookmarks[0], "Chapter marker", CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Equal("Chapter marker", viewModel.Bookmarks[0].Label);
        Assert.Equal("Bookmark renamed", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void ReaderViewModel_DeleteBookmark_RemovesBookmarkAndUpdatesStatus()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            new FakeReadingMemoryService(),
            localization);

        viewModel.OpenAsync("book-001", null, CancellationToken.None).GetAwaiter().GetResult();

        viewModel
            .DeleteBookmarkAsync(viewModel.Bookmarks[0], CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Empty(viewModel.Bookmarks);
        Assert.Equal("Bookmark removed", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public async Task ReaderViewModel_AutoSaveReadingMemory_PersistsEditedFields()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var readingMemory = new FakeReadingMemoryService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            readingMemory,
            localization);

        await viewModel.OpenAsync("book-001", null, CancellationToken.None);
        viewModel.OpenedBecause = "Class prep";
        viewModel.KeyInsight = "Autosave should preserve focus-out edits.";
        viewModel.OpenQuestions = "Should this surface in book detail?";
        viewModel.DispositionText = "5";

        await viewModel.AutoSaveReadingMemoryAsync(TimeSpan.Zero, CancellationToken.None);

        ReadingMemory saved = Assert.Single(readingMemory.SavedMemories);
        Assert.Equal("book-001", saved.BookId);
        Assert.Equal("Class prep", saved.OpenedBecause);
        Assert.Equal("Autosave should preserve focus-out edits.", saved.KeyInsight);
        Assert.Equal("Should this surface in book detail?", saved.OpenQuestions);
        Assert.Equal(5, saved.Disposition);
        Assert.Equal("Reading memory saved", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public async Task ReaderViewModel_AutoSaveReadingMemory_ReportsInvalidDisposition()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var readingMemory = new FakeReadingMemoryService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            readingMemory,
            localization);

        await viewModel.OpenAsync("book-001", null, CancellationToken.None);
        viewModel.DispositionText = "x";

        await viewModel.AutoSaveReadingMemoryAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Empty(readingMemory.SavedMemories);
        Assert.Equal("Disposition must be a number from 1 to 5", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public async Task ReaderViewModel_AutoSaveReadingMemory_ReportsOutOfRangeDisposition()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var readingMemory = new FakeReadingMemoryService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            readingMemory,
            localization);

        await viewModel.OpenAsync("book-001", null, CancellationToken.None);
        viewModel.DispositionText = "6";

        await viewModel.AutoSaveReadingMemoryAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.Empty(readingMemory.SavedMemories);
        Assert.Equal("Disposition must be a number from 1 to 5", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public async Task ReaderViewModel_AutoSaveReadingMemory_CancelsEarlierPendingSave()
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var readingMemory = new FakeReadingMemoryService();
        var viewModel = new ReaderViewModel(
            new FakeReaderSessionService(),
            new FakeAnnotationService(),
            new FakeBookmarkService(),
            new FakeLayerService(),
            new FakeCitationService(),
            readingMemory,
            localization);

        await viewModel.OpenAsync("book-001", null, CancellationToken.None);
        viewModel.KeyInsight = "First draft";
        Task firstSave = viewModel.AutoSaveReadingMemoryAsync(
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        viewModel.KeyInsight = "Final draft";
        Task secondSave = viewModel.AutoSaveReadingMemoryAsync(TimeSpan.Zero, CancellationToken.None);

        await Task.WhenAll(firstSave, secondSave);

        ReadingMemory saved = Assert.Single(readingMemory.SavedMemories);
        Assert.Equal("Final draft", saved.KeyInsight);
    }

    private sealed class FakeReaderSessionService : IReaderSessionService
    {
        public ZoomMode ZoomMode { get; init; } = ZoomMode.FitWidth;

        public double ZoomPercent { get; init; } = 100;

        public int PageRotationDegrees { get; init; }

        public ReaderSession? CurrentSession { get; private set; }

        public IPdfRenderer? CurrentRenderer => null;

        public Task<ReaderSession> OpenAsync(string bookId, int? pageHint, CancellationToken ct)
        {
            CurrentSession = new ReaderSession(
                bookId,
                "C:/fixtures/book.pdf",
                PageCount: 10,
                CurrentPageIndex: pageHint ?? 0,
                ScrollOffset: 0,
                ZoomMode,
                ZoomPercent,
                DisplayMode.SinglePage,
                PageRotationDegrees);
            return Task.FromResult(CurrentSession);
        }

        public Task CloseAsync(CancellationToken ct)
        {
            CurrentSession = null;
            return Task.CompletedTask;
        }

        public Task NavigateToAsync(int pageIndex, double scrollOffset = 0.0)
        {
            CurrentSession = CurrentSession?.WithPage(pageIndex, scrollOffset);
            return Task.CompletedTask;
        }

        public void UpdateScrollOffset(double scrollOffset)
        {
        }
    }

    private static Window ShowReaderWindow(ReaderViewModel viewModel)
    {
        var view = new ReaderView { DataContext = viewModel };
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = view,
        };

        window.Show();
        view.Focus();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string? GetAutomationName(Control control) =>
        control.GetValue(AutomationProperties.NameProperty);

    private static T AssertFocusableControl<T>(Window window, string automationName)
        where T : Control
    {
        T control = window.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control => GetAutomationName(control) == automationName)
            ?? throw new InvalidOperationException(
                $"Could not find {typeof(T).Name} with automation name '{automationName}'.");

        control.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(control.IsFocused, $"{typeof(T).Name} '{automationName}' should accept keyboard focus.");
        return control;
    }

    private static void InvokeReaderViewHandler(ReaderView view, string methodName, object sender)
    {
        MethodInfo method = typeof(ReaderView)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find ReaderView handler '{methodName}'.");

        method.Invoke(view, [sender, new RoutedEventArgs()]);
        Dispatcher.UIThread.RunJobs();
    }

    private static TimeSpan Percentile95(IReadOnlyList<TimeSpan> durations)
    {
        TimeSpan[] sorted = durations.OrderBy(static duration => duration).ToArray();
        int index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static string CompositeOverWhite(string foregroundHex, double alpha)
    {
        (int r, int g, int b) = ParseRgb(foregroundHex);
        int compositeR = (int)Math.Round((r * alpha) + (255 * (1 - alpha)));
        int compositeG = (int)Math.Round((g * alpha) + (255 * (1 - alpha)));
        int compositeB = (int)Math.Round((b * alpha) + (255 * (1 - alpha)));
        return FormattableString.Invariant($"#{compositeR:X2}{compositeG:X2}{compositeB:X2}");
    }

    private static double ContrastRatio(string firstHex, string secondHex)
    {
        double first = RelativeLuminance(firstHex);
        double second = RelativeLuminance(secondHex);
        double lighter = Math.Max(first, second);
        double darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        (int r, int g, int b) = ParseRgb(hex);
        return (0.2126 * Linearize(r)) + (0.7152 * Linearize(g)) + (0.0722 * Linearize(b));
    }

    private static double Linearize(int channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static (int R, int G, int B) ParseRgb(string hex)
    {
        string value = hex.TrimStart('#');
        return (
            int.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(value.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(value.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private sealed class PseudoLocalizationService : ILocalizationService
    {
        private readonly InMemoryLocalizationService _inner = new();

        public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en");

        public event EventHandler? CultureChanged;

        public string this[string key]
        {
            get
            {
                string value = _inner[key];
                if (value.StartsWith('\u27E6'))
                {
                    return value;
                }

                return $"[{Expand(value)}]";
            }
        }

        public void SetCulture(string cultureName)
        {
            CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            _inner.SetCulture("en");
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }

        private static string Expand(string value) =>
            string.Concat(value.Select(static c => c switch
            {
                'a' => "aa",
                'e' => "ee",
                'i' => "ii",
                'o' => "oo",
                'u' => "uu",
                'A' => "AA",
                'E' => "EE",
                'I' => "II",
                'O' => "OO",
                'U' => "UU",
                _ => c.ToString(),
            }));
    }

    private sealed class FakeBookmarkService : IBookmarkService
    {
        private readonly List<Bookmark> _bookmarks =
        [
            new()
            {
                Id = 1,
                BookId = "book-001",
                PageIndex = 2,
                Label = "Important page",
                CreatedUtc = DateTimeOffset.UtcNow,
            },
        ];

        public Task<Bookmark> CreateAsync(
            string bookId,
            int pageIndex,
            string? label,
            CancellationToken cancellationToken)
        {
            var bookmark = new Bookmark
            {
                Id = _bookmarks.Count + 1,
                BookId = bookId,
                PageIndex = pageIndex,
                Label = label ?? $"Page {pageIndex + 1}",
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            _bookmarks.Add(bookmark);
            return Task.FromResult(bookmark);
        }

        public Task RenameAsync(long bookmarkId, string newLabel, CancellationToken cancellationToken)
        {
            Bookmark? bookmark = _bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
            if (bookmark is not null)
            {
                bookmark.Label = newLabel;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(long bookmarkId, CancellationToken cancellationToken)
        {
            Bookmark? bookmark = _bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
            if (bookmark is not null)
            {
                _bookmarks.Remove(bookmark);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Bookmark>> GetForBookAsync(
            string bookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Bookmark>>(
                _bookmarks.Where(b => b.BookId == bookId).ToList());
    }

    private sealed class FakeAnnotationService : IAnnotationService
    {
        public List<AnnotationV2> CreatedAnnotations { get; } = [];
        public List<AnnotationV2> UpdatedAnnotations { get; } = [];
        public int DeleteCallCount { get; private set; }

        public Task<AnnotationV2> CreateHighlightAsync(
            string bookId,
            string? layerId,
            IReadOnlyList<AnnotationRegion> regions,
            string color,
            string? quoteText,
            CancellationToken cancellationToken)
        {
            var annotation = new AnnotationV2
            {
                Id = $"annotation-{CreatedAnnotations.Count + 1}",
                BookId = bookId,
                LayerId = layerId,
                Kind = AnnotationKind.Highlight,
                Regions = regions,
                HighlightColor = color,
                QuoteText = quoteText,
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            };
            CreatedAnnotations.Add(annotation);
            return Task.FromResult(annotation);
        }

        public Task<AnnotationV2> CreateNoteAsync(
            string bookId,
            string? layerId,
            AnnotationRegion region,
            string noteText,
            CancellationToken cancellationToken)
        {
            var annotation = new AnnotationV2
            {
                Id = $"annotation-{CreatedAnnotations.Count + 1}",
                BookId = bookId,
                LayerId = layerId,
                Kind = AnnotationKind.Note,
                Regions = [region],
                NoteText = noteText,
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            };
            CreatedAnnotations.Add(annotation);
            return Task.FromResult(annotation);
        }

        public Task UpdateAsync(AnnotationV2 annotation, CancellationToken cancellationToken)
        {
            AnnotationV2? existing = CreatedAnnotations.FirstOrDefault(a => a.Id == annotation.Id);
            if (existing is not null)
            {
                existing.NoteText = annotation.NoteText;
                existing.HighlightColor = annotation.HighlightColor;
                existing.ModifiedUtc = annotation.ModifiedUtc;
            }

            UpdatedAnnotations.Add(annotation);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string annotationId, CancellationToken cancellationToken)
        {
            DeleteCallCount++;
            AnnotationV2? annotation = CreatedAnnotations.FirstOrDefault(a => a.Id == annotationId);
            if (annotation is not null)
            {
                CreatedAnnotations.Remove(annotation);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AnnotationV2>> GetForPageAsync(
            string bookId,
            int pageIndex,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnnotationV2>>(
                CreatedAnnotations
                    .Where(a => a.BookId == bookId && a.Regions.Any(r => r.PageIndex == pageIndex))
                    .ToList());
    }

    private sealed class FakeLayerService : IAnnotationLayerService
    {
        private readonly List<AnnotationLayer> _layers = [];
        private readonly FakeAnnotationService? _annotations;

        public FakeLayerService(FakeAnnotationService? annotations = null)
        {
            _annotations = annotations;
        }

        public Task<IReadOnlyList<AnnotationLayer>> GetLayersAsync(
            string bookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnnotationLayer>>(
                _layers.Where(l => l.BookId == bookId).ToList());

        public Task<AnnotationLayer> CreateLayerAsync(
            string bookId,
            string name,
            string color,
            CancellationToken cancellationToken)
        {
            var layer = new AnnotationLayer
            {
                Id = $"layer-{_layers.Count + 1}",
                BookId = bookId,
                Name = name,
                Color = color,
                IsVisible = true,
                SortOrder = _layers.Count,
            };
            _layers.Add(layer);
            return Task.FromResult(layer);
        }

        public Task RenameLayerAsync(string layerId, string newName, CancellationToken cancellationToken)
        {
            AnnotationLayer? layer = _layers.FirstOrDefault(l => l.Id == layerId);
            if (layer is not null)
            {
                layer.Name = newName;
            }

            return Task.CompletedTask;
        }

        public Task SetVisibilityAsync(string layerId, bool isVisible, CancellationToken cancellationToken)
        {
            AnnotationLayer? layer = _layers.FirstOrDefault(l => l.Id == layerId);
            if (layer is not null)
            {
                layer.IsVisible = isVisible;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string bookId, string layerId, CancellationToken cancellationToken)
        {
            List<AnnotationLayer> bookLayers = _layers.Where(l => l.BookId == bookId).ToList();
            if (bookLayers.Count <= 1)
            {
                throw new InvalidOperationException("Cannot delete the last remaining annotation layer.");
            }

            AnnotationLayer? layer = _layers.FirstOrDefault(l => l.Id == layerId);
            if (layer is not null)
            {
                _layers.Remove(layer);
            }

            return Task.CompletedTask;
        }

        public Task MergeLayersAsync(
            string bookId,
            string sourceLayerId,
            string targetLayerId,
            CancellationToken cancellationToken)
        {
            if (sourceLayerId == targetLayerId)
            {
                throw new InvalidOperationException("Cannot merge an annotation layer into itself.");
            }

            List<AnnotationLayer> bookLayers = _layers.Where(l => l.BookId == bookId).ToList();
            if (bookLayers.Count <= 1)
            {
                throw new InvalidOperationException("Cannot delete the last remaining annotation layer.");
            }

            AnnotationLayer? source = bookLayers.FirstOrDefault(l => l.Id == sourceLayerId);
            AnnotationLayer? target = bookLayers.FirstOrDefault(l => l.Id == targetLayerId);
            if (source is null || target is null)
            {
                throw new InvalidOperationException("Both annotation layers must belong to the requested book.");
            }

            foreach (AnnotationV2 annotation in _annotations?.CreatedAnnotations ?? [])
            {
                if (annotation.BookId == bookId && annotation.LayerId == sourceLayerId)
                {
                    annotation.LayerId = targetLayerId;
                }
            }

            _layers.Remove(source);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCitationService : ICitationService
    {
        public CitationCard? ExportedCard { get; private set; }

        public Task<CitationCard> CaptureAsync(
            string bookId,
            int pageIndex,
            string selectedText,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CitationCard(
                bookId,
                "Test Title",
                "Test Author",
                pageIndex + 1,
                selectedText));

        public Task<string> ExportAsync(CitationCard card, CancellationToken cancellationToken)
        {
            ExportedCard = card;
            return Task.FromResult(Path.Combine(Path.GetTempPath(), "citation.txt"));
        }
    }

    private sealed class FakeTextLayerService : ITextLayerService
    {
        private readonly TextLayer _layer;

        public FakeTextLayerService(TextLayer layer)
        {
            _layer = layer;
        }

        public Task<TextLayer> ExtractAsync(string bookId, int pageIndex, CancellationToken ct) =>
            Task.FromResult(_layer);

        public Task<ExtractionQuality> GetQualityAsync(string bookId, int pageIndex, CancellationToken ct) =>
            Task.FromResult(_layer.Quality);
    }

    private sealed class FakeReadingMemoryService : IReadingMemoryService
    {
        public List<ReadingMemory> SavedMemories { get; } = [];

        public Task<ReadingMemory> LoadAsync(string bookId, CancellationToken cancellationToken) =>
            Task.FromResult(new ReadingMemory
            {
                BookId = bookId,
                OpenedBecause = "Research",
                KeyInsight = "Reader memory is durable.",
                OpenQuestions = "How should export work?",
                Disposition = 4,
            });

        public Task SaveAsync(ReadingMemory memory, CancellationToken cancellationToken)
        {
            if (memory.Disposition is < 1 or > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "Disposition must be between 1 and 5.");
            }

            SavedMemories.Add(new ReadingMemory
            {
                BookId = memory.BookId,
                OpenedBecause = memory.OpenedBecause,
                KeyInsight = memory.KeyInsight,
                OpenQuestions = memory.OpenQuestions,
                Disposition = memory.Disposition,
                CreatedAtUtc = memory.CreatedAtUtc,
                UpdatedAtUtc = memory.UpdatedAtUtc,
            });

            return Task.CompletedTask;
        }
    }
}
