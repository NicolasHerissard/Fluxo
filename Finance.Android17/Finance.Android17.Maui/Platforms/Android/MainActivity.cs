using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Finance.Android17.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		// Soldes et dépenses restent hors de l'aperçu des applications récentes, des captures et de la recopie d'écran.
		Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
		SystemBarAppearance.Apply();
	}
}
