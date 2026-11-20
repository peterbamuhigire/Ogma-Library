using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.App.Settings;
using OgmaLibrary.App.ViewModels.Settings;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Diagnostics;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Ui;

/// <summary>
/// Sept-23 Phase 14, slice 5: Settings → Search and AI. Every model state shows an honest line
/// and only the commands that make sense; the download needs an explicit click after the size
/// and destination are stated; erasing needs a confirmation; Off is a choice, not a failure.
/// </summary>
public sealed class SemanticSearchSettingsViewModelTests
{
    private const long ModelBytes = 123_400_000;

    [Fact]
    public async Task NotInstalled_StatesSizeAndDestination_AndOffersOnlyDownload()
    {
        Harness h = CreateHarness();

        await h.ViewModel.LoadAsync();

        Assert.Equal(SemanticModelState.NotInstalled, h.ViewModel.ModelState);
        Assert.True(h.ViewModel.IsBuiltIn);
        Assert.Contains("123.4 MB", h.ViewModel.ModelStatusText, StringComparison.Ordinal);
        Assert.True(h.ViewModel.ShowDownloadConsent);
        Assert.Contains("123.4 MB", h.ViewModel.DownloadConsentText, StringComparison.Ordinal);
        Assert.Contains("huggingface.co", h.ViewModel.DownloadConsentText, StringComparison.Ordinal);
        Assert.True(h.ViewModel.CanDownload);
        Assert.False(h.ViewModel.CanCancelDownload);
        Assert.False(h.ViewModel.CanRemoveModel);
        Assert.False(h.ViewModel.HasDiskUse);
        Assert.Equal(0, h.Installer.InstallCalls);
    }

    [Fact]
    public async Task Downloading_ShowsPercent_AndOffersOnlyCancel()
    {
        Harness h = CreateHarness();
        await h.ViewModel.LoadAsync();
        h.Installer.HoldInstall = new TaskCompletionSource<EmbeddingModelInstallResult>();

        Task download = h.ViewModel.DownloadAsync();
        h.Installer.Report(new EmbeddingModelInstallProgress(ModelBytes / 4, ModelBytes));

        Assert.Equal(SemanticModelState.Downloading, h.ViewModel.ModelState);
        Assert.Equal(25, h.ViewModel.DownloadPercent, 0);
        Assert.Contains("25", h.ViewModel.ModelStatusText, StringComparison.Ordinal);
        Assert.True(h.ViewModel.IsDownloadProgressVisible);
        Assert.False(h.ViewModel.CanDownload);
        Assert.True(h.ViewModel.CanCancelDownload);
        Assert.False(h.ViewModel.CanRemoveModel);

        h.Installer.Report(new EmbeddingModelInstallProgress(ModelBytes, ModelBytes));
        Assert.Equal(SemanticModelState.Verifying, h.ViewModel.ModelState);

        h.ViewModel.CancelDownload();
        await download;
        Assert.Equal(SemanticModelState.NotInstalled, h.ViewModel.ModelState);
        Assert.True(h.ViewModel.CanDownload);
        Assert.False(h.ViewModel.ModelStatusText.StartsWith('⟦'));
    }

    [Fact]
    public async Task Ready_AfterDownload_RefreshesCapability_AndOffersRemove()
    {
        Harness h = CreateHarness();
        await h.ViewModel.LoadAsync();
        h.Installer.NextOutcome = EmbeddingModelInstallOutcome.Installed;

        await h.ViewModel.DownloadAsync();

        Assert.Equal(SemanticModelState.Ready, h.ViewModel.ModelState);
        Assert.Equal(1, h.Installer.InstallCalls);
        Assert.True(h.Capability.RefreshCalls > 0);
        Assert.True(h.ViewModel.CanRemoveModel);
        Assert.False(h.ViewModel.CanDownload);
        Assert.False(h.ViewModel.ShowDownloadConsent);
        Assert.True(h.ViewModel.HasDiskUse);
        Assert.Contains("123.4 MB", h.ViewModel.DiskUseText, StringComparison.Ordinal);

        await h.ViewModel.RemoveModelAsync();
        Assert.Equal(SemanticModelState.NotInstalled, h.ViewModel.ModelState);
        Assert.Equal(1, h.Installer.RemoveCalls);
    }

    [Theory]
    [InlineData(EmbeddingModelInstallOutcome.ChecksumMismatch, SemanticModelState.Error)]
    [InlineData(EmbeddingModelInstallOutcome.InsufficientSpace, SemanticModelState.Error)]
    [InlineData(EmbeddingModelInstallOutcome.ServerError, SemanticModelState.Error)]
    [InlineData(EmbeddingModelInstallOutcome.HostNotAllowed, SemanticModelState.Error)]
    [InlineData(EmbeddingModelInstallOutcome.Offline, SemanticModelState.Offline)]
    public async Task FailedDownload_ShowsCause_AndOffersRetry(EmbeddingModelInstallOutcome outcome, SemanticModelState expected)
    {
        Harness h = CreateHarness();
        await h.ViewModel.LoadAsync();
        h.Installer.NextOutcome = outcome;

        await h.ViewModel.DownloadAsync();

        Assert.Equal(expected, h.ViewModel.ModelState);
        Assert.True(h.ViewModel.CanDownload);
        Assert.Equal("Retry download", h.ViewModel.DownloadLabel);
        Assert.False(h.ViewModel.CanCancelDownload);
        Assert.False(h.ViewModel.ModelStatusText.StartsWith('⟦'));
        Assert.NotEqual(h.ViewModel.ModelStatusText, CreateHarness().ViewModel.ModelStatusText);
    }

