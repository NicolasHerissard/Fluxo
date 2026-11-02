namespace Finance.Android17.Maui;

internal static class SystemBarAppearance
{
    public static void Apply()
    {
#if ANDROID
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(Apply);
            return;
        }

        if (Platform.CurrentActivity?.Window is not Android.Views.Window window)
            return;

        var isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
        var background = Android.Graphics.Color.ParseColor(isDark ? "#0B0E1A" : "#F6F5FF");

        // Android 15+ dessine les barres en transparence : la couleur du décor
        // devient alors leur fond. Les anciennes versions acceptent encore une
        // couleur de barre explicite.
        window.DecorView.SetBackgroundColor(background);
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetStatusBarColor(background);
            window.SetNavigationBarColor(background);
        }

        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = !isDark;
            controller.AppearanceLightNavigationBars = !isDark;
        }
        window.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
#endif
    }
}
