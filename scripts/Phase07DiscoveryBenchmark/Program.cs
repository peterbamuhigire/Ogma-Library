using System.Diagnostics;
using System.Threading.Channels;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Ingestion;

const int fileCount = 50_000;
string root = Path.Combine(Path.GetTempPath(), $"ogma-phase07-benchmark-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);

try
{
    for (int index = 0; index < fileCount; index++)
    {
        File.WriteAllBytes(Path.Combine(root, $"book-{index:D5}.pdf"), []);
    }

    var channel = Channel.CreateBounded<DiscoveredFile>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait });
    var stopwatch = Stopwatch.StartNew();
    Task discovery = new PdfDiscoveryService().DiscoverAsync(
        root,
        [],
        channel.Writer,
        cancellationToken: CancellationToken.None);
    int observed = 0;
    await foreach (DiscoveredFile _ in channel.Reader.ReadAllAsync().ConfigureAwait(false))
    {
        observed++;
    }

    await discovery.ConfigureAwait(false);
    stopwatch.Stop();
    Console.WriteLine($"{{\"files\":{observed},\"elapsedMilliseconds\":{stopwatch.ElapsedMilliseconds},\"channelCapacity\":500}}");
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}
