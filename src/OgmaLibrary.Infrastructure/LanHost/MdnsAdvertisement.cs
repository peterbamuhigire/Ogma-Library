namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Validated mDNS/DNS-SD advertisement payload.</summary>
internal sealed record MdnsAdvertisement(
    string InstanceName,
    string ServiceType,
    int Port,
    IReadOnlyDictionary<string, string> Txt);
