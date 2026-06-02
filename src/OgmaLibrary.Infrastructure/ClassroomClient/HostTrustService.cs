using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>TOFU trust-pin evaluator for Classroom Client onboarding.</summary>
internal sealed class HostTrustService : IHostTrustService
{
    private readonly IHostTrustStore _store;
    private readonly IClassroomJoinParser _joinParser;

    public HostTrustService(IHostTrustStore store, IClassroomJoinParser joinParser)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _joinParser = joinParser ?? throw new ArgumentNullException(nameof(joinParser));
    }

    public async Task<HostTrustEvaluation> EvaluateAsync(
        ClassroomJoinRequest request,
        string presentedFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedPresented = NormalizeFingerprint(request, presentedFingerprint);
        string hostKey = CreateHostKey(request);
        HostTrustPin? pin = await _store.GetAsync(hostKey, cancellationToken).ConfigureAwait(false);

        if (!normalizedPresented.Equals(request.CertificateFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return new HostTrustEvaluation(
                request,
                HostTrustState.Mismatch,
                normalizedPresented,
                pin?.CertificateFingerprint ?? request.CertificateFingerprint);
        }

        if (pin is null)
        {
            return new HostTrustEvaluation(request, HostTrustState.FirstUse, normalizedPresented, null);
        }

        HostTrustState state = normalizedPresented.Equals(
            pin.CertificateFingerprint,
            StringComparison.OrdinalIgnoreCase)
                ? HostTrustState.Trusted
                : HostTrustState.Mismatch;
        return new HostTrustEvaluation(request, state, normalizedPresented, pin.CertificateFingerprint);
    }

    public async Task<HostTrustEvaluation> AcceptAsync(
        ClassroomJoinRequest request,
        string presentedFingerprint,
        CancellationToken cancellationToken = default)
    {
        HostTrustEvaluation evaluation = await EvaluateAsync(
                request,
                presentedFingerprint,
                cancellationToken)
            .ConfigureAwait(false);

        if (evaluation.State == HostTrustState.Mismatch)
        {
            return evaluation;
        }

        string hostKey = CreateHostKey(request);
        var pin = new HostTrustPin(
            hostKey,
            request.Address,
            request.Port,
            evaluation.PresentedFingerprint,
            DateTimeOffset.UtcNow);
        await _store.SaveAsync(pin, cancellationToken).ConfigureAwait(false);
        return evaluation with
        {
            State = HostTrustState.Trusted,
            PinnedFingerprint = evaluation.PresentedFingerprint,
        };
    }

    internal static string CreateHostKey(ClassroomJoinRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return $"{request.Address.Trim().ToLowerInvariant()}:{request.Port}";
    }

    private string NormalizeFingerprint(ClassroomJoinRequest request, string fingerprint)
    {
        string payload = $"ogma-lan://{request.Address}:{request.Port}/join?fp={Uri.EscapeDataString(fingerprint)}";
        ClassroomJoinRequest parsed = _joinParser.Parse(payload);
        return parsed.CertificateFingerprint;
    }
}
