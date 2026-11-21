using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Finance.Android17.Presentation;

/// <summary>
/// Annulation des suppressions : chaque suppression est empilée et reste annulable, la dernière d'abord. Une suppression
/// qui suit une autre à moins de <see cref="UndoLifetime"/> n'empêche donc plus d'annuler la précédente. Sans nouvelle
/// suppression ni annulation pendant ce délai, la proposition disparaît.
/// </summary>
public abstract partial class UndoableViewModelBase : StatusViewModelBase
{
    private readonly Stack<(string Message, Func<Task> Restore)> _undoStack = new();
    private int _undoVersion;

    [ObservableProperty] private bool isUndoVisible;
    [ObservableProperty] private string undoMessage = string.Empty;

    protected virtual TimeSpan UndoLifetime => TimeSpan.FromSeconds(8);

    /// <summary>Recharge les données affichées une fois une suppression annulée.</summary>
    protected abstract Task ReloadAfterUndoAsync();

    protected void OfferUndo(string message, Func<Task> restore)
    {
        _undoStack.Push((message, restore));
        ShowLatestUndo();
    }

    private void ShowLatestUndo()
    {
        if (_undoStack.TryPeek(out var latest))
        {
            UndoMessage = latest.Message;
            IsUndoVisible = true;
            _ = HideUndoLaterAsync(++_undoVersion);
        }
        else
        {
            _undoVersion++;
            IsUndoVisible = false;
        }
    }

    private async Task HideUndoLaterAsync(int version)
    {
        await Task.Delay(UndoLifetime).ConfigureAwait(true);
        if (version != _undoVersion) return;
        _undoStack.Clear();
        IsUndoVisible = false;
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        if (!_undoStack.TryPop(out var undo)) return;
        ShowLatestUndo();
        try
        {
            await undo.Restore();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowError(Describe(ex, "Annulation impossible."));
            return;
        }
        await ReloadAfterUndoAsync();
    }
}
