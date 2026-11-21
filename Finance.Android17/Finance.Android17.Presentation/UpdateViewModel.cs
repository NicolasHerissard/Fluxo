using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;

namespace Finance.Android17.Presentation;

/// <summary>Carte « Mises à jour » des réglages : vérification manuelle, puis installation après confirmation.</summary>
public partial class UpdateViewModel(IAppUpdateService updates) : StatusViewModelBase
{
    private AppUpdateInfo? _available;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanCheck))] private bool isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasUpdate))] private string availableText = string.Empty;
    [ObservableProperty] private double progress;

    public string CurrentVersionText => $"Version installée : {updates.CurrentVersionName} (build {updates.CurrentVersionCode})";
    public bool HasUpdate => _available is not null && !IsBusy;
    public bool CanCheck => !IsBusy;

    /// <summary>Confirmation fournie par la page (titre, message, libellé du bouton de confirmation) ; sans elle, rien ne s'installe.</summary>
    public Func<string, string, string, Task<bool>>? ConfirmInstallAsync { get; set; }

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusMessage = "Recherche d’une mise à jour…";
            _available = await updates.CheckAsync();
            if (_available is null)
            {
                AvailableText = string.Empty;
                StatusMessage = "Fluxo est à jour.";
            }
            else
            {
                AvailableText = $"Nouvelle version disponible : {_available.VersionName}" + SizeSuffix(_available.SizeBytes);
                StatusMessage = string.Empty;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _available = null;
            AvailableText = string.Empty;
            ShowError(Describe(exception, "Impossible de vérifier les mises à jour. Vérifiez votre connexion Internet et réessayez."));
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasUpdate));
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        var update = _available;
        if (update is null || IsBusy) return;
        if (ConfirmInstallAsync is null || !await ConfirmInstallAsync(
                "Installer la mise à jour ?",
                $"{update.VersionName}{SizeSuffix(update.SizeBytes)} sera téléchargée (connexion Internet), puis Android vous demandera de confirmer l’installation. Vos données sont conservées. Fluxo se fermera pendant l’installation : rouvrez-la ensuite.",
                "Installer")) return;

        IsBusy = true;
        Progress = 0;
        try
        {
            StatusMessage = "Téléchargement de la mise à jour…";
            await updates.InstallAsync(update, new Progress<double>(value => Progress = value));
            StatusMessage = "Mise à jour prête : confirmez l’installation dans la fenêtre Android.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowError(Describe(exception, "La mise à jour a échoué. Vos données n’ont pas été modifiées ; réessayez plus tard."));
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasUpdate));
        }
    }

    private static string SizeSuffix(long bytes) => bytes > 0 ? $" ({bytes / 1_048_576.0:0.#} Mo)" : string.Empty;
}
