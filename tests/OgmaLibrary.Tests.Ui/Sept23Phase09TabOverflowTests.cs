using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OgmaLibrary.App.Controls;
using OgmaLibrary.App.Themes;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;
using OgmaApp = OgmaLibrary.App.App;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 09 (criterion 6): the compact tab strip keeps one row at Body size and offers a
/// keyboard-reachable "More tabs" menu for the tabs that do not fit; button states resolve to
/// token brushes.
/// </summary>
public sealed class Sept23Phase09TabOverflowTests
{
    private static readonly string[] Headers =
        ["File", "Bibliographic", "Reading", "Enrichment", "Provenance", "History"];

    [AvaloniaFact]
    public void NarrowStrip_ShowsOverflowButtonListingHiddenTabs()
    {
        NewApp();
        (Window window, TabControl tabs) = Show(width: 260);

        TabOverflowButton more = FindOverflow(tabs);
        Assert.True(more.IsVisible);
        Assert.True(more.Focusable);
        Assert.True(more.IsTabStop);

        IReadOnlyList<int> hidden = more.HiddenTabIndices();
        Assert.NotEmpty(hidden);
        Assert.DoesNotContain(0, hidden);

        IReadOnlyList<MenuItem> items = more.BuildMenuItems();
        Assert.Equal(hidden.Select(i => Headers[i]), items.Select(i => (string?)i.Header));
        window.Close();
    }

    [AvaloniaFact]
    public void WideStrip_HidesOverflowButton()
    {
        NewApp();
        (Window window, TabControl tabs) = Show(width: 1400);

        TabOverflowButton more = FindOverflow(tabs);
        Assert.False(more.IsVisible);
        Assert.Empty(more.HiddenTabIndices());
        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingAMenuItem_SelectsThatTab()
    {
        NewApp();
        (Window window, TabControl tabs) = Show(width: 260);
        TabOverflowButton more = FindOverflow(tabs);
        int target = more.HiddenTabIndices()[^1];

        MenuItem item = more.BuildMenuItems()[^1];
        item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(target, tabs.SelectedIndex);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("fr")]
    public void OverflowButton_HasLocalizedAccessibleName(string culture)
    {
        NewApp();
        var localization = new InMemoryLocalizationService();
        localization.SetCulture(culture);
        StyleStrings.Apply(Avalonia.Application.Current!.Resources, localization);

        (Window window, TabControl tabs) = Show(width: 260);
        string? name = AutomationProperties.GetName(FindOverflow(tabs));
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.False(name!.StartsWith('⟦'), $"Missing {culture} string: {name}");
        Assert.Equal(localization[StyleStrings.TabStripMoreKey], name);
        window.Close();
    }

    [AvaloniaFact]
    public void OverflowName_IsDifferentInFrench()
    {
        var en = new InMemoryLocalizationService();
        var fr = new InMemoryLocalizationService();
        fr.SetCulture("fr");
        Assert.NotEqual(en[StyleStrings.TabStripMoreKey], fr[StyleStrings.TabStripMoreKey]);
    }

    [AvaloniaFact]
    public void BusyAndDisabledButtons_ResolveToTokenBrushes()
    {
        OgmaApp app = NewApp();
        var busy = new Button { Content = "Working", Classes = { "primary", "busy" } };
        var busySecondary = new Button { Content = "Working", Classes = { "busy" } };
        var disabled = new Button { Content = "Disabled", IsEnabled = false };
        var window = new Window
        {
            Width = 500,
            Height = 120,
            Content = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Children = { busy, busySecondary, disabled },
            },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Token(app, "Color.Accent.Primary"), ((ISolidColorBrush)busy.Background!).Color);
        Assert.Equal(Token(app, "Color.Control.Background"), ((ISolidColorBrush)busySecondary.Background!).Color);

        ContentPresenter presenter = disabled.GetVisualDescendants().OfType<ContentPresenter>()
            .First(p => p.Name == "PART_ContentPresenter");
        Assert.Equal(Token(app, "Color.Control.BackgroundDisabled"), ((ISolidColorBrush)presenter.Background!).Color);
        Assert.Equal(Token(app, "Color.Text.Disabled"), ((ISolidColorBrush)presenter.Foreground!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void ComponentGallery_ShowsOverflowingStripAndBusyForEveryVariant()
    {
        NewApp();
        var gallery = new OgmaLibrary.App.Views.Dev.ComponentGallery
        {
            DataContext = new OgmaLibrary.App.Views.Dev.ComponentGalleryModel(new InMemoryLocalizationService()),
        };
        var window = new Window { Width = 1180, Height = 1500, Content = gallery };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TabControl narrow = gallery.GetVisualDescendants().OfType<TabControl>().Single(t => t.Name == "NarrowTabStrip");
        Assert.True(FindOverflow(narrow).IsVisible);
        Assert.NotEmpty(FindOverflow(narrow).BuildMenuItems());

        Button[] buttons = gallery.GetVisualDescendants().OfType<Button>().ToArray();
        foreach (string variant in new[] { "primary", "tool", "danger", "link" })
        {
            Assert.Contains(buttons, b => b.Classes.Contains(variant) && b.Classes.Contains("busy"));
            Assert.Contains(buttons, b => b.Classes.Contains(variant) && !b.IsEnabled);
            Assert.Contains(buttons, b => b.Classes.Contains(variant) && b.IsEnabled && !b.Classes.Contains("busy"));
        }

        Assert.Contains(buttons, b => b.Classes.Count == 1 && b.Classes.Contains("busy"));
        window.Close();
    }

    private static (Window Window, TabControl Tabs) Show(double width)
    {
        var tabs = new TabControl();
        foreach (string header in Headers)
        {
            tabs.Items.Add(new TabItem { Header = header, Content = new TextBlock { Text = header } });
        }

        var window = new Window { Width = width, Height = 200, RequestedThemeVariant = ThemeVariant.Light, Content = tabs };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, tabs);
    }

    private static TabOverflowButton FindOverflow(TabControl tabs) =>
        tabs.GetVisualDescendants().OfType<TabOverflowButton>().Single();

    private static OgmaApp NewApp()
    {
        var app = new OgmaApp();
        app.Initialize();
        return app;
    }

    private static Color Token(OgmaApp app, string key)
    {
        Assert.True(app.TryGetResource(key, ThemeVariant.Light, out object? value));
        return Assert.IsType<Color>(value);
    }
}
