using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

public partial class LockViewModel(StartupService startup, IAppLockService appLock, IBiometricService biometrics, ISettingsService settings, IDatabaseRecovery recovery) : ObservableObject
{
    private bool _initialized;
    public event EventHandler? Unlocked;

    [ObservableProperty] private bool isFirstRun;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isPinHidden = true;
    [ObservableProperty] private bool canUseBiometrics;
    [ObservableProperty] private string pin = string.Empty;
    [ObservableProperty] private string pinTitle = "Déverrouillage";
    [ObservableProperty] private string pinHelp = "Saisissez votre code PIN.";
    [ObservableProperty] private string pinDots = "○ ○ ○ ○";
    [ObservableProperty] private string pinVisibilityIcon = "eye";
    [ObservableProperty] private string currencySymbol = "€";
    [ObservableProperty] private string errorMessage = string.Empty;
    /// <summary>Vrai quand la clé de la base est introuvable : la page propose alors, après confirmation, de repartir d'une base neuve.</summary>
    [ObservableProperty] private bool canStartOver;

    partial void OnPinChanged(string value)
    {
        var count = Math.Max(PinPolicy.MinLength, value.Length);
        PinDots = string.Join(" ", Enumerable.Range(0, count).Select(i => i < value.Length ? "●" : "○"));
    }

    partial void OnIsPinHiddenChanged(bool value) => PinVisibilityIcon = value ? "eye" : "eye-off";

    [RelayCommand]
    public async Task InitializeAsync()
    {
        CurrencySymbol = MoneyFormatter.CurrencySymbol(settings.CurrencyCode);
        if (_initialized) return;
        _initialized = true;
        IsFirstRun = !await appLock.HasPinAsync();
        PinTitle = IsFirstRun ? "Créer votre code PIN" : "Bon retour";
        PinHelp = IsFirstRun ? $"Choisissez {PinPolicy.MinLength} à {PinPolicy.MaxLength} chiffres pour protéger vos données." : "Déverrouillez vos finances personnelles.";
        CanUseBiometrics = !IsFirstRun && settings.BiometricsEnabled && biometrics.IsAvailable;
    }

    [RelayCommand]
    private async Task SubmitPinAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;
        if (!PinPolicy.IsValid(Pin))
        {
            ErrorMessage = $"Le PIN doit contenir {PinPolicy.RequirementText}.";
            return;
        }

        IsBusy = true;
        try
        {
            if (IsFirstRun) await appLock.SetPinAsync(Pin);
            else if (!await appLock.VerifyPinAsync(Pin))
            {
                ErrorMessage = await PinLockMessages.AfterFailureAsync(appLock, "Code PIN incorrect. Réessayez.");
                Pin = string.Empty;
                return;
            }
            await startup.PrepareAsync();
            Pin = string.Empty;
            Unlocked?.Invoke(this, EventArgs.Empty);
        }
        catch (DatabaseKeyUnavailableException ex) { ShowKeyLost(ex); }
        catch (Exception ex) { ErrorMessage = $"Ouverture impossible : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UseBiometricsAsync()
    {
        if (!CanUseBiometrics || IsBusy) return;
        IsBusy = true;
        try
        {
            if (await biometrics.AuthenticateAsync("Déverrouiller Fluxo"))
            {
                await startup.PrepareAsync();
                Unlocked?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (DatabaseKeyUnavailableException ex) { ShowKeyLost(ex); }
        catch (Exception ex) { ErrorMessage = $"Biométrie indisponible : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void ShowKeyLost(DatabaseKeyUnavailableException ex)
    {
        ErrorMessage = ex.Message;
        CanStartOver = true;
    }

    /// <summary>À appeler après une confirmation explicite : écarte la base illisible puis ouvre une base neuve.</summary>
    public async Task StartOverAsync()
    {
        if (!CanStartOver || IsBusy) return;
        IsBusy = true;
        try
        {
            await recovery.StartOverAsync();
            await startup.PrepareAsync();
            CanStartOver = false;
            ErrorMessage = string.Empty;
            Pin = string.Empty;
            Unlocked?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { ErrorMessage = $"Réinitialisation impossible : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void TogglePinVisibility() => IsPinHidden = !IsPinHidden;
}
