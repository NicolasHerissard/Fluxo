using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Finance.Android17.Application;
using Finance.Android17.Domain;

namespace Finance.Android17.Presentation;

public partial class SubscriptionsViewModel(ISubscriptionRepository subscriptionRepository, ISettingsService settings, SubscriptionReminderScheduler reminders, TimeProvider clock) : ObservableObject
{
    public ObservableCollection<SubscriptionRow> Subscriptions { get; } = [];
    public IReadOnlyList<string> Frequencies { get; } = BillingFrequency.Options;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string amount = string.Empty;
    [ObservableProperty] private string currencySymbol = "€";
    [ObservableProperty] private string selectedFrequency = BillingFrequency.Monthly;
    [ObservableProperty] private DateTime nextBillingDate = clock.GetLocalNow().Date.AddMonths(1);
    [ObservableProperty] private string monthlyTotal = "0,00 € / mois";
    [ObservableProperty] private string annualTotal = "0,00 € / an";
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string formTitle = "Ajouter un abonnement";
    [ObservableProperty] private string saveButtonText = "Ajouter l’abonnement";
    private IReadOnlyList<Subscription> _loaded = [];
    private Guid? _editingId;

    [RelayCommand]
    public async Task LoadAsync()
    {
        CurrencySymbol = MoneyFormatter.CurrencySymbol(settings.CurrencyCode);
        _loaded = await subscriptionRepository.GetAllAsync();
        Subscriptions.Clear();
        foreach (var item in _loaded)
            Subscriptions.Add(new(item.Id, item.Name, settings.HideAmounts ? MoneyFormatter.Hidden : MoneyFormatter.Money(item.AmountMinor, settings.CurrencyCode),
                BillingFrequency.ToDisplay(item.BillingIntervalMonths), MoneyFormatter.Date(item.NextBillingDate)));
        var monthly = SubscriptionCosts.MonthlyMinor(_loaded);
        MonthlyTotal = settings.HideAmounts ? $"{MoneyFormatter.Hidden} / mois" : $"{MoneyFormatter.Money(monthly, settings.CurrencyCode)} / mois";
        AnnualTotal = settings.HideAmounts ? $"{MoneyFormatter.Hidden} / an" : $"{MoneyFormatter.Money(SubscriptionCosts.AnnualMinor(_loaded), settings.CurrencyCode)} / an";
        IsEmpty = _loaded.Count == 0;
        await reminders.RefreshAsync(_loaded);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Indiquez le nom de l’abonnement."; return; }
        if (!AmountParser.TryParse(Amount, out var minor) || minor <= 0) { ErrorMessage = "Indiquez un montant valide."; return; }
        var interval = BillingFrequency.ToMonths(SelectedFrequency);
        if (_editingId is not Guid editingId)
            await subscriptionRepository.AddAsync(Name, minor, interval, NextBillingDate, settings.CurrencyCode);
        else
            await subscriptionRepository.UpdateAsync(editingId, Name, minor, interval, NextBillingDate, settings.CurrencyCode);
        CancelEdit();
        await LoadAsync();
    }

    [RelayCommand]
    private void Edit(SubscriptionRow item)
    {
        var subscription = _loaded.FirstOrDefault(x => x.Id == item.Id);
        if (subscription is null) return;
        _editingId = subscription.Id;
        Name = subscription.Name;
        Amount = MoneyFormatter.FixedAmount(subscription.AmountMinor);
        SelectedFrequency = BillingFrequency.ToOption(subscription.BillingIntervalMonths);
        NextBillingDate = subscription.NextBillingDate;
        IsEditing = true;
        FormTitle = "Modifier l’abonnement";
        SaveButtonText = "Enregistrer les modifications";
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingId = null;
        Name = Amount = string.Empty;
        SelectedFrequency = BillingFrequency.Monthly;
        NextBillingDate = clock.GetLocalNow().Date.AddMonths(1);
        IsEditing = false;
        FormTitle = "Ajouter un abonnement";
        SaveButtonText = "Ajouter l’abonnement";
        ErrorMessage = string.Empty;
    }

    [ObservableProperty] private bool isUndoVisible;
    [ObservableProperty] private string undoMessage = string.Empty;
    private Func<Task>? _undoAction;
    private int _undoVersion;

    private void OfferUndo(string message, Func<Task> restore)
    {
        _undoAction = restore;
        UndoMessage = message;
        IsUndoVisible = true;
        _ = HideUndoLaterAsync(++_undoVersion);
    }

    private async Task HideUndoLaterAsync(int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(8));
        if (version != _undoVersion) return;
        IsUndoVisible = false;
        _undoAction = null;
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        if (_undoAction is not { } restore) return;
        _undoAction = null;
        _undoVersion++;
        IsUndoVisible = false;
        await restore();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(SubscriptionRow item)
    {
        if (_editingId == item.Id) CancelEdit();
        await subscriptionRepository.DeleteAsync(item.Id);
        OfferUndo("Abonnement supprimé", () => subscriptionRepository.RestoreAsync(item.Id));
        await LoadAsync();
    }
}
