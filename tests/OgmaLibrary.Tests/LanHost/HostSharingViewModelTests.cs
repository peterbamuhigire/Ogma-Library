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
        Assert.True(viewModel.CanShare);
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

    [Fact]
    public async Task HostSharingViewModel_RiskyActions_RequireExplicitConfirmation()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var viewModel = new HostSharingViewModel(host, settings);

        viewModel.RequestStartConfirmation();

        Assert.True(viewModel.IsStartConfirmationOpen);
        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.CanStart);

        viewModel.CancelStartConfirmation();

        Assert.False(viewModel.IsStartConfirmationOpen);
        Assert.True(viewModel.CanStart);

        viewModel.RequestStartConfirmation();
        await viewModel.ConfirmStartAsync();

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.IsStartConfirmationOpen);

        await viewModel.StopAsync();
        await viewModel.RequestContentModeChangeAsync();

        Assert.True(viewModel.IsFileStreamConfirmationOpen);
        Assert.Equal(HostContentDeliveryMode.PageRender, settings.Settings.ContentMode);

        viewModel.CancelFileStreamConfirmation();

        Assert.False(viewModel.IsFileStreamConfirmationOpen);
        Assert.Equal(HostContentDeliveryMode.PageRender, settings.Settings.ContentMode);

        await viewModel.RequestContentModeChangeAsync();
        await viewModel.ConfirmFileStreamAsync();

        Assert.False(viewModel.IsFileStreamConfirmationOpen);
        Assert.Equal(HostContentDeliveryMode.FileStream, settings.Settings.ContentMode);
    }

    [Fact]
    public async Task HostSharingViewModel_SharePanel_BuildsQrJoinPayloadAndCopyConfirmations()
    {
        var host = new FakeLibraryHostService();
        var settings = new FakeHostModeSettingsRepository();
        var viewModel = new HostSharingViewModel(host, settings);

        Assert.False(viewModel.CanShare);
        Assert.False(viewModel.IsSharePanelOpen);

        await viewModel.StartAsync();
        viewModel.OpenSharePanel();

        Assert.True(viewModel.IsSharePanelOpen);
        Assert.Contains("ogma-lan://127.0.0.1:7473/join", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Contains("name=Ogma%20Test%20Host", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Contains("fp=0123456789abcdef", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Contains("code=ABCD2345", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Contains("auth=enrollment-code", viewModel.ManualJoinUri, StringComparison.Ordinal);
        Assert.Equal("Enrollment code: ABCD2345", viewModel.EnrollmentCodeText);
        Assert.Contains("\u2588", viewModel.QrCodeText, StringComparison.Ordinal);
        Assert.Contains("0123 4567 89AB CDEF", viewModel.FullFingerprintText, StringComparison.Ordinal);

        viewModel.MarkJoinLinkCopied();

        Assert.True(viewModel.HasShareConfirmation);
        Assert.Equal("Join link copied to clipboard", viewModel.ShareConfirmationText);

        viewModel.MarkFingerprintCopied();

        Assert.Equal("Fingerprint copied to clipboard", viewModel.ShareConfirmationText);

        await viewModel.StopAsync();

        Assert.False(viewModel.CanShare);
        Assert.False(viewModel.IsSharePanelOpen);
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
                ErrorMessage: null,
                HostAddress: "127.0.0.1",
                EnrollmentCode: "ABCD2345");
            return Task.FromResult(_status);
        }

        public Task<LibraryHostStatus> StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _status = _status with
            {
                State = LibraryHostState.Stopped,
                CertificateFingerprint = null,
                HostAddress = null,
                EnrollmentCode = null,
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
