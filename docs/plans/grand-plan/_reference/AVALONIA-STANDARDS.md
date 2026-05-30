# Avalonia UI Engineering Standards (Ogma Library)

Practical, prescriptive standards for building the Ogma Library desktop app on
**.NET 10 LTS + Avalonia 11+**, targeting **Windows and macOS** (Linux as a
free bonus). Distilled from *Avalonia UI Succinctly* (Alessandro Del Sole,
Syncfusion, 2025) and updated to modern Avalonia 11 / .NET 10 practice where the
book (written against Avalonia 11.0 / .NET 7) is now out of date.

> **How to read this doc.** Each section gives the rule ("do X, avoid Y"),
> a code example, and where relevant a note on how it applies to Ogma Library
> (a local-first PDF library with a ~2,000-book catalogue, a colourful PNG icon
> system, en/fr at MVP plus es/it/de later, and a 3D shelf rendered with
> Three.js inside a native WebView).

Source citations refer to chapters of the book; **[Modern]** flags guidance that
supersedes or extends the book for Avalonia 11.x.

---

## 0. Golden rules (the short list)

1. **Define UI in XAML (`.axaml`), logic in C#.** Build UI in C# only to generate elements at runtime. (Ch. 3)
2. **Use compiled bindings everywhere.** Set `x:DataType` on every view, keep `AvaloniaUseCompiledBindingsByDefault` on, avoid reflection bindings. **[Modern]**
3. **Use `CommunityToolkit.Mvvm` for MVVM**, not hand-rolled `INotifyPropertyChanged`. Reserve ReactiveUI for genuinely reactive flows. **[Modern]**
4. **Style with selectors (CSS-like), not WPF-style keyed styles.** (Ch. 6)
5. **Virtualize large lists.** Bind `ItemsSource` over `ObservableCollection` for the 2,000-book catalogue; never wrap a `ListBox`/`DataGrid` in a `ScrollViewer`. (Ch. 6)
6. **Bundle images as `AvaloniaResource`, reference with `avares://`.** (Ch. 5)
7. **Never block the UI thread.** Do file/PDF/DB work on background threads; touch UI objects only on the UI thread. **[Modern]**

---

## 1. Project structure & app lifecycle (Ch. 1-2)

### 1.1 Templates and SDK

The book installs templates via `dotnet new install Avalonia.Templates` and
targets .NET 7 / Avalonia 11.0. **[Modern]** For Ogma, target **.NET 10 LTS** and
the latest stable **Avalonia 11.x**. Create projects from the CLI so CI is
reproducible:

```bash
dotnet new install Avalonia.Templates
dotnet new avalonia.mvvm -o Ogma.App   # MVVM desktop app
```

Key project facts to know (Ch. 2):

- XAML files use the **`.axaml`** extension.
- `App.axaml` holds application-level resources/styles; `App.axaml.cs` holds
  startup code in `OnFrameworkInitializationCompleted`.
- `Program.cs` is the entry point and configures the `AppBuilder`.
- NuGet split: `Avalonia` (runtime), `Avalonia.Desktop` (desktop backends),
  `Avalonia.Diagnostics` (dev-time inspector). **[Modern]** Add
  `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, and
  `CommunityToolkit.Mvvm`.

### 1.2 Recommended solution layout for Ogma

```
Ogma.sln
 ├─ Ogma.Core/         # models, domain, services (no Avalonia ref)
 ├─ Ogma.App/          # shared Avalonia UI: Views/, ViewModels/, Assets/, Styles/
 ├─ Ogma.Desktop/      # Windows + macOS entry point (Avalonia.Desktop)
 └─ Ogma.Tests/        # unit + headless UI tests
```

Keep the cross-platform UI in one shared project; platform projects contain only
startup code, manifests, and icons (Ch. 2). Put **no** Avalonia dependency in
`Ogma.Core` so domain logic stays testable and portable.

### 1.3 Application lifetime (Ch. 2)

The runtime detects the platform and exposes a typed `ApplicationLifetime`.
Desktop is `IClassicDesktopStyleApplicationLifetime` (multi-window); mobile/web
is `ISingleViewApplicationLifetime` (single view). Wire the root view here:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        // [Modern] disable Avalonia's duplicate validation so CommunityToolkit
        // ObservableValidator is the single source of truth.
        DisableAvaloniaDataAnnotationValidation();
        desktop.MainWindow = new MainWindow { DataContext = new MainViewModel() };
    }
    else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
    {
        single.MainView = new MainView { DataContext = new MainViewModel() };
    }
    base.OnFrameworkInitializationCompleted();
}
```

**Do** create the root `DataContext` here. **Avoid** scattering view-model
construction across code-behind. For DI, build a service provider in
`Program.cs` and resolve view models from it (see §13).

---

## 2. XAML fundamentals (Ch. 3)

### 2.1 Structure

