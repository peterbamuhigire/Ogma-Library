using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Creates and loads the local LAN Host certificate authority.</summary>
internal sealed class LocalCertificateProvisioner : ICertificateProvisioner, IHostServerCertificateProvider
{
    private const string CertificateDirectoryName = "LanHost";
    private const string ProtectedCertificateFileName = "host-ca.pfx.dpapi";
    private const string FallbackCertificateFileName = "host-ca.pfx";
    private const int RsaKeySizeBits = 3072;
    private readonly string _certificateDirectory;

    [ActivatorUtilitiesConstructor]
    public LocalCertificateProvisioner()
        : this(CatalogueServiceExtensions.GetDefaultDataDirectory())
    {
    }

    internal LocalCertificateProvisioner(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _certificateDirectory = Path.Combine(dataDirectory, CertificateDirectoryName);
    }

    /// <inheritdoc />
    public async Task<CertificateProvisioningResult> EnsureProvisionedAsync(
        CancellationToken cancellationToken = default)
    {
        using X509Certificate2 certificate = await LoadOrCreateRootCertificateAsync(cancellationToken)
            .ConfigureAwait(false);
        return new CertificateProvisioningResult(Fingerprint(certificate), certificate.NotAfter.ToUniversalTime());
    }

    public async Task<X509Certificate2> LoadOrCreateCertificateAsync(CancellationToken cancellationToken = default)
    {
        using X509Certificate2 root = await LoadOrCreateRootCertificateAsync(cancellationToken)
            .ConfigureAwait(false);
        return CreateServerCertificate(root);
    }

    internal async Task<X509Certificate2> LoadOrCreateRootCertificateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_certificateDirectory);

        string protectedPath = Path.Combine(_certificateDirectory, ProtectedCertificateFileName);
        if (OperatingSystem.IsWindows() && File.Exists(protectedPath))
        {
            byte[] protectedBytes = await File.ReadAllBytesAsync(protectedPath, cancellationToken)
                .ConfigureAwait(false);
            byte[] pfxBytes = WindowsDpapi.Unprotect(protectedBytes);
            return LoadFromPfx(pfxBytes);
        }

        string fallbackPath = Path.Combine(_certificateDirectory, FallbackCertificateFileName);
        if (File.Exists(fallbackPath))
        {
            byte[] pfxBytes = await File.ReadAllBytesAsync(fallbackPath, cancellationToken)
                .ConfigureAwait(false);
            return LoadFromPfx(pfxBytes);
        }

        using X509Certificate2 created = CreateRootCertificate();
        byte[] export = created.Export(X509ContentType.Pkcs12);

        if (OperatingSystem.IsWindows())
        {
            byte[] protectedBytes = WindowsDpapi.Protect(export);
            await File.WriteAllBytesAsync(protectedPath, protectedBytes, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await File.WriteAllBytesAsync(fallbackPath, export, cancellationToken)
                .ConfigureAwait(false);
            RestrictUnixFile(fallbackPath);
        }

        return LoadFromPfx(export);
    }

    internal static string Fingerprint(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return Convert.ToHexStringLower(SHA256.HashData(certificate.RawData));
    }

    private static X509Certificate2 CreateRootCertificate()
    {
        using RSA rsa = RSA.Create(RsaKeySizeBits);
        var subject = new X500DistinguishedName("CN=Ogma Library LAN Host CA");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
            critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        DateTimeOffset notAfter = notBefore.AddYears(5);
        using X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter);
        byte[] pfxBytes = certificate.Export(X509ContentType.Pkcs12);
        return LoadFromPfx(pfxBytes);
    }

    private static X509Certificate2 CreateServerCertificate(X509Certificate2 root)
    {
        using RSA rsa = RSA.Create(2048);
        var subject = new X500DistinguishedName("CN=localhost");
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection
            {
                new("1.3.6.1.5.5.7.3.1"),
            },
            critical: false));
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        DateTimeOffset notAfter = Min(root.NotAfter.ToUniversalTime().AddDays(-1), notBefore.AddYears(1));
        byte[] serial = RandomNumberGenerator.GetBytes(16);
        using X509Certificate2 certificate = request.Create(root, notBefore, notAfter, serial);
        using X509Certificate2 withPrivateKey = certificate.CopyWithPrivateKey(rsa);
        byte[] pfxBytes = withPrivateKey.Export(X509ContentType.Pkcs12);
        return LoadFromPfx(pfxBytes);
    }

    private static X509Certificate2 LoadFromPfx(byte[] pfxBytes)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(
                pfxBytes,
                password: null,
                X509KeyStorageFlags.Exportable);
        }
        finally
        {
            Array.Clear(pfxBytes);
        }
    }

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) =>
        first <= second ? first : second;

    private static void RestrictUnixFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException)
        {
            // Best effort: macOS Keychain integration is tracked separately in Phase 16.
        }
    }

    private static class WindowsDpapi
    {
        private const int CryptProtectUiForbidden = 0x1;

        internal static byte[] Protect(byte[] clearBytes) =>
            Transform(clearBytes, protect: true);

        internal static byte[] Unprotect(byte[] protectedBytes) =>
            Transform(protectedBytes, protect: false);

        private static byte[] Transform(byte[] input, bool protect)
        {
            var inputBlob = DataBlob.FromBytes(input);
            try
            {
                IntPtr description = IntPtr.Zero;
                bool ok = protect
                    ? CryptProtectData(
                        ref inputBlob,
                        "Ogma Library LAN Host CA",
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out DataBlob outputBlob)
                    : CryptUnprotectData(
                        ref inputBlob,
                        out description,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob);

                if (!ok)
                {
                    throw new CryptographicException(Marshal.GetLastWin32Error());
                }

                try
                {
                    return outputBlob.ToArrayAndFree();
                }
                finally
                {
                    if (description != IntPtr.Zero)
                    {
                        LocalFree(description);
                    }
                }
            }
            finally
            {
                inputBlob.Free();
            }
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            out IntPtr dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Length;
            public IntPtr Data;

            public static DataBlob FromBytes(byte[] bytes)
            {
                var blob = new DataBlob
                {
                    Length = bytes.Length,
                    Data = Marshal.AllocHGlobal(bytes.Length),
                };
                Marshal.Copy(bytes, 0, blob.Data, bytes.Length);
                return blob;
            }

            public readonly byte[] ToArrayAndFree()
            {
                byte[] bytes = new byte[Length];
                Marshal.Copy(Data, bytes, 0, Length);
                LocalFree(Data);
                return bytes;
            }

            public readonly void Free()
            {
                if (Data != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Data);
                }
            }
        }
    }
}
