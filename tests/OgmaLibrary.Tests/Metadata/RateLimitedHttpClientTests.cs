using System.Net;
using OgmaLibrary.Infrastructure.Metadata;

namespace OgmaLibrary.Tests.Metadata;

/// <summary>Phase 15 rate-limited metadata HTTP client tests.</summary>
public sealed class RateLimitedHttpClientTests
{
    [Fact]
    public async Task RateLimitedHttpClient_429_Triggers_Backoff_ThenReturnsSuccess()
    {
        var delays = new List<TimeSpan>();
        var policy = new MetadataProviderRateLimitPolicy(
            "TestProvider",
            MinInterval: TimeSpan.Zero,
            MaxRetries: 5,
            BaseBackoff: TimeSpan.FromSeconds(2),
            MaxBackoff: TimeSpan.FromSeconds(60));
        var inner = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2)) },
            },
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new RateLimitedHttpClientHandler(policy, RecordDelayAsync)
        {
            InnerHandler = inner,
        })
        {
            BaseAddress = new Uri("https://metadata.test/"),
        };

        using HttpResponseMessage response = await client.GetAsync("books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
        Assert.Contains(TimeSpan.FromSeconds(2), delays);

        Task RecordDelayAsync(TimeSpan delay, CancellationToken _)
        {
            delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RateLimitedHttpClient_503_StopsAfterMaxRetries()
    {
        var policy = new MetadataProviderRateLimitPolicy(
            "TestProvider",
            MinInterval: TimeSpan.Zero,
            MaxRetries: 2,
            BaseBackoff: TimeSpan.FromMilliseconds(1),
            MaxBackoff: TimeSpan.FromMilliseconds(10));
        var inner = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(new RateLimitedHttpClientHandler(
            policy,
            static (_, _) => Task.CompletedTask)
        {
            InnerHandler = inner,
        });

        using HttpResponseMessage response = await client.GetAsync("https://metadata.test/books");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task RateLimitedHttpClient_SequentialRequests_RespectProviderSpacing()
    {
        var delays = new List<TimeSpan>();
        var policy = new MetadataProviderRateLimitPolicy(
            "TestProvider",
            MinInterval: TimeSpan.FromSeconds(1),
            MaxRetries: 0,
            BaseBackoff: TimeSpan.FromMilliseconds(1),
            MaxBackoff: TimeSpan.FromMilliseconds(1));
        var inner = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(new RateLimitedHttpClientHandler(policy, RecordDelayAsync)
        {
            InnerHandler = inner,
        })
        {
            BaseAddress = new Uri("https://metadata.test/"),
        };

        using HttpResponseMessage first = await client.GetAsync("first");
        using HttpResponseMessage second = await client.GetAsync("second");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(delays, delay => delay >= TimeSpan.FromMilliseconds(900));

        Task RecordDelayAsync(TimeSpan delay, CancellationToken _)
        {
            delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