A view's root is usually a `Window` (desktop) or `UserControl` (page/component).
The `Content` property is implicit — children placed between the tags become
`Content`. A window/panel holds **one** root visual element, so wrap multiple
children in a panel.

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="Ogma.App.Views.MainWindow"
        Title="Ogma Library">
    <StackPanel Margin="10" Spacing="8">
        <TextBlock Text="Welcome to Ogma" />
        <Button Content="Open library" />
    </StackPanel>
</Window>
```

- `xmlns` → Avalonia controls; `xmlns:x` → XAML built-ins (`x:Class`, `x:Key`,
  `x:Name`, `x:DataType`).
- XAML is **case-sensitive** for type, property, and member names. Binding to
  `FullName` ≠ `Fullname`; a typo throws at runtime.
- **Type converters** turn strings into typed values automatically (e.g.
  `Margin="0,10,0,0"` → `Thickness`; `Background="Red"` → `SolidColorBrush`).

### 2.2 Events and code-behind

Avalonia uses **routed events** (Tunnel / Bubble / Direct, from
`RoutingStrategy`). A single handler can serve multiple controls; cast `sender`
to identify the source.

```xml
<Button Content="Click here" Click="OnButtonClick" />
```

```csharp
private void OnButtonClick(object? sender, RoutedEventArgs e) { /* ... */ }
```

**Prescription for Ogma:** prefer **commands over `Click` handlers**. Reserve
code-behind events for genuinely view-only concerns (focus, drag, animation
triggers). Note the book's caveat: the VS XAML editor will not auto-generate
handlers — declare the handler in C# first, then reference it in XAML.

---

## 3. Layout panels (Ch. 4)

Controls should **not** have fixed positions except in rare cases; arrange them
in panels so the UI adapts to window size. Available panels:

| Panel | Use it for |
| --- | --- |
| `StackPanel` | Stack children vertically/horizontally on one line. Use `Spacing`, not per-child `Margin`. |
| `WrapPanel` | Like StackPanel but wraps to next row/column when out of space. |
| `Grid` | Rows/columns; **the most versatile and performant** general layout. |
| `Canvas` | Absolute positioning (`Canvas.Left/Top`). Use as a root only. |
| `RelativePanel` | Position elements relative to siblings/panel. |
| `DockPanel` | Dock children to edges (menus, toolbars, status bars). `LastChildFill` fills the rest. |
| `ScrollViewer` | Scroll oversized content. Do **not** nest, and do **not** wrap `ListBox`/`DataGrid` in it. |

### 3.1 Grid sizing

`RowDefinition`/`ColumnDefinition` sizes use `GridUnitType`:

- **`Auto`** — size to content.
- **`*`** (Star) — proportion of remaining space (`2*` = twice `*`).
- **Absolute** — fixed pixels.

```xml
<Grid ColumnDefinitions="Auto,*,200" RowDefinitions="Auto,*">
    <TextBlock Text="Search:" />
    <TextBox Grid.Column="1" Watermark="Title or author…" />
    <Button Grid.Column="2" Content="Filter" />
    <ListBox Grid.Row="1" Grid.ColumnSpan="3" />
</Grid>
```

Use `Grid.RowSpan` / `Grid.ColumnSpan` to span cells. **[Modern]** the inline
`ColumnDefinitions="…"` shorthand (shown above) is cleaner than the verbose
`<Grid.ColumnDefinitions>` element form in the book.

**Alignment:** `HorizontalAlignment` / `VerticalAlignment` take
`Left/Right/Center/Stretch` and `Top/Bottom/Center/Stretch`. **Spacing:**
`Margin` (Thickness) for distance to neighbours; `Spacing` on `StackPanel`.

**Ogma layout shell:** a `DockPanel` (top menu + bottom status bar) wrapping a
`Grid` (left navigation rail / main content) is a solid main-window skeleton.

---

## 4. Controls catalogue (Ch. 5)

All controls derive from `Control`; templated controls from `TemplatedControl`.
Common properties: `Width`, `Height`, `Margin`, `Background`, `BorderBrush`,
`BorderThickness`, `CornerRadius`, `FontFamily/Size/Weight/Style`, `Foreground`,
`HorizontalAlignment`, `VerticalAlignment`.

**Buttons.** `Button` (single `Click`), `RepeatButton` (repeats while held),
`ToggleButton` (`IsChecked`), `RadioButton` (`GroupName`, mutually exclusive),
`ButtonSpinner` (increment/decrement), `SplitButton` (button + `MenuFlyout`).
`Content` is `object` — buttons can host any visual tree (a "content control").

**Text.** `TextBlock` (read-only; `TextWrapping`), `TextBox` (`Watermark`,
`PasswordChar`/`RevealPassword`, `AcceptsReturn`), `MaskedTextBox` (`Mask`),
`AutoCompleteBox` (`FilterMode`, e.g. `StartsWith`/`Contains`). For Ogma's
search box, `AutoCompleteBox` over the catalogue gives type-ahead.

**Dates/time.** `DatePicker`, `Calendar`, `CalendarDatePicker`, `TimePicker`.

**Selection.** `CheckBox` (`IsThreeState`), `Slider` (`Minimum`/`Maximum`/`Value`;
note: no value-changed event — observe `PropertyChanged`/bind instead),
`ComboBox` (`SelectedItem`/`SelectedIndex`, `ItemsSource`).

**Images.** `Image` with `avares://` source and `Stretch`
(`Uniform`/`UniformToFill`/`Fill`/`None`). See §9 for bundling.

