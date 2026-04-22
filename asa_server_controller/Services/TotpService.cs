using System.Security.Cryptography;
using System.Text;

namespace asa_server_controller.Services;

public sealed class TotpService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(20);
        return ToBase32(bytes);
    }

    public string BuildOtpAuthUri(string issuer, string accountName, string secret)
    {
        string encodedIssuer = Uri.EscapeDataString(issuer);
        string encodedAccount = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool ValidateCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string normalizedCode = new(code.Where(char.IsDigit).ToArray());
        if (normalizedCode.Length != 6)
        {
            return false;
        }

        byte[] key = FromBase32(secret);
        long currentCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        for (long counter = currentCounter - 1; counter <= currentCounter + 1; counter++)
        {
            if (GenerateCode(key, counter) == normalizedCode)
            {
                return true;
            }
        }

        return false;
    }

    private static string GenerateCode(byte[] key, long counter)
    {
        byte[] counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        using HMACSHA1 hmac = new(key);
        byte[] hash = hmac.ComputeHash(counterBytes);
        int offset = hash[^1] & 0x0F;
        int binary =
            ((hash[offset] & 0x7F) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];

        int otp = binary % 1_000_000;
        return otp.ToString("D6");
    }

    private static string ToBase32(byte[] data)
    {
        StringBuilder builder = new();
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                builder.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            builder.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return builder.ToString();
    }

    private static byte[] FromBase32(string input)
    {
        string normalized = input.Trim().TrimEnd('=').ToUpperInvariant();
        List<byte> bytes = [];
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char value in normalized)
        {
            int index = Base32Alphabet.IndexOf(value);
            if (index < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;

            if (bitsLeft < 8)
            {
                continue;
            }

            bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
            bitsLeft -= 8;
        }

        return [.. bytes];
    }
}
