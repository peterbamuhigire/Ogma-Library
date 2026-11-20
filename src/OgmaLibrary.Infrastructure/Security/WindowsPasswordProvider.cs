using System.Runtime.InteropServices;
using System.Text;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.Security;

/// <summary>Windows Credential Manager-backed password store (lookup and opt-in storage only; the dialog is in-app) for protected PDFs.</summary>
public sealed class WindowsPasswordProvider : IPasswordProvider
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    /// <inheritdoc />
    public Task<PasswordResult> GetPasswordAsync(
        PasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string target = PasswordCredentialKey.Create(request.ContentHash);
        char[]? stored = TryReadStoredPassword(target);
        if (stored is not null)
        {
            return Task.FromResult(PasswordResult.Success(stored, wasStored: true));
        }

        return Task.FromResult(PasswordResult.Failed("No stored password was found for this PDF."));
    }

    /// <inheritdoc />
    public Task<bool> StorePasswordAsync(
        PasswordRequest request,
        char[] password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(password);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(WriteCredential(PasswordCredentialKey.Create(request.ContentHash), password));
    }

    /// <inheritdoc />
    public Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        string target = PasswordCredentialKey.Create(request.ContentHash);
        CredDelete(target, CredTypeGeneric, 0);
        return Task.CompletedTask;
    }

    private static char[]? TryReadStoredPassword(string target)
    {
        if (!CredRead(target, CredTypeGeneric, 0, out IntPtr credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize <= 0)
            {
                return null;
            }

            byte[] bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            try
            {
                return Encoding.Unicode.GetString(bytes).TrimEnd('\0').ToCharArray();
            }
            finally
            {
                Array.Clear(bytes);
            }
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    private static bool WriteCredential(string target, char[] password)
    {
        byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
        try
        {
            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = passwordBytes.Length,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName,
            };

            credential.CredentialBlob = Marshal.AllocCoTaskMem(passwordBytes.Length);
            try
            {
                Marshal.Copy(passwordBytes, 0, credential.CredentialBlob, passwordBytes.Length);
                return CredWrite(ref credential, 0);
            }
            finally
            {
                Marshal.FreeCoTaskMem(credential.CredentialBlob);
            }
        }
        finally
        {
            Array.Clear(passwordBytes);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
