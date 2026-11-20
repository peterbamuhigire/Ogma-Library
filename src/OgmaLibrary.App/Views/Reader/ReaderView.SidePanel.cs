using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.App.Views.Reader;

/// <summary>Side panel resizing (D-82) and the citation copy/save menus (READ-013).</summary>
public partial class ReaderView
{
    /// <summary>The keyboard step, in device-independent pixels, for Left and Right on the handle.</summary>
    public const double SidePanelKeyStep = 16;

    /// <summary>How long after the last keyboard resize step the width is saved.</summary>
    private static readonly TimeSpan SidePanelWidthSaveDelay = TimeSpan.FromMilliseconds(500);

    private DispatcherTimer? _sidePanelWidthSaveTimer;

    private DispatcherTimer SidePanelWidthSaveTimer
    {
        get
        {
            if (_sidePanelWidthSaveTimer is null)
            {
                _sidePanelWidthSaveTimer = new DispatcherTimer { Interval = SidePanelWidthSaveDelay };
                _sidePanelWidthSaveTimer.Tick += (_, _) => CommitSidePanelWidthNow();
            }

            return _sidePanelWidthSaveTimer;
        }
    }

    /// <summary>Saves a pending side panel width at once (the view is going away, or the timer fired).</summary>
    private void CommitSidePanelWidthNow()
    {
        _sidePanelWidthSaveTimer?.Stop();
        (DataContext as ReaderViewModel)?.CommitSidePanelWidth();
    }

    private void SidePanelResizeHandle_DragDelta(object? sender, VectorEventArgs e)
    {
        // The handle is on the panel's left edge, so dragging left widens the panel.
        if (e.Vector.X != 0 && DataContext is ReaderViewModel vm)
        {
            vm.ResizeSidePanel(vm.SidePanelWidth - e.Vector.X);
        }
    }

    private void SidePanelResizeHandle_DragCompleted(object? sender, VectorEventArgs e) =>
        CommitSidePanelWidthNow();

    private void SidePanelResizeHandle_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || DataContext is not ReaderViewModel vm)
        {
            return;
        }

        double delta = e.Key switch
        {
            Key.Left => SidePanelKeyStep,
            Key.Right => -SidePanelKeyStep,
            _ => 0,
        };
        if (delta == 0)
        {
            return;
        }

        e.Handled = true;
        vm.ResizeSidePanel(vm.SidePanelWidth + delta);

        // Debounced: a run of arrow presses is saved once.
        SidePanelWidthSaveTimer.Stop();
        SidePanelWidthSaveTimer.Start();
    }

    private void CopyCitationAsItem_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => CopyCitationAsAsync(sender), "reader.copy_citation_as");

    private async Task CopyCitationAsAsync(object? sender)
    {
        if (DataContext is not ReaderViewModel vm || FormatOf(sender) is not { } format)
        {
            return;
        }

        string text = vm.FormatCitation(format);
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (string.IsNullOrEmpty(text) || topLevel?.Clipboard is null)
        {
            return;
        }

        await topLevel.Clipboard.SetTextAsync(text).ConfigureAwait(true);
        vm.MarkCitationCopied();
    }

    private void SaveCitationAsItem_Click(object? sender, RoutedEventArgs e) =>
        UiActions.Run(() => SaveCitationAsAsync(sender), "reader.save_citation_as");

    private async Task SaveCitationAsAsync(object? sender)
    {
        if (DataContext is not ReaderViewModel vm || FormatOf(sender) is not { } format)
        {
            return;
        }

        string text = vm.FormatCitation(format);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider.CanSave != true)
        {
            // No save dialog on this host: keep the citation in the library's own export folder.
            await vm.ExportCitationAsync(format).ConfigureAwait(true);
            return;
        }

        string extension = CitationFormatInfo.Extension(format);
        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                SuggestedFileName = $"citation-p{vm.CurrentPageIndex + 1}.{extension}",
                DefaultExtension = extension,
                FileTypeChoices =
                [
                    new FilePickerFileType(vm.CitationFormatLabel(format))
                    {
                        Patterns = [$"*.{extension}"],
                    },
                ],
            }).ConfigureAwait(true);
        if (file is null)
        {
            return;
        }

        Stream stream = await file.OpenWriteAsync().ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
            await stream.WriteAsync(bytes).ConfigureAwait(false);
        }

        vm.MarkCitationSaved();
    }

    private static CitationFormat? FormatOf(object? sender) =>
        sender is Control { Tag: string tag } && Enum.TryParse(tag, out CitationFormat format) ? format : null;
}
