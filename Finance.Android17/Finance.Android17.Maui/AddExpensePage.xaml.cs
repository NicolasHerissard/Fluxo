using Finance.Android17.Presentation;

namespace Finance.Android17.Maui;

public partial class AddExpensePage : ContentPage
{
    private readonly AddExpenseViewModel _viewModel;
    private bool _scanRequested;
    public AddExpensePage(AddExpenseViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.Saved += OnSaved;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        SystemBarAppearance.Apply();
        await Task.Delay(150);
        if (_scanRequested) return;
        FocusAmount();
    }

    private void FocusAmount()
    {
        AmountEntry.Focus();
#if ANDROID
        if (AmountEntry.Handler?.PlatformView is Android.Widget.EditText nativeEntry &&
            Platform.CurrentActivity?.Window is Android.Views.Window window)
        {
            window.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
            nativeEntry.Post(() =>
            {
                nativeEntry.RequestFocus();
                AndroidX.Core.View.WindowCompat.GetInsetsController(window, nativeEntry)?
                    .Show(AndroidX.Core.View.WindowInsetsCompat.Type.Ime());
            });
        }
#endif
    }

    private async void OnOtherDateClicked(object? sender, EventArgs e)
    {
        _viewModel.SelectDateCommand.Execute("Autre");
        await Task.Delay(50);
        CustomDatePicker.Focus();
    }

    private async void OnSaved(object? sender, EventArgs e) => await Navigation.PopModalAsync();
    private async void OnCancelClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
    private async void OnScanReceiptClicked(object? sender, EventArgs e) => await ScanReceiptAsync();

    /// <summary>À appeler avant d'afficher la page quand le scan démarre aussitôt : évite d'ouvrir le clavier sous l'appareil photo.</summary>
    public void PrepareForScan() => _scanRequested = true;

    public async Task ScanReceiptAsync()
    {
        try
        {
#if ANDROID
            var imagePath = await CameraCapture.CapturePhotoAsync();
            if (imagePath is null) return;
            try { await _viewModel.ApplyReceiptAsync(imagePath); }
            finally { try { System.IO.File.Delete(imagePath); } catch (IOException) { } }
#else
            var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Choisir une photo de ticket" });
            if (photo is null) return;
            await _viewModel.ApplyReceiptAsync(photo.FullPath);
#endif
        }
        catch (Exception ex) { await DisplayAlertAsync("Scan impossible", ex.Message, "OK"); }
    }
    public Task BeginEditAsync(Guid expenseId) => _viewModel.BeginEditAsync(expenseId);
}
