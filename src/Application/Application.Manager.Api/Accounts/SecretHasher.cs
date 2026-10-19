using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// Slow salted hashes for secrets such as recovery phrases. The hash describes itself,
/// <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>, so the iterations can be raised later: old hashes still
/// verify, and <see cref="NeedsRehash"/> says when to store a new one. Secrets are hashed as UTF-8 exactly as given: normalize first.
/// </summary>
public sealed class SecretHasher
{
    public const int DefaultIterations = 210_000;
    public const int MaxIterations = 10_000_000;
    private const string Algorithm = "pbkdf2-sha256";
    private const char FieldSeparator = '$';
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public SecretHasher(int iterations = DefaultIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(iterations, MaxIterations);
        Iterations = iterations;
    }

    /// <summary>The iterations new hashes are made with.</summary>
    public int Iterations { get; }

    public string Hash(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(secret, salt, Iterations);
        return string.Join(FieldSeparator, Algorithm, Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>True when <paramref name="secret"/> is the one <paramref name="hash"/> was made from. False, never an exception, for a malformed hash.</summary>
    public bool Verify(string? secret, string? hash)
    {
        if (secret is null || !TryParse(hash, out var stored))
            return false;
        return CryptographicOperations.FixedTimeEquals(Derive(secret, stored.Salt, stored.Iterations), stored.Hash);
    }

    /// <summary>True when <paramref name="hash"/> was made with fewer iterations than <see cref="Iterations"/>, or cannot be read.</summary>
    public bool NeedsRehash(string? hash)
        => !TryParse(hash, out var stored) || stored.Iterations < Iterations;

    private static byte[] Derive(string secret, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, iterations, HashAlgorithmName.SHA256, HashSize);

    private static bool TryParse(string? text, out StoredHash stored)
    {
        stored = default;
        var fields = text?.Split(FieldSeparator) ?? [];
        if (fields.Length != 4 || fields[0] != Algorithm)
            return false;
        if (!TryReadIterations(fields[1], out var iterations))
            return false;
        if (!TryReadBytes(fields[2], SaltSize, out var salt) || !TryReadBytes(fields[3], HashSize, out var hash))
            return false;
        stored = new StoredHash(iterations, salt, hash);
        return true;
    }

    private static bool TryReadIterations(string field, out int iterations)
        => int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out iterations) && iterations is >= 1 and <= MaxIterations;

    private static bool TryReadBytes(string field, int size, out byte[] bytes)
    {
        bytes = new byte[size];
        return Convert.TryFromBase64String(field, bytes, out var written) && written == size;
    }

    private readonly record struct StoredHash(int Iterations, byte[] Salt, byte[] Hash);
}
