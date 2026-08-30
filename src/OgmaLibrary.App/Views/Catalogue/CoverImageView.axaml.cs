using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace OgmaLibrary.App.Views.Catalogue;

/// <summary>
/// Loads a manifest-relative cover asynchronously and keeps missing/corrupt files
/// on a deterministic, title-based placeholder. The control never accepts an
/// absolute asset path from the catalogue projection.
/// </summary>
public partial class CoverImageView : UserControl, INotifyPropertyChanged
{
    /// <summary>Manifest-relative asset path from the catalogue.</summary>
    public static readonly StyledProperty<string?> RelativePathProperty =
        AvaloniaProperty.Register<CoverImageView, string?>(nameof(RelativePath));

    /// <summary>Configured library root used to resolve the relative path.</summary>
    public static readonly StyledProperty<string?> RootPathProperty =
        AvaloniaProperty.Register<CoverImageView, string?>(nameof(RootPath));

    /// <summary>Safe placeholder label, normally the book title.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<CoverImageView, string?>(nameof(Label));

    private CancellationTokenSource? _loadCancellation;
    private Bitmap? _image;

    /// <summary>Initializes the cover image control.</summary>
    public CoverImageView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public new event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Manifest-relative asset path.</summary>
    public string? RelativePath
    {
        get => GetValue(RelativePathProperty);
        set => SetValue(RelativePathProperty, value);
    }

    /// <summary>Configured library root.</summary>
    public string? RootPath
    {
        get => GetValue(RootPathProperty);
        set => SetValue(RootPathProperty, value);
    }

    /// <summary>Fallback label.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Loaded image, if the safe path resolved and decoded.</summary>
    public Bitmap? Image => _image;

    /// <summary>True while the placeholder is shown.</summary>
    public bool IsPlaceholder => _image is null;

    /// <summary>True when a decoded image is available.</summary>
    public bool IsImageVisible => _image is not null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RelativePathProperty || change.Property == RootPathProperty)
        {
            _ = ReloadAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _image?.Dispose();
        _image = null;
        RaiseImageStateChanged();
        base.OnDetachedFromVisualTree(e);
    }

    private async Task ReloadAsync()
    {
        CancellationTokenSource? previous = _loadCancellation;
        previous?.Cancel();
        previous?.Dispose();
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        string? path = ResolveSafePath(RootPath, RelativePath);
        Bitmap? loaded = null;
        if (path is not null)
        {
            loaded = await Task.Run(() => TryLoad(path), cancellation.Token).ConfigureAwait(false);
        }

        if (cancellation.IsCancellationRequested || !ReferenceEquals(_loadCancellation, cancellation))
        {
            loaded?.Dispose();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cancellation.IsCancellationRequested || !ReferenceEquals(_loadCancellation, cancellation))
            {
                loaded?.Dispose();
                return;
            }

            _image?.Dispose();
            _image = loaded;
            RaiseImageStateChanged();
        });
    }

    private static Bitmap? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ResolveSafePath(string? rootPath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            string root = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string relative = relativePath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relative) ||
                relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                    .Any(segment => segment is "." or ".."))
            {
                return null;
            }

            string candidate = Path.GetFullPath(Path.Combine(root, relative));
            return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? candidate : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void RaiseImageStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaceholder)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsImageVisible)));
    }
}
