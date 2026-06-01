using System.Net;

namespace OgmaLibrary.Infrastructure.Metadata.Providers;

/// <summary>Provider-specific HTTP rate limiting and transient retry policy.</summary>
public sealed record MetadataProviderRateLimitPolicy(
    string ProviderName,
    TimeSpan MinInterval,
    int MaxRetries,
    TimeSpan BaseBackoff,
    TimeSpan MaxBackoff)
{
    /// <summary>Google Books: one request per second, retry transient provider pressure.</summary>
    public static MetadataProviderRateLimitPolicy GoogleBooks { get; } =
        new("GoogleBooks", TimeSpan.FromSeconds(1), 5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60));

    /// <summary>Open Library: five requests per second, retry transient provider pressure.</summary>
    public static MetadataProviderRateLimitPolicy OpenLibrary { get; } =
        new("OpenLibrary", TimeSpan.FromMilliseconds(200), 5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60));
}

/// <summary>
/// Delegating handler that applies per-provider request spacing and exponential
/// backoff for HTTP 429/503 metadata-provider responses.
/// </summary>
public sealed class RateLimitedHttpClientHandler : DelegatingHandler
{
    private static readonly HttpStatusCode[] RetryableStatuses =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.ServiceUnavailable,
    ];

    private readonly MetadataProviderRateLimitPolicy _policy;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly object _gate = new();
    private DateTimeOffset _nextAllowedUtc = DateTimeOffset.MinValue;

    /// <summary>Initializes a new instance of <see cref="RateLimitedHttpClientHandler"/>.</summary>
    public RateLimitedHttpClientHandler(MetadataProviderRateLimitPolicy policy)
        : this(policy, static (delay, ct) => Task.Delay(delay, ct))
    {
    }

    internal RateLimitedHttpClientHandler(
        MetadataProviderRateLimitPolicy policy,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(delayAsync);
        if (policy.MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "MaxRetries cannot be negative.");
        }

        _policy = policy;
        _delayAsync = delayAsync;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpRequestMessage? original = await CloneForRetryAsync(request, cancellationToken)
            .ConfigureAwait(false);

        for (int attempt = 0; attempt <= _policy.MaxRetries; attempt++)
        {
            await WaitForRateLimitAsync(cancellationToken).ConfigureAwait(false);

            HttpRequestMessage attemptRequest = attempt == 0
                ? request
                : await CloneForRetryAsync(original!, cancellationToken).ConfigureAwait(false);

            HttpResponseMessage response = await base.SendAsync(attemptRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!ShouldRetry(response.StatusCode) || attempt == _policy.MaxRetries)
            {
                original?.Dispose();
                return response;
            }

            TimeSpan delay = GetBackoffDelay(attempt, response);
            response.Dispose();
            await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
        }

        original?.Dispose();
        throw new InvalidOperationException($"Provider retry loop exhausted for {_policy.ProviderName}.");
    }

    private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        TimeSpan delay;
        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            delay = _nextAllowedUtc > now ? _nextAllowedUtc - now : TimeSpan.Zero;
            _nextAllowedUtc = now + delay + _policy.MinInterval;
        }

        if (delay > TimeSpan.Zero)
        {
            await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan GetBackoffDelay(int attempt, HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfter && retryAfter > TimeSpan.Zero)
        {
            return Min(retryAfter, _policy.MaxBackoff);
        }

        double exponential = Math.Pow(2, attempt);
        TimeSpan raw = TimeSpan.FromMilliseconds(_policy.BaseBackoff.TotalMilliseconds * exponential);
        TimeSpan capped = Min(raw, _policy.MaxBackoff);
        double jitterRatio = Random.Shared.NextDouble() * 0.20;
        return Min(capped + TimeSpan.FromMilliseconds(capped.TotalMilliseconds * jitterRatio), _policy.MaxBackoff);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        RetryableStatuses.Contains(statusCode);

    private static TimeSpan Min(TimeSpan left, TimeSpan right) =>
        left <= right ? left : right;

    private static async Task<HttpRequestMessage> CloneForRetryAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (KeyValuePair<string, object?> option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
