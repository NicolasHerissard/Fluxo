using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Finance.Android17.Application;
using Finance.Android17.Tests.Support;

namespace Finance.Android17.Tests.Backup;

public sealed class EncryptedBackupServiceTests : IDisposable
{
    // Fichier .mfbackup produit hors de l'application (Python : PBKDF2-SHA256 200 000 itérations, AES-256-GCM,
    // AAD « MFBAK01 », sel 01..10, nonce 65..70) avec le mot de passe « correct horse battery ». Si ce test casse,
    // les sauvegardes déjà exportées par les utilisateurs ne sont plus lisibles.
    private const string GoldenPassword = "correct horse battery";
    private const string GoldenPayload =
        "TUZCQUswMQECAwQFBgcICQoLDA0ODxBlZmdoaWprbG1ub3DUOXb00NTeyLb3ghQjrrW/k6+jjDeaeT/DiSEemwaG4j2QSWyZmPBL0E1RAb1S5ttJwRMONWhcdkMTRr8CL/xHA58xN7CTB14QOTCwDBoqr7vziChxgQEQSJmMsFsjNXPdzKyceiteqoAC/glflQqhkrFHpQYN9tUCWYl1f6d0v9CLib5YlAZTnI7/isflT7862hhsC0QaW94NXoNRnHIWpp8RyKTWDutL93+8Nn3i6NiIDI5xZr+4FTserEmYRw5viCcD3ZYeHjRF/x362e/b97l2bger4KO+sKSOIr5aYlZTmd/JolOHqZggKjoX6IpnGEYHjHxSQ+sYFeMkjF2JRrw5Q9rTro88EUzrWxKjbEEpChZY3ue+E0DP1ZdGejd+LMjIxEw4Wz1Mlq7eDtBUHKmM1vgB4poE5nEA5OvWlobs3/5m7KjicBpWCKOBOJK/hLtIBbBCzpwkottO9f7t17RxYqdVxK5du8yUUcrxA/JNAw8V4xfvVcmDf/rpj5P4fsHOzHp+5RlZsbCUjbx10vgD7TdC89QglKV2L6Cm8Lql3aDbWMMJmuPckbGTO6IibGiVjStgkIZVOOyQx6eU2KnFdr/+UokXKvhOOQt2mMtB7Z6EvIkK8pSLk0/DD9S0MBT3ay+eLhRlt7a/mIt+mzkbmmi4YDIzlGKdGKJ7KdEnqt8T4CTkahGgfr+nwYaGztEeq6BeyzizvC5wBLHZ8kbN1H/uQwogmCw/fKsCEqYIn7UxE5iR7uvDl7qncXfa";

    private readonly InfrastructureHost _host = new();

    public void Dispose() => _host.Dispose();

    private IDataBackupService Backup => _host.Get<IDataBackupService>();
    private IFinanceStore Store => _host.Get<IFinanceStore>();

