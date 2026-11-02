using Finance.Android17.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Android17.Maui;

/// <summary>
/// En-tête commun des pages : surtitre en petites capitales, titre, sous-titre, et la cloche de notifications. Le point de la
/// cloche s'allume quand le centre de notifications contient une alerte pas encore consultée ; il est recalculé chaque fois
/// que la page s'affiche. La cloche ouvre <see cref="NotificationsPage"/>.
/// </summary>
public partial class PageHeader : ContentView
{
    public static readonly BindableProperty EyebrowProperty = BindableProperty.Create(
        nameof(Eyebrow), typeof(string), typeof(PageHeader), string.Empty,
        propertyChanged: (b, _, n) => ((PageHeader)b).SetText(((PageHeader)b).EyebrowLabel, (string?)n));

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(PageHeader), string.Empty,
        propertyChanged: (b, _, n) => ((PageHeader)b).TitleLabel.Text = (string?)n);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(PageHeader), string.Empty,
        propertyChanged: (b, _, n) => ((PageHeader)b).SetText(((PageHeader)b).SubtitleLabel, (string?)n));

    public static readonly BindableProperty ShowLockProperty = BindableProperty.Create(
        nameof(ShowLock), typeof(bool), typeof(PageHeader), false,
        propertyChanged: (b, _, n) => ((PageHeader)b).LockButton.IsVisible = (bool)n);

    private NotificationsViewModel? _notifications;
    private Page? _page;

    public PageHeader() => InitializeComponent();

    public string Eyebrow { get => (string)GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <summary>Ajoute le bouton qui verrouille l'application, à gauche de la cloche.</summary>
    public bool ShowLock { get => (bool)GetValue(ShowLockProperty); set => SetValue(ShowLockProperty, value); }

    private void SetText(Label label, string? text)
    {
        label.Text = text;
        label.IsVisible = !string.IsNullOrWhiteSpace(text);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (_page is not null) _page.Appearing -= OnPageAppearing;
        _page = null;
        if (Handler?.MauiContext?.Services is not { } services) return;

        _notifications ??= services.GetService<NotificationsViewModel>();
        _page = FindPage();
        if (_page is not null) _page.Appearing += OnPageAppearing;
        _ = RefreshBadgeAsync();
    }

    private Page? FindPage()
    {
        Element? element = this;
        while (element is not null and not Page) element = element.Parent;
        return element as Page;
    }

    private void OnPageAppearing(object? sender, EventArgs e) => _ = RefreshBadgeAsync();

    private async Task RefreshBadgeAsync()
    {
        if (_notifications is null) return;
        try
        {
            await _notifications.LoadAsync();
            BellButton.HasBadge = _notifications.HasUnread;
        }
        catch
        {
            // Le point de la cloche est un confort : une base pas encore prête ne doit pas empêcher d'afficher la page.
            BellButton.HasBadge = false;
        }
    }

    private async void OnBellClicked(object? sender, EventArgs e)
    {
        if (_page is null || Handler?.MauiContext?.Services is not { } services) return;

        var panel = services.GetRequiredService<NotificationsPage>();
        panel.Disappearing += OnPanelDisappearing;
        BellButton.HasBadge = false;
        await _page.Navigation.PushModalAsync(new NavigationPage(panel));
    }

    private void OnPanelDisappearing(object? sender, EventArgs e)
    {
        if (sender is Page panel) panel.Disappearing -= OnPanelDisappearing;
        _ = RefreshBadgeAsync();
    }

    private void OnLockClicked(object? sender, EventArgs e) => (Microsoft.Maui.Controls.Application.Current as App)?.ShowLock();
}
