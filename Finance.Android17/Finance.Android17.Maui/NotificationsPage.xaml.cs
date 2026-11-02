using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

/// <summary>Centre de notifications ouvert par la cloche de l'en-tête. La liste vue est marquée lue à la fermeture : le point de la cloche disparaît.</summary>
public partial class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel _viewModel;

    public NotificationsPage(NotificationsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SystemBarAppearance.Apply();
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        _viewModel.MarkAllReadCommand.Execute(null);
        base.OnDisappearing();
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