    [Fact]
    public async Task Off_IsNotAFailure_AndShowsTheKeywordOnlyLabel()
    {
        Harness h = CreateHarness();
        await h.ViewModel.LoadAsync();

        h.ViewModel.IsOff = true;
        await h.ViewModel.PendingWork;

        Assert.Equal(SemanticProviderChoice.Off, h.Preferences.Current.SemanticProvider);
        Assert.Equal(SemanticProviderChoice.Off, h.Capability.Choice);
        Assert.True(h.ViewModel.IsOff);
        Assert.False(h.ViewModel.IsBuiltInSelected);
        Assert.Equal("Smart search is off. Search uses titles, authors and the full text of your books.", h.ViewModel.KeywordOnlyText);
        Assert.False(h.ViewModel.HasError);
        Assert.False(h.ViewModel.CanBuildIndex);
        Assert.False(h.ViewModel.CanPause);
        Assert.False(h.ViewModel.CanDownload);
    }

    [Fact]
    public async Task Ollama_NotDetected_ShowsGuidance_AndCheckAgainRefreshes()
    {
        Harness h = CreateHarness();
        await h.ViewModel.LoadAsync();

        h.ViewModel.IsOllama = true;
        await h.ViewModel.PendingWork;

        Assert.True(h.ViewModel.IsOllamaSelected);
        Assert.Equal("Ollama was not found on this device.", h.ViewModel.OllamaStatusText);
        Assert.Contains("ollama pull nomic-embed-text", h.ViewModel.OllamaGuidanceText, StringComparison.Ordinal);
        int before = h.Capability.RefreshCalls;
        h.Capability.NextAvailable = true;
        await h.ViewModel.CheckOllamaAsync();
        Assert.Equal(before + 1, h.Capability.RefreshCalls);
        Assert.Equal("Ollama is running on this device and smart search uses it.", h.ViewModel.OllamaStatusText);
    }

    [Fact]
    public async Task IndexControls_FollowAvailabilityAndPause_AndShowBooksAndEta()
    {
        Harness h = CreateHarness(installed: true, available: true);
        h.Progress.Next = new SemanticIndexProgress(30, 100, 7, TimeSpan.FromMinutes(4), false, BooksTotal: 10);

        await h.ViewModel.LoadAsync();

        Assert.Equal(SemanticModelState.Ready, h.ViewModel.ModelState);
        Assert.True(h.ViewModel.CanBuildIndex);
        Assert.True(h.ViewModel.CanPause);
        Assert.False(h.ViewModel.CanResume);
        Assert.Equal("3 of 10 books ready", h.ViewModel.IndexProgressText);
        Assert.Equal(30, h.ViewModel.IndexPercent, 0);
        Assert.Equal("About 4 minutes left", h.ViewModel.IndexEtaText);

        h.ViewModel.Pause();
        Assert.True(h.Control.IsPaused);
        Assert.False(h.ViewModel.CanPause);
        Assert.True(h.ViewModel.CanResume);
        h.ViewModel.Resume();
        Assert.False(h.Control.IsPaused);

        await h.ViewModel.BuildNowAsync();
        Assert.Equal(1, h.Control.BuildNowCalls);
    }

