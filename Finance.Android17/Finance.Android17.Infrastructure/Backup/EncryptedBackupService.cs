using System.Text.Json;
using Finance.Android17.Application;

namespace Finance.Android17.Infrastructure;

/// <summary>Sauvegarde chiffrée : instantané JSON de la base, chiffré par <see cref="BackupCodec"/>, partagé comme fichier.</summary>
internal sealed class EncryptedBackupService(IFinanceDataStore data, IFileExchange files, TimeProvider clock) : IDataBackupService
{
    public async Task ExportEncryptedAsync(string password, CancellationToken cancellationToken = default)
    {
        ValidatePassword(password);
        var snapshot = await data.CreateSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var payload = BackupCodec.Encrypt(JsonSerializer.SerializeToUtf8Bytes(snapshot), password);
        var fileName = $"mes-finances-{clock.GetLocalNow():yyyyMMdd-HHmm}.mfbackup";
        await files.ShareAsync(fileName, payload, "Sauvegarde chiffrée Fluxo", cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ImportEncryptedAsync(string password, CancellationToken cancellationToken = default)
    {
        // À l'import, seul un mot de passe non vide est exigé : une sauvegarde faite avec l'ancien minimum de 8 caractères doit rester restaurable.
        if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Saisissez le mot de passe de la sauvegarde.");
        var payload = await files.PickAsync("Choisir une sauvegarde Fluxo", cancellationToken).ConfigureAwait(false);
        if (payload is null) return false;
        var plain = BackupCodec.Decrypt(payload, password);
        var snapshot = Deserialize(plain);
        await data.RestoreSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static FinanceBackupSnapshot Deserialize(byte[] plain)
    {
        try { return JsonSerializer.Deserialize<FinanceBackupSnapshot>(plain) ?? throw new InvalidOperationException("Sauvegarde vide ou illisible."); }
        catch (JsonException) { throw new InvalidOperationException("Sauvegarde vide ou illisible."); }
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < BackupCodec.MinimumPasswordLength)
            throw new InvalidOperationException($"Le mot de passe de sauvegarde doit contenir au moins {BackupCodec.MinimumPasswordLength} caractères.");
    }
}