**Menus & flyouts.** `Menu`/`MenuItem` (use `_` mnemonics, `InputGesture` for
shortcuts, `MenuItem.Icon`), `Flyout`/`MenuFlyout` (pop-overs), `Separator`.

**Containers.** `SplitView` (master–detail side pane; `DisplayMode`
Inline/Overlay/CompactInline/CompactOverlay, `OpenPaneLength`), `Expander`
(`Header`, `IsExpanded`), `TabControl`/`TabItem`, `ProgressBar` (`Minimum`,
`Maximum`, `Value`; `IsIndeterminate` for unknown duration **[Modern]**).

**For Ogma:** `SplitView` suits a collapsible nav pane; `Expander` groups filter
facets; `ProgressBar IsIndeterminate` covers PDF import/indexing.

---

## 5. MVVM (Ch. 6 & 8) — the core architectural pattern

MVVM cleanly separates **Model** (data), **ViewModel** (logic + bindable state),
and **View** (XAML). Views bind to view-model properties and commands; the
`DataContext` supplies the binding source.

### 5.1 INotifyPropertyChanged — the manual way (book) vs the modern way

The book implements `INotifyPropertyChanged` by hand with a
`[CallerMemberName]` helper (Ch. 6). **[Modern] Do not do this by hand.** Use
`CommunityToolkit.Mvvm` source generators:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class BookViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _author = "";

    // Generates an IsFavoriteChanged partial + raises HasFavorite too:
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isFavorite;

    public string StatusText => IsFavorite ? "★ Favorite" : "Unread";
}
```

`[ObservableProperty]` generates the full property + change notification;
`[NotifyPropertyChangedFor]` cascades notifications to computed properties. This
removes the entire boilerplate in book Code Listings 11 and 25.

### 5.2 Commands (Ch. 6)

The book binds `Button.Command` to a `void` method plus a `CanX()` method
(Code Listing 25-26). **[Modern]** use `[RelayCommand]`:

```csharp
public partial class LibraryViewModel : ObservableObject
{
    public ObservableCollection<BookViewModel> Books { get; } = new();

    [ObservableProperty] private BookViewModel? _selectedBook;

    [RelayCommand(CanExecute = nameof(CanOpenBook))]
    private async Task OpenBookAsync(BookViewModel book)
    {
        await _reader.OpenAsync(book.FilePath);   // async command, off-UI work
    }

    private bool CanOpenBook(BookViewModel book) => book is not null;
}
```

```xml
<Button Content="Open" Command="{Binding OpenBookCommand}"
        CommandParameter="{Binding SelectedBook}" />
```

`[RelayCommand]` generates an `IRelayCommand`/`IAsyncRelayCommand`. Call
`OpenBookCommand.NotifyCanExecuteChanged()` when guard conditions change. **Async
commands keep the UI responsive** — essential for PDF parsing and DB queries.

### 5.3 CommunityToolkit.Mvvm vs ReactiveUI — which, when

| Use **CommunityToolkit.Mvvm** | Use **ReactiveUI** |
| --- | --- |
| Default for Ogma. Simple, source-generated, low ceremony. | Complex reactive pipelines, derived/throttled streams. |
| `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, `WeakReferenceMessenger`. | `ReactiveObject`, `RaiseAndSetIfChanged`, `ReactiveCommand`, `WhenAnyValue` (Ch. 8). |
| Validation via `ObservableValidator`. | Reactive validation, `Interaction<,>`. |

**Prescription:** standardise on **CommunityToolkit.Mvvm**. The book's Ch. 8
ToDo sample uses ReactiveUI (`ViewModelBase : ReactiveObject`,
`ReactiveCommand.Create`, `WhenAnyValue` for `CanExecute`); adopt that pattern
**only** if a feature truly benefits from reactive streams (e.g. live-filtering
the 2,000-book list as the user types, with debounce).

### 5.4 ViewModel-first navigation & ViewLocator (Ch. 8)

Avalonia ships **no** navigation framework. The book's pattern: each page is a
`UserControl` with a backing view model; a `ViewLocator : IDataTemplate` maps a
`FooViewModel` to a `FooView` by name and the host control binds its `Content`
to the current view model.

