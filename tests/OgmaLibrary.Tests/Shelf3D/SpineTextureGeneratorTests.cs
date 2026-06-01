using OgmaLibrary.Infrastructure.Assets;
using SkiaSharp;

namespace OgmaLibrary.Tests.Shelf3D;

/// <summary>Phase 14 spine texture generation tests.</summary>
public sealed class SpineTextureGeneratorTests
{
    [Fact]
    public void SpineTextureGenerator_ProducesValidPng()
    {
        byte[] png = SpineTextureGenerator.GeneratePng(new SpineTextureRequest(
            "01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7",
            "Thinking in Systems",
            "Donella Meadows",
            new SKColor(38, 82, 96)));

        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);
        Assert.Equal(SpineTextureGenerator.Width, bitmap.Width);
        Assert.Equal(SpineTextureGenerator.Height, bitmap.Height);
    }

    [Fact]
    public void SpineTextureGenerator_LongTitle_Truncated()
    {
        byte[] png = SpineTextureGenerator.GeneratePng(new SpineTextureRequest(
            "01J4Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7",
            "A Very Long Introduction To Systems Thinking For Students Who Are Building Their First Research Project",
            "Ogma Library Research Group With A Very Long Author Name",
            new SKColor(120, 70, 45)));

        using SKBitmap bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);
        Assert.Equal(SpineTextureGenerator.Width, bitmap.Width);
        Assert.Equal(SpineTextureGenerator.Height, bitmap.Height);
    }

    [Fact]
    public void SpineTextureGenerator_ContrastColor_IsAdaptive()
    {
        Assert.Equal(SKColors.White, SpineTextureGenerator.ChooseTextColor(new SKColor(20, 20, 20)));
        Assert.Equal(new SKColor(30, 30, 30), SpineTextureGenerator.ChooseTextColor(new SKColor(240, 235, 220)));
    }
}
