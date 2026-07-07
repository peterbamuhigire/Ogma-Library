using OgmaLibrary.Workers.Pdf;

if (args.Length > 0 && string.Equals(args[0], "pdf-worker", StringComparison.Ordinal))
{
    return await PdfWorkerCommand.RunAsync(args[1..]).ConfigureAwait(false);
}

Console.Error.WriteLine("OgmaLibrary.Workers is intended to be launched by the Ogma Library host.");
return 2;
