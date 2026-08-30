using System.Security.Cryptography;

namespace OgmaLibrary.Infrastructure.Catalogue;

internal static class CanonicalIdGenerator
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string NewId()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<char> buffer = stackalloc char[26];
        for (int index = 9; index >= 0; index--)
        {
            buffer[index] = CrockfordAlphabet[(int)(timestamp & 0x1F)];
            timestamp >>= 5;
        }

        Span<byte> random = stackalloc byte[10];
        RandomNumberGenerator.Fill(random);
        int bitBuffer = 0;
        int bitCount = 0;
        int randomIndex = 0;
        for (int index = 10; index < 26; index++)
        {
            if (bitCount < 5)
            {
                bitBuffer = (bitBuffer << 8) | random[randomIndex++];
                bitCount += 8;
            }

            buffer[index] = CrockfordAlphabet[(bitBuffer >> (bitCount - 5)) & 0x1F];
            bitCount -= 5;
        }

        return new string(buffer);
    }
}
