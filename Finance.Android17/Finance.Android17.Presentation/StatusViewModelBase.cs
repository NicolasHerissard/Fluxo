using CommunityToolkit.Mvvm.ComponentModel;

namespace Finance.Android17.Presentation;

/// <summary>Message d'état affiché sous un formulaire, avec sa variante d'erreur : commun aux ViewModels de saisie.</summary>
public abstract partial class StatusViewModelBase : ObservableObject
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText))] private string statusMessage = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText))] private bool statusIsError;

    /// <summary>Message affiché : une erreur est préfixée pour ne pas dépendre de la couleur seule.</summary>
    public string StatusText => StatusIsError && StatusMessage.Length > 0 ? $"Erreur : {StatusMessage}" : StatusMessage;

    // Un nouveau message informatif efface l'état d'erreur ; ShowError le repose juste après.
    partial void OnStatusMessageChanged(string value) => StatusIsError = false;

    /// <summary>
    /// Demande de confirmation fournie par la page (boîte de dialogue : titre, message, puis vrai si l'utilisateur confirme).
    /// Sans elle, la suppression se fait directement.
    /// </summary>
    public Func<string, string, Task<bool>>? ConfirmDeleteAsync { get; set; }

    protected async Task<bool> ConfirmDeletionAsync(string title, string message) =>
        ConfirmDeleteAsync is null || await ConfirmDeleteAsync(title, message);

    protected void ShowError(string message)
    {
        StatusMessage = message;
        StatusIsError = true;
    }

    /// <summary>Message à montrer à l'utilisateur pour une exception : les erreurs attendues (<see cref="InvalidOperationException"/>) sont écrites en français pour lui, les autres restent génériques.</summary>
    protected static string Describe(Exception exception, string fallback) =>
        exception is InvalidOperationException or ArgumentException ? exception.Message : fallback;
}
