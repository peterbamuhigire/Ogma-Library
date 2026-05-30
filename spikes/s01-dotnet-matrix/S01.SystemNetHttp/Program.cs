// Spike 1 – System.Net.Http (built-in) reference probe
using System.Net.Http;
Console.WriteLine($"System.Net.Http: {typeof(HttpClient).Assembly.GetName().Version}");
