using System.Security.Cryptography;

namespace LastMile.Shipments.Core;

/// <summary>
/// Public tracking numbers (decision S3): <c>LM-</c> + 12 Crockford base32 characters ≈ 60 random bits, so they cannot
/// be guessed or enumerated. No I, L, O or U, to avoid confusion when read aloud or typed.
/// </summary>
public static class TrackingNumber
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const string Prefix = "LM-";
    private const int Length = 12;

    public static string New()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return Prefix + new string(chars);
    }

    /// <summary>Lenient input: trims and upper-cases what users paste (e.g. "lm-7k3f…").</summary>
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
