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

    /// <summary>Écriture du format d'après sa spécification, pour fabriquer une sauvegarde « d'une autre époque » (autre mot de passe).</summary>
    private static byte[] Seal(byte[] plain, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[plain.Length];
        using var aes = new AesGcm(Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32), 16);
        aes.Encrypt(nonce, plain, cipher, tag, "MFBAK01"u8);
        return [.. "MFBAK01"u8, .. salt, .. nonce, .. tag, .. cipher];
    }

    private async Task SeedAsync()
    {
        var food = (await _host.Categories.GetAllAsync()).Single(x => x.Name == "Alimentation");
        await _host.Categories.UpdateAsync(food.Id, food.Name, food.Color, 20_000);
        await _host.Expenses.AddAsync(1_250, "Pain", new DateTime(2026, 10, 2), food.Id);
        await _host.Incomes.AddAsync(200_000, "Salaire", new DateTime(2026, 10, 1));
        await _host.Profile.SetInitialBalanceAsync(50_000);
    }

    [Fact]
    public async Task Export_SharesAnEncryptedFile_NamedAfterTheClock()
    {
        await SeedAsync();

        await Backup.ExportEncryptedAsync("motdepasse-solide");

        var file = Assert.Single(_host.Files.Shared);
        Assert.Equal("mes-finances-20261020-0930.mfbackup", file.FileName);
        Assert.Equal("Sauvegarde chiffrée Fluxo", file.Title);
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
        await _host.Expenses.AddAsync(999, "Sera effacée", new DateTime(2026, 10, 3), (await _host.Categories.GetAllAsync()).First().Id);
        _host.Files.ToPick = Convert.FromBase64String(GoldenPayload);

        var imported = await Backup.ImportEncryptedAsync(GoldenPassword);

        Assert.True(imported);
        var category = Assert.Single(await _host.Categories.GetAllAsync());
        Assert.Equal("Alimentation", category.Name);
        Assert.Equal(20_000, category.BudgetMinor);
        var expense = Assert.Single(await _host.Expenses.GetAllAsync());
        Assert.Equal("Pain", expense.Description);
        Assert.Equal(1_250, expense.AmountMinor);
        Assert.Equal(50_000, await _host.Profile.GetInitialBalanceAsync());
    }

    [Fact]
    public async Task ExportThenImport_RoundTripsEverythingIntoAnotherDatabase()
    {
        await SeedAsync();
        await Backup.ExportEncryptedAsync("motdepasse-solide");

        using var other = new InfrastructureHost();
        other.Files.ToPick = _host.Shared().Content;
        Assert.True(await other.Get<IDataBackupService>().ImportEncryptedAsync("motdepasse-solide"));

        Assert.Equal("Pain", Assert.Single(await other.Expenses.GetAllAsync()).Description);
        Assert.Equal("Salaire", Assert.Single(await other.Incomes.GetAllAsync()).Description);
        Assert.Equal(50_000, await other.Profile.GetInitialBalanceAsync());
        Assert.Equal(20_000, (await other.Categories.GetAllAsync()).Single(x => x.Name == "Alimentation").BudgetMinor);
    }

    [Fact]
    public async Task ExportThenImport_CarriesTheSettingsToo()
    {
        await SeedAsync();
        var food = (await _host.Categories.GetAllAsync()).Single(x => x.Name == "Alimentation");
        var s = _host.Settings;
        s.HideAmounts = true; s.AutoLockMinutes = 10; s.Theme = ThemeOptions.Dark; s.AutoCreateSubscriptionExpenses = true;
        s.AutoCreateRecurringIncomes = false; s.SubscriptionRemindersEnabled = false; s.SubscriptionReminderDays = 7;
        s.MonthlyBudgetMinor = 150_000; s.CurrencyCode = "CHF"; s.DefaultCategoryId = food.Id.ToString();
        s.RememberLastCategory = false; s.AfterSaveBehavior = AfterSaveOptions.AddAnother; s.BackgroundAlertsEnabled = false;
        await Backup.ExportEncryptedAsync("motdepasse-solide");

        using var other = new InfrastructureHost();
        other.Settings.BiometricsEnabled = true;
        other.Settings.SeenNotifications = "deja-vue";
        other.Files.ToPick = _host.Shared().Content;
        Assert.True(await other.Get<IDataBackupService>().ImportEncryptedAsync("motdepasse-solide"));

        var r = other.Settings;
        Assert.True(r.HideAmounts);
        Assert.Equal(10, r.AutoLockMinutes);
        Assert.Equal(ThemeOptions.Dark, r.Theme);
        Assert.True(r.AutoCreateSubscriptionExpenses);
        Assert.False(r.AutoCreateRecurringIncomes);
        Assert.False(r.SubscriptionRemindersEnabled);
        Assert.Equal(7, r.SubscriptionReminderDays);
        Assert.Equal(150_000, r.MonthlyBudgetMinor);
        Assert.Equal("CHF", r.CurrencyCode);
        Assert.Equal(food.Id.ToString(), r.DefaultCategoryId);
        Assert.False(r.RememberLastCategory);
        Assert.Equal(AfterSaveOptions.AddAnother, r.AfterSaveBehavior);
        Assert.False(r.BackgroundAlertsEnabled);
        // Ce qui dépend de l'appareil n'est ni exporté ni écrasé.
        Assert.True(r.BiometricsEnabled);
        Assert.Equal("deja-vue", r.SeenNotifications);
    }

    [Fact]
    public async Task Import_OfABackupWithoutSettings_LeavesTheSettingsAlone()
    {
        _host.Settings.Theme = ThemeOptions.Dark;
        _host.Settings.MonthlyBudgetMinor = 99_000;
        _host.Files.ToPick = Convert.FromBase64String(GoldenPayload);

        Assert.True(await Backup.ImportEncryptedAsync(GoldenPassword));

        Assert.Equal(ThemeOptions.Dark, _host.Settings.Theme);
        Assert.Equal(99_000, _host.Settings.MonthlyBudgetMinor);
    }

    [Fact]
    public async Task Import_IgnoresInvalidSettings_AndAnUnknownDefaultCategory()
    {
        await SeedAsync();
        var snapshot = (await _host.Get<IFinanceDataStore>().CreateSnapshotAsync()) with
        {
            Settings = new SettingsBackup(false, -3, "Rose bonbon", true, true, true, 0, -5, "XXX", Guid.NewGuid().ToString(), true, "Explose", true)
        };
        _host.Settings.Theme = ThemeOptions.Light;
        _host.Settings.LastCategoryId = Guid.NewGuid().ToString();
        _host.Files.ToPick = Seal(JsonSerializer.SerializeToUtf8Bytes(snapshot), "motdepasse-solide");

        Assert.True(await Backup.ImportEncryptedAsync("motdepasse-solide"));

        var s = _host.Settings;
        Assert.Equal(5, s.AutoLockMinutes);
        Assert.Equal(ThemeOptions.Light, s.Theme);
        Assert.Equal(3, s.SubscriptionReminderDays);
        Assert.Equal(0, s.MonthlyBudgetMinor);
        Assert.Equal("EUR", s.CurrencyCode);
        Assert.Equal(AfterSaveOptions.Close, s.AfterSaveBehavior);
        Assert.Equal(string.Empty, s.DefaultCategoryId);
        Assert.Equal(string.Empty, s.LastCategoryId);
    }

    [Fact]
    public async Task Import_WithTheWrongPassword_IsRefused_AndLeavesDataAlone()
    {
        await SeedAsync();
        _host.Files.ToPick = Convert.FromBase64String(GoldenPayload);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ImportEncryptedAsync("un-autre-mot-de-passe"));

        Assert.Equal("Mot de passe incorrect ou sauvegarde endommagée.", error.Message);
        Assert.Equal("Pain", Assert.Single(await _host.Expenses.GetAllAsync()).Description);
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

        Assert.Equal("Ce fichier n’est pas une sauvegarde Fluxo valide.", error.Message);
    }

    [Fact]
    public async Task Import_WhenTheUserCancelsThePicker_DoesNothing()
    {
        await SeedAsync();
        _host.Files.ToPick = null;

        Assert.False(await Backup.ImportEncryptedAsync(GoldenPassword));
        Assert.Single(await _host.Expenses.GetAllAsync());
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
    public async Task Export_RefusesPasswordsShorterThanTwelveCharacters()
    {
        var export = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ExportEncryptedAsync("12345678901"));

        Assert.Equal("Le mot de passe de sauvegarde doit contenir au moins 12 caractères.", export.Message);
        Assert.Empty(_host.Files.Shared);
    }

    [Fact]
    public async Task Import_StillAccepts_BackupsMadeWithTheFormerEightCharacterMinimum()
    {
        await SeedAsync();
        var snapshot = await _host.Get<IFinanceDataStore>().CreateSnapshotAsync();
        _host.Files.ToPick = Seal(JsonSerializer.SerializeToUtf8Bytes(snapshot), "12345678");
        using var other = new InfrastructureHost();
        other.Files.ToPick = _host.Files.ToPick;

        Assert.True(await other.Get<IDataBackupService>().ImportEncryptedAsync("12345678"));

        Assert.Equal("Pain", Assert.Single(await other.Expenses.GetAllAsync()).Description);
    }

    [Fact]
    public async Task Import_RefusesAnEmptyPassword()
    {
        var import = await Assert.ThrowsAsync<InvalidOperationException>(() => Backup.ImportEncryptedAsync(string.Empty));

        Assert.Equal("Saisissez le mot de passe de la sauvegarde.", import.Message);
    }
}

internal static class InfrastructureHostBackupExtensions
{
    public static SharedFile Shared(this InfrastructureHost host) => host.Files.Shared.Single();
}
