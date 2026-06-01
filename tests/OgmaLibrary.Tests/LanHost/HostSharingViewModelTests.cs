using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Tests.LanHost;

public sealed class HostSharingViewModelTests
{
    [Fact]
    public async Task HostSharingViewModel_StartAndStop_UpdateControlState()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var viewModel = new HostSharingViewModel(host, settings);

        await viewModel.ToggleContentModeAsync();

        Assert.Equal(HostContentDeliveryMode.FileStream, settings.Settings.ContentMode);
        Assert.Equal("File Stream", viewModel.ContentModeText);
        Assert.Equal("Use Page Render", viewModel.ToggleContentModeText);

        await viewModel.StartAsync();

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.CanStart);
        Assert.True(viewModel.CanStop);
        Assert.False(viewModel.CanChangeContentMode);
        Assert.Equal("Running on :7473", viewModel.StatusText);
        Assert.Equal("0123456789ab", viewModel.FingerprintText);

        await viewModel.ToggleContentModeAsync();

        Assert.Equal(HostContentDeliveryMode.FileStream, settings.Settings.ContentMode);

        await viewModel.StopAsync();

        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.CanStart);
        Assert.False(viewModel.CanStop);
        Assert.Equal("Stopped", viewModel.StatusText);
    }

    private sealed class FakeHostModeSettingsRepository : IHostModeSettingsRepository
    {
        public HostModeSettings Settings { get; private set; } = new(
            IsEnabled: false,
            Port: 7473,
            ContentMode: HostContentDeliveryMode.PageRender,
            DisplayName: "Ogma Test Host");

        public Task<HostModeSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Settings);
        }

        public Task SaveAsync(HostModeSettings settings, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLibraryHostService : ILibraryHostService
    {
        private LibraryHostStatus _status = new(
            LibraryHostState.Stopped,
            Port: 7473,
            ConnectedClientCount: 0,
            CertificateFingerprint: null,
            ErrorMessage: null);

        public Task<LibraryHostStatus> StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _status = new LibraryHostStatus(
                LibraryHostState.Running,
                Port: 7473,
                ConnectedClientCount: 0,
                CertificateFingerprint: string.Concat(Enumerable.Repeat("0123456789abcdef", 4)),
                ErrorMessage: null);
            return Task.FromResult(_status);
        }

        public Task<LibraryHostStatus> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _status = _status with
            {
                State = LibraryHostState.Stopped,
                CertificateFingerprint = null,
            };
            return Task.FromResult(_status);
        }

        public Task<LibraryHostStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_status);
        }
    }
}
