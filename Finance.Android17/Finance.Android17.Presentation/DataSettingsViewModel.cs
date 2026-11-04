using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

/// <summary>Page « Mes données » : solde de départ, export CSV, sauvegarde chiffrée, restauration et remise à zéro.</summary>
public partial class DataSettingsViewModel(
    IFinanceDataStore dataStore,
    IExportService export,
    IDataBackupService backup,
    IAppLockService appLock,
    IFinancialProfileRepository profileRepository,
    ISubscriptionRepository subscriptionRepository,
    SubscriptionReminderScheduler reminders) : StatusViewModelBase
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
        await profileRepository.SetInitialBalanceAsync(amount);
        InitialBalance = MoneyFormatter.PlainAmount(amount);
        StatusMessage = "Solde initial enregistré.";
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        await export.ExportExpensesCsvAsync();
        StatusMessage = "Export CSV prêt à être partagé.";
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        try
        {
            IsBusy = true;
            await backup.ExportEncryptedAsync(BackupPassword);
            StatusMessage = "Sauvegarde chiffrée prête à être partagée.";
        }
        catch (Exception ex)
        {
            ShowError(Describe(ex, "Sauvegarde impossible."));
        }
        finally { IsBusy = false; }
    }

    public async Task RestoreBackupAsync()
    {
        try
        {
            IsBusy = true;
            if (!await backup.ImportEncryptedAsync(BackupPassword)) return;
            StatusMessage = "Sauvegarde restaurée.";
            await RefreshRemindersAsync();
        }
        catch (Exception ex)
        {
            ShowError(Describe(ex, "Restauration impossible."));
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
