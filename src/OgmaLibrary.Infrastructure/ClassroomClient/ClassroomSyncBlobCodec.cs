using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>Brotli + AES-256-GCM codec for opt-in private classroom sync blobs.</summary>
internal sealed class ClassroomSyncBlobCodec : IClassroomSyncBlobCodec
{
    internal const int CurrentVersion = 1;
    internal const string BlobContentType = "application/vnd.ogma.classroom-sync+binary";

    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int MaxBlobBytes = 5 * 1024 * 1024;
    private const int MaxPlaintextBytes = 20 * 1024 * 1024;

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OGMASYNC");
    private static readonly byte[] KdfInfo = Encoding.UTF8.GetBytes("Ogma.Library.Classroom.SyncBlob.v1");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EncryptedClassroomSyncBlob Encode(
        ClassroomSyncSnapshot snapshot,
        string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        byte[] compressed = Compress(plaintext);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceLength);
        byte[] tag = new byte[TagLength];
        byte[] ciphertext = new byte[compressed.Length];
        byte[] key = DeriveKey(sessionToken, salt);

        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(nonce, compressed, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(compressed);
        }

        byte[] content = new byte[Magic.Length + 1 + SaltLength + NonceLength + TagLength + ciphertext.Length];
        int offset = 0;
        Write(Magic, content, ref offset);
        content[offset++] = CurrentVersion;
        Write(salt, content, ref offset);
        Write(nonce, content, ref offset);
        Write(tag, content, ref offset);
        Write(ciphertext, content, ref offset);

        return new EncryptedClassroomSyncBlob(CurrentVersion, BlobContentType, content);
    }

    public ClassroomSyncSnapshot Decode(
        EncryptedClassroomSyncBlob blob,
        string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        if (blob.Version != CurrentVersion ||
            !BlobContentType.Equals(blob.ContentType, StringComparison.Ordinal))
        {
            throw new NotSupportedException("Unsupported classroom sync blob format.");
        }

        ReadOnlySpan<byte> content = blob.Content;
        if (content.Length > MaxBlobBytes)
        {
            throw new CryptographicException("Classroom sync blob exceeds the permitted size.");
        }

        int minimumLength = Magic.Length + 1 + SaltLength + NonceLength + TagLength;
        if (content.Length <= minimumLength || !content[..Magic.Length].SequenceEqual(Magic))
        {
            throw new CryptographicException("Classroom sync blob header is invalid.");
        }

        int offset = Magic.Length;
        byte version = content[offset++];
        if (version != CurrentVersion)
        {
            throw new NotSupportedException("Unsupported classroom sync blob version.");
        }

        ReadOnlySpan<byte> salt = content.Slice(offset, SaltLength);
        offset += SaltLength;
        ReadOnlySpan<byte> nonce = content.Slice(offset, NonceLength);
        offset += NonceLength;
        ReadOnlySpan<byte> tag = content.Slice(offset, TagLength);
        offset += TagLength;
        ReadOnlySpan<byte> ciphertext = content[offset..];
        byte[] compressed = new byte[ciphertext.Length];
        byte[] key = DeriveKey(sessionToken, salt);

        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, ciphertext, tag, compressed);
            byte[] plaintext = Decompress(compressed);
            try
            {
                return JsonSerializer.Deserialize<ClassroomSyncSnapshot>(plaintext, JsonOptions) ??
                    throw new CryptographicException("Classroom sync blob snapshot was empty.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(compressed);
        }
    }

    private static byte[] Compress(byte[] plaintext)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize))
        {
            brotli.Write(plaintext);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        byte[] buffer = new byte[81920];
        try
        {
            int read;
            while ((read = brotli.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxPlaintextBytes)
                {
                    throw new CryptographicException("Classroom sync blob expands beyond the permitted size.");
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }

        return output.ToArray();
    }

    private static byte[] DeriveKey(string sessionToken, ReadOnlySpan<byte> salt)
    {
        byte[] tokenBytes = Encoding.UTF8.GetBytes(sessionToken);
        byte[] saltBytes = salt.ToArray();
        byte[]? prk = null;
        byte[]? material = null;
        try
        {
            prk = HMACSHA256.HashData(saltBytes, tokenBytes);
            material = new byte[KdfInfo.Length + 1];
            KdfInfo.CopyTo(material, 0);
            material[^1] = 1;
            using var hmac = new HMACSHA256(prk);
            return hmac.ComputeHash(material)[..32];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tokenBytes);
            CryptographicOperations.ZeroMemory(saltBytes);
            if (prk is not null)
            {
                CryptographicOperations.ZeroMemory(prk);
            }

            if (material is not null)
            {
                CryptographicOperations.ZeroMemory(material);
            }
        }
    }

    private static void Write(byte[] source, byte[] destination, ref int offset)
    {
        source.CopyTo(destination, offset);
        offset += source.Length;
    }
}