```csharp
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        var name = data!.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);
        return type is null
            ? new TextBlock { Text = "Not Found: " + name }
            : (Control)Activator.CreateInstance(type)!;
    }
    public bool Match(object? data) => data is ViewModelBase;
}
```

```xml
<!-- App.axaml -->
<Application.DataTemplates>
    <local:ViewLocator />
</Application.DataTemplates>
```

The shell view model exposes a `CurrentPage` (or `Content`) view-model property;
swapping it navigates. **Honour the `*ViewModel` → `*View` naming convention** so
the locator resolves correctly. For Ogma, a `ShellViewModel.CurrentPage` driving
the `SplitView` content cleanly implements page navigation across desktop.

### 5.5 Windows & dialogs (Ch. 8)

Desktop multi-window: `new SecondaryWindow().Show()` (modeless) or
`.ShowDialog(owner)` (modal). Control placement with `WindowStartupLocation`
(`CenterOwner`/`CenterScreen`/`Manual`) and `WindowState`
(`Normal`/`Maximized`/`Minimized`/`FullScreen`). **[Modern]** keep windows out
of view models — use a small `IDialogService` so view models stay testable.

---

## 6. Data binding (Ch. 6) — including compiled bindings

### 6.1 Binding basics

```xml
<TextBox Text="{Binding FullName}" />
<DatePicker SelectedDate="{Binding DateOfBirth, Mode=TwoWay}" />
```

Modes: `TwoWay`, `OneWay`, `OneWayToSource`, `OneTime`, `Default` (Avalonia picks
per control — `TextBox.Text` is TwoWay, `TextBlock.Text` is OneWay). `Path=` is
optional. Auto-refresh requires the source to raise change notifications
(`INotifyPropertyChanged` / `ObservableObject`).

### 6.2 Compiled bindings — **mandatory for Ogma** **[Modern]**

The book mentions the "Use Compiled Bindings" checkbox in the new-project dialog
but does not teach the pattern. Make it the rule:

1. Keep `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`
   in every `.csproj`.
2. Declare `x:DataType` on every view (and on `DataTemplate`s).
3. Optionally use `{CompiledBinding}` explicitly where you mix sources.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Ogma.App.ViewModels"
             x:DataType="vm:LibraryViewModel"
             x:CompileBindings="True">
    <TextBox Text="{Binding SearchTerm}" />     <!-- compiled, type-checked -->
    <ListBox ItemsSource="{Binding Books}"
             SelectedItem="{Binding SelectedBook}">
        <ListBox.ItemTemplate>
            <DataTemplate x:DataType="vm:BookViewModel">
                <TextBlock Text="{Binding Title}" />
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</UserControl>
```

**Why:** compiled bindings are validated at build time (typos fail the build,
not runtime), are markedly faster (no reflection), and are essential for the
2,000-book list where reflection bindings would add measurable overhead.
**Avoid** reflection bindings except for rare dynamic/`object` scenarios.

### 6.3 Special binding sources

- **Element binding:** `{Binding #OtherControl.Property}` (or
  `ElementName=`/`Path=` long form).
- **Relative source:** `{Binding $parent[Window].DataContext.AddItem}` and
  `RelativeSource FindAncestor` (used by user controls binding to their own
  styled properties, Ch. 7).
- **`$self`, `$parent`** shorthands (Ch. 8).

### 6.4 Value converters (Ch. 6)

Implement `IValueConverter` (`Convert` / `ConvertBack`) for type mismatches a
type-converter can't bridge. Declare in resources with `x:Key`, consume via
`Converter={StaticResource …}`.

```csharp
public class BoolToFavGlyphConverter : IValueConverter
{
    public object? Convert(object? v, Type t, object? p, CultureInfo c)
        => (v is true) ? "" : "";   // filled vs outline star glyph
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}
```

**[Modern]** prefer **`FuncValueConverter<TIn,TOut>`** for one-off converters, and
the built-in `BoolConverters`, `StringConverters`, `ObjectConverters` for common
cases — they avoid extra classes.

---

## 7. Collections, DataTemplates & virtualization (Ch. 6) — Ogma's 2,000 books

### 7.1 Observable collections + DataTemplates

Bind `ItemsControl`/`ListBox`/`ComboBox`/`DataGrid` to
`ObservableCollection<T>` (raises add/remove notifications). A `DataTemplate`
tells the items control how to render each item.

