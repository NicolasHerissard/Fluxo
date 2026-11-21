using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

/// <summary>Page « Mes données » : solde de départ, export CSV, export et import complets chiffrés, remise à zéro.</summary>
public partial class DataSettingsViewModel(
    IFinanceDataStore dataStore,
    IExportService export,
    IDataBackupService backup,
    IAppLockService appLock,
    IFinancialProfileRepository profileRepository,
    ISubscriptionRepository subscriptionRepository,
    SubscriptionReminderScheduler reminders,
    ISettingsService settings,
    IDailyCheckScheduler dailyCheck) : StatusViewModelBase
{
    [ObservableProperty] private string initialBalance = string.Empty;
    [ObservableProperty] private string backupPassword = string.Empty;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    public async Task LoadAsync() =>
        InitialBalance = MoneyFormatter.PlainAmount(await profileRepository.GetInitialBalanceAsync());

    [RelayCommand]
    private async Task SaveInitialBalanceAsync()
    {
        if (!AmountParser.TryParse(InitialBalance, out var amount))
        {
            ShowError("Indiquez un solde initial valide.");
            return;
        }
        try
        {
            await profileRepository.SetInitialBalanceAsync(amount);
            InitialBalance = MoneyFormatter.PlainAmount(amount);
            StatusMessage = "Solde initial enregistré.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Describe(ex, "Enregistrement impossible."));
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            await export.ExportExpensesCsvAsync();
            StatusMessage = "Export CSV prêt à être partagé.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Describe(ex, "Export impossible."));
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        try
        {
            IsBusy = true;
            await backup.ExportEncryptedAsync(BackupPassword);
            StatusMessage = "Export complet prêt à être partagé.";
        }
        catch (Exception ex)
        {
            ShowError(Describe(ex, "Export impossible."));
        }
        finally { IsBusy = false; }
    }

    public async Task RestoreBackupAsync()
    {
        try
        {
            IsBusy = true;
            if (!await backup.ImportEncryptedAsync(BackupPassword)) return;
            StatusMessage = "Données et réglages importés.";
            await RefreshRemindersAsync();
            // L'autorisation de notifier se demande au premier plan, comme à l'activation du réglage.
            if (settings.BackgroundAlertsEnabled) await dailyCheck.EnsureScheduledAsync();
        }
        catch (Exception ex)
        {
            ShowError(Describe(ex, "Import impossible."));
        }
        finally { IsBusy = false; }
    }

    public async Task DeleteAllDataAsync(string pin)
    {
        try
        {
            if (!await appLock.VerifyPinAsync(pin))
            {
                ShowError(await PinLockMessages.AfterFailureAsync(appLock, "Code PIN incorrect. Suppression annulée."));
                return;
            }
            IsBusy = true;
            await dataStore.DeleteAllAsync();
            StatusMessage = "Toutes les données financières ont été supprimées.";
            await RefreshRemindersAsync();
        }
        catch (Exception ex)
        {
            ShowError(Describe(ex, "Suppression impossible."));
        }
        finally { IsBusy = false; }
    }

    // Les données ont été remplacées ou effacées : les alarmes de rappel doivent suivre.
    private async Task RefreshRemindersAsync()
    {
        try { await reminders.RefreshAsync(await subscriptionRepository.GetAllAsync()); }
        catch (Exception ex) { StatusMessage = $"{StatusMessage} Rappels non mis à jour : {Describe(ex, "erreur inattendue.")}"; }
    }
}
