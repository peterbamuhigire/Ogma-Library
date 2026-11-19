using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.Views.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 11 slice 2: the inspector has one header primary action, an overflow menu and
/// four segmented sections instead of seven tabs and a footer button row.
/// </summary>
public sealed class Phase11InspectorStructureTests
{
    private static readonly string[] SectionIds =
    [
        "Detail.Section.Overview", "Detail.Section.Details", "Detail.Section.Contents", "Detail.Section.Activity",
    ];

    [AvaloniaFact]
    public async Task Header_HasExactlyOnePrimaryButton_AndNoTabControl()
    {
        (Window window, BookDetailView view, _) = await ShowAsync(Projection(), 360);

        Assert.Empty(view.GetVisualDescendants().OfType<TabControl>());
        Button primary = Assert.Single(view.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("primary"));
        Assert.Equal("Detail.Action.Read", AutomationProperties.GetAutomationId(primary));
        Assert.Equal("Read", AutomationProperties.GetName(primary));
        Button more = FindById(view, "Detail.Action.More");
        Assert.IsType<MenuFlyout>(more.Flyout);
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(more)));
        window.Close();
    }

    [AvaloniaFact]
    public async Task PrimaryButton_FollowsTheStateDrivenAction()
    {
        BookDetailProjection resumed = Projection() with
        {
            ReadingProgress = new ReadingProgressProjection("P11-BOOK", 42, 0.2, null, (int)ReadingStatus.Reading),
        };
        (Window window, BookDetailView view, _) = await ShowAsync(resumed, 360);

        Button primary = FindById(view, "Detail.Action.Read");
        Assert.Equal("Continue reading · p. 43", AutomationProperties.GetName(primary));
        Assert.True(primary.IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public async Task OverflowMenu_OffersSecondaryActionsWithAccessibleNames()
    {
        (Window window, BookDetailView view, _) = await ShowAsync(Projection(), 360);

        Button more = FindById(view, "Detail.Action.More");
        var menu = (MenuFlyout)more.Flyout!;
        menu.ShowAt(more);
        Dispatcher.UIThread.RunJobs();
        MenuItem[] items = menu.Items.OfType<MenuItem>().ToArray();
        string?[] ids = items.Select(AutomationProperties.GetAutomationId).ToArray();
        Assert.Contains("Detail.Action.Enrich", ids);
        Assert.Contains("Detail.Action.RunOcr", ids);
        Assert.Contains("Detail.Action.AddToShelf", ids);
        Assert.Contains("Detail.Action.WriteMetadata", ids);
        Assert.Contains("Detail.Action.ShowInFolder", ids);
        Assert.Contains("Detail.Action.ForgetPassword", ids);
        Assert.DoesNotContain("Detail.Action.Remove", ids);
        Assert.All(items, item => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(item))));
        window.Close();
    }

    [AvaloniaFact]
    public async Task FourSegments_HaveAccessibleNames_AndSwitchContent()
    {
        (Window window, BookDetailView view, BookDetailViewModel vm) = await ShowAsync(Projection(), 360);

        Button[] segments = SectionIds.Select(id => FindById(view, id)).ToArray();
        Assert.All(segments, s => Assert.Contains("segment", s.Classes));
        Assert.Equal(["Overview", "Details", "Contents", "Activity"], segments.Select(AutomationProperties.GetName));
        Assert.Equal(InspectorSection.Overview, vm.SelectedSection);
        Assert.Contains("selected", segments[0].Classes);
        Assert.True(FindById<Control>(view, "Detail.Section.Overview.Content").IsEffectivelyVisible);

        foreach ((string id, InspectorSection section) in SectionIds.Zip(Enum.GetValues<InspectorSection>()))
        {
            FindById(view, id).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(section, vm.SelectedSection);
            Assert.Contains("selected", FindById(view, id).Classes);
            foreach (string other in SectionIds)
            {
                Assert.Equal(other == id, FindById<Control>(view, other + ".Content").IsEffectivelyVisible);
            }
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task IsbnLivesUnderDetails()
    {
        (Window window, BookDetailView view, BookDetailViewModel vm) = await ShowAsync(Projection(), 360);
        vm.SelectedSection = InspectorSection.Details;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t =>
            t.IsEffectivelyVisible && t.Text == "ISBN: 9780306406157");
        window.Close();
    }

    [AvaloniaFact]
    public async Task HashIsHidden_UntilTechnicalDetailsIsExpanded()
    {
        (Window window, BookDetailView view, BookDetailViewModel vm) = await ShowAsync(Projection(), 360);
        vm.SelectedSection = InspectorSection.Details;
        Dispatcher.UIThread.RunJobs();

        Expander technical = Assert.IsType<Expander>(FindById<Control>(view, "Detail.Technical"));
        Assert.False(technical.IsExpanded);
        Assert.DoesNotContain(VisibleTexts(view), t => t.Text == Hash);

        technical.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        TextBlock hash = Assert.Single(VisibleTexts(view), t => t.Text == Hash);
        Assert.Equal(Avalonia.Application.Current!.FindResource("Type.FontFamily.Mono"), hash.FontFamily);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(360)]
    [InlineData(520)]
    public async Task SegmentRow_IsASingleRow(double width)
    {
        (Window window, BookDetailView view, _) = await ShowAsync(Projection(), width);

        Button first = FindById(view, SectionIds[0]);
        Border row = Assert.IsType<Border>(FindById<Control>(view, "Detail.Sections"));
        Assert.All(SectionIds.Select(id => FindById(view, id)), s =>
            Assert.Equal(first.Bounds.Y, s.Bounds.Y, 3));
        Assert.True(row.Bounds.Height <= first.Bounds.Height + row.Padding.Top + row.Padding.Bottom + 2.01,
            $"Segment row {row.Bounds.Height} px tall; one segment is {first.Bounds.Height} px.");
        window.Close();
    }

    [AvaloniaFact]
    public async Task LongTitleAndManyAuthors_DoNotGrowTheHeaderBeyondItsBound()
    {
        (Window shortWindow, BookDetailView shortView, _) = await ShowAsync(Projection(), 360);
        double shortHeader = FindById<Control>(shortView, "Detail.Header").Bounds.Height;
        double oneTitleLine = FindById<TextBlock>(shortView, "Detail.Title").Bounds.Height;
        double oneAuthorLine = FindById<TextBlock>(shortView, "Detail.Authors").Bounds.Height;
        shortWindow.Close();

        BookDetailProjection huge = Projection() with
        {
            Title = string.Concat(Enumerable.Repeat("Exhaustive ", 19)) + "Treatise",
            Authors = Enumerable.Range(1, 12).Select(i => $"Author Number {i} Surname").ToArray(),
        };
        (Window window, BookDetailView view, _) = await ShowAsync(huge, 360);
        TextBlock title = FindById<TextBlock>(view, "Detail.Title");
        TextBlock authors = FindById<TextBlock>(view, "Detail.Authors");
        double header = FindById<Control>(view, "Detail.Header").Bounds.Height;

        Assert.True(huge.Title!.Length >= 200);
        Assert.True(title.Bounds.Height <= (2 * oneTitleLine) + 1, $"Title is {title.Bounds.Height} px.");
        Assert.True(authors.Bounds.Height <= (2 * oneAuthorLine) + 1, $"Authors are {authors.Bounds.Height} px.");
        Assert.True(header <= shortHeader + oneTitleLine + oneAuthorLine + 1, $"Header grew from {shortHeader} to {header} px.");
        Assert.Equal(huge.Title, ToolTip.GetTip(title));
        Assert.Equal(Avalonia.Application.Current!.FindResource("Type.FontFamily.Display"), title.FontFamily);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(360, "Light")]
    [InlineData(520, "Light")]
    [InlineData(360, "Dark")]
    [InlineData(520, "Dark")]
    public async Task CapturesInspectorFrames(double width, string theme)
    {
        Avalonia.Application app = Avalonia.Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        try
        {
            (Window window, _, _) = await ShowAsync(Projection(), width);
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            string dir = Path.Combine(AppContext.BaseDirectory, "artifacts", "phase-11");
            Directory.CreateDirectory(dir);
            frame!.Save(Path.Combine(dir, $"inspector-{width:0}-{theme.ToLowerInvariant()}.png"));
            window.Close();
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }
    }

    private const string Hash = "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12";

    private static IEnumerable<TextBlock> VisibleTexts(Visual view) =>
        view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible);

    private static Button FindById(Visual view, string id) => FindById<Button>(view, id);

    private static T FindById<T>(Visual view, string id)
        where T : Control =>
        Assert.Single(view.GetVisualDescendants().OfType<T>(), c => AutomationProperties.GetAutomationId(c) == id);

    private static async Task<(Window Window, BookDetailView View, BookDetailViewModel Vm)> ShowAsync(
        BookDetailProjection projection, double width)
    {
        var vm = new BookDetailViewModel(
            new StubReadModel(projection),
            new NoOpReaderNavigation(),
            new InMemoryLocalizationService(),
            fileLocator: new StubLocator());
        await vm.LoadBookAsync(projection.BookId);
        vm.IsVisible = true;
        var view = new BookDetailView { DataContext = vm, Width = width };
        var window = new Window { Width = width, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view, vm);
    }

    private static BookDetailProjection Projection() => new(
        "P11-BOOK",
        "Structure and Interpretation",
        ["Ada Example"],
        2021,
        "9780306406157",
        null,
        3,
        0,
        null,
        "books/structure.pdf",
        Hash,
        1_048_576,
        new ReadingProgressProjection("P11-BOOK", 0, 0, null, (int)ReadingStatus.Unread),
        2,
        [],
        IsPasswordProtected: false,
        IsAvailable: true);

    private sealed class StubReadModel(BookDetailProjection projection) : ICatalogueReadModel
    {
        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(projection);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(projection.ReadingProgress);
    }

    private sealed class StubLocator : OgmaLibrary.Application.Reader.IBookFileLocator
    {
        public Task<string?> LocateAsync(string bookId, CancellationToken ct) => Task.FromResult<string?>(null);
    }

    private sealed class NoOpReaderNavigation : IReaderNavigationService
    {
        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
