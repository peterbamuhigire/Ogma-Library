using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.LanHost;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>Phase 16 LAN Host certificate provisioning tests.</summary>
public sealed class LanHostCertificateProvisionerTests
{
    [Fact]
    public async Task CertificateProvisioner_GeneratesValidX509Root()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provisioner = new LocalCertificateProvisioner(dataDirectory);

            using X509Certificate2 certificate = await provisioner.LoadOrCreateRootCertificateAsync();
            CertificateProvisioningResult result = await provisioner.EnsureProvisionedAsync();

            Assert.Equal(3, certificate.Version);
            Assert.Equal(certificate.SubjectName.Name, certificate.IssuerName.Name);
            Assert.True(certificate.HasPrivateKey);
            Assert.True(certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddYears(2));
            Assert.Equal(64, result.Fingerprint.Length);
            Assert.Equal(LocalCertificateProvisioner.Fingerprint(certificate), result.Fingerprint);
            Assert.Contains(
                certificate.Extensions.OfType<X509BasicConstraintsExtension>(),
                extension => extension.CertificateAuthority);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task CertificateProvisioner_ServerCertificate_HasLoopbackSanAndPrivateKey()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provisioner = new LocalCertificateProvisioner(dataDirectory);

            using X509Certificate2 certificate = await provisioner.LoadOrCreateCertificateAsync(IPAddress.Loopback);
            (IReadOnlyList<string> dnsNames, IReadOnlyList<IPAddress> ipAddresses) =
                ReadSubjectAlternativeNames(certificate);

            Assert.True(certificate.HasPrivateKey);
            Assert.Contains("CN=localhost", certificate.Subject, StringComparison.Ordinal);
            Assert.Contains("localhost", dnsNames);
            Assert.Contains(IPAddress.Loopback, ipAddresses);
            Assert.Contains(
                certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>(),
                extension => extension.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.1"));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task CertificateProvisioner_ServerCertificate_IncludesSelectedLanAddressSan()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provisioner = new LocalCertificateProvisioner(dataDirectory);
            var bindAddress = IPAddress.Parse("192.168.10.25");

            using X509Certificate2 certificate = await provisioner.LoadOrCreateCertificateAsync(bindAddress);
            (IReadOnlyList<string> dnsNames, IReadOnlyList<IPAddress> ipAddresses) =
                ReadSubjectAlternativeNames(certificate);

            Assert.Contains("localhost", dnsNames);
            Assert.Contains(IPAddress.Loopback, ipAddresses);
            Assert.Contains(bindAddress, ipAddresses);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task CertificateProvisioner_FingerprintIsStableAcrossLoads()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var firstProvisioner = new LocalCertificateProvisioner(dataDirectory);
            CertificateProvisioningResult first = await firstProvisioner.EnsureProvisionedAsync();

            var secondProvisioner = new LocalCertificateProvisioner(dataDirectory);
            CertificateProvisioningResult second = await secondProvisioner.EnsureProvisionedAsync();

            Assert.Equal(first.Fingerprint, second.Fingerprint);
            Assert.Equal(first.NotAfterUtc, second.NotAfterUtc);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task CertificateProvisioner_DoesNotWritePrivateKeyToCatalogueDatabase()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var provisioner = new LocalCertificateProvisioner(dataDirectory);
            CertificateProvisioningResult result = await provisioner.EnsureProvisionedAsync();
            string databasePath = Path.Combine(dataDirectory, "catalogue.db");
            await File.WriteAllTextAsync(databasePath, "catalogue placeholder");

            string databaseText = await File.ReadAllTextAsync(databasePath);

            Assert.DoesNotContain(result.Fingerprint, databaseText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-lanhost-cert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static (IReadOnlyList<string> DnsNames, IReadOnlyList<IPAddress> IpAddresses) ReadSubjectAlternativeNames(
        X509Certificate2 certificate)
    {
        X509Extension extension = certificate.Extensions
            .Cast<X509Extension>()
            .Single(item => item.Oid?.Value == "2.5.29.17");
        var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER);
        AsnReader sequence = reader.ReadSequence();
        var dnsNames = new List<string>();
        var ipAddresses = new List<IPAddress>();

        while (sequence.HasData)
        {
            Asn1Tag tag = sequence.PeekTag();
            if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 2)
            {
                dnsNames.Add(sequence.ReadCharacterString(UniversalTagNumber.IA5String, tag));
                continue;
            }

            if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 7)
            {
                ipAddresses.Add(new IPAddress(sequence.ReadOctetString(tag)));
                continue;
            }

            sequence.ReadEncodedValue();
        }

        sequence.ThrowIfNotEmpty();
        reader.ThrowIfNotEmpty();
        return (dnsNames, ipAddresses);
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
