using System.Text.Json;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Reader.Progress;

/// <summary>
/// <see cref="IReaderLayoutPreferences"/> kept in a small JSON file in the data directory.
/// Writes go to a temporary file that is swapped in, so a crash never leaves a torn file;
/// an unreadable file simply falls back to the defaults.
/// </summary>
public sealed class JsonReaderLayoutPreferences : IReaderLayoutPreferences
{
    private readonly string _path;
    private readonly Lock _lock = new();
    private Dto _state;

    private sealed class Dto
    {
        public DisplayMode DefaultMode { get; set; } = DisplayMode.SinglePage;

        public bool DefaultCoverAlone { get; set; } = true;

        public Dictionary<string, BookDto> Books { get; set; } = [];
    }

    private sealed class BookDto
    {
        public DisplayMode Mode { get; set; }

        public bool CoverAlone { get; set; }
    }

    /// <summary>Creates the store under <paramref name="dataDirectory"/>.</summary>
    /// <param name="dataDirectory">The application data directory.</param>
    public JsonReaderLayoutPreferences(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "reader-layout.json");
        _state = Load();
    }

    /// <inheritdoc />
    public ReaderLayoutChoice GlobalDefault
    {
        get
        {
            lock (_lock)
            {
                return new ReaderLayoutChoice(Sanitise(_state.DefaultMode), _state.DefaultCoverAlone);
            }
        }
    }

    /// <inheritdoc />
    public void SetDefault(ReaderLayoutChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        lock (_lock)
        {
            _state.DefaultMode = Sanitise(choice.Mode);
            _state.DefaultCoverAlone = choice.CoverAlone;
            Save();
        }
    }

    /// <inheritdoc />
    public ReaderLayoutChoice GetForBook(string bookId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        lock (_lock)
        {
            return _state.Books.TryGetValue(bookId, out BookDto? book)
                ? new ReaderLayoutChoice(Sanitise(book.Mode), book.CoverAlone)
                : new ReaderLayoutChoice(Sanitise(_state.DefaultMode), _state.DefaultCoverAlone);
        }
    }

    /// <inheritdoc />
    public void SetForBook(string bookId, ReaderLayoutChoice choice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentNullException.ThrowIfNull(choice);
        lock (_lock)
        {
            _state.Books[bookId] = new BookDto { Mode = Sanitise(choice.Mode), CoverAlone = choice.CoverAlone };
            Save();
        }
    }

    private static DisplayMode Sanitise(DisplayMode mode) =>
        Enum.IsDefined(mode) ? mode : DisplayMode.SinglePage;

    private Dto Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<Dto>(File.ReadAllText(_path)) ?? new Dto()
                : new Dto();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Dto();
        }
    }

    private void Save()
    {
        try
        {
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_state));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Intentionally ignored: a view preference is never worth failing the reader; the choice stays in memory.
        }
    }
}
