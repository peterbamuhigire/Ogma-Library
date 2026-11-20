using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>Sept-23 Phase 12 slice 4: the reader's open-protected route (READ-009).</summary>
public sealed partial class ShellReaderNavigationTests
{
    private const string LockedBookId = "book-locked";

    [AvaloniaFact]
    public async Task Reader_LockedBook_AsksThenOpens_AndClearsPassword()
    {
        char[] typed = "s3cret-pass".ToCharArray();
        var prompt = new OneShotPrompt(new PasswordPromptResult(typed, remember: false));
        (ReaderViewModel reader, LockedSessions _) = CreateLockedReader(prompt, new CapturingLogger());

        bool opened = await reader.OpenAsync(LockedBookId, pageHint: 2, CancellationToken.None);

        Assert.True(opened);
        Assert.True(reader.IsOpen);
        Assert.Null(reader.StatusMessage);
        Assert.All(typed, ch => Assert.Equal('\0', ch));
    }

    [AvaloniaFact]
    public async Task ReaderTitle_ShowsBookTitle_WhenOpen_AndLabelBeforeThat()
    {
        var prompt = new OneShotPrompt(new PasswordPromptResult("s3cret-pass".ToCharArray(), remember: false));
        (ReaderViewModel reader, LockedSessions _) = CreateLockedReader(prompt, new CapturingLogger());
        string label = new InMemoryLocalizationService()["Reader.Panel.Title"];
        Assert.Equal(label, reader.ReaderTitle);

        await reader.OpenAsync(LockedBookId, pageHint: null, CancellationToken.None);

        Assert.Equal("Locked book", reader.ReaderTitle);
    }

    [AvaloniaFact]
    public async Task Reader_LockedBook_Cancel_LeavesLockedState()
    {
        var prompt = new OneShotPrompt((PasswordPromptResult?)null);
        (ReaderViewModel reader, LockedSessions _) = CreateLockedReader(prompt, new CapturingLogger());

        bool opened = await reader.OpenAsync(LockedBookId, pageHint: null, CancellationToken.None);

        Assert.False(opened);
        Assert.False(reader.IsOpen);
        Assert.Equal(new InMemoryLocalizationService()["Reader.Error.PasswordProtected"], reader.StatusMessage);
        Assert.DoesNotContain("not be found", reader.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task Password_NotLogged()
    {
        const string secret = "s3cret-pass";
        var logger = new CapturingLogger();
        var prompt = new OneShotPrompt(
            new PasswordPromptResult("wrong-guess".ToCharArray(), remember: true),
            new PasswordPromptResult(secret.ToCharArray(), remember: true));
        (ReaderViewModel reader, LockedSessions _) = CreateLockedReader(prompt, logger);

        await reader.OpenAsync(LockedBookId, pageHint: null, CancellationToken.None);

        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, m => m.Contains(secret, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("wrong-guess", StringComparison.Ordinal));
    }

    private static (ReaderViewModel Reader, LockedSessions Sessions) CreateLockedReader(
        IPasswordPrompt prompt,
        ILogger<ReaderViewModel> logger)
    {
        var localization = new InMemoryLocalizationService();
        var sessions = new LockedSessions("s3cret-pass");
        var unlock = new PasswordUnlockViewModel(new NoStoredPasswords(), localization, sessions, prompt);
        var reader = new ReaderViewModel(
            sessions,
            new EmptyAnnotationService(),
            new EmptyBookmarkService(),
            new EmptyLayerService(),
            new EmptyCitationService(),
            new EmptyReadingMemoryService(),
            localization,
            logger: logger,
            unlock: unlock,
            bookIdentity: (_, _) => Task.FromResult<(string Hash, string? Title)?>((new string('a', 64), "Locked book")));
        return (reader, sessions);
    }

    private sealed class OneShotPrompt : IPasswordPrompt
    {
        private readonly Queue<PasswordPromptResult?> _answers;

        public OneShotPrompt(params PasswordPromptResult?[] answers) => _answers = new Queue<PasswordPromptResult?>(answers);

        public Task<PasswordPromptResult?> PromptAsync(PasswordPromptRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
    }

    private sealed class NoStoredPasswords : IPasswordProvider
    {
        public Task<PasswordResult> GetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(PasswordResult.Failed("No stored password."));

        public Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CapturingLogger : ILogger<ReaderViewModel>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + exception);
    }

    private sealed class LockedSessions : IReaderSessionService
    {
        private readonly string _correct;

        public LockedSessions(string correct) => _correct = correct;

        public ReaderSession? CurrentSession { get; private set; }

        public IPdfRenderer? CurrentRenderer => null;

        public Task<ReaderSession> OpenAsync(string bookId, int? pageHint, CancellationToken ct) =>
            throw new PdfPasswordRequiredException("locked.pdf");

        public Task<ReaderSession> OpenProtectedAsync(string bookId, int? pageHint, char[] password, CancellationToken ct)
        {
            if (!new string(password).Equals(_correct, StringComparison.Ordinal))
            {
                throw new PdfPasswordIncorrectException("locked.pdf");
            }

            CurrentSession = new ReaderSession(
                bookId, "locked.pdf", PageCount: 4, CurrentPageIndex: pageHint ?? 0, ScrollOffset: 0,
                ZoomMode.FitWidth, ZoomPercent: 100, DisplayMode.SinglePage);
            return Task.FromResult(CurrentSession);
        }

        public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;

        public Task NavigateToAsync(int pageIndex, double scrollOffset = 0) => Task.CompletedTask;

        public void UpdateScrollOffset(double scrollOffset)
        {
        }
    }
}
