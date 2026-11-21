using System.Security.Cryptography;

namespace Finance.Android17.Infrastructure;

/// <summary>
/// Format binaire d'un fichier <c>.mfbackup</c> : <c>MFBAK01</c> | sel (16) | nonce (12) | tag (16) | données chiffrées.
/// AES-256-GCM, avec l'en-tête <c>MFBAK01</c> en données authentifiées ; clé dérivée du mot de passe par
/// PBKDF2-SHA256 (200 000 itérations). Ce format est celui des sauvegardes déjà créées : il ne doit pas changer.
/// </summary>
internal static class BackupCodec
{
    internal static readonly byte[] Magic = "MFBAK01"u8.ToArray();
    internal const int SaltSize = 16;
    internal const int NonceSize = 12;
    internal const int TagSize = 16;
    internal const int Iterations = 200_000;
    /// <summary>Minimum exigé à la création. Il ne s'applique pas à la restauration : les sauvegardes déjà faites avec 8 caractères restent lisibles.</summary>
    internal const int MinimumPasswordLength = 12;
    private const int KeySize = 32;
    private const int HeaderSize = 7 + SaltSize + NonceSize + TagSize;

    public static byte[] Encrypt(byte[] plain, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plain, cipher, tag, Magic);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
        return [.. Magic, .. salt, .. nonce, .. tag, .. cipher];
    }

    public static byte[] Decrypt(byte[] payload, string password)
    {
        if (payload.Length < HeaderSize || !payload.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidOperationException("Ce fichier n’est pas une sauvegarde Fluxo valide.");
        var salt = payload.AsSpan(Magic.Length, SaltSize).ToArray();
        var nonce = payload.AsSpan(Magic.Length + SaltSize, NonceSize).ToArray();
        var tag = payload.AsSpan(Magic.Length + SaltSize + NonceSize, TagSize).ToArray();
        var cipher = payload.AsSpan(HeaderSize).ToArray();
        var plain = new byte[cipher.Length];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, Magic);
            return plain;
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("Mot de passe incorrect ou sauvegarde endommagée.");
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
