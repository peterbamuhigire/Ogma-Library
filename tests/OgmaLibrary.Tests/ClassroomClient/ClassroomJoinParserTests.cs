using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 Host join payload parser tests.</summary>
public sealed class ClassroomJoinParserTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void JoinParser_ParsesPhase16OgmaLanJoinUri()
    {
        var parser = new ClassroomJoinParser();
        string payload =
            "ogma-lan://127.0.0.1:7473/join?name=Ogma%20Test%20Host" +
            $"&fp={Fingerprint}&code=ABCD2345&auth=enrollment-code";

        ClassroomJoinRequest request = parser.Parse(payload);

        Assert.Equal("127.0.0.1", request.Address);
        Assert.Equal(7473, request.Port);
        Assert.Equal(Fingerprint, request.CertificateFingerprint);
        Assert.Equal("Ogma Test Host", request.DisplayName);
        Assert.Equal("ABCD2345", request.EnrollmentCode);
        Assert.Equal("enrollment-code", request.AuthMethod);
    }

    [Fact]
    public void JoinParser_ParsesLegacyPlanUriWithAddrParameter()
    {
        var parser = new ClassroomJoinParser();
        string payload = $"ogma://host?addr=192.168.1.13:7473&fp={Fingerprint}";

        ClassroomJoinRequest request = parser.Parse(payload);

        Assert.Equal("192.168.1.13", request.Address);
        Assert.Equal(7473, request.Port);
        Assert.Equal(Fingerprint, request.CertificateFingerprint);
        Assert.Null(request.DisplayName);
        Assert.Null(request.EnrollmentCode);
    }

    [Fact]
    public void JoinParser_NormalizesChunkedFingerprint()
    {
        var parser = new ClassroomJoinParser();
        string chunked = string.Join(':', Enumerable.Range(0, 32).Select(index => $"{index % 16:x2}"));
        string payload = $"ogma-lan://library.local:7473/join?fp={chunked}";

        ClassroomJoinRequest request = parser.Parse(payload);

        Assert.Equal("library.local", request.Address);
        Assert.Equal(chunked.Replace(":", string.Empty), request.CertificateFingerprint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://127.0.0.1:7473/join?fp=0123")]
    [InlineData("ogma-lan://127.0.0.1:7473/not-join?fp=0123")]
    [InlineData("ogma-lan://127.0.0.1:0/join?fp=0123")]
    [InlineData("ogma-lan://127.0.0.1:7473/join?fp=not-a-fingerprint")]
    [InlineData("ogma://host?fp=0123")]
    public void JoinParser_RejectsMalformedPayloads(string payload)
    {
        var parser = new ClassroomJoinParser();

        bool ok = parser.TryParse(payload, out ClassroomJoinRequest? request, out string? errorMessage);

        Assert.False(ok);
        Assert.Null(request);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    [Fact]
    public void JoinParser_IsRegisteredInClassroomClientServices()
    {
        IClassroomJoinParser parser = new ServiceCollection()
            .AddClassroomClientServices(Path.Combine(Path.GetTempPath(), $"ogma-classroom-parser-{Guid.NewGuid():N}"))
            .BuildServiceProvider()
            .GetRequiredService<IClassroomJoinParser>();

        ClassroomJoinRequest request = parser.Parse($"ogma-lan://127.0.0.1:7473/join?fp={Fingerprint}");

        Assert.Equal(7473, request.Port);
    }
}
