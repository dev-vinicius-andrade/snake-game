using System.Security.Cryptography;
using System.Text;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// What is kept of a social account: not its id, but a keyed hash of it (HMAC SHA-256 with a secret only the server has). The hash is enough to
/// recognise the player next time, and a leaked accounts file cannot be turned back into provider ids, not even by trying every id.
/// </summary>
public static class LoginIdentifier
{
    /// <summary>64 lowercase hex characters. The provider's case and surrounding spaces in the provider and the id do not matter.</summary>
    public static string Hash(string provider, string subject, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var message = Encoding.UTF8.GetBytes($"{provider.Trim().ToLowerInvariant()}:{subject.Trim()}");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), message)).ToLowerInvariant();
    }
}
