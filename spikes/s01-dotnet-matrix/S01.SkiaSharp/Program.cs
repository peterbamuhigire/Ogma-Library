// Spike 1 – SkiaSharp reference probe
using SkiaSharp;
Console.WriteLine($"SkiaSharp: {typeof(SKCanvas).Assembly.GetName().Version}");
// Quick sanity: create an in-memory surface
using var surface = SKSurface.Create(new SKImageInfo(100, 100));
using var canvas = surface.Canvas;
canvas.Clear(SKColors.Blue);
Console.WriteLine("SkiaSharp surface created successfully.");
