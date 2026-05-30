using System.Diagnostics;
using System.Net;
using Makaretu.Dns;

// ─── LanHost: Spike 7 – LAN Transport ────────────────────────────────────────
// Minimal Kestrel HTTPS server (dev self-signed cert) serving a 10 MB byte
// payload at /stream, and registering an mDNS/DNS-SD service _ogma._tcp.
// Spike purpose: validate HTTPS-over-LAN + mDNS for ADR-0010.

const int    HttpsPort       = 5443;
const int    PayloadSizeMb   = 10;
const long   PayloadSizeBytes = PayloadSizeMb * 1024L * 1024L;
const string ServiceTypeName = "_ogma._tcp";
const string InstanceName    = "OgmaLibraryLanHost";

// Pre-generate the 10 MB payload once (deterministic: 0x41 'A' * 10 MB)
var payload = new byte[PayloadSizeBytes];
Array.Fill(payload, (byte)0x41);
Console.WriteLine($"[LanHost] 10 MB payload prepared ({PayloadSizeBytes:N0} bytes)");

// ─── mDNS service advertisement ──────────────────────────────────────────────
var mdns = new MulticastService();
var serviceDiscovery = new ServiceDiscovery(mdns);

// ServiceProfile(instanceName, serviceName, port, addresses)
var serviceProfile = new ServiceProfile(
    instanceName:  new DomainName(InstanceName),
    serviceName:   new DomainName(ServiceTypeName + ".local."),
    port:          (ushort)HttpsPort,
    addresses:     new[] { IPAddress.Loopback }
);
serviceProfile.AddProperty("proto", "https");
serviceProfile.AddProperty("port",  HttpsPort.ToString());

mdns.Start();
serviceDiscovery.Advertise(serviceProfile);
Console.WriteLine($"[LanHost] mDNS: advertising {InstanceName}.{ServiceTypeName} on port {HttpsPort}");

// ─── Kestrel HTTPS setup ─────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Listen on loopback; use the ASP.NET Core dev certificate
    kestrel.Listen(IPAddress.Loopback, HttpsPort, listenOptions =>
    {
        listenOptions.UseHttps(); // Uses the dotnet dev-cert (must be trusted)
    });
});

// Suppress default Kestrel HTTPS port from launchSettings
builder.WebHost.UseUrls($"https://localhost:{HttpsPort}");

var app = builder.Build();

// ─── Routes ──────────────────────────────────────────────────────────────────
app.MapGet("/", () => Results.Ok(new
{
    service   = InstanceName,
    protocol  = "https",
    port      = HttpsPort,
    streamUrl = $"https://localhost:{HttpsPort}/stream"
}));

app.MapGet("/stream", async (HttpContext ctx) =>
{
    var sw = Stopwatch.StartNew();
    ctx.Response.ContentType         = "application/octet-stream";
    ctx.Response.ContentLength        = PayloadSizeBytes;
    ctx.Response.Headers["X-Spike"]  = "s07-lan-transport";

    await ctx.Response.Body.WriteAsync(payload, ctx.RequestAborted);
    sw.Stop();

    var throughputMbs = PayloadSizeMb / sw.Elapsed.TotalSeconds;
    Console.WriteLine($"[LanHost] /stream: sent {PayloadSizeMb} MB in {sw.ElapsedMilliseconds} ms" +
                      $" = {throughputMbs:F1} MB/s (server-side write)");
});

app.MapGet("/ready", () => Results.Ok("ready"));

Console.WriteLine($"[LanHost] Listening at https://localhost:{HttpsPort}");
Console.WriteLine("[LanHost] Press Ctrl+C to stop.");

app.Run();
