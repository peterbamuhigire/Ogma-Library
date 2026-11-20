using OgmaLibrary.Application.Reader;
using OgmaLibrary.Reader.Progress;
using Xunit;

namespace OgmaLibrary.Tests.Reader;

public sealed class ReaderLayoutPreferencesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ogma-layout-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void DisplayMode_PersistsPerBook_DefaultFromSettings()
    {
        var store = new JsonReaderLayoutPreferences(_dir);
        Assert.Equal(DisplayMode.SinglePage, store.GlobalDefault.Mode);

        store.SetDefault(new ReaderLayoutChoice(DisplayMode.Continuous, true));
        store.SetForBook("book-a", new ReaderLayoutChoice(DisplayMode.TwoPage, false));

        // A new process sees the same answers: the book keeps its own layout, others get the default.
        var reopened = new JsonReaderLayoutPreferences(_dir);
        Assert.Equal(new ReaderLayoutChoice(DisplayMode.TwoPage, false), reopened.GetForBook("book-a"));
        Assert.Equal(new ReaderLayoutChoice(DisplayMode.Continuous, true), reopened.GetForBook("book-b"));
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "reader-layout.json"), "{ not json");
        var store = new JsonReaderLayoutPreferences(_dir);
        Assert.Equal(ReaderLayoutChoice.Default, store.GetForBook("any"));
    }
}
