using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// The secret a signed-in browser keeps and sends as <c>Authorization: Bearer &lt;token&gt;</c> (docs/ACCOUNTS.md): 32 random bytes as
/// base64url without padding (43 characters). Only its <see cref="Hash"/> is stored: SHA-256 of the token text (ASCII), lowercase hex.
/// </summary>
public static class DeviceTokens
{
    public const int TokenBytes = 32;
    public const int TokenLength = 43;

    private static readonly SearchValues<char> TokenAlphabet =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    /// <summary>A new token, to hand to the browser once, and the hash to store.</summary>
    public static (string Token, string Hash) Create()
    {
        var token = ToBase64Url(RandomNumberGenerator.GetBytes(TokenBytes));
        return (token, Hash(token));
    }

    /// <summary>What is stored for <paramref name="token"/>, and looked up with it.</summary>
    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }

    /// <summary>A cheap shape check (length and characters), to turn garbage away before hashing and looking it up.</summary>
    public static bool LooksLikeToken([NotNullWhen(true)] string? candidate)
        => candidate is { Length: TokenLength } && candidate.AsSpan().IndexOfAnyExcept(TokenAlphabet) < 0;

    private static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