    [Fact]
    public async Task Erase_RequiresConfirmation()
    {
        Harness h = CreateHarness(installed: true, available: true);
        await h.ViewModel.LoadAsync();

        h.ViewModel.RequestErase();
        Assert.True(h.ViewModel.IsEraseConfirmationVisible);
        Assert.Equal(0, h.Erasure.Calls);
        h.ViewModel.CancelErase();
        Assert.False(h.ViewModel.IsEraseConfirmationVisible);
        Assert.Equal(0, h.Erasure.Calls);

        await h.ViewModel.ConfirmEraseAsync();
        Assert.Equal(0, h.Erasure.Calls);

        h.ViewModel.RequestErase();
        await h.ViewModel.ConfirmEraseAsync();
        Assert.Equal(1, h.Erasure.Calls);
        Assert.False(h.ViewModel.IsEraseConfirmationVisible);
        Assert.Contains("5", h.ViewModel.EraseStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnvironmentOverride_DisablesProviderChoice_WithReason()
    {
        Harness h = CreateHarness();
        h.Capability.Managed = true;

        await h.ViewModel.LoadAsync();

        Assert.False(h.ViewModel.CanChooseProvider);
        Assert.True(h.ViewModel.IsProviderManaged);
        Assert.Contains("OGMA_SEMANTIC_PROVIDER", h.ViewModel.ProviderManagedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task French_ReRendersLabels()
    {
        Harness h = CreateHarness();
        await h.ViewModel.LoadAsync();
        string english = h.ViewModel.ModelStatusText;

        h.Localization.SetCulture("fr");

        Assert.NotEqual(english, h.ViewModel.ModelStatusText);
        Assert.False(h.ViewModel.ProviderLabel.StartsWith('⟦'));
        Assert.Equal("Recherche intelligente", h.ViewModel.ProviderLabel);
    }

    internal static Harness CreateHarness(bool installed = false, bool available = false)
    {
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("en");
        var capability = new FakeCapability { NextAvailable = available };
        capability.Available = available;
        var preferences = new UserPreferencesController(
            null,
            localization: localization,
            systemCulture: () => CultureInfo.GetCultureInfo("en-US"),
            semantic: capability);
        var installer = new FakeInstaller { Installed = installed };
        var control = new FakeControl();
        var progress = new FakeProgress();
        var erasure = new FakeErasure();
        var viewModel = new SemanticSearchSettingsViewModel(
            capability,
            installer,
            control,
            progress,
            erasure,
            preferences,
            localization,
            InlineUiDispatcher.Instance,
            NullLogger.Instance);
        return new Harness(viewModel, capability, installer, control, progress, erasure, preferences, localization);
    }

    internal sealed record Harness(
        SemanticSearchSettingsViewModel ViewModel,
        FakeCapability Capability,
        FakeInstaller Installer,
        FakeControl Control,
        FakeProgress Progress,
        FakeErasure Erasure,
        UserPreferencesController Preferences,
        InMemoryLocalizationService Localization);

    internal sealed class FakeCapability : ISemanticCapabilityService
    {
        public SemanticProviderChoice Choice { get; private set; } = SemanticProviderChoice.BuiltIn;

        public bool IsManagedByEnvironment => Managed;

        public bool Managed { get; set; }

        public bool IsAvailable => Available && Choice != SemanticProviderChoice.Off;

        public bool Available { get; set; }

        public bool NextAvailable { get; set; }

        public int RefreshCalls { get; private set; }

        public event EventHandler? Changed;

        public void ApplyPreferences(UserPreferences preferences)
        {
            if (Choice != preferences.SemanticProvider)
            {
                Choice = preferences.SemanticProvider;
                Available = false;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public Task<bool> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCalls++;
            Available = NextAvailable;
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(IsAvailable);
        }
    }

    internal sealed class FakeInstaller : IEmbeddingModelInstaller
    {
        private IProgress<EmbeddingModelInstallProgress>? _progress;

        public bool Installed { get; set; }

        public int InstallCalls { get; private set; }

        public int RemoveCalls { get; private set; }

        public EmbeddingModelInstallOutcome NextOutcome { get; set; } = EmbeddingModelInstallOutcome.Installed;

        public TaskCompletionSource<EmbeddingModelInstallResult>? HoldInstall { get; set; }

        public void Report(EmbeddingModelInstallProgress value) => _progress?.Report(value);

        public Task<EmbeddingModelInstallStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingModelInstallStatus(
                Installed ? EmbeddingModelInstallState.Installed : EmbeddingModelInstallState.NotInstalled,
                "multilingual-e5-small",
                "rev",
                "MIT",
                Installed ? 0 : ModelBytes,
                ModelBytes)
            {
                DownloadHost = "huggingface.co",
            });

        public async Task<EmbeddingModelInstallResult> InstallAsync(
            IProgress<EmbeddingModelInstallProgress>? progress,
            CancellationToken cancellationToken)
        {
            InstallCalls++;
            _progress = progress;
            if (HoldInstall is not null)
            {
                await HoldInstall.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            Installed = NextOutcome == EmbeddingModelInstallOutcome.Installed;
            return new EmbeddingModelInstallResult(NextOutcome);
        }

        public Task RemoveAsync(CancellationToken cancellationToken)
        {
            RemoveCalls++;
            Installed = false;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeControl : ISemanticIndexControl
    {
        public bool IsPaused { get; private set; }

        public int BuildNowCalls { get; private set; }

        public event EventHandler? Changed;

        public void Pause()
        {
            IsPaused = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void ResumeIndexing()
        {
            IsPaused = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public Task BuildNowAsync(CancellationToken cancellationToken)
        {
            BuildNowCalls++;
            IsPaused = false;
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeProgress : ISemanticSearchReadModel, IObservable<SemanticIndexEvent>
    {
        public SemanticIndexProgress Next { get; set; } = new(0, 0, 0, null, false);

        public IObservable<SemanticIndexEvent> Events => this;

        public Task<SemanticIndexProgress> GetProgressAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Next);

        public IDisposable Subscribe(IObserver<SemanticIndexEvent> observer) => new Unsubscriber();

        private sealed class Unsubscriber : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    internal sealed class FakeErasure : IEmbeddingErasureService
    {
        public int Calls { get; private set; }

        public Task<EmbeddingErasureResult> EraseAllAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new EmbeddingErasureResult(120, 5, DateTimeOffset.UtcNow));
        }
    }
}
