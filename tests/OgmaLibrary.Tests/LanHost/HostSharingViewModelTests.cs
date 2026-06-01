using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.LanHost;

namespace OgmaLibrary.Tests.LanHost;

public sealed class HostSharingViewModelTests
{
    [Fact]
    public async Task HostSharingViewModel_StartAndStop_UpdateControlState()
    {
        var host = new FakeLibraryHostService();
        var viewModel = new HostSharingViewModel(host);

        await viewModel.StartAsync();

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.CanStart);
        Assert.True(viewModel.CanStop);
        Assert.Equal("Running on :7473", viewModel.StatusText);
        Assert.Equal("0123456789ab", viewModel.FingerprintText);

        await viewModel.StopAsync();

        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.CanStart);
        Assert.False(viewModel.CanStop);
        Assert.Equal("Stopped", viewModel.StatusText);
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
