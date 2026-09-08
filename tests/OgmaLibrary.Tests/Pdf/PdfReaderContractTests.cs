using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Tests.Pdf;

/// <summary>Contract tests for the PDF capability and render-policy boundary.</summary>
public sealed class PdfReaderContractTests
{
    [Fact]
    public void RenderRequest_CacheFingerprintIncludesVisualPolicy()
    {
        RenderRequest baseline = new(1200);
        RenderRequest withAnnotations = baseline with
        {
            AnnotationMode = PdfAnnotationRenderMode.AppearanceOnly,
        };
        RenderRequest withRotation = baseline with { RotationDegrees = 90 };

        Assert.NotEqual(baseline.CacheFingerprint, withAnnotations.CacheFingerprint);
        Assert.NotEqual(baseline.CacheFingerprint, withRotation.CacheFingerprint);
    }

    [Theory]
    [InlineData(0, 595, 842, 595, 842)]
    [InlineData(90, 595, 842, 842, 595)]
    [InlineData(180, 595, 842, 595, 842)]
    [InlineData(270, 595, 842, 842, 595)]
    public void PageGeometry_DisplayDimensionsApplyRotation(
        int rotation,
        double width,
        double height,
        double expectedDisplayWidth,
        double expectedDisplayHeight)
    {
        var geometry = new PdfPageGeometry(3, width, height, rotation);

        Assert.Equal(expectedDisplayWidth, geometry.DisplayWidthPoints);
        Assert.Equal(expectedDisplayHeight, geometry.DisplayHeightPoints);
    }

    [Fact]
    public void CapabilityProfile_RefusesActiveContentByDefault()
    {
        PdfCapabilityProfile profile = PdfCapabilityProfile.Current;

        Assert.Equal(
            PdfFeatureSupportStatus.Refused,
            profile.Features["javascript-and-launch-actions"]);
        Assert.Equal(
            PdfFeatureSupportStatus.Refused,
            profile.Features["forms"]);
    }
}
