using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _viewModel;
    public event EventHandler? Unlocked;

    public LockPage(LockViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.Unlocked += (_, _) => Unlocked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.InitializeAsync();
    }
}
