using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Finance.Android17.Application;

namespace Finance.Android17.Maui;

public sealed class EncryptedBackupService(FinanceService finance) : IDataBackupService
{
    private static readonly byte[] Magic = "MFBAK01"u8.ToArray();

    public async Task ExportEncryptedAsync(string password, CancellationToken cancellationToken = default)
    {
        ValidatePassword(password);
        var snapshot = await finance.CreateBackupSnapshotAsync(cancellationToken);
        var plain = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, tag.Length)) aes.Encrypt(nonce, plain, cipher, tag, Magic);
        CryptographicOperations.ZeroMemory(key);
        var payload = Magic.Concat(salt).Concat(nonce).Concat(tag).Concat(cipher).ToArray();
        var path = Path.Combine(FileSystem.CacheDirectory, $"mes-finances-{DateTime.Now:yyyyMMdd-HHmm}.mfbackup");
        await File.WriteAllBytesAsync(path, payload, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest("Sauvegarde chiffrée Mes Finances", new ShareFile(path)));
    }

    public async Task<bool> ImportEncryptedAsync(string password, CancellationToken cancellationToken = default)
    {
        ValidatePassword(password);
        var selected = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choisir une sauvegarde Mes Finances" });
        if (selected is null) return false;
        await using var source = await selected.OpenReadAsync();
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        var payload = memory.ToArray();
        if (payload.Length < 51 || !payload.AsSpan(0, Magic.Length).SequenceEqual(Magic)) throw new InvalidOperationException("Ce fichier n’est pas une sauvegarde Mes Finances valide.");
        var salt = payload.AsSpan(7, 16).ToArray();
        var nonce = payload.AsSpan(23, 12).ToArray();
        var tag = payload.AsSpan(35, 16).ToArray();
        var cipher = payload.AsSpan(51).ToArray();
        var plain = new byte[cipher.Length];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32);
        try
        {
            using (var aes = new AesGcm(key, tag.Length)) aes.Decrypt(nonce, cipher, tag, plain, Magic);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("Mot de passe incorrect ou sauvegarde endommagée.");
        }
        finally { CryptographicOperations.ZeroMemory(key); }
        var snapshot = JsonSerializer.Deserialize<FinanceBackupSnapshot>(plain) ?? throw new InvalidOperationException("Sauvegarde vide ou illisible.");
        await finance.RestoreBackupSnapshotAsync(snapshot, cancellationToken);
        return true;
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8) throw new InvalidOperationException("Le mot de passe de sauvegarde doit contenir au moins 8 caractères.");
    }
}
