using CommunityToolkit.Mvvm.ComponentModel;

namespace Finance.Android17.Presentation;

/// <summary>Message d'état affiché sous un formulaire, avec sa variante d'erreur : commun aux ViewModels de saisie.</summary>
public abstract partial class StatusViewModelBase : ObservableObject
{
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool statusIsError;

    // Un nouveau message informatif efface l'état d'erreur ; ShowError le repose juste après.
    partial void OnStatusMessageChanged(string value) => StatusIsError = false;

    protected void ShowError(string message)
    {
        StatusMessage = message;
        StatusIsError = true;
    }

    /// <summary>Message à montrer à l'utilisateur pour une exception : les erreurs attendues (<see cref="InvalidOperationException"/>) sont écrites en français pour lui, les autres restent génériques.</summary>
    protected static string Describe(Exception exception, string fallback) =>
        exception is InvalidOperationException or ArgumentException ? exception.Message : fallback;
}
