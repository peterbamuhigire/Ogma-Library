using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Localization;

namespace OgmaLibrary.Tests.Security;

/// <summary>Sept-23 Phase 12 slice 4: the in-app unlock flow with opt-in keychain storage (READ-009).</summary>
public sealed class PasswordUnlockFlowTests
{
    private const string BookId = "BOOK-UNLOCK-00000000001";
    private static readonly string Hash = new('a', 64);

    [Fact]
    public async Task Unlock_CorrectPassword_OpensSession()
    {
        var prompt = new ScriptedPrompt(new PasswordPromptResult("right".ToCharArray(), remember: false));
        var sessions = new Sessions("right");
        PasswordUnlockViewModel vm = Create(new Provider(), sessions, prompt);

        ReaderSession? session = await vm.RequestAndOpenAsync(BookId, Hash, "Locked book");

        Assert.NotNull(session);
        Assert.Equal("right", sessions.OpenedWith);
        Assert.Equal("Book unlocked", vm.StatusText);
        Assert.All(prompt.Issued.Select(p => p.Password).Single(), ch => Assert.Equal('\0', ch));
    }

    [Fact]
    public async Task Unlock_WrongPassword_ShowsWrongPasswordMessage()
    {
        var prompt = new ScriptedPrompt(
            new PasswordPromptResult("bad".ToCharArray(), remember: true),
            new PasswordPromptResult("right".ToCharArray(), remember: false));
        var provider = new Provider();
        var localization = new InMemoryLocalizationService();
        PasswordUnlockViewModel vm = Create(provider, new Sessions("right"), prompt, localization);

        ReaderSession? session = await vm.RequestAndOpenAsync(BookId, Hash, "Locked book");

        Assert.NotNull(session);
        Assert.Equal([false, true], prompt.Requests.Select(r => r.WrongPassword));
        Assert.Empty(provider.Stored); // a wrong password is never remembered
        var dialog = new PasswordUnlockDialogViewModel(localization, prompt.Requests[1]);
        Assert.Contains("password", dialog.WrongPasswordText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("found", dialog.WrongPasswordText, StringComparison.OrdinalIgnoreCase);
        var french = new InMemoryLocalizationService();
        french.SetCulture("fr");
        string frenchText = new PasswordUnlockDialogViewModel(french, prompt.Requests[1]).WrongPasswordText;
        Assert.Contains("mot de passe", frenchText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("introuvable", frenchText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unlock_Cancel_LeavesLockedState()
    {
        var sessions = new Sessions("right");
        PasswordUnlockViewModel vm = Create(new Provider(), sessions, new ScriptedPrompt((PasswordPromptResult?)null));

        ReaderSession? session = await vm.RequestAndOpenAsync(BookId, Hash, "Locked book");

        Assert.Null(session);
        Assert.Null(sessions.OpenedWith);
        Assert.Equal("Password unlock cancelled", vm.StatusText);
    }

    [Fact]
    public async Task Remember_OptIn_StoresViaProvider()
    {
        var provider = new Provider();
        var optIn = new ScriptedPrompt(new PasswordPromptResult("right".ToCharArray(), remember: true));
        await Create(provider, new Sessions("right"), optIn).RequestAndOpenAsync(BookId, Hash, "Locked book");
        Assert.Equal(["right"], provider.Stored);

        var optOutProvider = new Provider();
        var optOut = new ScriptedPrompt(new PasswordPromptResult("right".ToCharArray(), remember: false));
        await Create(optOutProvider, new Sessions("right"), optOut).RequestAndOpenAsync(BookId, Hash, "Locked book");
        Assert.Empty(optOutProvider.Stored);
    }

    [Fact]
    public async Task Unlock_StoredPassword_SkipsDialog()
    {
        var provider = new Provider { StoredPassword = "right" };
        var prompt = new ScriptedPrompt((PasswordPromptResult?)null);

        ReaderSession? session = await Create(provider, new Sessions("right"), prompt)
            .RequestAndOpenAsync(BookId, Hash, "Locked book");

        Assert.NotNull(session);
        Assert.Empty(prompt.Requests);
    }

    [Fact]
    public async Task Unlock_StaleStoredPassword_IsForgottenThenAsks()
    {
        var provider = new Provider { StoredPassword = "old" };
        var prompt = new ScriptedPrompt(new PasswordPromptResult("right".ToCharArray(), remember: false));

        ReaderSession? session = await Create(provider, new Sessions("right"), prompt)
            .RequestAndOpenAsync(BookId, Hash, "Locked book");

        Assert.NotNull(session);
        Assert.True(provider.Forgotten);
        Assert.Single(prompt.Requests);
    }

    private static PasswordUnlockViewModel Create(
        Provider provider,
        Sessions sessions,
        IPasswordPrompt prompt,
        InMemoryLocalizationService? localization = null) =>
        new(provider, localization ?? new InMemoryLocalizationService(), sessions, prompt);

    private sealed class Provider : IPasswordProvider
    {
        public string? StoredPassword { get; init; }

        public List<string> Stored { get; } = [];

        public bool Forgotten { get; private set; }

        public Task<PasswordResult> GetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(StoredPassword is null
                ? PasswordResult.Failed("No stored password.")
                : PasswordResult.Success(StoredPassword.ToCharArray(), wasStored: true));

        public Task<bool> StorePasswordAsync(
            PasswordRequest request,
            char[] password,
            CancellationToken cancellationToken = default)
        {
            Stored.Add(new string(password));
            return Task.FromResult(true);
        }

        public Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default)
        {
            Forgotten = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ScriptedPrompt : IPasswordPrompt
    {
        private readonly Queue<PasswordPromptResult?> _answers;

        public ScriptedPrompt(params PasswordPromptResult?[] answers)
        {
            _answers = new Queue<PasswordPromptResult?>(answers);
            foreach (PasswordPromptResult? answer in answers.OfType<PasswordPromptResult>())
            {
                Issued.Add(answer);
            }
        }

        public List<PasswordPromptRequest> Requests { get; } = [];

        public List<PasswordPromptResult> Issued { get; } = [];

        public Task<PasswordPromptResult?> PromptAsync(PasswordPromptRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
        }
    }

    private sealed class Sessions : IReaderSessionService
    {
        private readonly string _correct;

        public Sessions(string correct) => _correct = correct;

        public string? OpenedWith { get; private set; }

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

            OpenedWith = new string(password);
            CurrentSession = new ReaderSession(
                bookId, "locked.pdf", PageCount: 1, CurrentPageIndex: 0, ScrollOffset: 0,
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