```xml
<ListBox ItemsSource="{Binding Books}"
         SelectedItem="{Binding SelectedBook}"
         SelectionMode="Single">
    <ListBox.ItemTemplate>
        <DataTemplate x:DataType="vm:BookViewModel">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Image Width="36" Height="48"
                       Source="{Binding CoverImage}" Stretch="UniformToFill" />
                <StackPanel>
                    <TextBlock Text="{Binding Title}" FontWeight="SemiBold" />
                    <TextBlock Text="{Binding Author}" Foreground="Gray" />
                </StackPanel>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

> **[Modern] note:** the book uses the Avalonia 11.0-era `Items="{Binding}"`
> property. In current Avalonia the bindable list property is **`ItemsSource`**
> (`Items` is now a read-only inline collection). Always bind **`ItemsSource`**.

Promote reusable templates to resources (`<DataTemplate x:Key="…">`) and
reference via `ItemTemplate="{StaticResource …}"`. Never wrap an items control in
a `ScrollViewer` — they scroll themselves.

### 7.2 Virtualization — the rule for large catalogues

For 2,000 books (and growing), **virtualization is non-negotiable**.

- **`ListBox`** virtualizes by default via `VirtualizingStackPanel`. Keep it.
- **`ItemsControl`** does **not** virtualize by default — set an
  `ItemsPanel` of `VirtualizingStackPanel` if you use it for the catalogue.
- The book's **`ItemsRepeater`** (Ch. 6) virtualizes and supports custom layouts
  (e.g. `UniformGridLayout` for a cover-grid). It's a good fit for a shelf/grid
  view; for a simple flat list prefer `ListBox` (more built-in behaviour).

```xml
<ListBox ItemsSource="{Binding Books}">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />   <!-- realises only visible rows -->
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
</ListBox>
```

**Prescriptions:** (1) keep item templates lightweight — heavy templates negate
virtualization gains; (2) load cover thumbnails lazily/async, not eagerly for all
2,000 rows; (3) for a grid view use `ItemsRepeater` + `UniformGridLayout`;
(4) consider grouping/filtering in the view model rather than rendering all rows.

### 7.3 DataGrid (tabular detail view)

`DataGrid` ships in `Avalonia.Controls.DataGrid`. Add the theme include to
`App.axaml`:

```xml
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/>
```

Prefer explicit columns (`AutoGenerateColumns="False"`) with
`DataGridTextColumn`, `DataGridCheckBoxColumn`, `DataGridTemplateColumn`. Use it
for an admin/metadata table, not the main browsing experience.

---

## 8. Resources, styles, control themes & theming (Ch. 6)

### 8.1 Resources & scope

`Resources` dictionaries exist on every control, window, and `Application`.
Scope flows down the tree. Reference with `StaticResource` (one-time) or
`DynamicResource` (live-updating — use for theme colours).

```xml
<Window.Resources>
    <SolidColorBrush x:Key="AccentBrush" Color="#2D6CDF" />
</Window.Resources>
...
<TextBlock Foreground="{DynamicResource AccentBrush}" />
```

Organise into **Resource Dictionaries** (`.axaml`) and merge:

```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceInclude Source="avares://Ogma.App/Styles/Colors.axaml"/>
</ResourceDictionary.MergedDictionaries>
```

### 8.2 Styles use **selectors** (CSS-like), not WPF keyed styles

This is the biggest WPF difference (Ch. 6). Styles live in a `Styles` collection
and target controls via `Selector`. Use the `Classes` attribute like CSS classes
and pseudo-classes (`:pointerover`, `:pressed`, `:focus`, `:disabled`,
`:checked`).

```xml
<Window.Styles>
    <Style Selector="Button.primary">
        <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="CornerRadius" Value="6" />
    </Style>
    <Style Selector="Button.primary:pointerover">
        <Setter Property="Background" Value="#1E5BD0" />
    </Style>
</Window.Styles>
...
<Button Classes="primary" Content="Read now" />
```

Organise styles in `Styles (Avalonia)` files and reference via
`<StyleInclude Source="…"/>`. **No explicit reference per control needed** — the
selector applies automatically.

### 8.3 Control themes **[Modern]**

The book restyles controls through full `ControlTemplate` overrides inside styles
(Ch. 7). Avalonia 11 added **`ControlTheme`** — the modern, recommended way to
re-skin a control as a single keyed, reusable unit (replacing WPF's
`Style TargetType`). Prefer `ControlTheme` for systematic restyling:

```xml
<ControlTheme x:Key="PillButton" TargetType="Button">
    <Setter Property="CornerRadius" Value="16" />
    <Setter Property="Padding" Value="16,6" />
    <Setter Property="Template"> … <ControlTemplate> … </ControlTemplate> </Setter>
    <Style Selector="^:pointerover">
        <Setter Property="Opacity" Value="0.9" />
    </Style>
</ControlTheme>
```

Apply with `Theme="{StaticResource PillButton}"`. `^` in a nested selector means
"this control".

### 8.4 Theming (Fluent) & dark mode

`App.axaml` applies the theme:

```xml
<Application.Styles>
    <FluentTheme />            <!-- [Modern] Mode is gone; theme follows variant -->
