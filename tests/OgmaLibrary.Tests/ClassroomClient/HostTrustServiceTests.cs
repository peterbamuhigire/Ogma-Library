using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 TOFU trust-pin tests.</summary>
public sealed class HostTrustServiceTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ChangedFingerprint = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public async Task HostTrustService_FirstUseRequiresExplicitAccept()
    {
        var store = new InMemoryHostTrustStore();
        var service = new HostTrustService(store, new ClassroomJoinParser());
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        HostTrustEvaluation evaluation = await service.EvaluateAsync(request, Fingerprint);

        Assert.Equal(HostTrustState.FirstUse, evaluation.State);
        Assert.Null(evaluation.PinnedFingerprint);
        Assert.Null(await store.GetAsync(HostTrustService.CreateHostKey(request)));
    }

    [Fact]
    public async Task HostTrustService_AcceptPinsAndThenTrustsMatchingFingerprint()
    {
        var store = new InMemoryHostTrustStore();
        var service = new HostTrustService(store, new ClassroomJoinParser());
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        HostTrustEvaluation accepted = await service.AcceptAsync(request, Fingerprint);
        HostTrustEvaluation trusted = await service.EvaluateAsync(request, Fingerprint);

        Assert.Equal(HostTrustState.Trusted, accepted.State);
        Assert.Equal(Fingerprint, accepted.PinnedFingerprint);
        Assert.Equal(HostTrustState.Trusted, trusted.State);
        Assert.Equal(Fingerprint, trusted.PinnedFingerprint);
    }

    [Fact]
    public async Task HostTrustService_RejectsPresentedCertificateMismatch()
    {
        var service = new HostTrustService(new InMemoryHostTrustStore(), new ClassroomJoinParser());
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        HostTrustEvaluation evaluation = await service.EvaluateAsync(request, ChangedFingerprint);
        HostTrustEvaluation accepted = await service.AcceptAsync(request, ChangedFingerprint);

        Assert.Equal(HostTrustState.Mismatch, evaluation.State);
        Assert.Equal(Fingerprint, evaluation.PinnedFingerprint);
        Assert.Equal(HostTrustState.Mismatch, accepted.State);
    }

    [Fact]
    public async Task HostTrustService_RejectsChangedCertificateAfterPin()
    {
        var service = new HostTrustService(new InMemoryHostTrustStore(), new ClassroomJoinParser());
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);
        await service.AcceptAsync(request, Fingerprint);

        HostTrustEvaluation evaluation = await service.EvaluateAsync(request, ChangedFingerprint);

        Assert.Equal(HostTrustState.Mismatch, evaluation.State);
        Assert.Equal(Fingerprint, evaluation.PinnedFingerprint);
    }

    [Fact]
    public async Task HostTrustService_IsRegisteredInClassroomClientServices()
    {
        IHostTrustService service = new ServiceCollection()
            .AddClassroomClientServices(Path.Combine(Path.GetTempPath(), $"ogma-classroom-trust-{Guid.NewGuid():N}"))
            .BuildServiceProvider()
            .GetRequiredService<IHostTrustService>();

        HostTrustEvaluation evaluation = await service.EvaluateAsync(
            new ClassroomJoinRequest("127.0.0.1", 7473, Fingerprint),
            Fingerprint);

        Assert.Equal(HostTrustState.FirstUse, evaluation.State);
    }
}
