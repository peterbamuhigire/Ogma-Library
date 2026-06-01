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
    public async Task CertificateProvisioner_UsesInjectedHostCaStoreWithoutFallbackPfx()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var store = new InMemoryHostCaStore();
            var firstProvisioner = new LocalCertificateProvisioner(dataDirectory, store);
            CertificateProvisioningResult first = await firstProvisioner.EnsureProvisionedAsync();

            var secondProvisioner = new LocalCertificateProvisioner(dataDirectory, store);
            CertificateProvisioningResult second = await secondProvisioner.EnsureProvisionedAsync();

            Assert.Equal(first.Fingerprint, second.Fingerprint);
            Assert.Equal(1, store.SaveCount);
            Assert.False(File.Exists(Path.Combine(dataDirectory, "LanHost", "host-ca.pfx")));
            Assert.False(File.Exists(Path.Combine(dataDirectory, "LanHost", "host-ca.pfx.dpapi")));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task MacOsKeychainHostCaStore_SavesAndLoadsBase64GenericPassword()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            var tool = new FakeMacOsSecurityTool();
            var store = new MacOsKeychainHostCaStore(tool, Path.Combine(dataDirectory, "host-ca.pfx"));
            byte[] pfxBytes = [1, 2, 3, 4, 5];

            await store.SaveAsync(pfxBytes, CancellationToken.None);
            byte[]? loaded = await store.LoadAsync(CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(pfxBytes, loaded);
            Assert.Contains(tool.Commands, command => command[0] == "add-generic-password");
            Assert.Contains(tool.Commands, command => command[0] == "find-generic-password");
            Assert.All(tool.Commands, command =>
            {
                Assert.Contains("-s", command);
                Assert.Contains(MacOsKeychainHostCaStore.ServiceName, command);
            });
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void MacOsKeychainHostCaStore_AccountName_IsScopedToCertificateDirectory()
    {
        string first = MacOsKeychainHostCaStore.CreateAccountName(Path.Combine("one", "LanHost"));
        string second = MacOsKeychainHostCaStore.CreateAccountName(Path.Combine("two", "LanHost"));

        Assert.StartsWith(Environment.UserName + ":", first, StringComparison.Ordinal);
        Assert.StartsWith(Environment.UserName + ":", second, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task MacOsKeychainHostCaStore_MigratesLegacyFallbackPfx()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            string fallbackPath = Path.Combine(dataDirectory, "host-ca.pfx");
            byte[] pfxBytes = [6, 7, 8, 9];
            await File.WriteAllBytesAsync(fallbackPath, pfxBytes);
            var tool = new FakeMacOsSecurityTool();
            var store = new MacOsKeychainHostCaStore(tool, fallbackPath);

            byte[]? loaded = await store.LoadAsync(CancellationToken.None);
            byte[]? reloaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(pfxBytes, loaded);
            Assert.Equal(pfxBytes, reloaded);
            Assert.False(File.Exists(fallbackPath));
            Assert.Contains(tool.Commands, command => command[0] == "add-generic-password");
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

    private sealed class InMemoryHostCaStore : IHostCaStore
    {
        private byte[]? _stored;

        public int SaveCount { get; private set; }

        public Task<byte[]?> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_stored?.ToArray());
        }

        public Task SaveAsync(byte[] pfxBytes, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pfxBytes);
            cancellationToken.ThrowIfCancellationRequested();
            _stored = pfxBytes.ToArray();
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMacOsSecurityTool : IMacOsSecurityTool
    {
        private string? _storedSecret;

        public List<IReadOnlyList<string>> Commands { get; } = [];

        public Task<MacOsSecurityToolResult> RunAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(arguments.ToArray());

            if (arguments.Count > 0 && arguments[0] == "find-generic-password")
            {
                return Task.FromResult(_storedSecret is null
                    ? new MacOsSecurityToolResult(44, string.Empty, "not found")
                    : new MacOsSecurityToolResult(0, _storedSecret + Environment.NewLine, string.Empty));
            }

            if (arguments.Count > 0 && arguments[0] == "add-generic-password")
            {
                int passwordIndex = -1;
                for (int index = 0; index < arguments.Count; index++)
                {
                    if (arguments[index] == "-w")
                    {
                        passwordIndex = index;
                        break;
                    }
                }

                Assert.True(passwordIndex >= 0 && passwordIndex + 1 < arguments.Count);
                _storedSecret = arguments[passwordIndex + 1];
                return Task.FromResult(new MacOsSecurityToolResult(0, string.Empty, string.Empty));
            }

            return Task.FromResult(new MacOsSecurityToolResult(64, string.Empty, "unsupported"));
        }
    }
}
