// Spike 1 – Avalonia reference probe
// Throwaway: confirms Avalonia resolves on net10.0
using Avalonia;

var version = typeof(AppBuilder).Assembly.GetName().Version;
System.Console.WriteLine($"Avalonia referenced: {version}");