</Application.Styles>
```

> **[Modern]:** the book's `<FluentTheme Mode="Light"/>` is outdated. In Avalonia
> 11 use **`RequestedThemeVariant`** for light/dark:
> `<Application … RequestedThemeVariant="Default">` (follows OS), or set
> `Application.Current.RequestedThemeVariant = ThemeVariant.Dark;` at runtime.
> Define colours with `ThemeDictionaries` keyed by `Light`/`Dark` variant so they
> swap automatically. For Ogma, follow the OS theme by default and offer an
> in-app override.

---

## 9. Assets & image bundling (Ch. 5, 9) — Ogma's colourful PNG icons

### 9.1 Bundling PNGs

Place PNGs under `Assets/` and set **Build Action = `AvaloniaResource`** (the
default `.csproj` glob already includes `Assets/**`). Reference with the
`avares://` URI scheme:

```
avares://<AssemblyName>/<Folder>/<file>.png
```

```xml
<Image Source="avares://Ogma.App/Assets/icons/shelf.png"
       Width="32" Height="32" Stretch="Uniform" />
```

`Stretch`: `Uniform` (fit, keep ratio — default choice), `UniformToFill` (fill,
keep ratio, may crop), `Fill` (distort), `None`. Supported formats: png, jpg,
gif, bmp, tif. For runtime-loaded images, construct a `Bitmap` and assign
`Image.Source`.

### 9.2 Prescriptions for the icon system

- Keep all UI icons under `Assets/icons/`; reference by a single helper or a
  converter that maps an icon enum/key → `avares://` URI so views stay clean.
- Ship icons at the resolutions you actually use (e.g. 32/48/64 px) to control
  bundle size; let `Stretch="Uniform"` handle minor scaling.
- For monochrome/scalable iconography prefer **vector** (`PathIcon` /
  `StreamGeometry` / embedded SVG via `Avalonia.Svg.Skia`) **[Modern]** — but
  Ogma's *colourful* icons are intentionally raster, so PNG + `AvaloniaResource`
  is the right call there.
- For window/app icons, set `Window.Icon` and the platform icon files in the
  desktop project.

### 9.3 Brushes for backgrounds (Ch. 9)

`SolidColorBrush`, `LinearGradientBrush` (note Avalonia uses **percentage**
`StartPoint`/`EndPoint`, e.g. `"0%,0%"`), `RadialGradientBrush`, `ImageBrush`
(texture fills with `Opacity`), `VisualBrush` (paint with another visual).
Define brushes as keyed resources to reuse colourful theming across shelves.

---

## 10. Localization / i18n (Ogma needs en/fr at MVP, +es/it/de later)

The book does not cover localization. **[Modern]** standard approach:

- Use **.NET resource files (`.resx`)**: `Resources.resx` (neutral/en),
  `Resources.fr.resx`, later `Resources.es/it/de.resx`. The generated strongly
  typed class exposes each key.
- Set culture at startup:
  `Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr");` and offer an
  in-app language switch persisted to settings.
- Bind UI text to a **`LocalizationService`/`ILocalizer`** exposed on view models
  (or an app-wide indexer view model), so changing language re-evaluates
  bindings without restart. A common pattern:

```xml
<TextBlock Text="{Binding [LibraryTitle], Source={x:Static loc:Tr.Instance}}" />
```

where `Tr` is an `INotifyPropertyChanged` indexer that looks up the current
culture's resource and raises a refresh when the language changes.

**Prescriptions:** (1) never hard-code user-facing strings in XAML — every label
goes through a resource key; (2) keep keys stable and descriptive
(`Library.EmptyState.Message`); (3) format dates/numbers with the current culture
(`CultureInfo`), relevant to the Ch. 5 date controls; (4) design layouts to flex
for longer translations (German/French run ~30% longer than English) — favour
`Auto`/`*` sizing and `TextWrapping="Wrap"`, avoid fixed widths.

---

## 11. Accessibility & automation peers **[Modern]**

The book does not cover accessibility. For a shippable Windows + macOS app:

- Set **`AutomationProperties.Name`** (and `HelpText`, `LabeledBy`) on
  interactive controls and icon-only buttons so screen readers (Narrator,
  VoiceOver) announce them.

```xml
<Button Classes="icon"
        AutomationProperties.Name="Open 3D shelf"
        Command="{Binding OpenShelfCommand}">
    <Image Source="avares://Ogma.App/Assets/icons/shelf.png" Width="24" Height="24"/>
</Button>
```

- Ensure keyboard operability: logical **tab order**, `IsTabStop`,
  `KeyboardNavigation.TabNavigation`, access keys (`_` mnemonics on menus),
  and visible `:focus` styles.
- Meet contrast guidance (WCAG AA) in both light and dark variants.
- Custom templated controls should expose an `AutomationPeer`
  (`OnCreateAutomationPeer`) when they introduce new interaction semantics.

---

## 12. WebView hosting (Ogma's 3D shelf: Three.js in WebView2 / WKWeb, **[Modern]**)

Avalonia has **no** first-party WebView. For the Three.js 3D shelf use a
community control that wraps the OS web engine:

- **`WebViewControl-Avalonia`** or **`Avalonia.WebView`** — render the OS engine
  (WebView2/Edge-Chromium on Windows, WKWebView on macOS) inside an Avalonia view.

```xml
<webview:WebView x:Name="ShelfView"
                 Source="avares://Ogma.App/Assets/shelf/index.html" />
```

**Prescriptions:**

1. **Bundle the Three.js scene** (`index.html`, JS, GLTF/textures) as
   `AvaloniaResource` and load via `avares://`, or serve from a localhost loopback
   for richer asset loading.
2. **Bridge C# ↔ JS** for selection events: post the selected book ID from JS to
   C# (host object / message channel) and route it into the `ShelfViewModel`; push
   catalogue data from C# into the scene via `ExecuteScript`/`PostMessage`.
3. **Platform dependency:** ensure the **WebView2 runtime** is present on Windows
   (bundle the evergreen bootstrapper in the installer); WKWebView is built into
   macOS.
4. **Threading:** WebView calls are async and must marshal results back to the UI
   thread before touching view models.
5. **Fallback:** keep the native `ListBox`/`ItemsRepeater` grid view as a
   non-WebView fallback so the catalogue is usable if the WebView fails to init.

---

## 13. Dependency injection & services **[Modern]**

The book wires view models directly in code-behind/lifetime. For Ogma, use
`Microsoft.Extensions.DependencyInjection`:

```csharp
// Program.cs / App startup
var services = new ServiceCollection();
services.AddSingleton<ILibraryRepository, SqliteLibraryRepository>();
services.AddSingleton<IPdfService, PdfPigService>();
services.AddSingleton<IDialogService, DialogService>();
services.AddTransient<LibraryViewModel>();
services.AddTransient<ShellViewModel>();
var provider = services.BuildServiceProvider();
```

Resolve the root view model from the container in
`OnFrameworkInitializationCompleted`. Keep services behind interfaces so view
models are unit-testable and platform code (file dialogs, WebView) is mockable.

---

## 14. Custom controls (Ch. 7)

- **User controls** aggregate existing controls into a reusable component
  (`UserControl` root). Expose bindable state via **styled properties**
  (`AvaloniaProperty.Register<TOwner, T>`) — Avalonia's equivalent of WPF
  dependency properties — and custom **routed events**
  (`RoutedEvent.Register<…>` with a `RoutingStrategies`).
- **Templated controls** are lookless: behaviour is separate from appearance. Use
  a `ControlTheme`/`ControlTemplate` and `ContentPresenter` +
  `{TemplateBinding}` so the template reflects the consumer's property values.
  **Never hard-code sizes/colours in a template** — bind them.

```csharp
public static readonly StyledProperty<string?> FileNameProperty =
    AvaloniaProperty.Register<FileBrowser, string?>(nameof(FileName));
public string? FileName
{
    get => GetValue(FileNameProperty);
    set => SetValue(FileNameProperty, value);
}
```

**Prescription:** prefer **user controls + view models** for Ogma's composite UI
(book card, filter panel). Reach for templated controls only when you need a
genuinely new reusable widget with custom visuals/states.

---

## 15. Graphics & animations (Ch. 9)

- **Shapes:** `Rectangle`, `Ellipse`, `Line`, `Polyline`, `Polygon`, `Path`
  (`Fill`, `Stroke`, `StrokeThickness`, `StrokeDashArray`). Shapes are visual
  elements and can be content of content controls.
- **Keyframe animations** live in `Style.Animations` with `KeyFrame Cue="x%"`
  setters; control with `Duration`, `Delay`, `IterationCount` (`INFINITE`),
  `PlaybackDirection`, and `Easing` functions.
- **Transitions** react to property changes per `Setter` (`ThicknessTransition`,
  `BrushTransition`, `DoubleTransition`, `TransformOperationsTransition`, …) —
  ideal for hover/selection micro-interactions.
- **Render transforms:** `RotateTransform`, `ScaleTransform`,
  `TranslateTransform`, `SkewTransform`, `MatrixTransform`.

```xml
<Style Selector="Border.bookcard:pointerover">
    <Setter Property="Transitions">
        <Transitions><DoubleTransition Property="Opacity" Duration="0:0:0.15"/></Transitions>
    </Setter>
    <Setter Property="Opacity" Value="0.92" />
</Style>
```

**Prescription:** use lightweight **transitions** for catalogue hover/selection;
reserve elaborate keyframe animations for the WebView 3D scene. Rendering is
GPU-accelerated via **Skia** ("pixel perfect on every platform"), so prefer
declarative transitions over per-frame C#.

---

## 16. Performance tips (cross-cutting)

1. **Compiled bindings everywhere** (§6.2) — build-time safety + speed.
2. **Virtualize** the 2,000-book list; keep item templates minimal (§7.2).
3. **Async + background threads** for PDF parsing, thumbnail generation, and DB
   queries; marshal UI updates back with `Dispatcher.UIThread.Post`. Never block
   the UI thread.
4. **`Grid` over deeply nested `StackPanel`s** for complex layouts — fewer
   measure/arrange passes.
5. Prefer **`DynamicResource` only where values truly change** (theming);
   `StaticResource` elsewhere (cheaper).
6. **Lazy-load** cover images; cache decoded `Bitmap`s; dispose them when evicted.
7. Avoid unnecessary `Opacity`/`Effect`/`OpacityMask` on large lists (forces
   intermediate render surfaces).
8. Use the **Avalonia DevTools** (`Avalonia.Diagnostics`, F12 in debug) to inspect
   the visual tree, bindings, and layout cost.

---

## 17. Testing Avalonia apps **[Modern]**

The book does not cover testing. Standards for Ogma:

- **View models are plain C#** — unit-test them with xUnit/NUnit and no Avalonia
  dependency. This is the main reason to keep logic in view models and `Ogma.Core`.
- **Headless UI tests** with **`Avalonia.Headless`** (+ `Avalonia.Headless.XUnit`)
  render and drive the real UI without a window manager — assert on control state,
  simulate input, and capture frames.

```csharp
[AvaloniaTest]
public void Typing_filters_the_book_list()
{
    var window = new MainWindow { DataContext = new LibraryViewModel(_repo) };
    window.Show();
    var search = window.FindControl<TextBox>("SearchBox")!;
    search.Text = "avalonia";
    Dispatcher.UIThread.RunJobs();
    Assert.True(((LibraryViewModel)window.DataContext!).Books.Count < 2000);
}
```

- Mock services behind interfaces (DI, §13) so tests don't hit the filesystem,
  SQLite, or the WebView.
- For end-to-end smoke tests of the WebView 3D shelf, drive the bundled HTML with
  a JS test harness separately from the Avalonia layer.

---

## 18. Cross-platform packaging — Windows + macOS (Ch. 2, **[Modern]**)

The book points to per-platform OS docs but doesn't package. For Ogma:

- **Build:** `dotnet publish -c Release -r win-x64` /
  `-r osx-arm64` / `-r osx-x64`. Consider **self-contained** publish so end users
  need no separate .NET install; trim cautiously (Avalonia + reflection bindings
  can break under aggressive trimming — another reason for compiled bindings).
- **Windows:** package as **MSIX** or a Velopack/Squirrel installer; **bundle the
  WebView2 evergreen runtime** for the 3D shelf. Sign the binary.
- **macOS:** produce a **`.app` bundle**, then **codesign** and **notarize**
  (required by Gatekeeper); distribute as a notarized `.dmg`. Set the bundle
  identifier, icon (`.icns`), and `Info.plist` entitlements. WKWebView is built in.
- Keep platform-specific assets (icons, manifests, entitlements) in the desktop
  project; share everything else.

---

## 19. Quick "do / avoid" cheat sheet

**Do**
- Define UI in `.axaml`; logic in view models; domain in `Ogma.Core`.
- Use `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).
- Set `x:DataType` + compiled bindings on every view.
- Bind `ItemsSource` to `ObservableCollection<T>`; virtualize big lists.
- Style with selectors/`Classes`; re-skin via `ControlTheme`.
- Bundle PNGs as `AvaloniaResource`, reference with `avares://`.
- Localize via `.resx` + a culture-aware indexer; flex layouts for long text.
- Set `AutomationProperties.Name` on icon-only controls.
- Run PDF/DB/IO async off the UI thread.
- Test view models headless; mock services via DI.

**Avoid**
- Hand-written `INotifyPropertyChanged` boilerplate.
- Reflection bindings / missing `x:DataType` on hot lists.
- Wrapping `ListBox`/`DataGrid` in a `ScrollViewer`; nesting `ScrollViewer`s.
- WPF-style `Style TargetType` thinking — Avalonia uses selectors + `ControlTheme`.
- Hard-coded strings, fixed widths that break translations, hard-coded template values.
- `<FluentTheme Mode="…"/>` (use `RequestedThemeVariant`) and `Items="{Binding}"`
  (use `ItemsSource`) — both are pre-11 / book-era and now outdated.
- Blocking the UI thread or touching UI objects off-thread.

---

*Sources: Alessandro Del Sole, "Avalonia UI Succinctly" (Syncfusion, 2025),
chapters 1–9 — environment, lifecycle, XAML, panels, controls, resources/binding/
MVVM, custom controls, windows/navigation, graphics/animations. **[Modern]**
items reflect current Avalonia 11.x / .NET 10 practice (CommunityToolkit.Mvvm,
compiled bindings, `ItemsSource`, `ControlTheme`, `RequestedThemeVariant`,
`Avalonia.Headless`, DI, localization, accessibility, WebView, packaging) not
covered by the book.*