    /// <summary>Lecteur du format, réécrit ici d'après sa spécification : MFBAK01 | sel 16 | nonce 12 | tag 16 | données.</summary>
    private static byte[] Open(byte[] payload, string password)
    {
        Assert.Equal("MFBAK01", Encoding.ASCII.GetString(payload, 0, 7));
        var salt = payload[7..23];
        var nonce = payload[23..35];
        var tag = payload[35..51];
        var cipher = payload[51..];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain, "MFBAK01"u8);
        return plain;
    }

    private async Task SeedAsync()
    {
        var food = (await Store.GetCategoriesAsync()).Single(x => x.Name == "Alimentation");
        await Store.UpdateCategoryAsync(food.Id, food.Name, food.Color, 20_000);
        await Store.AddExpenseAsync(1_250, "Pain", new DateTime(2026, 10, 2), food.Id);
        await Store.AddIncomeAsync(200_000, "Salaire", new DateTime(2026, 10, 1));
        await Store.SetInitialBalanceAsync(50_000);
    }

    [Fact]
    public async Task Export_SharesAnEncryptedFile_NamedAfterTheClock()
    {
        await SeedAsync();

        await Backup.ExportEncryptedAsync("motdepasse-solide");

        var file = Assert.Single(_host.Files.Shared);
        Assert.Equal("mes-finances-20261020-0930.mfbackup", file.FileName);
        Assert.Equal("Sauvegarde chiffrée Mes Finances", file.Title);
        Assert.Equal("MFBAK01", Encoding.ASCII.GetString(file.Content, 0, 7));
        Assert.DoesNotContain("Pain", Encoding.UTF8.GetString(file.Content));
    }

    [Fact]
    public async Task Export_ProducesTheDocumentedFormat()
    {
        await SeedAsync();

        await Backup.ExportEncryptedAsync("motdepasse-solide");

        var plain = Open(_host.Shared().Content, "motdepasse-solide");
        var snapshot = JsonSerializer.Deserialize<FinanceBackupSnapshot>(plain)!;
        Assert.Equal(2, snapshot.FormatVersion);
        Assert.Equal("Pain", Assert.Single(snapshot.Expenses).Description);
        Assert.Equal(200_000, Assert.Single(snapshot.Incomes!).AmountMinor);
        Assert.Equal(50_000, snapshot.InitialBalanceMinor);
        Assert.Equal(20_000, snapshot.Categories.Single(x => x.Name == "Alimentation").BudgetMinor);
    }

    [Fact]
    public async Task Export_UsesANewSaltAndNonceEveryTime()
    {
        await Backup.ExportEncryptedAsync("motdepasse-solide");
        await Backup.ExportEncryptedAsync("motdepasse-solide");

        var (first, second) = (_host.Files.Shared[0].Content, _host.Files.Shared[1].Content);
        Assert.NotEqual(first[7..23], second[7..23]);
        Assert.NotEqual(first[23..35], second[23..35]);
    }

    [Fact]
    public async Task Import_ReadsABackupProducedByAnotherImplementation()
    {
        await Store.AddExpenseAsync(999, "Sera effacée", new DateTime(2026, 10, 3), (await Store.GetCategoriesAsync()).First().Id);
        _host.Files.ToPick = Convert.FromBase64String(GoldenPayload);

        var imported = await Backup.ImportEncryptedAsync(GoldenPassword);

        Assert.True(imported);
        var category = Assert.Single(await Store.GetCategoriesAsync());
        Assert.Equal("Alimentation", category.Name);
        Assert.Equal(20_000, category.BudgetMinor);
        var expense = Assert.Single(await Store.GetExpensesAsync());
        Assert.Equal("Pain", expense.Description);
        Assert.Equal(1_250, expense.AmountMinor);
        Assert.Equal(50_000, await Store.GetInitialBalanceAsync());
    }

    [Fact]
    public async Task ExportThenImport_RoundTripsEverythingIntoAnotherDatabase()
    {
        await SeedAsync();
        await Backup.ExportEncryptedAsync("motdepasse-solide");

        using var other = new InfrastructureHost();
        other.Files.ToPick = _host.Shared().Content;
        Assert.True(await other.Get<IDataBackupService>().ImportEncryptedAsync("motdepasse-solide"));

        var store = other.Get<IFinanceStore>();
        Assert.Equal("Pain", Assert.Single(await store.GetExpensesAsync()).Description);
        Assert.Equal("Salaire", Assert.Single(await store.GetIncomesAsync()).Description);
        Assert.Equal(50_000, await store.GetInitialBalanceAsync());
        Assert.Equal(20_000, (await store.GetCategoriesAsync()).Single(x => x.Name == "Alimentation").BudgetMinor);
    }

    [Fact]
    public async Task Import_WithTheWrongPassword_IsRefused_AndLeavesDataAlone()
    {
        await SeedAsync();
        _host.Files.ToPick = Convert.FromBase64String(GoldenPayload);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ImportEncryptedAsync("un-autre-mot-de-passe"));

        Assert.Equal("Mot de passe incorrect ou sauvegarde endommagée.", error.Message);
        Assert.Equal("Pain", Assert.Single(await Store.GetExpensesAsync()).Description);
    }

    [Fact]
    public async Task Import_OfATamperedFile_IsRefused()
    {
        var payload = Convert.FromBase64String(GoldenPayload);
        payload[^1] ^= 0xFF;
        _host.Files.ToPick = payload;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ImportEncryptedAsync(GoldenPassword));

        Assert.Equal("Mot de passe incorrect ou sauvegarde endommagée.", error.Message);
    }

    [Theory]
    [InlineData("pas un fichier de sauvegarde, mais assez long pour passer la longueur minimale de l'en-tête")]
    [InlineData("MFBAK01 trop court")]
    [InlineData("")]
    public async Task Import_OfAnotherFile_IsRefused(string content)
    {
        _host.Files.ToPick = Encoding.UTF8.GetBytes(content);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ImportEncryptedAsync(GoldenPassword));

        Assert.Equal("Ce fichier n’est pas une sauvegarde Mes Finances valide.", error.Message);
    }

    [Fact]
    public async Task Import_WhenTheUserCancelsThePicker_DoesNothing()
    {
        await SeedAsync();
        _host.Files.ToPick = null;

        Assert.False(await Backup.ImportEncryptedAsync(GoldenPassword));
        Assert.Single(await Store.GetExpensesAsync());
    }

    [Fact]
    public async Task Import_OfNonJsonContent_ReportsAnUnreadableBackup()
    {
        // Contenu correctement chiffré mais qui n'est pas un instantané JSON.
        await Backup.ExportEncryptedAsync("motdepasse-solide");
        var good = _host.Shared().Content;
        var salt = good[7..23];
        var nonce = good[23..35];
        var key = Rfc2898DeriveBytes.Pbkdf2("motdepasse-solide", salt, 200_000, HashAlgorithmName.SHA256, 32);
        var plain = "ceci n'est pas du JSON"u8.ToArray();
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag, "MFBAK01"u8);
        _host.Files.ToPick = [.. good[..35], .. tag, .. cipher];

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ImportEncryptedAsync("motdepasse-solide"));

        Assert.Equal("Sauvegarde vide ou illisible.", error.Message);
    }

    [Fact]
    public async Task Passwords_ShorterThanEightCharacters_AreRefused_ForBothDirections()
    {
        var export = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ExportEncryptedAsync("1234567"));
        var import = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ImportEncryptedAsync("1234567"));

        Assert.Equal("Le mot de passe de sauvegarde doit contenir au moins 8 caractères.", export.Message);
        Assert.Equal(export.Message, import.Message);
        Assert.Empty(_host.Files.Shared);
    }
}

internal static class InfrastructureHostBackupExtensions
{
    public static SharedFile Shared(this InfrastructureHost host) => host.Files.Shared.Single();
}
