using System.Diagnostics;
using System.Net;
using System.Net.Security;
using Makaretu.Dns;

// ─── LanClient: Spike 7 – LAN Transport ──────────────────────────────────────
// Discovers the _ogma._tcp service via mDNS, connects over HTTPS (trusting the
// dev cert), streams the 10 MB payload, measures discovery time + throughput.
// Falls back to direct localhost connection if mDNS is blocked/unavailable.

const string ServiceType   = "_ogma._tcp";
const int    DefaultPort   = 5443;
const int    DiscoveryTimeoutSeconds = 5;
const int    PayloadSizeMb = 10;
const long   PayloadSizeBytes = PayloadSizeMb * 1024L * 1024L;

Console.WriteLine("=== Ogma Library – Spike 7: LAN Client ===");
Console.WriteLine($"Timestamp: {DateTimeOffset.UtcNow:O}");
Console.WriteLine($"Runtime  : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

// ─── Step 1: mDNS discovery ──────────────────────────────────────────────────
string? discoveredHost = null;
int     discoveredPort = DefaultPort;
bool    mdnsSuccess    = false;

Console.WriteLine($"\n[Client] Starting mDNS discovery for {ServiceType} (timeout {DiscoveryTimeoutSeconds}s)...");

var discoverySw = Stopwatch.StartNew();
var tcs         = new TaskCompletionSource<(string host, int port)>();

try
{
    var mdns             = new MulticastService();
    var serviceDiscovery = new ServiceDiscovery(mdns);

    serviceDiscovery.ServiceInstanceDiscovered += (_, e) =>
    {
        if (e.ServiceInstanceName.ToString().Contains("_ogma._tcp", StringComparison.OrdinalIgnoreCase))
        {
            // Query for the full instance details
            mdns.SendQuery(e.ServiceInstanceName, type: DnsType.SRV);
        }
    };

    mdns.AnswerReceived += (_, e) =>
    {
        foreach (var record in e.Message.Answers)
        {
            if (record is SRVRecord srv && record.Name.ToString()
                    .Contains("_ogma._tcp", StringComparison.OrdinalIgnoreCase))
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(("localhost", srv.Port));
                break;
            }
        }
    };

    mdns.Start();
    serviceDiscovery.QueryAllServices();

    var discoveryTask = Task.WhenAny(
        tcs.Task,
        Task.Delay(TimeSpan.FromSeconds(DiscoveryTimeoutSeconds))
    );

    var completed = await discoveryTask;

    if (completed == tcs.Task && tcs.Task.IsCompletedSuccessfully)
    {
        var (h, p) = tcs.Task.Result;
        discoveredHost = h;
        discoveredPort = p;
        mdnsSuccess    = true;
        discoverySw.Stop();
        Console.WriteLine($"[Client] mDNS discovery SUCCESS: host={discoveredHost} port={discoveredPort}" +
                          $"  discovery_time={discoverySw.ElapsedMilliseconds} ms");
    }
    else
    {
        discoverySw.Stop();
        Console.WriteLine($"[Client] mDNS discovery timed out after {discoverySw.ElapsedMilliseconds} ms" +
                          " – falling back to direct localhost connection");
        Console.WriteLine("[Client] NOTE: mDNS multicast may be blocked on this dev machine (Windows Firewall)." +
                          " Needs validation on real LAN.");
    }

    mdns.Stop();
}
catch (Exception ex)
{
    discoverySw.Stop();
    Console.WriteLine($"[Client] mDNS error: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("[Client] Falling back to direct localhost connection.");
}

// ─── Step 2: HTTPS throughput measurement ────────────────────────────────────
var host = discoveredHost ?? "localhost";
var port = discoveredHost != null ? discoveredPort : DefaultPort;
var url  = $"https://{host}:{port}/stream";

Console.WriteLine($"\n[Client] Connecting to {url}");

// Trust the ASP.NET Core dev certificate (spike-only; not for production)
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
    {
        // Accept the dev self-signed cert (subject contains "localhost" or "ASP.NET")
        var subject = cert?.Subject ?? "";
        return subject.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
               subject.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase) ||
               cert?.Issuer.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase) == true;
    }
};

using var http = new HttpClient(handler)
{
    Timeout = TimeSpan.FromSeconds(30)
};

// Confirm host is ready
bool hostReady = false;
for (int attempt = 1; attempt <= 10; attempt++)
{
    try
    {
        var readyResp = await http.GetAsync($"https://{host}:{port}/ready");
        if (readyResp.IsSuccessStatusCode) { hostReady = true; break; }
    }
    catch { }
    await Task.Delay(500);
    Console.Write($"  [waiting for host... attempt {attempt}]");
}
Console.WriteLine();

if (!hostReady)
{
    Console.WriteLine("[Client] ERROR: Host not reachable. Is LanHost running?");
    Console.WriteLine("         Run LanHost first, then LanClient.");
    Environment.Exit(1);
}

// Stream the 10 MB payload and measure throughput
var streamSw     = Stopwatch.StartNew();
long totalBytes  = 0;
var  buffer      = new byte[65536]; // 64 KB read buffer

using var resp   = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
resp.EnsureSuccessStatusCode();

using var stream = await resp.Content.ReadAsStreamAsync();
int read;
while ((read = await stream.ReadAsync(buffer)) > 0)
    totalBytes += read;

streamSw.Stop();

double elapsedSeconds   = streamSw.Elapsed.TotalSeconds;
double throughputMbs    = totalBytes / 1024.0 / 1024.0 / elapsedSeconds;
double discoveryMs      = mdnsSuccess ? discoverySw.ElapsedMilliseconds : -1;

Console.WriteLine($"\n[Client] ===== RESULTS =====");
Console.WriteLine($"  Bytes received     : {totalBytes:N0}");
Console.WriteLine($"  Elapsed            : {streamSw.ElapsedMilliseconds} ms");
Console.WriteLine($"  Throughput         : {throughputMbs:F2} MB/s");
if (mdnsSuccess)
    Console.WriteLine($"  mDNS discovery     : {discoveryMs:F0} ms  [PASS if <= 5000 ms]");
else
    Console.WriteLine("  mDNS discovery     : NOT MEASURED (timed out / blocked)");

bool throughputPass = throughputMbs >= 5.0;
Console.WriteLine($"  Throughput >= 5 MB/s: {(throughputPass ? "PASS" : "FAIL")}");
if (mdnsSuccess)
    Console.WriteLine($"  Discovery <= 5 s   : {(discoveryMs <= 5000 ? "PASS" : "FAIL")}");

Console.WriteLine($"\n=== Spike 7 client complete ===");
