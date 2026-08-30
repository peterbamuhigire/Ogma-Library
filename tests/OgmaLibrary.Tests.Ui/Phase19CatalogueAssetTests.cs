using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OgmaLibrary.App.Views.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Headless proof for safe local cover loading in the 2D catalogue.</summary>
public sealed class Phase19CatalogueAssetTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [AvaloniaFact]
    public async Task CoverImageView_LoadsSafeManifestRelativeImageOffTheUiThread()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-cover-{Guid.NewGuid():N}");
        string path = Path.Combine(root, ".ogma", "covers", "aa", "cover.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, OnePixelPng);

        try
        {
            var cover = new CoverImageView
            {
                RootPath = root,
                RelativePath = ".ogma/covers/aa/cover.png",
                Label = "Safe cover",
            };
            var window = new Avalonia.Controls.Window { Width = 120, Height = 160, Content = cover };
            window.Show();

            for (int attempt = 0; attempt < 20 && !cover.IsImageVisible; attempt++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.True(cover.IsImageVisible);
            Assert.False(cover.IsPlaceholder);
            window.Close();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task CoverImageView_RejectsTraversalAndKeepsPlaceholder()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ogma-cover-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var cover = new CoverImageView
            {
                RootPath = root,
                RelativePath = ".ogma/covers/../outside.png",
                Label = "Fallback",
            };
            var window = new Avalonia.Controls.Window { Width = 120, Height = 160, Content = cover };
            window.Show();
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();

            Assert.True(cover.IsPlaceholder);
            Assert.False(cover.IsImageVisible);
            window.Close();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
